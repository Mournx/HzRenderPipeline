using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEditor;
using UnityEngine.Profiling;

public class HzRenderPipeline : RenderPipeline
{
  private RenderTexture gdepth;
  private RenderTexture[] gbuffers = new RenderTexture[4];
  private RenderTargetIdentifier[] gbufferID = new RenderTargetIdentifier[4];
  private RenderTexture hizBuffer;
  
  private Matrix4x4 vpMatrix;
  private Matrix4x4 vpMatrixInv;
  private Matrix4x4 vpMatrixPrev;
  private Matrix4x4 vpMatrixInvPrev;
  
  //IBL贴图
  public Cubemap globalEnvMapDiffuse;
  public Cubemap globalEnvMapSpecular;
  public Texture brdfLut;
  
  //噪声图
  public Texture blueNoiseTex;
  
  //阴影设置
  public int shadowMapResolution = 1024;
  private CSM csm;
  public CSMSettings csmSettings;
  private RenderTexture[] shadowTextures = new RenderTexture[4];
  private RenderTexture shadowMask;
  private RenderTexture shadowStrength;

  private ClusterLight clusterLight;

  public InstanceData[] instanceDatas;
  
  public HzRenderPipeline()
  {
    QualitySettings.vSyncCount = 0;
    Application.targetFrameRate = 60;
    
    gdepth = new RenderTexture(Screen.width, Screen.height, 24, RenderTextureFormat.Depth, RenderTextureReadWrite.Linear);
    gbuffers[0] = new RenderTexture(Screen.width, Screen.height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear);
    gbuffers[1] = new RenderTexture(Screen.width, Screen.height, 0, RenderTextureFormat.ARGB2101010, RenderTextureReadWrite.Linear);
    gbuffers[2] = new RenderTexture(Screen.width, Screen.height, 0, RenderTextureFormat.ARGB64, RenderTextureReadWrite.Linear);
    gbuffers[3] = new RenderTexture(Screen.width, Screen.height, 0, RenderTextureFormat.ARGBFloat, RenderTextureReadWrite.Linear);
    for (int i = 0; i < 4; i++)
      gbufferID[i] = gbuffers[i];
    
    //Hi-z buffer
    int hizSize = Mathf.NextPowerOfTwo(Mathf.Max(Screen.width, Screen.height));
    hizBuffer = new RenderTexture(hizSize, hizSize, 0, RenderTextureFormat.RHalf);
    hizBuffer.autoGenerateMips = false;
    hizBuffer.useMipMap = true;
    hizBuffer.filterMode = FilterMode.Point;
    
    //创建阴影贴图
    shadowMask = new RenderTexture(Screen.width / 4, Screen.height / 4, 0, RenderTextureFormat.R8, RenderTextureReadWrite.Linear);
    shadowStrength = new RenderTexture(Screen.width , Screen.height, 0, RenderTextureFormat.R8, RenderTextureReadWrite.Linear);
    for (int i = 0; i < 4; i++)
      shadowTextures[i] = new RenderTexture(shadowMapResolution, shadowMapResolution, 24, RenderTextureFormat.Depth, RenderTextureReadWrite.Linear);

    csm = new CSM();
    clusterLight = new ClusterLight();
  }
  
  protected override void Render(ScriptableRenderContext context, Camera[] cameras)
  {
    Camera camera = cameras[0];
    
    Shader.SetGlobalFloat("_near", camera.nearClipPlane);
    Shader.SetGlobalFloat("_far", camera.farClipPlane);
    Shader.SetGlobalFloat("_screenWidth", Screen.width);
    Shader.SetGlobalFloat("_screenHeight", Screen.height);
    Shader.SetGlobalTexture("_noiseTex", blueNoiseTex);
    Shader.SetGlobalFloat("_noiseTexResolution", blueNoiseTex.width);
    
    //gbuffer
    Shader.SetGlobalTexture("_gdepth", gdepth);
    Shader.SetGlobalTexture("_hizBuffer", hizBuffer);
    for(int i = 0; i < 4; i++)
      Shader.SetGlobalTexture("_GT" + i, gbuffers[i]);
    
    //camera matrix
    Matrix4x4 viewMatrix = camera.worldToCameraMatrix;
    Matrix4x4 projMatrix = GL.GetGPUProjectionMatrix(camera.projectionMatrix, false);
    vpMatrix = projMatrix * viewMatrix;
    vpMatrixInv = vpMatrix.inverse;
    Shader.SetGlobalMatrix("_vpMatrix", vpMatrix);
    Shader.SetGlobalMatrix("_vpMatrixInv", vpMatrixInv);
    Shader.SetGlobalMatrix("_vpMatrixPrev", vpMatrixPrev);
    Shader.SetGlobalMatrix("_vpMatrixInvPrev", vpMatrixInvPrev);
    
    Shader.SetGlobalTexture("_GlobalEnvMapDiffuse", globalEnvMapDiffuse);
    Shader.SetGlobalTexture("_GlobalEnvMapSpecular", globalEnvMapSpecular);
    Shader.SetGlobalTexture("_PreintegratedDGFLut", brdfLut);

    //设置csm
    Shader.SetGlobalFloat("_orthoDistance", camera.farClipPlane - camera.nearClipPlane);
    Shader.SetGlobalFloat("_shadowMapResolution", shadowMapResolution);
    Shader.SetGlobalTexture("_shadowStrength", shadowStrength);
    Shader.SetGlobalTexture("_shadowMask", shadowMask);
    for (int i = 0; i < 4; i++)
    {
      Shader.SetGlobalTexture("_shadowtex"+i, shadowTextures[i]);
      Shader.SetGlobalFloat("_split"+i, csm.splts[i]);
    }
    
    
    ClusterLightingPass(context, camera);
    
    ShadowCastingPass(context, camera);
    
    GbufferPass(context, camera);
    
    InstanceDrawPass(context, Camera.main);

    if (!Handles.ShouldRenderGizmos())
    {
      HizPass(context, camera);
      vpMatrixPrev = vpMatrix;
    }
    
    ShadowMappingPass(context, camera);
    
    LightPass(context, camera);
    
    // skybox and Gizmos
    context.DrawSkybox(camera);
    if (Handles.ShouldRenderGizmos())
    {
      context.DrawGizmos(camera, GizmoSubset.PreImageEffects);
      context.DrawGizmos(camera, GizmoSubset.PostImageEffects);
    }
    
    context.Submit();
  }

