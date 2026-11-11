using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace MonsterAction.Battle.UI
{
    /// <summary>
    /// クエスト失敗 / クリアの表示パネル。BattleHUD の ResultPanel に付いています。
    /// 普段は透明（CanvasGroup の alpha = 0）で、Show が呼ばれるとフェードインします。
    /// 表示のきっかけは BattleFlowController が作ります。
    /// </summary>
    [RequireComponent(typeof(CanvasGroup))]
    public class BattleResultUI : MonoBehaviour
    {
        [SerializeField] private Text titleText;
        [SerializeField] private Text messageText;
        [Tooltip("報酬金の表示（クリア時だけ）")]
        [SerializeField] private Text rewardText;
        [SerializeField] private float fadeTime = 0.8f;

        [Header("文言と色")]
        [SerializeField] private string failedTitle = "QUEST FAILED";
        [SerializeField] private Color failedColor = new Color(0.9f, 0.25f, 0.2f);
        [SerializeField] private string clearedTitle = "QUEST CLEAR";
        [SerializeField] private Color clearedColor = new Color(1f, 0.85f, 0.3f);
        [Tooltip("{0} に Home に戻るまでの秒数が入ります")]
        [SerializeField] private string returnMessage = "{0} 秒後に拠点へ戻ります";
        [Tooltip("{0} に報酬金、{1} に所持金が入ります")]
        [SerializeField] private string rewardMessage = "報酬金 {0} G　（所持金 {1} G）";

        private CanvasGroup group;
        private float returnAt;

        private void Awake()
        {
            group = GetComponent<CanvasGroup>();
            group.alpha = 0f;
            group.blocksRaycasts = false;
            group.interactable = false;
        }

        public void ShowFailed(float secondsUntilReturn) => Show(failedTitle, failedColor, secondsUntilReturn, null);

        public void ShowCleared(float secondsUntilReturn, int reward, int totalMoney) =>
            Show(clearedTitle, clearedColor, secondsUntilReturn, string.Format(rewardMessage, reward, totalMoney));

        public void Show(string title, Color color, float secondsUntilReturn, string reward)
        {
            if (rewardText != null)
            {
                rewardText.text = reward ?? "";
                rewardText.gameObject.SetActive(!string.IsNullOrEmpty(reward));
            }
            if (titleText != null)
            {
                titleText.text = title;
                titleText.color = color;
            }
            returnAt = Time.unscaledTime + secondsUntilReturn;
            StopAllCoroutines();
            StartCoroutine(FadeIn());
        }

        private void Update()
        {
            if (messageText == null || group.alpha <= 0f) return;
            int remain = Mathf.Max(0, Mathf.CeilToInt(returnAt - Time.unscaledTime));
            messageText.text = string.Format(returnMessage, remain);
        }

        private IEnumerator FadeIn()
        {
            float t = 0f;
            while (t < 1f)
            {
                t += Time.unscaledDeltaTime / Mathf.Max(0.01f, fadeTime);
                group.alpha = Mathf.Clamp01(t);
                yield return null;
            }
        }
    }
}
