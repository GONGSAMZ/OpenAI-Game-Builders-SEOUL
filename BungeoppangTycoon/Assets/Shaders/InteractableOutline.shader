Shader "Bungeoppang/Interactable Outline"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _OutlineWidth ("Outline Width", Range(0.5, 4)) = 1.5

        // SpriteRenderer.color가 이 색과 알파를 전달한다.
        [HideInInspector] _Color ("Tint", Color) = (1,1,1,1)
        [HideInInspector] _RendererColor ("RendererColor", Color) = (1,1,1,1)
        [HideInInspector] _SpriteUVRect ("Sprite UV Rect", Vector) = (0,0,1,1)
        [HideInInspector] _AlphaTex ("External Alpha", 2D) = "white" {}
        [HideInInspector] _EnableExternalAlpha ("Enable External Alpha", Float) = 0
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Transparent"
            "RenderType" = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
            "CanUseSpriteAtlas" = "True"
        }

        Blend SrcAlpha OneMinusSrcAlpha, One OneMinusSrcAlpha
        Cull Off
        ZWrite Off

        Pass
        {
            HLSLPROGRAM
            #include "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/Core2D.hlsl"

            #pragma vertex OutlineVertex
            #pragma fragment OutlineFragment
            #pragma multi_compile_instancing
            #pragma multi_compile _ DEBUG_DISPLAY SKINNED_SPRITE

            struct Attributes
            {
                COMMON_2D_INPUTS
                half4 color : COLOR;
                UNITY_SKINNED_VERTEX_INPUTS
            };

            struct Varyings
            {
                COMMON_2D_OUTPUTS
                half4 color : COLOR;
            };

            #include "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/2DCommon.hlsl"

            float4 _MainTex_TexelSize;

            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                float _OutlineWidth;
                float4 _SpriteUVRect;
            CBUFFER_END

            Varyings OutlineVertex(Attributes input)
            {
                UNITY_SKINNED_VERTEX_COMPUTE(input);
                SetUpSpriteInstanceProperties();
                input.positionOS = UnityFlipSprite(input.positionOS, unity_SpriteProps.xy);

                Varyings output = CommonUnlitVertex(input);
                output.color = input.color * _Color * unity_SpriteColor;
                return output;
            }

            half SampleAlpha(float2 uv)
            {
                return SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv).a;
            }

            half SampleAlphaInsideSprite(float2 uv)
            {
                // 한 파일에 든 다른 표정 조각까지 외곽선으로 읽지 않도록 현재 조각 내부로 제한한다.
                float2 halfTexel = _MainTex_TexelSize.xy * 0.5;
                float2 minimum = _SpriteUVRect.xy + halfTexel;
                float2 maximum = _SpriteUVRect.zw - halfTexel;
                return SampleAlpha(clamp(uv, minimum, maximum));
            }

            half4 OutlineFragment(Varyings input) : SV_Target
            {
                half sourceAlpha = SampleAlpha(input.uv);
                float2 offset = _MainTex_TexelSize.xy * _OutlineWidth;

                // 원본 픽셀 밖에서만 주변 알파를 읽어 실제 테두리만 남긴다.
                half neighbourAlpha = 0;
                neighbourAlpha = max(neighbourAlpha, SampleAlphaInsideSprite(input.uv + float2( offset.x, 0)));
                neighbourAlpha = max(neighbourAlpha, SampleAlphaInsideSprite(input.uv + float2(-offset.x, 0)));
                neighbourAlpha = max(neighbourAlpha, SampleAlphaInsideSprite(input.uv + float2(0,  offset.y)));
                neighbourAlpha = max(neighbourAlpha, SampleAlphaInsideSprite(input.uv + float2(0, -offset.y)));
                neighbourAlpha = max(neighbourAlpha, SampleAlphaInsideSprite(input.uv + float2( offset.x,  offset.y)));
                neighbourAlpha = max(neighbourAlpha, SampleAlphaInsideSprite(input.uv + float2(-offset.x,  offset.y)));
                neighbourAlpha = max(neighbourAlpha, SampleAlphaInsideSprite(input.uv + float2( offset.x, -offset.y)));
                neighbourAlpha = max(neighbourAlpha, SampleAlphaInsideSprite(input.uv + float2(-offset.x, -offset.y)));

                half outlineAlpha = saturate(neighbourAlpha - sourceAlpha) * input.color.a;
                return half4(input.color.rgb, outlineAlpha);
            }
            ENDHLSL
        }
    }
}
