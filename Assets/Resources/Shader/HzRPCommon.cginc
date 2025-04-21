#ifndef HzRP_COMMON_INCLUDED
#define HzRP_COMMON_INCLUDED

#include "UnityCG.cginc"

#define DEFAULT_REFLECTANCE (.4f)
#define MIN_LINEAR_ROUGHNESS (.045f)
#define MIN_ROUGHNESS (.002025f)

#define SPEC_IBL_MAX_MIP (6u)
#define DIFF_IBL_MAX_MIP (11u)

#define KILL_MICRO_MOVEMENT
#define MICRO_MOVEMENT_THRESHOLD (.01f / _ScreenParams.xy)

#define HALF_MAX_MINUS1 65472.0 // (2 - 2^-9) * 2^15
//////////////////////////////////////////
// Image Based Lighting Variables       //
//////////////////////////////////////////

float _GlobalEnvMapRotation;
float _SkyboxMipLevel;
float _SkyboxIntensity;

//////////////////////////////////////////
//         Global uniform  sampler    //
//////////////////////////////////////////

sampler sampler_linear_clamp;
sampler sampler_point_clamp;

Texture2D _PreintegratedDGFLut;
sampler sampler_PreintegratedDGFLut;
Texture2D _PreintegratedDLut;
sampler sampler_PreintegratedDLut;
Texture2D _PreintegratedGFLut;
sampler sampler_PreintegratedGFLut;
Texture2D _MainTex;
sampler sampler_MainTex;
float4 _MainTex_TexelSize;

TextureCube _GlobalEnvMapSpecular;
sampler sampler_GlobalEnvMapSpecular;
TextureCube _GlobalEnvMapDiffuse;
sampler sampler_GlobalEnvMapDiffuse;

sampler2D _gdepth;
sampler2D _GT0;
sampler2D _GT1;
sampler2D _GT2;
sampler2D _GT3;
sampler2D _PreviousColorBuffer;

sampler2D _noiseTex;

sampler2D _shadowStrength;
sampler2D _shadowtex0;
sampler2D _shadowtex1;
sampler2D _shadowtex2;
sampler2D _shadowtex3;
sampler2D _shadowMask;

float _far;
float _near;
float _screenWidth;
float _screenHeight;
float _usingShadowMask;
float _csmMaxDistance;

float4x4 _vpMatrix;
float4x4 _vpMatrixInv;
float4x4 _nonjitterVPMatrix;
float4x4 _nonjitterVPMatrixInv;
float4x4 _vpMatrixPrev;
float4x4 _vpMatrixInvPrev;
float4x4 _FrustumCornersWS;
float4x4 _PrevFrustumCornersWS;

float4x4 _shadowVpMatrix0;
float4x4 _shadowVpMatrix1;
float4x4 _shadowVpMatrix2;
float4x4 _shadowVpMatrix3;

float _split0;
float _split1;
float _split2;
float _split3;

float _orthoWidth0;
float _orthoWidth1;
float _orthoWidth2;
float _orthoWidth3;

float _orthoDistance;
float _shadowMapResolution;

float _noiseTexResolution;

float _shadingPointNormalBias0;
float _shadingPointNormalBias1;
float _shadingPointNormalBias2;
float _shadingPointNormalBias3;

float _depthNormalBias0;
float _depthNormalBias1;
float _depthNormalBias2;
float _depthNormalBias3;

float _pcssSearchRadius0;
float _pcssSearchRadius1;
float _pcssSearchRadius2;
float _pcssSearchRadius3;

float _pcssFilterRadius0;
float _pcssFilterRadius1;
float _pcssFilterRadius2;
float _pcssFilterRadius3;

float _EnableReprojection;
float _Sharpness;
float2 _Jitter;
float2 _LastJitter;
float4 _FinalBlendParameters;
float4 _TemporalClipBounding; 
//////////////////////////////////////////
//         Cluster Related            //
//////////////////////////////////////////
struct PointLight
{
    float3 color;
    float intensity;
    float3 position;
    float radius;
};

struct LightIndex
{
    int count;
    int start;
};

StructuredBuffer<PointLight> _lightBuffer;
StructuredBuffer<uint> _lightAssignBuffer;
StructuredBuffer<LightIndex> _assignTable;

float _numClusterX;
float _numClusterY;
float _numClusterZ;

uint Index3DTo1D(uint3 i)
{
    return i.z * _numClusterX * _numClusterY
        + i.y * _numClusterX
        + i.x;
}


//////////////////////////////////////////
//         Utility Functions     //
//////////////////////////////////////////

float pow2(float m){
    return m*m;
}
float pow5(float m){
    float pow2 = m*m;
    float pow4 = pow2*pow2;
    return pow4*m;
}
float pow8(float m){
    float pow2 = m*m;
    float pow4 = pow2*pow2;
    return pow4*pow4;
}
float PositivePow(float base, float power){ return pow(abs(base), power);}

float Min3(float x, float y, float z){
    return min(x,min(y,z));
}

float3 RotateAroundYInDegrees(float3 vertex, float degrees) {
    float alpha = degrees * UNITY_PI / 180.0f;
    float sina, cosa;
    sincos(alpha, sina, cosa);
    float2x2 m = float2x2(cosa, -sina, sina, cosa);
    return float3(mul(m, vertex.xz), vertex.y).xzy;
}

float SignNotZero(float n) {
    return n >= .0f ? 1.0f : -1.0f;
}

float2 SignNotZero(float2 n) {
    return float2(SignNotZero(n.x), SignNotZero(n.y));
}

float3 SignNotZero(float3 n) {
    return float3(SignNotZero(n.x), SignNotZero(n.y), SignNotZero(n.z));
}

float4 SignNotZero(float4 n) {
    return float4(SignNotZero(n.x), SignNotZero(n.y), SignNotZero(n.z), SignNotZero(n.w));
}

float DistanceSqr(float3 a, float3 b) {
    float3 diff = a - b;
    return dot(diff, diff);
}

