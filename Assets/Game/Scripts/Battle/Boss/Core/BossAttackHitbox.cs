using MonsterAction.Combat;
using UnityEngine;

namespace MonsterAction.Battle.Boss
{
    /// <summary>
    /// ボスの攻撃判定（球）。口・爪・頭などのボーンの子に付けます。
    /// Begin(ダメージ, ...) から End() までの間、範囲内のプレイヤーにダメージを与えます（1回の攻撃で1ヒットまで）。
    ///
    /// ・半径はワールド座標の m 単位（ボーンのスケールに影響されない）
    /// ・シーンビューでボスを選ぶと、判定の範囲が表示されます（攻撃中は赤）
    /// </summary>
    public class BossAttackHitbox : MonoBehaviour
    {
        [Tooltip("判定の半径（m）")]
        [SerializeField] private float radius = 1.2f;
        [SerializeField] private LayerMask hitLayers = ~0;

        public bool IsActive { get; private set; }

        private Transform owner;
        private DamageInfo template;
        private bool hasHit;
        private readonly Collider[] results = new Collider[16];

        public void SetOwner(Transform ownerRoot) => owner = ownerRoot;

        /// <summary>
        /// 攻撃判定 ON。
        /// knockDown = true にすると「大攻撃」になり、当たったプレイヤーはダウンする。
        /// </summary>
        public void Begin(float damage, float knockback = 1f, bool causesFlinch = true, bool knockDown = false)
        {
            template = new DamageInfo(damage, Vector3.zero, Vector3.zero,
                                      owner != null ? owner.gameObject : gameObject, causesFlinch, knockback, knockDown);
            hasHit = false;
            IsActive = true;
        }

        /// <summary>攻撃判定 OFF</summary>
        public void End() => IsActive = false;

        private void LateUpdate()
        {
            if (!IsActive || hasHit) return;

            int count = Physics.OverlapSphereNonAlloc(transform.position, radius, results, hitLayers, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
            {
                Collider col = results[i];
                if (owner != null && col.transform.IsChildOf(owner)) continue; // 自分の体は無視

                var target = col.GetComponentInParent<IDamageable>();
                if (target == null || !target.IsAlive) continue;

                DamageInfo info = template;
                info.HitPoint = col.ClosestPointOnBounds(transform.position);
                Vector3 dir = col.transform.position - (owner != null ? owner.position : transform.position);
                dir.y = 0f;
                info.Direction = dir.sqrMagnitude > 0.0001f ? dir.normalized : transform.forward;

                if (target.ApplyDamage(info))
                {
                    hasHit = true; // ガード・回避されなかったら、この攻撃はもう当たらない
                    return;
                }
            }
        }

#if UNITY_EDITOR
        public void EditorSetup(float r) => radius = r;
#endif

        private void OnDrawGizmos()
        {
            Gizmos.color = IsActive ? Color.red : new Color(1f, 0.6f, 0f, 0.5f);
            Gizmos.DrawWireSphere(transform.position, radius);
        }
    }
}
