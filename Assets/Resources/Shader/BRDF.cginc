#define PI 3.14159265358

#define DEFAULT_REFLECTANCE (.4f)
#define MIN_LINEAR_ROUGHNESS (.045f)
#define MIN_ROUGHNESS (.002025f)

#define SPEC_IBL_MAX_MIP (6u)
#define DIFF_IBL_MAX_MIP (11u)

#include "random.cginc"

//////////////////////////////////////////
// Image Based Lighting Variables       //
//////////////////////////////////////////

float _GlobalEnvMapRotation;
float _SkyboxMipLevel;
float _SkyboxIntensity;


sampler sampler_linear_clamp;
/*
Texture2D _PreintegratedDGFLut;
sampler sampler_PreintegratedDGFLut;
Texture2D _PreintegratedDLut;
sampler sampler_PreintegratedDLut;
Texture2D _PreintegratedGFLut;
sampler sampler_PreintegratedGFLut;

TextureCube _GlobalEnvMapSpecular;
sampler sampler_GlobalEnvMapSpecular;
TextureCube _GlobalEnvMapDiffuse;
sampler sampler_GlobalEnvMapDiffuse;
*/
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

float3 RotateAroundYInDegrees(float3 vertex, float degrees) {
    float alpha = degrees * PI / 180.0f;
    float sina, cosa;
    sincos(alpha, sina, cosa);
    float2x2 m = float2x2(cosa, -sina, sina, cosa);
    return float3(mul(m, vertex.xz), vertex.y).xzy;
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
float D_GGX(float NdotH, float a){
    float a2 = a * a;
    float NdotH2 = NdotH * NdotH;

    float nom = a2;
    float denom = (NdotH2 * (a2 - 1.0) + 1.0);
    denom = PI * denom * denom;

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
    float phi = 2.0 * PI * u.x;
    float cosTheta = sqrt((1.0f - u.y) / (1.0f + (alphaG2 * alphaG2 - 1.0f) * u.y));
    float sinTheta = sqrt(1.0f - cosTheta * cosTheta);

    float3 H;
    H.x = cos(phi) * sinTheta;
    H.y = sin(phi) * sinTheta;
    H.z = cosTheta;

    float3 up = abs(N.z) < 0.999f ? float3(.0f, .0f, 1.0f) : float3(1.0f, .0f, .0f);
    float3 tangent = normalize(cross(up, N));
    float3 bitangent = cross(N, tangent);

    return tangent * H.x + bitangent * H.y + N * H.z;
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
            r.x += Gv;
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

            float D = D_GGX(NdotH, alphaG2) / PI;
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

/*
float3 SampleGlobalEnvMapDiffuse(float3 dir){
    dir = RotateAroundYInDegrees(dir, _GlobalEnvMapRotation);
    return _GlobalEnvMapDiffuse.SampleLevel(sampler_GlobalEnvMapDiffuse, dir, DIFF_IBL_MAX_MIP).rgb * _SkyboxIntensity;
}

float3 SampleGlobalEnvMapSpecular(float3 dir, float miplevel){
    dir = RotateAroundYInDegrees(dir, _GlobalEnvMapRotation);
    return _GlobalEnvMapSpecular.SampleLevel(sampler_GlobalEnvMapSpecular, dir, miplevel);
}
*/

float ComputeHorizonSpecularOcclusion(float3 R, float3 vertexNormal, float horizonFade){
    const float horizon = saturate(1.0f + horizonFade * dot(R, vertexNormal));
    return horizon * horizon;
}

float ComputeHorizonSpecularOcclusion(float3 R, float3 vertexNormal) {
    const float horizon = saturate(1.0f + dot(R, vertexNormal));
    return horizon * horizon;
}

/*
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
    float3 reflectionGF = lerp(envGF.ggg, envGF.rrr, specularColor);
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
*/
    
//Direct lighting
float3 PBR(float3 N, float3 V, float3 L, float3 albedo, float3 radiance, float linearRoughness, float metallic)
{
    //保证平滑物体也有高光
    linearRoughness = max(linearRoughness, 0.05); 

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
    float3 fr = CalculateFr(NdotV, NdotL, NdotH, LdotH, alphaG2, f0);
    
    float3 color = (fd + fr) * radiance * NdotL;
    
    return color;
}

//indirect lighting
float3 IBL(float3 N, float3 V, float3 albedo, float linearRoughness, float metallic, samplerCUBE _diffuseIBL, samplerCUBE _specularIBL, sampler2D
 _brdfLut)
{
    linearRoughness = min(linearRoughness, 0.99);

    float3 H = normalize(N);
    float NdotV = max(dot(N, V), 0);
    float HdotV = max(dot(H, V), 0);
    float3 R = normalize(reflect(-V, N));
    
    float3 f0 = GetF0(albedo, metallic);
    float3 k_s = F_SchlickRoughness(HdotV, f0, linearRoughness);
    float3 k_d = (1.0f - k_s) * (1.0f - metallic);

    //diffuse reflection
    float3 IBL_d = texCUBE(_diffuseIBL, N).rgb;
    float3 diffuse = k_d * albedo * IBL_d * UNITY_INV_PI;
    
    //sepcular reflection
    float lod = LinearRoughnessToMipmapLevel(linearRoughness, SPEC_IBL_MAX_MIP);
    float3 IBL_s = texCUBElod(_specularIBL, float4(R, lod)).rgb;
    float2 brdf = tex2D(_brdfLut, float2(NdotV, linearRoughness)).rg;
    float3 energyCompensation = 1.0f + f0 * (1.0f / brdf.r - 1.0f);
    float3 specular = IBL_s * (f0 * brdf.x + brdf.y) * energyCompensation;

    float3 ambient = diffuse + specular;

    return ambient;
}