float4 VertexIDToPosCS(uint vertexID) {
    return float4(
        vertexID <= 1 ? -1.0f : 3.0f,
        vertexID == 1 ? 3.0f : -1.0f,
        .0f,
        1.0f);
}

float2 VertexIDToScreenUV(uint vertexID) {
    return float2(
        vertexID <= 1 ? .0f : 2.0f,
        vertexID == 1 ? 2.0f : .0f);
}

float4 VertexIDToFrustumCorners(uint vertexID) {
    return _FrustumCornersWS[vertexID];
}

float4 GetZBufferParams() {
    return _FrustumCornersWS[3];
}

float4 GetPrevZBufferParams() {
    return _PrevFrustumCornersWS[3];
}

float4 DepthToWorldPosFast(float depth, float3 ray) {
    float3 worldPos = _WorldSpaceCameraPos.xyz + Linear01Depth(depth) * ray;
    return float4(worldPos, 1.0f);
}

float4 DepthToWorldPos(float depth, float2 uv) {
    float4 ndc = float4(uv.x * 2.0f - 1.0f, uv.y * 2.0f - 1.0f, depth, 1.0f);
    float4 worldPosAccurate = mul(_vpMatrixInv, ndc);
    worldPosAccurate /= worldPosAccurate.w;
    worldPosAccurate.w = 1.0f;
    return worldPosAccurate;
}

float2 CalculateMotionVector(float4 posCS, float4 prevPosCS){
    float2 posNDC = posCS.xy / posCS.w;
    float2 prevPosNDC = prevPosCS.xy / prevPosCS.w;
    float2 mv = posNDC - prevPosNDC;

    #ifdef KILL_MICRO_MOVEMENT
    mv.x = abs(mv.x) < MICRO_MOVEMENT_THRESHOLD.x ? .0f : mv.x;
    mv.y = abs(mv.y) < MICRO_MOVEMENT_THRESHOLD.y ? .0f : mv.y;
    mv = clamp(mv, -1.0f + MICRO_MOVEMENT_THRESHOLD, 1.0f - MICRO_MOVEMENT_THRESHOLD);
    #else
    mv = clamp(mv, -1.0f + MICRO_MOVEMENT_THRESHOLD, 1.0f - MICRO_MOVEMENT_THRESHOLD);
    #endif

    if (_ProjectionParams.x < .0f) mv.y = -mv.y;
    // mv.x = -mv.x;

    return mv * .5f;
}

float2 PackNormalOctQuadEncode(float3 n)
{
    n *= rcp(max(dot(abs(n), 1.0), 1e-6));
    float t = saturate(-n.z);
    return n.xy + float2(n.x >= 0.0 ? t : -t, n.y >= 0.0 ? t : -t);
}
float3 UnpackNormalOctQuadEncode(float2 f)
{
    float3 n = float3(f.x, f.y, 1.0 - abs(f.x) - abs(f.y));

    //float2 val = 1.0 - abs(n.yx);
    //n.xy = (n.zz < float2(0.0, 0.0) ? (n.xy >= 0.0 ? val : -val) : n.xy);

    // Optimized version of above code:
    float t = max(-n.z, 0.0);
    n.xy += float2(n.x >= 0.0 ? -t : t, n.y >= 0.0 ? -t : t);

    return normalize(n);
}
// Convert Normal from [-1, 1] to [0, 1]
float2 EncodeNormalComplex(float3 N)
{
    return PackNormalOctQuadEncode(N) * .5f + .5f;
}

// Convert Normal from [0, 1] to [-1, 1]
float3 DecodeNormalComplex(float2 N)
{
    return UnpackNormalOctQuadEncode(N * 2.0f - 1.0f);
}
float3 SampleNormalWS(float2 uv){
    return DecodeNormalComplex(tex2D(_GT1, uv).rg);
}

/*
 * 生成随机向量，依赖于 frameCounter 帧计数器
 * 代码来源：https://blog.demofox.org/2020/05/25/casual-shadertoy-path-tracing-1-basic-camera-diffuse-emissive/
*/

// screen uv, screen resolution (width, height)
uint RandomSeed(float2 uv, float2 screenWH)
{
    return uint(
        uint(uv.x * screenWH.x)  * uint(1973) + 
        uint(uv.y * screenWH.y) * uint(9277) + 
        uint(114514) * uint(26699)) | uint(1);
}

uint wang_hash(inout uint seed) {
    seed = uint(seed ^ uint(61)) ^ uint(seed >> uint(16));
    seed *= uint(9);
    seed = seed ^ (seed >> 4);
    seed *= uint(0x27d4eb2d);
    seed = seed ^ (seed >> 15);
    return seed;
}
 
float rand(inout uint seed) {
    return float(wang_hash(seed)) / 4294967296.0;
}


// Ref: http://holger.dammertz.org/stuff/notes_HammersleyOnHemisphere.html
uint ReverseBits32(uint bits)
{
    #if (SHADER_TARGET >= 45)
    return reversebits(bits);
    #else
    bits = (bits << 16) | (bits >> 16);
    bits = ((bits & 0x00ff00ff) << 8) | ((bits & 0xff00ff00) >> 8);
    bits = ((bits & 0x0f0f0f0f) << 4) | ((bits & 0xf0f0f0f0) >> 4);
    bits = ((bits & 0x33333333) << 2) | ((bits & 0xcccccccc) >> 2);
    bits = ((bits & 0x55555555) << 1) | ((bits & 0xaaaaaaaa) >> 1);
    return bits;
    #endif
}

float VanDerCorputBase2(uint i)
{
    return ReverseBits32(i) * rcp(4294967296.0); // 2^-32
}

float2 Hammersley2dSeq(uint i, uint sequenceLength)
{
    return float2(float(i) / float(sequenceLength), VanDerCorputBase2(i));
}

