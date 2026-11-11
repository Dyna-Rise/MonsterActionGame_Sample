using System;
using MonsterAction.Combat;
using MonsterAction.Data;
using UnityEngine;
using UnityEngine.Serialization;

namespace MonsterAction.Battle.Player
{
    /// <summary>
    /// Battle シーンのプレイヤー「共通部分」の基底クラス。
    ///
    /// 全タイプ共通で持つもの：
    ///   ・カメラ基準の移動 / 回転 / 重力、ダッシュとスタミナ
    ///   ・回避（無敵時間つき）
    ///   ・被弾（ひるみ・ノックバック）/ 死亡
    ///   ・ターゲットカメラ（ロックオン）… 〇ボタンで切り替え
    ///   ・集中モード … L1 を押している間。カメラが肩越しになり、キャラはカメラの向きを向いて横歩きする
    ///   ・照準：GetAimPoint() / GetAimDirection() で「画面中央が指している場所」が取れる
    ///   ・HP / スタミナとイベント（UI 担当や GameManager が購読する）
    ///
    /// 【Player タイプ担当の人へ】
    /// このクラスを継承して新しいタイプ（大剣、弓、双剣…）を作ります。
    /// 最低限 override するのは次の2つだけです：
    ///   ・TryStartAction()  … 通常移動中に毎フレーム呼ばれる。攻撃開始ならアクション状態に入って true を返す
    ///   ・UpdateAction(dt)  … アクション状態の間、毎フレーム呼ばれる。終わったら ReturnToLocomotion()
    /// 例は PlayerTypeA_SwordShield を見てください。
    ///
    /// 【Boss 担当の人へ】
    /// PlayerBattleController.Active で現在のプレイヤーを取得できます（狙う対象として使ってください）。
    /// プレイヤーへの攻撃は IDamageable.ApplyDamage(DamageInfo) を呼ぶだけです。
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    [RequireComponent(typeof(PlayerInputReader))]
    public abstract class PlayerBattleController : MonoBehaviour, IDamageable
    {
        public enum State
        {
            Locomotion, // 通常移動
            Action,     // 攻撃などタイプ固有の行動（中身はサブクラスが担当）
            Dodge,      // 回避ステップ
            Hit,        // 被弾ひるみ
            Down,       // ダウン（大攻撃で吹き飛んで倒れている）
            UseItem,    // アイテム使用中（回復薬を飲んでいる）
            Dead,       // 死亡
        }

        // ---- Animator のステート名 / パラメータ名（Animator Controller と一致させる） ----
        protected const string AnimStateLocomotion = "Locomotion";
        protected const string AnimStateFocusLocomotion = "FocusLocomotion";
        protected static readonly int AnimParamMoveX = Animator.StringToHash("MoveX");
        protected static readonly int AnimParamMoveY = Animator.StringToHash("MoveY");
        protected const string AnimStateDodge = "Dodge";
        protected const string AnimStateHit = "Hit";
        protected const string AnimStateDeath = "Death";
        /// <summary>ダウン（倒れる）。Animator に無ければ Death ステートで代用</summary>
        protected const string AnimStateKnockDown = "KnockDown";
        /// <summary>起き上がり。Animator に無ければ、そのまま Locomotion へ戻る</summary>
        protected const string AnimStateGetUp = "GetUp";
        /// <summary>アイテム使用。Animator に無ければ、その場で待機</summary>
        protected const string AnimStateUseItem = "UseItem";
        protected static readonly int AnimParamSpeed = Animator.StringToHash("Speed");
        protected static readonly int AnimParamDodgeX = Animator.StringToHash("DodgeX");
        protected static readonly int AnimParamDodgeY = Animator.StringToHash("DodgeY");
        protected static readonly int AnimParamActionSpeed = Animator.StringToHash("ActionSpeed");

        /// <summary>現在シーンにいるプレイヤー（Boss の索敵などで使う）</summary>
        public static PlayerBattleController Active { get; private set; }

        [Header("ステータス")]
        [SerializeField] private float maxHp = 150f;

        [Header("移動")]
        [SerializeField] private float moveSpeed = 5f;
        [Tooltip("1秒あたりの最大旋回角度")]
        [SerializeField] private float turnSpeed = 720f;
        [SerializeField] private float gravity = -25f;

