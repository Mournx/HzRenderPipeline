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

