// 화면 가장자리 연출용 UI 셰이더. ScreenEdgeEffect가 사용합니다.
// - 화면 전체를 덮는 Image 하나에 붙여 씁니다. 텍스처 없이 수식으로만 그립니다.
// - 속도선(달리기): 화면 가장자리에서 가운데를 향해 뻗는 가시 모양 선. 일정 간격마다 선 배치가 바뀌어 지글거립니다.
// - 피격(빨간 가장자리): 가장자리에 붙은 붉은 테두리. 붉은색과 검붉은색이 얼룩처럼 섞여 흐릅니다.
// - 두 효과의 세기(_SpeedAmount, _DamageAmount)와 시간(_EffectTime)은 스크립트가 매 프레임 넣어 줍니다.
//   (게임이 멈추면 스크립트가 시간을 멈추므로 선도 같이 멈춥니다)
Shader "Project/UI/ScreenEdgeEffect"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}

        [Header(Speed Lines)]
        _SpeedColor ("Speed Line Color", Color) = (1, 1, 1, 0.7)
        _SpeedAmount ("Speed Amount", Range(0, 1)) = 0
        _LineCount ("Line Count", Float) = 90
        _LineWidth ("Line Width", Range(0.05, 1)) = 0.35
        _LineDensity ("Line Density", Range(0, 1)) = 0.55
        _LineInner ("Line Inner Radius", Range(0, 1.5)) = 0.75
        _LineFlickerRate ("Line Flicker Rate", Float) = 14
        // 선마다 Speed Line Color ~ 이 색 사이에서 무작위로 고릅니다. (투명도는 Speed Line Color 것을 씀)
        _SpeedColor2 ("Speed Line Color 2", Color) = (0.72, 0.72, 0.75, 1)

        [Header(Damage)]
        _DamageColor ("Damage Color", Color) = (0.85, 0, 0, 0.85)
        _DamageAmount ("Damage Amount", Range(0, 1)) = 0
        _DamageInner ("Damage Inner Radius", Range(0, 1.5)) = 0.55
        // 붉은색(Damage Color)과 얼룩지듯 섞이는 검붉은색. 맨 바깥쪽일수록 이 색이 많아집니다. (투명도는 Damage Color 것을 씀)
        _DamageColor2 ("Damage Color 2 (Dark)", Color) = (0.25, 0, 0.02, 1)
        // Damage Inner Radius에 더해서 빨간 테두리를 바깥으로 밀어 냅니다.
        _DamageOuterOffset ("Damage Outer Offset", Range(0, 0.8)) = 0.3
        // 빨간 테두리 안쪽 경계가 울퉁불퉁한 정도
        _DamageEdgeWobble ("Damage Edge Wobble", Range(0, 0.4)) = 0.15
        // 두 색이 섞이는 얼룩의 크기(클수록 잘게)와 움직이는 속도
        _DamageNoiseScale ("Damage Noise Scale", Float) = 3
        _DamageNoiseSpeed ("Damage Noise Speed", Float) = 0.6

        [HideInInspector] _EffectTime ("Effect Time", Float) = 0

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
                UNITY_VERTEX_OUTPUT_STEREO
            };

            half4 _SpeedColor;
            half4 _SpeedColor2;
            half _SpeedAmount;
            float _LineCount;
            half _LineWidth;
            half _LineDensity;
            half _LineInner;
            float _LineFlickerRate;

            half4 _DamageColor;
            half4 _DamageColor2;
            half _DamageAmount;
            half _DamageInner;
            half _DamageOuterOffset;
            half _DamageEdgeWobble;
            float _DamageNoiseScale;
            float _DamageNoiseSpeed;

            float _EffectTime;

            v2f vert(appdata_t v)
            {
                v2f OUT;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(OUT);
                OUT.vertex = UnityObjectToClipPos(v.vertex);
                OUT.texcoord = v.texcoord;
                OUT.color = v.color;
                return OUT;
            }

            // 0~1 사이의 의사 난수
            float Hash(float n)
            {
                return frac(sin(n * 12.9898) * 43758.5453);
            }

            // 2D 좌표용 의사 난수
            float Hash2(float2 p)
            {
                return frac(sin(dot(p, float2(127.1, 311.7))) * 43758.5453);
            }

            // 부드럽게 이어지는 2D 노이즈(0~1). 격자 꼭짓점의 난수를 부드럽게 보간합니다.
            float ValueNoise(float2 p)
            {
                float2 cell = floor(p);
                float2 f = frac(p);
                float2 u = f * f * (3.0 - 2.0 * f);

                float a = Hash2(cell);
                float b = Hash2(cell + float2(1, 0));
                float c = Hash2(cell + float2(0, 1));
                float d = Hash2(cell + float2(1, 1));
                return lerp(lerp(a, b, u.x), lerp(c, d, u.x), u.y);
            }

            // 가장자리에서 가운데로 뻗는 가시 모양 선의 진하기(0~1)
            // - 화면을 각도로 _LineCount개 칸으로 나누고, 칸마다 선을 그릴지/길이/굵기/색을 난수로 정합니다.
            // - 선은 안쪽 끝이 뾰족하고 바깥으로 갈수록 굵어집니다.
            // - lineColor: 이 선의 색 (Speed Line Color ~ Speed Line Color 2 사이)
            half SpeedLines(float2 aspectPos, float edgeDistance, out half3 lineColor)
            {
                const float TWO_PI = 6.2831853;

                // 각도는 화면 비율을 맞춘 좌표로 구해야 선이 화면 가운데를 정확히 향합니다.
                float angle01 = atan2(aspectPos.y, aspectPos.x) / TWO_PI + 0.5;

                float slot = floor(angle01 * _LineCount);
                float inSlot = frac(angle01 * _LineCount);

                // 일정 시간마다 칸별 난수를 바꿔서 선이 깜빡이며 바뀌게 합니다.
                float flickerStep = floor(_EffectTime * _LineFlickerRate);
                float seed = slot * 7.31 + flickerStep * 13.17;
                float showRand = Hash(seed);
                float lengthRand = Hash(seed + 1.7);
                float widthRand = Hash(seed + 3.9);
                float colorRand = Hash(seed + 5.3);

                lineColor = lerp(_SpeedColor.rgb, _SpeedColor2.rgb, colorRand);

                if (showRand > _LineDensity)
                {
                    return 0;
                }

                // 선이 시작되는 안쪽 반지름. 달리는 세기가 클수록 더 안쪽까지 들어옵니다.
                float startRadius = _LineInner + lengthRand * 0.35 - _SpeedAmount * 0.15;
                float along = edgeDistance - startRadius;
                if (along <= 0)
                {
                    return 0;
                }

                // 안쪽 끝은 0, 바깥으로 갈수록 굵어지는 가시 모양
                float halfWidth = _LineWidth * (0.3 + widthRand * 0.7) * saturate(along * 2.5) * 0.5;
                float distToCenterLine = abs(inSlot - 0.5);
                half lineAlpha = 1.0 - smoothstep(halfWidth * 0.6, halfWidth, distToCenterLine);

                // 안쪽 끝은 부드럽게 사라지게 합니다.
                return lineAlpha * saturate(along * 4.0);
            }

            // 가장자리에 붙은 붉은 테두리의 진하기(0~1)
            // - 시작 위치 = Damage Inner Radius + Damage Outer Offset. 안쪽 경계는 노이즈로 울퉁불퉁하게 흔들립니다.
            // - edgeColor: 붉은색과 검붉은색이 얼룩처럼 섞인 색. 맨 바깥쪽일수록 검붉어집니다.
            half DamageEdge(float2 aspectPos, float edgeDistance, out half3 edgeColor)
            {
                float2 noisePos = aspectPos * _DamageNoiseScale;
                float noiseTime = _EffectTime * _DamageNoiseSpeed;

                // 두 겹의 노이즈를 서로 다른 방향으로 흘려서 겹칩니다.
                float noiseA = ValueNoise(noisePos + float2(noiseTime, -noiseTime * 0.7));
                float noiseB = ValueNoise(noisePos * 2.3 + float2(-noiseTime * 1.3, noiseTime));
                float mixNoise = noiseA * 0.6 + noiseB * 0.4;

                float startRadius = _DamageInner + _DamageOuterOffset + (mixNoise - 0.5) * _DamageEdgeWobble;
                half edge = smoothstep(startRadius, startRadius + 0.35, edgeDistance);

                // 얼룩(노이즈) + 바깥으로 갈수록 검붉게
                half darkAmount = saturate(mixNoise * 1.2 - 0.1 + smoothstep(1.0, 1.4, edgeDistance) * 0.5);
                edgeColor = lerp(_DamageColor.rgb, _DamageColor2.rgb, darkAmount);

                return edge;
            }

            fixed4 frag(v2f IN) : SV_Target
            {
                // 화면 가운데 = (0, 0), 상하좌우 가장자리 = 1, 모서리 = 약 1.41
                float2 centered = (IN.texcoord - 0.5) * 2.0;
                float edgeDistance = length(centered);
                float2 aspectPos = centered * float2(_ScreenParams.x / _ScreenParams.y, 1.0);

                half speedAlpha = 0;
                half3 speedRgb = _SpeedColor.rgb;
                if (_SpeedAmount > 0.001)
                {
                    speedAlpha = SpeedLines(aspectPos, edgeDistance, speedRgb) * _SpeedAmount * _SpeedColor.a;
                }

                half damageAlpha = 0;
                half3 damageRgb = _DamageColor.rgb;
                if (_DamageAmount > 0.001)
                {
                    damageAlpha = DamageEdge(aspectPos, edgeDistance, damageRgb) * _DamageAmount * _DamageColor.a;
                }

                // 두 효과가 겹치면 진한 쪽 색이 더 보이게 섞습니다.
                half totalAlpha = saturate(speedAlpha + damageAlpha);
                half3 rgb = (speedRgb * speedAlpha + damageRgb * damageAlpha) / max(speedAlpha + damageAlpha, 0.0001);

                fixed4 color = fixed4(rgb, totalAlpha * IN.color.a);
                clip(color.a - 0.001);
                return color;
            }
            ENDCG
        }
    }
}