        [Header("ダッシュ / スタミナ")]
        [SerializeField] private float maxStamina = 100f;
        [Tooltip("ダッシュ中の移動速度")]
        [SerializeField] private float sprintSpeed = 8f;
        [Tooltip("ダッシュ中、1秒あたりに減るスタミナ")]
        [SerializeField] private float sprintStaminaPerSecond = 20f;
        [Tooltip("1秒あたりに回復するスタミナ")]
        [SerializeField] private float staminaRegenPerSecond = 30f;
        [Tooltip("スタミナを使ってから回復が始まるまでの秒数")]
        [SerializeField] private float staminaRegenDelay = 0.6f;
        [Tooltip("スタミナ切れ後、この割合まで回復するまでダッシュできない")]
        [SerializeField, Range(0f, 1f)] private float exhaustedRecoverRate = 0.3f;
        [Tooltip("回避1回で使うスタミナ（0 なら消費なし）")]
        [SerializeField] private float dodgeStaminaCost = 0f;

        [Header("回避")]
        [Tooltip("ON：回避方向へ向き直ってから転がる（ローリング）\nOFF：向きはそのままで方向別ステップ")]
        [SerializeField] private bool faceDodgeDirection = true;
        [SerializeField] private float dodgeDistance = 6f;
        [SerializeField] private float dodgeDuration = 0.8f;
        [Tooltip("回避中の速度の変化（横軸：0〜1 の経過割合、縦軸：速さの比率）。合計移動距離は dodgeDistance になるよう自動で調整されます")]
        [SerializeField] private AnimationCurve dodgeSpeedCurve = new AnimationCurve(
            new Keyframe(0f, 1f), new Keyframe(0.6f, 1f), new Keyframe(1f, 0f));
        [Tooltip("無敵時間の開始（回避開始からの秒数）")]
        [SerializeField] private float dodgeInvincibleStart = 0.05f;
        [Tooltip("無敵時間の終了（回避開始からの秒数）")]
        [SerializeField] private float dodgeInvincibleEnd = 0.45f;
        [Tooltip("Mixamo のモーション（回避・ガードなど）を再生している間だけ無効にするコンポーネント。\n例：Kevin Iglesias の SpineProxy（背骨の向きを上書きするため、切らないと上半身が直立したままになる）")]
        [FormerlySerializedAs("disableWhileDodging")]
        [SerializeField] private Behaviour[] disableDuringMixamoMotion = new Behaviour[0];
        [Tooltip("この割合を過ぎたら次の行動（回避・攻撃）を受け付ける")]
        [SerializeField, Range(0f, 1f)] private float dodgeRecoverRate = 0.8f;

        [Header("ターゲットカメラ / 集中モード")]
        [Tooltip("ロックオンできる最大距離")]
        [SerializeField] private float lockOnMaxDistance = 40f;
        [Tooltip("集中モード中の移動速度")]
        [SerializeField] private float focusMoveSpeed = 4.5f;
        [Tooltip("照準のレイの最大距離")]
        [SerializeField] private float aimMaxDistance = 100f;
        [Tooltip("照準のレイが当たるレイヤー")]
        [SerializeField] private LayerMask aimLayers = ~0;

        [Header("被弾")]
        [SerializeField] private float hitStunDuration = 0.6f;
        [Tooltip("被弾後の無敵時間（連続ヒット防止）")]
        [SerializeField] private float hitInvincibleTime = 0.5f;

        [Header("アイテム（△ボタン）")]
        [Tooltip("△で使うアイテム。空なら ID \"potion\"（回復薬）を Resources/Items から探す")]
        [SerializeField] private ItemDefinition quickItem;

        [Header("ダウン（大攻撃）")]
        [Tooltip("倒れている時間（秒）。この間は無敵")]
        [SerializeField] private float downDuration = 1.6f;
        [Tooltip("吹き飛ぶ時間（秒）")]
        [SerializeField] private float downKnockbackTime = 0.4f;
        [Tooltip("ノックバック距離がこれより短い大攻撃でも、最低これだけは吹き飛ぶ（m）")]
        [SerializeField] private float downMinKnockback = 2f;
        [Tooltip("倒れてからこの秒数を過ぎたら、回避ボタンで転がって起き上がれる")]
        [SerializeField] private float downRecoverTime = 0.9f;
        [Tooltip("起き上がりモーション（GetUp ステート）の時間（秒）。GetUp が無ければ使わない")]
        [SerializeField] private float getUpDuration = 0.8f;

        [Header("参照（空なら自動取得）")]
        [SerializeField] private Transform cameraTransform;
        [SerializeField] private Animator animator;

        // ---- サブクラスから使うもの ----
        protected Animator Anim => animator;
        protected CharacterController Controller { get; private set; }
        protected PlayerInputReader Input { get; private set; }
        /// <summary>現在のステートに入ってからの経過秒数</summary>
        protected float StateTime { get; private set; }

