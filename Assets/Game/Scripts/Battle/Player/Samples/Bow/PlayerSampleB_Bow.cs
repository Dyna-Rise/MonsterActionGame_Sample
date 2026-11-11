using KevinIglesias;
using MonsterAction.Combat;
using UnityEngine;

namespace MonsterAction.Battle.Player.Samples
{
    /// <summary>
    /// （見本・先生用）Player タイプB：弓。課題 PlayerTypeB_Bow の答えの一例です。
    ///
    ///  ・□ を押す        → 弓を引いて（Load）すぐ放つ（溜め 1 段階）
    ///  ・□ を押し続ける  → 引いたまま構える（Hold）。長く溜めるほど段階が上がる（スタミナを使う）
    ///                       1段階：矢1本 / 2段階：強い矢1本 / 3段階：強い矢3本（扇状）
    ///  ・□ を離す        → 放つ（Release）
    ///  ・L1 集中モード中 → 照準（画面中央）へ、ロックオン中 → ターゲットへ、それ以外 → 正面へ撃つ
    ///  ・引いている間・撃った後は ×（回避）でキャンセルできる
    ///
    /// 弓の見た目（弦が引かれる・手の矢が消える）は、モデルに付いている HumanArcherController に任せています。
    /// </summary>
    public class PlayerSampleB_Bow : PlayerBattleController
    {
        private const string StateLoad = "BowLoad";
        private const string StateHold = "BowHold";
        private const string StateRelease = "BowRelease";

        private enum Phase { Load, Hold, Release }

        [Header("弓：矢")]
        [SerializeField] private PlayerProjectile arrowPrefab;
        [SerializeField] private float arrowSpeed = 45f;
        [Tooltip("矢の落ち方（0 でまっすぐ。負の値で放物線）")]
        [SerializeField] private float arrowGravity = -2f;
        [Tooltip("3段階目の矢の広がり（度）")]
        [SerializeField] private float spreadAngle = 7f;

        [Header("弓：溜め")]
        [Tooltip("段階ごとのダメージ（1段階, 2段階, 3段階）。3段階は1本あたり")]
        [SerializeField] private float[] chargeDamage = { 10f, 18f, 14f };
        [Tooltip("2段階・3段階になるまでの溜め時間（秒、Hold に入ってから）")]
        [SerializeField] private float[] chargeTime = { 0.5f, 1.2f };
        [Tooltip("引いている間、1秒あたりに使うスタミナ。切れたら自動で放つ")]
        [SerializeField] private float holdStaminaPerSecond = 15f;
        [Tooltip("溜めている間に向きを変える速さ（集中モード中は照準の向き）")]
        [SerializeField] private bool turnWhileHolding = true;

        [Header("弓：タイミング（秒）")]
        [SerializeField] private float loadDuration = 0.45f;
        [SerializeField] private float releaseDuration = 0.5f;
        [Tooltip("Release に入ってから矢が出るまで")]
        [SerializeField] private float releaseFireTime = 0.03f;
        [Tooltip("撃った後、移動でキャンセルできるまで")]
        [SerializeField] private float releaseMoveCancel = 0.3f;

        [Header("参照（空なら自動取得）")]
        [SerializeField] private HumanArcherController archer;
        [Tooltip("矢が出る位置（空なら弦の位置）")]
        [SerializeField] private Transform muzzle;

        /// <summary>今の溜め段階（1〜3。溜めていなければ 0）。UI に出したいとき用</summary>
        public int ChargeLevel { get; private set; }

        private Phase phase;
        private float phaseTime;
        private bool fired;
        private bool releaseRequested;

        protected override void Awake()
        {
            base.Awake();
            if (archer == null) archer = GetComponentInChildren<HumanArcherController>(true);
            if (archer != null) archer.animationToPlay = ArcherAnimation.Nothing; // デモ用のアニメ切り替えは使わない
        }

        // ================================================================
        //  開始
        // ================================================================

        protected override bool TryStartAction()
        {
            if (!Input.ConsumeAttack()) return false;

            BeginAction();
            FaceDirection(GetAttackFacingDirection(), instant: true);
            SetPhase(Phase.Load);
            ChargeLevel = 1;
            fired = false;
            releaseRequested = false;
            if (archer != null) archer.LoadBow(0f, loadDuration);
            return true;
        }

        // ================================================================
        //  毎フレーム
        // ================================================================

