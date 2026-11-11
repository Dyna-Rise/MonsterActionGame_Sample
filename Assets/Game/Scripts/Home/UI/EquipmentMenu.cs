using System;
using System.Collections.Generic;
using MonsterAction.Data;
using UnityEngine;
using UnityEngine.UI;

namespace MonsterAction.Home.UI
{
    /// <summary>
    /// 装備画面（家のドアを調べると開く）。
    ///   ・プレイヤータイプ … どれか1つを選ぶ（GameData.SelectedPlayerTypeId）
    ///   ・スキル         … 買ったスキルを付ける / 外す（GameData.MaxEquippedSkills 個まで）
    /// 新しいタイプを作ったら playerTypes に1行追加してください（ID は Battle の BattlePlayerSpawner と同じにする）。
    /// </summary>
    public class EquipmentMenu : HomeMenuBase
    {
        [Serializable]
        public struct PlayerTypeOption
        {
            [Tooltip("BattlePlayerSpawner の typeId と同じにする")]
            public string id;
            public string displayName;
            [TextArea] public string description;
        }

        private static readonly Color OnColor = new Color(0.45f, 1f, 0.5f);
        private static readonly Color OffColor = new Color(0.75f, 0.75f, 0.75f);
        private static readonly Color DisabledColor = new Color(0.5f, 0.5f, 0.5f);

        [SerializeField] private PlayerTypeOption[] playerTypes =
        {
            new PlayerTypeOption { id = "SwordShield", displayName = "片手剣", description = "□ 連続攻撃 / □長押し ガード。\n攻守のバランスがよい。" },
            new PlayerTypeOption { id = "Bow", displayName = "弓", description = "□ 矢を撃つ / □長押し 溜め撃ち / L1 で狙う。\n離れて戦う。" },
        };

        [Tooltip("「スキル 1 / 2」のような枠の表示")]
        [SerializeField] private Text slotText;

        // 行の中身（タイプかスキルのどちらか）
        private struct Entry
        {
            public int typeIndex; // -1 ならスキル
            public SkillType skill;
            public bool IsType => typeIndex >= 0;
        }

        private readonly List<Entry> entries = new List<Entry>();

        /// <summary>タイプ ID から表示名を返す（出撃画面などで使う）</summary>
        public string GetPlayerTypeName(string id)
        {
            foreach (var t in playerTypes)
                if (t.id == id) return t.displayName;
            return id;
        }

        protected override void BuildRows()
        {
            entries.Clear();

            CreateHeader("プレイヤータイプ");
            for (int i = 0; i < playerTypes.Length; i++)
            {
                CreateRow();
                entries.Add(new Entry { typeIndex = i });
            }

            CreateHeader("スキル");
            foreach (var skill in SkillData.All)
            {
                CreateRow();
                entries.Add(new Entry { typeIndex = -1, skill = skill });
            }
        }

        protected override void Refresh()
        {
            if (moneyText != null) moneyText.text = "所持金  " + FormatMoney(GameData.Money);
            if (slotText != null) slotText.text = $"スキル枠  {GameData.EquippedSkillCount} / {GameData.MaxEquippedSkills}";

            for (int i = 0; i < rows.Count && i < entries.Count; i++)
            {
                var e = entries[i];
                if (e.IsType)
                {
                    var t = playerTypes[e.typeIndex];
                    bool selected = GameData.SelectedPlayerTypeId == t.id;
                    rows[i].Set(t.displayName, "", selected ? "● 選択中" : "", OnColor);
                }
                else
                {
                    int level = GameData.GetSkillLevel(e.skill);
                    if (level <= 0)
                    {
                        rows[i].Set(SkillData.GetName(e.skill), SkillData.GetStars(0), "未購入", DisabledColor);
                        continue;
                    }
                    bool on = GameData.IsSkillEquipped(e.skill);
                    rows[i].Set(SkillData.GetName(e.skill),
                                $"{SkillData.GetStars(level)}  ×{SkillData.GetMultiplier(level):0.00}",
                                on ? "装備中" : "外している",
                                on ? OnColor : OffColor);
                }
            }
        }

        protected override void OnRowSelected(int index)
        {
            if (index < 0 || index >= entries.Count) return;
            var e = entries[index];
            if (e.IsType)
            {
                SetDescription(playerTypes[e.typeIndex].description);
                return;
            }
            string text = SkillData.GetDescription(e.skill);
            if (GameData.GetSkillLevel(e.skill) <= 0) text += "\n村人のショップで買えます。";
            SetDescription(text);
        }

        protected override void OnRowClicked(int index)
        {
            if (index < 0 || index >= entries.Count) return;
            var e = entries[index];

            if (e.IsType)
            {
                var t = playerTypes[e.typeIndex];
                GameData.SelectedPlayerTypeId = t.id;
                ShowMessage($"{t.displayName} にした");
            }
            else if (GameData.GetSkillLevel(e.skill) <= 0)
            {
                ShowMessage("まだ買っていません");
            }
            else
            {
                bool on = !GameData.IsSkillEquipped(e.skill);
                if (GameData.SetSkillEquipped(e.skill, on))
                    ShowMessage(on ? $"{SkillData.GetName(e.skill)} を付けた" : $"{SkillData.GetName(e.skill)} を外した");
                else
                    ShowMessage("スキル枠がいっぱいです。先にどれかを外してください");
            }

            Refresh();
        }
    }
}