        // ---- 外部（UI・Boss・GameManager）に公開するもの ----
        public State CurrentState { get; private set; } = State.Locomotion;
        public float MaxHp => maxHp;
        public float CurrentHp { get; private set; }
        public bool IsAlive => CurrentState != State.Dead;
        public bool IsInvincible => dodgeInvincible || Time.time < invincibleUntil || CurrentState == State.Down;
        public float MaxStamina => maxStamina;
        public float CurrentStamina { get; private set; }
        /// <summary>ダッシュ中か</summary>
        public bool IsSprinting { get; private set; }
        /// <summary>スタミナ切れで疲れている（ダッシュ不可）か</summary>
        public bool IsExhausted { get; private set; }

        /// <summary>ロックオン中のターゲット（いなければ null）</summary>
        public LockOnTarget LockTarget { get; private set; }
        /// <summary>集中モード中か</summary>
        public bool IsFocusing { get; private set; }
        /// <summary>カメラの Transform（照準計算などに使う）</summary>
        public Transform CameraTransform => cameraTransform;

        /// <summary>スタミナが変わった（現在, 最大）</summary>
        public event Action<float, float> StaminaChanged;
        /// <summary>ロックオン対象が変わった（null = 解除）</summary>
        public event Action<LockOnTarget> LockTargetChanged;
        /// <summary>集中モードの ON/OFF が変わった</summary>
        public event Action<bool> FocusChanged;
        /// <summary>ロックオンする相手がいないときに〇ボタン → カメラを背後に戻す合図</summary>
        public event Action CameraResetRequested;

        /// <summary>HP が変わった（現在HP, 最大HP）</summary>
        public event Action<float, float> HealthChanged;
        /// <summary>ダメージを受けた（ガードで減ったあとの値）</summary>
        public event Action<DamageInfo> Damaged;
        /// <summary>攻撃を防いだ（ガードなど）。エフェクトや SE を出したいとき用</summary>
        public event Action<DamageInfo> Guarded;
        /// <summary>HP が回復した（回復量）</summary>
        public event Action<float> Healed;
        /// <summary>アイテムを使った（効果が出た瞬間）</summary>
        public event Action<ItemDefinition> ItemUsed;

        /// <summary>△で使うアイテム（回復薬）</summary>
        public ItemDefinition QuickItem => quickItem;

        /// <summary>サブクラスが「防いだ」ことを知らせる（Damaged より先に呼ばれる）</summary>
        protected void NotifyGuarded(DamageInfo info) => Guarded?.Invoke(info);
        /// <summary>死亡した</summary>
        public event Action Died;

        private Vector3 horizontalVelocity;
        private float verticalVelocity;
        private Vector3 dodgeDirection;
        private bool dodgeInvincible;
        private float invincibleUntil;
        private Vector3 knockbackVelocity;
        private float dodgeCurveArea = 1f;
        private bool actionUsesMixamoMotion;
        private bool mixamoMotionApplied;
        private Vector3 pushVelocity;
        private float pushDeceleration;
        private float lastStaminaUseTime = float.NegativeInfinity;
        private bool focusLocomotionAnim; // Animator が FocusLocomotion 側を再生中か
        private readonly RaycastHit[] aimHits = new RaycastHit[16];

        /// <summary>
        /// 攻撃力アップのスキル倍率。WeaponHitbox はこれを自動で掛けます。
        /// 弓の矢など、WeaponHitbox を使わない攻撃は、ダメージにこれを掛けてください。
        /// </summary>
        public float AttackMultiplier => GameData.GetSkillMultiplier(SkillType.Attack);

        /// <summary>防御力アップのスキル倍率（受けるダメージをこれで割る）</summary>
        public float DefenseMultiplier => GameData.GetSkillMultiplier(SkillType.Defense);

        /// <summary>Locomotion ブレンドツリーで「ダッシュ」に当たる Speed の値（セットアップツールと合わせる）</summary>
        public const float SprintAnimBlendValue = 1.6f;

        // ================================================================
        //  Unity メッセージ
        // ================================================================

        protected virtual void Awake()
        {
            Controller = GetComponent<CharacterController>();
            Input = GetComponent<PlayerInputReader>();
            if (animator == null) animator = GetComponentInChildren<Animator>();
            if (animator != null) animator.applyRootMotion = false; // 移動はスクリプトで制御する
            // Home で付けたスキル（体力アップ・スタミナアップ）
            maxHp *= GameData.GetSkillMultiplier(SkillType.Health);
            maxStamina *= GameData.GetSkillMultiplier(SkillType.Stamina);
            CurrentHp = maxHp;
            CurrentStamina = maxStamina;
            if (quickItem == null) quickItem = ItemDefinition.Find(GameData.StartItemId);
            dodgeCurveArea = CalcCurveArea(dodgeSpeedCurve);
        }

