using System;
using System.IO;
using UnityEngine;

namespace MonsterAction.Data
{
    /// <summary>
    /// セーブ / ロード（スロット式）。中身は GameData の値で、JSON ファイルとして保存します。
    ///   保存場所：Application.persistentDataPath/save_0.json 〜 save_2.json
    ///   （Windows なら C:\Users\ユーザー名\AppData\LocalLow\会社名\プロダクト名\）
    ///
    ///   SaveSystem.NewGame(slot)   … 初期状態から始める（すぐ保存）
    ///   SaveSystem.Load(slot)      … 読み込んで GameData に入れる
    ///   SaveSystem.Save()          … 今のスロットに保存
    ///   SaveSystem.AutoSave()      … 今のスロットがあれば保存（タイトルを通さずに Play したときは何もしない）
    ///   SaveSystem.Peek(slot)      … 中身だけ読む（タイトルのスロット一覧の表示用）
    /// </summary>
    public static class SaveSystem
    {
        public const int SlotCount = 3;

        /// <summary>今遊んでいるスロット（-1 = 無し。Home / Battle を直接 Play したとき）</summary>
        public static int CurrentSlot { get; private set; } = -1;

        /// <summary>保存した（スロット番号）。「セーブしました」の表示などに</summary>
        public static event Action<int> Saved;

        private static float playSecondsAtLoad;
        private static float loadRealtime;

        public static bool HasSlot => CurrentSlot >= 0;

        /// <summary>今のプレイ時間（秒）</summary>
        public static float PlaySeconds => playSecondsAtLoad + (Time.realtimeSinceStartup - loadRealtime);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOnPlay()
        {
            CurrentSlot = -1;
            Saved = null;
            playSecondsAtLoad = 0f;
            loadRealtime = 0f;
        }

        public static string GetPath(int slot) => Path.Combine(Application.persistentDataPath, $"save_{slot}.json");

        public static bool Exists(int slot) => IsValidSlot(slot) && File.Exists(GetPath(slot));

        /// <summary>はじめから：初期状態にして、すぐそのスロットに保存</summary>
        public static void NewGame(int slot)
        {
            if (!IsValidSlot(slot)) return;
            GameData.ResetToDefault();
            CurrentSlot = slot;
            playSecondsAtLoad = 0f;
            loadRealtime = Time.realtimeSinceStartup;
            Save();
        }

        /// <summary>つづきから：読み込む。失敗したら false（GameData は変わらない）</summary>
        public static bool Load(int slot)
        {
            var data = Peek(slot);
            if (data == null) return false;
            GameData.LoadFromSaveData(data);
            CurrentSlot = slot;
            playSecondsAtLoad = data.playSeconds;
            loadRealtime = Time.realtimeSinceStartup;
            return true;
        }

        /// <summary>今のスロットに保存する。スロットが無ければ false</summary>
        public static bool Save()
        {
            if (!HasSlot) return false;
            var data = GameData.ToSaveData();
            data.playSeconds = PlaySeconds;
            data.savedAt = DateTime.Now.ToString("yyyy/MM/dd HH:mm");

            try
            {
                string path = GetPath(CurrentSlot);
                // 書きかけで壊れないよう、一時ファイルに書いてから置き換える
                string temp = path + ".tmp";
                File.WriteAllText(temp, JsonUtility.ToJson(data, true));
                if (File.Exists(path)) File.Delete(path);
                File.Move(temp, path);
            }
            catch (Exception e)
            {
                Debug.LogError($"[Save] 保存に失敗しました: {e.Message}");
                return false;
            }

            Saved?.Invoke(CurrentSlot);
            return true;
        }

        /// <summary>スロットがあれば保存（Home に戻ったとき・メニューを閉じたときに呼ばれる）</summary>
        public static void AutoSave()
        {
            if (HasSlot) Save();
        }

        /// <summary>スロットの中身を読むだけ（無い・壊れているなら null）</summary>
        public static SaveData Peek(int slot)
        {
            if (!Exists(slot)) return null;
            try
            {
                return JsonUtility.FromJson<SaveData>(File.ReadAllText(GetPath(slot)));
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[Save] スロット{slot + 1} を読めません: {e.Message}");
                return null;
            }
        }

        public static void Delete(int slot)
        {
            if (!Exists(slot)) return;
            File.Delete(GetPath(slot));
            if (CurrentSlot == slot) CurrentSlot = -1;
        }

        /// <summary>プレイ時間を "1:23:45" や "12:05" の形に</summary>
        public static string FormatPlayTime(float seconds)
        {
            var t = TimeSpan.FromSeconds(Mathf.Max(0f, seconds));
            return t.TotalHours >= 1 ? $"{(int)t.TotalHours}:{t.Minutes:00}:{t.Seconds:00}" : $"{t.Minutes}:{t.Seconds:00}";
        }

        private static bool IsValidSlot(int slot) => slot >= 0 && slot < SlotCount;
    }
}
