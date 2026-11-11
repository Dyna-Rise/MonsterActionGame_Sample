using System;
using System.Collections;
using MonsterAction.Battle.Player;
using MonsterAction.Battle.UI;
using MonsterAction.Data;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace MonsterAction.Battle.Flow
{
    /// <summary>
    /// Battle シーンの進行役（シーンに1つ）。
    ///
    ///   プレイヤー死亡 → 少し待つ → 「QUEST FAILED」表示 → 数秒後に Home シーンへ
    ///   ボス討伐       → 報酬金を所持金に追加 → 少し待つ → 「QUEST CLEAR」＋報酬表示 → 数秒後に Home シーンへ
    ///
    /// 【Boss 担当の人へ】
    /// ボスの HP が 0 になったら BattleFlowController.Instance.ClearQuest() を呼んでください。
    ///
    /// 【今後の拡張】
    /// 報酬金の加算、選んだボスの受け渡しなどは、Home と共有するデータ（GameData など）を作ってから
    /// OnQuestCleared / OnQuestFailed の中に足していく予定です。
    /// </summary>
    public class BattleFlowController : MonoBehaviour
    {
        public enum Phase { Playing, Failed, Cleared }

        public static BattleFlowController Instance { get; private set; }

        [Header("シーン遷移")]
        [Tooltip("戻り先のシーン名（Build Profiles のシーン一覧に入っている必要があります）")]
        [SerializeField] private string homeSceneName = "Home";
        [Tooltip("死亡 / 討伐から結果表示までの秒数（死亡モーションなどを見せる時間）")]
        [SerializeField] private float resultDelay = 2f;
        [Tooltip("結果表示から Home に戻るまでの秒数")]
        [SerializeField] private float returnDelay = 5f;

        [Header("参照")]
        [SerializeField] private BattleResultUI resultUI;

        [Header("デバッグ")]
        [Tooltip("ON のとき F1 = 自分を倒す（失敗のテスト）、F2 = クエストクリア")]
        [SerializeField] private bool debugKeys = true;

        public Phase CurrentPhase { get; private set; } = Phase.Playing;

        /// <summary>クエスト失敗が確定した瞬間</summary>
        public event Action QuestFailed;
        /// <summary>クエストクリアが確定した瞬間</summary>
        public event Action QuestCleared;

        private PlayerBattleController player;

        private void Awake()
        {
            Instance = this;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
            if (player != null) player.Died -= FailQuest;
        }

        private void Start()
        {
            player = PlayerBattleController.Active;
            if (player != null) player.Died += FailQuest;
            else Debug.LogWarning("[BattleFlow] プレイヤーが見つかりません", this);
        }

        private void Update()
        {
            if (!debugKeys || CurrentPhase != Phase.Playing || Keyboard.current == null) return;
            if (Keyboard.current.f1Key.wasPressedThisFrame && player != null)
                player.ApplyDamage(new Combat.DamageInfo(99999f, player.transform.position, Vector3.forward, gameObject));
            if (Keyboard.current.f2Key.wasPressedThisFrame)
                ClearQuest();
        }

        /// <summary>クエスト失敗（プレイヤー死亡で自動的に呼ばれる）</summary>
        public void FailQuest()
        {
            if (CurrentPhase != Phase.Playing) return;
            CurrentPhase = Phase.Failed;
            QuestFailed?.Invoke();
            StartCoroutine(EndRoutine(cleared: false));
        }

        /// <summary>このクエストでもらった報酬金</summary>
        public int EarnedReward { get; private set; }

        /// <summary>
        /// クエストクリア（ボス討伐時に BossBase が自動で呼ぶ）。
        /// 報酬金はボスの Reward Money。テスト用に reward を直接指定することもできる
        /// </summary>
        public void ClearQuest(int reward = -1)
        {
            if (CurrentPhase != Phase.Playing) return;
            CurrentPhase = Phase.Cleared;

            // 報酬金を所持金に足す（Home に戻っても残る）
            EarnedReward = reward >= 0 ? reward : (Boss.BossBase.Active != null ? Boss.BossBase.Active.RewardMoney : 0);
            GameData.AddMoney(EarnedReward);
            GameData.AddQuestClear();

            QuestCleared?.Invoke();
            StartCoroutine(EndRoutine(cleared: true));
        }

        private IEnumerator EndRoutine(bool cleared)
        {
            // 以降は操作させない
            var input = player != null ? player.GetComponent<PlayerInputReader>() : null;
            if (input != null) input.InputEnabled = false;

            yield return new WaitForSeconds(resultDelay);

            if (resultUI != null)
            {
                if (cleared) resultUI.ShowCleared(returnDelay, EarnedReward, GameData.Money);
                else resultUI.ShowFailed(returnDelay);
            }

            yield return new WaitForSeconds(returnDelay);

            SceneManager.LoadScene(homeSceneName);
        }
    }
}
