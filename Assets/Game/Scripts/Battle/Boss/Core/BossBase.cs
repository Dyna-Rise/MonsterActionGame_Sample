using System;
using System.Collections;
using MonsterAction.Battle.Flow;
using MonsterAction.Battle.Player;
using MonsterAction.Combat;
using UnityEngine;

namespace MonsterAction.Battle.Boss
{
    /// <summary>
    /// ボスの土台（抽象クラス）。ボスを作るときはこれを継承します。
    ///
    /// 【しくみ】
    /// ボスは「行動」を1つずつ順番に実行します。行動はコルーチン（IEnumerator）で書きます。
    ///   1. 行動中でなければ ThinkNextAction() が呼ばれる → 次の行動（コルーチン）を返す
    ///   2. その行動が最後まで終わったら、また ThinkNextAction() が呼ばれる
    ///   3. ひるみ・死亡のときは、行動が途中で打ち切られる
    ///
    /// 【行動の書き方の例】
    ///   IEnumerator Bite()
    ///   {
    ///       yield return TurnToPlayer(0.5f);          // プレイヤーの方を向く
    ///       Play("Basic Attack");                     // モーション再生
    ///       yield return WaitStateTime("Basic Attack", 0.3f);  // モーションの30%まで待つ
    ///       biteHitbox.Begin(25f, knockback: 2f);     // 攻撃判定 ON
    ///       yield return WaitStateTime("Basic Attack", 0.55f);
    ///       biteHitbox.End();                         // 攻撃判定 OFF
    ///       yield return WaitStateTime("Basic Attack", 0.95f);
    ///       yield return Rest(1.0f);                  // 隙（プレイヤーが攻撃するチャンス）
    ///   }
    ///
    /// 【ダメージの受け方】
    /// 頭・胴・尻尾などに BossHurtbox（部位）を付けると、部位ごとの倍率でダメージを受けます。
    /// HP が 0 になると、死亡モーション → BattleFlowController.ClearQuest()（クエストクリア）
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public abstract class BossBase : MonoBehaviour, IDamageable
    {
        /// <summary>現在シーンにいるボス（HP バーなどで使う）</summary>
        public static BossBase Active { get; private set; }

        [Header("ステータス")]
        [SerializeField] private string bossName = "Boss";
        [SerializeField] private float maxHp = 600f;
        [Tooltip("この量のダメージがたまるとひるむ（0 ならひるまない）")]
        [SerializeField] private float flinchThreshold = 120f;

        [Header("攻撃の間隔")]
        [Tooltip("Rest()（攻撃後の休み）の時間の倍率。大きいほどボスの攻撃がゆっくりになる")]
        [SerializeField] private float restTimeScale = 1.6f;
        [Tooltip("Rest() にランダムで足す時間（0〜この秒数）。攻撃のリズムを読まれにくくする")]
        [SerializeField] private float restRandomExtra = 0.8f;

        [Header("移動")]
        [Tooltip("1秒あたりの旋回角度")]
        [SerializeField] private float turnSpeed = 120f;
        [SerializeField] private float gravity = -30f;

        [Header("体の押し出し（プレイヤーが乗り上げない / めり込まないように）")]
        [Tooltip("体の太さ（半径 m）")]
        [SerializeField] private float bodyRadius = 2.2f;
        [Tooltip("体の前後の長さの半分（m）。0 なら円、長くすると前後に長いカプセル")]
        [SerializeField] private float bodyHalfLength = 1.5f;
        [Tooltip("体の中心のずれ（ボスから見て前が +z）")]
        [SerializeField] private Vector3 bodyCenterOffset = Vector3.zero;

        [Header("Animator のステート名")]
        [SerializeField] protected string idleState = "Idle";
        [SerializeField] protected string hitState = "GetHit";
        [SerializeField] protected string dieState = "Die";

        [Header("参照（空なら自動取得）")]
        [SerializeField] private Animator animator;

        // ---- サブクラスから使うもの ----
        protected Animator Anim => animator;
        protected CharacterController Controller { get; private set; }
        /// <summary>狙う相手（プレイヤー）</summary>
        protected PlayerBattleController Player => PlayerBattleController.Active;
        /// <summary>行動中か</summary>
        protected bool IsBusy => currentAction != null;

        // ---- 外部（UI・GameManager）に公開するもの ----
        public string BossName => bossName;
        public float MaxHp => maxHp;
        public float CurrentHp { get; private set; }
        public float HpRate => maxHp > 0f ? CurrentHp / maxHp : 0f;
        public bool IsAlive => CurrentHp > 0f;

        /// <summary>HP が変わった（現在, 最大）</summary>
        public event Action<float, float> HealthChanged;
        /// <summary>ダメージを受けた（情報, 部位名, 実際のダメージ）</summary>
        public event Action<DamageInfo, string, float> Damaged;
        /// <summary>倒れた</summary>
        public event Action Died;

        private Coroutine currentAction;
        private float verticalVelocity;
        private Vector3 frameVelocity;
        private float flinchAccumulated;
        private BossAttackHitbox[] attackHitboxes;

        // ================================================================
        //  Unity メッセージ
        // ================================================================

        protected virtual void Awake()
        {
            Controller = GetComponent<CharacterController>();
            if (animator == null) animator = GetComponentInChildren<Animator>();
            if (animator != null) animator.applyRootMotion = false;
            CurrentHp = maxHp;
            attackHitboxes = GetComponentsInChildren<BossAttackHitbox>(true);
            foreach (var h in attackHitboxes) h.SetOwner(transform);
        }

        protected virtual void OnEnable() => Active = this;
        protected virtual void OnDisable()
        {
            if (Active == this) Active = null;
        }

        protected virtual void Start()
        {
            HealthChanged?.Invoke(CurrentHp, maxHp);
            Play(idleState, 0f);
        }

        protected virtual void Update()
        {
            if (IsAlive && !IsBusy)
            {
                bool hasTarget = Player != null && Player.IsAlive &&
                                 (BattleFlowController.Instance == null ||
                                  BattleFlowController.Instance.CurrentPhase == BattleFlowController.Phase.Playing);
                IEnumerator next = hasTarget ? ThinkNextAction() : IdleWhileNoTarget();
                if (next != null) StartAction(next);
            }

        }

        // 行動のコルーチン（Move を呼ぶ）は Update の「後」に動くので、
        // 実際の移動はさらに後の LateUpdate で行う
        protected virtual void LateUpdate()
        {
            ApplyMotion(Time.deltaTime);
            frameVelocity = Vector3.zero; // 次のフレームも動くなら、コルーチンがまた Move を呼ぶ
            PushPlayerOut();
        }

        private PlayerBattleController ignoredPlayer;

        /// <summary>
        /// プレイヤーとの物理的なぶつかり合いは止めて（乗り上げ防止）、
        /// 代わりにボスの体（前後に長いカプセル）の中に入ったプレイヤーを横へ押し出す。
        /// </summary>
        private void PushPlayerOut()
        {
            var player = Player;
            if (player == null) return;
            var playerCc = player.GetComponent<CharacterController>();
            if (playerCc == null) return;

            // 新しいプレイヤーとは、CharacterController 同士の衝突を無視する
            if (ignoredPlayer != player)
            {
                Physics.IgnoreCollision(Controller, playerCc, true);
                ignoredPlayer = player;
            }
            if (!IsAlive) return; // 倒れた後は押し出さない

            // ボスの体の芯（水平な線分）上で、プレイヤーに一番近い点
            Vector3 center = transform.TransformPoint(bodyCenterOffset);
            Vector3 forward = transform.forward; forward.y = 0f; forward.Normalize();
            Vector3 a = center - forward * bodyHalfLength;
            Vector3 b = center + forward * bodyHalfLength;
            Vector3 p = player.transform.position;
            Vector3 ab = b - a; ab.y = 0f;
            Vector3 ap = p - a; ap.y = 0f;
            float t = ab.sqrMagnitude > 0.0001f ? Mathf.Clamp01(Vector3.Dot(ap, ab) / ab.sqrMagnitude) : 0f;
            Vector3 closest = a + ab * t;

            Vector3 away = p - closest; away.y = 0f;
            float minDistance = bodyRadius + playerCc.radius;
            float distance = away.magnitude;
            if (distance >= minDistance) return;

            Vector3 dir = distance > 0.001f ? away / distance : -transform.forward; // 真ん中なら後ろへ
            player.ForceMove(dir * (minDistance - distance));
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0.6f, 1f, 0.4f, 0.8f);
            Vector3 center = transform.TransformPoint(bodyCenterOffset);
            Vector3 forward = transform.forward; forward.y = 0f; forward.Normalize();
            Vector3 a = center - forward * bodyHalfLength;
            Vector3 b = center + forward * bodyHalfLength;
            Gizmos.DrawWireSphere(a, bodyRadius);
            Gizmos.DrawWireSphere(b, bodyRadius);
            Gizmos.DrawLine(a, b);
        }