        /// <summary>カーブの 0〜1 の面積（移動距離をぴったり dodgeDistance にするための補正値）</summary>
        private static float CalcCurveArea(AnimationCurve curve)
        {
            if (curve == null || curve.length == 0) return 1f;
            const int steps = 50;
            float sum = 0f;
            for (int i = 0; i < steps; i++) sum += curve.Evaluate((i + 0.5f) / steps);
            return Mathf.Max(0.01f, sum / steps);
        }

        protected virtual void OnEnable() => Active = this;

        protected virtual void OnDisable()
        {
            if (Active == this) Active = null;
        }

        protected virtual void Start()
        {
            if (cameraTransform == null && Camera.main != null) cameraTransform = Camera.main.transform;
            HealthChanged?.Invoke(CurrentHp, maxHp);
            StaminaChanged?.Invoke(CurrentStamina, maxStamina);
        }

        protected virtual void Update()
        {
            float dt = Time.deltaTime;
            StateTime += dt;
            horizontalVelocity = Vector3.zero;
            IsSprinting = false;

            UpdateLockOn();
            UpdateFocus();

            switch (CurrentState)
            {
                case State.Locomotion: UpdateLocomotion(dt); break;
                case State.Action: UpdateAction(dt); break;
                case State.Dodge: UpdateDodge(dt); break;
                case State.Hit: UpdateHit(dt); break;
                case State.Down: UpdateDown(dt); break;
                case State.UseItem: UpdateUseItem(dt); break;
                case State.Dead: break;
            }

            UpdateStamina(dt);
            ApplyMotion(dt);
        }

        // ================================================================
        //  サブクラスが実装するもの
        // ================================================================

        /// <summary>
        /// 通常移動中、毎フレーム呼ばれる。
        /// 攻撃入力などがあればアクションを開始し（BeginAction を呼ぶ）、true を返す。
        /// </summary>
        protected abstract bool TryStartAction();

        /// <summary>アクション状態の間、毎フレーム呼ばれる。終わったら ReturnToLocomotion() を呼ぶ。</summary>
        protected abstract void UpdateAction(float deltaTime);

        /// <summary>アクションが回避・被弾・死亡で中断されたときに呼ばれる（当たり判定を消すなどの後始末）</summary>
        protected virtual void OnActionInterrupted() { }

        /// <summary>
        /// ダメージを受ける直前に呼ばれる。ガード・カウンター・ダメージ軽減スキルなどを作るときに override する。
        ///   ・info の中身（Amount, CausesFlinch, KnockbackDistance）を書き換えると、受けるダメージが変わる
        ///   ・false を返すと、その攻撃を完全に無効にする（当たらなかった扱い）
        /// </summary>
        protected virtual bool OnBeforeDamage(ref DamageInfo info) => true;

        // ================================================================
        //  サブクラス用ヘルパー
        // ================================================================

        /// <summary>アクション状態に入る（同じアクション状態からでも StateTime がリセットされる）</summary>
        protected void BeginAction() => ChangeState(State.Action);

        /// <summary>通常移動に戻る</summary>
        protected void ReturnToLocomotion(float fadeTime = 0.2f)
        {
            ChangeState(State.Locomotion);
            focusLocomotionAnim = IsFocusing;
            CrossFade(IsFocusing ? AnimStateFocusLocomotion : AnimStateLocomotion, fadeTime);
        }

        /// <summary>カメラの正面方向（水平）</summary>
        protected Vector3 CameraForwardFlat
        {
            get
            {
                Vector3 f = cameraTransform != null ? cameraTransform.forward : transform.forward;
                f.y = 0f;
                return f.sqrMagnitude > 0.0001f ? f.normalized : transform.forward;
            }
        }

        /// <summary>
        /// 攻撃を出すときに向くべき方向。
        ///   集中モード中 → カメラの向き（照準の方向）
        ///   移動入力あり → 入力方向
        ///   ロックオン中 → ターゲットの方向
        ///   それ以外     → 今の向き
        /// </summary>
        protected Vector3 GetAttackFacingDirection()
        {
            if (IsFocusing) return CameraForwardFlat;
            Vector3 move = GetMoveDirection();
            if (move.sqrMagnitude > 0.01f) return move;
            if (LockTarget != null)
            {
                Vector3 to = LockTarget.AimPosition - transform.position;
                to.y = 0f;
                if (to.sqrMagnitude > 0.0001f) return to.normalized;
            }
            return transform.forward;
        }

