using System.Collections.Generic;
using MonsterAction.Combat;
using UnityEngine;

namespace MonsterAction.Battle.Player.Samples
{
    /// <summary>
    /// （見本・先生用）Player タイプC：魔法使い。課題 PlayerTypeC_Magic の答えの一例です。
    ///
    ///  ・□ を押す        → 火球（ファイアボール）を撃つ。続けて押すと右手 → 左手 → 右手…と連射
    ///  ・□ を押し続ける  → 足元に魔法陣を出して溜める（スタミナを使う）。溜めるほど範囲・威力が上がる
    ///  ・□ を離す        → 周りに衝撃波（範囲攻撃）。3段階目はボスがひるむ
    ///  ・L1 集中モード中 → 照準へ、ロックオン中 → ターゲットへ、それ以外 → 正面へ撃つ
    ///  ・詠唱中も ×（回避）でキャンセルできる
    ///
    /// エフェクトは Kevin Iglesias の Human Spellcasting Animations の Spells を使っています。
    /// </summary>
    public class PlayerSampleC_Magic : PlayerBattleController
    {
        private const string StateCastR = "CastR";
        private const string StateCastL = "CastL";
        private const string StateCharge = "ChargeLoad";
        private const string StateBurst = "ChargeCast";

        private enum Phase { Cast, Charge, Burst }

        [Header("火球（□ 押し）")]
        [SerializeField] private PlayerProjectile fireballPrefab;
        [SerializeField] private float fireballDamage = 12f;
        [SerializeField] private float fireballSpeed = 22f;
        [Tooltip("詠唱を始めてから火球が出るまで（秒）")]
        [SerializeField] private float castFireTime = 0.45f;
        [Tooltip("詠唱モーション全体の長さ（秒）")]
        [SerializeField] private float castDuration = 1.0f;
        [Tooltip("火球が出たあと、次の火球に繋げられるようになるまで（秒、火球が出てから）")]
        [SerializeField] private float comboDelay = 0.1f;
        [Tooltip("火球が出たあと、移動でキャンセルできるまで（秒、火球が出てから）")]
        [SerializeField] private float castMoveCancel = 0.3f;

        [Header("溜め → 衝撃波（□ 長押し）")]
        [Tooltip("この秒数より長く押していたら溜めに切り替える")]
        [SerializeField] private float holdThreshold = 0.3f;
        [Tooltip("段階ごとのダメージ（1, 2, 3段階）")]
        [SerializeField] private float[] burstDamage = { 20f, 35f, 55f };
        [Tooltip("段階ごとの範囲（半径 m）")]
        [SerializeField] private float[] burstRadius = { 4f, 5f, 6.5f };
        [Tooltip("2段階・3段階になるまでの溜め時間（秒）")]
        [SerializeField] private float[] chargeTime = { 0.8f, 1.6f };
        [SerializeField] private float chargeStaminaPerSecond = 25f;
        [Tooltip("衝撃波のモーション全体の長さ / 衝撃波が出るタイミング（秒）")]
        [SerializeField] private float burstDuration = 1.0f;
        [SerializeField] private float burstHitTime = 0.25f;

        [Header("エフェクト（無くてもよい）")]
        [Tooltip("溜めている間、足元に出す")]
        [SerializeField] private GameObject chargeAuraEffect;
        [Tooltip("衝撃波")]
        [SerializeField] private GameObject burstEffect;
        [Tooltip("burstEffect の大きさ 1 のときの半径（m）。範囲に合わせて拡大する")]
        [SerializeField] private float burstEffectBaseRadius = 4f;

        /// <summary>今の溜め段階（1〜3。溜めていなければ 0）。UI に出したいとき用</summary>
        public int ChargeLevel { get; private set; }

        private Phase phase;
        private float phaseTime;
        private bool rightHand = true;
        private bool fired;
        private float firedTime;
        private bool burstDone;
        private GameObject aura;
        private readonly Collider[] overlaps = new Collider[32];
        private readonly HashSet<IDamageable> burstHit = new HashSet<IDamageable>();

        // ================================================================
        //  開始
        // ================================================================

        protected override bool TryStartAction()
        {
            if (!Input.ConsumeAttack()) return false;
            rightHand = true;
            StartCast();
            return true;
        }

