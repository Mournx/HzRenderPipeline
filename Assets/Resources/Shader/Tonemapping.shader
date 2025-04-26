Shader "HzRP/Tonemapping"
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
            #pragma vertex TonemapVert
            #pragma fragment TonemapFragment
            
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl"
            
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

            sampler2D _MainTex;

            int _TonemappingType;
            float4 _ColorGradeParams;
            float4 _ColorFilter;

            float3 ColorGradePostExposure(float3 color){
                return color * _ColorGradeParams.x;
            }

            float3 ColorGradeContrast(float3 color){
                color = LinearToLogC(color); 
                color = (color - ACEScc_MIDGRAY) * _ColorGradeParams.y + ACEScc_MIDGRAY;
                color = LogCToLinear(color);
                return color;
            }

            float3 ColorGradeFilter(float3 color){
                return color * _ColorFilter.rgb;
            }

            float3 ColorGradeHueShift(float3 color){
                color = RgbToHsv(color);
                float hue = color.x + _ColorGradeParams.z;
                color.x = RotateHue(hue, .0f, 1.0f);
                color = HsvToRgb(color);
                return color;
            }

            float3 ColorGradeSaturation(float3 color){
                float luminance = Luminance(color);
                return (color - luminance) * _ColorGradeParams.w + luminance;
            }

            float3 ColorGrade(float3 color){
                color = ColorGradePostExposure(color);
                color = ColorGradeContrast(color);
                color = ColorGradeFilter(color);

                color = max(color, .0f);

                color = ColorGradeHueShift(color);
                color = ColorGradeSaturation(color);

                return max(color, .0f);
            }

            float3 ReinhardTonemap(float3 color){
                return color.rgb / (color.rgb + 1.0f);
            }
            

            v2f TonemapVert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                return o;
            }

            float4 TonemapFragment(v2f i) : SV_Target
            {
                float2 uv = i.uv;
                float4 color = tex2D(_MainTex, uv);
                color.rgb = ColorGrade(color.rgb);

                if (_TonemappingType == 1) color.rgb = ReinhardTonemap(color.rgb);
                else if (_TonemappingType == 2) color.rgb = NeutralTonemap(color.rgb);
                else if (_TonemappingType == 3) color.rgb = AcesTonemap(unity_to_ACES(color.rgb));
                
                return color;
            }

           
            ENDCG
        }
    }
}
