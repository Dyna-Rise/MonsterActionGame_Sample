using MonsterAction.Battle.Boss;
using UnityEngine;
using UnityEngine.UI;

namespace MonsterAction.Battle.UI
{
    /// <summary>
    /// ボスの名前と HP バー（BattleHUD の BossBar に付いています）。
    /// シーンにボス（BossBase）がいれば表示、いなければ隠します。
    /// </summary>
    public class BossStatusHUD : MonoBehaviour
    {
        [Tooltip("表示 / 非表示を切り替える部分")]
        [SerializeField] private GameObject content;
        [SerializeField] private Text bossNameText;
        [Tooltip("Image Type = Filled")]
        [SerializeField] private Image hpFill;
        [SerializeField] private float smoothSpeed = 8f;

        private BossBase boss;

        private void Update()
        {
            // ボスが後から出てくる / 入れ替わる場合にも対応
            if (boss != BossBase.Active)
            {
                boss = BossBase.Active;
                if (boss != null && bossNameText != null) bossNameText.text = boss.BossName;
                if (boss != null && hpFill != null) hpFill.fillAmount = boss.HpRate;
            }

            bool show = boss != null;
            if (content != null && content.activeSelf != show) content.SetActive(show);
            if (!show || hpFill == null) return;

            float k = 1f - Mathf.Exp(-smoothSpeed * Time.deltaTime);
            hpFill.fillAmount = Mathf.Lerp(hpFill.fillAmount, boss.HpRate, k);
        }
    }
}
