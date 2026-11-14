using System;
using System.Linq;
using AlchemyTower;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace AlchemyTower.Editor
{
    internal static class M2TowerSceneBuilder
    {
        private const string RootFolder = "Assets/_AlchemyTower";
        private const string FloorPrefabPath = RootFolder + "/Prefabs/UI/FloorView.prefab";
        private static readonly string[] FloorPrefabIds = { "first_floor", "second_floor", "third_floor" };
        private static readonly string[] FloorVariantPaths =
        {
            RootFolder + "/Prefabs/UI/Floor01.prefab",
            RootFolder + "/Prefabs/UI/Floor02.prefab",
            RootFolder + "/Prefabs/UI/Floor03.prefab"
        };
        private const string LockedPrefabPath = RootFolder + "/Prefabs/UI/LockedFloorView.prefab";
        private const string PrefabBackupsFolder = RootFolder + "/Prefabs/UI/Backups";
        private const string TowerDefinitionPath = RootFolder + "/Data/TowerDefinition.asset";
        private const string ScenePath = RootFolder + "/Scenes/M1FirstFloor.unity";
        private const string SceneBackupsFolder = RootFolder + "/Scenes/Backups";
        private const string InputActionsPath = "Assets/Settings/InputSystem_Actions.inputactions";
        private const float FloorHeight = 820f;

        [MenuItem("Tools/Alchemy Tower/Build M2 Tower Scene")]
        private static void BuildM2TowerScene()
        {
            BuildScene(rebuildExisting: false);
        }

        [MenuItem("Tools/Alchemy Tower/Rebuild M2 Tower Scene")]
        private static void RebuildM2TowerScene()
        {
            BuildScene(rebuildExisting: true);
        }

        [MenuItem("Tools/Alchemy Tower/Upgrade FloorView Prefab (Tower Layout)")]
        public static void UpgradeFloorPrefabLayout()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(FloorPrefabPath);
            if (prefab == null || prefab.GetComponent<FloorView>() == null)
            {
                EditorUtility.DisplayDialog("FloorView Prefab missing", $"A valid Prefab is required at {FloorPrefabPath}.", "OK");
                return;
            }

            if (IsTowerLayout(prefab))
            {
                EditorUtility.DisplayDialog("FloorView Prefab", "Tower layout is already applied. Nothing was modified.", "OK");
                return;
            }

            if (!EditorUtility.DisplayDialog(
                    "Upgrade FloorView Prefab",
                    "A timestamped backup will be created, then the room and existing controls will be rearranged for the vertical tower. Existing child artwork objects are preserved.",
                    "Back up and upgrade",
                    "Cancel"))
                return;

            EnsureFolder(RootFolder + "/Prefabs/UI", "Backups");
            string backupPath = AssetDatabase.GenerateUniqueAssetPath(
                $"{PrefabBackupsFolder}/FloorView_Tower_{DateTime.Now:yyyyMMdd_HHmmss}.prefab");
            if (!AssetDatabase.CopyAsset(FloorPrefabPath, backupPath))
            {
                EditorUtility.DisplayDialog("Backup failed", "The Prefab was not modified.", "OK");
                return;
            }

            GameObject contents = PrefabUtility.LoadPrefabContents(FloorPrefabPath);
            bool saved = false;
            try
            {
                ApplyTowerLayout(contents);
                GameObject result = PrefabUtility.SaveAsPrefabAsset(contents, FloorPrefabPath, out saved);
                saved = saved && result != null;
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(contents);
            }

            AssetDatabase.SaveAssets();
            EditorUtility.DisplayDialog(
                saved ? "Tower layout applied" : "Prefab update failed",
                saved ? $"Backup: {backupPath}" : $"Unity could not save the Prefab. Backup: {backupPath}",
                "OK");
        }

        public static void BuildScene(bool rebuildExisting)
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return;

            SceneAsset existingScene = AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath);
            if (existingScene != null)
            {
                bool open = EditorUtility.DisplayDialog(
                    rebuildExisting ? "Tower scene preserved" : "Tower scene already exists",
                    $"{ScenePath} contains editable scene artwork and was not overwritten. "
                    + "The command can only create a missing scene. Open the current scene to validate or edit it.",
                    "Open current scene",
                    "Cancel");
                if (open)
                    EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
                return;
            }

            TowerDefinitionSO towerDefinition = AssetDatabase.LoadAssetAtPath<TowerDefinitionSO>(TowerDefinitionPath);
            GameObject floorPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(FloorPrefabPath);
            if (towerDefinition == null || floorPrefab == null || floorPrefab.GetComponent<FloorView>() == null)
            {
                EditorUtility.DisplayDialog("Missing tower assets", "TowerDefinition and a valid FloorView Prefab are required.", "OK");
                return;
            }

            if (!IsTowerLayout(floorPrefab))
            {
                EditorUtility.DisplayDialog(
                    "FloorView needs tower layout",
                    "Run Tools/Alchemy Tower/Upgrade FloorView Prefab (Tower Layout) first. The Prefab was not modified automatically.",
                    "OK");
                return;
            }

            GameObject lockedPrefab = GetOrCreateLockedFloorPrefab();
            if (lockedPrefab == null || !ValidateDependencies())
                return;

            ConfigurePortraitOrientation();
            CreateTowerScene(towerDefinition, floorPrefab.GetComponent<FloorView>(), lockedPrefab.GetComponent<LockedFloorView>());
        }

        private static void ApplyTowerLayout(GameObject root)
        {
            VerticalLayoutGroup legacyLayout = root.GetComponent<VerticalLayoutGroup>();
            if (legacyLayout != null)
                UnityEngine.Object.DestroyImmediate(legacyLayout);

            LayoutElement rootSize = root.GetComponent<LayoutElement>();
            rootSize.minHeight = FloorHeight;
            rootSize.preferredHeight = FloorHeight;
            rootSize.flexibleHeight = 0f;

            RectTransform room = RequireRect(root.transform, "Room Panel");
            SetStretch(room, 0f, 0f, 0f, 0f);
            LayoutElement roomSize = room.GetComponent<LayoutElement>();
            if (roomSize != null)
                UnityEngine.Object.DestroyImmediate(roomSize);

            SetStretch(RequireRect(room, "RoomArtwork"), 12f, 12f, 12f, 12f);
            SetAnchors(RequireRect(room, "Floor Name"), 0.04f, 0.80f, 0.67f, 0.96f);
            SetAnchors(RequireRect(room, "Brewing Status"), 0.04f, 0.63f, 0.67f, 0.80f);
            SetAnchors(RequireRect(room, "Floor Level"), 0.06f, 0.47f, 0.34f, 0.61f);
            SetAnchors(RequireRect(room, "Potion Reward"), 0.35f, 0.47f, 0.67f, 0.61f);
            SetAnchors(RequireRect(room, "Progress Track"), 0.06f, 0.26f, 0.67f, 0.44f);
            SetAnchors(RequireRect(room, "Interaction Hint"), 0.06f, 0.04f, 0.67f, 0.23f);

            RectTransform upgrade = RequireRect(root.transform, "Upgrade Control");
            RectTransform automation = RequireRect(root.transform, "Automation Control");
            SetAnchors(upgrade, 0.70f, 0.54f, 0.96f, 0.82f);
            SetAnchors(automation, 0.70f, 0.20f, 0.96f, 0.48f);
            ResetControlInsets(upgrade);
            ResetControlInsets(automation);
            upgrade.SetAsLastSibling();
            automation.SetAsLastSibling();
        }

        private static void ResetControlInsets(RectTransform control)
        {
            for (int i = 0; i < control.childCount; i++)
            {
                RectTransform child = control.GetChild(i) as RectTransform;
                if (child != null)
                    SetStretch(child, 8f, 8f, 8f, 8f);
            }
        }

        private static bool IsTowerLayout(GameObject prefab)
        {
            LayoutElement size = prefab.GetComponent<LayoutElement>();
            return prefab.GetComponent<VerticalLayoutGroup>() == null
                && size != null
                && Mathf.Abs(size.preferredHeight - FloorHeight) < 1f;
        }

        private static GameObject GetOrCreateLockedFloorPrefab()
        {
            UnityEngine.Object existing = AssetDatabase.LoadMainAssetAtPath(LockedPrefabPath);
            if (existing != null)
            {
                if (existing is GameObject prefab && prefab.GetComponent<LockedFloorView>() != null)
                    return prefab;

                EditorUtility.DisplayDialog("Invalid locked-floor Prefab", $"{LockedPrefabPath} was not modified.", "OK");
                return null;
            }

            Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            GameObject root = CreateUiObject("LockedFloorView", null, typeof(CanvasRenderer), typeof(Image), typeof(LayoutElement), typeof(LockedFloorView));
            Image room = root.GetComponent<Image>();
            room.color = new Color(0.24f, 0.24f, 0.28f, 1f);
            LayoutElement size = root.GetComponent<LayoutElement>();
            size.minHeight = FloorHeight;
            size.preferredHeight = FloorHeight;
            size.flexibleHeight = 0f;

            Text plus = CreateText("Unlock Plus", root.transform, font, 150, TextAnchor.MiddleCenter, new Color(0.82f, 0.82f, 0.86f));
            plus.text = "+";
            SetAnchors(plus.rectTransform, 0.20f, 0.42f, 0.80f, 0.76f);
            Text price = CreateText("Unlock Price", root.transform, font, 46, TextAnchor.MiddleCenter, Color.white);
            SetAnchors(price.rectTransform, 0.15f, 0.27f, 0.85f, 0.44f);

            Image hitArea = CreateImage("Unlock Hit Area", root.transform, Color.clear, true);
            SetStretch(hitArea.rectTransform, 0f, 0f, 0f, 0f);
            Button button = hitArea.gameObject.AddComponent<Button>();
            button.targetGraphic = hitArea;
            button.transition = Selectable.Transition.ColorTint;

            SerializedObject serialized = new SerializedObject(root.GetComponent<LockedFloorView>());
            SetReference(serialized, "unlockButton", button);
            SetReference(serialized, "unlockPriceText", price);
            serialized.ApplyModifiedPropertiesWithoutUndo();

            GameObject prefabResult = PrefabUtility.SaveAsPrefabAsset(root, LockedPrefabPath, out bool success);
            UnityEngine.Object.DestroyImmediate(root);
            AssetDatabase.SaveAssets();
            return success ? prefabResult : null;
        }

        private static void CreateTowerScene(
            TowerDefinitionSO towerDefinition,
            FloorView floorPrefab,
            LockedFloorView lockedPrefab)
        {
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            CreateMainCamera();

            GameObject canvasObject = new GameObject("Portrait UI Canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasObject.layer = UiLayer;
            Canvas canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080f, 1920f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0f;

            Image background = CreateImage("Background Artwork", canvasObject.transform, new Color(0.08f, 0.07f, 0.13f), false);
            SetStretch(background.rectTransform, 0f, 0f, 0f, 0f);
            GameObject safeArea = CreateUiObject("Mobile Safe Area", canvasObject.transform, typeof(SafeAreaFitter));
            SetStretch(safeArea.GetComponent<RectTransform>(), 0f, 0f, 0f, 0f);

            GameObject header = CreateUiObject("Fixed Coin HUD", safeArea.transform);
            SetAnchors(header.GetComponent<RectTransform>(), 0.04f, 0.87f, 0.96f, 0.99f);
            Text title = CreateText("Game Title", header.transform, font, 58, TextAnchor.MiddleCenter, Color.white);
            title.text = "ALCHEMY TOWER";
            SetAnchors(title.rectTransform, 0f, 0.45f, 1f, 1f);
            Text coins = CreateText("Coin Counter", header.transform, font, 44, TextAnchor.MiddleCenter, new Color(1f, 0.84f, 0.25f));
            SetAnchors(coins.rectTransform, 0f, 0f, 1f, 0.48f);

            // Mask needs opaque geometry to write its stencil. showMaskGraphic=false keeps
            // the viewport itself invisible without culling the mask mesh.
            Image viewportImage = CreateImage("Tower Viewport", safeArea.transform, Color.white, true);
            SetAnchors(viewportImage.rectTransform, 0.08f, 0.02f, 0.92f, 0.86f);
            Mask mask = viewportImage.gameObject.AddComponent<Mask>();
            mask.showMaskGraphic = false;
            ScrollRect scrollRect = viewportImage.gameObject.AddComponent<ScrollRect>();
            TowerScrollNavigator scrollNavigator = viewportImage.gameObject.AddComponent<TowerScrollNavigator>();

            GameObject content = CreateUiObject("Continuous Tower", viewportImage.transform, typeof(CanvasRenderer), typeof(Image), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
            RectTransform contentRect = content.GetComponent<RectTransform>();
            contentRect.anchorMin = new Vector2(0f, 0f);
            contentRect.anchorMax = new Vector2(1f, 0f);
            contentRect.pivot = new Vector2(0.5f, 0f);
            contentRect.anchoredPosition = Vector2.zero;
            contentRect.sizeDelta = Vector2.zero;
            content.GetComponent<Image>().color = new Color(0.13f, 0.10f, 0.20f, 1f);
            VerticalLayoutGroup layout = content.GetComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(0, 0, 0, 0);
            layout.spacing = 0f;
            layout.childAlignment = TextAnchor.LowerCenter;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            ContentSizeFitter fitter = content.GetComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            scrollRect.viewport = viewportImage.rectTransform;
            scrollRect.content = contentRect;
            scrollRect.horizontal = false;
            scrollRect.vertical = true;
            scrollRect.movementType = ScrollRect.MovementType.Clamped;
            scrollRect.inertia = true;
            scrollRect.decelerationRate = 0.135f;
            scrollRect.scrollSensitivity = 45f;
            scrollRect.verticalNormalizedPosition = 0f;

            TowerController controller = safeArea.AddComponent<TowerController>();
            SerializedObject controllerSerialized = new SerializedObject(controller);
            SetReference(controllerSerialized, "towerDefinition", towerDefinition);
            SetReference(controllerSerialized, "floorViewPrefab", floorPrefab);
            SetReference(controllerSerialized, "lockedFloorViewPrefab", lockedPrefab);
            SetFloorPrefabCatalog(controllerSerialized);
            SetReference(controllerSerialized, "floorContainer", contentRect);
            SetReference(controllerSerialized, "coinsText", coins);
            SetReference(controllerSerialized, "scrollNavigator", scrollNavigator);
            controllerSerialized.ApplyModifiedPropertiesWithoutUndo();

            CreateEventSystem();
            if (!EditorSceneManager.SaveScene(scene, ScenePath))
                throw new InvalidOperationException($"Unity could not save {ScenePath}.");

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Selection.activeObject = AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath);
            EditorUtility.DisplayDialog("Vertical tower scene created", $"Created {ScenePath} with a fixed HUD and clamped vertical ScrollRect.", "OK");
        }

        private static void SetFloorPrefabCatalog(SerializedObject controllerSerialized)
        {
            SerializedProperty catalog = controllerSerialized.FindProperty("floorPrefabsById");
            catalog.arraySize = FloorVariantPaths.Length;
            for (int i = 0; i < FloorVariantPaths.Length; i++)
            {
                SerializedProperty entry = catalog.GetArrayElementAtIndex(i);
                entry.FindPropertyRelative("floorId").stringValue = FloorPrefabIds[i];
                GameObject variant = AssetDatabase.LoadAssetAtPath<GameObject>(FloorVariantPaths[i]);
                entry.FindPropertyRelative("prefab").objectReferenceValue = variant != null
                    ? variant.GetComponent<FloorView>()
                    : null;
            }
        }

        private static bool BackUpScene()
        {
            EnsureFolder(RootFolder + "/Scenes", "Backups");
            string backupPath = AssetDatabase.GenerateUniqueAssetPath(
                $"{SceneBackupsFolder}/M1FirstFloor_{DateTime.Now:yyyyMMdd_HHmmss}.unity");
            if (AssetDatabase.CopyAsset(ScenePath, backupPath))
                return true;

            EditorUtility.DisplayDialog("Backup failed", "The scene was not rebuilt.", "OK");
            return false;
        }

        private static bool ValidateDependencies()
        {
            try
            {
                if (Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf") == null)
                    throw new InvalidOperationException("LegacyRuntime.ttf was not found.");
                if (AssetDatabase.LoadAssetAtPath<InputActionAsset>(InputActionsPath) == null)
                    throw new InvalidOperationException($"Input Actions not found at {InputActionsPath}.");
                FindActionReference("UI/Point");
                FindActionReference("UI/Click");
                FindActionReference("UI/ScrollWheel");
                return true;
            }
            catch (Exception exception)
            {
                EditorUtility.DisplayDialog("Cannot build tower UI", exception.Message, "OK");
                return false;
            }
        }

        private static void ConfigurePortraitOrientation()
        {
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.Portrait;
            PlayerSettings.allowedAutorotateToPortrait = true;
            PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
            PlayerSettings.allowedAutorotateToLandscapeLeft = false;
            PlayerSettings.allowedAutorotateToLandscapeRight = false;
        }

        private static void CreateMainCamera()
        {
            GameObject cameraObject = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener));
            cameraObject.tag = "MainCamera";
            cameraObject.transform.position = new Vector3(0f, 0f, -10f);
            Camera camera = cameraObject.GetComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.08f, 0.07f, 0.13f, 1f);
            camera.orthographic = true;
        }

        private static void CreateEventSystem()
        {
            GameObject eventSystem = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
            InputSystemUIInputModule module = eventSystem.GetComponent<InputSystemUIInputModule>();
            module.actionsAsset = AssetDatabase.LoadAssetAtPath<InputActionAsset>(InputActionsPath);
            module.point = FindActionReference("UI/Point");
            module.leftClick = FindActionReference("UI/Click");
            module.rightClick = FindActionReference("UI/RightClick");
            module.middleClick = FindActionReference("UI/MiddleClick");
            module.scrollWheel = FindActionReference("UI/ScrollWheel");
            module.move = FindActionReference("UI/Navigate");
            module.submit = FindActionReference("UI/Submit");
            module.cancel = FindActionReference("UI/Cancel");
            module.trackedDevicePosition = FindActionReference("UI/TrackedDevicePosition");
            module.trackedDeviceOrientation = FindActionReference("UI/TrackedDeviceOrientation");
        }

        private static InputActionReference FindActionReference(string actionPath)
        {
            InputActionReference reference = AssetDatabase.LoadAllAssetsAtPath(InputActionsPath)
                .OfType<InputActionReference>()
                .FirstOrDefault(candidate => candidate.action != null
                    && candidate.action.actionMap != null
                    && $"{candidate.action.actionMap.name}/{candidate.action.name}" == actionPath);
            if (reference == null)
                throw new InvalidOperationException($"Input action '{actionPath}' was not found.");
            return reference;
        }

        private static RectTransform RequireRect(Transform parent, string childName)
        {
            Transform child = parent.Find(childName);
            if (child == null || child is not RectTransform rect)
                throw new InvalidOperationException($"Required UI object '{childName}' was not found under '{parent.name}'.");
            return rect;
        }

        private static GameObject CreateUiObject(string name, Transform parent, params Type[] components)
        {
            GameObject result = new GameObject(name, typeof(RectTransform));
            result.layer = UiLayer;
            if (parent != null)
                result.transform.SetParent(parent, false);
            foreach (Type component in components)
                result.AddComponent(component);
            return result;
        }

        private static Image CreateImage(string name, Transform parent, Color color, bool raycastTarget)
        {
            GameObject result = CreateUiObject(name, parent, typeof(CanvasRenderer), typeof(Image));
            Image image = result.GetComponent<Image>();
            image.color = color;
            image.raycastTarget = raycastTarget;
            return image;
        }

        private static Text CreateText(string name, Transform parent, Font font, int size, TextAnchor alignment, Color color)
        {
            GameObject result = CreateUiObject(name, parent, typeof(CanvasRenderer), typeof(Text));
            Text text = result.GetComponent<Text>();
            text.font = font;
            text.fontSize = size;
            text.alignment = alignment;
            text.color = color;
            text.raycastTarget = false;
            text.resizeTextForBestFit = true;
            text.resizeTextMinSize = 16;
            text.resizeTextMaxSize = size;
            return text;
        }

        private static void SetReference(SerializedObject serialized, string propertyName, UnityEngine.Object value)
        {
            SerializedProperty property = serialized.FindProperty(propertyName)
                ?? throw new InvalidOperationException($"Serialized property '{propertyName}' was not found.");
            property.objectReferenceValue = value;
        }

        private static void SetAnchors(RectTransform rect, float minX, float minY, float maxX, float maxY)
        {
            rect.anchorMin = new Vector2(minX, minY);
            rect.anchorMax = new Vector2(maxX, maxY);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        private static void SetStretch(RectTransform rect, float left, float right, float bottom, float top)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(left, bottom);
            rect.offsetMax = new Vector2(-right, -top);
        }

        private static void EnsureFolder(string parent, string child)
        {
            string path = $"{parent}/{child}";
            if (!AssetDatabase.IsValidFolder(path))
                AssetDatabase.CreateFolder(parent, child);
        }

        private static int UiLayer => LayerMask.NameToLayer("UI");
    }
}
