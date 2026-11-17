using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.UI;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace AlchemyTower.Editor
{
    [CustomEditor(typeof(RoundedImage))]
    [CanEditMultipleObjects]
    public sealed class RoundedImageEditor : GraphicEditor
    {
        private SerializedProperty cornerRadius;
        private SerializedProperty borderWidth;
        private SerializedProperty borderColor;

        protected override void OnEnable()
        {
            base.OnEnable();
            cornerRadius = serializedObject.FindProperty("cornerRadius");
            borderWidth = serializedObject.FindProperty("borderWidth");
            borderColor = serializedObject.FindProperty("borderColor");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            EditorGUILayout.PropertyField(m_Script);
            AppearanceControlsGUI();

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Rounded Shape", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(cornerRadius, new GUIContent("Corner Radius"));
            EditorGUILayout.PropertyField(borderWidth, new GUIContent("Border Width"));
            EditorGUILayout.PropertyField(borderColor, new GUIContent("Border Color"));

            EditorGUILayout.Space();
            RaycastControlsGUI();
            MaskableControlsGUI();

            serializedObject.ApplyModifiedProperties();
        }
    }

    internal static class RoundedImageConverter
    {
        private const string MenuPath = "Tools/Alchemy Tower/UI/Convert to Rounded Image";

        private sealed class ReferenceBinding
        {
            public UnityEngine.Object Owner { get; }
            public string PropertyPath { get; }

            public ReferenceBinding(UnityEngine.Object owner, string propertyPath)
            {
                Owner = owner;
                PropertyPath = propertyPath;
            }
        }

        private readonly struct ImageState
        {
            public readonly bool Enabled;
            public readonly Color Color;
            public readonly Material Material;
            public readonly bool RaycastTarget;
            public readonly Vector4 RaycastPadding;
            public readonly bool Maskable;

            public ImageState(Image image)
            {
                Enabled = image.enabled;
                Color = image.color;
                SerializedObject serialized = new SerializedObject(image);
                Material = serialized.FindProperty("m_Material").objectReferenceValue as Material;
                RaycastTarget = image.raycastTarget;
                RaycastPadding = image.raycastPadding;
                Maskable = image.maskable;
            }

            public void Apply(RoundedImage image)
            {
                image.enabled = Enabled;
                image.color = Color;
                image.material = Material;
                image.raycastTarget = RaycastTarget;
                image.raycastPadding = RaycastPadding;
                image.maskable = Maskable;
            }
        }

        [MenuItem(MenuPath, true)]
        private static bool ValidateSelectedImage()
        {
            return Selection.gameObjects.Length == 1
                && Selection.activeGameObject != null
                && Selection.activeGameObject.GetComponent<Image>() is Image image
                && image.GetType() == typeof(Image);
        }

        [MenuItem(MenuPath, false, 220)]
        private static void ConvertSelectedImage()
        {
            Image image = Selection.activeGameObject != null
                ? Selection.activeGameObject.GetComponent<Image>()
                : null;
            ConvertWithDialog(image);
        }

        [MenuItem("CONTEXT/Image/Convert to Rounded Image", true)]
        private static bool ValidateContextImage(MenuCommand command)
        {
            return command.context is Image image && image.GetType() == typeof(Image);
        }

        [MenuItem("CONTEXT/Image/Convert to Rounded Image")]
        private static void ConvertContextImage(MenuCommand command)
        {
            ConvertWithDialog(command.context as Image);
        }

        private static void ConvertWithDialog(Image image)
        {
            if (!TryValidate(image, out List<ReferenceBinding> references, out string warning))
            {
                EditorUtility.DisplayDialog("Cannot convert Image", warning, "OK");
                return;
            }

            Undo.IncrementCurrentGroup();
            int undoGroup = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Convert to Rounded Image");

            try
            {
                ImageState state = new ImageState(image);
                GameObject gameObject = image.gameObject;

                foreach (ReferenceBinding binding in references)
                    Undo.RegisterCompleteObjectUndo(binding.Owner, "Update RoundedImage reference");

                Undo.DestroyObjectImmediate(image);
                RoundedImage roundedImage = Undo.AddComponent<RoundedImage>(gameObject);
                Undo.RecordObject(roundedImage, "Configure Rounded Image");
                state.Apply(roundedImage);

                foreach (ReferenceBinding binding in references)
                {
                    SerializedObject owner = new SerializedObject(binding.Owner);
                    SerializedProperty property = owner.FindProperty(binding.PropertyPath)
                        ?? throw new InvalidOperationException(
                            $"Serialized reference '{binding.PropertyPath}' no longer exists on '{binding.Owner.name}'.");
                    property.objectReferenceValue = roundedImage;
                    owner.ApplyModifiedPropertiesWithoutUndo();
                    EditorUtility.SetDirty(binding.Owner);
                }

                EditorUtility.SetDirty(roundedImage);
                PrefabUtility.RecordPrefabInstancePropertyModifications(roundedImage);
                Selection.activeObject = roundedImage;
                Undo.CollapseUndoOperations(undoGroup);
            }
            catch (Exception exception)
            {
                Undo.RevertAllDownToGroup(undoGroup);
                EditorUtility.DisplayDialog(
                    "Image conversion cancelled",
                    $"No changes were kept because the Image could not be replaced safely.\n\n{exception.Message}",
                    "OK");
            }
        }

        private static bool TryValidate(
            Image image,
            out List<ReferenceBinding> references,
            out string warning)
        {
            references = null;
            warning = null;

            if (image == null || image.GetType() != typeof(Image))
            {
                warning = "Select exactly one GameObject with a standard Image component.";
                return false;
            }

            if (EditorUtility.IsPersistent(image))
            {
                warning = "Open the Prefab in Prefab Mode before converting its Image. Direct asset conversion is intentionally disabled.";
                return false;
            }

            if (PrefabUtility.IsPartOfImmutablePrefab(image))
            {
                warning = "The selected Image belongs to an immutable Prefab and cannot be replaced safely.";
                return false;
            }

            PrefabStage prefabStage = PrefabStageUtility.GetCurrentPrefabStage();
            if (prefabStage != null && PrefabUtility.IsPartOfPrefabInstance(image))
            {
                warning = "The selected Image belongs to a nested Prefab. Open that nested Prefab itself before converting the component.";
                return false;
            }

            if (image.sprite != null || image.overrideSprite != null)
            {
                warning = "RoundedImage v1 supports solid colors only. Remove the Source Image/Override Sprite first, or keep the existing Image.";
                return false;
            }

            if (image.type != Image.Type.Simple)
            {
                warning = "RoundedImage v1 does not preserve Sliced, Tiled, or Filled modes. Change the Image Type to Simple before conversion.";
                return false;
            }

            if (image.alphaHitTestMinimumThreshold > 0f)
            {
                warning = "The Image uses Alpha Hit Test, which procedural RoundedImage v1 does not preserve.";
                return false;
            }

            if (image.onCullStateChanged.GetPersistentEventCount() > 0)
            {
                warning = "The Image has persistent On Cull State Changed listeners. Remove or migrate them before conversion.";
                return false;
            }

            if (!HasOnlySupportedPrefabOverrides(image, out warning))
                return false;

            return TryFindReferences(image, prefabStage, out references, out warning);
        }

        private static bool HasOnlySupportedPrefabOverrides(Image image, out string warning)
        {
            warning = null;
            GameObject instanceRoot = PrefabUtility.GetOutermostPrefabInstanceRoot(image.gameObject);
            if (instanceRoot == null)
                return true;

            UnityEngine.Object sourceImage = PrefabUtility.GetCorrespondingObjectFromSource(image);
            PropertyModification[] modifications = PrefabUtility.GetPropertyModifications(instanceRoot);
            if (modifications == null)
                return true;

            foreach (PropertyModification modification in modifications)
            {
                if (modification.target != image && modification.target != sourceImage)
                    continue;

                string path = modification.propertyPath;
                if (path == "m_Enabled"
                    || path == "m_Material"
                    || path == "m_RaycastTarget"
                    || path == "m_Maskable"
                    || path.StartsWith("m_Color.", StringComparison.Ordinal)
                    || path.StartsWith("m_RaycastPadding.", StringComparison.Ordinal))
                    continue;

                warning = $"The Image has an unsupported Prefab Override ('{path}'). Apply, revert, or migrate that override before conversion.";
                return false;
            }

            return true;
        }

        private static bool TryFindReferences(
            Image image,
            PrefabStage prefabStage,
            out List<ReferenceBinding> references,
            out string warning)
        {
            references = new List<ReferenceBinding>();
            warning = null;

            List<GameObject> roots = new List<GameObject>();
            if (prefabStage != null && prefabStage.scene == image.gameObject.scene)
            {
                roots.Add(prefabStage.prefabContentsRoot);
            }
            else
            {
                Scene scene = image.gameObject.scene;
                if (!scene.IsValid() || !scene.isLoaded)
                {
                    warning = "The selected Image is not part of a loaded editable Scene or Prefab Stage.";
                    return false;
                }

                for (int sceneIndex = 0; sceneIndex < SceneManager.sceneCount; sceneIndex++)
                {
                    Scene loadedScene = SceneManager.GetSceneAt(sceneIndex);
                    if (loadedScene.IsValid() && loadedScene.isLoaded)
                        roots.AddRange(loadedScene.GetRootGameObjects());
                }
            }

            foreach (GameObject root in roots)
            {
                Component[] components = root.GetComponentsInChildren<Component>(true);
                foreach (Component component in components)
                {
                    if (component == null)
                    {
                        warning = $"'{root.name}' contains a missing script. References cannot be audited safely until it is fixed.";
                        return false;
                    }

                    if (component == image)
                        continue;

                    try
                    {
                        SerializedObject serialized = new SerializedObject(component);
                        SerializedProperty iterator = serialized.GetIterator();
                        while (iterator.Next(true))
                        {
                            if (iterator.propertyType == SerializedPropertyType.ObjectReference
                                && iterator.objectReferenceValue == image)
                            {
                                references.Add(new ReferenceBinding(component, iterator.propertyPath));
                            }
                        }
                    }
                    catch (Exception exception)
                    {
                        warning = $"References on '{component.name}' could not be inspected safely: {exception.Message}";
                        return false;
                    }
                }
            }

            return true;
        }
    }
}
