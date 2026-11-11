using System.Collections.Generic;
using MonsterAction.Data;
using MonsterAction.Home.UI;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace MonsterAction.Title
{
    /// <summary>
    /// タイトル画面。
    ///   PRESS ANY BUTTON → はじめから / つづきから / ゲーム終了
    ///     はじめから → スロットを選ぶ（データがあるスロットは、もう一度決定で上書き）→ Home へ
    ///     つづきから → データがあるスロットを選ぶ → 読み込んで Home へ
    ///   〇 / Esc でひとつ前に戻る。
    /// </summary>
    public class TitleMenu : MonoBehaviour
    {
        private enum Screen { Press, Main, NewGame, Continue }

        [Header("表示")]
        [SerializeField] private Text titleText;
        [SerializeField] private string gameTitle = "MONSTER ACTION";
        [Tooltip("「PRESS ANY BUTTON」の表示（点滅させる）")]
        [SerializeField] private Text pressText;
        [Tooltip("メニュー（一覧・説明）全体。PRESS ANY BUTTON の間は隠す")]
        [SerializeField] private GameObject menuRoot;
        [SerializeField] private Text headerText;
        [SerializeField] private RectTransform listRoot;
        [SerializeField] private MenuRow rowTemplate;
        [SerializeField] private Text descriptionText;
        [SerializeField] private Text messageText;

        [Header("シーン")]
        [SerializeField] private string homeSceneName = "Home";

        private static readonly Color OnColor = new Color(0.45f, 1f, 0.5f);
        private static readonly Color OffColor = new Color(0.55f, 0.55f, 0.55f);
        private static readonly Color WarnColor = new Color(1f, 0.55f, 0.35f);

        private Screen screen = Screen.Press;
        private readonly List<MenuRow> rows = new List<MenuRow>();
        private int pendingSelectFrame = -1;
        private int confirmOverwriteSlot = -1;
        private bool starting;

        private void Awake()
        {
            if (titleText != null) titleText.text = gameTitle;
            if (rowTemplate != null) rowTemplate.gameObject.SetActive(false);
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        private void Start() => Show(Screen.Press);

        private void Update()
        {
            if (starting) return;

            if (pressText != null && pressText.gameObject.activeInHierarchy)
            {
                var c = pressText.color;
                c.a = 0.45f + 0.55f * Mathf.Abs(Mathf.Sin(Time.unscaledTime * 2.2f));
                pressText.color = c;
            }

            if (screen == Screen.Press)
            {
                if (AnyPressed()) Show(Screen.Main);
                return;
            }

            if (pendingSelectFrame >= 0 && Time.frameCount > pendingSelectFrame)
            {
                pendingSelectFrame = -1;
                SelectFirst();
            }

            if (CancelPressed())
            {
                if (screen == Screen.Main) Show(Screen.Press);
                else Show(Screen.Main);
            }
        }

        // ================================================================
        //  画面の切り替え
        // ================================================================

        private void Show(Screen next)
        {
            screen = next;
            confirmOverwriteSlot = -1;
            ShowMessage("");
            ClearRows();

            bool press = next == Screen.Press;
            if (pressText != null) pressText.gameObject.SetActive(press);
            if (menuRoot != null) menuRoot.SetActive(!press);
            if (press)
            {
                if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);
                return;
            }

            switch (next)
            {
                case Screen.Main:
                    SetHeader("メニュー");
                    bool anySave = AnySave();
                    AddRow("はじめから", "", "", null, () => Show(Screen.NewGame),
                           "新しく冒険を始めます。");
                    AddRow("つづきから", "", anySave ? "" : "データなし", OffColor,
                           () => { if (AnySave()) Show(Screen.Continue); else ShowMessage("セーブデータがありません"); },
                           "セーブしたところから続けます。");
                    AddRow("ゲーム終了", "", "", null, Quit, "ゲームを終わります。");
                    break;

                case Screen.NewGame:
                case Screen.Continue:
                    SetHeader(next == Screen.NewGame ? "はじめから：スロットを選ぶ" : "つづきから：スロットを選ぶ");
                    for (int i = 0; i < SaveSystem.SlotCount; i++)
                    {
                        int slot = i;
                        var data = SaveSystem.Peek(slot);
                        if (data == null)
                        {
                            AddRow($"スロット {slot + 1}", "データなし", "", null, () => OnSlotClicked(slot), "空きスロットです。");
                        }
                        else
                        {
                            AddRow($"スロット {slot + 1}", $"{data.money:#,0} G", data.savedAt, OnColor,
                                   () => OnSlotClicked(slot), Describe(data));
                        }
                    }
                    break;
            }

            if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);
            pendingSelectFrame = Time.frameCount; // 決定ボタンが次の画面でも押されたことにならないよう、選択は次のフレーム
        }

        private void OnSlotClicked(int slot)
        {
            bool exists = SaveSystem.Exists(slot);

            if (screen == Screen.Continue)
            {
                if (!exists) { ShowMessage("このスロットにはデータがありません"); return; }
                if (!SaveSystem.Load(slot)) { ShowMessage("読み込めませんでした（データが壊れているかもしれません）"); return; }
                StartGame();
                return;
            }

            // はじめから：データがあるなら、もう一度決定で上書き
            if (exists && confirmOverwriteSlot != slot)
            {
                confirmOverwriteSlot = slot;
                ShowMessage($"スロット {slot + 1} のデータを消して始めます。もう一度決定で上書き", WarnColor);
                return;
            }
            SaveSystem.NewGame(slot);
            StartGame();
        }

        private void StartGame()
        {
            starting = true;
            ShowMessage("Now Loading...");
            SceneManager.LoadScene(homeSceneName);
        }

        private static void Quit()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        // ================================================================
        //  部品
        // ================================================================

        private static string Describe(SaveData data)
        {
            string type = data.playerTypeId switch
            {
                "SwordShield" => "片手剣",
                "Bow" => "弓",
                "Magic" => "魔法",
                _ => data.playerTypeId,
            };
            return $"所持金  {data.money:#,0} G\n" +
                   $"タイプ  {type}\n" +
                   $"クリア  {data.questClearCount} 回\n" +
                   $"プレイ時間  {SaveSystem.FormatPlayTime(data.playSeconds)}\n" +
                   $"保存日時  {data.savedAt}";
        }

        private static bool AnySave()
        {
            for (int i = 0; i < SaveSystem.SlotCount; i++)
                if (SaveSystem.Exists(i)) return true;
            return false;
        }

        private void AddRow(string name, string info, string right, Color? rightColor, System.Action onClick, string description)
        {
            var row = Instantiate(rowTemplate, listRoot);
            row.gameObject.SetActive(true);
            row.Set(name, info, right, rightColor);
            row.Clicked += _ => { if (!starting) onClick(); };
            row.Selected += _ => { if (descriptionText != null) descriptionText.text = description; };
            rows.Add(row);
        }

        private void ClearRows()
        {
            foreach (var r in rows)
                if (r != null) Destroy(r.gameObject);
            rows.Clear();
            if (descriptionText != null) descriptionText.text = "";
        }

        private void SelectFirst()
        {
            var es = EventSystem.current;
            if (es == null || rows.Count == 0) return;
            es.SetSelectedGameObject(rows[0].gameObject);
        }

        private void SetHeader(string text)
        {
            if (headerText != null) headerText.text = text;
        }

        private void ShowMessage(string text, Color? color = null)
        {
            if (messageText == null) return;
            messageText.text = text;
            messageText.color = color ?? new Color(1f, 0.82f, 0.35f);
        }

        private static bool AnyPressed()
        {
            if (Keyboard.current != null && Keyboard.current.anyKey.wasPressedThisFrame) return true;
            if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame) return true;
            var pad = Gamepad.current;
            return pad != null && (pad.buttonSouth.wasPressedThisFrame || pad.buttonEast.wasPressedThisFrame ||
                                   pad.buttonWest.wasPressedThisFrame || pad.buttonNorth.wasPressedThisFrame ||
                                   pad.startButton.wasPressedThisFrame);
        }

        private static bool CancelPressed() =>
            (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame) ||
            (Gamepad.current != null && Gamepad.current.buttonEast.wasPressedThisFrame);
    }
}
