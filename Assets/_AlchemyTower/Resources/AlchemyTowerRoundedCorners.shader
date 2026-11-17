Shader "AlchemyTower/UI/Rounded Corners"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)

        _StencilComp ("Stencil Comparison", Float) = 8
        _Stencil ("Stencil ID", Float) = 0
        _StencilOp ("Stencil Operation", Float) = 0
        _StencilWriteMask ("Stencil Write Mask", Float) = 255
        _StencilReadMask ("Stencil Read Mask", Float) = 255
        _ColorMask ("Color Mask", Float) = 15
        [Toggle(UNITY_UI_ALPHACLIP)] _UseUIAlphaClip ("Use Alpha Clip", Float) = 0

        [HideInInspector] _RoundedRect ("Rounded Rect", Vector) = (0,0,100,100)
        [HideInInspector] _CornerRadii ("Corner Radii TL TR BR BL", Vector) = (0,0,0,0)
        [HideInInspector] _BorderWidth ("Border Width", Float) = 0
        [HideInInspector] _BorderColor ("Border Color", Color) = (0,0,0,0)
        [HideInInspector] _EdgeSoftness ("Edge Softness", Float) = 1
    }

    SubShader
    {
        Tags
        {
            "Queue"="Transparent"
            "IgnoreProjector"="True"
            "RenderType"="Transparent"
            "PreviewType"="Plane"
            "CanUseSpriteAtlas"="True"
        }

        Stencil
        {
            Ref [_Stencil]
            Comp [_StencilComp]
            Pass [_StencilOp]
            ReadMask [_StencilReadMask]
            WriteMask [_StencilWriteMask]
        }

        Cull Off
        Lighting Off
        ZWrite Off
        ZTest [unity_GUIZTestMode]
        Blend SrcAlpha OneMinusSrcAlpha
        ColorMask [_ColorMask]

        Pass
        {
            Name "Default"
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 2.0

            #include "UnityCG.cginc"
            #include "UnityUI.cginc"

            #pragma multi_compile_local _ UNITY_UI_CLIP_RECT
            #pragma multi_compile_local _ UNITY_UI_ALPHACLIP

            struct appdata_t
            {
                float4 vertex : POSITION;
                float4 color : COLOR;
                float2 texcoord : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                fixed4 color : COLOR;
                float2 texcoord : TEXCOORD0;
                float2 localPosition : TEXCOORD1;
                float4 worldPosition : TEXCOORD2;
                half4 mask : TEXCOORD3;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            sampler2D _MainTex;
            fixed4 _Color;
            fixed4 _TextureSampleAdd;
            float4 _ClipRect;
            float _UIMaskSoftnessX;
            float _UIMaskSoftnessY;
            float4 _RoundedRect;
            float4 _CornerRadii;
            float _BorderWidth;
            fixed4 _BorderColor;
            float _EdgeSoftness;

            v2f vert(appdata_t v)
            {
                v2f OUT;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(OUT);
                OUT.worldPosition = v.vertex;
                OUT.localPosition = v.vertex.xy;
                OUT.vertex = UnityObjectToClipPos(v.vertex);
                OUT.texcoord = v.texcoord;
                OUT.color = v.color * _Color;

                float2 pixelSize = OUT.vertex.w;
                pixelSize /= abs(mul((float2x2)UNITY_MATRIX_P, _ScreenParams.xy));
                float4 clampedRect = clamp(_ClipRect, -2e10, 2e10);
                OUT.mask = half4(
                    v.vertex.xy * 2.0 - clampedRect.xy - clampedRect.zw,
                    0.25 / (0.25 * half2(_UIMaskSoftnessX, _UIMaskSoftnessY) + abs(pixelSize.xy)));
                return OUT;
            }

            float SelectRadius(float2 positionFromCenter, float4 radii)
            {
                if (positionFromCenter.x >= 0.0)
                    return positionFromCenter.y >= 0.0 ? radii.y : radii.z;
                return positionFromCenter.y >= 0.0 ? radii.x : radii.w;
            }

            float RoundedRectDistance(float2 localPosition, float4 rect, float4 radii)
            {
                float2 center = (rect.xy + rect.zw) * 0.5;
                float2 halfSize = max((rect.zw - rect.xy) * 0.5, 0.0);
                float2 positionFromCenter = localPosition - center;
                float radius = SelectRadius(positionFromCenter, radii);
                float2 q = abs(positionFromCenter) - halfSize + radius;
                return min(max(q.x, q.y), 0.0) + length(max(q, 0.0)) - radius;
            }

            fixed4 frag(v2f IN) : SV_Target
            {
                fixed4 sampled = (tex2D(_MainTex, IN.texcoord) + _TextureSampleAdd) * IN.color;

                float outerDistance = RoundedRectDistance(IN.localPosition, _RoundedRect, _CornerRadii);
                float antialias = max(fwidth(outerDistance) * 0.5, max(_EdgeSoftness, 0.0001));
                float outerCoverage = 1.0 - smoothstep(-antialias, antialias, outerDistance);

                float2 innerMin = _RoundedRect.xy + _BorderWidth;
                float2 innerMax = _RoundedRect.zw - _BorderWidth;
                float innerExists = step(innerMin.x, innerMax.x) * step(innerMin.y, innerMax.y);
                float4 innerRect = float4(innerMin, innerMax);
                float4 innerRadii = max(_CornerRadii - _BorderWidth, 0.0);
                float innerDistance = RoundedRectDistance(IN.localPosition, innerRect, innerRadii);
                float innerCoverage = (1.0 - smoothstep(-antialias, antialias, innerDistance)) * innerExists;
                float borderCoverage = saturate(outerCoverage - innerCoverage) * step(0.0001, _BorderWidth);

                // Image.color alpha remains the overall Graphic opacity, including the border.
                // This keeps tab selection fades and Button ColorTint transitions predictable.
                float borderBlend = saturate(borderCoverage * _BorderColor.a * IN.color.a);
                sampled.rgb = lerp(sampled.rgb, _BorderColor.rgb, borderBlend);
                sampled.a = max(sampled.a * outerCoverage, borderBlend);

                #ifdef UNITY_UI_CLIP_RECT
                half2 mask = saturate(
                    (_ClipRect.zw - _ClipRect.xy - abs(IN.mask.xy)) * IN.mask.zw);
                sampled.a *= mask.x * mask.y;
                #endif

                #ifdef UNITY_UI_ALPHACLIP
                clip(sampled.a - 0.001);
                #endif

                return sampled;
            }
            ENDCG
        }
    }
}
