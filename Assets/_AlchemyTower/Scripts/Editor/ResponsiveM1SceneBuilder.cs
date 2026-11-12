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
    public static class M1SceneBuilder
    {
        private const string RootFolder = "Assets/_AlchemyTower";
        private const string DataFolder = RootFolder + "/Data";
        private const string FloorsFolder = DataFolder + "/Floors";
        private const string PrefabsFolder = RootFolder + "/Prefabs";
        private const string UiPrefabsFolder = PrefabsFolder + "/UI";
        private const string UiPrefabBackupsFolder = UiPrefabsFolder + "/Backups";
        private const string ScenesFolder = RootFolder + "/Scenes";
        private const string BackupsFolder = ScenesFolder + "/Backups";
        private const string DefinitionPath = FloorsFolder + "/FirstFloor.asset";
        private const string FloorPrefabPath = UiPrefabsFolder + "/FloorView.prefab";
        private const string ScenePath = ScenesFolder + "/M1FirstFloor.unity";
        private const string InputActionsPath = "Assets/Settings/InputSystem_Actions.inputactions";

        [MenuItem("Tools/Alchemy Tower/Build M1 Scene")]
        public static void BuildM1Scene()
        {
            M2TowerSceneBuilder.BuildScene(rebuildExisting: false);
        }

        [MenuItem("Tools/Alchemy Tower/Rebuild M1 Scene (Responsive)")]
        public static void RebuildResponsiveScene()
        {
            M2TowerSceneBuilder.BuildScene(rebuildExisting: true);
        }

        [MenuItem("Tools/Alchemy Tower/Upgrade FloorView Prefab (Automation)")]
        public static void UpgradeFloorViewPrefabForAutomation()
        {
            EnsureProjectFolders();
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(FloorPrefabPath);
            if (prefab == null)
            {
                EditorUtility.DisplayDialog(
                    "FloorView Prefab not found",
                    $"No Prefab exists at {FloorPrefabPath}. Run Build M1 Scene first; newly created Prefabs already include automation.",
                    "OK");
                return;
            }

            FloorView existingView = prefab.GetComponent<FloorView>();
            if (existingView == null)
            {
                EditorUtility.DisplayDialog("Invalid FloorView Prefab", "FloorView is missing on the Prefab root.", "OK");
                return;
            }

            SerializedObject existingSerialized = new SerializedObject(existingView);
            bool alreadyUpgraded = existingSerialized.FindProperty("automationButton")?.objectReferenceValue != null
                && existingSerialized.FindProperty("automationButtonText")?.objectReferenceValue != null;
            if (alreadyUpgraded)
            {
                EditorUtility.DisplayDialog("FloorView Prefab", "Automation controls are already configured. Nothing was modified.", "OK");
                return;
            }

            bool confirmed = EditorUtility.DisplayDialog(
                "Upgrade FloorView Prefab",
                "A backup will be created. The command adds only Automation Control below the existing upgrade control and assigns its FloorView references. Existing artwork is preserved.",
                "Back up and upgrade",
                "Cancel");
            if (!confirmed)
                return;

            string backupPath = AssetDatabase.GenerateUniqueAssetPath(
                $"{UiPrefabBackupsFolder}/FloorView_{DateTime.Now:yyyyMMdd_HHmmss}.prefab");
            if (!AssetDatabase.CopyAsset(FloorPrefabPath, backupPath))
            {
                EditorUtility.DisplayDialog("Backup failed", $"Could not create {backupPath}. The Prefab was not modified.", "OK");
                return;
            }

            GameObject contents = PrefabUtility.LoadPrefabContents(FloorPrefabPath);
            bool saved = false;
            try
            {
                FloorView floorView = contents.GetComponent<FloorView>();
                Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                Transform automationControl = contents.transform.Find("Automation Control");
                Button automationButton;
                Text automationLabel;

                if (automationControl == null)
                {
                    CreateAutomationControl(contents.transform, font, out automationButton, out automationLabel);
                }
                else
                {
                    automationButton = automationControl.Find("Automation Hit Area")?.GetComponent<Button>();
                    automationLabel = automationControl.Find("Automation Label")?.GetComponent<Text>();
                    if (automationButton == null || automationLabel == null)
                    {
                        EditorUtility.DisplayDialog(
                            "Automation Control is incomplete",
                            "An existing Automation Control was found, but its hit area or label is missing. The Prefab was not modified.",
                            "OK");
                        return;
                    }
                }

                LayoutElement rootSize = contents.GetComponent<LayoutElement>();
                if (rootSize != null)
                {
                    rootSize.minHeight = Mathf.Max(rootSize.minHeight, 1160f);
                    rootSize.preferredHeight = Mathf.Max(rootSize.preferredHeight, 1460f);
                }

                SerializedObject serialized = new SerializedObject(floorView);
                SetReference(serialized, "automationButton", automationButton);
                SetReference(serialized, "automationButtonText", automationLabel);
                serialized.ApplyModifiedPropertiesWithoutUndo();

                GameObject savedPrefab = PrefabUtility.SaveAsPrefabAsset(contents, FloorPrefabPath, out saved);
                saved = saved && savedPrefab != null;
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(contents);
            }

            if (saved)
                ConfigureFirstFloorAutomationDefinition();
            AssetDatabase.SaveAssets();
            EditorUtility.DisplayDialog(
                saved ? "FloorView Prefab upgraded" : "FloorView Prefab upgrade failed",
                saved
                    ? $"Automation Control was added. Backup: {backupPath}. The scene was not rebuilt."
                    : $"Unity could not save the upgraded Prefab. Backup: {backupPath}.",
                "OK");
        }

        private static void ConfigureFirstFloorAutomationDefinition()
        {
            FloorDefinitionSO definition = AssetDatabase.LoadAssetAtPath<FloorDefinitionSO>(DefinitionPath);
            if (definition == null)
                return;

            SerializedObject serialized = new SerializedObject(definition);
            serialized.FindProperty("floorNumber").intValue = 1;
            serialized.FindProperty("unlockCostCoins").intValue = 0;
            serialized.FindProperty("firstFloorAutomationCostCoins").intValue = 100;
            serialized.FindProperty("automationUnlockCostMultiplier").intValue = 20;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(definition);
        }

        private static bool PrepareAssets(out FloorDefinitionSO definition, out GameObject floorPrefab)
        {
            definition = null;
            floorPrefab = null;

            EnsureProjectFolders();
            if (!ValidateDependencies())
                return false;

            definition = GetOrCreateDefinition();
            if (definition == null)
                return false;

            floorPrefab = GetOrCreateFloorPrefab();
            return floorPrefab != null;
        }

        private static FloorDefinitionSO GetOrCreateDefinition()
        {
            UnityEngine.Object existing = AssetDatabase.LoadMainAssetAtPath(DefinitionPath);
            if (existing != null)
            {
                if (existing is FloorDefinitionSO definition)
                    return definition;

                ShowBlockedPath(DefinitionPath);
                return null;
            }

            FloorDefinitionSO created = ScriptableObject.CreateInstance<FloorDefinitionSO>();
            AssetDatabase.CreateAsset(created, DefinitionPath);
            AssetDatabase.SaveAssets();
            return AssetDatabase.LoadAssetAtPath<FloorDefinitionSO>(DefinitionPath);
        }

        private static GameObject GetOrCreateFloorPrefab()
        {
            UnityEngine.Object existing = AssetDatabase.LoadMainAssetAtPath(FloorPrefabPath);
            if (existing != null)
            {
                if (existing is not GameObject existingPrefab)
                {
                    ShowBlockedPath(FloorPrefabPath);
                    return null;
                }

                if (existingPrefab.GetComponent<FloorView>() == null)
                {
                    EditorUtility.DisplayDialog(
                        "Invalid FloorView Prefab",
                        $"{FloorPrefabPath} exists but has no FloorView on its root. It was not modified.",
                        "OK");
                    return null;
                }

                if (!ValidateFloorPrefabReferences(existingPrefab))
                    return null;

                return existingPrefab;
            }

            Scene activeScene = SceneManager.GetActiveScene();
            Scene prefabStagingScene = EditorSceneManager.NewScene(
                NewSceneSetup.EmptyScene,
                NewSceneMode.Additive);
            try
            {
                SceneManager.SetActiveScene(prefabStagingScene);
                Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                GameObject root = CreateFloorPrefabHierarchy(font, prefabStagingScene);
                GameObject savedPrefab = PrefabUtility.SaveAsPrefabAsset(root, FloorPrefabPath, out bool success);
                if (!success || savedPrefab == null)
                {
                    EditorUtility.DisplayDialog("Prefab creation failed", $"Could not create {FloorPrefabPath}.", "OK");
                    return null;
                }
            }
            finally
            {
                if (activeScene.IsValid() && activeScene.isLoaded)
                    SceneManager.SetActiveScene(activeScene);
                if (prefabStagingScene.IsValid() && prefabStagingScene.isLoaded)
                    EditorSceneManager.CloseScene(prefabStagingScene, true);
            }

            AssetDatabase.SaveAssets();
            GameObject createdPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(FloorPrefabPath);
            return createdPrefab != null && ValidateFloorPrefabReferences(createdPrefab) ? createdPrefab : null;
        }

        private static bool ValidateFloorPrefabReferences(GameObject prefab)
        {
            FloorView floorView = prefab.GetComponent<FloorView>();
            SerializedObject serialized = new SerializedObject(floorView);
            string[] internalReferences =
            {
                "productionButton", "upgradeButton", "floorNameText", "statusText", "levelText",
                "rewardText", "progressText", "upgradeButtonText", "progressFill",
                "automationButton", "automationButtonText"
            };

            string[] missing = internalReferences
                .Where(propertyName => serialized.FindProperty(propertyName)?.objectReferenceValue == null)
                .ToArray();
            if (missing.Length == 0)
                return true;

            EditorUtility.DisplayDialog(
                "FloorView Prefab has missing references",
                $"{FloorPrefabPath} was not modified. Repair these fields in Prefab Mode: {string.Join(", ", missing)}.",
                "OK");
            return false;
        }

        private static GameObject CreateFloorPrefabHierarchy(Font font, Scene targetScene)
        {
            GameObject root = CreateUiObject("FloorView", null, typeof(VerticalLayoutGroup), typeof(LayoutElement), typeof(FloorView));
            SceneManager.MoveGameObjectToScene(root, targetScene);

            VerticalLayoutGroup rootLayout = root.GetComponent<VerticalLayoutGroup>();
            rootLayout.padding = new RectOffset(0, 0, 0, 0);
            rootLayout.spacing = 24f;
            rootLayout.childAlignment = TextAnchor.UpperCenter;
            rootLayout.childControlWidth = true;
            rootLayout.childControlHeight = true;
            rootLayout.childForceExpandWidth = true;
            rootLayout.childForceExpandHeight = false;

            LayoutElement rootSize = root.GetComponent<LayoutElement>();
            rootSize.minHeight = 1160f;
            rootSize.preferredHeight = 1460f;
            rootSize.flexibleHeight = 1f;

            Image roomPanel = CreateImage("Room Panel", root.transform, new Color(0.20f, 0.15f, 0.30f), Image.Type.Sliced);
            LayoutElement roomSize = roomPanel.gameObject.AddComponent<LayoutElement>();
            roomSize.minHeight = 820f;
            roomSize.preferredHeight = 1080f;
            roomSize.flexibleHeight = 1f;

            Image roomArtwork = CreateImage("Room Artwork Slot", roomPanel.transform, new Color(0.13f, 0.10f, 0.20f), Image.Type.Simple);
            SetStretch(roomArtwork.rectTransform, 26f, 26f, 26f, 26f);

            Text floorName = CreateText("Floor Name", roomPanel.transform, font, 50, TextAnchor.MiddleCenter, Color.white);
            SetAnchors(floorName.rectTransform, 0.07f, 0.82f, 0.93f, 0.96f);

            Text status = CreateText("Brewing Status", roomPanel.transform, font, 38, TextAnchor.MiddleCenter, Color.white);
            SetAnchors(status.rectTransform, 0.07f, 0.67f, 0.93f, 0.81f);

            Text level = CreateText("Floor Level", roomPanel.transform, font, 34, TextAnchor.MiddleLeft, Color.white);
            SetAnchors(level.rectTransform, 0.10f, 0.54f, 0.49f, 0.66f);

            Text reward = CreateText("Potion Reward", roomPanel.transform, font, 34, TextAnchor.MiddleRight, Color.white);
            SetAnchors(reward.rectTransform, 0.51f, 0.54f, 0.90f, 0.66f);

            Image progressTrack = CreateImage("Progress Track", roomPanel.transform, new Color(0.06f, 0.05f, 0.09f), Image.Type.Sliced);
            SetAnchors(progressTrack.rectTransform, 0.10f, 0.36f, 0.90f, 0.50f);

            Image progressFill = CreateImage("Progress Fill", progressTrack.transform, new Color(0.55f, 0.25f, 0.90f), Image.Type.Filled);
            SetStretch(progressFill.rectTransform, 0f, 0f, 0f, 0f);
            progressFill.fillMethod = Image.FillMethod.Horizontal;
            progressFill.fillOrigin = (int)Image.OriginHorizontal.Left;
            progressFill.fillAmount = 0f;

            Text progressText = CreateText("Progress Time", progressTrack.transform, font, 30, TextAnchor.MiddleCenter, Color.white);
            SetStretch(progressText.rectTransform, 0f, 0f, 0f, 0f);

            Text tapHint = CreateText("Interaction Hint", roomPanel.transform, font, 30, TextAnchor.MiddleCenter, new Color(0.84f, 0.78f, 0.94f));
            SetAnchors(tapHint.rectTransform, 0.08f, 0.10f, 0.92f, 0.30f);
            tapHint.text = "Tap the room to speed up brewing\nor collect when the potion is ready";

            Image brewingHitImage = CreateImage("Brewing Hit Area", roomPanel.transform, Color.clear, Image.Type.Simple, raycastTarget: true);
            SetStretch(brewingHitImage.rectTransform, 0f, 0f, 0f, 0f);
            Button productionButton = brewingHitImage.gameObject.AddComponent<Button>();
            productionButton.targetGraphic = brewingHitImage;
            productionButton.transition = Selectable.Transition.None;

            GameObject upgradeControl = CreateUiObject("Upgrade Control", root.transform, typeof(LayoutElement));
            LayoutElement upgradeSize = upgradeControl.GetComponent<LayoutElement>();
            upgradeSize.minHeight = 130f;
            upgradeSize.preferredHeight = 160f;
            upgradeSize.flexibleHeight = 0f;

            Image upgradeBackground = CreateImage("Upgrade Background", upgradeControl.transform, new Color(0.85f, 0.48f, 0.12f), Image.Type.Sliced);
            SetStretch(upgradeBackground.rectTransform, 100f, 100f, 0f, 0f);

            Image upgradeHitImage = CreateImage("Upgrade Hit Area", upgradeControl.transform, Color.clear, Image.Type.Simple, raycastTarget: true);
            SetStretch(upgradeHitImage.rectTransform, 100f, 100f, 0f, 0f);
            Button upgradeButton = upgradeHitImage.gameObject.AddComponent<Button>();
            upgradeButton.targetGraphic = upgradeHitImage;
            upgradeButton.transition = Selectable.Transition.None;

            Text upgradeLabel = CreateText("Upgrade Label", upgradeControl.transform, font, 38, TextAnchor.MiddleCenter, Color.white);
            SetStretch(upgradeLabel.rectTransform, 120f, 120f, 12f, 12f);

            CreateAutomationControl(root.transform, font, out Button automationButton, out Text automationLabel);

            FloorView floorView = root.GetComponent<FloorView>();
            SerializedObject serialized = new SerializedObject(floorView);
            SetReference(serialized, "productionButton", productionButton);
            SetReference(serialized, "upgradeButton", upgradeButton);
            SetReference(serialized, "floorNameText", floorName);
            SetReference(serialized, "statusText", status);
            SetReference(serialized, "levelText", level);
            SetReference(serialized, "rewardText", reward);
            SetReference(serialized, "progressText", progressText);
            SetReference(serialized, "upgradeButtonText", upgradeLabel);
            SetReference(serialized, "progressFill", progressFill);
            SetReference(serialized, "automationButton", automationButton);
            SetReference(serialized, "automationButtonText", automationLabel);
            serialized.ApplyModifiedPropertiesWithoutUndo();

            return root;
        }

        private static void CreateAutomationControl(
            Transform parent,
            Font font,
            out Button automationButton,
            out Text automationLabel)
        {
            GameObject automationControl = CreateUiObject("Automation Control", parent, typeof(LayoutElement));
            LayoutElement automationSize = automationControl.GetComponent<LayoutElement>();
            automationSize.minHeight = 130f;
            automationSize.preferredHeight = 160f;
            automationSize.flexibleHeight = 0f;

            Image automationBackground = CreateImage(
                "Automation Background",
                automationControl.transform,
                new Color(0.23f, 0.58f, 0.48f),
                Image.Type.Sliced);
            SetStretch(automationBackground.rectTransform, 100f, 100f, 0f, 0f);

            Image automationHitImage = CreateImage(
                "Automation Hit Area",
                automationControl.transform,
                Color.clear,
                Image.Type.Simple,
                raycastTarget: true);
            SetStretch(automationHitImage.rectTransform, 100f, 100f, 0f, 0f);
            automationButton = automationHitImage.gameObject.AddComponent<Button>();
            automationButton.targetGraphic = automationHitImage;
            automationButton.transition = Selectable.Transition.None;

            automationLabel = CreateText(
                "Automation Label",
                automationControl.transform,
                font,
                36,
                TextAnchor.MiddleCenter,
                Color.white);
            automationLabel.text = "Hire Assistant — 100";
            SetStretch(automationLabel.rectTransform, 120f, 120f, 12f, 12f);
        }

        private static void CreateResponsiveScene(FloorDefinitionSO definition, GameObject floorPrefab)
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

            Image background = CreateImage("Background Artwork", canvasObject.transform, new Color(0.08f, 0.07f, 0.13f), Image.Type.Simple);
            SetStretch(background.rectTransform, 0f, 0f, 0f, 0f);

            GameObject safeArea = CreateUiObject("Mobile Safe Area", canvasObject.transform, typeof(SafeAreaFitter));
            SetStretch(safeArea.GetComponent<RectTransform>(), 0f, 0f, 0f, 0f);

            GameObject content = CreateUiObject("Portrait Content", safeArea.transform, typeof(VerticalLayoutGroup));
            SetStretch(content.GetComponent<RectTransform>(), 0f, 0f, 0f, 0f);
            VerticalLayoutGroup contentLayout = content.GetComponent<VerticalLayoutGroup>();
            contentLayout.padding = new RectOffset(48, 48, 36, 48);
            contentLayout.spacing = 32f;
            contentLayout.childAlignment = TextAnchor.UpperCenter;
            contentLayout.childControlWidth = true;
            contentLayout.childControlHeight = true;
            contentLayout.childForceExpandWidth = true;
            contentLayout.childForceExpandHeight = false;

            GameObject header = CreateUiObject("Game Header", content.transform, typeof(LayoutElement));
            LayoutElement headerSize = header.GetComponent<LayoutElement>();
            headerSize.minHeight = 180f;
            headerSize.preferredHeight = 230f;
            headerSize.flexibleHeight = 0f;

            Text title = CreateText("Game Title", header.transform, font, 64, TextAnchor.MiddleCenter, Color.white);
            SetAnchors(title.rectTransform, 0f, 0.43f, 1f, 1f);
            title.text = "ALCHEMY TOWER";

            Text coins = CreateText("Coin Counter", header.transform, font, 48, TextAnchor.MiddleCenter, new Color(1f, 0.84f, 0.25f));
            SetAnchors(coins.rectTransform, 0f, 0f, 1f, 0.46f);

            GameObject floorInstance = PrefabUtility.InstantiatePrefab(floorPrefab, scene) as GameObject;
            if (floorInstance == null)
                throw new InvalidOperationException("Could not instantiate FloorView Prefab.");

            floorInstance.name = "First Floor";
            floorInstance.transform.SetParent(content.transform, false);
            FloorView floorView = floorInstance.GetComponent<FloorView>();
            SerializedObject floorSerialized = new SerializedObject(floorView);
            SetReference(floorSerialized, "definition", definition);
            SetReference(floorSerialized, "coinsText", coins);
            floorSerialized.ApplyModifiedPropertiesWithoutUndo();

            CreateEventSystem();

            if (!EditorSceneManager.SaveScene(scene, ScenePath))
                throw new InvalidOperationException($"Unity could not save {ScenePath}.");

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Selection.activeObject = AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath);
            EditorUtility.DisplayDialog(
                "Responsive M1 scene created",
                $"Created {ScenePath} from {FloorPrefabPath}.\n\nReference: 1080 x 1920, Canvas Scaler matches width, Safe Area enabled, Android orientation set to portrait.",
                "OK");
        }

        private static bool BackUpExistingScene()
        {
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) == null)
                return true;

            string backupPath = AssetDatabase.GenerateUniqueAssetPath(
                $"{BackupsFolder}/M1FirstFloor_{DateTime.Now:yyyyMMdd_HHmmss}.unity");
            if (AssetDatabase.CopyAsset(ScenePath, backupPath))
            {
                AssetDatabase.SaveAssets();
                return true;
            }

            EditorUtility.DisplayDialog(
                "Backup failed",
                $"Could not copy {ScenePath}. The scene was not rebuilt.",
                "OK");
            return false;
        }

        private static void ConfigurePortraitOrientation()
        {
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.Portrait;
            PlayerSettings.allowedAutorotateToPortrait = true;
            PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
            PlayerSettings.allowedAutorotateToLandscapeLeft = false;
            PlayerSettings.allowedAutorotateToLandscapeRight = false;
            AssetDatabase.SaveAssets();
        }

        private static bool ValidateDependencies()
        {
            try
            {
                if (Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf") == null)
                    throw new InvalidOperationException("Unity built-in font LegacyRuntime.ttf was not found.");
                if (AssetDatabase.LoadAssetAtPath<InputActionAsset>(InputActionsPath) == null)
                    throw new InvalidOperationException($"Input Actions asset was not found at {InputActionsPath}.");

                string[] requiredActions =
                {
                    "UI/Point", "UI/Click", "UI/RightClick", "UI/MiddleClick", "UI/ScrollWheel",
                    "UI/Navigate", "UI/Submit", "UI/Cancel", "UI/TrackedDevicePosition", "UI/TrackedDeviceOrientation"
                };
                foreach (string action in requiredActions)
                    FindActionReference(action);

                return true;
            }
            catch (Exception exception)
            {
                EditorUtility.DisplayDialog("Cannot build responsive M1 UI", exception.Message, "OK");
                return false;
            }
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
            camera.orthographicSize = 5f;
            camera.targetDisplay = 0;
        }

        private static void CreateEventSystem()
        {
            GameObject eventSystemObject = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
            InputSystemUIInputModule module = eventSystemObject.GetComponent<InputSystemUIInputModule>();
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
                .FirstOrDefault(candidate => candidate.action != null && candidate.action.actionMap != null
                    && $"{candidate.action.actionMap.name}/{candidate.action.name}" == actionPath);

            if (reference == null)
                throw new InvalidOperationException($"Input action reference '{actionPath}' was not found.");
            return reference;
        }

        private static GameObject CreateUiObject(string name, Transform parent, params Type[] components)
        {
            GameObject gameObject = new GameObject(name, typeof(RectTransform));
            gameObject.layer = UiLayer;
            if (parent != null)
                gameObject.transform.SetParent(parent, false);
            foreach (Type component in components)
                gameObject.AddComponent(component);
            return gameObject;
        }

        private static Image CreateImage(
            string name,
            Transform parent,
            Color color,
            Image.Type type,
            bool raycastTarget = false)
        {
            GameObject gameObject = CreateUiObject(name, parent, typeof(CanvasRenderer), typeof(Image));
            Image image = gameObject.GetComponent<Image>();
            image.color = color;
            image.type = type;
            image.raycastTarget = raycastTarget;
            return image;
        }

        private static Text CreateText(
            string name,
            Transform parent,
            Font font,
            int fontSize,
            TextAnchor alignment,
            Color color)
        {
            GameObject gameObject = CreateUiObject(name, parent, typeof(CanvasRenderer), typeof(Text));
            Text text = gameObject.GetComponent<Text>();
            text.font = font;
            text.fontSize = fontSize;
            text.alignment = alignment;
            text.color = color;
            text.raycastTarget = false;
            text.resizeTextForBestFit = true;
            text.resizeTextMinSize = 18;
            text.resizeTextMaxSize = fontSize;
            return text;
        }

        private static void SetReference(SerializedObject serialized, string propertyName, UnityEngine.Object value)
        {
            SerializedProperty property = serialized.FindProperty(propertyName);
            if (property == null)
                throw new InvalidOperationException($"Serialized property '{propertyName}' was not found.");
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

        private static void EnsureProjectFolders()
        {
            EnsureFolder("Assets", "_AlchemyTower");
            EnsureFolder(RootFolder, "Data");
            EnsureFolder(DataFolder, "Floors");
            EnsureFolder(RootFolder, "Prefabs");
            EnsureFolder(PrefabsFolder, "UI");
            EnsureFolder(UiPrefabsFolder, "Backups");
            EnsureFolder(RootFolder, "Scenes");
            EnsureFolder(ScenesFolder, "Backups");
        }

        private static void EnsureFolder(string parent, string child)
        {
            string path = $"{parent}/{child}";
            if (!AssetDatabase.IsValidFolder(path))
                AssetDatabase.CreateFolder(parent, child);
        }

        private static void ShowBlockedPath(string path)
        {
            EditorUtility.DisplayDialog(
                "Asset path is already occupied",
                $"Another asset occupies {path}. It was not modified.",
                "OK");
        }

        private static int UiLayer => LayerMask.NameToLayer("UI");
    }
}