// Performs uniform sampling of the unit disk.
// Ref: PBRT v3, p. 777.
float2 SampleDiskUniform(float u1, float u2)
{
    float r   = sqrt(u1);
    float phi = UNITY_TWO_PI * u2;

    float sinPhi, cosPhi;
    sincos(phi, sinPhi, cosPhi);

    return r * float2(cosPhi, sinPhi);
}

// Performs cosine-weighted sampling of the hemisphere.
// Ref: PBRT v3, p. 780.
float3 SampleHemisphereCosine(float u1, float u2)
{
    float3 localL;

    // Since we don't floatly care about the area distortion,
    // we substitute uniform disk sampling for the concentric one.
    localL.xy = SampleDiskUniform(u1, u2);

    // Project the point from the disk onto the hemisphere.
    localL.z = sqrt(1.0 - u1);

    return localL;
}


//////////////////////////////////////////
//         Shadow Function             //
//////////////////////////////////////////

/*
#define N_SAMPLE 16
static float2 poissonDisk[16] = {
    float2( -0.94201624, -0.39906216 ),
    float2( 0.94558609, -0.76890725 ),
    float2( -0.094184101, -0.92938870 ),
    float2( 0.34495938, 0.29387760 ),
    float2( -0.91588581, 0.45771432 ),
    float2( -0.81544232, -0.87912464 ),
    float2( -0.38277543, 0.27676845 ),
    float2( 0.97484398, 0.75648379 ),
    float2( 0.44323325, -0.97511554 ),
    float2( 0.53742981, -0.47373420 ),
    float2( -0.26496911, -0.41893023 ),
    float2( 0.79197514, 0.19090188 ),
    float2( -0.24188840, 0.99706507 ),
    float2( -0.81409955, 0.91437590 ),
    float2( 0.19984126, 0.78641367 ),
    float2( 0.14383161, -0.14100790 )
};
*/

#define N_SAMPLE 64
static float2 poissonDisk[N_SAMPLE] = {
    float2(-0.5119625f, -0.4827938f),
    float2(-0.2171264f, -0.4768726f),
    float2(-0.7552931f, -0.2426507f),
    float2(-0.7136765f, -0.4496614f),
    float2(-0.5938849f, -0.6895654f),
    float2(-0.3148003f, -0.7047654f),
    float2(-0.42215f, -0.2024607f),
    float2(-0.9466816f, -0.2014508f),
    float2(-0.8409063f, -0.03465778f),
    float2(-0.6517572f, -0.07476326f),
    float2(-0.1041822f, -0.02521214f),
    float2(-0.3042712f, -0.02195431f),
    float2(-0.5082307f, 0.1079806f),
    float2(-0.08429877f, -0.2316298f),
    float2(-0.9879128f, 0.1113683f),
    float2(-0.3859636f, 0.3363545f),
    float2(-0.1925334f, 0.1787288f),
    float2(0.003256182f, 0.138135f),
    float2(-0.8706837f, 0.3010679f),
    float2(-0.6982038f, 0.1904326f),
    float2(0.1975043f, 0.2221317f),
    float2(0.1507788f, 0.4204168f),
    float2(0.3514056f, 0.09865579f),
    float2(0.1558783f, -0.08460935f),
    float2(-0.0684978f, 0.4461993f),
    float2(0.3780522f, 0.3478679f),
    float2(0.3956799f, -0.1469177f),
    float2(0.5838975f, 0.1054943f),
    float2(0.6155105f, 0.3245716f),
    float2(0.3928624f, -0.4417621f),
    float2(0.1749884f, -0.4202175f),
    float2(0.6813727f, -0.2424808f),
    float2(-0.6707711f, 0.4912741f),
    float2(0.0005130528f, -0.8058334f),
    float2(0.02703013f, -0.6010728f),
    float2(-0.1658188f, -0.9695674f),
    float2(0.4060591f, -0.7100726f),
    float2(0.7713396f, -0.4713659f),
    float2(0.573212f, -0.51544f),
    float2(-0.3448896f, -0.9046497f),
    float2(0.1268544f, -0.9874692f),
    float2(0.7418533f, -0.6667366f),
    float2(0.3492522f, 0.5924662f),
    float2(0.5679897f, 0.5343465f),
    float2(0.5663417f, 0.7708698f),
    float2(0.7375497f, 0.6691415f),
    float2(0.2271994f, -0.6163502f),
    float2(0.2312844f, 0.8725659f),
    float2(0.4216993f, 0.9002838f),
    float2(0.4262091f, -0.9013284f),
    float2(0.2001408f, -0.808381f),
    float2(0.149394f, 0.6650763f),
    float2(-0.09640376f, 0.9843736f),
    float2(0.7682328f, -0.07273844f),
    float2(0.04146584f, 0.8313184f),
    float2(0.9705266f, -0.1143304f),
    float2(0.9670017f, 0.1293385f),
    float2(0.9015037f, -0.3306949f),
    float2(-0.5085648f, 0.7534177f),
    float2(0.9055501f, 0.3758393f),
    float2(0.7599946f, 0.1809109f),
    float2(-0.2483695f, 0.7942952f),
    float2(-0.4241052f, 0.5581087f),
    float2(-0.1020106f, 0.6724468f)
};

float2 RotateVec2(float2 v, float angle)
{
    float s = sin(angle);
    float c = cos(angle);

    return float2(v.x * c + v.y * s, -v.x * s + v.y * c);
}

float shadowMap01(float4 worldPos, sampler2D _shadowtex, float4x4 _shadowVpMatrix, float bias)
{
    float4 shadowNdc = mul(_shadowVpMatrix, worldPos);
    shadowNdc /= shadowNdc.w;
    float2 uv = shadowNdc.xy * 0.5 + 0.5;

    float d = shadowNdc.z;
    float d_sample = tex2D(_shadowtex, uv).r;

#if defined(UNITY_REVERSED_Z)
    if(d_sample > d+bias) return 0.0f;
#else
    if(d_sample+bias < d) return 0.0f;
#endif

    return 1.0f;
}

