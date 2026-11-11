using System.Collections.Generic;
using UnityEngine;

namespace MonsterAction.Combat
{
    /// <summary>
    /// ターゲットカメラ（ロックオン）の対象になれる印。
    ///
    /// 【Boss 担当の人へ】
    /// ボスのルートにこのコンポーネントを付けてください。
    /// aimPoint にはカメラが注目する位置（胸・頭など）の Transform を入れます（空ならこの Transform + offset）。
    /// 同じ GameObject か親に IDamageable があれば、死亡したら自動でロックオン対象から外れます。
    /// </summary>
    public class LockOnTarget : MonoBehaviour
    {
        private static readonly List<LockOnTarget> active = new List<LockOnTarget>();

        /// <summary>現在ロックオン可能な全ターゲット</summary>
        public static IReadOnlyList<LockOnTarget> All => active;

        [Tooltip("カメラが注目する位置。空なら この Transform + offset")]
        [SerializeField] private Transform aimPoint;
        [SerializeField] private Vector3 offset = new Vector3(0f, 1f, 0f);
        [Tooltip("ロックオンの優先度（大きいほど優先。ボス本体を部位より優先したいときなど）")]
        [SerializeField] private int priority = 0;

        private IDamageable damageable;

        /// <summary>カメラ・照準が狙う位置</summary>
        public Vector3 AimPosition => aimPoint != null ? aimPoint.position : transform.TransformPoint(offset);

        public int Priority => priority;

        /// <summary>ロックオンできる状態か</summary>
        public bool IsTargetable => isActiveAndEnabled && (damageable == null || damageable.IsAlive);

        private void Awake() => damageable = GetComponentInParent<IDamageable>();
        private void OnEnable() => active.Add(this);
        private void OnDisable() => active.Remove(this);

        /// <summary>
        /// from から見て、viewForward の方向に近く、距離の近いターゲットを選ぶ。
        /// 見つからなければ null。
        /// </summary>
        public static LockOnTarget FindBest(Vector3 from, Vector3 viewForward, float maxDistance)
        {
            LockOnTarget best = null;
            float bestScore = float.MaxValue;
            viewForward.y = 0f;
            viewForward.Normalize();

            foreach (var t in active)
            {
                if (!t.IsTargetable) continue;
                Vector3 to = t.AimPosition - from;
                float dist = to.magnitude;
                if (dist > maxDistance) continue;

                to.y = 0f;
                float angle = to.sqrMagnitude > 0.0001f ? Vector3.Angle(viewForward, to) : 0f;
                // 画面中央に近いほど・近いほど・優先度が高いほど選ばれやすい
                float score = angle * 1f + dist * 0.5f - t.priority * 1000f;
                if (score < bestScore)
                {
                    bestScore = score;
                    best = t;
                }
            }
            return best;
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(AimPosition, 0.3f);
        }
    }
}
