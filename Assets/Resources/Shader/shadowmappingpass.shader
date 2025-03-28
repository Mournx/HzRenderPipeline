Shader "HzRP/shadowmappingpass"
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
                float2 uv = i.uv;
                float3  normal = tex2D(_GT1, uv).rgb * 2 - 1;
                float d = UNITY_SAMPLE_DEPTH(tex2D(_gdepth, uv));
                float d_lin = Linear01Depth(d);

                float4 ndcPos = float4(uv * 2 - 1, d, 1);
                float4 worldPos = mul(_vpMatrixInv, ndcPos);
                worldPos /= worldPos.w;

                float4 worldPosOffset = worldPos;
                float3 lightDir = normalize(_WorldSpaceLightPos0.xyz);
                float NdotL = clamp(dot(lightDir, normal), 0, 1);

                if(NdotL < 0.005) return NdotL;

                //随机旋转角度
                float2 uv_noise = uv * float2(_screenWidth, _screenHeight) / _noiseTexResolution;
                float rotateAngle = tex2D(_noiseTex, uv_noise * 0.5).r * 2.0 * 3.1415926;

                //shadowmap Mask优化
                if(_usingShadowMask)
                {
                    float mask = tex2D(_shadowMask, uv).r;
                    if(mask < 0.000000001) return 0.0;
                    if(mask > 0.999999999) return 1.0; 
                }
                
                float shadow = 1.0;
                float csmlevel = d_lin * (_far - _near) / _csmMaxDistance;
                if(csmlevel < _split0)
                {
                    worldPosOffset.xyz += normal * _shadingPointNormalBias0;
                    float bias = (1 * _orthoWidth0 / _shadowMapResolution) * _depthNormalBias0;

                    //shadow *= shadowMap01(worldPosOffset, _shadowtex0, _shadowVpMatrix0, bias);
                    //shadow *= PCF3x3(worldPosOffset, _shadowtex0, _shadowVpMatrix0, _shadowMapResolution, 0);
                    shadow *= shadowMapPCSS(worldPosOffset, _shadowtex0, _shadowVpMatrix0, _orthoWidth0, _orthoDistance, _shadowMapResolution, rotateAngle, _pcssSearchRadius0, _pcssFilterRadius0, bias);
                }
                else if(csmlevel < _split0 + _split1)
                {
                    worldPosOffset.xyz += normal * _shadingPointNormalBias1;
                    float bias = (1 * _orthoWidth1 / _shadowMapResolution) * _depthNormalBias1;

                    //shadow *= shadowMap01(worldPosOffset, _shadowtex1, _shadowVpMatrix1, bias);
                    //shadow *= PCF3x3(worldPosOffset, _shadowtex1, _shadowVpMatrix1, _shadowMapResolution, 0);
                    shadow *= shadowMapPCSS(worldPosOffset, _shadowtex1, _shadowVpMatrix1, _orthoWidth1, _orthoDistance, _shadowMapResolution, rotateAngle, _pcssSearchRadius1, _pcssFilterRadius1, bias);
                }
                else if(csmlevel < _split0 + _split1 + _split2)
                {
                    worldPosOffset.xyz += normal * _shadingPointNormalBias2;
                    float bias = (1 * _orthoWidth2 / _shadowMapResolution) * _depthNormalBias2;

                    shadow *= shadowMap01(worldPosOffset, _shadowtex2, _shadowVpMatrix2, bias);
                    //shadow *= PCF3x3(worldPosOffset, _shadowtex2, _shadowVpMatrix2, _shadowMapResolution, rotateAngle, 0);
                   //shadow *= shadowMapPCSS(worldPosOffset, _shadowtex2, _shadowVpMatrix2, _orthoWidth2, _orthoDistance, _shadowMapResolution, rotateAngle, _pcssSearchRadius2, _pcssFilterRadius2, bias);
                }             
                else if(csmlevel < _split0 + _split1 + _split2 + _split3)
                {
                    worldPosOffset.xyz += normal * _shadingPointNormalBias3;
                    float bias = (1 * _orthoWidth3 / _shadowMapResolution) * _depthNormalBias3;

                    shadow *= shadowMap01(worldPosOffset, _shadowtex3, _shadowVpMatrix3, bias);
                    //shadow *= PCF3x3(worldPosOffset, _shadowtex3, _shadowVpMatrix3, _shadowMapResolution, rotateAngle, 0);
                    //shadow *= shadowMapPCSS(worldPosOffset, _shadowtex3, _shadowVpMatrix3, _orthoWidth3, _orthoDistance, _shadowMapResolution, rotateAngle, _pcssSearchRadius3, _pcssFilterRadius3, bias);
                }
                
                return shadow;
            }
            ENDCG
        }
    }
}
