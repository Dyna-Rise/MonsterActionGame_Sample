using MonsterAction.Data;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace MonsterAction.Home.UI
{
    /// <summary>
    /// Home のメニュー（Esc / OPTIONS で開く）。
    ///   ・セーブする
    ///   ・タイトルへ戻る（セーブしてから戻る）
    ///   ・閉じる
    /// ※ 自動セーブ：Home に戻ってきたとき、ショップ・装備などのメニューを閉じたときにも保存されます。
    /// </summary>
    public class HomePauseMenu : HomeMenuBase
    {
        [SerializeField] private string titleSceneName = "Title";

        private bool leaving;

        protected override void Update()
        {
            if (!IsOpen)
            {
                // 他のメニューを Esc で閉じた同じフレームには開かない
                if (Current == null && Time.frameCount != LastClosedFrame && OpenPressed()) Open();
                return;
            }
            if (Gamepad.current != null && Gamepad.current.startButton.wasPressedThisFrame)
            {
                Close();
                return;
            }
            base.Update();
        }

        protected override bool CanClose => !leaving;

        protected override void BuildRows()
        {
            leaving = false;
            CreateHeader("メニュー");
            CreateRow(); // セーブ
            CreateRow(); // タイトルへ
            CreateRow(); // 閉じる
        }

        protected override void Refresh()
        {
            if (moneyText != null) moneyText.text = "所持金  " + FormatMoney(GameData.Money);
            if (rows.Count < 3) return;
            string slot = SaveSystem.HasSlot ? $"スロット {SaveSystem.CurrentSlot + 1}" : "スロットなし";
            rows[0].Set("セーブする", slot, "");
            rows[1].Set("タイトルへ戻る", "", "");
            rows[2].Set("閉じる", "", "");
        }

        protected override void OnRowSelected(int index)
        {
            switch (index)
            {
                case 0:
                    SetDescription(SaveSystem.HasSlot
                        ? $"今の状態を保存します。\n\nプレイ時間  {SaveSystem.FormatPlayTime(SaveSystem.PlaySeconds)}\nクリア  {GameData.QuestClearCount} 回\n\n※ Home に戻ったときや、ショップ・装備を閉じたときにも自動で保存されます。"
                        : "タイトルから始めていないので、セーブできません。\n（Home シーンを直接 Play しているとき）");
                    break;
                case 1:
                    SetDescription("セーブしてからタイトル画面に戻ります。");
                    break;
                case 2:
                    SetDescription("メニューを閉じます。");
                    break;
            }
        }

        protected override void OnRowClicked(int index)
        {
            if (leaving) return;
            switch (index)
            {
                case 0:
                    if (!SaveSystem.HasSlot) ShowMessage("タイトルから始めるとセーブできます");
                    else ShowMessage(SaveSystem.Save() ? "セーブしました" : "セーブに失敗しました");
                    OnRowSelected(0);
                    break;
                case 1:
                    leaving = true;
                    SaveSystem.AutoSave();
                    ShowMessage("タイトルへ戻ります…");
                    SceneManager.LoadScene(titleSceneName);
                    break;
                case 2:
                    Close();
                    break;
            }
        }

        private static bool OpenPressed() =>
            (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame) ||
            (Gamepad.current != null && Gamepad.current.startButton.wasPressedThisFrame);
    }
}
