Shader "HzRP/HzRPSimpleSkybox" {

    Subshader {
        
        Pass {
        
            Cull Off
            ZWrite Off
        
            Tags {
                "Queue" = "Background"
                "RenderType" = "Background"
                "PreviewType" = "Skybox"
            }

            CGPROGRAM
        
            #pragma vertex SkyboxVertex
            #pragma fragment SkyboxFragment

            #include "HzRPCommon.cginc"

            struct VertexInput {
                float3 posOS : POSITION;
            };

            struct VertexOutput {
                float4 posCS : SV_POSITION;
                float3 dir : TEXCOORD0;
            };

            VertexOutput SkyboxVertex(VertexInput input) {
                VertexOutput output;
                // float3 rotated = RotateAroundYInDegrees(input.posOS, _GlobalEnvMapRotation);
                output.posCS =  UnityObjectToClipPos(input.posOS);
                output.dir = input.posOS.xyz;
                return output;
            }

            float4 SkyboxFragment(VertexOutput input) : SV_Target {
                /*
                float4 skybox = _GlobalEnvMapSpecular.SampleLevel(sampler_GlobalEnvMapSpecular, input.dir, _SkyboxMipLevel);
                skybox *= _GlobalEnvMapExposure;
                */

                float3 dir = normalize(input.dir);
                float3 skybox = SampleGlobalEnvMapSpecular(dir, _SkyboxMipLevel);
                return float4(skybox, 1.0f);
            }

            ENDCG
        }
    }
}