        // ================================================================
        //  サブクラスが実装するもの
        // ================================================================

        /// <summary>次の行動を決めて返す（行動中でないときに呼ばれる）</summary>
        protected abstract IEnumerator ThinkNextAction();

        /// <summary>プレイヤーがいない / 倒れたときの行動（初期値：その場で待機）</summary>
        protected virtual IEnumerator IdleWhileNoTarget()
        {
            Play(idleState);
            yield return Wait(1f);
        }

        /// <summary>ひるんだときの行動（初期値：ひるみモーションを最後まで）</summary>
        protected virtual IEnumerator Flinch()
        {
            Play(hitState, 0.05f);
            yield return WaitStateTime(hitState, 0.95f);
        }

        // ================================================================
        //  行動を書くための道具
        // ================================================================

        /// <summary>Animator のステートを再生する</summary>
        protected void Play(string stateName, float fadeTime = 0.15f)
        {
            if (animator != null) animator.CrossFadeInFixedTime(stateName, fadeTime, 0, 0f);
        }

        /// <summary>そのステートが今どこまで再生されたか（0〜1、ループすると 1 を超える）。再生していなければ 0</summary>
        protected float GetStateTime(string stateName)
        {
            if (animator == null) return 0f;
            if (animator.IsInTransition(0))
            {
                var next = animator.GetNextAnimatorStateInfo(0);
                if (next.IsName(stateName)) return next.normalizedTime;
            }
            var current = animator.GetCurrentAnimatorStateInfo(0);
            return current.IsName(stateName) ? current.normalizedTime : 0f;
        }

