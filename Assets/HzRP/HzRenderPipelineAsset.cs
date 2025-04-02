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
            minHistoryWeight = .6f, maxHistoryWeight = .95f, minClipScale = .5f, maxClipScale = 1.25f, 
            minVelocityRejection = 1f, velocityRejectionScale = 0f, minDepthRejection = 1f, 
            minSharpness = .25f, maxSharpness = .25f
        };
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
        [Range(0f, 1f)] public float jitterSpread;
        [Range(0f, 1f)] public float minHistoryWeight;
        [Range(0f, 1f)] public float maxHistoryWeight;
        [Range(.05f, 6f)] public float minClipScale;
        [Range(.05f, 6f)] public float maxClipScale;

        [Tooltip("Used for anti-flickering")] [Range(.05f, 12f)]
        public float staticClipScale;

        [Range(0f, 1f)] public float minVelocityRejection;
        [Range(0f, 10f)] public float velocityRejectionScale;

        [Tooltip("Distance in eye space")] [Range(0f, 50f)]
        public float minDepthRejection;

        [Range(0f, 2f)] public float resamplingSharpness;
        [Range(0f, 0.1f)] public float minSharpness;
        [Range(0f, 0.1f)] public float maxSharpness;
        [Range(0f, 10f)] public float motionSharpeningFactor;
        [Range(0f, 0.5f)] public float minEdgeBlurness;
        [Range(0f, 1f)] public float invalidHistoryThreshold;

        public Vector4 TaaParams0 => new(minHistoryWeight, maxHistoryWeight, minClipScale, maxClipScale);

        public Vector4 TaaParams1 => new(minVelocityRejection, velocityRejectionScale, minDepthRejection,
            resamplingSharpness);

        public Vector4 TaaParams2 => new(minSharpness, maxSharpness, motionSharpeningFactor, staticClipScale);
        public Vector4 TaaParams3 => new(minEdgeBlurness, invalidHistoryThreshold, 0, 0);

        public Matrix4x4 TaaParams
        {
            get
            {
                var mat = new Matrix4x4();
                mat.SetRow(0, TaaParams0);
                mat.SetRow(1, TaaParams1);
                mat.SetRow(2, TaaParams2);
                mat.SetRow(3, TaaParams3);
                return mat;
            }
        }
    }
    public enum JitterNum
    {
        _2 = 2,
        _4 = 4,
        _8 = 8,
        _16 = 16
    }
}