float PCF3x3(float4 worldPos, sampler2D _shadowtex, float4x4 _shadowVpMatrix, float shadowMapResolution, float bias)
{
    float4 shadowNdc = mul(_shadowVpMatrix, worldPos);
    shadowNdc /= shadowNdc.w;
    float2 uv = shadowNdc.xy * 0.5 + 0.5;

    if(uv.x < 0 || uv.y < 0 || uv.x > 1 || uv.y > 1) return 1.0f;
    
    float d_shadingPoint = shadowNdc.z;
    float shadow = 0.0;

    for(int i=-1; i<=1; i++)
    {
        for(int j=-1; j<=1; j++)
        {
            float2 offset = float2(i, j) / shadowMapResolution;
            float d_sample = tex2D(_shadowtex, uv+offset*0.1).r;

            #if defined (UNITY_REVERSED_Z)
            if(d_sample-bias>d_shadingPoint)
                #else
            if(d_sample+bias<d_shadingPoint)
                #endif
                shadow += 1.0;
        }
    }

    return 1.0 - (shadow / 9.0);
}

float2 AverageBlockerDepth(float4 shadowNdc, sampler2D _shadowtex, float d_shadingPoint, float searchWidth, float rotateAngle, float bias)
{
    float2 uv = shadowNdc * 0.5 + 0.5;
    float step = 3.0;
    float d_average = 0.0;
    float count = 0.0005;

    
    for(int i = -step; i <= step; i++)
    {
        for(int j = -step; j <= step; j++)
        {
            float2 unitOffset = float2(i, j) / step;
            float2 offset = unitOffset * searchWidth;
            float2 uvOffset = uv + offset;

            float d_sample = tex2D(_shadowtex, uvOffset).r;
            if(d_sample > d_shadingPoint)
            {
                count += 1;
                d_average += d_sample;
            }
        }
    }
/*
    for(int i = 0; i < N_SAMPLE; i++)
    {
        float2 unitOffset = RotateVec2(poissonDisk[2], rotateAngle);
        float2 offset = unitOffset * searchWidth;
        float2 uvOffset = uv + offset;

        float d_sample = tex2D(_shadowtex, uvOffset).r;
        if(d_sample > d_shadingPoint + bias)
        {
            count += 1;
            d_average += d_sample;
        }
    }
    */
    return float2(d_average / count, count);
}

float shadowMapPCSS(float4 worldPos, sampler2D _shadowtex, float4x4 _shadowVpMatrix,
    float orthoWidth, float orthoDistance, float shadowMapResolution, float rotateAngle,
    float pcssSearchRadius, float pcssFilterRadius, float bias)
{
    float4 shadowNdc = mul(_shadowVpMatrix, worldPos);
    shadowNdc /= shadowNdc.w;
    float2 uv = shadowNdc.xy * 0.5 + 0.5;

    float d_shadingPoint = shadowNdc.z;

    //计算平均遮挡深度
    float searchWidth = pcssSearchRadius / orthoWidth;
    float2 blocker = AverageBlockerDepth(shadowNdc, _shadowtex, d_shadingPoint, searchWidth, rotateAngle, bias);
    float d_average = blocker.x;
    float blockCount = blocker.y;
    if(blockCount < 1) return 1.0;

    //转换到世界空间, 用于计算PCSS, 注意Reverse Z
    float d_receiver = (1.0 - d_shadingPoint) * orthoDistance;
    float d_blocker = (1.0 - d_average) * orthoDistance;
    float w = (d_receiver - d_blocker) * pcssFilterRadius / d_blocker;
    //深度图上的filter半径
    float radius = w / orthoWidth;

    float shadow = 0.0f;

    /*
    float sum  = 0;
    float step = 3;
    for(int i = -step; i <= step; i++)
    {
        for(int j = -step; j <= step; j++)
        {
            sum += 1;
            float2 offset = float2(i, j) / step;
            float2 uvOffset = uv + offset * radius;
            float d_sample = tex2D(_shadowtex, uvOffset).r;
            if(d_sample > d_shadingPoint) shadow += 1.0f;
        }
    }
    shadow /= sum;
   */

    for(int i = 0; i < N_SAMPLE; i++)
    {
        float2 offset = poissonDisk[i];
        offset = RotateVec2(offset, rotateAngle);
        float2 uvOffset = uv + offset * radius;

        float d_sample = tex2D(_shadowtex, uvOffset).r;
        if(d_sample > d_shadingPoint + bias) shadow += 1.0f;
    }
    shadow /= N_SAMPLE;
    
    return 1 - shadow;
}


//////////////////////////////////////////
// PBR Utility Functions                //
//////////////////////////////////////////

float LinearSmoothnessToLinearRoughness(float linearSmoothness){
    return 1.0f - linearSmoothness;
}

// Alpha = Roughness = (linear Roughness) ^ 2
float LinearRoughnessToRoughness(float linearRoughness){
    return linearRoughness * linearRoughness;
}

// AlphaG2 = (Alpha) ^ 2 = (linear Roughness) ^ 4
float RoughnessToAlphaG2(float roughness){
    return roughness * roughness;
}

float LinearRoughnessToAlphaG2(float linearRoughness) {
    float roughness = linearRoughness * linearRoughness;
    return roughness * roughness;
}

float ClampMinLinearRoughness(float linearRoughness){
    return max(linearRoughness, MIN_LINEAR_ROUGHNESS); // Anti specular flickering
}

float ClampMinRoughness(float roughness){
    return max(roughness, MIN_ROUGHNESS); // Anti specular flickering
}

// maxMipLevel: start from 0
float LinearRoughnessToMipmapLevel(float linearRoughness, uint maxMipLevel) {
    // return linearRoughness * maxMipLevel;
    linearRoughness = linearRoughness * (2.0f - linearRoughness);
    // linearRoughness = linearRoughness * (1.7f - .7f * linearRoughness);
    return linearRoughness * maxMipLevel;
}

