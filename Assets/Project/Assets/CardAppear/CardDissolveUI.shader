// 카드 UI 등장(Dissolve 복구) 연출용 UI 셰이더. CardAppearEffect가 사용합니다.
// - 정점 알파(Image.color.a)를 "드러난 정도"로 씁니다. (1 = 다 드러남, 0 = 다 타서 없음) 투명도로는 쓰지 않습니다.
// - Edge Only = 0 : 카드 테두리 Image용. 탄 부분은 clip으로 버리므로, Mask와 함께 쓰면 하위 UI도 같은 모양으로 잘립니다.
// - Edge Only = 1 : 카드 맨 위 오버레이 Image용. 타는 경계(Edge Color)만 그립니다.
// - Dissolve UV는 Image의 UV(스프라이트가 없으면 사각형 전체 0~1)를 씁니다.
Shader "Project/UI/CardDissolve"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1, 1, 1, 1)

        [Header(Dissolve)]
        _DissolveTex ("Dissolve Texture", 2D) = "white" {}
        [HDR] _DissolveEdgeColor ("Dissolve Edge Color", Color) = (5.992157, 3.073262, 0, 1)
        _DissolveEdgeWidth ("Dissolve Edge Width", Range(0, 0.2)) = 0.07
        [IntRange] _EdgeOnly ("Edge Only", Range(0, 1)) = 0

        // UI Mask(스텐실) 호환용 - Mask/MaskableGraphic이 값을 채웁니다.
        [HideInInspector] _StencilComp ("Stencil Comparison", Float) = 8
        [HideInInspector] _Stencil ("Stencil ID", Float) = 0
        [HideInInspector] _StencilOp ("Stencil Operation", Float) = 0
        [HideInInspector] _StencilWriteMask ("Stencil Write Mask", Float) = 255
        [HideInInspector] _StencilReadMask ("Stencil Read Mask", Float) = 255
        [HideInInspector] _ColorMask ("Color Mask", Float) = 15
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Transparent"
            "IgnoreProjector" = "True"
            "RenderType" = "Transparent"
            "PreviewType" = "Plane"
            "CanUseSpriteAtlas" = "True"
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
                float4 worldPosition : TEXCOORD1;
                float2 dissolveUV : TEXCOORD2;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            sampler2D _MainTex;
            float4 _MainTex_ST;
            fixed4 _Color;
            fixed4 _TextureSampleAdd;
            float4 _ClipRect;

            sampler2D _DissolveTex;
            float4 _DissolveTex_ST;
            half4 _DissolveEdgeColor;
            half _DissolveEdgeWidth;
            half _EdgeOnly;

            v2f vert(appdata_t v)
            {
                v2f OUT;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(OUT);
                OUT.worldPosition = v.vertex;
                OUT.vertex = UnityObjectToClipPos(v.vertex);
                OUT.texcoord = TRANSFORM_TEX(v.texcoord, _MainTex);
                OUT.dissolveUV = TRANSFORM_TEX(v.texcoord, _DissolveTex);
                OUT.color = v.color * _Color;
                return OUT;
            }

            fixed4 frag(v2f IN) : SV_Target
            {
                // 드러난 정도(정점 알파) → 타서 없어진 양. 경계 폭만큼 더 밀어서 0일 때 경계가 남지 않게 합니다.
                half amount = 1.0 - IN.color.a;
                half threshold = amount * (1.0 + _DissolveEdgeWidth + 0.001) - _DissolveEdgeWidth;
                half d = tex2D(_DissolveTex, IN.dissolveUV).r;

                // 탄 부분은 버립니다. (Mask가 스텐실을 쓰지 않으므로 하위 UI도 여기서 잘립니다)
                clip(d - threshold);

                // 경계: threshold ~ threshold + 폭
                half edge = 1.0 - step(threshold + _DissolveEdgeWidth, d);

                fixed4 body = (tex2D(_MainTex, IN.texcoord) + _TextureSampleAdd) * fixed4(IN.color.rgb, 1.0);
                fixed4 color = _EdgeOnly > 0.5
                    ? half4(_DissolveEdgeColor.rgb, _DissolveEdgeColor.a * edge)
                    : body;

                #ifdef UNITY_UI_CLIP_RECT
                color.a *= UnityGet2DClipping(IN.worldPosition.xy, _ClipRect);
                #endif

                // 경계 오버레이는 경계 밖에서 아무것도 남기지 않습니다.
                clip(color.a - 0.001);
                return color;
            }
            ENDCG
        }
    }
}