        /// <summary>
        /// 照準が指しているワールド座標（画面中央からのレイが当たった場所）。
        /// 何にも当たらなければ aimMaxDistance 先の点。自分自身には当たらない。
        /// </summary>
        public Vector3 GetAimPoint()
        {
            if (cameraTransform == null) return transform.position + transform.forward * aimMaxDistance;

            var ray = new Ray(cameraTransform.position, cameraTransform.forward);
            // ボスの部位（トリガー）も狙えるように Collide
            int count = Physics.RaycastNonAlloc(ray, aimHits, aimMaxDistance, aimLayers, QueryTriggerInteraction.Collide);
            float best = aimMaxDistance;
            Vector3 point = ray.GetPoint(aimMaxDistance);
            for (int i = 0; i < count; i++)
            {
                if (aimHits[i].transform.IsChildOf(transform)) continue;
                if (aimHits[i].distance < best)
                {
                    best = aimHits[i].distance;
                    point = aimHits[i].point;
                }
            }
            return point;
        }

        /// <summary>from（矢の発射位置など）から照準へ向かう方向（正規化済み）</summary>
        public Vector3 GetAimDirection(Vector3 from)
        {
            Vector3 dir = GetAimPoint() - from;
            return dir.sqrMagnitude > 0.0001f ? dir.normalized : transform.forward;
        }

        /// <summary>ロックオン対象を設定する（null で解除）</summary>
        public void SetLockTarget(LockOnTarget target)
        {
            if (LockTarget == target) return;
            LockTarget = target;
            LockTargetChanged?.Invoke(target);
        }

        /// <summary>回避入力があればアクションを中断して回避する（キャンセル可能タイミングで呼ぶ）</summary>
        protected bool TryCancelIntoDodge()
        {
            return Input.ConsumeDodge() && StartDodge(GetMoveDirection());
        }

        /// <summary>
        /// スタミナを消費する。足りなければ消費せず false を返す。
        /// （溜め攻撃・ガード・スキルなど、タイプ固有のアクションで使う）
        /// </summary>
        public bool TryUseStamina(float amount)
        {
            if (amount <= 0f) return true;
            if (CurrentStamina < amount) return false;
            CurrentStamina -= amount;
            lastStaminaUseTime = Time.time;
            StaminaChanged?.Invoke(CurrentStamina, maxStamina);
            return true;
        }

        /// <summary>スタミナを回復する（アイテム・スキル用）</summary>
        public void RestoreStamina(float amount)
        {
            if (amount <= 0f) return;
            CurrentStamina = Mathf.Min(maxStamina, CurrentStamina + amount);
            StaminaChanged?.Invoke(CurrentStamina, maxStamina);
        }

        /// <summary>Animator のステートへクロスフェードする</summary>
        protected void CrossFade(string stateName, float fadeTime = 0.1f, int layer = 0)
        {
            if (animator != null) animator.CrossFadeInFixedTime(stateName, fadeTime, layer, 0f);
        }

        /// <summary>
        /// 外から押される力を加える（ガード時の押し戻しなど）。だんだん弱まって止まる。
        /// distance m だけ押される。
        /// </summary>
        protected void AddPush(Vector3 direction, float distance, float duration = 0.25f)
        {
            direction.y = 0f;
            if (direction.sqrMagnitude < 0.0001f || distance <= 0f) return;
            pushDeceleration = 2f * distance / (duration * duration);
            pushVelocity = direction.normalized * (pushDeceleration * duration); // 等減速で distance 進む初速
        }

        /// <summary>
        /// 今のアクションで Mixamo のモーションを再生するときに、BeginAction の後で呼ぶ。
        /// （Kevin Iglesias の SpineProxy などを一時的に止めて、上半身が正しく動くようにする）
        /// アクションが終わると自動で元に戻る。
        /// </summary>
        protected void UseMixamoMotion()
        {
            actionUsesMixamoMotion = true;
            RefreshMixamoMotionMode();
        }

        /// <summary>Animator のステートへ、途中の位置（0〜1）からクロスフェードする</summary>
        protected void CrossFadeAt(string stateName, float normalizedTime, float fadeTime = 0.1f, int layer = 0)
        {
            if (animator != null) animator.CrossFade(stateName, fadeTime, layer, normalizedTime);
        }

        /// <summary>このフレームの水平移動速度を設定する（m/s）</summary>
        protected void SetHorizontalVelocity(Vector3 velocity)
        {
            velocity.y = 0f;
            horizontalVelocity = velocity;
        }

        /// <summary>カメラ基準のワールド移動方向（長さ 0〜1）</summary>
        protected Vector3 GetMoveDirection()
        {
            Vector2 input = Input.Move;
            if (input.sqrMagnitude < 0.0001f) return Vector3.zero;

            Vector3 forward = cameraTransform != null ? cameraTransform.forward : Vector3.forward;
            Vector3 right = cameraTransform != null ? cameraTransform.right : Vector3.right;
            forward.y = 0f; right.y = 0f;
            forward.Normalize(); right.Normalize();
            return Vector3.ClampMagnitude(forward * input.y + right * input.x, 1f);
        }

