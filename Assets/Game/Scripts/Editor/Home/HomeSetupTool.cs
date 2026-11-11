using System.IO;
using System.Linq;
using MonsterAction.Battle.CameraSystem;
using MonsterAction.Battle.Player;
using MonsterAction.Home.Interaction;
using MonsterAction.Home.Player;
using MonsterAction.Home.UI;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace MonsterAction.EditorTools
{
    /// <summary>
    /// Home シーンのセットアップ。
    ///   MonsterAction > Home > Setup Home Scene を実行すると、開いているシーンに次のものを置きます。
    ///     ・村を歩くプレイヤー（HomePlayer）とカメラ（見下ろしの HomeCameraController）
    ///     ・画面表示（HomeHUD）、ショップ（ShopMenu）、装備画面（EquipmentMenu）、EventSystem
    ///     ・ドア（Door_painted.001）を調べられるようにする → 装備画面
    ///     ・村人（HumanF_Dummy_Red）を置いて話しかけられるようにする → ショップ
    ///     ・出撃口（DepartureGate）… 柵の間などに置く箱。入ると出撃画面（QuestMenu、ボス選択）
    ///   もう置いてあるものは作り直しません（何度実行しても大丈夫）。
    /// </summary>
    public static class HomeSetupTool
    {
        private const string MenuRoot = "MonsterAction/Home/";

        public const string DoorObjectName = "Door_painted.001";

        private const string CharactersDir = PlayerSetupCommon.KevinRoot + "/Unity Demo Scenes/Human Melee Animations/Prefabs/Characters";
        private const string PlayerModelPath = CharactersDir + "/HumanM_Dummy_Red.prefab";
        private const string VillagerModelPath = CharactersDir + "/HumanF_Dummy_Red.prefab";

        private const string ShaderPath = "Assets/Game/Shaders/InteractHighlight.shader";
        private const string HighlightMaterialPath = "Assets/Game/Resources/InteractHighlight.mat";

        private const string HomeAnimDir = "Assets/Game/Animations/Home";
        private const string PlayerControllerPath = HomeAnimDir + "/HomePlayer.controller";
        private const string VillagerControllerPath = HomeAnimDir + "/Villager.controller";

        private const string HomePrefabDir = "Assets/Game/Prefabs/Home";
        private const string PlayerPrefabPath = HomePrefabDir + "/HomePlayer.prefab";
        private const string VillagerPrefabPath = HomePrefabDir + "/Villager_Shop.prefab";
        // 以前の版で置いていた受付の村人（出撃口に変えたので、シーンにあれば取り除く）
        private const string QuestVillagerPrefabPath = HomePrefabDir + "/Villager_Quest.prefab";

        private const string UIPrefabDir = "Assets/Game/Prefabs/UI";
        private const string HUDPrefabPath = UIPrefabDir + "/HomeHUD.prefab";
        private const string ShopPrefabPath = UIPrefabDir + "/ShopMenu.prefab";
        private const string EquipmentPrefabPath = UIPrefabDir + "/EquipmentMenu.prefab";
        private const string QuestPrefabPath = UIPrefabDir + "/QuestMenu.prefab";

        // ================================================================
        //  メニュー
        // ================================================================

        [MenuItem(MenuRoot + "Setup Home Scene", priority = 0)]
        public static void SetupSceneMenu()
        {
            ItemSetupTool.EnsureDefaultItems();
            var highlight = EnsureHighlightMaterial();

            // 古い版（CharacterAnimatorSetup が無い）の Prefab は作り直す
            var playerPrefab = LoadOrBuild(PlayerPrefabPath, BuildPlayerPrefab, needsAnimatorSetup: true);
            var villagerPrefab = LoadOrBuild(VillagerPrefabPath, () => BuildShopVillagerPrefab(highlight), needsAnimatorSetup: true);
            var hudPrefab = LoadOrBuild(HUDPrefabPath, BuildHUDPrefab);
            var shopPrefab = LoadOrBuild(ShopPrefabPath, () => BuildMenuPrefab<ShopMenu>("ShopMenu", "ショップ", false));
            var equipPrefab = LoadOrBuild(EquipmentPrefabPath, () => BuildMenuPrefab<EquipmentMenu>("EquipmentMenu", "装備・タイプ選択", true));
            var questPrefab = LoadOrBuild(QuestPrefabPath, () => BuildMenuPrefab<QuestMenu>("QuestMenu", "出撃（モンスター選択）", false));

            EnsureEventSystem();
            var player = EnsurePlayer(playerPrefab);
            EnsureCamera(player);
            EnsureInstance<HomeHUD>(hudPrefab);
            var shop = EnsureInstance<ShopMenu>(shopPrefab);
            var equip = EnsureInstance<EquipmentMenu>(equipPrefab);
            var quest = EnsureInstance<QuestMenu>(questPrefab);
            FillQuestBossPrefabs(quest);

            SetupDoor(equip, highlight);
            // ショップの村人はプレイヤーの右前
            SetupVillager(villagerPrefab, player, shop, new Vector3(4f, 0f, 2f));
            RemoveOldQuestVillager();
            var gate = SetupDepartureGate(player, quest);

            BattleHUDBuilder.EnsureScenesInBuild();
            EditorSceneManager.MarkSceneDirty(player.scene);
            Selection.activeGameObject = gate != null ? gate : player;
            Debug.Log("[HomeSetup] 完了。村人の位置は Scene ビューで好きな場所へ動かしてください。" +
                      "出撃口（DepartureGate、緑の箱）は柵の間に動かし、Size で通り道をふさぐ大きさにしてください。");
        }

        [MenuItem(MenuRoot + "Rebuild Home Prefabs (Player / Villagers / UI)", priority = 20)]
        public static void RebuildPrefabsMenu()
        {
            var paths = new[] { PlayerPrefabPath, VillagerPrefabPath, HUDPrefabPath, ShopPrefabPath, EquipmentPrefabPath, QuestPrefabPath };
            if (!PlayerSetupCommon.ConfirmOverwrite(paths)) return;
            var highlight = EnsureHighlightMaterial();
            BuildPlayerPrefab();
            BuildShopVillagerPrefab(highlight);
            BuildHUDPrefab();
            BuildMenuPrefab<ShopMenu>("ShopMenu", "ショップ", false);
            BuildMenuPrefab<EquipmentMenu>("EquipmentMenu", "装備・タイプ選択", true);
            BuildMenuPrefab<QuestMenu>("QuestMenu", "出撃（モンスター選択）", false);
            Debug.Log("[HomeSetup] Prefab を作り直しました。シーンに置いてある分は自動で更新されます（シーンで変えた値は残る）。");
        }

        // ================================================================
        //  シーンへの配置
        // ================================================================

        private static GameObject LoadOrBuild(string path, System.Func<GameObject> build, bool needsAnimatorSetup = false)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab != null && needsAnimatorSetup && prefab.GetComponent<CharacterAnimatorSetup>() == null) prefab = null;
            return prefab != null ? prefab : build();
        }

        private static void EnsureEventSystem()
        {
            var es = Object.FindFirstObjectByType<EventSystem>();
            if (es == null)
            {
                var go = new GameObject("EventSystem", typeof(EventSystem));
                Undo.RegisterCreatedObjectUndo(go, "Create EventSystem");
                es = go.GetComponent<EventSystem>();
            }
            var old = es.GetComponent<StandaloneInputModule>();
            if (old != null) Undo.DestroyObjectImmediate(old);
            if (es.GetComponent<InputSystemUIInputModule>() == null) Undo.AddComponent<InputSystemUIInputModule>(es.gameObject);
        }

        private static GameObject EnsurePlayer(GameObject prefab)
        {
            var existing = Object.FindFirstObjectByType<HomePlayerController>();
            if (existing != null) return existing.gameObject;

            var player = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            Undo.RegisterCreatedObjectUndo(player, "Place HomePlayer");

            // ドアの前 → 無ければ Scene ビューの中心に置く
            Vector3 pos = SceneView.lastActiveSceneView != null ? SceneView.lastActiveSceneView.pivot : Vector3.zero;
            var door = FindInScene(DoorObjectName);
            if (door != null)
            {
                var r = door.GetComponentInChildren<Renderer>();
                Vector3 center = r != null ? r.bounds.center : door.transform.position;
                Vector3 toCam = SceneView.lastActiveSceneView != null ? SceneView.lastActiveSceneView.camera.transform.position - center : Vector3.back;
                toCam.y = 0f;
                pos = center + (toCam.sqrMagnitude > 0.01f ? toCam.normalized : Vector3.back) * 3f;
            }
            player.transform.position = SnapToGround(pos);
            return player;
        }

        private static void EnsureCamera(GameObject player)
        {
            Camera cam = Camera.main;
            if (cam == null)
            {
                var camGo = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener)) { tag = "MainCamera" };
                Undo.RegisterCreatedObjectUndo(camGo, "Create Camera");
                cam = camGo.GetComponent<Camera>();
            }
            // Home は回らない見下ろしカメラ（Battle 用のカメラが付いていたら外す）
            var battleCam = cam.GetComponent<BattleCameraController>();
            if (battleCam != null) Undo.DestroyObjectImmediate(battleCam);

            var follow = cam.GetComponent<HomeCameraController>();
            bool isNew = follow == null;
            if (isNew) follow = Undo.AddComponent<HomeCameraController>(cam.gameObject);
            var so = new SerializedObject(follow);
            so.FindProperty("target").objectReferenceValue = player.transform;
            if (isNew)
            {
                // 画面の上方向 = プレイヤーからドアへの方向（ドアが奥に見える）
                var door = FindInScene(DoorObjectName);
                if (door != null)
                {
                    var r = door.GetComponentInChildren<Renderer>();
                    Vector3 to = (r != null ? r.bounds.center : door.transform.position) - player.transform.position;
                    to.y = 0f;
                    if (to.sqrMagnitude > 0.01f) so.FindProperty("yaw").floatValue = Mathf.Atan2(to.x, to.z) * Mathf.Rad2Deg;
                }
            }
            so.ApplyModifiedProperties();

            float pitch = so.FindProperty("pitch").floatValue;
            float yaw = so.FindProperty("yaw").floatValue;
            float dist = so.FindProperty("distance").floatValue;
            var rot = Quaternion.Euler(pitch, yaw, 0f);
            Undo.RecordObject(cam.transform, "Move Camera");
            cam.transform.SetPositionAndRotation(player.transform.position + Vector3.up - rot * Vector3.forward * dist, rot);
        }

        private static T EnsureInstance<T>(GameObject prefab) where T : Component
        {
            var existing = Object.FindFirstObjectByType<T>(FindObjectsInactive.Include);
            if (existing != null) return existing;
            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            Undo.RegisterCreatedObjectUndo(go, "Place " + prefab.name);
            return go.GetComponent<T>();
        }

        /// <summary>出撃画面の一覧で Boss Prefab が空なら、Prefabs/Boss/Boss_<ID>.prefab を入れる（報酬金の表示用）</summary>
        private static void FillQuestBossPrefabs(QuestMenu quest)
        {
            var so = new SerializedObject(quest);
            var quests = so.FindProperty("quests");
            for (int i = 0; i < quests.arraySize; i++)
            {
                var e = quests.GetArrayElementAtIndex(i);
                var prefabProp = e.FindPropertyRelative("bossPrefab");
                if (prefabProp.objectReferenceValue != null) continue;
                string path = "Assets/Game/Prefabs/Boss/Boss_" + e.FindPropertyRelative("id").stringValue + ".prefab";
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab != null) prefabProp.objectReferenceValue = prefab;
            }
            so.ApplyModifiedProperties();
        }

        /// <summary>出撃口を置き、入ったら出撃画面が開くようにする。新しく作ったときだけ返す（選択するため）</summary>
        private static GameObject SetupDepartureGate(GameObject player, QuestMenu quest)
        {
            var gate = Object.FindFirstObjectByType<DepartureGate>(FindObjectsInactive.Include);
            GameObject created = null;
            if (gate == null)
            {
                created = new GameObject("DepartureGate", typeof(DepartureGate));
                Undo.RegisterCreatedObjectUndo(created, "Create DepartureGate");
                // とりあえずプレイヤーの後ろに置く（柵の間へ動かしてもらう）
                Vector3 pos = SnapToGround(player.transform.position - player.transform.forward * 6f);
                created.transform.SetPositionAndRotation(pos, player.transform.rotation);
                gate = created.GetComponent<DepartureGate>();
            }

            var so = new SerializedObject(gate);
            so.FindProperty("onEnter.m_PersistentCalls.m_Calls").ClearArray();
            so.ApplyModifiedProperties();
            UnityEventTools.AddVoidPersistentListener(gate.OnEnter, quest.Open);
            EditorUtility.SetDirty(gate);
            return created;
        }

        private static void RemoveOldQuestVillager()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(QuestVillagerPrefabPath);
            if (prefab == null) return;
            foreach (var it in Object.FindObjectsByType<Interactable>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (PrefabUtility.GetCorrespondingObjectFromSource(it.gameObject) != prefab) continue;
                Debug.Log("[HomeSetup] 受付の村人を取り除きました（出撃は出撃口から）");
                Undo.DestroyObjectImmediate(it.gameObject);
            }
        }

        private static void SetupDoor(EquipmentMenu menu, Material highlight)
        {
            var door = FindInScene(DoorObjectName);
            if (door == null)
            {
                Debug.LogWarning($"[HomeSetup] シーンに {DoorObjectName} が見つかりません。調べたい物に Interactable を付け、On Interact に EquipmentMenu.Open を登録してください。");
                return;
            }

            // Static（バッチング）だとハイライト用にメッシュを複製できないので外す
            foreach (var t in door.GetComponentsInChildren<Transform>(true))
            {
                var flags = GameObjectUtility.GetStaticEditorFlags(t.gameObject);
                if ((flags & StaticEditorFlags.BatchingStatic) == 0) continue;
                Undo.RecordObject(t.gameObject, "Door Static");
                GameObjectUtility.SetStaticEditorFlags(t.gameObject, flags & ~StaticEditorFlags.BatchingStatic);
            }

            var it = door.GetComponent<Interactable>();
            if (it == null) it = Undo.AddComponent<Interactable>(door);
            ConfigureInteractable(it, "装備を整える", 2.5f, highlight, menu, nameof(HomeMenuBase.Open));
            Debug.Log($"[HomeSetup] {DoorObjectName} を調べると装備画面が開くようにしました。", door);
        }

        private static void SetupVillager(GameObject prefab, GameObject player, HomeMenuBase menu, Vector3 offset)
        {
            Interactable it = Object.FindObjectsByType<Interactable>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                .FirstOrDefault(x => PrefabUtility.GetCorrespondingObjectFromSource(x.gameObject) == prefab);
            GameObject villager;
            if (it == null)
            {
                villager = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
                Undo.RegisterCreatedObjectUndo(villager, "Place " + prefab.name);
                // プレイヤーの右前あたり、プレイヤーの方を向かせる
                Vector3 pos = SnapToGround(player.transform.position + player.transform.TransformDirection(offset));
                Vector3 look = player.transform.position - pos; look.y = 0f;
                villager.transform.SetPositionAndRotation(pos, look.sqrMagnitude > 0.01f ? Quaternion.LookRotation(look) : Quaternion.identity);
                it = villager.GetComponent<Interactable>();
            }
            villager = it.gameObject;

            // 話しかけたらメニューを開く（シーン上のメニューと結ぶ）
            var so = new SerializedObject(it);
            var calls = so.FindProperty("onInteract.m_PersistentCalls.m_Calls");
            calls.ClearArray();
            so.ApplyModifiedPropertiesWithoutUndo();
            UnityEventTools.AddVoidPersistentListener(it.OnInteract, menu.Open);
            EditorUtility.SetDirty(it);
            PrefabUtility.RecordPrefabInstancePropertyModifications(it);
        }

        private static void ConfigureInteractable(Interactable it, string prompt, float radius, Material highlight,
                                                  HomeMenuBase menu, string method)
        {
            var so = new SerializedObject(it);
            so.FindProperty("prompt").stringValue = prompt;
            so.FindProperty("interactRadius").floatValue = radius;
            so.FindProperty("highlightMaterial").objectReferenceValue = highlight;
            so.FindProperty("onInteract.m_PersistentCalls.m_Calls").ClearArray();
            so.ApplyModifiedProperties();

            if (menu != null) UnityEventTools.AddVoidPersistentListener(it.OnInteract, menu.Open);
            EditorUtility.SetDirty(it);
            if (PrefabUtility.IsPartOfPrefabInstance(it)) PrefabUtility.RecordPrefabInstancePropertyModifications(it);
        }

        private static GameObject FindInScene(string name)
        {
            var scene = EditorSceneManager.GetActiveScene();
            foreach (var root in scene.GetRootGameObjects())
            {
                var t = root.GetComponentsInChildren<Transform>(true).FirstOrDefault(x => x.name == name);
                if (t != null) return t.gameObject;
            }
            return null;
        }

        private static Vector3 SnapToGround(Vector3 pos)
        {
            // 上から下へレイを飛ばして地面に置く（Terrain・建物の床どちらでも）
            if (Physics.Raycast(pos + Vector3.up * 50f, Vector3.down, out var hit, 200f, ~0, QueryTriggerInteraction.Ignore))
                pos.y = hit.point.y;
            else if (Terrain.activeTerrain != null)
                pos.y = Terrain.activeTerrain.SampleHeight(pos) + Terrain.activeTerrain.transform.position.y;
            return pos;
        }

        // ================================================================
        //  ハイライト用マテリアル
        // ================================================================

        private static Material EnsureHighlightMaterial()
        {
            var mat = AssetDatabase.LoadAssetAtPath<Material>(HighlightMaterialPath);
            if (mat != null) return mat;
            var shader = AssetDatabase.LoadAssetAtPath<Shader>(ShaderPath);
            if (shader == null) throw new FileNotFoundException(ShaderPath);
            PlayerSetupCommon.EnsureFolder(Path.GetDirectoryName(HighlightMaterialPath).Replace('\\', '/'));
            mat = new Material(shader);
            AssetDatabase.CreateAsset(mat, HighlightMaterialPath);
            AssetDatabase.SaveAssets();
            return mat;
        }

        // ================================================================
        //  Player / 村人
        // ================================================================

        private static GameObject BuildPlayerPrefab()
        {
            var ctrl = BuildLocomotionController(PlayerControllerPath, "Male/Idles/HumanM@Idle01.fbx");
            var modelSrc = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerModelPath);
            if (modelSrc == null) throw new FileNotFoundException(PlayerModelPath);

            var root = new GameObject("HomePlayer") { tag = "Player" };
            var cc = root.AddComponent<CharacterController>();
            cc.center = new Vector3(0f, 0.9f, 0f);
            cc.height = 1.8f;
            cc.radius = 0.3f;
            cc.stepOffset = 0.3f;
            cc.skinWidth = 0.03f;
            root.AddComponent<PlayerInputReader>();
            root.AddComponent<HomePlayerController>();

            var model = (GameObject)PrefabUtility.InstantiatePrefab(modelSrc, root.transform);
            model.name = "Model";
            SetupAnimator(model, ctrl);

            return SavePrefab(root, PlayerPrefabPath);
        }

        private static GameObject BuildShopVillagerPrefab(Material highlight) =>
            BuildVillagerPrefab(highlight, VillagerModelPath, VillagerControllerPath, "Female/Idles/HumanF@Idle01.fbx", "話しかける", VillagerPrefabPath);

        private static GameObject BuildVillagerPrefab(Material highlight, string modelPath, string controllerPath,
                                                      string idleClip, string prompt, string savePath)
        {
            var ctrl = BuildIdleController(controllerPath, idleClip);
            var modelSrc = AssetDatabase.LoadAssetAtPath<GameObject>(modelPath);
            if (modelSrc == null) throw new FileNotFoundException(modelPath);

            var root = new GameObject(Path.GetFileNameWithoutExtension(savePath));
            var col = root.AddComponent<CapsuleCollider>(); // 通り抜けないように
            col.center = new Vector3(0f, 0.9f, 0f);
            col.height = 1.8f;
            col.radius = 0.35f;

            var model = (GameObject)PrefabUtility.InstantiatePrefab(modelSrc, root.transform);
            model.name = "Model";
            SetupAnimator(model, ctrl);

            var it = root.AddComponent<Interactable>();
            var so = new SerializedObject(it);
            so.FindProperty("prompt").stringValue = prompt;
            so.FindProperty("interactRadius").floatValue = 2.5f;
            so.FindProperty("highlightMaterial").objectReferenceValue = highlight;
            so.ApplyModifiedPropertiesWithoutUndo();

            return SavePrefab(root, savePath);
        }

        private static void SetupAnimator(GameObject model, AnimatorController ctrl)
        {
            // 起動時にも Controller をセットする（モデル側の設定が外れても確実に動くように）
            var root = model.transform.parent.gameObject;
            var setup = root.AddComponent<CharacterAnimatorSetup>();
            var so = new SerializedObject(setup);
            so.FindProperty("controller").objectReferenceValue = ctrl;
            so.FindProperty("randomStartTime").boolValue = root.GetComponent<HomePlayerController>() == null;
            so.ApplyModifiedPropertiesWithoutUndo();

            model.transform.localPosition = Vector3.zero;
            model.transform.localRotation = Quaternion.identity;
            var animator = model.GetComponentInChildren<Animator>();
            if (animator == null) animator = model.AddComponent<Animator>();
            animator.runtimeAnimatorController = ctrl;
            animator.applyRootMotion = false;
        }

        /// <summary>待機 / 歩き / 走り / ダッシュ（Speed：0 / 0.4 / 1 / 1.6）だけの Animator Controller</summary>
        private static AnimatorController BuildLocomotionController(string path, string idleClip)
        {
            PlayerSetupCommon.EnsureFolder(HomeAnimDir);
            // 既にあれば使い回す（作り直すと参照が切れて T ポーズになることがあるため）
            var existing = AssetDatabase.LoadAssetAtPath<AnimatorController>(path);
            if (existing != null) return existing;
            var ctrl = AnimatorController.CreateAnimatorControllerAtPath(path);
            ctrl.AddParameter("Speed", AnimatorControllerParameterType.Float);

            var state = ctrl.CreateBlendTreeInController("Locomotion", out var tree, 0);
            tree.blendType = BlendTreeType.Simple1D;
            tree.blendParameter = "Speed";
            tree.useAutomaticThresholds = false;
            var run = PlayerSetupCommon.LoadKevinClip("Male/Movement/Run/HumanM@Run01_Forward.fbx");
            tree.AddChild(PlayerSetupCommon.LoadKevinClip(idleClip), 0f);
            tree.AddChild(PlayerSetupCommon.LoadKevinClip("Male/Movement/Walk/HumanM@Walk01_Forward.fbx"), 0.4f);
            tree.AddChild(run, 1f);
            tree.AddChild(run, PlayerBattleController.SprintAnimBlendValue);
            var children = tree.children;
            children[children.Length - 1].timeScale = 1.5f;
            tree.children = children;
            ctrl.layers[0].stateMachine.defaultState = state;
            AssetDatabase.SaveAssets();
            return ctrl;
        }

        private static AnimatorController BuildIdleController(string path, string idleClip)
        {
            PlayerSetupCommon.EnsureFolder(HomeAnimDir);
            // 既にあれば使い回す（作り直すと参照が切れて T ポーズになることがあるため）
            var existing = AssetDatabase.LoadAssetAtPath<AnimatorController>(path);
            if (existing != null) return existing;
            var ctrl = AnimatorController.CreateAnimatorControllerAtPath(path);
            var state = ctrl.layers[0].stateMachine.AddState("Idle");
            state.motion = PlayerSetupCommon.LoadKevinClip(idleClip);
            ctrl.layers[0].stateMachine.defaultState = state;
            AssetDatabase.SaveAssets();
            return ctrl;
        }

        private static GameObject SavePrefab(GameObject root, string path)
        {
            try
            {
                PlayerSetupCommon.EnsureFolder(Path.GetDirectoryName(path).Replace('\\', '/'));
                return PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally
            {
                Object.DestroyImmediate(root);
                AssetDatabase.SaveAssets();
                Debug.Log($"[HomeSetup] 作成完了: {path}");
            }
        }

        // ================================================================
        //  UI
        // ================================================================

        private static readonly Color WindowColor = new Color(0.07f, 0.09f, 0.13f, 0.96f);
        private static readonly Color DimColor = new Color(0f, 0f, 0f, 0.55f);
        private static readonly Color HeaderColor = new Color(1f, 0.82f, 0.35f);
        private static readonly Color PanelColor = new Color(0f, 0f, 0f, 0.35f);

        private static Font DefaultFont => Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        private static Sprite DefaultSprite => AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");

        private static GameObject CreateCanvas(string name, int sortingOrder)
        {
            var root = new GameObject(name, typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            root.layer = LayerMask.NameToLayer("UI");
            var canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = sortingOrder;
            var scaler = root.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            return root;
        }

        private static GameObject BuildHUDPrefab()
        {
            var root = CreateCanvas("HomeHUD", 0);
            var hud = root.AddComponent<HomeHUD>();

            // 右上：所持金
            var moneyBack = Img("Money", root.transform, new Vector2(1f, 1f), new Vector2(-30f, -30f), new Vector2(300f, 60f), PanelColor);
            Txt("Label", moneyBack.transform, new Vector2(0f, 0.5f), new Vector2(20f, 0f), new Vector2(120f, 50f), 22, "所持金", TextAnchor.MiddleLeft);
            var money = Txt("Value", moneyBack.transform, new Vector2(1f, 0.5f), new Vector2(-20f, 0f), new Vector2(200f, 50f), 30, "300 G", TextAnchor.MiddleRight);
            money.fontStyle = FontStyle.Bold;

            // 下：□ 調べる
            var prompt = Img("Prompt", root.transform, new Vector2(0.5f, 0f), new Vector2(0f, 140f), new Vector2(520f, 64f), PanelColor);
            var promptText = Txt("Text", prompt.transform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(500f, 60f), 30, "□  調べる", TextAnchor.MiddleCenter);

            var so = new SerializedObject(hud);
            so.FindProperty("moneyText").objectReferenceValue = money;
            so.FindProperty("promptRoot").objectReferenceValue = prompt.gameObject;
            so.FindProperty("promptText").objectReferenceValue = promptText;
            so.ApplyModifiedPropertiesWithoutUndo();

            return SavePrefab(root, HUDPrefabPath);
        }

        /// <summary>ショップ / 装備画面の Prefab（見た目は同じで、付けるスクリプトだけ違う）</summary>
        private static GameObject BuildMenuPrefab<T>(string name, string title, bool withSlotText) where T : HomeMenuBase
        {
            var root = CreateCanvas(name, 20);
            var menu = root.AddComponent<T>();

            // 画面全体を暗くする背景（＝ panel。これの表示 / 非表示で開け閉めする）
            var panel = Img("Panel", root.transform, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero, DimColor);
            Stretch(panel.rectTransform);
            panel.raycastTarget = true; // 後ろをクリックできないように

            var window = Img("Window", panel.transform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1200f, 780f), WindowColor);
            var titleText = Txt("Title", window.transform, new Vector2(0.5f, 1f), new Vector2(0f, -50f), new Vector2(800f, 70f), 44, title, TextAnchor.MiddleCenter);
            titleText.fontStyle = FontStyle.Bold;
            var money = Txt("Money", window.transform, new Vector2(1f, 1f), new Vector2(-100f, -50f), new Vector2(360f, 50f), 28, "所持金  300 G", TextAnchor.MiddleRight);
            Text slot = withSlotText
                ? Txt("Slots", window.transform, new Vector2(0f, 1f), new Vector2(40f, -50f), new Vector2(360f, 50f), 26, "スキル枠  0 / 2", TextAnchor.MiddleLeft)
                : null;

            // 閉じるボタン（右上の ×）
            var close = Img("CloseButton", window.transform, new Vector2(1f, 1f), new Vector2(-20f, -20f), new Vector2(56f, 56f), new Color(0.6f, 0.2f, 0.2f, 1f));
            close.raycastTarget = true;
            var closeButton = close.gameObject.AddComponent<Button>();
            closeButton.navigation = new Navigation { mode = Navigation.Mode.None }; // ゲームパッドでは〇で閉じる
            Txt("X", close.transform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(56f, 56f), 34, "×", TextAnchor.MiddleCenter);

            // 左：一覧
            var list = new GameObject("List", typeof(RectTransform), typeof(VerticalLayoutGroup)).GetComponent<RectTransform>();
            list.gameObject.layer = LayerMask.NameToLayer("UI");
            Place(list, window.transform, new Vector2(0f, 1f), new Vector2(40f, -110f), new Vector2(700f, 560f));
            var layout = list.GetComponent<VerticalLayoutGroup>();
            layout.spacing = 6f;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;

            var header = Txt("HeaderTemplate", list, new Vector2(0f, 1f), Vector2.zero, new Vector2(700f, 44f), 26, "見出し", TextAnchor.LowerLeft);
            header.color = HeaderColor;
            header.fontStyle = FontStyle.Bold;
            header.gameObject.AddComponent<LayoutElement>().preferredHeight = 44f;

            var row = BuildRowTemplate(list);

            // 右：説明
            var descBack = Img("Description", window.transform, new Vector2(1f, 1f), new Vector2(-40f, -110f), new Vector2(380f, 560f), PanelColor);
            var desc = Txt("Text", descBack.transform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(340f, 520f), 24, "説明", TextAnchor.UpperLeft);
            desc.horizontalOverflow = HorizontalWrapMode.Wrap;

            // 下：メッセージと操作説明
            var message = Txt("Message", window.transform, new Vector2(0.5f, 0f), new Vector2(0f, 80f), new Vector2(1100f, 44f), 28, "", TextAnchor.MiddleCenter);
            message.color = HeaderColor;
            Txt("Hint", window.transform, new Vector2(0.5f, 0f), new Vector2(0f, 30f), new Vector2(1100f, 40f), 22,
                "✕ / Enter / クリック：決定　　〇 / Esc：閉じる", TextAnchor.MiddleCenter).color = new Color(0.8f, 0.8f, 0.8f);

            var so = new SerializedObject(menu);
            so.FindProperty("panel").objectReferenceValue = panel.gameObject;
            so.FindProperty("listRoot").objectReferenceValue = list;
            so.FindProperty("rowTemplate").objectReferenceValue = row;
            so.FindProperty("headerTemplate").objectReferenceValue = header;
            so.FindProperty("moneyText").objectReferenceValue = money;
            so.FindProperty("descriptionText").objectReferenceValue = desc;
            so.FindProperty("messageText").objectReferenceValue = message;
            so.FindProperty("closeButton").objectReferenceValue = closeButton;
            if (slot != null) so.FindProperty("slotText").objectReferenceValue = slot;
            so.ApplyModifiedPropertiesWithoutUndo();

            header.gameObject.SetActive(false);
            row.gameObject.SetActive(false);
            panel.gameObject.SetActive(false);

            string path = typeof(T) == typeof(ShopMenu) ? ShopPrefabPath
                        : typeof(T) == typeof(QuestMenu) ? QuestPrefabPath
                        : EquipmentPrefabPath;
            return SavePrefab(root, path);
        }

        private static MenuRow BuildRowTemplate(Transform parent)
        {
            var bg = Img("RowTemplate", parent, new Vector2(0f, 1f), Vector2.zero, new Vector2(700f, 58f), Color.white);
            bg.raycastTarget = true;
            bg.gameObject.AddComponent<LayoutElement>().preferredHeight = 58f;

            var button = bg.gameObject.AddComponent<Button>();
            button.targetGraphic = bg;
            var colors = button.colors;
            colors.normalColor = new Color(0.16f, 0.19f, 0.26f, 1f);
            colors.highlightedColor = new Color(0.28f, 0.32f, 0.45f, 1f);
            colors.selectedColor = new Color(0.62f, 0.46f, 0.12f, 1f); // ゲームパッドで選んでいる行
            colors.pressedColor = new Color(0.85f, 0.65f, 0.2f, 1f);
            colors.fadeDuration = 0.05f;
            button.colors = colors;
            button.navigation = new Navigation { mode = Navigation.Mode.Vertical };

            var name = Txt("Name", bg.transform, new Vector2(0f, 0.5f), new Vector2(20f, 0f), new Vector2(260f, 50f), 28, "名前", TextAnchor.MiddleLeft);
            var info = Txt("Info", bg.transform, new Vector2(0f, 0.5f), new Vector2(280f, 0f), new Vector2(260f, 50f), 22, "★☆☆", TextAnchor.MiddleLeft);
            var right = Txt("Right", bg.transform, new Vector2(1f, 0.5f), new Vector2(-20f, 0f), new Vector2(160f, 50f), 26, "100 G", TextAnchor.MiddleRight);

            var row = bg.gameObject.AddComponent<MenuRow>();
            var so = new SerializedObject(row);
            so.FindProperty("nameText").objectReferenceValue = name;
            so.FindProperty("infoText").objectReferenceValue = info;
            so.FindProperty("rightText").objectReferenceValue = right;
            so.ApplyModifiedPropertiesWithoutUndo();
            return row;
        }

        // ---- 小さな部品 ----

        /// <summary>anchor の位置を基準に置く（pivot も anchor と同じにする）</summary>
        private static void Place(RectTransform rt, Transform parent, Vector2 anchor, Vector2 pos, Vector2 size)
        {
            rt.SetParent(parent, false);
            rt.anchorMin = rt.anchorMax = rt.pivot = anchor;
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
        }

        private static void Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
        }

        private static Image Img(string name, Transform parent, Vector2 anchor, Vector2 pos, Vector2 size, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.layer = LayerMask.NameToLayer("UI");
            Place((RectTransform)go.transform, parent, anchor, pos, size);
            var img = go.GetComponent<Image>();
            img.sprite = DefaultSprite;
            img.type = Image.Type.Sliced;
            img.color = color;
            img.raycastTarget = false;
            return img;
        }

        private static Text Txt(string name, Transform parent, Vector2 anchor, Vector2 pos, Vector2 size, int fontSize,
                                string text, TextAnchor alignment)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Text));
            go.layer = LayerMask.NameToLayer("UI");
            Place((RectTransform)go.transform, parent, anchor, pos, size);
            var t = go.GetComponent<Text>();
            t.font = DefaultFont;
            t.fontSize = fontSize;
            t.alignment = alignment;
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            t.color = Color.white;
            t.text = text;
            t.raycastTarget = false;
            return t;
        }
    }
}