  void GbufferPass(ScriptableRenderContext context, Camera camera)
  {
    Profiler.BeginSample("gbufferDraw");
    
    context.SetupCameraProperties(camera);
    CommandBuffer cmd = new CommandBuffer();
    cmd.name = "gbuffer";
    
    cmd.SetRenderTarget(gbufferID, gdepth);
    cmd.ClearRenderTarget(true, true, Color.red);
    context.ExecuteCommandBuffer(cmd);
    cmd.Clear();

    camera.TryGetCullingParameters(out var cullingParameters);
    var cullingResults = context.Cull(ref cullingParameters);

    ShaderTagId shaderTagId = new ShaderTagId("gbuffer");
    SortingSettings sortingSettings = new SortingSettings(camera);
    DrawingSettings drawingSettings = new DrawingSettings(shaderTagId, sortingSettings);
    FilteringSettings filteringSettings = FilteringSettings.defaultValue;
    
    context.DrawRenderers(cullingResults, ref drawingSettings, ref filteringSettings);
    context.Submit();
    
    Profiler.EndSample();
  }
  
  void LightPass(ScriptableRenderContext context, Camera camera)
  {
    Profiler.BeginSample("lightingPass");
    
    CommandBuffer cmd = new CommandBuffer();
    cmd.name = "lightpass";
    
    Material mat = new Material(Shader.Find("HzRP/lightpass"));
    cmd.Blit(gbufferID[0], BuiltinRenderTextureType.CameraTarget, mat);
    context.ExecuteCommandBuffer(cmd);
    cmd.Clear();
    
    context.Submit();
    Profiler.EndSample();
  }

  //阴影贴图pass
  void ShadowCastingPass(ScriptableRenderContext context, Camera camera)
  {
    Profiler.BeginSample("ShadowCastingPass");
    
    Light light = RenderSettings.sun;
    Vector3 lightDir = light.transform.rotation * Vector3.forward;
    
    csm.Update(camera, lightDir, csmSettings);
    csmSettings.Set();
    
    csm.SaveMainCameraSettings(ref camera);
    for (int level = 0; level < 4; level++)
    {
      csm.ConfigCameraToShadowSpace(ref camera, lightDir, level, shadowMapResolution);

      Matrix4x4 v = camera.worldToCameraMatrix;
      Matrix4x4 p = GL.GetGPUProjectionMatrix(camera.projectionMatrix, false);
      Shader.SetGlobalMatrix("_shadowVpMatrix"+level, p * v);
      Shader.SetGlobalFloat("_orthoWidth"+level, csm.orthoWidths[level]);
      
      CommandBuffer cmd = new CommandBuffer();
      cmd.name = "shadowmap" + level;
      
      context.SetupCameraProperties(camera);
      cmd.SetRenderTarget(shadowTextures[level]);
      cmd.ClearRenderTarget(true, true, Color.clear);
      context.ExecuteCommandBuffer(cmd);
      cmd.Clear();

      camera.TryGetCullingParameters(out var cullingParameters);
      var cullingResults = context.Cull(ref cullingParameters);

      ShaderTagId shaderTagId = new ShaderTagId("depthOnly");
      SortingSettings sortingSettings = new SortingSettings(camera);
      DrawingSettings drawingSettings = new DrawingSettings(shaderTagId, sortingSettings);
      FilteringSettings filteringSettings = FilteringSettings.defaultValue;
      
      context.DrawRenderers(cullingResults, ref drawingSettings, ref filteringSettings);
      context.Submit();
    }
    csm.RevertMainCameraSettings(ref camera);
    
    Profiler.EndSample();
  }
  
