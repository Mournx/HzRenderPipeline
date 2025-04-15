Shader "HzRP/gbuffer"
{
    Properties
    {
        _MainTex ("Albedo Map", 2D) = "white" {}
        _AlbedoTint("Albedo Tint", Color) = (1,1,1,1)
        
        [NoScaleOffset]
        [Normal] _BumpMap ("Normal Map", 2D) = "bump" {}
        _MetallicGlossMap ("Metallic Map", 2D) = "white" {}
        _Metallic_global ("Metallic", Range(0, 1)) = 0.5
        _Smoothness_global ("Smoothness", Range(0, 1)) = 0.5
        _OcclusionMap ("Occlusion Map", 2D) = "white" {}
        
        [HDR]
        _EmissionTint("Emissive Tint", Color) = (0,0,0,1)
        _EmissionMap ("Emission Map", 2D) = "black" {}
    }
    SubShader
    {
        Pass
        {
            Tags {"LightMode"="depthOnly"}
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

           #include "HzRPCommon.cginc"

            struct v2f
            {
                float4 vertex : SV_POSITION;
                float2 depth : TEXCOORD0;
            };

            v2f vert(appdata_base v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.depth = o.vertex.zw;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float d = i.depth.x / i.depth.y;
            #if defined(UNITY_REVERSED_Z)
            d = 1.0 - d;
            #endif
                fixed4 c = EncodeFloatRGBA(d);
                return c;
            }
            ENDHLSL
        }
        
        Pass
        {
            Tags { "LightMode"="gbuffer" }

            HLSLPROGRAM
            #pragma multi_compile_instancing
            #pragma vertex vert
            #pragma fragment frag

            #include "HzRPCommon.cginc"
            
            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
                float3 normal : NORMAL;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float2 uv : TEXCOORD0;
                float4 vertex : SV_POSITION;
                float3 normal : NORMAL;
                float4 preposCS : POSITION1;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };
            
            sampler2D _MainTex;
            sampler2D _MetallicGlossMap;
            sampler2D _EmissionMap;
            sampler2D _OcclusionMap;
            sampler2D _BumpMap;
            
            UNITY_INSTANCING_BUFFER_START(UnityPerMaterial)
                UNITY_DEFINE_INSTANCED_PROP(float, _Metallic_global)
                UNITY_DEFINE_INSTANCED_PROP(float, _Smoothness_global)
                UNITY_DEFINE_INSTANCED_PROP(float4, _EmissionTint)
                UNITY_DEFINE_INSTANCED_PROP(float4, _AlbedoTint)
                UNITY_DEFINE_INSTANCED_PROP(float4, _MainTex_ST)
            UNITY_INSTANCING_BUFFER_END(UnityPerMaterial)
            
            v2f vert (appdata v)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_TRANSFER_INSTANCE_ID(v,o);
                
                o.vertex = UnityObjectToClipPos(v.vertex);
                float4 albedoST = UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _MainTex_ST);
                o.uv = v.uv * albedoST.xy + albedoST.zw;
                o.normal = UnityObjectToWorldNormal(v.normal);
                o.preposCS = mul(_vpMatrixPrev, v.vertex);
                
                return o;
            }

            void frag(v2f i, out float4 GT0 : SV_Target0, out float4 GT1 : SV_Target1, out float4 GT2 : SV_Target2, out float4 GT3 : SV_Target3) 
            {
                UNITY_SETUP_INSTANCE_ID(i);
                
                float4 color = tex2D(_MainTex, i.uv);
                color *= UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _AlbedoTint);
                float3 emission = tex2D(_EmissionMap, i.uv).rgb;
                emission += UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _EmissionTint).rgb;
                float3 normal = i.normal;
                float ao = tex2D(_OcclusionMap, i.uv).g;
                
                
                float4 metallicSmoothness = tex2D(_MetallicGlossMap, i.uv);
                float linearSmoothness = metallicSmoothness.a;
                linearSmoothness *= UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _Smoothness_global);
                float linearRoughness = LinearSmoothnessToLinearRoughness(linearSmoothness);
                linearRoughness = ClampMinLinearRoughness(linearRoughness);
                
                float metallic = metallicSmoothness.r;
                metallic *= UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _Metallic_global);
                //if(_Use_Normal_Map) normal = UnpackNormal(tex2D(_BumpMap, i.uv));

                float2 motionVec = CalculateMotionVector(i.vertex, i.preposCS);
                GT0 = color;
                GT1 = float4(normal*0.5+0.5, 0);
                GT2 = float4(motionVec, linearRoughness, metallic);
                GT3 = float4(emission, ao);
            }
            ENDHLSL
        }
    }
}
