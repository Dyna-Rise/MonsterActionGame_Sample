using System;
using System.Collections.Generic;
using MonsterAction.Combat;
using UnityEngine;

namespace MonsterAction.Battle.Player
{
    /// <summary>
    /// 武器の攻撃判定（カプセル形）。武器のモデル（剣など）に付けます。
    /// SetActive(true) の間、毎フレーム OverlapCapsule で当たりを調べ、IDamageable にダメージを与えます。
    /// 1回の振り（BeginSwing 〜 次の BeginSwing）の中では、同じ相手には1回しか当たりません。
    ///
    /// ・Physics のトリガーイベントを使わないので、相手側には Collider さえあれば OK
    ///   （Rigidbody 不要。Boss の部位ごとに Collider を付ければ部位判定もできる）
    /// ・シーンビューで武器を選択すると判定の形が表示されます（攻撃判定中は赤）
    /// </summary>
    public class WeaponHitbox : MonoBehaviour
    {
        [Tooltip("カプセルの端点A（この Transform のローカル座標）")]
        public Vector3 pointA = Vector3.zero;
        [Tooltip("カプセルの端点B（この Transform のローカル座標）")]
        public Vector3 pointB = new Vector3(0f, 0.8f, 0f);
        public float radius = 0.12f;
        [Tooltip("当たり判定を取るレイヤー")]
        public LayerMask hitLayers = ~0;

        /// <summary>何かに当たったとき（ヒットエフェクト・ヒットストップ・SE などを付けたいときに使う）</summary>
        public event Action<IDamageable, DamageInfo> Hit;

        public bool IsActive { get; private set; }

        private Transform owner;
        private DamageInfo template;
        private readonly HashSet<IDamageable> alreadyHit = new HashSet<IDamageable>();
        private readonly Collider[] results = new Collider[32];
        private Vector3 prevA, prevB;
        private bool hasPrev;

        /// <summary>持ち主を設定（持ち主自身の Collider には当たらない）</summary>
        public void SetOwner(Transform ownerRoot) => owner = ownerRoot;

        /// <summary>新しい振りを始める。ヒット済みリストをリセットする</summary>
        public void BeginSwing(float damage, bool causesFlinch, float knockback = 0f)
        {
            alreadyHit.Clear();
            template = new DamageInfo(damage, Vector3.zero, Vector3.zero,
                                      owner != null ? owner.gameObject : gameObject, causesFlinch, knockback);
        }

        public void SetActive(bool active)
        {
            if (active && !IsActive) hasPrev = false;
            IsActive = active;
        }

        // アニメーション適用後のボーン位置で判定したいので LateUpdate
        private void LateUpdate()
        {
            if (!IsActive) return;

            Vector3 a = transform.TransformPoint(pointA);
            Vector3 b = transform.TransformPoint(pointB);

            Check(a, b);
            // 速い振りですり抜けないよう、前フレームとの中間位置も調べる
            if (hasPrev) Check((a + prevA) * 0.5f, (b + prevB) * 0.5f);

            prevA = a; prevB = b; hasPrev = true;
        }

        private void Check(Vector3 a, Vector3 b)
        {
            int count = Physics.OverlapCapsuleNonAlloc(a, b, radius, results, hitLayers, QueryTriggerInteraction.Collide);

            // 1周目：部位（ボスの頭・尻尾など）を優先、2周目：それ以外（本体のコライダーなど）
            for (int pass = 0; pass < 2; pass++)
            for (int i = 0; i < count; i++)
            {
                Collider col = results[i];
                if (owner != null && col.transform.IsChildOf(owner)) continue;

                var target = col.GetComponentInParent<IDamageable>();
                if (target == null || !target.IsAlive) continue;
                bool isPart = target is IDamagePart;
                if ((pass == 0) != isPart) continue;

                // 1回の振りで、同じ相手（部位なら持ち主）には1回だけ
                IDamageable key = isPart ? ((IDamagePart)target).Owner : target;
                if (key == null || alreadyHit.Contains(key)) continue;
                alreadyHit.Add(key);

                Vector3 center = (a + b) * 0.5f;
                DamageInfo info = template;
                info.HitPoint = col.ClosestPointOnBounds(center);
                Vector3 dir = col.bounds.center - (owner != null ? owner.position : transform.position);
                dir.y = 0f;
                info.Direction = dir.sqrMagnitude > 0.0001f ? dir.normalized : transform.forward;

                if (target.ApplyDamage(info)) Hit?.Invoke(target, info);
            }
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = IsActive ? Color.red : new Color(1f, 0.6f, 0f, 0.8f);
            Vector3 a = transform.TransformPoint(pointA);
            Vector3 b = transform.TransformPoint(pointB);
            Gizmos.DrawWireSphere(a, radius);
            Gizmos.DrawWireSphere(b, radius);
            Gizmos.DrawLine(a, b);
        }
    }
}
