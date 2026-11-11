using System.Collections.Generic;
using MonsterAction.Combat;
using UnityEngine;

namespace MonsterAction.Battle.DebugTools
{
    /// <summary>
    /// まっすぐ飛ぶ弾。IDamageable に当たったらダメージを与えて消える。
    /// 回避の無敵中に当たった場合はすり抜けて飛び続ける。地面や壁に当たったら消える。
    ///
    /// Rigidbody を使わず、毎フレーム「前の位置 → 今の位置」を SphereCast して当たりを調べています。
    /// （速い弾でもすり抜けにくい。弓担当の矢やボスの弾の参考にどうぞ）
    /// </summary>
    public class DummyProjectile : MonoBehaviour
    {
        [SerializeField] private float radius = 0.25f;
        [SerializeField] private float lifeTime = 5f;
        [SerializeField] private LayerMask hitLayers = ~0;

        private Vector3 velocity;
        private float damage;
        private GameObject owner;
        private float age;
        private readonly HashSet<Collider> ignored = new HashSet<Collider>();
        private readonly RaycastHit[] hits = new RaycastHit[16];

        /// <summary>発射する</summary>
        public void Launch(Vector3 velocity, float damage, GameObject owner)
        {
            this.velocity = velocity;
            this.damage = damage;
            this.owner = owner;
            if (velocity.sqrMagnitude > 0.0001f) transform.rotation = Quaternion.LookRotation(velocity);
        }

        private void Update()
        {
            float dt = Time.deltaTime;
            age += dt;
            if (age > lifeTime)
            {
                Destroy(gameObject);
                return;
            }

            Vector3 step = velocity * dt;
            float dist = step.magnitude;
            if (dist <= 0f) return;

            int count = Physics.SphereCastNonAlloc(transform.position, radius, step / dist, hits, dist, hitLayers, QueryTriggerInteraction.Collide); // ボスの部位（トリガー）にも当たる
            System.Array.Sort(hits, 0, count, HitDistanceComparer.Instance); // 近い順

            for (int i = 0; i < count; i++)
            {
                Collider col = hits[i].collider;
                if (ignored.Contains(col)) continue;
                if (owner != null && col.transform.IsChildOf(owner.transform)) continue;

                var target = col.GetComponentInParent<IDamageable>();
                if (target != null)
                {
                    var info = new DamageInfo(damage, hits[i].point, velocity.normalized, owner, causesFlinch: true, knockbackDistance: 1f);
                    if (target.ApplyDamage(info))
                    {
                        Destroy(gameObject);
                        return;
                    }
                    ignored.Add(col); // 回避された → すり抜ける
                    continue;
                }

                // 地面・壁など
                Destroy(gameObject);
                return;
            }

            transform.position += step;
        }

        /// <summary>Prefab が無いとき用：色付きの球を作る</summary>
        public static DummyProjectile CreateDefault(Vector3 position, float radius, Color color)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.name = "DummyProjectile";
            Destroy(go.GetComponent<Collider>()); // 当たり判定は SphereCast で行う
            go.transform.position = position;
            go.transform.localScale = Vector3.one * radius * 2f;

            var block = new MaterialPropertyBlock();
            block.SetColor("_BaseColor", color);
            block.SetColor("_Color", color);
            go.GetComponent<Renderer>().SetPropertyBlock(block);

            var p = go.AddComponent<DummyProjectile>();
            p.radius = radius;
            return p;
        }

        private class HitDistanceComparer : IComparer<RaycastHit>
        {
            public static readonly HitDistanceComparer Instance = new HitDistanceComparer();
            public int Compare(RaycastHit a, RaycastHit b) => a.distance.CompareTo(b.distance);
        }
    }
}
