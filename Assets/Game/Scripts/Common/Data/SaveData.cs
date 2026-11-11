using System;
using System.Collections.Generic;

namespace MonsterAction.Data
{
    /// <summary>
    /// セーブファイル1つ分の中身（JSON にして保存する）。
    /// 保存したい値を増やすときは、ここにフィールドを足して、GameData.ToSaveData / LoadFromSaveData にも書き足してください。
    /// （古いセーブに無いフィールドは初期値のまま読み込まれるので、足しても古いセーブは壊れません）
    /// </summary>
    [Serializable]
    public class SaveData
    {
        /// <summary>セーブの形式の番号（形式を大きく変えたときに上げる）</summary>
        public int version = 1;

        // ---- 表示用（タイトルのスロット一覧） ----
        public string savedAt;          // 保存した日時（"2026/11/12 10:30"）
        public float playSeconds;       // プレイ時間（秒）

        // ---- ゲームの中身 ----
        public int money;
        public List<ItemCount> items = new List<ItemCount>();
        public List<SkillLevel> skills = new List<SkillLevel>();
        public List<string> equippedSkills = new List<string>();
        public string playerTypeId;
        public string bossId;
        public int questClearCount;

        [Serializable]
        public struct ItemCount
        {
            public string id;
            public int count;
        }

        [Serializable]
        public struct SkillLevel
        {
            public string skill; // SkillType の名前（"Health" など。並び順が変わっても壊れないように文字で保存）
            public int level;
        }
    }
}
