using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace AlchemyTower
{
    [ExecuteAlways]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Image))]
    [AddComponentMenu("UI (Canvas)/Rounded Corners", 13)]
    public sealed class RoundedCorners : UIBehaviour, IMaterialModifier
    {
        private const string ShaderResourcePath = "AlchemyTowerRoundedCorners";

        private static readonly int RectProperty = Shader.PropertyToID("_RoundedRect");
        private static readonly int RadiiProperty = Shader.PropertyToID("_CornerRadii");
        private static readonly int BorderWidthProperty = Shader.PropertyToID("_BorderWidth");
        private static readonly int BorderColorProperty = Shader.PropertyToID("_BorderColor");
        private static readonly int EdgeSoftnessProperty = Shader.PropertyToID("_EdgeSoftness");

        [SerializeField, Min(0f)] private float cornerRadius = 16f;
        [SerializeField] private bool useIndividualCorners;
        [SerializeField, Min(0f)] private float topLeftRadius = 16f;
        [SerializeField, Min(0f)] private float topRightRadius = 16f;
        [SerializeField, Min(0f)] private float bottomRightRadius = 16f;
        [SerializeField, Min(0f)] private float bottomLeftRadius = 16f;
        [SerializeField, Min(0f)] private float borderWidth;
        [SerializeField] private Color borderColor = Color.clear;
        [SerializeField, Min(0f)] private float edgeSoftness = 1f;

        private Image image;
        private Material modifiedMaterial;
        private Material sourceMaterial;
        private Rect lastRenderedRect;
        private bool hasLastRenderedRect;
        private static Shader roundedShader;
        private static bool missingShaderLogged;

        public float CornerRadius
        {
            get => cornerRadius;
            set => SetFloat(ref cornerRadius, value);
        }

        public bool UseIndividualCorners
        {
            get => useIndividualCorners;
            set
            {
                if (useIndividualCorners == value)
                    return;

                useIndividualCorners = value;
                InvalidateMaterial();
            }
        }

        public float TopLeftRadius
        {
            get => topLeftRadius;
            set => SetFloat(ref topLeftRadius, value);
        }

        public float TopRightRadius
        {
            get => topRightRadius;
            set => SetFloat(ref topRightRadius, value);
        }

        public float BottomRightRadius
        {
            get => bottomRightRadius;
            set => SetFloat(ref bottomRightRadius, value);
        }

        public float BottomLeftRadius
        {
            get => bottomLeftRadius;
            set => SetFloat(ref bottomLeftRadius, value);
        }

        public float BorderWidth
        {
            get => borderWidth;
            set => SetFloat(ref borderWidth, value);
        }

        public Color BorderColor
        {
            get => borderColor;
            set
            {
                if (borderColor == value)
                    return;

                borderColor = value;
                InvalidateMaterial();
            }
        }

        public float EdgeSoftness
        {
            get => edgeSoftness;
            set => SetFloat(ref edgeSoftness, value);
        }

        public Material GetModifiedMaterial(Material baseMaterial)
        {
            if (!isActiveAndEnabled || baseMaterial == null)
                return baseMaterial;

            Shader shader = GetRoundedShader();
            if (shader == null)
                return baseMaterial;

            if (modifiedMaterial == null || sourceMaterial != baseMaterial)
            {
                ReleaseMaterial();
                modifiedMaterial = new Material(shader)
                {
                    name = $"{baseMaterial.name} (Rounded Corners)",
                    hideFlags = HideFlags.HideAndDontSave
                };
                modifiedMaterial.CopyPropertiesFromMaterial(baseMaterial);
                modifiedMaterial.shader = shader;
                sourceMaterial = baseMaterial;
            }

            ApplyProperties(modifiedMaterial);
            return modifiedMaterial;
        }

        public Vector4 GetEffectiveCornerRadii()
        {
            Rect rect = GetRenderedRect();
            float maximum = Mathf.Max(0f, Mathf.Min(rect.width, rect.height) * 0.5f);
            if (!useIndividualCorners)
            {
                float radius = Mathf.Min(cornerRadius, maximum);
                return new Vector4(radius, radius, radius, radius);
            }

            return new Vector4(
                Mathf.Min(topLeftRadius, maximum),
                Mathf.Min(topRightRadius, maximum),
                Mathf.Min(bottomRightRadius, maximum),
                Mathf.Min(bottomLeftRadius, maximum));
        }

        protected override void OnEnable()
        {
            base.OnEnable();
            GetImage().SetMaterialDirty();
        }

        protected override void OnDisable()
        {
            ReleaseMaterial();
            if (image != null)
                image.SetMaterialDirty();
            base.OnDisable();
        }

        protected override void OnDestroy()
        {
            ReleaseMaterial();
            base.OnDestroy();
        }

        protected override void OnRectTransformDimensionsChange()
        {
            base.OnRectTransformDimensionsChange();
            InvalidateMaterial();
        }

        private void LateUpdate()
        {
            if (modifiedMaterial == null)
                return;

            Rect renderedRect = GetRenderedRect();
            if (!hasLastRenderedRect || renderedRect != lastRenderedRect)
                ApplyProperties(modifiedMaterial);
        }

        protected override void OnDidApplyAnimationProperties()
        {
            base.OnDidApplyAnimationProperties();
            InvalidateMaterial();
        }

#if UNITY_EDITOR
        protected override void OnValidate()
        {
            cornerRadius = Mathf.Max(0f, cornerRadius);
            topLeftRadius = Mathf.Max(0f, topLeftRadius);
            topRightRadius = Mathf.Max(0f, topRightRadius);
            bottomRightRadius = Mathf.Max(0f, bottomRightRadius);
            bottomLeftRadius = Mathf.Max(0f, bottomLeftRadius);
            borderWidth = Mathf.Max(0f, borderWidth);
            edgeSoftness = Mathf.Max(0f, edgeSoftness);
            base.OnValidate();
            InvalidateMaterial();
        }
#endif

        private Image GetImage()
        {
            if (image == null)
                image = GetComponent<Image>();
            return image;
        }

        private static Shader GetRoundedShader()
        {
            if (roundedShader == null)
                roundedShader = Resources.Load<Shader>(ShaderResourcePath);

            if (roundedShader == null && !missingShaderLogged)
            {
                missingShaderLogged = true;
                Debug.LogError(
                    $"RoundedCorners shader resource '{ShaderResourcePath}' was not found. The original Image will be rendered unchanged.");
            }

            return roundedShader;
        }

        private void ApplyProperties(Material material)
        {
            Rect rect = GetRenderedRect();
            float maximum = Mathf.Max(0f, Mathf.Min(rect.width, rect.height) * 0.5f);
            material.SetVector(RectProperty, new Vector4(rect.xMin, rect.yMin, rect.xMax, rect.yMax));
            material.SetVector(RadiiProperty, GetEffectiveCornerRadii());
            material.SetFloat(BorderWidthProperty, Mathf.Min(borderWidth, maximum));
            material.SetColor(BorderColorProperty, borderColor);
            material.SetFloat(EdgeSoftnessProperty, Mathf.Min(edgeSoftness, maximum));
            lastRenderedRect = rect;
            hasLastRenderedRect = true;
        }

        private Rect GetRenderedRect()
        {
            Image targetImage = GetImage();
            Rect rect = targetImage.GetPixelAdjustedRect();
            Sprite activeSprite = targetImage.overrideSprite;
            bool usesPreservedDrawingRect = activeSprite != null
                && targetImage.preserveAspect
                && (targetImage.type == Image.Type.Simple || targetImage.type == Image.Type.Filled);
            if (usesPreservedDrawingRect)
            {
                Vector2 spriteSize = activeSprite.rect.size;
                if (spriteSize.sqrMagnitude > 0f && rect.width > 0f && rect.height > 0f)
                {
                    float spriteRatio = spriteSize.x / spriteSize.y;
                    float rectRatio = rect.width / rect.height;
                    if (spriteRatio > rectRatio)
                    {
                        float oldHeight = rect.height;
                        rect.height = rect.width / spriteRatio;
                        rect.y += (oldHeight - rect.height) * targetImage.rectTransform.pivot.y;
                    }
                    else
                    {
                        float oldWidth = rect.width;
                        rect.width = rect.height * spriteRatio;
                        rect.x += (oldWidth - rect.width) * targetImage.rectTransform.pivot.x;
                    }
                }
            }

            if (activeSprite != null && targetImage.type == Image.Type.Filled)
            {
                float amount = targetImage.fillAmount;
                if (targetImage.fillMethod == Image.FillMethod.Horizontal)
                {
                    float oldWidth = rect.width;
                    rect.width *= amount;
                    if (targetImage.fillOrigin == (int)Image.OriginHorizontal.Right)
                        rect.x += oldWidth - rect.width;
                }
                else if (targetImage.fillMethod == Image.FillMethod.Vertical)
                {
                    float oldHeight = rect.height;
                    rect.height *= amount;
                    if (targetImage.fillOrigin == (int)Image.OriginVertical.Top)
                        rect.y += oldHeight - rect.height;
                }
            }

            return rect;
        }

        private void SetFloat(ref float field, float value)
        {
            float clamped = Mathf.Max(0f, value);
            if (Mathf.Approximately(field, clamped))
                return;

            field = clamped;
            InvalidateMaterial();
        }

        private void InvalidateMaterial()
        {
            hasLastRenderedRect = false;
            if (modifiedMaterial != null)
                ApplyProperties(modifiedMaterial);
            if (isActiveAndEnabled && image != null)
                image.SetMaterialDirty();
        }

        private void ReleaseMaterial()
        {
            sourceMaterial = null;
            hasLastRenderedRect = false;
            if (modifiedMaterial == null)
                return;

            if (Application.isPlaying)
                Destroy(modifiedMaterial);
            else
                DestroyImmediate(modifiedMaterial);
            modifiedMaterial = null;
        }
    }
}
