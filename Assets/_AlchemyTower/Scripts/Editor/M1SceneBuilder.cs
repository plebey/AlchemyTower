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
    public static class LegacyM1SceneRepair
    {
        private const string RootFolder = "Assets/_AlchemyTower";
        private const string DataFolder = RootFolder + "/Data";
        private const string FloorsFolder = DataFolder + "/Floors";
        private const string ScenesFolder = RootFolder + "/Scenes";
        private const string DefinitionPath = FloorsFolder + "/FirstFloor.asset";
        private const string ScenePath = ScenesFolder + "/M1FirstFloor.unity";
        private const string InputActionsPath = "Assets/Settings/InputSystem_Actions.inputactions";

        [MenuItem("Tools/Alchemy Tower/Legacy/Build Placeholder M1 Scene")]
        public static void BuildM1Scene()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return;

            EnsureFolder("Assets", "_AlchemyTower");
            EnsureFolder(RootFolder, "Data");
            EnsureFolder(DataFolder, "Floors");
            EnsureFolder(RootFolder, "Scenes");

            UnityEngine.Object sceneAsset = AssetDatabase.LoadMainAssetAtPath(ScenePath);
            if (sceneAsset != null)
            {
                if (sceneAsset is not SceneAsset)
                {
                    EditorUtility.DisplayDialog(
                        "Cannot create M1 scene",
                        $"Another asset already occupies {ScenePath}. It was not modified.",
                        "OK");
                    return;
                }

                bool openExisting = EditorUtility.DisplayDialog(
                    "M1 scene already exists",
                    $"{ScenePath} already exists and will not be rebuilt. Repair only fills an empty FloorView definition reference, adds a missing Main Camera, and fixes M1 button selected colors.",
                    "Open and repair M1 setup",
                    "Cancel");

                if (openExisting)
                    OpenAndRepairExistingScene();
                return;
            }

            if (!ValidateBuilderDependencies())
                return;

            FloorDefinitionSO definition = GetOrCreateDefinition();
            if (definition == null)
                return;

            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            BuildSceneHierarchy(definition);
            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Selection.activeObject = AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath);
            EditorUtility.DisplayDialog(
                "M1 scene created",
                $"Created {ScenePath}\n\nNo Build Settings or Project Settings were changed.",
                "OK");
        }

        [MenuItem("Tools/Alchemy Tower/Legacy/Repair Placeholder M1 Scene")]
        public static void RepairExistingM1Scene()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return;

            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) == null)
            {
                EditorUtility.DisplayDialog(
                    "M1 scene not found",
                    $"No scene exists at {ScenePath}. Run Build M1 Scene first.",
                    "OK");
                return;
            }

            bool confirmed = EditorUtility.DisplayDialog(
                "Repair existing M1 scene",
                "This will only fill empty FloorView definition references, add a Main Camera if needed, and fix the selected colors of the M1 production and upgrade buttons. It will not rebuild the UI hierarchy.",
                "Repair",
                "Cancel");

            if (confirmed)
                OpenAndRepairExistingScene();
        }

        private static bool ValidateBuilderDependencies()
        {
            try
            {
                if (Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf") == null)
                    throw new InvalidOperationException("Unity built-in font LegacyRuntime.ttf was not found.");

                string[] requiredActions =
                {
                    "UI/Point",
                    "UI/Click",
                    "UI/RightClick",
                    "UI/MiddleClick",
                    "UI/ScrollWheel",
                    "UI/Navigate",
                    "UI/Submit",
                    "UI/Cancel",
                    "UI/TrackedDevicePosition",
                    "UI/TrackedDeviceOrientation"
                };

                foreach (string action in requiredActions)
                    FindActionReference(action);

                return true;
            }
            catch (Exception exception)
            {
                EditorUtility.DisplayDialog(
                    "Cannot build M1 scene",
                    $"Required built-in UI resources could not be loaded. Nothing was created.\n\n{exception.Message}",
                    "OK");
                return false;
            }
        }

        private static FloorDefinitionSO GetOrCreateDefinition()
        {
            UnityEngine.Object existing = AssetDatabase.LoadMainAssetAtPath(DefinitionPath);
            if (existing != null)
            {
                if (existing is FloorDefinitionSO definition)
                    return definition;

                EditorUtility.DisplayDialog(
                    "Cannot create floor definition",
                    $"Another asset already occupies {DefinitionPath}. It was not modified.",
                    "OK");
                return null;
            }

            FloorDefinitionSO created = ScriptableObject.CreateInstance<FloorDefinitionSO>();
            AssetDatabase.CreateAsset(created, DefinitionPath);
            AssetDatabase.SaveAssets();

            // Reload the persistent object. Holding the just-created instance across
            // AssetDatabase.SaveAssets can produce a null scene reference after serialization.
            return AssetDatabase.LoadAssetAtPath<FloorDefinitionSO>(DefinitionPath);
        }

        private static void OpenAndRepairExistingScene()
        {
            FloorDefinitionSO definition = GetOrCreateDefinition();
            if (definition == null)
                return;

            Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            FloorView[] floorViews = UnityEngine.Object.FindObjectsByType<FloorView>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);

            int repairedReferenceCount = 0;
            int repairedButtonCount = 0;
            foreach (FloorView floorView in floorViews)
            {
                SerializedObject serialized = new SerializedObject(floorView);
                serialized.Update();
                SerializedProperty definitionProperty = serialized.FindProperty("definition");
                if (definitionProperty != null && definitionProperty.objectReferenceValue == null)
                {
                    Undo.RecordObject(floorView, "Repair M1 Floor Definition Reference");
                    definitionProperty.objectReferenceValue = definition;
                    serialized.ApplyModifiedProperties();
                    EditorUtility.SetDirty(floorView);
                    repairedReferenceCount++;
                }

                repairedButtonCount += RepairButtonSelectedColor(
                    serialized.FindProperty("productionButton")?.objectReferenceValue as Button);
                repairedButtonCount += RepairButtonSelectedColor(
                    serialized.FindProperty("upgradeButton")?.objectReferenceValue as Button);
            }

            bool cameraCreated = EnsureMainCamera();
            bool sceneChanged = repairedReferenceCount > 0 || repairedButtonCount > 0 || cameraCreated;
            if (sceneChanged)
            {
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
                AssetDatabase.SaveAssets();
            }

            EditorUtility.DisplayDialog(
                "M1 scene opened",
                sceneChanged
                    ? $"Repaired definition references: {repairedReferenceCount}. Button colors: {repairedButtonCount}. Main Camera created: {(cameraCreated ? "yes" : "no")}. No UI objects were rebuilt."
                    : "No missing references, button color issues, or camera were found. The scene was not modified.",
                "OK");
        }

        private static int RepairButtonSelectedColor(Button button)
        {
            if (button == null)
                return 0;

            ColorBlock colors = button.colors;
            if (colors.selectedColor == colors.normalColor)
                return 0;

            Undo.RecordObject(button, "Repair M1 Button Selected Color");
            colors.selectedColor = colors.normalColor;
            button.colors = colors;
            EditorUtility.SetDirty(button);
            return 1;
        }

        private static void BuildSceneHierarchy(FloorDefinitionSO definition)
        {
            Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            CreateMainCamera();

            GameObject canvasObject = new GameObject("M1 Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            Canvas canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080f, 1920f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

            Image background = CreateImage("Background", canvasObject.transform, new Color(0.08f, 0.07f, 0.13f));
            Stretch(background.rectTransform);

            Text title = CreateText("Title", canvasObject.transform, font, 58, TextAnchor.MiddleCenter, Color.white);
            SetAnchors(title.rectTransform, 0.08f, 0.88f, 0.92f, 0.97f);
            title.text = "ALCHEMY TOWER";

            Text coins = CreateText("Coins", canvasObject.transform, font, 48, TextAnchor.MiddleCenter, new Color(1f, 0.84f, 0.25f));
            SetAnchors(coins.rectTransform, 0.08f, 0.79f, 0.92f, 0.87f);

            Image floorImage = CreateImage("Floor Area", canvasObject.transform, new Color(0.20f, 0.15f, 0.30f));
            SetAnchors(floorImage.rectTransform, 0.07f, 0.30f, 0.93f, 0.77f);
            Button productionButton = floorImage.gameObject.AddComponent<Button>();
            ConfigureButtonColors(productionButton, floorImage.color);

            Text floorName = CreateText("Floor Name", floorImage.transform, font, 48, TextAnchor.MiddleCenter, Color.white);
            SetAnchors(floorName.rectTransform, 0.06f, 0.80f, 0.94f, 0.96f);

            Text status = CreateText("Status", floorImage.transform, font, 38, TextAnchor.MiddleCenter, Color.white);
            SetAnchors(status.rectTransform, 0.05f, 0.65f, 0.95f, 0.80f);

            Text level = CreateText("Level", floorImage.transform, font, 34, TextAnchor.MiddleLeft, Color.white);
            SetAnchors(level.rectTransform, 0.09f, 0.53f, 0.50f, 0.65f);

            Text reward = CreateText("Reward", floorImage.transform, font, 34, TextAnchor.MiddleRight, Color.white);
            SetAnchors(reward.rectTransform, 0.50f, 0.53f, 0.91f, 0.65f);

            Image progressBackground = CreateImage("Progress Background", floorImage.transform, new Color(0.07f, 0.06f, 0.10f));
            SetAnchors(progressBackground.rectTransform, 0.09f, 0.35f, 0.91f, 0.49f);

            Image progressFill = CreateImage("Progress Fill", progressBackground.transform, new Color(0.55f, 0.25f, 0.90f));
            Stretch(progressFill.rectTransform);
            progressFill.type = Image.Type.Filled;
            progressFill.fillMethod = Image.FillMethod.Horizontal;
            progressFill.fillOrigin = (int)Image.OriginHorizontal.Left;
            progressFill.fillAmount = 0f;

            Text progress = CreateText("Progress", progressBackground.transform, font, 30, TextAnchor.MiddleCenter, Color.white);
            Stretch(progress.rectTransform);

            Text tapHint = CreateText("Tap Hint", floorImage.transform, font, 32, TextAnchor.MiddleCenter, new Color(0.82f, 0.76f, 0.92f));
            SetAnchors(tapHint.rectTransform, 0.06f, 0.08f, 0.94f, 0.28f);
            tapHint.text = "Tap this large area to speed up brewing\nor collect when the potion is ready";

            Image upgradeImage = CreateImage("Upgrade Button", canvasObject.transform, new Color(0.85f, 0.48f, 0.12f));
            SetAnchors(upgradeImage.rectTransform, 0.15f, 0.11f, 0.85f, 0.23f);
            Button upgradeButton = upgradeImage.gameObject.AddComponent<Button>();
            ConfigureButtonColors(upgradeButton, upgradeImage.color);

            Text upgradeText = CreateText("Upgrade Label", upgradeImage.transform, font, 38, TextAnchor.MiddleCenter, Color.white);
            Stretch(upgradeText.rectTransform);

            GameObject controller = new GameObject("Floor Controller", typeof(FloorView));
            FloorView floorView = controller.GetComponent<FloorView>();
            AssignFloorViewReferences(
                floorView,
                definition,
                productionButton,
                upgradeButton,
                floorName,
                coins,
                status,
                level,
                reward,
                progress,
                upgradeText,
                progressFill);

            CreateEventSystem();
        }

        private static bool EnsureMainCamera()
        {
            Camera[] cameras = UnityEngine.Object.FindObjectsByType<Camera>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);
            if (cameras.Length > 0)
                return false;

            CreateMainCamera();
            return true;
        }

        private static Camera CreateMainCamera()
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
            return camera;
        }

        private static void CreateEventSystem()
        {
            GameObject eventSystemObject = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
            InputSystemUIInputModule module = eventSystemObject.GetComponent<InputSystemUIInputModule>();
            InputActionAsset actions = AssetDatabase.LoadAssetAtPath<InputActionAsset>(InputActionsPath);
            if (actions == null)
                throw new InvalidOperationException($"Input Actions asset was not found at {InputActionsPath}.");

            module.actionsAsset = actions;
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

        private static void AssignFloorViewReferences(
            FloorView floorView,
            FloorDefinitionSO definition,
            Button productionButton,
            Button upgradeButton,
            Text floorName,
            Text coins,
            Text status,
            Text level,
            Text reward,
            Text progress,
            Text upgradeText,
            Image progressFill)
        {
            SerializedObject serialized = new SerializedObject(floorView);
            serialized.FindProperty("definition").objectReferenceValue = definition;
            serialized.FindProperty("productionButton").objectReferenceValue = productionButton;
            serialized.FindProperty("upgradeButton").objectReferenceValue = upgradeButton;
            serialized.FindProperty("floorNameText").objectReferenceValue = floorName;
            serialized.FindProperty("coinsText").objectReferenceValue = coins;
            serialized.FindProperty("statusText").objectReferenceValue = status;
            serialized.FindProperty("levelText").objectReferenceValue = level;
            serialized.FindProperty("rewardText").objectReferenceValue = reward;
            serialized.FindProperty("progressText").objectReferenceValue = progress;
            serialized.FindProperty("upgradeButtonText").objectReferenceValue = upgradeText;
            serialized.FindProperty("progressFill").objectReferenceValue = progressFill;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static Image CreateImage(string name, Transform parent, Color color)
        {
            GameObject gameObject = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            gameObject.transform.SetParent(parent, false);
            Image image = gameObject.GetComponent<Image>();
            image.color = color;
            return image;
        }

        private static Text CreateText(string name, Transform parent, Font font, int fontSize, TextAnchor alignment, Color color)
        {
            GameObject gameObject = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            gameObject.transform.SetParent(parent, false);
            Text text = gameObject.GetComponent<Text>();
            text.font = font;
            text.fontSize = fontSize;
            text.alignment = alignment;
            text.color = color;
            text.raycastTarget = false;
            text.resizeTextForBestFit = true;
            text.resizeTextMinSize = 16;
            text.resizeTextMaxSize = fontSize;
            return text;
        }

        private static void ConfigureButtonColors(Button button, Color normal)
        {
            Graphic targetGraphic = button.GetComponent<Graphic>();
            button.targetGraphic = targetGraphic;

            ColorBlock colors = button.colors;
            colors.normalColor = normal;
            colors.highlightedColor = Color.Lerp(normal, Color.white, 0.15f);
            colors.pressedColor = Color.Lerp(normal, Color.black, 0.20f);
            colors.selectedColor = normal;
            colors.disabledColor = new Color(normal.r, normal.g, normal.b, 0.35f);
            colors.colorMultiplier = 1f;
            button.colors = colors;
            targetGraphic.color = Color.white;
        }

        private static void SetAnchors(RectTransform rect, float minX, float minY, float maxX, float maxY)
        {
            rect.anchorMin = new Vector2(minX, minY);
            rect.anchorMax = new Vector2(maxX, maxY);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        private static void Stretch(RectTransform rect)
        {
            SetAnchors(rect, 0f, 0f, 1f, 1f);
        }

        private static void EnsureFolder(string parent, string child)
        {
            string path = $"{parent}/{child}";
            if (!AssetDatabase.IsValidFolder(path))
                AssetDatabase.CreateFolder(parent, child);
        }
    }
}
