using System.Collections.Generic;
using MonsterAction.Data;
using UnityEngine;

namespace MonsterAction.Home.UI
{
    /// <summary>
    /// アイテム・スキルのショップ（村人に話しかけると開く）。
    ///   ・アイテム … ItemDefinition.All（Resources/Items）を並べる。最大数（回復薬は10個）まで買える
    ///   ・スキル   … SkillData.All を並べる。買うたびに★が1つ上がり、★3 で売り切れ
    /// 買う処理は GameData.TryBuy / TryBuySkill です。
    /// </summary>
    public class ShopMenu : HomeMenuBase
    {
        private static readonly Color PriceColor = new Color(1f, 0.85f, 0.3f);
        private static readonly Color DisabledColor = new Color(0.6f, 0.6f, 0.6f);

        // 行の中身（アイテムかスキルのどちらか）
        private struct Entry
        {
            public ItemDefinition item;
            public SkillType skill;
            public bool IsItem => item != null;
        }

        private readonly List<Entry> entries = new List<Entry>();

        protected override void BuildRows()
        {
            entries.Clear();

            CreateHeader("アイテム");
            foreach (var item in ItemDefinition.All)
            {
                if (item == null) continue;
                CreateRow();
                entries.Add(new Entry { item = item });
            }

            CreateHeader("スキル");
            foreach (var skill in SkillData.All)
            {
                CreateRow();
                entries.Add(new Entry { skill = skill });
            }
        }

        protected override void Refresh()
        {
            if (moneyText != null) moneyText.text = "所持金  " + FormatMoney(GameData.Money);

            for (int i = 0; i < rows.Count && i < entries.Count; i++)
            {
                var e = entries[i];
                if (e.IsItem)
                {
                    int count = GameData.GetCount(e.item);
                    bool full = count >= e.item.MaxCount;
                    rows[i].Set(e.item.DisplayName,
                                $"所持 {count} / {e.item.MaxCount}",
                                full ? "所持上限" : FormatMoney(e.item.Price),
                                full ? DisabledColor : PriceColor);
                }
                else
                {
                    int level = GameData.GetSkillLevel(e.skill);
                    bool maxed = GameData.IsSkillMaxed(e.skill);
                    string info = maxed
                        ? $"{SkillData.GetStars(level)}  ×{SkillData.GetMultiplier(level):0.00}"
                        : $"{SkillData.GetStars(level)} → ★{level + 1}  ×{SkillData.GetMultiplier(level + 1):0.00}";
                    rows[i].Set(SkillData.GetName(e.skill), info,
                                maxed ? "売切れ" : FormatMoney(GameData.GetNextSkillPrice(e.skill)),
                                maxed ? DisabledColor : PriceColor);
                }
            }
        }

        protected override void OnRowSelected(int index)
        {
            if (index < 0 || index >= entries.Count) return;
            var e = entries[index];
            SetDescription(e.IsItem ? e.item.Description : SkillData.GetDescription(e.skill) + "\n装備画面（ドア）で付けると効果が出ます。");
        }

        protected override void OnRowClicked(int index)
        {
            if (index < 0 || index >= entries.Count) return;
            var e = entries[index];

            if (e.IsItem)
            {
                if (GameData.GetCount(e.item) >= e.item.MaxCount) ShowMessage("これ以上持てません");
                else if (GameData.TryBuy(e.item)) ShowMessage($"{e.item.DisplayName} を買った！");
                else ShowMessage("お金が足りません");
            }
            else
            {
                if (GameData.IsSkillMaxed(e.skill)) ShowMessage("売り切れです");
                else if (GameData.TryBuySkill(e.skill))
                    ShowMessage($"{SkillData.GetName(e.skill)} が ★{GameData.GetSkillLevel(e.skill)} になった！");
                else ShowMessage("お金が足りません");
            }

            Refresh();
        }
    }
}
