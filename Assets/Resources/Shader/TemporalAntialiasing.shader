Shader "HzRP/TemporalAntialiasing"
{
     Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
    }
    CGINCLUDE
        #include "HzRPCommon.cginc"
        struct appdata
		{
			float4 vertex : POSITION;
			float2 texcoord : TEXCOORD0;
		};

		struct v2f
		{
			float4 vertex : SV_POSITION;
			float2 texcoord : TEXCOORD0;
		};

		v2f vert(appdata v)
		{
			v2f o;
			o.vertex = UnityObjectToClipPos(v.vertex);
		    o.texcoord = v.texcoord;
			return o;
		};
            
        float Luma4(float3 Color)
        {
            return (Color.g * 2) + (Color.r + Color.b);
        }
            
        float3 RGBToYCoCg(float3 RGB)
        {
            const float3x3 mat = float3x3(0.25,0.5,0.25,0.5,0,-0.5,-0.25,0.5,-0.25);
            float3 col = mul(mat, RGB);
            return col;
        }
        
        float3 YCoCgToRGB(float3 YCoCg)
        {
            const float3x3 mat = float3x3(1,1,-1,1,0,1,1,-1,-1);
            return mul(mat, YCoCg);
        }

        float4 RGBToYCoCg(float4 RGB)
        {
            return float4(RGBToYCoCg(RGB.xyz), RGB.w);
        }

        float4 YCoCgToRGB(float4 YCoCg)
        {
            return float4(YCoCgToRGB(YCoCg.xyz), YCoCg.w); 
        }
        float Luma(float3 Color)
        {
            return (Color.g * 0.5) + (Color.r + Color.b) * 0.25;
        }
        #define TONE_BOUND 0.5
        float3 Tonemap(float3 x) 
        { 
            float luma = Luma(x);
            [flatten]
            if(luma <= TONE_BOUND) return x;
            else return x * (TONE_BOUND * TONE_BOUND - luma) / (luma * (2 * TONE_BOUND - 1 - luma));
            //return x * weight;
        }

        float3 TonemapInvert(float3 x) { 
            float luma = Luma(x);
            [flatten]
            if(luma <= TONE_BOUND) return x;
            else return x * (TONE_BOUND * TONE_BOUND - (2 * TONE_BOUND - 1) * luma) / (luma * (1 - luma));
        }

        float HdrWeight4(float3 Color, const float Exposure) 
        {
            return rcp(Luma4(Color) * Exposure + 4);
        }

        float3 ClipToAABB(float3 color, float3 minimum, float3 maximum)
        {
            // Note: only clips towards aabb center (but fast!)
            float3 center = 0.5 * (maximum + minimum);
            float3 extents = 0.5 * (maximum - minimum);

            // This is actually `distance`, however the keyword is reserved
            float3 offset = color.rgb - center;

            float3 ts = abs(extents / (offset + 0.0001));
            float t = saturate(min3(ts.x, ts.y, ts.z));
            color.rgb = center + offset * t;
            return color;
        }

        static const int2 _OffsetArray[8] = {
            int2(-1, -1),
            int2(0, -1),
            int2(1, -1),
            int2(-1, 0),
            int2(1, 1),
            int2(1, 0),
            int2(-1, 1),
            int2(0, 1)
        };
        #if defined(UNITY_REVERSED_Z)
            #define COMPARE_DEPTH(a, b) step(b, a)
        #else
            #define COMPARE_DEPTH(a, b) step(a, b)
        #endif
        float2 ReprojectedMotionVectorUV(float2 uv, out float outDepth)
        {
            float neighborhood;
            const float2 k = 1.0 / _ScreenParams.xy;
            uint i;
            outDepth  = tex2D(_gdepth, uv).x;
            float3 result = float3(0, 0,  outDepth);
            [unroll]
            for(i = 0; i < 8; ++i){
                neighborhood = tex2D(_gdepth, uv + _OffsetArray[i]*(_ScreenParams.zw - 1));
                result = lerp(result, float3(_OffsetArray[i], neighborhood), COMPARE_DEPTH(neighborhood, result.z));
            }

            return uv + result.xy * k;
        }

        float4 Solver_TAA(v2f i) : SV_TARGET0
        {
            const float ExposureScale = 10;
            float2 uv = i.texcoord - _Jitter;
            float depth;
            float2 closest = ReprojectedMotionVectorUV(i.texcoord, /*out*/depth);
            float2 velocity = tex2D(_GT2, closest).xy;

    //////////////////TemporalClamp
            float2 PrevCoord = (i.texcoord - velocity);
            float4 MiddleCenter = _MainTex.Sample(sampler_linear_clamp, uv);
            if (PrevCoord.x > 1 || PrevCoord.y > 1 || PrevCoord.x < 0 || PrevCoord.y < 0 || _EnableReprojection < .0f) {
                return float4(MiddleCenter.xyz, 1);
            }
            
            float4 TopLeft = _MainTex.SampleLevel(sampler_linear_clamp, uv, 0, int2(-1, -1));
            float4 TopCenter = _MainTex.SampleLevel(sampler_linear_clamp, uv, 0, int2(0, -1));
            float4 TopRight = _MainTex.SampleLevel(sampler_linear_clamp, uv, 0, int2(1, -1));
            float4 MiddleLeft = _MainTex.SampleLevel(sampler_linear_clamp, uv, 0, int2(-1, 0));
            float4 MiddleRight = _MainTex.SampleLevel(sampler_linear_clamp, uv, 0, int2(1, 0));
            float4 BottomLeft = _MainTex.SampleLevel(sampler_linear_clamp, uv, 0, int2(-1, 1));
            float4 BottomCenter = _MainTex.SampleLevel(sampler_linear_clamp, uv, 0, int2(0, 1));
            float4 BottomRight = _MainTex.SampleLevel(sampler_linear_clamp, uv, 0, int2(1, 1));
            float SampleWeights[9];
            SampleWeights[0] = HdrWeight4(TopLeft.rgb, ExposureScale);
            SampleWeights[1] = HdrWeight4(TopCenter.rgb, ExposureScale);
            SampleWeights[2] = HdrWeight4(TopRight.rgb, ExposureScale);
            SampleWeights[3] = HdrWeight4(MiddleLeft.rgb, ExposureScale);
            SampleWeights[4] = HdrWeight4(MiddleCenter.rgb, ExposureScale);
            SampleWeights[5] = HdrWeight4(MiddleRight.rgb, ExposureScale);
            SampleWeights[6] = HdrWeight4(BottomLeft.rgb, ExposureScale);
            SampleWeights[7] = HdrWeight4(BottomCenter.rgb, ExposureScale);
            SampleWeights[8] = HdrWeight4(BottomRight.rgb, ExposureScale);
            TopLeft = RGBToYCoCg(TopLeft);
            TopCenter = RGBToYCoCg(TopCenter);
            TopRight = RGBToYCoCg(TopRight);
            MiddleLeft = RGBToYCoCg(MiddleLeft);
            MiddleCenter = RGBToYCoCg(MiddleCenter);
            MiddleRight = RGBToYCoCg(MiddleRight);
            BottomLeft = RGBToYCoCg(BottomLeft);
            BottomCenter = RGBToYCoCg(BottomCenter);
            BottomRight = RGBToYCoCg(BottomRight);


            float TotalWeight = SampleWeights[0] + SampleWeights[1] + SampleWeights[2] + SampleWeights[3] + SampleWeights[4] + SampleWeights[5] + SampleWeights[6] + SampleWeights[7] + SampleWeights[8];                   
            float4 Filtered = (TopLeft * SampleWeights[0] + TopCenter * SampleWeights[1] + TopRight * SampleWeights[2] + MiddleLeft * SampleWeights[3] + MiddleCenter * SampleWeights[4] + MiddleRight * SampleWeights[5] + BottomLeft * SampleWeights[6] + BottomCenter * SampleWeights[7] + BottomRight * SampleWeights[8]) / TotalWeight;
                
            // Resolver Average
            float VelocityLength = length(velocity);
            float VelocityWeight = saturate(VelocityLength * _TemporalClipBounding.z);
            float AABBScale = lerp(_TemporalClipBounding.x, _TemporalClipBounding.y, VelocityWeight);

            float4 m1 = TopLeft + TopCenter + TopRight + MiddleLeft + MiddleCenter + MiddleRight + BottomLeft + BottomCenter + BottomRight;
            float4 m2 = TopLeft * TopLeft + TopCenter * TopCenter + TopRight * TopRight + MiddleLeft * MiddleLeft + MiddleCenter * MiddleCenter + MiddleRight * MiddleRight + BottomLeft * BottomLeft + BottomCenter * BottomCenter + BottomRight * BottomRight;
            
            float4 mean = m1 / 9;
            float4 stddev = sqrt(m2 / 9 - mean * mean);  

            float4 minColor = mean - AABBScale * stddev;
            float4 maxColor = mean + AABBScale * stddev;
            minColor = min(minColor, Filtered);
            maxColor = max(maxColor, Filtered);

    //////////////////TemporalResolver
            float4 CurrColor = YCoCgToRGB(MiddleCenter);

            // Sharpen output
            float4 corners = ( YCoCgToRGB(TopLeft + BottomRight + TopRight + BottomLeft) - CurrColor ) * 2;
            CurrColor += ( CurrColor - (corners * 0.166667) ) * 2.718282 * _Sharpness;
            CurrColor = clamp(CurrColor, 0, HALF_MAX_MINUS1);

            // HistorySample
          
            float4 PrevColor = tex2D(_PreviousColorBuffer, PrevCoord);
            PrevColor.xyz =  YCoCgToRGB(ClipToAABB( RGBToYCoCg(PrevColor.xyz), minColor.xyz, maxColor.xyz ));
            // HistoryBlend
          //  return float4(lerp(depthAdaptiveForce, 1, (depth01.x > 0.9999) || (depth01.y > 0.9999)).xxx, depthAdaptiveForce);
            
            float HistoryWeight = lerp(_FinalBlendParameters.x, _FinalBlendParameters.y, VelocityWeight);
            CurrColor.xyz = Tonemap(CurrColor.xyz);
            PrevColor.xyz = Tonemap(PrevColor.xyz);
            float4 TemporalColor = lerp(CurrColor, PrevColor, HistoryWeight);
            TemporalColor.xyz = TonemapInvert(TemporalColor.xyz);
            return max(0, TemporalColor);
        }

    ENDCG

    SubShader
    {
    
        Pass
        {
            Cull Off ZWrite Off ZTest Always
            CGPROGRAM

                #pragma vertex vert
                #pragma fragment Solver_TAA

            ENDCG
        }

    }
}