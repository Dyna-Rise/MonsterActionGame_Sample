using System.Collections.Generic;
using MonsterAction.Combat;
using UnityEngine;

namespace MonsterAction.Battle.Boss
{
    /// <summary>
    /// ボスの飛び道具（ブレス・投げる岩など）。
    /// Launch で速度・ダメージ・重力を渡すと飛んでいき、プレイヤーに当たればダメージ、地面や壁に当たれば消えます。
    /// 回避の無敵中に当たった場合はすり抜けて飛び続けます。
    ///
    /// 見た目を付けたいときは、このスクリプトを付けた Prefab を作って、ボスのインスペクターに入れてください。
    /// （Prefab が無いときは、色付きの球を自動で作ります）
    /// </summary>
    public class BossProjectile : MonoBehaviour
    {
        [SerializeField] private float radius = 0.5f;
        [SerializeField] private float lifeTime = 6f;
        [SerializeField] private LayerMask hitLayers = ~0;

        private Vector3 velocity;
        private float gravity;
        private DamageInfo template;
        private Transform owner;
        private float age;
        private readonly HashSet<Collider> ignored = new HashSet<Collider>();
        private readonly RaycastHit[] hits = new RaycastHit[16];

        /// <summary>
        /// 発射する。
        /// gravity を負の値（例 -20）にすると放物線を描く（投げた岩など）。
        /// </summary>
        public void Launch(Vector3 velocity, DamageInfo damage, Transform owner, float gravity = 0f)
        {
            this.velocity = velocity;
            this.gravity = gravity;
            this.owner = owner;
            template = damage;
        }

        /// <summary>start から target に、flightTime 秒で届く放物線の初速を計算する</summary>
        public static Vector3 BallisticVelocity(Vector3 start, Vector3 target, float flightTime, float gravity)
        {
            flightTime = Mathf.Max(0.1f, flightTime);
            Vector3 v = (target - start) / flightTime;
            v.y -= 0.5f * gravity * flightTime; // 重力で落ちる分を上向きに足す
            return v;
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

            velocity.y += gravity * dt;
            Vector3 step = velocity * dt;
            float dist = step.magnitude;
            if (dist <= 0f) return;

            int count = Physics.SphereCastNonAlloc(transform.position, radius, step / dist, hits, dist, hitLayers, QueryTriggerInteraction.Ignore);
            System.Array.Sort(hits, 0, count, HitComparer.Instance);

            for (int i = 0; i < count; i++)
            {
                Collider col = hits[i].collider;
                if (ignored.Contains(col)) continue;
                if (owner != null && col.transform.IsChildOf(owner)) continue; // ボス自身は無視

                var target = col.GetComponentInParent<IDamageable>();
                if (target != null)
                {
                    DamageInfo info = template;
                    info.HitPoint = hits[i].point;
                    Vector3 dir = velocity; dir.y = 0f;
                    info.Direction = dir.sqrMagnitude > 0.0001f ? dir.normalized : Vector3.forward;
                    if (target.ApplyDamage(info))
                    {
                        Destroy(gameObject);
                        return;
                    }
                    ignored.Add(col); // 回避された → すり抜ける
                    continue;
                }

                Destroy(gameObject); // 地面・壁
                return;
            }

            transform.position += step;
            if (velocity.sqrMagnitude > 0.0001f) transform.rotation = Quaternion.LookRotation(velocity);
        }

        /// <summary>Prefab が無いとき用：色付きの球を作る</summary>
        public static BossProjectile CreateDefault(Vector3 position, float radius, Color color)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.name = "BossProjectile";
            Destroy(go.GetComponent<Collider>());
            go.transform.position = position;
            go.transform.localScale = Vector3.one * radius * 2f;

            var block = new MaterialPropertyBlock();
            block.SetColor("_BaseColor", color);
            block.SetColor("_Color", color);
            go.GetComponent<Renderer>().SetPropertyBlock(block);

            var p = go.AddComponent<BossProjectile>();
            p.radius = radius;
            return p;
        }

        /// <summary>Prefab か、無ければ色付きの球を、position に作る</summary>
        public static BossProjectile Spawn(BossProjectile prefab, Vector3 position, float radius, Color color)
        {
            return prefab != null ? Instantiate(prefab, position, Quaternion.identity) : CreateDefault(position, radius, color);
        }

        private class HitComparer : IComparer<RaycastHit>
        {
            public static readonly HitComparer Instance = new HitComparer();
            public int Compare(RaycastHit a, RaycastHit b) => a.distance.CompareTo(b.distance);
        }
    }
}