        private void StartCast()
        {
            BeginAction();
            FaceDirection(GetAttackFacingDirection(), instant: true);
            SetPhase(Phase.Cast);
            fired = false;
        }

        // ================================================================
        //  毎フレーム
        // ================================================================

        protected override void UpdateAction(float deltaTime)
        {
            phaseTime += deltaTime;

            switch (phase)
            {
                case Phase.Cast: UpdateCast(); break;
                case Phase.Charge: UpdateCharge(deltaTime); break;
                case Phase.Burst: UpdateBurst(); break;
            }
        }

        private void UpdateCast()
        {
            if (TryCancelIntoDodge()) return;
            if (!fired) FaceDirection(GetAttackFacingDirection());

            // 押しっぱなし → 溜めへ（最初の1発目だけ。連射中は溜めない）
            if (!fired && rightHand && Input.AttackHeld && phaseTime >= holdThreshold)
            {
                SetPhase(Phase.Charge);
                ChargeLevel = 1;
                StartAura();
                return;
            }

            if (!fired && phaseTime >= castFireTime)
            {
                FireBall();
                fired = true;
                firedTime = phaseTime;
            }

            if (!fired) return;
            float since = phaseTime - firedTime;

            // 連射：もう一度 □ → 反対の手で次の火球
            if (since >= comboDelay && Input.ConsumeAttack())
            {
                rightHand = !rightHand;
                StartCast();
                return;
            }

            if (phaseTime >= castDuration || (since >= castMoveCancel && GetMoveDirection().sqrMagnitude > 0.01f))
                ReturnToLocomotion();
        }

        private void UpdateCharge(float deltaTime)
        {
            if (TryCancelIntoDodge()) return;
            FaceDirection(GetAttackFacingDirection());

            int level = 1;
            for (int i = 0; i < chargeTime.Length; i++)
                if (phaseTime >= chargeTime[i]) level = i + 2;
            ChargeLevel = Mathf.Min(level, burstDamage.Length);

            bool outOfStamina = !TryUseStamina(chargeStaminaPerSecond * deltaTime);
            if (!Input.AttackHeld || outOfStamina)
            {
                StopAura();
                SetPhase(Phase.Burst);
                burstDone = false;
            }
        }

        private void UpdateBurst()
        {
            if (!burstDone && phaseTime >= burstHitTime)
            {
                Burst();
                burstDone = true;
            }
            if (burstDone && TryCancelIntoDodge()) return;
            if (phaseTime >= burstDuration) ReturnToLocomotion();
        }

        protected override void OnActionInterrupted()
        {
            StopAura();
            ChargeLevel = 0;
        }

        protected override void OnDisable()
        {
            base.OnDisable();
            StopAura();
        }

        // ================================================================
        //  中身
        // ================================================================

        private void SetPhase(Phase next)
        {
            phase = next;
            phaseTime = 0f;
            switch (next)
            {
                case Phase.Cast: CrossFade(rightHand ? StateCastR : StateCastL, 0.08f); break;
                case Phase.Charge: CrossFade(StateCharge, 0.15f); break;
                case Phase.Burst: CrossFade(StateBurst, 0.05f); break;
            }
        }

        private void FireBall()
        {
            Vector3 from = GetHandPosition(rightHand) + transform.forward * 0.4f;
            Vector3 dir = GetShotDirection(from);

            PlayerProjectile ball = fireballPrefab != null
                ? Instantiate(fireballPrefab, from, Quaternion.LookRotation(dir))
                : CreateFallbackBall(from);
            var info = new DamageInfo(fireballDamage * AttackMultiplier, from, dir, gameObject, causesFlinch: false);
            ball.Launch(dir * fireballSpeed, info, transform);
        }