float3 GetF0(float3 albedo, float metallic){
    float3 f0 = float3(.04f, .04f, .04f);
    return f0 * (1.0 - metallic) + albedo * metallic;
}

float3 GetF0(float3 reflectance) {
    return .16f * (reflectance * reflectance);
}

float3 F0ClearCoatToSurface(float3 f0) {
    return saturate(f0 * (f0 * (.941892f - .263008f * f0) + .346479f) - .0285998f);
}

float3 ShiftTangent(float3 T, float3 N, float shift) {
    return normalize(T + N * shift);
}

void GetAnisotropyTB(float anisotropy, float roughness, out float2 atb) {
    atb.x = ClampMinRoughness(roughness * (1.0f + anisotropy));
    atb.y = ClampMinRoughness(roughness * (1.0f - anisotropy));
}

// D
// Requires caller to "div PI"
float D_GGX(float NdotH, float a){
    float a2 = a * a;
    float NdotH2 = NdotH * NdotH;

    float nom = a2;
    float denom = (NdotH2 * (a2 - 1.0) + 1.0);
    denom = denom * denom;

    return nom / max(denom, .00001f);
}

// Requires caller to "div PI"
float D_GGX_Anisotropic(float NdotH, float TdotH, float BdotH, float2 atb){
    float a2 = atb.x * atb.y;
    float3 V = float3(atb.y * TdotH, atb.x * BdotH, a2 * NdotH);
    float v2 = dot(V, V);
    float w2 = a2 / v2;
    return a2 * w2 * w2;
}

// Requires caller to "div PI"
// Ashikhmin 2007, "Distribution-based BRDFs"
float D_Ashikhmin(float NdotH, float alphaG2){
    float cos2h = NdotH * NdotH;
    float sin2h = max(1.0f-cos2h, .0078125f); // 2^(-14/2), so sin2h^2 > 0 in fp16
    float sin4h = sin2h * sin2h;
    float cot2 = -cos2h / (alphaG2 * sin2h);
    return 1.0f / ((4.0f * alphaG2 + 1.0f) * sin4h) * (4.0f * exp(cot2) + sin4h);
}

// Requires caller to "div PI"
// Estevez and Kulla 2017, "Production Friendly Microfacet Sheen BRDF"
float D_Charlie(float NdotH, float roughness){
    float invAlpha = 1.0f / roughness;
    float cos2h = NdotH * NdotH;
    float sin2h = max(1.0f - cos2h, .0078125f); // 2^(-14/2), so sin2h^2 > 0 in fp16
    return (2.0f + invAlpha) * pow(sin2h, invAlpha * .5f) * .5f;
}

// F
float3 F_Schlick(float u, float3 f0){
    return f0 + (float3(1.0f, 1.0f, 1.0f) - f0) * pow5(1.0f - u);
}

float3 F_Schlick(float u, float3 f0, float3 f90){
    return f0 + (f90 - f0) * pow5(1.0f - u);
}

//Unity use this as IBL F
float3 F_SchlickRoughness(float u, float3 f0, float linearRoughness)
{
    float r1 = 1.0f - linearRoughness;
    return f0 + (max(float3(r1, r1, r1), f0) - f0) *pow5(saturate(1.0 - u));
}

// V
float V_SchlickGGX(float NdotV, float k){
    float nom = NdotV;
    float denom = NdotV * (1.0 - k) + k;

    return nom / denom;
}

float V_SmithGGX(float NdotL, float NdotV, float alphaG2){
    const float lambdaV = NdotL * sqrt((-NdotV * alphaG2 + NdotV) * NdotV + alphaG2);
    const float lambdaL = NdotV * sqrt((-NdotL * alphaG2 + NdotL) * NdotL + alphaG2);
    return .5f / max(lambdaV + lambdaL, .00001f);
}

float V_SmithGGX_Anisotropic(float2 atb, float TdotV, float BdotV, float TdotL, float BdotL, float NdotV, float NdotL){
    const float lambdaV = NdotL * length(float3(atb.x * TdotV, atb.y * BdotV, NdotV));
    const float lambdaL = NdotV * length(float3(atb.x * TdotL, atb.y * BdotL, NdotL));
    float v = .5f / max(lambdaV + lambdaL, .00001f);
    return saturate(v);
}

float V_Kelemen(float LdotH){
    return .25f / max(pow2(LdotH), .00001f);
}

float V_Neubelt(float NdotL, float NdotV){
    return saturate(1.0f / (4.0f * (NdotL + NdotV - NdotL * NdotV)));
}

float CharlieL(float x, float r){
    r = saturate(r);
    r = 1.0f - pow2(1.0f - r);

    float a = lerp(25.3245f, 21.5473f, r);
    float b = lerp(3.32435f, 3.82987f, r);
    float c = lerp(.16801f, 0.19823f, r);
    float d = lerp(-1.27393f, -1.97760f, r);
    float e = lerp(-4.85967f, -4.32054f, r);

    return a / (1.0f + b * PositivePow(x, c)) + d * x + e;
}

float SoftenCharlie(float base, float cos_theta){
    const float softenTerm = 1.0f + 2.0f * pow8(1.0f - cos_theta);
    return pow(base, softenTerm);
}

float V_Charlie_No_Softening(float NdotL, float NdotV, float roughness){
    const float lambdaV = NdotV < .5f ? exp(CharlieL(NdotV, roughness)) : exp(2.0f * CharlieL(.5f, roughness) - CharlieL(1.0f - NdotV, roughness));
    const float lambdaL = NdotL < .5f ? exp(CharlieL(NdotL, roughness)) : exp(2.0f * CharlieL(.5f, roughness) - CharlieL(1.0f - NdotL, roughness));
    return 1.0f / ((1.0f + lambdaV + lambdaL) * (4.0f * NdotV * NdotL));
}

