using System;
using System.Collections;
using System.Collections.Generic;
using HzRenderPipeline.Runtime.Cameras;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEditor;
using UnityEngine.Profiling;

namespace HzRenderPipeline.Runtime
{
  public class HzRenderPipeline : RenderPipeline
  {

    public static HzRenderPipeline instance { get; private set; }

    public static bool ReversedZ { get; private set; }

    public static HzRenderPipelineSettings settings;

    private static readonly Dictionary<Camera, CameraRenderer> cameraRenderers = new Dictionary<Camera, CameraRenderer>(2);

    private static readonly List<KeyValuePair<Camera, CameraRenderer>> tempCameras = new List<KeyValuePair<Camera, CameraRenderer>>(10);

    private static readonly Dictionary<CommandBuffer, Action> independentCMDRequests = new Dictionary<CommandBuffer, Action>();

    public bool IsOnFirstFrame => _frameNum == 1; // start at 1
    private int _frameNum;
    public HzRenderPipeline(HzRenderPipelineSettings settings)
    {
      QualitySettings.vSyncCount = 0;
      Application.targetFrameRate = 60;
      GraphicsSettings.useScriptableRenderPipelineBatching = settings.enableSRPBatching;
      GraphicsSettings.lightsUseLinearIntensity = true;

      instance = this;
      HzRenderPipeline.settings = settings;
      ReversedZ = SystemInfo.usesReversedZBuffer;

      SetupUniformData();
    }

    #region Static Methods

    public static void RequestCameraCheck() {
      foreach (var pair in cameraRenderers) {
        if(!pair.Key || pair.Value == null) tempCameras.Add(pair);
      }

      foreach (var pair in tempCameras) {
        var cam = pair.Value;
        cameraRenderers.Remove(pair.Key);
        cam.Dispose();
      }
      
      tempCameras.Clear();
    }

    public static bool RemoveCamera(Camera camera) {
      if (!camera) return false;
      if (cameraRenderers.TryGetValue(camera, out var renderer)) {
        renderer.Dispose();
        return cameraRenderers.Remove(camera);
      }

      return false;
    }

    public static bool RegisterCamera(Camera camera) => RegisterCamera(camera, CameraRenderer.GetCameraType(camera));

    public static bool RegisterCamera(Camera camera, HzCameraType type) {
      if (cameraRenderers.ContainsKey(camera)) return false;
      cameraRenderers.Add(camera, CameraRenderer.CreateCameraRenderer(camera, type));
      return true;
    }
    
    private static void ExecuteIndependentCommandBufferRequest(ScriptableRenderContext context) {
      if (independentCMDRequests.Count == 0) return;
			
      foreach (var pair in independentCMDRequests) context.ExecuteCommandBuffer(pair.Key);
			
      context.Submit();

      foreach (var pair in independentCMDRequests) {
        pair.Key.Release();
        pair.Value();
      }
			
      independentCMDRequests.Clear();
    }
    #endregion

    protected override void Render(ScriptableRenderContext context, Camera[] cameras)
    {
      RequestCameraCheck();
      
      var screenWidth = Screen.width;
      var screenHeight = Screen.height;
      
      _frameNum++;
      
      BeginFrameRendering(context, cameras);

      foreach (var camera in cameras)
      {
        var pixelWidth = camera.pixelWidth;
        var pixelHeight = camera.pixelHeight;

        var cameraRenderer = GetCameraRenderer(camera);
        cameraRenderer.PreUpdate();
        cameraRenderer.SetResolutionAndRatio(pixelWidth, pixelHeight, 1f, 1f);
        
        BeginCameraRendering(context, camera);
        
        cameraRenderer.Render(context);
        
        EndCameraRendering(context, camera);
        
        cameraRenderer.PostUpdate();
      }
      
      EndFrameRendering(context, cameras);
    }

    public void SetupUniformData()
    {
      Shader.SetGlobalTexture("_GlobalEnvMapDiffuse", settings.globalEnvMapDiffuse);
      Shader.SetGlobalTexture("_GlobalEnvMapSpecular", settings.globalEnvMapSpecular);
      Shader.SetGlobalFloat("_GlobalEnvMapRotation", settings.globalEnvMapRotation);
      Shader.SetGlobalFloat("_SkyboxMipLevel", settings.skyboxMipLevel);
      Shader.SetGlobalFloat("_SkyboxIntensity", settings.skyboxIntensity);
      Shader.SetGlobalTexture("_PreintegratedDGFLut", settings.brdfLut);
      
      Shader.SetGlobalFloat("_screenWidth", Screen.width);
      Shader.SetGlobalFloat("_screenHeight", Screen.height);
      Shader.SetGlobalTexture("_noiseTex", settings.blueNoiseTex);
      Shader.SetGlobalFloat("_noiseTexResolution", settings.blueNoiseTex.width);
    }

    internal CameraRenderer GetCameraRenderer(Camera camera) {
      var cameraType = CameraRenderer.GetCameraType(camera);
      if (!cameraRenderers.TryGetValue(camera, out var renderer))
      {
        renderer = CameraRenderer.CreateCameraRenderer(camera, cameraType);
        cameraRenderers.Add(camera, renderer);
      }
      else {
        if (cameraType != renderer.cameraType) {
          var oldRenderer = renderer;
          renderer = CameraRenderer.CreateCameraRenderer(camera, cameraType);
          cameraRenderers[camera] = renderer;
          oldRenderer.Dispose();
        }
      }

      return renderer;
    }
  }
  [Serializable]
  public struct PackedRTHandleProperties
  {
    public int4 viewportSize;
    public int4 rtSize;
    public float4 rtHandleScale;
  }

  [Serializable]
  public struct CameraData
  {
    public float4 cameraPosWS;
    public float4 cameraFwdWS;
    public float4 screenSize;
    public float4x4 frustumCornersWS;
    public float4x4 prevFrustumCornersWS;
    public PackedRTHandleProperties _rtHandleProps;
  }
}