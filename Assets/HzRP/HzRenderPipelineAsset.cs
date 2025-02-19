using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

[CreateAssetMenu(menuName = "Rendering/HzRenderPipeline")]
public class HzRenderPipelineAsset : RenderPipelineAsset
{
    public Cubemap diffuseIBL;
    public Cubemap specularIBL;
    public Texture brdfLut;

    public Texture blueNoiseTex;
    
    [SerializeField]
    public CSMSettings csmSettings;
    public InstanceData[] instanceDatas;
    
    protected override RenderPipeline CreatePipeline()
    {
        HzRenderPipeline rp =  new HzRenderPipeline();

        rp.diffuseIBL = diffuseIBL;
        rp.specularIBL = specularIBL;
        rp.brdfLut = brdfLut;

        rp.blueNoiseTex = blueNoiseTex;
        
        rp.csmSettings = csmSettings;
        rp.instanceDatas = instanceDatas;
        
        return rp;
    }
}