float V_Charlie(float NdotL, float NdotV, float roughness){
    float lambdaV = NdotV < .5f ? exp(CharlieL(NdotV, roughness)) : exp(2.0f * CharlieL(.5f, roughness) - CharlieL(1.0f - NdotV, roughness));
    lambdaV = SoftenCharlie(lambdaV, NdotV);
    float lambdaL = NdotL < .5f ? exp(CharlieL(NdotL, roughness)) : exp(2.0f * CharlieL(.5f, roughness) - CharlieL(1.0f - NdotL, roughness));
    lambdaL = SoftenCharlie(lambdaL, NdotL);
    return 1.0f / ((1.0f + lambdaV + lambdaL) * (4.0f * NdotV * NdotL));
}

float DisneyDiffuseRenormalized(float NdotV, float NdotL, float LdotH, float linearRoughness){
    float energyBias = lerp(.0f, .5f, linearRoughness);
    float energyFactor = lerp(1.0f, 1.0f / 1.51f, linearRoughness);
    float f90 = energyBias + 2.0f * LdotH * LdotH * linearRoughness;
    const float3 f0 = float3(1.0f, 1.0f, 1.0f);
    float lightScatter = F_Schlick(f0, f90, NdotL).r;
    float viewScatter = F_Schlick(f0, f90, NdotV).r;

    return lightScatter * viewScatter * energyFactor;
}

float3 DisneyDiffuseRenormalized(float NdotV, float NdotL, float LdotH, float linearRoughness, float3 diffuse){
    float energyBias = lerp(.0f, .5f, linearRoughness);
    float energyFactor = lerp(1.0f, 1.0f / 1.51f, linearRoughness);
    float f90 = energyBias + 2.0f * LdotH * LdotH * linearRoughness;
    const float3 f0 = float3(1.0f, 1.0f, 1.0f);
    float lightScatter = F_Schlick(f0, f90, NdotL).r;
    float viewScatter = F_Schlick(f0, f90, NdotV).r;

    return lightScatter * viewScatter * energyFactor * diffuse;
}

float3 DisneyDiffuseMultiScatter(float NdotV, float NdotL, float NdotH, float LdotH, float alphaG2, float3 diffuse){
    float g = saturate(.18455f * log(2.0f / alphaG2 - 1.0f));
    float f0 = LdotH + pow5(1.0f - LdotH);
    float f1 = (1.0f - .75f * pow5(1.0f - NdotL)) * (1.0f - .75f * pow5(NdotV));
    float t = saturate(2.2f * g - .5f);
    float fd = f0 + (f1 - f0) * t;
    float fb = ((34.5f * g - 59.0f) * g + 24.5f) * LdotH * exp2(-max(73.2f * g - 21.2f, 8.9f) * sqrt(NdotH));
    return max(fd+fb, .0f) * diffuse;
}

float3 FabricLambertDiffuse(float roughness, float3 diffuse){
    return lerp(1.0f, .5f, roughness) * diffuse;
}

float3 CalculateFd(float NdotV, float NdotL, float LdotH, float linearRoughness, float3 diffuse){
    float3 D = DisneyDiffuseRenormalized(NdotV, NdotL, LdotH, linearRoughness, diffuse);
    return D * UNITY_INV_PI;
}

float3 CalculateFdMultiScatter(float NdotV, float NdotL, float NdotH, float LdotH, float alphaG2, float3 diffuse) {
    float3 D = DisneyDiffuseMultiScatter(NdotV, NdotL, NdotH, LdotH, alphaG2, diffuse);
    return D * UNITY_INV_PI;
}

float3 CalculateFdFabric(float roughness, float3 diffuse) {
    float3 D = FabricLambertDiffuse(roughness, diffuse);
    return D * UNITY_INV_PI;
}

float3 CalculateFr(float NdotV, float NdotL, float NdotH, float LdotH, float alphaG2, float3 f0){
    float V = V_SmithGGX(NdotL, NdotV, alphaG2);
    float D = D_GGX(NdotH, alphaG2);
    float3 F = F_Schlick(LdotH, f0);
    return D * V * F * UNITY_INV_PI;
}

float3 CalculateFrMultiScatter(float NdotV, float NdotL, float NdotH, float LdotH, float alphaG2, float3 f0, float3 energyCompensation){
    return CalculateFr(NdotV, NdotL, NdotH, LdotH, alphaG2, f0) * energyCompensation;
}

float CalculateFrClearCoat(float NdotH, float LdotH, float clearCoatAlphaG2, float clearCoat, out float fc) {
    float V = V_Kelemen(LdotH);
    float D = D_GGX(NdotH, clearCoatAlphaG2);
    float F = F_Schlick(LdotH, .04f).r * clearCoat;
    fc = F;
    return D * V  * F * UNITY_INV_PI;
}

float3 CalculateFrAnisotropic(float NdotV, float NdotL, float NdotH, float LdotH, float TdotH, float BdotH, float2 atb, float TdotV, float BdotV, float TdotL, float BdotL, float3 f0) {
    float V = V_SmithGGX_Anisotropic(atb, TdotV, BdotV, TdotL, BdotL, NdotV, NdotL);
    float D = D_GGX_Anisotropic(NdotH, TdotH, BdotH, atb);
    float3 F = F_Schlick(LdotH, f0);
    return D * V * F * UNITY_INV_PI;
}

float3 CalculateFrAnisotropicMultiscatter(float NdotV, float NdotL, float NdotH, float LdotH, float TdotH, float BdotH, float2 atb, float TdotV, float BdotV, float TdotL, float BdotL, float3 f0, float3 energyCompensation) {
    return CalculateFrAnisotropic(NdotV, NdotL, NdotH, LdotH, TdotH, BdotH, atb, TdotV, BdotV, TdotL, BdotL, f0) * energyCompensation;
}

