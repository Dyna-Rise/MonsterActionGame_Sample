using MonsterAction.Combat;
using UnityEngine;

namespace MonsterAction.Battle.Boss
{
    /// <summary>
    /// ボスの「部位」（頭・胴・尻尾など）の当たり判定。ボーンの子に Collider と一緒に付けます。
    /// ここに当たった攻撃は、damageMultiplier 倍になってボス本体（BossBase）のダメージになります。
    ///
    /// ・Collider は「Is Trigger」にしておく（プレイヤーの移動を邪魔しないため）
    /// ・ボーンに付けるので、アニメーションに合わせて一緒に動きます
    /// ・シーンビューでボスを選ぶと、部位の範囲が水色で表示されます
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public class BossHurtbox : MonoBehaviour, IDamageable, IDamagePart
    {
        [SerializeField] private string partName = "Body";
        [Tooltip("この部位に当たったときのダメージ倍率（弱点なら 1.5 など）")]
        [SerializeField] private float damageMultiplier = 1f;

        private BossBase boss;

        public string PartName => partName;
        public float DamageMultiplier => damageMultiplier;
        public bool IsAlive => boss != null && boss.IsAlive;
        public IDamageable Owner => boss;

        private void Awake()
        {
            boss = GetComponentInParent<BossBase>();
            var col = GetComponent<Collider>();
            col.isTrigger = true;
        }

        public bool ApplyDamage(DamageInfo info)
        {
            return boss != null && boss.ApplyPartDamage(info, damageMultiplier, partName);
        }

#if UNITY_EDITOR
        /// <summary>セットアップツール用</summary>
        public void EditorSetup(string part, float multiplier)
        {
            partName = part;
            damageMultiplier = multiplier;
        }
#endif

        private void OnDrawGizmos()
        {
            var sphere = GetComponent<SphereCollider>();
            if (sphere == null) return;
            Gizmos.color = damageMultiplier > 1f ? new Color(1f, 0.4f, 0.4f, 0.6f) : new Color(0.3f, 0.8f, 1f, 0.5f);
            float scale = Mathf.Max(transform.lossyScale.x, transform.lossyScale.y, transform.lossyScale.z);
            Gizmos.DrawWireSphere(transform.TransformPoint(sphere.center), sphere.radius * scale);
        }
    }
}
