using System.Collections.Generic;
using MonsterAction.Combat;
using UnityEngine;

namespace MonsterAction.Battle.Player.Samples
{
    /// <summary>
    /// （見本）プレイヤーの飛び道具（矢・火球）。
    /// Launch で速度とダメージを渡すと飛んでいき、
    ///   ・ボスなど IDamageable に当たる → ダメージ（部位＝BossHurtbox を優先）
    ///   ・地面や壁に当たる               → 止まる（矢なら刺さったまま少し残る）
    /// 1フレームで進む分を SphereCast で調べるので、速くてもすり抜けません。
    /// </summary>
    public class PlayerProjectile : MonoBehaviour
    {
        [SerializeField] private float radius = 0.15f;
        [SerializeField] private float lifeTime = 4f;
        [SerializeField] private LayerMask hitLayers = ~0;

        [Header("当たったとき")]
        [Tooltip("ON：当たった物に刺さって stuckTime 秒残る（矢）。OFF：すぐ消える（火球）")]
        [SerializeField] private bool stickOnHit = true;
        [SerializeField] private float stuckTime = 3f;
        [Tooltip("当たった場所に出すエフェクト（無くてもよい）")]
        [SerializeField] private GameObject impactEffect;
        [SerializeField] private float impactEffectScale = 1f;
        [SerializeField] private float impactEffectLifeTime = 2f;

        private Vector3 velocity;
        private float gravity;
        private DamageInfo template;
        private Transform owner;
        private float age;
        private bool launched;
        private bool stopped;
        private readonly RaycastHit[] hits = new RaycastHit[24];
        private readonly Collider[] overlaps = new Collider[16];

        /// <summary>
        /// 発射する。damage.Amount にダメージ、owner に撃った人（自分には当たらない）。
        /// gravity を負にすると放物線になる。
        /// </summary>
        public void Launch(Vector3 velocity, DamageInfo damage, Transform owner, float gravity = 0f)
        {
            this.velocity = velocity;
            this.gravity = gravity;
            this.owner = owner;
            template = damage;
            launched = true;
            if (velocity.sqrMagnitude > 0.0001f) transform.rotation = Quaternion.LookRotation(velocity);

            // 相手に密着して撃ったとき（発射位置が相手の中）も当たるように
            CheckOverlapAtStart();
        }

        private void Update()
        {
            if (!launched || stopped) return;

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
            Vector3 dir = step / dist;

            int count = Physics.SphereCastNonAlloc(transform.position, radius, dir, hits, dist, hitLayers, QueryTriggerInteraction.Collide);

            // 一番手前の「壁・地面」までの距離
            float wallDist = float.MaxValue;
            int wallIndex = -1;
            for (int i = 0; i < count; i++)
            {
                var col = hits[i].collider;
                if (IsOwn(col) || col.isTrigger) continue;
                if (col.GetComponentInParent<IDamageable>() != null) continue;
                if (hits[i].distance < wallDist) { wallDist = hits[i].distance; wallIndex = i; }
            }

            // 壁より手前の相手：部位を優先、無ければ本体
            int best = -1;
            bool bestIsPart = false;
            for (int i = 0; i < count; i++)
            {
                var col = hits[i].collider;
                if (IsOwn(col) || hits[i].distance > wallDist) continue;
                var target = col.GetComponentInParent<IDamageable>();
                if (target == null || !target.IsAlive) continue;
                bool isPart = target is IDamagePart;
                if (best < 0 || (isPart && !bestIsPart) || (isPart == bestIsPart && hits[i].distance < hits[best].distance))
                {
                    best = i;
                    bestIsPart = isPart;
                }
            }

            if (best >= 0)
            {
                var hit = hits[best];
                if (TryDamage(hit.collider, hit.point == Vector3.zero ? transform.position : hit.point, dir))
                {
                    Stop(hit.point == Vector3.zero ? transform.position : hit.point, hit.collider.transform);
                    return;
                }
            }

            if (wallIndex >= 0)
            {
                Vector3 p = transform.position + dir * wallDist;
                Stop(p, hits[wallIndex].collider.transform);
                return;
            }

            transform.position += step;
            transform.rotation = Quaternion.LookRotation(velocity);
        }

        private void CheckOverlapAtStart()
        {
            int count = Physics.OverlapSphereNonAlloc(transform.position, radius, overlaps, hitLayers, QueryTriggerInteraction.Collide);
            Collider body = null;
            for (int i = 0; i < count; i++)
            {
                var col = overlaps[i];
                if (IsOwn(col)) continue;
                var target = col.GetComponentInParent<IDamageable>();
                if (target == null || !target.IsAlive) continue;
                if (target is IDamagePart) { body = col; break; }
                if (body == null) body = col;
            }
            if (body != null && TryDamage(body, transform.position, velocity.normalized))
                Stop(transform.position, body.transform);
        }

        private bool TryDamage(Collider col, Vector3 point, Vector3 dir)
        {
            var target = col.GetComponentInParent<IDamageable>();
            if (target == null) return false;
            DamageInfo info = template;
            info.HitPoint = point;
            dir.y = 0f;
            info.Direction = dir.sqrMagnitude > 0.0001f ? dir.normalized : transform.forward;
            return target.ApplyDamage(info);
        }

        private void Stop(Vector3 point, Transform hitTransform)
        {
            stopped = true;
            transform.position = point;

            if (impactEffect != null)
            {
                var fx = Instantiate(impactEffect, point, Quaternion.identity);
                fx.transform.localScale *= impactEffectScale;
                Destroy(fx, impactEffectLifeTime);
            }

            if (stickOnHit)
            {
                // 刺さった物と一緒に動く（ボスに刺さった矢がボスについていく）
                if (hitTransform != null) transform.SetParent(hitTransform, true);
                Destroy(gameObject, stuckTime);
            }
            else
            {
                // 火球：見た目（パーティクル）を止めて、尾を引いた分が消えるまで少し待つ
                foreach (var ps in GetComponentsInChildren<ParticleSystem>())
                    ps.Stop(true, ParticleSystemStopBehavior.StopEmitting);
                foreach (var r in GetComponentsInChildren<MeshRenderer>()) r.enabled = false;
                foreach (var l in GetComponentsInChildren<Light>()) l.enabled = false;
                Destroy(gameObject, 1f);
            }
        }

        private bool IsOwn(Collider col) => owner != null && col.transform.IsChildOf(owner);

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(transform.position, radius);
        }
    }
}
