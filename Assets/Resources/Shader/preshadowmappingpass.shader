Shader "HzRP/preshadowmappingpass"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
    }
    SubShader
    {
        Cull Off ZWrite Off ZTest Always

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag

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

            float frag (v2f i) : SV_Target
            {
                float sum = 0;
                float2 uv = i.uv;
                for(float i = -1.5; i <= 1.51; i++)
                {
                    for(float j = -1.5; j <= 1.51; j++)
                    {
                        float2 offset = float2(i, j) / float2(_screenWidth, _screenHeight);
                        float2 uv_Offset = uv + offset;
                        float3 normal = SampleNormalWS(uv_Offset);
                        float d = UNITY_SAMPLE_DEPTH(tex2D(_gdepth, uv_Offset));
                        float d_lin = Linear01Depth(d);

                        //反投影重建世界坐标
                        float4 ndcPos = float4(uv_Offset * 2 - 1, d, 1);
                        float4 worldPos = mul(_vpMatrixInv, ndcPos);
                        worldPos /= worldPos.w;

                        //向着法线偏移采样点
                        float4 worldPosOffset = worldPos;

                        float shadow = 1.0;
                        float csmLevel = d_lin * (_far - _near) * _csmMaxDistance;
                        if(csmLevel < _split0)
                        {
                            worldPosOffset.xyz += normal * _shadingPointNormalBias0;
                            float bias = ( 1 * _orthoWidth0 / _shadowMapResolution) * _depthNormalBias0;
                            shadow *= shadowMap01(worldPosOffset, _shadowtex0, _shadowVpMatrix0, bias);
                        }
                        else if(csmLevel < _split0 + _split1)
                        {
                            worldPosOffset.xyz += normal * _shadingPointNormalBias1;
                            float bias = ( 1 * _orthoWidth1 / _shadowMapResolution) * _depthNormalBias1;
                            shadow *= shadowMap01(worldPosOffset, _shadowtex1, _shadowVpMatrix1, bias);
                        }
                        else if(csmLevel < _split0 + _split1 + _split2)
                        {
                            worldPosOffset.xyz += normal * _shadingPointNormalBias2;
                            float bias = ( 1 * _orthoWidth2 / _shadowMapResolution) * _depthNormalBias2;
                            shadow *= shadowMap01(worldPosOffset, _shadowtex2, _shadowVpMatrix2, bias);
                        }
                        else if(csmLevel < _split0 + _split1 + _split2 + _split3)
                        {
                            worldPosOffset.xyz += normal * _shadingPointNormalBias3;
                            float bias = ( 1 * _orthoWidth3 / _shadowMapResolution) * _depthNormalBias3;
                            shadow *= shadowMap01(worldPosOffset, _shadowtex3, _shadowVpMatrix3, bias);
                        }
                        sum += shadow;
                    }
                }
                return sum / 16;
            }
            ENDCG
        }
    }
}
