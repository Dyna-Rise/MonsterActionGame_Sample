using System.Collections.Generic;
using MonsterAction.Battle.CameraSystem;
using MonsterAction.Home.Player;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace MonsterAction.Home.UI
{
    /// <summary>
    /// Home のメニュー画面の共通部分（ShopMenu / EquipmentMenu の親クラス）。
    ///   ・Open() で開く（Interactable の On Interact に登録して使う）
    ///   ・開いている間はプレイヤーが動けず、マウスカーソルが出る
    ///   ・〇 / Esc / 閉じるボタンで閉じる。✕ / Enter / クリックで決定
    ///   ・行（MenuRow）はひな形（rowTemplate）を複製して作る（CreateRow）
    /// </summary>
    public abstract class HomeMenuBase : MonoBehaviour
    {
        /// <summary>今開いているメニュー（無ければ null）</summary>
        public static HomeMenuBase Current { get; private set; }

        [Tooltip("表示 / 非表示を切り替える部分")]
        [SerializeField] protected GameObject panel;
        [Tooltip("行を並べる場所（Vertical Layout Group）")]
        [SerializeField] protected RectTransform listRoot;
        [Tooltip("行のひな形（非表示にしておく）")]
        [SerializeField] protected MenuRow rowTemplate;
        [Tooltip("見出しのひな形（非表示にしておく）")]
        [SerializeField] protected Text headerTemplate;
        [SerializeField] protected Text moneyText;
        [Tooltip("選んでいる行の説明")]
        [SerializeField] protected Text descriptionText;
        [Tooltip("「お金が足りません」などのメッセージ")]
        [SerializeField] protected Text messageText;
        [SerializeField] protected Button closeButton;

        protected readonly List<MenuRow> rows = new List<MenuRow>();
        private readonly List<GameObject> created = new List<GameObject>();
        private float messageTimer;
        private int openFrame;
        private bool pendingSelect;

        public bool IsOpen => panel != null && panel.activeSelf;

        /// <summary>最後にメニューが閉じたフレーム（Esc で閉じた同じフレームに別のメニューが開かないように）</summary>
        protected static int LastClosedFrame { get; private set; } = -1;

        /// <summary>false の間は閉じられない（出撃中など）</summary>
        protected virtual bool CanClose => true;

        protected virtual void Awake()
        {
            if (panel != null) panel.SetActive(false);
            if (rowTemplate != null) rowTemplate.gameObject.SetActive(false);
            if (headerTemplate != null) headerTemplate.gameObject.SetActive(false);
            if (closeButton != null) closeButton.onClick.AddListener(Close);
        }

        /// <summary>メニューを開く</summary>
        public void Open()
        {
            if (IsOpen || Current != null) return;
            Current = this;
            panel.SetActive(true);
            SetGameplayPaused(true);
            ShowMessage("");

            ClearRows();
            BuildRows();
            Refresh();
            // 開いた瞬間の決定ボタン（Enter など）で買ってしまわないよう、選択は次のフレームから
            if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);
            openFrame = Time.frameCount;
            pendingSelect = true;
        }

        /// <summary>メニューを閉じる</summary>
        public void Close()
        {
            if (!IsOpen || !CanClose) return;
            panel.SetActive(false);
            if (Current == this) Current = null;
            LastClosedFrame = Time.frameCount;
            SetGameplayPaused(false);
            if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);

            // 自動セーブ（買い物・装備の変更を保存。タイトルを通していなければ何もしない）
            MonsterAction.Data.SaveSystem.AutoSave();
        }

        /// <summary>行を作る（開いたときに1回呼ばれる）。CreateHeader / CreateRow を使う</summary>
        protected abstract void BuildRows();

        /// <summary>表示を最新にする（買った・装備したあとに呼ぶ）</summary>
        protected abstract void Refresh();

        protected MenuRow CreateRow()
        {
            var row = Instantiate(rowTemplate, listRoot);
            row.gameObject.SetActive(true);
            row.Selected += r => OnRowSelected(rows.IndexOf(r));
            row.Clicked += r => OnRowClicked(rows.IndexOf(r));
            rows.Add(row);
            created.Add(row.gameObject);
            return row;
        }

        protected void CreateHeader(string text)
        {
            if (headerTemplate == null) return;
            var header = Instantiate(headerTemplate, listRoot);
            header.gameObject.SetActive(true);
            header.text = text;
            created.Add(header.gameObject);
        }

        /// <summary>行にカーソルが乗った（index は CreateRow した順番）</summary>
        protected virtual void OnRowSelected(int index) { }

        /// <summary>行が決定された</summary>
        protected virtual void OnRowClicked(int index) { }

        protected void SetDescription(string text)
        {
            if (descriptionText != null) descriptionText.text = text;
        }

        protected void ShowMessage(string text, float seconds = 2f)
        {
            if (messageText == null) return;
            messageText.text = text;
            messageTimer = seconds;
        }

        protected virtual void Update()
        {
            if (!IsOpen) return;

            if (pendingSelect && Time.frameCount > openFrame)
            {
                pendingSelect = false;
                SelectFirst();
            }

            if (messageTimer > 0f)
            {
                messageTimer -= Time.unscaledDeltaTime;
                if (messageTimer <= 0f && messageText != null) messageText.text = "";
            }

            // 〇 / Esc で閉じる
            bool cancel = (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame) ||
                          (Gamepad.current != null && Gamepad.current.buttonEast.wasPressedThisFrame);
            if (cancel)
            {
                Close();
                return;
            }

            // マウスで何もない所をクリックして選択が外れたら、ゲームパッドを動かしたときに戻す
            var es = EventSystem.current;
            if (es != null && es.currentSelectedGameObject == null && Gamepad.current != null &&
                (Gamepad.current.dpad.ReadValue().sqrMagnitude > 0.1f || Gamepad.current.leftStick.ReadValue().sqrMagnitude > 0.25f))
                SelectFirst();
        }

        private void SelectFirst()
        {
            var es = EventSystem.current;
            if (es == null) return;
            GameObject first = rows.Count > 0 ? rows[0].gameObject : (closeButton != null ? closeButton.gameObject : null);
            es.SetSelectedGameObject(null);
            es.SetSelectedGameObject(first);
            if (rows.Count > 0) OnRowSelected(0);
        }

        private void ClearRows()
        {
            foreach (var go in created)
                if (go != null) Destroy(go);
            created.Clear();
            rows.Clear();
        }

        private static void SetGameplayPaused(bool paused)
        {
            var player = HomePlayerController.Active;
            if (player != null) player.ControlEnabled = !paused;

            var cam = Camera.main != null ? Camera.main.GetComponent<BattleCameraController>() : null;
            if (cam != null) cam.SetMenuMode(paused);
        }

        protected virtual void OnDisable()
        {
            if (Current == this) Close();
        }

        protected static string FormatMoney(int money) => $"{money:#,0} G";
    }
}
