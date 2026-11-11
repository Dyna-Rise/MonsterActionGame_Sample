using System;
using System.Collections;
using System.Text;
using MonsterAction.Battle.Boss;
using MonsterAction.Data;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MonsterAction.Home.UI
{
    /// <summary>
    /// 出撃画面（村の出口の DepartureGate に入ると開く）。
    ///   ・モンスター … 戦うボスを1体選ぶ（GameData.SelectedBossId）
    ///   ・出撃する   … Battle シーンへ（BattleBossSpawner が選んだボスに差し替える）
    ///
    /// 【新しいボスを作ったら】
    ///   1. quests に1行追加（id・表示名・説明・ボスの Prefab）
    ///   2. Battle シーンの BossSpawner の一覧にも同じ id で追加
    /// </summary>
    public class QuestMenu : HomeMenuBase
    {
        [Serializable]
        public struct QuestOption
        {
            [Tooltip("BattleBossSpawner の bossId と同じにする")]
            public string id;
            [Tooltip("空ならボスの Prefab の Boss Name を使う")]
            public string displayName;
            [Tooltip("★の数（難しさの目安）")]
            [Range(1, 5)] public int difficulty;
            [TextArea] public string description;
            [Tooltip("報酬金の表示に使う（無くても出撃はできる）")]
            public GameObject bossPrefab;
        }

        private static readonly Color OnColor = new Color(0.45f, 1f, 0.5f);
        private static readonly Color DepartColor = new Color(1f, 0.82f, 0.35f);

        [SerializeField] private QuestOption[] quests =
        {
            new QuestOption
            {
                id = "DragonNightmare", displayName = "ナイトメアドラゴン", difficulty = 3,
                description = "村の近くに現れた黒い竜。\n噛みつき・尻尾・火球に注意。",
            },
        };

        [Tooltip("出撃先のシーン名（Build Profiles のシーン一覧に入っている必要があります）")]
        [SerializeField] private string battleSceneName = "Battle";
        [Tooltip("「出撃！」と出てからシーンを切り替えるまでの秒数")]
        [SerializeField] private float departDelay = 0.8f;

        private bool departing;

        protected override bool CanClose => !departing;

        // 行の並び：quests の順 → 最後に「出撃する」
        private int DepartRowIndex => quests.Length;

        protected override void BuildRows()
        {
            departing = false;

            // 一覧に無いボスが選ばれていたら、先頭のボスにする
            if (FindQuest(GameData.SelectedBossId) < 0 && quests.Length > 0) GameData.SelectedBossId = quests[0].id;

            CreateHeader("モンスター");
            foreach (var _ in quests) CreateRow();
            CreateHeader("出撃");
            CreateRow();
        }

        protected override void Refresh()
        {
            if (moneyText != null) moneyText.text = "所持金  " + FormatMoney(GameData.Money);

            for (int i = 0; i < quests.Length && i < rows.Count; i++)
            {
                bool selected = GameData.SelectedBossId == quests[i].id;
                rows[i].Set(GetName(quests[i]), Stars(quests[i].difficulty), selected ? "● 選択中" : "", OnColor);
            }

            if (DepartRowIndex < rows.Count)
            {
                int q = FindQuest(GameData.SelectedBossId);
                rows[DepartRowIndex].Set("出撃する", q >= 0 ? GetName(quests[q]) : "", "▶", DepartColor);
            }
        }

        protected override void OnRowSelected(int index)
        {
            if (index >= 0 && index < quests.Length)
            {
                var q = quests[index];
                var sb = new StringBuilder();
                sb.AppendLine(GetName(q));
                sb.AppendLine("難しさ  " + Stars(q.difficulty));
                int reward = GetReward(q);
                if (reward > 0) sb.AppendLine("報酬金  " + FormatMoney(reward));
                sb.AppendLine();
                sb.Append(q.description);
                SetDescription(sb.ToString());
            }
            else if (index == DepartRowIndex)
            {
                SetDescription(LoadoutText());
            }
        }

        protected override void OnRowClicked(int index)
        {
            if (departing) return;

            if (index >= 0 && index < quests.Length)
            {
                GameData.SelectedBossId = quests[index].id;
                ShowMessage($"{GetName(quests[index])} に決めた");
                Refresh();
            }
            else if (index == DepartRowIndex)
            {
                if (FindQuest(GameData.SelectedBossId) < 0)
                {
                    ShowMessage("モンスターを選んでください");
                    return;
                }
                StartCoroutine(Depart());
            }
        }

        private IEnumerator Depart()
        {
            departing = true;
            ShowMessage("出撃！", departDelay + 1f);
            yield return new WaitForSecondsRealtime(departDelay);
            SceneManager.LoadScene(battleSceneName);
        }

        // ---- 表示用 ----

        /// <summary>出撃前の確認（タイプ・スキル・アイテム）</summary>
        private string LoadoutText()
        {
            var sb = new StringBuilder();
            int q = FindQuest(GameData.SelectedBossId);
            sb.AppendLine("相手  " + (q >= 0 ? GetName(quests[q]) : "（未選択）"));
            sb.AppendLine();

            var equip = FindFirstObjectByType<EquipmentMenu>(FindObjectsInactive.Include);
            string typeName = equip != null ? equip.GetPlayerTypeName(GameData.SelectedPlayerTypeId) : GameData.SelectedPlayerTypeId;
            sb.AppendLine("タイプ  " + typeName);

            sb.AppendLine("スキル");
            bool any = false;
            foreach (var s in SkillData.All)
            {
                if (!GameData.IsSkillEquipped(s)) continue;
                sb.AppendLine($"  {SkillData.GetName(s)} {SkillData.GetStars(GameData.GetSkillLevel(s))}");
                any = true;
            }
            if (!any) sb.AppendLine("  なし");

            sb.AppendLine("アイテム");
            foreach (var item in ItemDefinition.All)
                sb.AppendLine($"  {item.DisplayName}  {GameData.GetCount(item)} / {item.MaxCount}");

            sb.AppendLine();
            sb.Append("装備は家のドアで変えられます。");
            return sb.ToString();
        }

        private int FindQuest(string id)
        {
            for (int i = 0; i < quests.Length; i++)
                if (quests[i].id == id) return i;
            return -1;
        }

        private static string GetName(QuestOption q)
        {
            if (!string.IsNullOrEmpty(q.displayName)) return q.displayName;
            var boss = q.bossPrefab != null ? q.bossPrefab.GetComponent<BossBase>() : null;
            return boss != null ? boss.BossName : q.id;
        }

        private static int GetReward(QuestOption q)
        {
            var boss = q.bossPrefab != null ? q.bossPrefab.GetComponent<BossBase>() : null;
            return boss != null ? boss.RewardMoney : 0;
        }

        private static string Stars(int n)
        {
            n = Mathf.Clamp(n, 1, 5);
            return new string('★', n) + new string('☆', 5 - n);
        }
    }
}