        /// <summary>指定方向へ旋回する。instant = true なら即座に向く</summary>
        protected void FaceDirection(Vector3 direction, bool instant = false)
        {
            direction.y = 0f;
            if (direction.sqrMagnitude < 0.0001f) return;
            Quaternion target = Quaternion.LookRotation(direction);
            transform.rotation = instant
                ? target
                : Quaternion.RotateTowards(transform.rotation, target, turnSpeed * Time.deltaTime);
        }

#if UNITY_EDITOR
        /// <summary>セットアップツール用：回避の長さと無敵時間を設定</summary>
        public void EditorSetupDodge(bool faceDirection, float distance, float duration, float invincibleStart, float invincibleEnd,
                                     Behaviour[] disableDuringDodge)
        {
            dodgeDistance = distance;
            disableDuringMixamoMotion = disableDuringDodge;
            faceDodgeDirection = faceDirection;
            dodgeDuration = duration;
            dodgeInvincibleStart = invincibleStart;
            dodgeInvincibleEnd = invincibleEnd;
        }
#endif

        /// <summary>
        /// 外から位置をずらす（ボスの体に押し出されるときなど）。地形には引っかかる。
        /// </summary>
        public void ForceMove(Vector3 offset)
        {
            if (Controller != null && Controller.enabled) Controller.Move(offset);
        }

        /// <summary>HP を回復する（回復アイテム・スキル用）</summary>
        public void Heal(float amount)
        {
            if (!IsAlive || amount <= 0f) return;
            float before = CurrentHp;
            CurrentHp = Mathf.Min(maxHp, CurrentHp + amount);
            HealthChanged?.Invoke(CurrentHp, maxHp);
            Healed?.Invoke(CurrentHp - before);
        }

        // ================================================================
        //  ステートごとの処理
        // ================================================================

        private void ChangeState(State next)
        {
            if (CurrentState == State.Action && next != State.Action) OnActionInterrupted();
            CurrentState = next;
            StateTime = 0f;
            actionUsesMixamoMotion = false; // 新しいアクションでは毎回リセット（使うならサブクラスが UseMixamoMotion を呼ぶ）
            RefreshMixamoMotionMode();
            dodgeInvincible = false;
        }

        /// <summary>回避中、または Mixamo モーションのアクション中だけ、指定コンポーネントを無効にする</summary>
        private void RefreshMixamoMotionMode()
        {
            bool mixamo = CurrentState == State.Dodge || (CurrentState == State.Action && actionUsesMixamoMotion);
            if (mixamo == mixamoMotionApplied) return;
            mixamoMotionApplied = mixamo;
            if (disableDuringMixamoMotion == null) return;
            foreach (var b in disableDuringMixamoMotion)
                if (b != null) b.enabled = !mixamo;
        }

        private void UpdateLockOn()
        {
            // 対象が倒れた・離れすぎたら自動解除
            if (LockTarget != null &&
                (!LockTarget.IsTargetable ||
                 (LockTarget.AimPosition - transform.position).sqrMagnitude > lockOnMaxDistance * lockOnMaxDistance * 2.25f))
            {
                SetLockTarget(null);
            }

            if (!IsAlive || !Input.LockOnPressedThisFrame) return;

            if (LockTarget != null)
            {
                SetLockTarget(null);
                return;
            }

            var best = LockOnTarget.FindBest(transform.position, CameraForwardFlat, lockOnMaxDistance);
            if (best != null) SetLockTarget(best);
            else CameraResetRequested?.Invoke(); // 相手がいなければカメラを背後へ
        }

        private void UpdateFocus()
        {
            bool want = Input.FocusHeld && IsAlive && CurrentState != State.Hit && CurrentState != State.Down;
            if (want == IsFocusing) return;
            IsFocusing = want;
            FocusChanged?.Invoke(want);
        }

