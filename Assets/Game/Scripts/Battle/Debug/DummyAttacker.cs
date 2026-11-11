using MonsterAction.Battle.Flow;
using MonsterAction.Battle.Player;
using MonsterAction.Combat;
using UnityEngine;

namespace MonsterAction.Battle.DebugTools
{
    /// <summary>
    /// カカシに攻撃させるテスト用 AI。被弾・回避・ゲームオーバーの確認に使います。
    ///
    ///   ・プレイヤーが遠い → 予兆（黄色に光る）のあと、球を撃ってくる
    ///   ・プレイヤーが近い → 予兆（赤く光る）のあと、周囲を殴る（ノックバック付き）
    ///
    /// 【Boss 担当の人へ】
    /// 「距離で行動を選ぶ → 予兆 → 攻撃 → 待ち」という一番単純なボス AI の形です。
    /// プレイヤーへのダメージは IDamageable.ApplyDamage(DamageInfo) を呼ぶだけ、という例にもなっています。
    /// </summary>
    public class DummyAttacker : MonoBehaviour
    {
        private enum Phase { Wait, Telegraph }
        private enum AttackKind { Shoot, Melee }

        [SerializeField] private bool attackEnabled = true;

        [Header("距離")]
        [Tooltip("この距離より近ければ近接攻撃")]
        [SerializeField] private float meleeRange = 3f;
        [Tooltip("この距離より遠ければ何もしない")]
        [SerializeField] private float shootRange = 30f;

        [Header("射撃")]
        [SerializeField] private DummyProjectile projectilePrefab; // 空なら球を自動で作る
        [SerializeField] private float shootInterval = 2.5f;
        [SerializeField] private float shootTelegraph = 0.6f;
        [SerializeField] private float shootDamage = 20f;
        [SerializeField] private float projectileSpeed = 14f;
        [SerializeField] private float projectileRadius = 0.25f;
        [SerializeField] private Color shootColor = new Color(1f, 0.85f, 0.2f);

        [Header("近接")]
        [SerializeField] private float meleeInterval = 2f;
        [SerializeField] private float meleeTelegraph = 0.7f;
        [SerializeField] private float meleeDamage = 30f;
        [Tooltip("攻撃が届く半径（カカシの中心から）")]
        [SerializeField] private float meleeRadius = 3.5f;
        [SerializeField] private float meleeKnockback = 3f;
        [SerializeField] private Color meleeColor = new Color(1f, 0.25f, 0.2f);

        [Header("見た目")]
        [Tooltip("弾を撃つ高さ（カカシの中心から）")]
        [SerializeField] private Vector3 muzzleOffset = new Vector3(0f, 0.6f, 0f);

        private Phase phase = Phase.Wait;
        private AttackKind kind;
        private float timer = 1.5f; // 最初の攻撃までの待ち
        private Renderer[] renderers;
        private MaterialPropertyBlock block;
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int ColorId = Shader.PropertyToID("_Color");

        private void Awake()
        {
            renderers = GetComponentsInChildren<Renderer>();
            block = new MaterialPropertyBlock();
        }

        private void Update()
        {
            var player = PlayerBattleController.Active;
            bool canAct = attackEnabled && player != null && player.IsAlive &&
                          (BattleFlowController.Instance == null ||
                           BattleFlowController.Instance.CurrentPhase == BattleFlowController.Phase.Playing);
            if (!canAct)
            {
                phase = Phase.Wait;
                SetTint(null);
                return;
            }

            Vector3 toPlayer = player.transform.position - transform.position;
            toPlayer.y = 0f;
            float dist = toPlayer.magnitude;
            if (toPlayer.sqrMagnitude > 0.01f) transform.rotation = Quaternion.LookRotation(toPlayer); // プレイヤーの方を向く

            timer -= Time.deltaTime;

            switch (phase)
            {
                case Phase.Wait:
                    if (timer > 0f || dist > shootRange) break;
                    // 距離で攻撃を選んで、予兆に入る
                    kind = dist <= meleeRange ? AttackKind.Melee : AttackKind.Shoot;
                    phase = Phase.Telegraph;
                    timer = kind == AttackKind.Melee ? meleeTelegraph : shootTelegraph;
                    SetTint(kind == AttackKind.Melee ? meleeColor : shootColor);
                    break;

                case Phase.Telegraph:
                    // 予兆中は点滅
                    bool on = Mathf.Repeat(Time.time * 10f, 1f) < 0.6f;
                    SetTint(on ? (kind == AttackKind.Melee ? meleeColor : shootColor) : (Color?)null);
                    if (timer > 0f) break;

                    if (kind == AttackKind.Melee) DoMelee(player);
                    else DoShoot(player);

                    SetTint(null);
                    phase = Phase.Wait;
                    timer = kind == AttackKind.Melee ? meleeInterval : shootInterval;
                    break;
            }
        }

        private void DoShoot(PlayerBattleController player)
        {
            Vector3 muzzle = transform.position + muzzleOffset;
            Vector3 target = player.transform.position + Vector3.up * 1.0f; // 胸のあたり
            Vector3 dir = (target - muzzle).normalized;

            DummyProjectile projectile = projectilePrefab != null
                ? Instantiate(projectilePrefab, muzzle, Quaternion.LookRotation(dir))
                : DummyProjectile.CreateDefault(muzzle, projectileRadius, shootColor);
            projectile.Launch(dir * projectileSpeed, shootDamage, gameObject);
        }

        private void DoMelee(PlayerBattleController player)
        {
            // 見た目：一瞬ふくらむ
            transform.localScale = baseScale * 1.25f;
            Invoke(nameof(ResetScale), 0.12f);

            Vector3 toPlayer = player.transform.position - transform.position;
            toPlayer.y = 0f;
            if (toPlayer.magnitude > meleeRadius) return; // 届かなかった（回避で離れた）

            var info = new DamageInfo(meleeDamage, player.transform.position + Vector3.up,
                                      toPlayer.normalized, gameObject, causesFlinch: true, knockbackDistance: meleeKnockback);
            player.ApplyDamage(info); // 回避の無敵中なら false が返ってダメージなし
        }

        private Vector3 baseScale;
        private void Start() => baseScale = transform.localScale;
        private void ResetScale() => transform.localScale = baseScale;

        private void SetTint(Color? color)
        {
            foreach (var r in renderers)
            {
                if (color.HasValue)
                {
                    block.Clear();
                    block.SetColor(BaseColorId, color.Value);
                    block.SetColor(ColorId, color.Value);
                    r.SetPropertyBlock(block);
                }
                else
                {
                    r.SetPropertyBlock(null);
                }
            }
        }

        private void OnDisable() => SetTint(null);

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(transform.position, meleeRange);
            Gizmos.color = new Color(1f, 0.85f, 0.2f);
            Gizmos.DrawWireSphere(transform.position, shootRange);
        }
    }
}