float3 CalculateFrFabric(float NdotV, float NdotL, float NdotH, float LdotH, float roughness, float3 sheen) {
    float V = V_Neubelt(NdotL, NdotV);
    // float V = V_Charlie(NdotL, NdotV, roughness);
    float D = D_Charlie(NdotH, roughness);
    float3 F = F_Schlick(LdotH, sheen);
    // return V - V_Charlie(NdotL, NdotV, roughness);
    return D * V * F * UNITY_INV_PI;
}

//////////////////////////////////////////
// Offline IBL Utility Functions        //
//////////////////////////////////////////

float3 ImportanceSampleGGX(float2 u, float3 N, float alphaG2){
    float phi = 2.0 * UNITY_PI * u.x;
    float cosTheta = sqrt((1.0f - u.y) / (1.0f + (alphaG2 * alphaG2 - 1.0f) * u.y));
    float sinTheta = sqrt(1.0f - cosTheta * cosTheta);

    float3 H;
    H.x = cos(phi) * sinTheta;
    H.y = sin(phi) * sinTheta;
    H.z = cosTheta;

    float3 up = abs(N.z) < 0.999f ? float3(.0f, .0f, 1.0f) : float3(1.0f, .0f, .0f);
    float3 tangent = normalize(cross(up, N));
    float3 bitangent = cross(N, tangent);

    float3 sampleVec = tangent * H.x + bitangent * H.y + N * H.z;
    return normalize(sampleVec);
}

float IBL_G_SmithGGX(float NdotV, float NdotL, float alphaG2){
    const float lambdaV = NdotL * sqrt((-NdotV * alphaG2 + NdotV) * NdotV + alphaG2);
    const float lambdaL = NdotV * sqrt((-NdotL * alphaG2 + NdotL) * NdotL + alphaG2);
    return (2 * NdotL) / max(lambdaL + lambdaV, .00001f);
}

float IBL_Diffuse(float NdotV, float NdotL, float LdotH, float linearRoughness){
    return DisneyDiffuseRenormalized(NdotV, NdotL, LdotH, linearRoughness);
}

float PrecomputeDiffuseL_DFG(float3 V, float NdotV, float linearRoughness){
    // float3 V = float3(sqrt(1.0f - NdotV * NdotV), .0f, NdotV);
    float r = .0f;
    const uint SAMPLE_COUNT = 2048u;
    for(uint i = 0; i < SAMPLE_COUNT; i++){
        float2 E = Hammersley2dSeq(i, SAMPLE_COUNT);
        float3 H = SampleHemisphereCosine(E.x, E.y);
        float3 L = 2.0f * dot(V, H) * H - V;

        float NdotL = saturate(L.z);
        float LdotH = saturate(dot(L,H));

        if(LdotH > .0f){
            float diffuse = IBL_Diffuse(NdotV, NdotL, LdotH, linearRoughness);
            r += diffuse;
        }
    }
    return r / (float) SAMPLE_COUNT;
}

float2 PrecomputeSpecularL_DFG(float3 V, float NdotV, float linearRoughness){
    // float3 V = float3(sqrt(1.0f - NdotV * NdotV), .0f, NdotV);
    float alphaG2 = LinearRoughnessToAlphaG2(linearRoughness);
    float2 r = .0f;
    float3 N = float3(.0f, .0f, 1.0f);
    const uint SAMPLE_COUNT = 2048u;
    for(uint i = 0; i < SAMPLE_COUNT; i++){
        float2 Xi = Hammersley2dSeq(i, SAMPLE_COUNT);
        float3 H = ImportanceSampleGGX(Xi, N, alphaG2);
        float3 L = 2.0f * dot(V, H) * H - V;

        float NdotL = saturate(L.z);
        float NdotH = saturate(H.z);
        float VdotH = saturate(dot(V,H));

        if(NdotL > .0f){
            float G = IBL_G_SmithGGX(NdotV, NdotL, alphaG2);
            float Gv = G * VdotH / NdotH;
            float Fc = pow5(1.0f - VdotH);
            r.x += Gv ;
            r.y += Gv * Fc;
        }
    }
    return r / (float) SAMPLE_COUNT;
}

float4 PrecomputeL_DFG(float NdotV, float linearRoughness){
    float3 V = float3(sqrt(1.0f - NdotV * NdotV), .0f, NdotV);
    float4 color;
    color.xy = PrecomputeSpecularL_DFG(V, NdotV, linearRoughness);
    color.z  = PrecomputeDiffuseL_DFG(V, NdotV, linearRoughness);
    color.w = 1.0f;
    return color;
}

float4 PrefilterEnvMap(TextureCube envMap, float resolution, float roughness, float3 reflectionDir){
    float alphaG2 = RoughnessToAlphaG2(roughness);
    float3 N, R, V;
    N = R = V = reflectionDir;
    float3 prefilteredColor = float3(.0f, .0f, .0f);
    float totalWeight = .0f;
    const uint SAMPLE_COUNT = 2048u;
    for(uint i = 0; i < SAMPLE_COUNT; i++){
        float2 Xi = Hammersley2dSeq(i, SAMPLE_COUNT);
        float3 H = ImportanceSampleGGX(Xi, N, alphaG2);
        float3 L = 2.0f * dot(V,H) * H - V;

        float NdotL = saturate(dot(N, L));

        if(NdotL > .0f){
            float VdotH = saturate(dot(V, H));
            float NdotH = VdotH;

            float D = D_GGX(NdotH, alphaG2) / UNITY_PI;
            float pdf = D * NdotH / (4.0f * VdotH) + .0001f;

            float omegasS = 1.0f / float(SAMPLE_COUNT) * pdf;
            float omegaP = 4.0f * UNITY_PI / (6.0f * resolution * resolution);
            float mipLevel = roughness == .0f ? .0f : .5f * log2(omegasS / omegaP);

            totalWeight += NdotL;
            prefilteredColor += envMap.SampleLevel(sampler_linear_clamp, L, mipLevel).rgb * NdotL;
        }
    }
    return float4(prefilteredColor / totalWeight, 1.0f);
}