        /// <summary>ステートが t（0〜1）まで再生されるのを待つ</summary>
        protected IEnumerator WaitStateTime(string stateName, float t, float timeout = 6f)
        {
            float limit = Time.time + timeout;
            yield return null; // CrossFade が反映されるのを1フレーム待つ
            while (GetStateTime(stateName) < t && Time.time < limit) yield return null;
        }

        /// <summary>
        /// ステートが t まで再生されるのを、プレイヤーへ軸合わせ（向き直し）しながら待つ。
        /// 攻撃の「振りかぶり」の間に使うと、プレイヤーが横に逃げても追いかけて向きを合わせてくる。
        /// turnMultiplier：向き直る速さの倍率（大きいほど逃げにくい）
        /// </summary>
        protected IEnumerator WaitStateTimeTracking(string stateName, float t, float turnMultiplier = 1f, float timeout = 6f)
        {
            float limit = Time.time + timeout;
            yield return null;
            while (GetStateTime(stateName) < t && Time.time < limit)
            {
                FaceTowards(DirectionToPlayer, turnMultiplier);
                yield return null;
            }
        }

        /// <summary>プレイヤーの胸あたりの位置（飛び道具の狙い先）</summary>
        protected Vector3 PlayerAimPosition => Player != null ? Player.transform.position + Vector3.up * 1.0f : transform.position + transform.forward * 10f;

        /// <summary>このボスが攻撃者になる DamageInfo を作る（飛び道具用）</summary>
        protected DamageInfo MakeDamage(float damage, float knockback, bool causesFlinch = true, bool knockDown = false) =>
            new DamageInfo(damage, Vector3.zero, Vector3.zero, gameObject, causesFlinch, knockback, knockDown);

        /// <summary>秒数だけ待つ</summary>
        protected IEnumerator Wait(float seconds)
        {
            yield return new WaitForSeconds(seconds);
        }

        /// <summary>
        /// 待機モーションで休む（攻撃後の隙）。
        /// 実際の時間 = seconds × restTimeScale ＋ ランダム(0〜restRandomExtra)
        /// </summary>
        protected IEnumerator Rest(float seconds)
        {
            Play(idleState, 0.25f);
            yield return Wait(seconds * restTimeScale + UnityEngine.Random.Range(0f, restRandomExtra));
        }

        /// <summary>プレイヤーの方を向く（maxSeconds 以内、ほぼ向いたら終わり）</summary>
        protected IEnumerator TurnToPlayer(float maxSeconds, string turnAnimState = null)
        {
            if (turnAnimState != null) Play(turnAnimState);
            float end = Time.time + maxSeconds;
            while (Time.time < end && Mathf.Abs(AngleToPlayer) > 5f)
            {
                FaceTowards(DirectionToPlayer);
                yield return null;
            }
        }

        /// <summary>このフレームの移動速度（m/s）を設定する。行動の中で毎フレーム呼ぶ</summary>
        protected void Move(Vector3 velocity)
        {
            velocity.y = 0f;
            frameVelocity = velocity;
        }

