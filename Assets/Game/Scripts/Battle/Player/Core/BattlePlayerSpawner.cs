using System;
using MonsterAction.Battle.CameraSystem;
using MonsterAction.Data;
using UnityEngine;

namespace MonsterAction.Battle.Player
{
    /// <summary>
    /// Home で選んだプレイヤータイプ（GameData.SelectedPlayerTypeId）の Player を Battle に出します。
    /// Battle シーンに置いてある Player を、選ばれたタイプの Prefab に差し替えます
    /// （同じタイプなら何もしない。一覧に無い ID ならシーンの Player をそのまま使う）。
    ///
    /// 新しいタイプを作ったら、types に「ID と Prefab」を1行追加してください。
    /// ID は Home の装備画面（EquipmentMenu）のタイプ一覧の ID と同じにします。
    /// </summary>
    [DefaultExecutionOrder(-500)] // Player やカメラより先に差し替える
    public class BattlePlayerSpawner : MonoBehaviour
    {
        [Serializable]
        public struct Entry
        {
            public string typeId;
            public GameObject prefab;
        }

        [SerializeField] private Entry[] types =
        {
            new Entry { typeId = "SwordShield" },
            new Entry { typeId = "Bow" },
            new Entry { typeId = "Magic" },
        };

        private void Awake()
        {
            var prefab = FindPrefab(GameData.SelectedPlayerTypeId);
            if (prefab == null) return;
            var prefabType = prefab.GetComponent<PlayerBattleController>();
            if (prefabType == null) return;

            var existing = FindFirstObjectByType<PlayerBattleController>(FindObjectsInactive.Include);
            if (existing != null && existing.GetType() == prefabType.GetType()) return; // もう同じタイプ

            Vector3 pos = existing != null ? existing.transform.position : transform.position;
            Quaternion rot = existing != null ? existing.transform.rotation : transform.rotation;
            if (existing != null)
            {
                existing.gameObject.SetActive(false);
                Destroy(existing.gameObject);
            }

            var player = Instantiate(prefab, pos, rot);
            player.name = prefab.name;

            // カメラの追従先を新しい Player に
            var cam = Camera.main != null ? Camera.main.GetComponent<BattleCameraController>() : null;
            if (cam != null) cam.SetTarget(player.transform);
        }

        private GameObject FindPrefab(string typeId)
        {
            foreach (var e in types)
                if (e.typeId == typeId) return e.prefab;
            return null;
        }
    }
}
