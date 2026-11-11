using System;
using System.IO;
using System.Linq;
using MonsterAction.Battle.CameraSystem;
using MonsterAction.Battle.DebugTools;
using MonsterAction.Battle.Player;
using MonsterAction.Combat;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace MonsterAction.EditorTools
{
    /// <summary>
    /// 全 Player タイプ共通のセットアップ処理。
    ///   ・共通 Animator Controller（移動 / 集中移動 / 回避 / 被弾 / 死亡 / 手の握り）の生成
    ///   ・Player の Prefab の土台（CharacterController・入力・タイプのスクリプト・モデル）の生成
    ///   ・シーンへの配置（Player・カメラ・HUD・カカシ）
    /// タイプ別の処理は PlayerASetupTool（片手剣）/ PlayerBSetupTool（弓）を見てください。
    /// </summary>
    public static class PlayerSetupCommon
    {
        public const string KevinRoot = "Assets/MyAssets/Kevin Iglesias/Human Animations";
        public const string KevinAnimRoot = KevinRoot + "/Animations";
        private const string HandsMaskPath = KevinRoot + "/Models/Avatar Masks/Hands/Human Hands Mask.mask";

        public const string AnimatorDir = "Assets/Game/Animations/Player";
        public const string PrefabDir = "Assets/Game/Prefabs/Player";

        // 回避（ローリング）：Mixamo
        private const string RollClipPath = "Assets/MyAssets/Mixamo/Sprinting Forward Roll.fbx";
        private const float RollAnimSpeed = 1.2f;         // 再生速度
        private const float RollDistance = 6f;            // 移動距離（m）
        private const float RollInvincibleStart = 0.05f;  // 無敵開始（秒）
        private const float RollInvincibleRate = 0.55f;   // 無敵終了（回避時間に対する割合）

        private const string MenuRoot = "MonsterAction/Player/";

        // ================================================================
        //  メニュー（共通）
        // ================================================================

        [MenuItem(MenuRoot + "Create Common Animator For New Type...", priority = 100)]
        public static void CreateCommonAnimatorMenu()
        {
            EnsureFolder(AnimatorDir);
            string path = EditorUtility.SaveFilePanelInProject(
                "新しいタイプ用の Animator Controller", "PlayerX_NewType", "controller",
                "共通ステート（移動・集中移動・回避・被弾・死亡）だけが入った Animator Controller を作ります。", AnimatorDir);
            if (string.IsNullOrEmpty(path)) return;

            var ctrl = CreateCommonController(path, LoadKevinClip("Male/Combat/HumanM@CombatIdle01.fbx"));
            AssetDatabase.SaveAssets();
            EditorGUIUtility.PingObject(ctrl);
        }

        [MenuItem(MenuRoot + "Place Selected Player Prefab + Camera In Scene", priority = 200)]
        public static void PlaceSelectedPlayerMenu()
        {
            // Project ウィンドウで選択中の Player Prefab。無ければ PlayerA
            var selected = Selection.activeObject as GameObject;
            GameObject prefab = selected != null && PrefabUtility.IsPartOfPrefabAsset(selected) &&
                                selected.GetComponent<PlayerBattleController>() != null
                ? selected
                : AssetDatabase.LoadAssetAtPath<GameObject>(PlayerASetupTool.PrefabPath);

            if (prefab == null)
            {
                EditorUtility.DisplayDialog("Player", "Player の Prefab がありません。先に Build PlayerA などを実行してください。", "OK");
                return;
            }
            PlacePlayerInScene(prefab);
        }

        [MenuItem(MenuRoot + "Place Training Dummy In Scene", priority = 201)]
        public static void PlaceDummyMenu()
        {
            var player = Object.FindFirstObjectByType<PlayerBattleController>();
            Vector3 basePos = player != null ? player.transform.position + player.transform.forward * 4f : GetSpawnPosition();

            var dummy = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            dummy.name = "TrainingDummy";
            dummy.transform.position = SnapToGround(basePos) + Vector3.up * 1f;
            dummy.AddComponent<TrainingDummy>();
            var lockOn = dummy.AddComponent<LockOnTarget>(); // ロックオンの練習用
            var so = new SerializedObject(lockOn);
            so.FindProperty("offset").vector3Value = new Vector3(0f, 0.4f, 0f);
            so.ApplyModifiedPropertiesWithoutUndo();
            dummy.AddComponent<DummyAttacker>(); // 遠距離は射撃、近距離は殴り（不要ならチェックを外す）

            Undo.RegisterCreatedObjectUndo(dummy, "Place Training Dummy");
            Selection.activeGameObject = dummy;
            EditorSceneManager.MarkSceneDirty(dummy.scene);
        }

        // ================================================================
        //  Animator Controller（共通部分）
        // ================================================================

        /// <summary>
        /// 共通ステートだけが入った Animator Controller を作る（既にあれば作り直す）。
        /// タイプ固有の攻撃ステートは、戻り値に AddActionState などで追加する。
        /// </summary>
        /// <param name="idleClip">待機モーション（タイプごとに武器の構えが違うので引数で渡す）</param>
        public static AnimatorController CreateCommonController(string path, AnimationClip idleClip)
        {
            ConfigureRollImport();

            var walk = LoadKevinClip("Male/Movement/Walk/HumanM@Walk01_Forward.fbx");
            var run = LoadKevinClip("Male/Movement/Run/HumanM@Run01_Forward.fbx");
            var roll = LoadClipAt(RollClipPath);

            if (File.Exists(path)) AssetDatabase.DeleteAsset(path);
            var ctrl = AnimatorController.CreateAnimatorControllerAtPath(path);

            ctrl.AddParameter("Speed", AnimatorControllerParameterType.Float);
            ctrl.AddParameter("MoveX", AnimatorControllerParameterType.Float);
            ctrl.AddParameter("MoveY", AnimatorControllerParameterType.Float);
            ctrl.AddParameter("DodgeX", AnimatorControllerParameterType.Float);
            ctrl.AddParameter("DodgeY", AnimatorControllerParameterType.Float);
            ctrl.AddParameter(new AnimatorControllerParameter
            {
                name = "ActionSpeed", type = AnimatorControllerParameterType.Float, defaultFloat = 1f
            });

            var sm = ctrl.layers[0].stateMachine;

            // --- Locomotion（Speed で 待機 / 歩き / 走り / ダッシュ） ---
            var loco = ctrl.CreateBlendTreeInController("Locomotion", out var locoTree, 0);
            locoTree.blendType = BlendTreeType.Simple1D;
            locoTree.blendParameter = "Speed";
            locoTree.useAutomaticThresholds = false;
            locoTree.AddChild(idleClip, 0f);
            locoTree.AddChild(walk, 0.4f);
            locoTree.AddChild(run, 1f);
            locoTree.AddChild(run, PlayerBattleController.SprintAnimBlendValue); // ダッシュ = 走りの速回し
            var children = locoTree.children;
            children[children.Length - 1].timeScale = 1.5f;
            locoTree.children = children;
            sm.defaultState = loco;

            // --- FocusLocomotion（集中モード：向きを固定したまま8方向に走る） ---
            var focusTree = new BlendTree
            {
                name = "FocusLocomotionTree",
                blendType = BlendTreeType.FreeformDirectional2D,
                blendParameter = "MoveX",
                blendParameterY = "MoveY",
                useAutomaticThresholds = false,
                hideFlags = HideFlags.HideInHierarchy,
            };
            AssetDatabase.AddObjectToAsset(focusTree, ctrl);
            focusTree.AddChild(idleClip, Vector2.zero);
            focusTree.AddChild(run, new Vector2(0f, 1f));
            focusTree.AddChild(LoadKevinClip("Male/Movement/Run/HumanM@Run01_Backward.fbx"), new Vector2(0f, -1f));
            focusTree.AddChild(LoadKevinClip("Male/Movement/Run/HumanM@Run01_Left.fbx"), new Vector2(-1f, 0f));
            focusTree.AddChild(LoadKevinClip("Male/Movement/Run/HumanM@Run01_Right.fbx"), new Vector2(1f, 0f));
            focusTree.AddChild(LoadKevinClip("Male/Movement/Run/HumanM@Run01_ForwardLeft.fbx"), new Vector2(-0.7f, 0.7f));
            focusTree.AddChild(LoadKevinClip("Male/Movement/Run/HumanM@Run01_ForwardRight.fbx"), new Vector2(0.7f, 0.7f));
            focusTree.AddChild(LoadKevinClip("Male/Movement/Run/HumanM@Run01_BackwardLeft.fbx"), new Vector2(-0.7f, -0.7f));
            focusTree.AddChild(LoadKevinClip("Male/Movement/Run/HumanM@Run01_BackwardRight.fbx"), new Vector2(0.7f, -0.7f));
            sm.AddState("FocusLocomotion").motion = focusTree;

            // --- 回避（ローリング） ---
            var dodge = sm.AddState("Dodge");
            dodge.motion = roll;
            dodge.speed = RollAnimSpeed;

            // --- 被弾 / 死亡 ---
            sm.AddState("Hit").motion = LoadKevinClip("Male/Combat/HumanM@CombatDamage01.fbx");
            var death = LoadKevinClip("Male/Combat/HumanM@Death01.fbx");
            sm.AddState("Death").motion = death;
            // ダウン：専用モーションが無いので倒れるモーションで代用。
            // 起き上がり（GetUp）ステートは、モーションを手に入れたら追加する（無ければスクリプトがそのまま立たせる）
            sm.AddState("KnockDown").motion = death;
            // アイテム使用：専用モーションが無いので「背中に手を伸ばす」動きで代用
            sm.AddState("UseItem").motion = LoadKevinClip("Male/Misc/Unsheathe/HumanM@UnsheatheBack01_R.fbx");

            // --- Hands レイヤー（武器を握る手の形を常に上書き） ---
            var handsSm = new AnimatorStateMachine { name = "Hands", hideFlags = HideFlags.HideInHierarchy };
            AssetDatabase.AddObjectToAsset(handsSm, ctrl);
            var grip = handsSm.AddState("Grip");
            grip.motion = LoadKevinClip("Masked Poses/Human@ObjectGripHands01.fbx");
            handsSm.defaultState = grip;
            ctrl.AddLayer(new AnimatorControllerLayer
            {
                name = "Hands",
                stateMachine = handsSm,
                avatarMask = AssetDatabase.LoadAssetAtPath<AvatarMask>(HandsMaskPath),
                defaultWeight = 1f,
                blendingMode = AnimatorLayerBlendingMode.Override,
            });

            EditorUtility.SetDirty(ctrl);
            return ctrl;
        }

        /// <summary>攻撃などのアクション用ステートを追加（再生速度は ActionSpeed パラメータで変えられる）</summary>
        public static AnimatorState AddActionState(AnimatorController ctrl, string name, AnimationClip clip)
        {
            var state = ctrl.layers[0].stateMachine.AddState(name);
            state.motion = clip;
            state.speedParameterActive = true;
            state.speedParameter = "ActionSpeed";
            return state;
        }

        // ================================================================
        //  Prefab（共通部分）
        // ================================================================

        /// <summary>
        /// Player の土台を作る（まだ Prefab として保存はしない）。
        /// 返ってきた root にタイプ固有の設定をしてから SavePrefab を呼ぶ。
        /// </summary>
        public static GameObject CreatePlayerRoot(string name, string modelPrefabPath, AnimatorController controller,
                                                  Type playerType, out GameObject model, out PlayerBattleController player)
        {
            var modelSrc = AssetDatabase.LoadAssetAtPath<GameObject>(modelPrefabPath);
            if (modelSrc == null) throw new FileNotFoundException(modelPrefabPath);

            var root = new GameObject(name) { tag = "Player" };

            var cc = root.AddComponent<CharacterController>();
            cc.center = new Vector3(0f, 0.9f, 0f);
            cc.height = 1.8f;
            cc.radius = 0.3f;
            cc.stepOffset = 0.3f;
            cc.skinWidth = 0.03f;

            root.AddComponent<PlayerInputReader>();
            player = (PlayerBattleController)root.AddComponent(playerType);

            model = (GameObject)PrefabUtility.InstantiatePrefab(modelSrc, root.transform);
            model.name = "Model";
            model.transform.localPosition = Vector3.zero;
            model.transform.localRotation = Quaternion.identity;

            var animator = model.GetComponentInChildren<Animator>();
            animator.runtimeAnimatorController = controller;
            animator.applyRootMotion = false;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;

            // 回避の設定（Kevin Iglesias の SpineProxy はローリング中だけ切る）
            float rollDuration = LoadClipAt(RollClipPath).length / RollAnimSpeed;
            var spineProxies = model.GetComponentsInChildren<MonoBehaviour>(true)
                .Where(m => m != null && m.GetType().Name == "SpineProxy")
                .Cast<Behaviour>()
                .ToArray();
            player.EditorSetupDodge(true, RollDistance, rollDuration, RollInvincibleStart,
                                    rollDuration * RollInvincibleRate, spineProxies);

            return root;
        }

        /// <summary>Prefab として保存し、作業用の GameObject を消す</summary>
        public static void SavePrefab(GameObject root, string prefabPath)
        {
            try
            {
                EnsureFolder(Path.GetDirectoryName(prefabPath).Replace('\\', '/'));
                PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
            AssetDatabase.SaveAssets();
            EditorGUIUtility.PingObject(AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath));
            Debug.Log($"[PlayerSetup] 作成完了: {prefabPath}");
        }

        /// <summary>上書き確認。作ってよければ true</summary>
        public static bool ConfirmOverwrite(params string[] paths)
        {
            var existing = paths.Where(File.Exists).ToArray();
            if (existing.Length == 0) return true;
            return EditorUtility.DisplayDialog("作り直しますか？",
                string.Join("\n", existing) + "\n\nを上書きします（手で調整した値や追加したステートは消えます）。",
                "作り直す", "キャンセル");
        }

        // ================================================================
        //  シーン配置
        // ================================================================

        public static void PlacePlayerInScene(GameObject prefab)
        {
            Vector3 spawnPos = GetSpawnPosition();
            Quaternion spawnRot = Quaternion.identity;

            var existing = Object.FindFirstObjectByType<PlayerBattleController>();
            if (existing != null)
            {
                if (!EditorUtility.DisplayDialog("Player", $"シーンの {existing.name} を {prefab.name} に置き換えますか？", "置き換える", "キャンセル"))
                    return;
                spawnPos = existing.transform.position;
                spawnRot = existing.transform.rotation;
                Undo.DestroyObjectImmediate(existing.gameObject);
            }

            var player = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            Undo.RegisterCreatedObjectUndo(player, "Place Player");
            player.transform.SetPositionAndRotation(spawnPos, spawnRot);

            // カメラ
            Camera cam = Camera.main;
            if (cam == null)
            {
                var camGo = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener)) { tag = "MainCamera" };
                Undo.RegisterCreatedObjectUndo(camGo, "Create Camera");
                cam = camGo.GetComponent<Camera>();
            }
            var follow = cam.GetComponent<BattleCameraController>();
            if (follow == null) follow = Undo.AddComponent<BattleCameraController>(cam.gameObject);
            var so = new SerializedObject(follow);
            so.FindProperty("target").objectReferenceValue = player.transform;
            so.ApplyModifiedProperties();
            cam.transform.SetPositionAndRotation(player.transform.position + new Vector3(0f, 2.5f, -5f), Quaternion.Euler(15f, 0f, 0f));

            // HUD（Canvas）とバトル進行役
            BattleHUDBuilder.EnsureHUDAndFlowInScene();
            BattleHUDBuilder.EnsureScenesInBuild();

            Selection.activeGameObject = player;
            EditorSceneManager.MarkSceneDirty(player.scene);
        }

        // ================================================================
        //  Utility
        // ================================================================

        private static void ConfigureRollImport() => ConfigureMixamoClip(RollClipPath, loop: false);

        /// <summary>
        /// Mixamo のモーションを、このプロジェクト用のインポート設定にする（どのタイプからも使える）。
        /// ・Humanoid（Kevin Iglesias のモデルにリターゲットするため）
        /// ・回転と高さ(Y)はポーズに焼き込み（転がる上下動などはそのまま見せる）
        /// ・水平移動(XZ)は焼き込まない → その場で動き、移動はスクリプトが行う
        /// Start/End（トリミング）を手で設定していれば、それは保持します。
        /// ※ 再生するときは、アクション側で UseMixamoMotion() を呼んでください（SpineProxy 対策）
        /// </summary>
        public static void ConfigureMixamoClip(string fbxPath, bool loop)
        {
            var importer = AssetImporter.GetAtPath(fbxPath) as ModelImporter;
            if (importer == null) throw new FileNotFoundException($"モーションが見つかりません: {fbxPath}");

            if (importer.animationType != ModelImporterAnimationType.Human)
            {
                importer.animationType = ModelImporterAnimationType.Human;
                importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            }

            var clips = importer.clipAnimations;
            if (clips == null || clips.Length == 0) clips = importer.defaultClipAnimations;
            foreach (var clip in clips)
            {
                clip.loopTime = loop;
                clip.loopPose = loop;
                clip.lockRootRotation = true;
                clip.keepOriginalOrientation = true;
                clip.lockRootHeightY = true;
                clip.keepOriginalPositionY = true;
                clip.lockRootPositionXZ = false;
            }
            importer.clipAnimations = clips;
            importer.SaveAndReimport();
        }

        /// <summary>Kevin Iglesias の Animations フォルダからクリップを読む</summary>
        public static AnimationClip LoadKevinClip(string relativePath) => LoadClipAt($"{KevinAnimRoot}/{relativePath}");

        public static AnimationClip LoadClipAt(string path)
        {
            var clip = AssetDatabase.LoadAllAssetsAtPath(path)
                .OfType<AnimationClip>()
                .FirstOrDefault(x => !x.name.StartsWith("__preview__"));
            if (clip == null) throw new FileNotFoundException($"AnimationClip が見つかりません: {path}");
            return clip;
        }

        public static Transform FindChildByName(GameObject root, string name) =>
            root.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == name);

        private static Vector3 GetSpawnPosition()
        {
            var terrain = Terrain.activeTerrain;
            if (terrain == null) return Vector3.zero;
            Vector3 center = terrain.transform.position + terrain.terrainData.size * 0.5f;
            return SnapToGround(center);
        }

        private static Vector3 SnapToGround(Vector3 pos)
        {
            var terrain = Terrain.activeTerrain;
            if (terrain != null) pos.y = terrain.SampleHeight(pos) + terrain.transform.position.y;
            else if (Physics.Raycast(pos + Vector3.up * 100f, Vector3.down, out var hit, 500f)) pos.y = hit.point.y;
            return pos;
        }

        public static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = Path.GetDirectoryName(path).Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }
    }
}
