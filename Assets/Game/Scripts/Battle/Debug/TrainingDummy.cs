using MonsterAction.Combat;
using UnityEngine;

namespace MonsterAction.Battle.DebugTools
{
    /// <summary>
    /// テスト用の打たれ役（カカシ）。Collider と一緒に置くと、プレイヤーの攻撃が当たったか確認できます。
    /// ボス担当の人は、これを IDamageable の実装例として参考にしてください。
    /// </summary>
    public class TrainingDummy : MonoBehaviour, IDamageable
    {
        [SerializeField] private bool logDamage = true;
        [SerializeField] private float punchScale = 0.15f;
        [Tooltip("合計ダメージがこの値に達したらクエストクリア（0 なら倒れない）。クリアの流れのテスト用")]
        [SerializeField] private float questClearDamage = 0f;

        private float totalDamage;
        private int hitCount;
        private Vector3 baseScale;
        private float punch;

        public bool IsAlive => true;

        private void Awake() => baseScale = transform.localScale;

        public bool ApplyDamage(DamageInfo info)
        {
            totalDamage += info.Amount;
            hitCount++;
            punch = 1f;
            if (logDamage)
                Debug.Log($"[TrainingDummy] {info.Amount} ダメージ（{hitCount}ヒット / 合計 {totalDamage}）", this);

            // ボスが倒れたときと同じように、クエストクリアを知らせる
            if (questClearDamage > 0f && totalDamage >= questClearDamage && Flow.BattleFlowController.Instance != null)
                Flow.BattleFlowController.Instance.ClearQuest();
            return true;
        }

        private void Update()
        {
            if (punch <= 0f) return;
            punch = Mathf.Max(0f, punch - Time.deltaTime * 6f);
            transform.localScale = baseScale * (1f + punchScale * punch);
        }
    }
}
