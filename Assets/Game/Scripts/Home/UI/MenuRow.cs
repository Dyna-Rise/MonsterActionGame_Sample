using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace MonsterAction.Home.UI
{
    /// <summary>
    /// メニューの1行（ボタン）。ShopMenu / EquipmentMenu が、ひな形（Template）を複製して並べます。
    ///   名前 ｜ 情報（★や所持数）｜ 右端（値段や「装備中」）
    /// </summary>
    [RequireComponent(typeof(Button))]
    public class MenuRow : MonoBehaviour, ISelectHandler, IPointerEnterHandler
    {
        [SerializeField] private Text nameText;
        [SerializeField] private Text infoText;
        [SerializeField] private Text rightText;

        private Button button;

        public Button Button => button != null ? button : (button = GetComponent<Button>());

        /// <summary>カーソルが乗った（説明文の切り替えに使う）</summary>
        public event Action<MenuRow> Selected;

        /// <summary>決定された</summary>
        public event Action<MenuRow> Clicked;

        private void Awake() => Button.onClick.AddListener(() => Clicked?.Invoke(this));

        public void Set(string name, string info, string right, Color? rightColor = null)
        {
            if (nameText != null) nameText.text = name;
            if (infoText != null) infoText.text = info;
            if (rightText != null)
            {
                rightText.text = right;
                rightText.color = rightColor ?? Color.white;
            }
        }

        public void OnSelect(BaseEventData eventData) => Selected?.Invoke(this);

        public void OnPointerEnter(PointerEventData eventData)
        {
            // マウスを乗せたらそれを選択状態にする（ゲームパッドと同じ見た目になる）
            if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(gameObject);
        }
    }
}