        private void UpdateLocomotion(float dt)
        {
            Vector3 move = GetMoveDirection();

            if (Input.ConsumeDodge() && StartDodge(move)) return;
            if (Input.ConsumeUseItem() && StartUseItem()) return;
            if (TryStartAction()) return;

            // 集中モードの ON/OFF に合わせて Animator のステートを切り替える
            if (focusLocomotionAnim != IsFocusing)
            {
                focusLocomotionAnim = IsFocusing;
                CrossFade(IsFocusing ? AnimStateFocusLocomotion : AnimStateLocomotion, 0.2f);
            }

            if (IsFocusing)
            {
                // 集中モード：カメラの向きを向いたまま、横歩き・後ろ歩き
                SetHorizontalVelocity(move * focusMoveSpeed);
                FaceDirection(CameraForwardFlat);
                if (animator != null)
                {
                    Vector3 local = transform.InverseTransformDirection(move);
                    animator.SetFloat(AnimParamMoveX, local.x, 0.1f, dt);
                    animator.SetFloat(AnimParamMoveY, local.z, 0.1f, dt);
                    animator.SetFloat(AnimParamSpeed, 0f);
                }
                return;
            }

            // ダッシュ：ボタンを押していて、移動入力があり、スタミナが残っていて、疲れていない
            IsSprinting = Input.SprintHeld && move.sqrMagnitude > 0.01f && !IsExhausted && CurrentStamina > 0f;

            SetHorizontalVelocity(move * (IsSprinting ? sprintSpeed : moveSpeed));
            FaceDirection(move);
            if (animator != null)
            {
                float blend = move.magnitude * (IsSprinting ? SprintAnimBlendValue : 1f);
                animator.SetFloat(AnimParamSpeed, blend, 0.15f, dt);
            }
        }

        private void UpdateStamina(float dt)
        {
            float before = CurrentStamina;

            if (IsSprinting)
            {
                CurrentStamina = Mathf.Max(0f, CurrentStamina - sprintStaminaPerSecond * dt);
                lastStaminaUseTime = Time.time;
                if (CurrentStamina <= 0f) IsExhausted = true;
            }
            else if (IsAlive && Time.time - lastStaminaUseTime >= staminaRegenDelay)
            {
                CurrentStamina = Mathf.Min(maxStamina, CurrentStamina + staminaRegenPerSecond * dt);
            }

            if (IsExhausted && CurrentStamina >= maxStamina * exhaustedRecoverRate) IsExhausted = false;

            if (!Mathf.Approximately(before, CurrentStamina)) StaminaChanged?.Invoke(CurrentStamina, maxStamina);
        }

        /// <summary>回避を開始する。スタミナ不足なら false</summary>
        private bool StartDodge(Vector3 worldDirection)
        {
            if (!TryUseStamina(dodgeStaminaCost)) return false;

            bool hasInput = worldDirection.sqrMagnitude > 0.01f;
            Vector3 local;

            if (faceDodgeDirection)
            {
                // ローリング：入力方向へ向き直って前転（入力が無ければ正面へ）
                dodgeDirection = hasInput ? worldDirection.normalized : transform.forward;
                FaceDirection(dodgeDirection, instant: true);
                local = Vector3.forward;
            }
            else
            {
                // ステップ：向きはそのまま、向きに対する相対方向でアニメを選ぶ（入力が無ければ後ろ）
                dodgeDirection = hasInput ? worldDirection.normalized : -transform.forward;
                local = transform.InverseTransformDirection(dodgeDirection);
            }

            if (animator != null)
            {
                animator.SetFloat(AnimParamDodgeX, local.x);
                animator.SetFloat(AnimParamDodgeY, local.z);
                animator.SetFloat(AnimParamSpeed, 0f);
            }

            ChangeState(State.Dodge);
            CrossFade(AnimStateDodge, 0.05f);
            return true;
        }

        private void UpdateDodge(float dt)
        {
            float t = Mathf.Clamp01(StateTime / dodgeDuration);

            // 速度 = 平均速度 × カーブの値（面積で割って、合計移動距離 = dodgeDistance にする）
            float speed = dodgeDistance / dodgeDuration * dodgeSpeedCurve.Evaluate(t) / dodgeCurveArea;
            SetHorizontalVelocity(dodgeDirection * speed);

            dodgeInvincible = StateTime >= dodgeInvincibleStart && StateTime <= dodgeInvincibleEnd;

            if (t >= dodgeRecoverRate)
            {
                // 回避の後半は次の回避・攻撃で割り込める
                if (Input.ConsumeDodge() && StartDodge(GetMoveDirection())) return;
                if (TryStartAction()) return;
            }

            if (t >= 1f) ReturnToLocomotion(0.15f);
        }

        private void UpdateHit(float dt)
        {
            float t = Mathf.Clamp01(StateTime / hitStunDuration);
            SetHorizontalVelocity(knockbackVelocity * 2f * (1f - t));
            if (t >= 1f) ReturnToLocomotion();
        }

        private bool gettingUp;

        private void StartDown(Vector3 direction, float knockbackDistance)
        {
            float distance = Mathf.Max(downMinKnockback, knockbackDistance);
            knockbackVelocity = direction * (distance / downKnockbackTime);
            gettingUp = false;

            Input.ClearBuffer();
            ChangeState(State.Down);
            CrossFade(HasAnimState(AnimStateKnockDown) ? AnimStateKnockDown : AnimStateDeath, 0.05f);
        }