  //阴影计算 pass, 输出阴影强度图
  void ShadowMappingPass(ScriptableRenderContext context, Camera camera)
  {
    Profiler.BeginSample("ShadowMapping Pass");
    
    CommandBuffer cmd = new CommandBuffer();
    cmd.name = "shadowmappingpass";

    RenderTexture tempTex1 = RenderTexture.GetTemporary(Screen.width / 4, Screen.height / 4, 0, RenderTextureFormat.R8, RenderTextureReadWrite.Linear);
    RenderTexture tempTex2 = RenderTexture.GetTemporary(Screen.width / 4, Screen.height / 4, 0, RenderTextureFormat.R8, RenderTextureReadWrite.Linear);
    RenderTexture tempTex3 = RenderTexture.GetTemporary(Screen.width, Screen.height, 0, RenderTextureFormat.R8, RenderTextureReadWrite.Linear);

    if (csmSettings.usingShadowMask)
    {
      cmd.Blit(gbufferID[0], tempTex1, new Material(Shader.Find("HzRP/preshadowmappingpass")));
      cmd.Blit(tempTex1, tempTex2, new Material(Shader.Find("HzRP/blurNx1")));
      cmd.Blit(tempTex2, shadowMask, new Material(Shader.Find("HzRP/blur1xN")));
    }
    
    cmd.Blit(gbufferID[0], tempTex3, new Material(Shader.Find("HzRP/shadowmappingpass")));
    cmd.Blit(tempTex3, shadowStrength, new Material(Shader.Find("HzRP/blurNxN")));
    
    RenderTexture.ReleaseTemporary(tempTex1);
    RenderTexture.ReleaseTemporary(tempTex2);
    RenderTexture.ReleaseTemporary(tempTex3);
    
    context.ExecuteCommandBuffer(cmd);
    cmd.Clear();
    context.Submit();
    
    Profiler.EndSample();
  }

  void ClusterLightingPass(ScriptableRenderContext context, Camera camera)
  {
    camera.TryGetCullingParameters(out var cullingParameters);
    var cullingResults = context.Cull(ref cullingParameters);
    
    clusterLight.UpdateLightBuffer(cullingResults.visibleLights.ToArray());
    
    clusterLight.ClusterGenerate(camera);
    
    clusterLight.LightAssign();
    
    clusterLight.SetShaderParameters();
  }

  void InstanceDrawPass(ScriptableRenderContext context, Camera camera)
  {
    Profiler.BeginSample("InstanceDraw Pass");
    
    CommandBuffer cmd = new CommandBuffer();
    cmd.name = "instance gbuffer";
    cmd.SetRenderTarget(gbufferID, gdepth);
    
    // Draw Instance
    ComputeShader computeShader = Resources.Load<ComputeShader>("Shader/InstanceCulling");
    for(int i = 0; i < instanceDatas.Length; i++)
      InstanceDraw.Draw(instanceDatas[i], Camera.main, computeShader, vpMatrixPrev, hizBuffer, ref cmd);
    
    context.ExecuteCommandBuffer(cmd);
    cmd.Clear();
    context.Submit();
    
    Profiler.EndSample();
  }

  void HizPass(ScriptableRenderContext context, Camera camera)
  {
    Profiler.BeginSample("Hiz Pass");

    CommandBuffer cmd = new CommandBuffer();
    cmd.name = "hiz pass";

    int size = hizBuffer.width;
    int mipNums = (int)Mathf.Log(size, 2);
    RenderTexture[] mips = new RenderTexture[mipNums];
    for (int i = 0; i < mips.Length; i++)
    {
      int subSize = size / (int)Mathf.Pow(2, i);
      mips[i] = RenderTexture.GetTemporary(subSize, subSize, 0, RenderTextureFormat.RHalf, RenderTextureReadWrite.Linear);
      mips[i].filterMode = FilterMode.Point;
    }
    
    // Generate mipmap
    Material mat = new Material(Shader.Find("HzRP/hizBlit"));
    cmd.Blit(gdepth, mips[0]);
    for(int i = 1; i < mips.Length; i++)
      cmd.Blit(mips[i-1], mips[i], mat);

    for (int i = 0; i < mips.Length; i++)
    {
      cmd.CopyTexture(mips[i],0,0,hizBuffer,0,i);
      RenderTexture.ReleaseTemporary(mips[i]);
    }
    
    context.ExecuteCommandBuffer(cmd);
    cmd.Clear();
    context.Submit();
    
    Profiler.EndSample();
  }
}