        /// <summary>周りの相手にまとめてダメージ（1体につき1回）</summary>
        private void Burst()
        {
            int level = Mathf.Clamp(ChargeLevel, 1, burstDamage.Length);
            float radius = burstRadius[Mathf.Min(level, burstRadius.Length) - 1];
            float damage = burstDamage[level - 1] * AttackMultiplier;
            Vector3 center = transform.position + Vector3.up * 1f;

            if (burstEffect != null)
            {
                var fx = Instantiate(burstEffect, transform.position, Quaternion.identity);
                fx.transform.localScale *= radius / Mathf.Max(0.1f, burstEffectBaseRadius);
                Destroy(fx, 3f);
            }

            burstHit.Clear();
            int count = Physics.OverlapSphereNonAlloc(center, radius, overlaps, ~0, QueryTriggerInteraction.Collide);
            // 1周目：部位（頭・尻尾など）を優先、2周目：本体
            for (int pass = 0; pass < 2; pass++)
            for (int i = 0; i < count; i++)
            {
                var col = overlaps[i];
                if (col.transform.IsChildOf(transform)) continue;
                var target = col.GetComponentInParent<IDamageable>();
                if (target == null || !target.IsAlive) continue;
                bool isPart = target is IDamagePart;
                if ((pass == 0) != isPart) continue;
                IDamageable key = isPart ? ((IDamagePart)target).Owner : target;
                if (key == null || !burstHit.Add(key)) continue;

                Vector3 dir = col.bounds.center - transform.position;
                dir.y = 0f;
                var info = new DamageInfo(damage, col.ClosestPointOnBounds(center),
                                          dir.sqrMagnitude > 0.0001f ? dir.normalized : transform.forward,
                                          gameObject, causesFlinch: level >= 3);
                target.ApplyDamage(info);
            }
            ChargeLevel = 0;
        }

        private void StartAura()
        {
            if (chargeAuraEffect == null || aura != null) return;
            aura = Instantiate(chargeAuraEffect, transform.position, Quaternion.identity, transform);
        }

        private void StopAura()
        {
            if (aura == null) return;
            Destroy(aura);
            aura = null;
        }

        private Vector3 GetHandPosition(bool right)
        {
            var bone = Anim != null && Anim.isHuman ? Anim.GetBoneTransform(right ? HumanBodyBones.RightHand : HumanBodyBones.LeftHand) : null;
            return bone != null ? bone.position : transform.position + Vector3.up * 1.3f + transform.right * (right ? 0.3f : -0.3f);
        }

        /// <summary>集中モード → 照準、ロックオン → ターゲット、それ以外 → 正面</summary>
        private Vector3 GetShotDirection(Vector3 from)
        {
            if (IsFocusing) return GetAimDirection(from);
            if (LockTarget != null)
            {
                Vector3 to = LockTarget.AimPosition - from;
                if (to.sqrMagnitude > 0.0001f) return to.normalized;
            }
            return transform.forward;
        }

        /// <summary>火球の Prefab が無いとき用：オレンジの球</summary>
        private static PlayerProjectile CreateFallbackBall(Vector3 position)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.name = "Fireball";
            Destroy(go.GetComponent<Collider>());
            go.transform.position = position;
            go.transform.localScale = Vector3.one * 0.4f;
            var block = new MaterialPropertyBlock();
            block.SetColor("_BaseColor", new Color(1f, 0.45f, 0.1f));
            go.GetComponent<Renderer>().SetPropertyBlock(block);
            return go.AddComponent<PlayerProjectile>();
        }

        private void OnDrawGizmosSelected()
        {
            if (burstRadius == null || burstRadius.Length == 0) return;
            Gizmos.color = new Color(1f, 0.5f, 0.1f, 0.6f);
            Gizmos.DrawWireSphere(transform.position + Vector3.up, burstRadius[burstRadius.Length - 1]);
        }

#if UNITY_EDITOR
        /// <summary>セットアップツール用：クリップの長さに合わせてタイミングとエフェクトを設定する</summary>
        public void EditorSetup(PlayerProjectile fireball, GameObject aura, GameObject burst,
                                float castLoadLength, float castFullLength, float burstLength)
        {
            fireballPrefab = fireball;
            chargeAuraEffect = aura;
            burstEffect = burst;
            castFireTime = Mathf.Max(0.15f, castLoadLength);
            castDuration = Mathf.Max(castFireTime + 0.2f, castFullLength);
            burstDuration = Mathf.Max(0.4f, burstLength);
            burstHitTime = Mathf.Min(0.25f, burstDuration * 0.4f);
            if (holdThreshold >= castFireTime) holdThreshold = castFireTime * 0.7f;
        }
#endif
    }
}
