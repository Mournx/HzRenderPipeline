using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace HzRenderPipeline.Runtime
{
    [CreateAssetMenu(menuName = "Hz Render Pipeline/ HzRP Asset")]
    public class HzRenderPipelineAsset : RenderPipelineAsset
    {
        public HzRenderPipelineSettings settings;

        protected override RenderPipeline CreatePipeline() => new HzRenderPipeline(settings);

    }

    [Serializable]
    public class HzRenderPipelineSettings
    {
        [Header("Editor")]
        public bool enableTaaInEditor = true;
        [Header("Batch Settings")]
        public bool enableAutoInstancing = true;
        public bool enableSRPBatching = true;
        [Header("Image Based Lighting")]
        public Texture brdfLut;
        public Cubemap globalEnvMapDiffuse;
        public Cubemap globalEnvMapSpecular;
        [Range(0f, 360.0f)] public float globalEnvMapRotation;
        [Range(0f, 11.0f)] public float skyboxMipLevel;
        [Range(0f, 3f)] public float skyboxIntensity = 1.0f;
        
        [Header("Utilities")]
        public Texture blueNoiseTex;

        [Header("Cascade ShadowMapping Settings")] 
        [SerializeField]
        public CSMSettings csmSettings = new () {
            maxDistance = 500, usingShadowMask = false,
            level0 = new(){depthNormalBias = 0.1f, shadingPointNormalBias = 0.005f, pcssSearchRadius = 1.0f, pcssFilterRadius = 7.0f},
            level1 = new(){depthNormalBias = 0.1f, shadingPointNormalBias = 0.005f, pcssSearchRadius = 1.0f, pcssFilterRadius = 7.0f},
            level2 = new(){depthNormalBias = 0.1f, shadingPointNormalBias = 0.005f, pcssSearchRadius = 1.0f, pcssFilterRadius = 7.0f},
            level3 = new(){depthNormalBias = 0.1f, shadingPointNormalBias = 0.005f, pcssSearchRadius = 1.0f, pcssFilterRadius = 7.0f},
        };
        [Header("Instance Data")]
        public InstanceData[] instanceDatas;
        [Header("Anti Aliasing")]
        public TemporalAntiAliasingSettings taaSettings = new() {
            enabled = true, jitterNum = JitterNum._8, jitterSpread = .75f, 
            sharpness = .25f, stationaryBlending =  0.95f, motionBlending = 0.9f,
            stationaryAABBScale = 1.25f, motionAABBScale = 0.5f
        };
        [Header("Color Grading & Tonemapping")]
        public ColorGradingSettings colorSettings = new () { colorFilter = Color.white };
        public TonemappingSettings  tonemappingSettings = new () { tonemappingType = TonemappingType.ACES };
    }
    
    [Serializable]
    public struct ShadowSettings
    {
        public float shadingPointNormalBias;
        public float depthNormalBias;
        public float pcssSearchRadius ;
        public float pcssFilterRadius ;
    }

    [Serializable]
    public struct CSMSettings
    {
        public float maxDistance;
        public bool usingShadowMask;
        public ShadowSettings level0;
        public ShadowSettings level1;
        public ShadowSettings level2;
        public ShadowSettings level3;

        public void Set()
        {
            ShadowSettings[] levels = { level0, level1, level2, level3 };
            for (int i = 0; i < 4; i++)
            {
                Shader.SetGlobalFloat("_shadingPointNormalBias" + i, levels[i].shadingPointNormalBias);
                Shader.SetGlobalFloat("_depthNormalBias" + i, levels[i].depthNormalBias);
                Shader.SetGlobalFloat("_pcssSearchRadius" + i, levels[i].pcssSearchRadius);
                Shader.SetGlobalFloat("_pcssFilterRadius" + i, levels[i].pcssFilterRadius);
            }
            Shader.SetGlobalFloat("_usingShadowMask", usingShadowMask ? 1.0f : 0.0f);
            Shader.SetGlobalFloat("_csmMaxDistance", maxDistance);
        }
    }

    
    [Serializable]
    public struct TemporalAntiAliasingSettings
    {
        public bool enabled;
        public JitterNum jitterNum;
        [Tooltip("The diameter (in texels) inside which jitter samples are spread. Smaller values result in crisper but more aliased output, while larger values result in more stable but blurrier output.")]
        [Range(0f, 1f)] public float jitterSpread;
        
        [Tooltip("Controls the amount of sharpening applied to the color buffer. High values may introduce dark-border artifacts.")]
        [Range(0f, 3f)]
        public float sharpness;

        [Tooltip("The blend coefficient for a stationary fragment. Controls the percentage of history sample blended into the final color.")]
        [Range(0f, 0.99f)]
        public float stationaryBlending ;

        [Tooltip("The blend coefficient for a fragment with significant motion. Controls the percentage of history sample blended into the final color.")]
        [Range(0f, 0.99f)]
        public float motionBlending ;
        [Tooltip("Screen Space AABB Bounding for stationary state(Larger will take less flask but more ghost)")]
        [Range(0.05f, 6f)]
        public float stationaryAABBScale;
        [Tooltip("Screen Space AABB Bounding for motion state(Larger will take less flask but more ghost)")]
        [Range(0.05f, 6f)]
        public float motionAABBScale ;
    }

    [Serializable]
    public struct ColorGradingSettings
    {
        [Range(-10f, 10f)] public float postExposure;
        [Range(-100f, 100f)] public float contrast;
        [ColorUsage(false, true)] public Color colorFilter;
        [Range(-180f, 180f)] public float hueShift;
        [Range(-100f, 100f)] public float saturation;
    }

    [Serializable]
    public struct TonemappingSettings
    {
        public TonemappingType tonemappingType;
        public Shader tonemappingShader;
    }
    public enum JitterNum
    {
        _2 = 2,
        _4 = 4,
        _8 = 8,
        _16 = 16
    }
    public enum TonemappingType
    {
       None = 0,
       Reinhard = 1,
       Neutral = 2,
       ACES = 3
    }
}