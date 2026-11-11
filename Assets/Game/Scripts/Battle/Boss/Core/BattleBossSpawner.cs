using System;
using MonsterAction.Data;
using UnityEngine;

namespace MonsterAction.Battle.Boss
{
    /// <summary>
    /// Home の出撃画面で選んだボス（GameData.SelectedBossId）を Battle に出します。
    /// Battle シーンに置いてあるボスを、選ばれたボスの Prefab に差し替えます
    /// （同じボスなら何もしない。一覧に無い ID ならシーンのボスをそのまま使う）。
    /// シーンにボスが置いていないときは、このオブジェクトの位置に出します。
    ///
    /// 【新しいボスを作ったら】
    ///   1. bosses に「ID と Prefab」を1行追加
    ///   2. Home の出撃画面（QuestMenu）の quests にも同じ ID で1行追加
    /// </summary>
    [DefaultExecutionOrder(-500)] // HUD やボス自身より先に差し替える
    public class BattleBossSpawner : MonoBehaviour
    {
        [Serializable]
        public struct Entry
        {
            [Tooltip("QuestMenu の id と同じにする")]
            public string bossId;
            public GameObject prefab;
        }

        [SerializeField] private Entry[] bosses =
        {
            new Entry { bossId = "DragonNightmare" },
        };

        private void Awake()
        {
            var prefab = FindPrefab(GameData.SelectedBossId);
            if (prefab == null) return;
            var prefabBoss = prefab.GetComponent<BossBase>();
            if (prefabBoss == null)
            {
                Debug.LogWarning($"[BossSpawner] {prefab.name} に BossBase（を継承したスクリプト）が付いていません", this);
                return;
            }

            var existing = FindFirstObjectByType<BossBase>(FindObjectsInactive.Include);
            if (existing != null && existing.GetType() == prefabBoss.GetType()) return; // もう同じボス

            Vector3 pos = existing != null ? existing.transform.position : transform.position;
            Quaternion rot = existing != null ? existing.transform.rotation : transform.rotation;
            if (existing != null)
            {
                existing.gameObject.SetActive(false);
                Destroy(existing.gameObject);
            }

            var boss = Instantiate(prefab, pos, rot);
            boss.name = prefab.name;
        }

        private GameObject FindPrefab(string bossId)
        {
            foreach (var e in bosses)
                if (e.bossId == bossId) return e.prefab;
            return null;
        }
    }
}
