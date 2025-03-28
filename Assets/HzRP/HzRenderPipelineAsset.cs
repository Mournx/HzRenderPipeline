using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

[CreateAssetMenu(menuName = "Hz Render Pipeline/ HzRP Asset")]
public class HzRenderPipelineAsset : RenderPipelineAsset
{
    public Texture brdfLut;
    
    public Cubemap globalEnvMapDiffuse;
    public Cubemap globalEnvMapSpecular;
    [Range(0f, 360.0f)]
    public float globalEnvMapRotation;
    [Range(0f, 11.0f)]
    public float skyboxMipLevel;
    [Range(0f, 3f)]
    public float skyboxIntensity = 1.0f;
    
    public Texture blueNoiseTex;
    
    [SerializeField]
    public CSMSettings csmSettings;
    public InstanceData[] instanceDatas;
    
    protected override RenderPipeline CreatePipeline()
    {
        HzRenderPipeline rp =  new HzRenderPipeline();

        rp.globalEnvMapDiffuse = globalEnvMapDiffuse;
        rp.globalEnvMapSpecular = globalEnvMapSpecular;
        rp.brdfLut = brdfLut;

        rp.blueNoiseTex = blueNoiseTex;
        
        rp.csmSettings = csmSettings;
        rp.instanceDatas = instanceDatas;
        
        return rp;
    }
}
