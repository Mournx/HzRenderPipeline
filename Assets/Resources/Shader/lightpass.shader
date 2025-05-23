Shader "HzRP/lightpass"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
    }
    SubShader
    {
        Cull Off ZWrite On ZTest Always

        Pass
        {
            Tags {"LightMode" = "ForwardBase"}
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            
            #include "UnityLightingCommon.cginc"
            #include "HzRPCommon.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float2 uv : TEXCOORD0;
                float4 vertex : SV_POSITION;
            };

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                return o;
            }           

            fixed4 frag(v2f i, out float depthOut : SV_Depth) : SV_Target
            {
                float2 uv = i.uv;
                float4 GT2 = tex2D(_GT2, uv);
                float4 GT3 = tex2D(_GT3, uv);

                float3 albedo = tex2D(_GT0, uv).rgb;
                float3 normal = SampleNormalWS(uv);
                float2 motionVec = GT2.rg;
                float linearRoughness = GT2.b;
                float metallic = GT2.a;
                float3 emission = GT3.rgb;
                float occlusion = GT3.a;

                float d = UNITY_SAMPLE_DEPTH(tex2D(_gdepth, uv));
                float d_lin = Linear01Depth(d);
                depthOut = d;

                //Reconstruct world pos
                float4 ndcPos = float4(uv * 2 - 1, d, 1);
                float4 worldPos = mul(_vpMatrixInv, ndcPos);
                worldPos /= worldPos.w;

                float3 color = float3(0, 0, 0);
                float3 N = normalize(normal);
                float3 L = normalize(_WorldSpaceLightPos0.xyz);
                float3 V = normalize(_WorldSpaceCameraPos.xyz - worldPos.xyz);
                float3 radiance = _LightColor0.rgb;

                float3 R = reflect(-V, N);
                float iblOcclusion = ComputeHorizonSpecularOcclusion(R, normal);

                float3 direct = PBR(N, V, L, albedo, radiance, linearRoughness, metallic);
                float3 ambient = IBL(N, V, albedo, linearRoughness, metallic);
                
                color += ambient * min(occlusion, iblOcclusion);
                color += emission;

                //shadow
                float shadow = tex2D(_shadowStrength, uv).r;
                color += direct * shadow ;

                //return float4(color, 1);

                //Cluster Based Lighting
                uint x = floor(uv.x * _numClusterX);
                uint y = floor(uv.y * _numClusterY);
                uint z = floor((1 - d_lin) * _numClusterZ); // z 是反的

                uint clusterId = Index3DTo1D(uint3(x, y, z));
                LightIndex lightIndex = _assignTable[clusterId];

                int start = lightIndex.start;
                int end = lightIndex.start + lightIndex.count;
                for(int j = start; j < end; j++)
                {
                    uint lightId = _lightAssignBuffer[j];
                    PointLight light = _lightBuffer[lightId];

                    L = normalize(light.position - worldPos.xyz);
                    radiance = light.color;

                    float dis = distance(light.position, worldPos.xyz);
                    float d2 = dis * dis;
                    float r2 = light.radius * light.radius;
                    float attenuation = saturate(1 - (d2 / r2) * (d2 / r2));
                    attenuation *= attenuation;

                    color += PBR(N, V, L, albedo, radiance, linearRoughness, metallic) * light.intensity * attenuation;
                }

                return float4(color, 1);
            }
            ENDCG
        }
    }
}
