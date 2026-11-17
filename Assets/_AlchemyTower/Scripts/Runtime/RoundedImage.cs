using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace AlchemyTower
{
    [AddComponentMenu("UI (Canvas)/Rounded Image", 12)]
    public sealed class RoundedImage : Image
    {
        private const int SegmentsPerCorner = 8;

        [SerializeField, Min(0f)] private float cornerRadius = 16f;
        [SerializeField, Min(0f)] private float borderWidth;
        [SerializeField] private Color borderColor = Color.clear;

        public float CornerRadius
        {
            get => cornerRadius;
            set
            {
                float clamped = Mathf.Max(0f, value);
                if (Mathf.Approximately(cornerRadius, clamped))
                    return;

                cornerRadius = clamped;
                SetVerticesDirty();
            }
        }

        public float BorderWidth
        {
            get => borderWidth;
            set
            {
                float clamped = Mathf.Max(0f, value);
                if (Mathf.Approximately(borderWidth, clamped))
                    return;

                borderWidth = clamped;
                SetVerticesDirty();
            }
        }

        public Color BorderColor
        {
            get => borderColor;
            set
            {
                if (borderColor == value)
                    return;

                borderColor = value;
                SetVerticesDirty();
            }
        }

        // RoundedImage deliberately renders solid geometry only. Returning the shared
        // white texture keeps the standard UI/Default material and batching path.
        public override Texture mainTexture => Texture2D.whiteTexture;

        protected override void OnPopulateMesh(VertexHelper vertexHelper)
        {
            vertexHelper.Clear();

            Rect rect = rectTransform.rect;
            if (rect.width <= 0f || rect.height <= 0f)
                return;

            float radius = Mathf.Min(cornerRadius, Mathf.Min(rect.width, rect.height) * 0.5f);
            float width = Mathf.Min(borderWidth, Mathf.Min(rect.width, rect.height) * 0.5f);

            List<Vector2> outer = BuildContour(rect, radius);
            if (width <= 0f)
            {
                AddFilledContour(vertexHelper, outer, rect.center, color);
                return;
            }

            Rect innerRect = new Rect(
                rect.xMin + width,
                rect.yMin + width,
                Mathf.Max(0f, rect.width - width * 2f),
                Mathf.Max(0f, rect.height - width * 2f));

            if (innerRect.width <= 0f || innerRect.height <= 0f)
            {
                AddFilledContour(vertexHelper, outer, rect.center, borderColor);
                return;
            }

            float innerRadius = Mathf.Max(0f, radius - width);
            List<Vector2> inner = BuildContour(innerRect, innerRadius, outer.Count > 4);
            AddBorder(vertexHelper, outer, inner, borderColor);
            AddFilledContour(vertexHelper, inner, innerRect.center, color);
        }

        protected override void OnRectTransformDimensionsChange()
        {
            base.OnRectTransformDimensionsChange();
            SetVerticesDirty();
        }

        protected override void OnValidate()
        {
            cornerRadius = Mathf.Max(0f, cornerRadius);
            borderWidth = Mathf.Max(0f, borderWidth);
            base.OnValidate();
            SetVerticesDirty();
        }

        private static List<Vector2> BuildContour(Rect rect, float radius, bool preserveCornerSegments = false)
        {
            if (radius <= 0f && !preserveCornerSegments)
            {
                return new List<Vector2>(4)
                {
                    new Vector2(rect.xMax, rect.yMin),
                    new Vector2(rect.xMax, rect.yMax),
                    new Vector2(rect.xMin, rect.yMax),
                    new Vector2(rect.xMin, rect.yMin)
                };
            }

            List<Vector2> points = new List<Vector2>(4 * (SegmentsPerCorner + 1));
            AddCorner(points, new Vector2(rect.xMax - radius, rect.yMin + radius), radius, -90f);
            AddCorner(points, new Vector2(rect.xMax - radius, rect.yMax - radius), radius, 0f);
            AddCorner(points, new Vector2(rect.xMin + radius, rect.yMax - radius), radius, 90f);
            AddCorner(points, new Vector2(rect.xMin + radius, rect.yMin + radius), radius, 180f);
            return points;
        }

        private static void AddCorner(ICollection<Vector2> points, Vector2 center, float radius, float startAngle)
        {
            for (int i = 0; i <= SegmentsPerCorner; i++)
            {
                float angle = (startAngle + 90f * i / SegmentsPerCorner) * Mathf.Deg2Rad;
                points.Add(center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius);
            }
        }

        private static void AddBorder(
            VertexHelper vertexHelper,
            IReadOnlyList<Vector2> outer,
            IReadOnlyList<Vector2> inner,
            Color32 color)
        {
            int count = outer.Count;
            for (int i = 0; i < count; i++)
            {
                AddVertex(vertexHelper, outer[i], color);
                AddVertex(vertexHelper, inner[i], color);
            }

            for (int i = 0; i < count; i++)
            {
                int next = (i + 1) % count;
                int outerCurrent = i * 2;
                int innerCurrent = outerCurrent + 1;
                int outerNext = next * 2;
                int innerNext = outerNext + 1;

                vertexHelper.AddTriangle(outerCurrent, outerNext, innerNext);
                vertexHelper.AddTriangle(outerCurrent, innerNext, innerCurrent);
            }
        }

        private static void AddFilledContour(
            VertexHelper vertexHelper,
            IReadOnlyList<Vector2> contour,
            Vector2 center,
            Color32 color)
        {
            int centerIndex = vertexHelper.currentVertCount;
            AddVertex(vertexHelper, center, color);
            int firstContourIndex = vertexHelper.currentVertCount;

            for (int i = 0; i < contour.Count; i++)
                AddVertex(vertexHelper, contour[i], color);

            for (int i = 0; i < contour.Count; i++)
            {
                int current = firstContourIndex + i;
                int next = firstContourIndex + (i + 1) % contour.Count;
                vertexHelper.AddTriangle(centerIndex, current, next);
            }
        }

        private static void AddVertex(VertexHelper vertexHelper, Vector2 position, Color32 color)
        {
            UIVertex vertex = UIVertex.simpleVert;
            vertex.position = position;
            vertex.color = color;
            vertex.uv0 = Vector2.zero;
            vertexHelper.AddVert(vertex);
        }
    }
}