        /// <summary>向きを direction へ turnSpeed で近づける（毎フレーム呼ぶ）</summary>
        protected void FaceTowards(Vector3 direction, float speedMultiplier = 1f)
        {
            direction.y = 0f;
            if (direction.sqrMagnitude < 0.0001f) return;
            transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(direction),
                                                          turnSpeed * speedMultiplier * Time.deltaTime);
        }

        /// <summary>プレイヤーまでの水平距離</summary>
        protected float DistanceToPlayer
        {
            get
            {
                if (Player == null) return float.MaxValue;
                Vector3 d = Player.transform.position - transform.position;
                d.y = 0f;
                return d.magnitude;
            }
        }

        /// <summary>プレイヤーへの水平方向（正規化）</summary>
        protected Vector3 DirectionToPlayer
        {
            get
            {
                if (Player == null) return transform.forward;
                Vector3 d = Player.transform.position - transform.position;
                d.y = 0f;
                return d.sqrMagnitude > 0.0001f ? d.normalized : transform.forward;
            }
        }

        /// <summary>正面から見たプレイヤーの角度（-180〜180、右がプラス）</summary>
        protected float AngleToPlayer => Vector3.SignedAngle(transform.forward, DirectionToPlayer, Vector3.up);

        /// <summary>
        /// 範囲攻撃（叩きつけ・咆哮など）。center から radius 以内のプレイヤーにダメージ。
        /// damage = 0 でも causesFlinch = true なら、のけぞらせることができる（咆哮）
        /// knockDown = true なら大攻撃（ダウンさせる）
        /// </summary>
        protected void AreaAttack(Vector3 center, float radius, float damage, float knockback,
                                  bool causesFlinch = true, bool knockDown = false)
        {
            var player = Player;
            if (player == null) return;
            Vector3 to = player.transform.position - center;
            to.y = 0f;
            if (to.magnitude > radius) return;
            Vector3 dir = to.sqrMagnitude > 0.0001f ? to.normalized : transform.forward;
            player.ApplyDamage(new DamageInfo(damage, player.transform.position + Vector3.up, dir, gameObject,
                                              causesFlinch, knockback, knockDown));
        }

        // ================================================================
        //  ダメージ
        // ================================================================

        /// <summary>IDamageable：部位以外（本体のコライダーなど）に当たったとき</summary>
        public bool ApplyDamage(DamageInfo info) => ApplyPartDamage(info, 1f, "Body");

        /// <summary>部位（BossHurtbox）からダメージを受ける</summary>
        public virtual bool ApplyPartDamage(DamageInfo info, float multiplier, string partName)
        {
            if (!IsAlive) return false;

            float damage = info.Amount * multiplier;
            CurrentHp = Mathf.Max(0f, CurrentHp - damage);
            HealthChanged?.Invoke(CurrentHp, maxHp);
            Damaged?.Invoke(info, partName, damage);

            if (CurrentHp <= 0f)
            {
                Die();
                return true;
            }

            if (flinchThreshold > 0f)
            {
                flinchAccumulated += damage;
                if (flinchAccumulated >= flinchThreshold)
                {
                    flinchAccumulated = 0f;
                    InterruptAction();
                    StartAction(Flinch());
                }
            }
            return true;
        }

        private void Die()
        {
            InterruptAction();
            Play(dieState, 0.1f);
            Died?.Invoke();
            if (BattleFlowController.Instance != null) BattleFlowController.Instance.ClearQuest();
        }

        // ================================================================
        //  内部
        // ================================================================

        private void StartAction(IEnumerator routine)
        {
            currentAction = StartCoroutine(RunAction(routine));
        }

        private IEnumerator RunAction(IEnumerator routine)
        {
            yield return routine;
            EndAllHitboxes();
            currentAction = null;
        }

        /// <summary>今の行動を打ち切る（攻撃判定も全部 OFF）</summary>
        protected void InterruptAction()
        {
            if (currentAction != null) StopCoroutine(currentAction);
            currentAction = null;
            EndAllHitboxes();
        }

        private void EndAllHitboxes()
        {
            if (attackHitboxes == null) return;
            foreach (var h in attackHitboxes) h.End();
        }

        private void ApplyMotion(float dt)
        {
            if (!Controller.enabled) return;
            if (Controller.isGrounded && verticalVelocity < 0f) verticalVelocity = -2f;
            verticalVelocity += gravity * dt;
            Controller.Move((frameVelocity + Vector3.up * verticalVelocity) * dt);
        }
    }
}
