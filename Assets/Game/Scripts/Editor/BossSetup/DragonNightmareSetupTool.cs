using System.IO;
using System.Linq;
using MonsterAction.Battle.Boss;
using MonsterAction.Battle.Player;
using MonsterAction.Combat;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace MonsterAction.EditorTools
{
    /// <summary>
    /// ボスのサンプル（ナイトメアドラゴン・アルビノ）の Animator Controller と Prefab を作る（先生用）。
    ///
    /// 作るもの：
    ///   Assets/Game/Animations/Boss/DragonNightmare.controller
    ///   Assets/Game/Prefabs/Boss/Boss_DragonNightmare.prefab
    ///     Boss_DragonNightmare   CharacterController + DragonNightmareBoss + LockOnTarget
    ///     └ Model                Albino.prefab（ボーンの子に部位 / 攻撃判定を追加）
    /// </summary>
    public static class DragonNightmareSetupTool
    {
        private const string DragonRoot = "Assets/MyAssets/FourEvilDragonsHP";
        private const string ModelPrefabPath = DragonRoot + "/Prefab/DragonNightmare/Albino.prefab";
        private const string AnimDir = DragonRoot + "/Animations/DragonNightMare";

        private const string ControllerPath = "Assets/Game/Animations/Boss/DragonNightmare.controller";
        public const string PrefabPath = "Assets/Game/Prefabs/Boss/Boss_DragonNightmare.prefab";

        /// <summary>ループさせるモーション（それ以外はループしない）</summary>
        private static readonly string[] LoopClips = { "idle01", "idle02", "walk", "walkBack", "walkLeft", "walkRight", "run", "Sleep" };

        /// <summary>(Animator のステート名, FBX ファイル名)</summary>
        private static readonly (string state, string file)[] States =
        {
            ("Idle", "idle01"), ("Idle 2", "idle02"), ("Walk", "walk"), ("Walk Back", "walkBack"),
            ("Walk Left", "walkLeft"), ("Walk Right", "walkRight"), ("Run", "run"),
            ("Basic Attack", "Basic Attack"), ("Claw Attack", "Claw Attack"), ("Horn Attack", "Horn Attack"),
            ("Jump", "Jump"), ("Scream", "scream"), ("GetHit", "getHit"), ("Die", "die"),
            ("Defend", "defend"), ("Sleep", "Sleep"),
        };

        // ================================================================
        //  メニュー
        // ================================================================

        [MenuItem("MonsterAction/Boss/Build Dragon Nightmare Sample", priority = 0)]
        public static void Build()
        {
            if (!PlayerSetupCommon.ConfirmOverwrite(PrefabPath, ControllerPath)) return;

            ConfigureClipImports();
            var ctrl = BuildController();
            BuildPrefab(ctrl);
        }

        [MenuItem("MonsterAction/Boss/Place Selected Boss Prefab In Scene", priority = 1)]
        public static void PlaceInScene()
        {
            var selected = Selection.activeObject as GameObject;
            GameObject prefab = selected != null && PrefabUtility.IsPartOfPrefabAsset(selected) &&
                                selected.GetComponent<BossBase>() != null
                ? selected
                : AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            if (prefab == null)
            {
                EditorUtility.DisplayDialog("Boss", "先に Build Dragon Nightmare Sample を実行してください。", "OK");
                return;
            }

            var existing = Object.FindFirstObjectByType<BossBase>();
            if (existing != null)
            {
                if (!EditorUtility.DisplayDialog("Boss", $"シーンの {existing.name} を {prefab.name} に置き換えますか？", "置き換える", "キャンセル"))
                    return;
                Undo.DestroyObjectImmediate(existing.gameObject);
            }

            // プレイヤーの 20m 前に、プレイヤーの方を向けて置く
            var player = Object.FindFirstObjectByType<PlayerBattleController>();
            Vector3 pos = player != null ? player.transform.position + player.transform.forward * 20f : Vector3.zero;
            pos = SnapToGround(pos);

            var boss = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            Undo.RegisterCreatedObjectUndo(boss, "Place Boss");
            Quaternion rot = player != null ? Quaternion.LookRotation(-player.transform.forward) : Quaternion.identity;
            boss.transform.SetPositionAndRotation(pos, rot);

            Selection.activeGameObject = boss;
            EditorSceneManager.MarkSceneDirty(boss.scene);
        }

        // ================================================================
        //  モーションのインポート設定
        // ================================================================

        private static void ConfigureClipImports()
        {
            foreach (var (_, file) in States)
            {
                string path = $"{AnimDir}/{file}.fbx";
                var importer = AssetImporter.GetAtPath(path) as ModelImporter;
                if (importer == null) throw new FileNotFoundException(path);

                bool loop = LoopClips.Contains(file);
                var clips = importer.clipAnimations;
                if (clips == null || clips.Length == 0) clips = importer.defaultClipAnimations;
                bool changed = false;
                foreach (var c in clips)
                {
                    if (c.loopTime == loop) continue;
                    c.loopTime = loop;
                    changed = true;
                }
                if (!changed) continue;
                importer.clipAnimations = clips;
                importer.SaveAndReimport();
            }
        }

        // ================================================================
        //  Animator Controller
        // ================================================================

        private static AnimatorController BuildController()
        {
            PlayerSetupCommon.EnsureFolder(Path.GetDirectoryName(ControllerPath).Replace('\\', '/'));
            if (File.Exists(ControllerPath)) AssetDatabase.DeleteAsset(ControllerPath);
            var ctrl = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
            var sm = ctrl.layers[0].stateMachine;

            // 遷移（矢印）は作らない。スクリプトから Play("ステート名") で切り替える
            foreach (var (state, file) in States)
            {
                var s = sm.AddState(state);
                s.motion = PlayerSetupCommon.LoadClipAt($"{AnimDir}/{file}.fbx");
                if (state == "Idle") sm.defaultState = s;
            }
            EditorUtility.SetDirty(ctrl);
            return ctrl;
        }

        // ================================================================
        //  Prefab
        // ================================================================

        private static void BuildPrefab(AnimatorController ctrl)
        {
            var modelSrc = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPrefabPath);
            if (modelSrc == null) throw new FileNotFoundException(ModelPrefabPath);

            var root = new GameObject("Boss_DragonNightmare");

            // 体の当たり（プレイヤーがすり抜けないように）
            var cc = root.AddComponent<CharacterController>();
            cc.center = new Vector3(0f, 2.5f, 0f);
            cc.height = 5f;
            cc.radius = 1.8f;
            cc.stepOffset = 0.5f;
            cc.slopeLimit = 50f;

            var boss = root.AddComponent<DragonNightmareBoss>();
            var bossSo = new SerializedObject(boss);
            bossSo.FindProperty("bossName").stringValue = "ナイトメアドラゴン";
            bossSo.ApplyModifiedPropertiesWithoutUndo();

            var model = (GameObject)PrefabUtility.InstantiatePrefab(modelSrc, root.transform);
            model.name = "Model";
            model.transform.localPosition = Vector3.zero;
            model.transform.localRotation = Quaternion.identity;

            var animator = model.GetComponentInChildren<Animator>();
            animator.runtimeAnimatorController = ctrl;
            animator.applyRootMotion = false;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;

            // ---- 部位（ダメージを受ける場所）：(ボーン名, 部位名, 半径 m, 倍率) ----
            AddHurtbox(model, "Head", "頭", 1.0f, 1.5f);   // 弱点
            AddHurtbox(model, "Neck02", "首", 0.8f, 1.2f);
            AddHurtbox(model, "Chest", "胴", 1.8f, 1.0f);
            AddHurtbox(model, "Spine01", "胴", 1.8f, 1.0f);
            AddHurtbox(model, "Spine02", "胴", 1.6f, 1.0f);
            AddHurtbox(model, "Tail01", "尻尾", 1.1f, 1.0f);
            AddHurtbox(model, "Tail02", "尻尾", 0.9f, 1.0f);
            AddHurtbox(model, "Tail03", "尻尾", 0.7f, 1.0f);
            AddHurtbox(model, "Tail04", "尻尾", 0.6f, 1.0f);
            AddHurtbox(model, "L_UpperLeg", "脚", 1.0f, 0.8f);
            AddHurtbox(model, "R_UpperReg", "脚", 1.0f, 0.8f);
            AddHurtbox(model, "L_Lowerleg01", "脚", 0.8f, 0.8f);
            AddHurtbox(model, "R_Rowerleg01", "脚", 0.8f, 0.8f);
            AddHurtbox(model, "R_Wrist", "翼", 0.9f, 0.9f);
            AddHurtbox(model, "R_Wrist 1", "翼", 0.9f, 0.9f);

            // ---- 攻撃判定 ----
            var bite = AddAttackHitbox(model, "Jaw01", "Hitbox_Bite", 1.4f);   // ブレスの発射位置にも使う
            var clawR = AddAttackHitbox(model, "R_Hand", "Hitbox_Claw", 1.6f);
            var clawL = AddAttackHitbox(model, "R_Hand 1", "Hitbox_Claw", 1.6f);
            var body = AddAttackHitbox(model, "Chest", "Hitbox_Body", 2.8f);   // 突進・ドリフト
            boss.EditorSetup(bite, new[] { clawR, clawL }.Where(h => h != null).ToArray(), body);

            // ---- ロックオン（胸を狙う） ----
            var lockOn = root.AddComponent<LockOnTarget>();
            var chest = PlayerSetupCommon.FindChildByName(model, "Chest");
            if (chest != null)
            {
                var lockSo = new SerializedObject(lockOn);
                lockSo.FindProperty("aimPoint").objectReferenceValue = chest;
                lockSo.FindProperty("priority").intValue = 10;
                lockSo.ApplyModifiedPropertiesWithoutUndo();
            }

            PlayerSetupCommon.SavePrefab(root, PrefabPath);
        }

        /// <summary>
        /// ボーンの子に「部位」を作る。ボーンは 0.01 倍などに縮小されていることがあるので、
        /// 子のスケールで打ち消して、半径を m 単位で指定できるようにしている。
        /// </summary>
        private static void AddHurtbox(GameObject model, string boneName, string partName, float radius, float multiplier)
        {
            var bone = PlayerSetupCommon.FindChildByName(model, boneName);
            if (bone == null)
            {
                Debug.LogWarning($"[BossSetup] ボーン {boneName} が見つかりません");
                return;
            }
            var go = CreateUnitScaleChild(bone, $"Hurtbox_{partName}");
            var sphere = go.AddComponent<SphereCollider>();
            sphere.radius = radius;
            sphere.isTrigger = true;
            go.AddComponent<BossHurtbox>().EditorSetup(partName, multiplier);
        }

        private static BossAttackHitbox AddAttackHitbox(GameObject model, string boneName, string name, float radius)
        {
            var bone = PlayerSetupCommon.FindChildByName(model, boneName);
            if (bone == null)
            {
                Debug.LogWarning($"[BossSetup] ボーン {boneName} が見つかりません");
                return null;
            }
            var hitbox = CreateUnitScaleChild(bone, name).AddComponent<BossAttackHitbox>();
            hitbox.EditorSetup(radius);
            return hitbox;
        }

        private static GameObject CreateUnitScaleChild(Transform bone, string name)
        {
            var go = new GameObject(name);
            go.transform.SetParent(bone, false);
            Vector3 s = bone.lossyScale;
            go.transform.localScale = new Vector3(1f / s.x, 1f / s.y, 1f / s.z);
            return go;
        }

        private static Vector3 SnapToGround(Vector3 pos)
        {
            var terrain = Terrain.activeTerrain;
            if (terrain != null) pos.y = terrain.SampleHeight(pos) + terrain.transform.position.y;
            else if (Physics.Raycast(pos + Vector3.up * 100f, Vector3.down, out var hit, 500f)) pos.y = hit.point.y;
            return pos;
        }
    }
}