        private void UpdateDown(float dt)
        {
            // 吹き飛び（だんだん減速）
            if (StateTime < downKnockbackTime)
            {
                float t = StateTime / downKnockbackTime;
                SetHorizontalVelocity(knockbackVelocity * 2f * (1f - t));
            }

            // 受け身：回避ボタンで転がって起き上がる
            if (!gettingUp && StateTime >= downRecoverTime && Input.ConsumeDodge() && StartDodge(GetMoveDirection())) return;

            if (!gettingUp && StateTime >= downDuration)
            {
                // 起き上がりモーションがあれば再生、無ければそのまま立つ
                if (HasAnimState(AnimStateGetUp))
                {
                    gettingUp = true;
                    CrossFade(AnimStateGetUp, 0.15f);
                }
                else
                {
                    ReturnToLocomotion(0.35f);
                    return;
                }
            }

            if (gettingUp && StateTime >= downDuration + getUpDuration) ReturnToLocomotion(0.2f);
        }

        private bool itemEffectApplied;

        /// <summary>アイテム使用を始める。持っていなければ false</summary>
        private bool StartUseItem()
        {
            if (quickItem == null || GameData.GetCount(quickItem) <= 0) return false;

            itemEffectApplied = false;
            ChangeState(State.UseItem);
            if (HasAnimState(AnimStateUseItem)) CrossFade(AnimStateUseItem, 0.15f);
            else
            {
                focusLocomotionAnim = false;
                CrossFade(AnimStateLocomotion, 0.15f);
                if (animator != null) animator.SetFloat(AnimParamSpeed, 0f); // その場で待機
            }
            return true;
        }

        private void UpdateUseItem(float dt)
        {
            // 効果が出る前に回避すると、使うのをやめる（アイテムは減らない）
            if (TryCancelIntoDodge()) return;

            if (!itemEffectApplied && StateTime >= quickItem.EffectTime)
            {
                itemEffectApplied = true;
                if (GameData.TryConsume(quickItem))
                {
                    Heal(maxHp * quickItem.HealRate);
                    ItemUsed?.Invoke(quickItem);
                }
            }

            if (StateTime >= quickItem.UseTime) ReturnToLocomotion(0.2f);
        }

        private bool HasAnimState(string stateName) =>
            animator != null && animator.HasState(0, Animator.StringToHash(stateName));

        private void ApplyMotion(float dt)
        {
            if (Controller.isGrounded && verticalVelocity < 0f) verticalVelocity = -2f; // 接地を保つ
            verticalVelocity += gravity * dt;

            Vector3 motion = horizontalVelocity + pushVelocity + Vector3.up * verticalVelocity;
            pushVelocity = Vector3.MoveTowards(pushVelocity, Vector3.zero, pushDeceleration * dt);
            if (Controller.enabled) Controller.Move(motion * dt);
        }

        // ================================================================
        //  IDamageable
        // ================================================================

        public virtual bool ApplyDamage(DamageInfo info)
        {
            if (!IsAlive || IsInvincible) return false;
            if (info.Attacker == gameObject) return false;
            if (!OnBeforeDamage(ref info)) return false; // ガードなどで無効化
            info.Amount /= DefenseMultiplier;           // 防御力アップ（1.2 倍なら ダメージ ÷ 1.2）

            CurrentHp = Mathf.Max(0f, CurrentHp - info.Amount);
            HealthChanged?.Invoke(CurrentHp, maxHp);
            Damaged?.Invoke(info);

            if (CurrentHp <= 0f)
            {
                Die();
                return true;
            }

            invincibleUntil = Time.time + hitInvincibleTime;

            Vector3 dir = info.Direction; dir.y = 0f;
            if (dir.sqrMagnitude > 0.0001f)
            {
                dir.Normalize();
                // のけぞる / 倒れるときは、攻撃してきた方を向く
                if (info.KnockDown || info.CausesFlinch) FaceDirection(-dir, instant: true);
            }

            if (info.KnockDown)
            {
                // 大攻撃：吹き飛んでダウン
                StartDown(dir, info.KnockbackDistance);
            }
            else if (info.CausesFlinch)
            {
                knockbackVelocity = dir * (info.KnockbackDistance / hitStunDuration);

                Input.ClearBuffer();
                ChangeState(State.Hit);
                CrossFade(AnimStateHit, 0.05f);
            }
            return true;
        }

        private void Die()
        {
            Input.ClearBuffer();
            ChangeState(State.Dead);
            CrossFade(AnimStateDeath, 0.1f);
            Died?.Invoke();
        }
    }
}
