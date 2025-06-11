Shader "Custom/BinaryLitShader"
{
    Properties
    {
        _BaseMap("Albedo", 2D) = "white" {}
        _ShadowColor("Shadow Color", Color) = (0, 0, 0, 1)
        _LightColor("Light Color", Color) = (1, 1, 1, 1)
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" }
        LOD 100

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                float3 normalOS : NORMAL;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                float3 posWS : TEXCOORD2;
                float4 shadowCoords : TEXCOORD3;
            };

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);
            float4 _BaseMap_ST;

            float4 _ShadowColor;
            float4 _LightColor;

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                VertexPositionInputs posInputs = GetVertexPositionInputs(IN.positionOS.xyz);
                OUT.positionHCS = posInputs.positionCS;
                OUT.uv = TRANSFORM_TEX(IN.uv, _BaseMap);
                OUT.normalWS = TransformObjectToWorldNormal(IN.normalOS);
                OUT.posWS = TransformObjectToWorld(IN.positionOS).xyz;
                OUT.shadowCoords = GetShadowCoord(posInputs);
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                float3 normalWS = normalize(IN.normalWS);
                float3 lightDir = normalize(_MainLightPosition.xyz);

                Light mainLight = GetMainLight(IN.shadowCoords);

                // Determine if front-facing and not in shadow
                float NdotL = dot(normalWS, mainLight.direction);

                // Binary shadow check
                bool lit = NdotL > 0.0 && mainLight.shadowAttenuation > 0.5;

                float4 albedo = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, IN.uv);
                float4 lightMask = lit ? _LightColor : _ShadowColor;

                return albedo * lightMask;
            }
            ENDHLSL
        }
        // shadow casting support
        UsePass "Legacy Shaders/VertexLit/SHADOWCASTER"
    }
    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}