        protected override void UpdateAction(float deltaTime)
        {
            phaseTime += deltaTime;

            switch (phase)
            {
                case Phase.Load:
                    if (TryCancelIntoDodge()) return;
                    TurnToAim();
                    if (!Input.AttackHeld) releaseRequested = true; // 短押し：引き終わったらすぐ放つ
                    if (phaseTime >= loadDuration)
                    {
                        if (releaseRequested) SetPhase(Phase.Release);
                        else SetPhase(Phase.Hold);
                    }
                    break;

                case Phase.Hold:
                    if (TryCancelIntoDodge()) return;
                    TurnToAim();
                    UpdateChargeLevel();
                    // 離した / スタミナが切れた → 放つ
                    bool outOfStamina = !TryUseStamina(holdStaminaPerSecond * deltaTime);
                    if (!Input.AttackHeld || outOfStamina) SetPhase(Phase.Release);
                    break;

                case Phase.Release:
                    if (!fired && phaseTime >= releaseFireTime) Fire();
                    if (fired && TryCancelIntoDodge()) return;
                    // 撃った直後にもう一度 □ → すぐ次を引く（連射）
                    if (fired && phaseTime >= releaseMoveCancel && Input.ConsumeAttack())
                    {
                        TryStartActionAgain();
                        return;
                    }
                    if (phaseTime >= releaseDuration ||
                        (phaseTime >= releaseMoveCancel && GetMoveDirection().sqrMagnitude > 0.01f))
                    {
                        ReturnToLocomotion();
                    }
                    break;
            }
        }

        protected override void OnActionInterrupted()
        {
            // 回避・被弾で中断：弦を戻し、手に矢を戻す
            if (archer != null)
            {
                if (!fired) archer.CancelLoadBow(0f, 0.15f);
                archer.GetArrow(0.2f);
            }
            ChargeLevel = 0;
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
                case Phase.Load: CrossFade(StateLoad, 0.1f); break;
                case Phase.Hold: CrossFade(StateHold, 0.15f); break;
                case Phase.Release: CrossFade(StateRelease, 0.05f); break;
            }
        }

        private void TryStartActionAgain()
        {
            Input.ClearBuffer();
            BeginAction();
            FaceDirection(GetAttackFacingDirection(), instant: true);
            SetPhase(Phase.Load);
            ChargeLevel = 1;
            fired = false;
            releaseRequested = false;
            if (archer != null) archer.LoadBow(0f, loadDuration);
        }

        private void UpdateChargeLevel()
        {
            int level = 1;
            for (int i = 0; i < chargeTime.Length; i++)
                if (phaseTime >= chargeTime[i]) level = i + 2;
            ChargeLevel = Mathf.Min(level, chargeDamage.Length);
        }

        private void TurnToAim()
        {
            if (!turnWhileHolding) return;
            FaceDirection(GetAttackFacingDirection());
        }

        private void Fire()
        {
            fired = true;
            int level = Mathf.Clamp(ChargeLevel, 1, chargeDamage.Length);
            float damage = chargeDamage[level - 1] * AttackMultiplier;

            Vector3 from = GetMuzzlePosition();
            Vector3 dir = GetShotDirection(from);

            if (level >= 3)
            {
                for (int i = -1; i <= 1; i++)
                    SpawnArrow(from, Quaternion.AngleAxis(spreadAngle * i, Vector3.up) * dir, damage, flinch: i == 0);
            }
            else
            {
                SpawnArrow(from, dir, damage, flinch: level >= 2);
            }

            // 弓の見た目：弦を戻す（手の矢を隠して、少し後に戻す）
            if (archer != null)
            {
                archer.CancelLoadBow(0f, 0.08f);
                if (archer.arrowInHand != null) archer.arrowInHand.SetActive(false);
                archer.GetArrow(0.35f);
            }
            ChargeLevel = 0;
        }

        private void SpawnArrow(Vector3 from, Vector3 dir, float damage, bool flinch)
        {
            PlayerProjectile arrow = arrowPrefab != null
                ? Instantiate(arrowPrefab, from, Quaternion.LookRotation(dir))
                : CreateFallbackArrow(from);
            var info = new DamageInfo(damage, from, dir, gameObject, causesFlinch: flinch);
            arrow.Launch(dir * arrowSpeed, info, transform, arrowGravity);
        }

        private Vector3 GetMuzzlePosition()
        {
            if (muzzle != null) return muzzle.position;
            if (archer != null && archer.nockPoint != null) return archer.nockPoint.position + transform.forward * 0.3f;
            return transform.position + Vector3.up * 1.45f + transform.forward * 0.6f;
        }

        /// <summary>集中モード → 照準、ロックオン → ターゲット、それ以外 → 正面（胸の高さ）</summary>
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

        /// <summary>矢の Prefab が無いとき用：細い棒</summary>
        private static PlayerProjectile CreateFallbackArrow(Vector3 position)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = "Arrow";
            Destroy(go.GetComponent<Collider>());
            go.transform.position = position;
            go.transform.localScale = new Vector3(0.03f, 0.03f, 0.8f);
            return go.AddComponent<PlayerProjectile>();
        }

#if UNITY_EDITOR
        /// <summary>セットアップツール用：クリップの長さに合わせてタイミングを設定する</summary>
        public void EditorSetup(PlayerProjectile arrow, float loadLength, float releaseLength)
        {
            arrowPrefab = arrow;
            loadDuration = Mathf.Max(0.2f, loadLength);
            releaseDuration = Mathf.Max(0.3f, releaseLength);
        }
#endif
    }
}