//////////////////////////////////////////
// Runtime IBL Utility Functions        //
//////////////////////////////////////////


float3 SampleGlobalEnvMapDiffuse(float3 dir){
    dir = RotateAroundYInDegrees(dir, _GlobalEnvMapRotation);
    return _GlobalEnvMapDiffuse.SampleLevel(sampler_GlobalEnvMapDiffuse, dir, DIFF_IBL_MAX_MIP).rgb * _SkyboxIntensity;
}

float3 SampleGlobalEnvMapSpecular(float3 dir, float miplevel){
    dir = RotateAroundYInDegrees(dir, _GlobalEnvMapRotation);
    return _GlobalEnvMapSpecular.SampleLevel(sampler_GlobalEnvMapSpecular, dir, miplevel);
}


float ComputeHorizonSpecularOcclusion(float3 R, float3 vertexNormal, float horizonFade){
    const float horizon = saturate(1.0f + horizonFade * dot(R, vertexNormal));
    return horizon * horizon;
}

float ComputeHorizonSpecularOcclusion(float3 R, float3 vertexNormal) {
    const float horizon = saturate(1.0f + dot(R, vertexNormal));
    return horizon * horizon;
}


float3 EvaluateDiffuseIBL(float3 kD, float3 N, float3 diffuse, float d){
    float3 indirectDiffuse = SampleGlobalEnvMapDiffuse(N);
    indirectDiffuse *= diffuse * kD * d * UNITY_INV_PI;
    return indirectDiffuse;
}

float3 EvaluateSpecularIBL(float3 R, float linearRoughness, float3 GF, float3 energyCompensation){
    float3 indirectSpecular = SampleGlobalEnvMapSpecular(R, LinearRoughnessToMipmapLevel(linearRoughness, SPEC_IBL_MAX_MIP));
    indirectSpecular *= GF * energyCompensation;
    return indirectSpecular;
}

float3 CompensateDirectBRDF(float2 envGF, inout float3 energyCompensation, float3 specularColor){
    float3 reflectionGF = lerp(saturate(50.f * specularColor.g) * envGF.ggg, envGF.rrr, specularColor);
    energyCompensation = 1.0f + specularColor * (1.0f / envGF.r - 1.0f);

    return reflectionGF;
}

float4 CompensateDirectBRDF(float3 envGFD, inout float3 energyCompensation, float3 specularColor){
    float3 GF = CompensateDirectBRDF(envGFD.rg, energyCompensation, specularColor);
    return float4(GF, envGFD.b);
}

float4 GetDGFFromLut(inout float3 energyCompensation, float3 specularColor, float roughness, float NdotV) {
    float3 envGFD = _PreintegratedDGFLut.SampleLevel(sampler_PreintegratedDGFLut, float2(NdotV, roughness), 0).rgb;
    return CompensateDirectBRDF(envGFD, energyCompensation, specularColor);
}

float GetDFromLut(inout float3 energyCompensation, float3 specularColor, float roughness, float NdotV){
    float2 envGD = _PreintegratedDLut.SampleLevel(sampler_PreintegratedDLut, float2(NdotV, roughness), 0).rg;
    energyCompensation = 1.0f + specularColor * (1.0f / envGD.r - 1.0f);
    return envGD.g;
}

float3 GetGFFromLut(inout float3 energyCompensation, float3 specularColor, float roughness, float NdotV){
    float2 envGF = _PreintegratedGFLut.SampleLevel(sampler_PreintegratedGFLut, float2(NdotV, roughness), 0).rg;
    return CompensateDirectBRDF(envGF, energyCompensation, specularColor);
}

    
//Direct lighting
float3 PBR(float3 N, float3 V, float3 L, float3 albedo, float3 radiance, float linearRoughness, float metallic)
{
    float3 H = normalize(L + V);
    float NdotL = saturate(dot(N, L));
    float NdotV = saturate(dot(N, V));
    float NdotH = saturate(dot(N, H));
    float LdotH = saturate(dot(H,L));
    float alpha = LinearRoughnessToRoughness(linearRoughness);
    float alphaG2 = RoughnessToAlphaG2(alpha);

    float3 f0 = GetF0(albedo, metallic);
    
    float3 diffuse = (1.0f - metallic) * albedo;
    float3 fd = CalculateFdMultiScatter(NdotV, NdotL, NdotH, LdotH, alphaG2, diffuse);
    //float3 fd = CalculateFd(NdotV, NdotL, LdotH, linearRoughness, diffuse);
    float3 energyCompensation;
    float4 lut = GetDGFFromLut(energyCompensation, f0, alpha, NdotV);
    float3 fr = CalculateFrMultiScatter(NdotV, NdotL, NdotH, LdotH, alphaG2, f0, energyCompensation);
    float3 color = (fd + fr) * radiance * NdotL;
    
    return color;
}

//indirect lighting
float3 IBL(float3 N, float3 V, float3 albedo, float linearRoughness, float metallic)
{
    linearRoughness = min(linearRoughness, 0.99); 
    float roughness = LinearRoughnessToRoughness(linearRoughness);
    float3 H = normalize(N);
    float NdotV = max(dot(N, V), 0);
    float HdotV = max(dot(H, V), 0);
    float3 R = normalize(reflect(-V, N));
    
    float3 f0 = GetF0(albedo, metallic);
    float3 k_s = F_SchlickRoughness(HdotV, f0, linearRoughness);
    float3 k_d = (1.0f - k_s) * (1.0f - metallic);

    //diffuse reflection
    float3 energyCompensation;
    float4 lut = GetDGFFromLut(energyCompensation, f0, roughness, NdotV);
    float envD = lut.a;
    float3 diffuseIBL = EvaluateDiffuseIBL(k_d, N, albedo, envD);
    
    //sepcular reflection
    float3 specularIBL = EvaluateSpecularIBL(R, linearRoughness, lut.rgb, energyCompensation);

    float3 ambient = diffuseIBL + specularIBL;

    return ambient;
}

#endif