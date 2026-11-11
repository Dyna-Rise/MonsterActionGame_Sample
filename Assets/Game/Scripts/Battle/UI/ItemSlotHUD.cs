using MonsterAction.Battle.Player;
using MonsterAction.Data;
using UnityEngine;
using UnityEngine.UI;

namespace MonsterAction.Battle.UI
{
    /// <summary>
    /// △で使うアイテム（回復薬）の名前と残り数（BattleHUD の ItemSlot に付いています）。
    /// 0 個になると暗くなります。
    /// </summary>
    public class ItemSlotHUD : MonoBehaviour
    {
        [SerializeField] private Image icon;
        [SerializeField] private Text nameText;
        [SerializeField] private Text countText;
        [Tooltip("残り 0 のときに暗くする部分")]
        [SerializeField] private CanvasGroup group;
        [SerializeField] private float emptyAlpha = 0.4f;

        private ItemDefinition item;

        private void OnEnable() => GameData.ItemCountChanged += OnItemCountChanged;
        private void OnDisable() => GameData.ItemCountChanged -= OnItemCountChanged;

        private void Update()
        {
            // プレイヤーが持っている△のアイテムに合わせる
            var player = PlayerBattleController.Active;
            var current = player != null ? player.QuickItem : null;
            if (current == item) return;

            item = current;
            if (nameText != null) nameText.text = item != null ? item.DisplayName : "-";
            if (icon != null && item != null && item.Icon != null) icon.sprite = item.Icon;
            Refresh();
        }

        private void OnItemCountChanged(ItemDefinition changed, int count)
        {
            if (changed == item) Refresh();
        }

        private void Refresh()
        {
            int count = GameData.GetCount(item);
            if (countText != null) countText.text = $"× {count}";
            if (group != null) group.alpha = count > 0 ? 1f : emptyAlpha;
        }
    }
}
