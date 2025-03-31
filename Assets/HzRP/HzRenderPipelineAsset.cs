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
        public Texture brdfLut;

        public Cubemap globalEnvMapDiffuse;
        public Cubemap globalEnvMapSpecular;
        [Range(0f, 360.0f)] public float globalEnvMapRotation;
        [Range(0f, 11.0f)] public float skyboxMipLevel;
        [Range(0f, 3f)] public float skyboxIntensity = 1.0f;

        public Texture blueNoiseTex;

        [SerializeField] public CSMSettings csmSettings;
        public InstanceData[] instanceDatas;
        public TemporalAntiAliasingSettings taaSettings;

        protected override RenderPipeline CreatePipeline()
        {
            HzRenderPipeline rp = new HzRenderPipeline();

            rp.globalEnvMapDiffuse = globalEnvMapDiffuse;
            rp.globalEnvMapSpecular = globalEnvMapSpecular;
            rp.skyboxIntensity = skyboxIntensity;
            rp.skyboxMipLevel = skyboxMipLevel;
            rp.globalEnvMapRotation = globalEnvMapRotation;
            rp.brdfLut = brdfLut;

            rp.blueNoiseTex = blueNoiseTex;

            rp.csmSettings = csmSettings;
            rp.taaSettings = taaSettings;
            rp.instanceDatas = instanceDatas;

            return rp;
        }

        public enum JitterNum
        {
            _2 = 2,
            _4 = 4,
            _8 = 8,
            _16 = 16
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
    }
}