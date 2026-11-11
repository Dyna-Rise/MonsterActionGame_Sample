using MonsterAction.Data;
using MonsterAction.Home.Interaction;
using MonsterAction.Home.Player;
using UnityEngine;
using UnityEngine.UI;

namespace MonsterAction.Home.UI
{
    /// <summary>
    /// Home の画面表示：所持金（右上）と「□ 話しかける」などの案内（下）。
    /// </summary>
    public class HomeHUD : MonoBehaviour
    {
        [SerializeField] private Text moneyText;
        [Tooltip("案内の表示 / 非表示を切り替える部分")]
        [SerializeField] private GameObject promptRoot;
        [SerializeField] private Text promptText;
        [SerializeField] private string promptFormat = "□  {0}";

        private HomePlayerController player;

        private void OnEnable() => GameData.MoneyChanged += OnMoneyChanged;

        private void OnDisable()
        {
            GameData.MoneyChanged -= OnMoneyChanged;
            if (player != null) player.FocusChanged -= OnFocusChanged;
            player = null;
        }

        private void Start()
        {
            OnMoneyChanged(GameData.Money);
            OnFocusChanged(null);
        }

        private void Update()
        {
            // プレイヤーが後から出てきても大丈夫なように毎フレーム確認
            if (player == HomePlayerController.Active) return;
            if (player != null) player.FocusChanged -= OnFocusChanged;
            player = HomePlayerController.Active;
            if (player != null)
            {
                player.FocusChanged += OnFocusChanged;
                OnFocusChanged(player.Focused);
            }
        }

        private void OnMoneyChanged(int money)
        {
            if (moneyText != null) moneyText.text = $"{money:#,0} G";
        }

        private void OnFocusChanged(Interactable target)
        {
            if (promptRoot != null) promptRoot.SetActive(target != null);
            if (promptText != null && target != null) promptText.text = string.Format(promptFormat, target.Prompt);
        }
    }
}
