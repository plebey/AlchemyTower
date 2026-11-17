using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace AlchemyTower.Editor
{
    [CustomEditor(typeof(RoundedCorners))]
    [CanEditMultipleObjects]
    public sealed class RoundedCornersEditor : UnityEditor.Editor
    {
        private const int PreviewSegmentsPerCorner = 8;

        private SerializedProperty cornerRadius;
        private SerializedProperty useIndividualCorners;
        private SerializedProperty topLeftRadius;
        private SerializedProperty topRightRadius;
        private SerializedProperty bottomRightRadius;
        private SerializedProperty bottomLeftRadius;
        private SerializedProperty borderWidth;
        private SerializedProperty borderColor;
        private SerializedProperty edgeSoftness;
        private bool previewInactiveShape;

        private void OnEnable()
        {
            cornerRadius = serializedObject.FindProperty("cornerRadius");
            useIndividualCorners = serializedObject.FindProperty("useIndividualCorners");
            topLeftRadius = serializedObject.FindProperty("topLeftRadius");
            topRightRadius = serializedObject.FindProperty("topRightRadius");
            bottomRightRadius = serializedObject.FindProperty("bottomRightRadius");
            bottomLeftRadius = serializedObject.FindProperty("bottomLeftRadius");
            borderWidth = serializedObject.FindProperty("borderWidth");
            borderColor = serializedObject.FindProperty("borderColor");
            edgeSoftness = serializedObject.FindProperty("edgeSoftness");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            EditorGUILayout.PropertyField(useIndividualCorners, new GUIContent("Individual Corners"));
            if (useIndividualCorners.boolValue)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.PropertyField(topLeftRadius, new GUIContent("Top Left"));
                EditorGUILayout.PropertyField(topRightRadius, new GUIContent("Top Right"));
                EditorGUILayout.PropertyField(bottomRightRadius, new GUIContent("Bottom Right"));
                EditorGUILayout.PropertyField(bottomLeftRadius, new GUIContent("Bottom Left"));
                EditorGUI.indentLevel--;
            }
            else
            {
                EditorGUILayout.PropertyField(cornerRadius, new GUIContent("Corner Radius"));
            }

            EditorGUILayout.PropertyField(borderWidth, new GUIContent("Border Width"));
            EditorGUILayout.PropertyField(borderColor, new GUIContent("Border Color"));
            EditorGUILayout.PropertyField(edgeSoftness, new GUIContent("Edge Softness"));

            serializedObject.ApplyModifiedProperties();

            RoundedCorners roundedCorners = target as RoundedCorners;
            Image image = roundedCorners != null ? roundedCorners.GetComponent<Image>() : null;
            if (image != null)
            {
                EditorGUILayout.Space();
                EditorGUILayout.HelpBox(
                    $"The existing Image remains unchanged. Current mode: {image.type}; sprite and UV geometry are rendered by standard uGUI.",
                    MessageType.Info);

                if (!image.gameObject.activeInHierarchy && targets.Length == 1)
                {
                    bool nextPreview = EditorGUILayout.ToggleLeft(
                        "Preview inactive shape in Scene view (session only)",
                        previewInactiveShape);
                    if (nextPreview != previewInactiveShape)
                    {
                        previewInactiveShape = nextPreview;
                        SceneView.RepaintAll();
                    }

                    if (previewInactiveShape)
                    {
                        EditorGUILayout.HelpBox(
                            "The preview draws only the rounded shape and colors. It does not activate the object, execute gameplay code, or save visibility state.",
                            MessageType.None);
                    }
                }

                Mask mask = image.GetComponent<Mask>();
                if (mask != null && IsMaskAfterRoundedCorners(image.gameObject, roundedCorners))
                {
                    EditorGUILayout.HelpBox(
                        "For reliable live parameter updates when this Image is also a stencil Mask, keep Rounded Corners below Mask in the component list.",
                        MessageType.Warning);
                }
            }
        }

        private void OnSceneGUI()
        {
            if (!previewInactiveShape || targets.Length != 1)
                return;

            RoundedCorners roundedCorners = target as RoundedCorners;
            if (roundedCorners == null || roundedCorners.gameObject.activeInHierarchy)
                return;

            Image image = roundedCorners.GetComponent<Image>();
            RectTransform rectTransform = image.rectTransform;
            Rect rect = rectTransform.rect;
            Vector4 radii = roundedCorners.GetEffectiveCornerRadii();
            List<Vector3> worldPoints = BuildWorldContour(rectTransform, rect, radii);
            if (worldPoints.Count < 3)
                return;

            Color fill = image.color;
            fill.a = Mathf.Min(0.3f, Mathf.Max(0.08f, fill.a * 0.3f));
            Handles.color = fill;
            Handles.DrawAAConvexPolygon(worldPoints.ToArray());

            Color outline = roundedCorners.BorderWidth > 0f
                ? roundedCorners.BorderColor
                : new Color(1f, 1f, 1f, 0.8f);
            outline.a = Mathf.Max(0.55f, outline.a);
            Handles.color = outline;
            Handles.DrawAAPolyLine(3f, CloseContour(worldPoints));
            Handles.Label(rectTransform.TransformPoint(rect.center), "Inactive RoundedCorners preview");
        }

        private static List<Vector3> BuildWorldContour(
            RectTransform transform,
            Rect rect,
            Vector4 radii)
        {
            List<Vector3> points = new List<Vector3>(4 * (PreviewSegmentsPerCorner + 1));
            AddCorner(points, transform, new Vector2(rect.xMax - radii.z, rect.yMin + radii.z), radii.z, -90f);
            AddCorner(points, transform, new Vector2(rect.xMax - radii.y, rect.yMax - radii.y), radii.y, 0f);
            AddCorner(points, transform, new Vector2(rect.xMin + radii.x, rect.yMax - radii.x), radii.x, 90f);
            AddCorner(points, transform, new Vector2(rect.xMin + radii.w, rect.yMin + radii.w), radii.w, 180f);
            return points;
        }

        private static void AddCorner(
            ICollection<Vector3> points,
            RectTransform transform,
            Vector2 center,
            float radius,
            float startAngle)
        {
            for (int i = 0; i <= PreviewSegmentsPerCorner; i++)
            {
                float angle = (startAngle + 90f * i / PreviewSegmentsPerCorner) * Mathf.Deg2Rad;
                Vector2 local = center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
                points.Add(transform.TransformPoint(local));
            }
        }

        private static Vector3[] CloseContour(IReadOnlyList<Vector3> points)
        {
            Vector3[] closed = new Vector3[points.Count + 1];
            for (int i = 0; i < points.Count; i++)
                closed[i] = points[i];
            closed[points.Count] = points[0];
            return closed;
        }

        private static bool IsMaskAfterRoundedCorners(GameObject gameObject, RoundedCorners roundedCorners)
        {
            Component[] components = gameObject.GetComponents<Component>();
            int roundedIndex = -1;
            int maskIndex = -1;
            for (int i = 0; i < components.Length; i++)
            {
                if (components[i] == roundedCorners)
                    roundedIndex = i;
                else if (components[i] is Mask)
                    maskIndex = i;
            }

            return maskIndex > roundedIndex && roundedIndex >= 0;
        }
    }

    internal static class RoundedCornersMenu
    {
        private const string MenuPath = "Tools/Alchemy Tower/UI/Add Rounded Corners";

        [MenuItem(MenuPath, true)]
        private static bool ValidateSelectedImage()
        {
            return Selection.gameObjects.Length == 1
                && Selection.activeGameObject != null
                && Selection.activeGameObject.GetComponent<Image>() != null
                && Selection.activeGameObject.GetComponent<RoundedCorners>() == null
                && Selection.activeGameObject.GetComponent<RoundedImage>() == null;
        }

        [MenuItem(MenuPath, false, 219)]
        private static void AddToSelectedImage()
        {
            Add(Selection.activeGameObject != null
                ? Selection.activeGameObject.GetComponent<Image>()
                : null);
        }

        [MenuItem("CONTEXT/Image/Add Rounded Corners", true)]
        private static bool ValidateContextImage(MenuCommand command)
        {
            return command.context is Image image
                && image.GetComponent<RoundedCorners>() == null
                && image.GetComponent<RoundedImage>() == null;
        }

        [MenuItem("CONTEXT/Image/Add Rounded Corners")]
        private static void AddContextImage(MenuCommand command)
        {
            Add(command.context as Image);
        }

        private static void Add(Image image)
        {
            if (image == null || image.GetComponent<RoundedCorners>() != null)
                return;

            RoundedCorners result = Undo.AddComponent<RoundedCorners>(image.gameObject);
            Selection.activeObject = result;
            EditorUtility.SetDirty(image.gameObject);
        }
    }
}
