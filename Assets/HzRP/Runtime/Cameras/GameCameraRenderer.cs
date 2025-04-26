using System;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;

namespace HzRenderPipeline.Runtime.Cameras {

    public unsafe class GameCameraRenderer : CameraRenderer {

        #region Pipeline Callbacks

        public event Action beforeCull;
        public event Action beforePostProcess;
        public event Action afterLastPass;

        #endregion

        protected bool _enableTaa = true;
        protected string _rendererDesc;

        private readonly BufferedRTHandleSystem _historyBuffers = new();
        private RTHandle gdepth;
        private RTHandle[] gbuffers = new RTHandle[4];
        private RTHandle hizBuffer;
        private RTHandle _colorTex;
        private RTHandle _prevColorTex;
        private RTHandle _displayTex;

        //阴影设置
        public int shadowMapResolution = 1024;
        private CSM csm;
        private RTHandle[] shadowTextures = new RTHandle[4];
        private RTHandle shadowMask;
        private RTHandle shadowStrength;

        private ClusterLight clusterLight;
        
        public GameCameraRenderer(Camera camera) : base(camera)
        {
            cameraType = HzCameraType.Game;
            _rendererDesc = "Render Game (" + camera.name + ")";
            
            InitBuffers();
            csm = new CSM();
            clusterLight = new ClusterLight();
        }
        public override void Setup()
        {
            //设置TAA
            _cmd.SetGlobalVector("_LastJitter",_curJitter);
            if (_enableTaa && settings.taaSettings.enabled) {
                ConfigureProjectionMatrix(ref _curJitter);
            } else camera.ResetProjectionMatrix();
            _cmd.SetGlobalVector("_Jitter", _curJitter);
            
            var transform = camera.transform;
            var cameraViewMatrix = camera.worldToCameraMatrix;
            
            _cameraPosWS = transform.position;
            _cameraFwdWS = cameraViewMatrix.GetViewForward();
            _cameraUpWS = cameraViewMatrix.GetViewUp();
            _cameraRightWS = cameraViewMatrix.GetViewRight();

            Vector4 screenSize = new Vector4(InternalRes.x, InternalRes.y, 1.0f / InternalRes.x, 1.0f / InternalRes.y);
            
            var viewMatrix = camera.worldToCameraMatrix;
            var projectionMatrix = camera.projectionMatrix;
            _vpMatrix = GL.GetGPUProjectionMatrix(projectionMatrix, false) * viewMatrix;
            _vpMatrixInv = _vpMatrix.inverse;
            _nonjitterVPMatrix = GL.GetGPUProjectionMatrix(camera.nonJitteredProjectionMatrix, false) * viewMatrix;
            _nonjitterVPMatrixInv = _nonjitterVPMatrix.inverse;
            
            var farHalfFovTan = _farPlane * _verticalFovTan;
			
            _prevFrustumCornersWS = _frustumCornersWS;

            _frustumCornersWS = new Matrix4x4();
			
            var fwdDir = _cameraFwdWS * _farPlane;
            var upDir = _cameraUpWS * farHalfFovTan;
            var rightDir = _cameraRightWS * farHalfFovTan * _aspect;
			
            var topLeft = fwdDir + upDir - rightDir;
            var topRight = fwdDir + upDir + rightDir * 3f;
            var bottomLeft = fwdDir - upDir * 3f - rightDir;
            // var bottomRight = fwdDir - upDir * 3f + rightDir * 3f;

            var zBufferParams = new float4((_farPlane - _nearPlane) / _nearPlane,  1f, (_farPlane - _nearPlane) / (_nearPlane * _farPlane), 1f / _farPlane);

            _frustumCornersWS.SetRow(0, new float4(topLeft, .0f));
            _frustumCornersWS.SetRow(1, new float4(bottomLeft, .0f));
            _frustumCornersWS.SetRow(2, new float4(topRight, .0f));
            _frustumCornersWS.SetRow(3, zBufferParams);
          
            SetupCameraProperties();
            
            _cmd.SetGlobalFloat("_near", _nearPlane);
            _cmd.SetGlobalFloat("_far", _farPlane);
            _cmd.SetGlobalVector("_SceenSize", screenSize);

            //gbuffer
            _cmd.SetGlobalTexture("_gdepth", gdepth);
            _cmd.SetGlobalTexture("_hizBuffer", hizBuffer);
            for (int i = 0; i < 4; i++)
              _cmd.SetGlobalTexture("_GT" + i, gbuffers[i]);
            
            //设置csm
            _cmd.SetGlobalFloat("_orthoDistance", _farPlane - _nearPlane);
            _cmd.SetGlobalFloat("_shadowMapResolution", shadowMapResolution);
            _cmd.SetGlobalTexture("_shadowStrength", shadowStrength);
            _cmd.SetGlobalTexture("_shadowMask", shadowMask);
            for (int i = 0; i < 4; i++)
            {
              _cmd.SetGlobalTexture("_shadowtex" + i, shadowTextures[i]);
              _cmd.SetGlobalFloat("_split" + i, csm.splts[i]);
            }
          
            ExecuteCommand();
        }

        internal void SetupCameraProperties() {
            _context.SetupCameraProperties(camera);
            _cmd.SetGlobalMatrix("_vpMatrix", _vpMatrix);
            _cmd.SetGlobalMatrix("_vpMatrixInv", _vpMatrixInv);
            _cmd.SetGlobalMatrix("_vpMatrixPrev", _vpMatrixPrev);
            _cmd.SetGlobalMatrix("_vpMatrixInvPrev", _vpMatrixInvPrev);
            _cmd.SetGlobalMatrix("_nonjitterVPMatrix", _nonjitterVPMatrix);
            _cmd.SetGlobalMatrix("_nonjitterVPMatrixInv", _nonjitterVPMatrixInv);
            _cmd.SetGlobalMatrix("_FrustumCornersWS", _frustumCornersWS);
            ExecuteCommand();
        }

        public override void Render(ScriptableRenderContext context) {
            _context = context;
            _cmd = CommandBufferPool.Get(_rendererDesc);
            
            GetBuffers();
            Setup();

            beforeCull?.Invoke();
            
            ClusterLightingPass();

            ShadowCastingPass();

            GbufferPass();

            //InstanceDrawPass();

            if (!Handles.ShouldRenderGizmos())
            {
              HizPass();
            }

            ShadowMappingPass();

            LightPass();

            // skybox and Gizmos
            _context.DrawSkybox(camera);
            
            beforePostProcess?.Invoke();
            
            ResolveTAAPass();
            TonemapPass();
            
            afterLastPass?.Invoke();
            Submit();
        }
        
        
        void GbufferPass() {
          BeginSample("gbufferDraw");
          _context.SetupCameraProperties(camera);

          SetRenderTarget(gbuffers, gdepth);
          
          _cmd.ClearRenderTarget(true, true, Color.clear);
          ExecuteCommand();

          camera.TryGetCullingParameters(out var cullingParameters);
          var cullingResults = _context.Cull(ref cullingParameters);

          ShaderTagId shaderTagId = new ShaderTagId("gbuffer");
          SortingSettings sortingSettings = new SortingSettings(camera);
          DrawingSettings drawingSettings = new DrawingSettings(shaderTagId, sortingSettings);
          FilteringSettings filteringSettings = FilteringSettings.defaultValue;

          _context.DrawRenderers(cullingResults, ref drawingSettings, ref filteringSettings);
          //Submit();
          EndSample("gbufferDraw");
        }

        void LightPass() {
          BeginSample("lightingPass");

          Material mat = new Material(Shader.Find("HzRP/lightpass"));
          
          _cmd.Blit(null,BuiltinRenderTextureType.CameraTarget, mat);

          ExecuteCommand();

          //Submit();
          EndSample("lightingPass");
        }

        //阴影贴图pass
        void ShadowCastingPass() {
          BeginSample("ShadowCastingPass");

          Light light = RenderSettings.sun;
          Vector3 lightDir = light.transform.rotation * Vector3.forward;

          csm.Update(camera, lightDir, settings.csmSettings);
          settings.csmSettings.Set();

          csm.SaveMainCameraSettings(ref camera);
          for (int level = 0; level < 4; level++)
          {
            csm.ConfigCameraToShadowSpace(ref camera, lightDir, level, shadowMapResolution);

            Matrix4x4 v = camera.worldToCameraMatrix;
            Matrix4x4 p = GL.GetGPUProjectionMatrix(camera.projectionMatrix, false);
            Shader.SetGlobalMatrix("_shadowVpMatrix" + level, p * v);
            Shader.SetGlobalFloat("_orthoWidth" + level, csm.orthoWidths[level]);
            
            _context.SetupCameraProperties(camera);
            _cmd.SetRenderTarget(shadowTextures[level]);
            _cmd.ClearRenderTarget(true, true, Color.clear);
            ExecuteCommand();

            camera.TryGetCullingParameters(out var cullingParameters);
            var cullingResults = _context.Cull(ref cullingParameters);

            ShaderTagId shaderTagId = new ShaderTagId("depthOnly");
            SortingSettings sortingSettings = new SortingSettings(camera);
            DrawingSettings drawingSettings = new DrawingSettings(shaderTagId, sortingSettings);
            FilteringSettings filteringSettings = FilteringSettings.defaultValue;

            _context.DrawRenderers(cullingResults, ref drawingSettings, ref filteringSettings);
            //Submit();
          }

          csm.RevertMainCameraSettings(ref camera);

          EndSample("ShadowCastingPass");
        }

      //阴影计算 pass, 输出阴影强度图
      void ShadowMappingPass() {
        BeginSample("ShadowMapping Pass");
        
        RenderTexture tempTex1 = RenderTexture.GetTemporary(InternalRes.x / 4, InternalRes.y / 4, 0,
          RenderTextureFormat.R8, RenderTextureReadWrite.Linear);
        RenderTexture tempTex2 = RenderTexture.GetTemporary(InternalRes.x / 4, InternalRes.y / 4, 0,
          RenderTextureFormat.R8, RenderTextureReadWrite.Linear);
        RenderTexture tempTex3 = RenderTexture.GetTemporary(InternalRes.x, InternalRes.y, 0, RenderTextureFormat.R8,
          RenderTextureReadWrite.Linear);

        if (settings.csmSettings.usingShadowMask)
        {
          _cmd.Blit(gbuffers[0], tempTex1, new Material(Shader.Find("HzRP/preshadowmappingpass")));
          _cmd.Blit(tempTex1, tempTex2, new Material(Shader.Find("HzRP/blurNx1")));
          _cmd.Blit(tempTex2, shadowMask, new Material(Shader.Find("HzRP/blur1xN")));
        }

        _cmd.Blit(gbuffers[0], tempTex3, new Material(Shader.Find("HzRP/shadowmappingpass")));
        _cmd.Blit(tempTex3, shadowStrength, new Material(Shader.Find("HzRP/blurNxN")));

        RenderTexture.ReleaseTemporary(tempTex1);
        RenderTexture.ReleaseTemporary(tempTex2);
        RenderTexture.ReleaseTemporary(tempTex3);

        
        ExecuteCommand();
        //Submit();
        
        EndSample("ShadowMapping Pass");
      }

      void ClusterLightingPass()
      {
        camera.TryGetCullingParameters(out var cullingParameters);
        var cullingResults = _context.Cull(ref cullingParameters);

        clusterLight.UpdateLightBuffer(cullingResults.visibleLights.ToArray());

        clusterLight.ClusterGenerate(camera);

        clusterLight.LightAssign();

        clusterLight.SetShaderParameters();
      }
/*
      void InstanceDrawPass()
      {
        BeginSample("InstanceDraw Pass");

        _cmd.SetRenderTarget(gbufferID, gdepth);

        // Draw Instance
        ComputeShader computeShader = Resources.Load<ComputeShader>("Shader/InstanceCulling");
        for (int i = 0; i < instanceDatas.Length; i++)
          InstanceDraw.Draw(instanceDatas[i], Camera.main, computeShader, _vpMatrixPrev, hizBuffer, ref cmd);

        ExecuteCommand();
        Submit();

        EndSample("InstanceDraw Pass");
      }
*/
      void HizPass()
      {
        BeginSample("Hiz Pass");

        int size = hizBuffer.referenceSize.x;
        int mipNums = (int)Mathf.Log(size, 2);
        RenderTexture[] mips = new RenderTexture[mipNums];
        for (int i = 0; i < mips.Length; i++)
        {
          int subSize = size / (int)Mathf.Pow(2, i);
          mips[i] = RenderTexture.GetTemporary(subSize, subSize, 0, RenderTextureFormat.RHalf,
            RenderTextureReadWrite.Linear);
          mips[i].filterMode = FilterMode.Point;
        }

        // Generate mipmap
        Material mat = new Material(Shader.Find("HzRP/hizBlit"));
        _cmd.Blit(gdepth, mips[0]);
        for (int i = 1; i < mips.Length; i++)
          _cmd.Blit(mips[i - 1], mips[i], mat);

        for (int i = 0; i < mips.Length; i++)
        {
          _cmd.CopyTexture(mips[i], 0, 0, hizBuffer, 0, i);
          RenderTexture.ReleaseTemporary(mips[i]);
        }

        ExecuteCommand();
       // Submit();
        
        EndSample("Hiz Pass");
      }

      void ResolveTAAPass()
      {
        BeginSample("TAA Pass");

        var enableProjection = -1f;
        if (!IsOnFirstFrame && settings.taaSettings.enabled && _enableTaa) enableProjection = 1f;
        Material mat = new Material(Shader.Find("HzRP/TemporalAntialiasing"));
  
        const float kMotionAmplification_Blending = 100f * 60f;
        const float kMotionAmplification_Bounding = 100f * 30f;
        mat.SetFloat("_EnableReprojection", enableProjection);
        
        _cmd.SetGlobalFloat("_Sharpness", settings.taaSettings.sharpness);
        _cmd.SetGlobalTexture("_PreviousColorBuffer", _prevColorTex);
        _cmd.SetGlobalVector("_FinalBlendParameters", new Vector4(settings.taaSettings.stationaryBlending, settings.taaSettings.motionBlending, kMotionAmplification_Blending, 0f));
        _cmd.SetGlobalVector("_TemporalClipBounding", new Vector4(settings.taaSettings.stationaryAABBScale, settings.taaSettings.motionAABBScale, kMotionAmplification_Bounding, 0f));
        
        _cmd.Blit(BuiltinRenderTextureType.CameraTarget, _colorTex, mat);
        
        ExecuteCommand();
        //Submit();
        EndSample("TAA Pass");
      }

      void TonemapPass()
      {
        BeginSample("Tonemap Pass");
        var colorGradeParams = new Vector4(
          Mathf.Pow(2f, settings.colorSettings.postExposure),
          settings.colorSettings.contrast * .01f + 1f,
          settings.colorSettings.hueShift * (1f / 360f),
          settings.colorSettings.saturation * .01f + 1f);

        Material mat = new Material(settings.tonemappingSettings.tonemappingShader);
        mat.SetInteger(ShaderKeywordManager.TONEMAPPING_TYPE, (int)settings.tonemappingSettings.tonemappingType);
        mat.SetVector(ShaderKeywordManager.COLOR_GRADE_PARAMS, colorGradeParams);
        mat.SetColor(ShaderKeywordManager.COLOR_FILTER, settings.colorSettings.colorFilter.linear);
        
        _cmd.Blit(_colorTex, _displayTex, mat);
        
        if(cameraType == HzCameraType.SceneView)
          _cmd.Blit(_displayTex, BuiltinRenderTextureType.CameraTarget);
        else
          _cmd.Blit(_displayTex, BuiltinRenderTextureType.CameraTarget, new Vector2(1, -1), new Vector2(0, 1));
       
        ExecuteCommand();
        EndSample("Tonemap Pass");
      }
      
      internal void InitBuffers() {
        _historyBuffers.AllocBuffer(ShaderKeywordManager.DISPLAY_TEXTURE,
          (system, i) => system.Alloc(size => InternalRes, colorFormat: GraphicsFormat.R16G16B16A16_SFloat,
            filterMode: FilterMode.Bilinear, name: "DisplayTex"), 1);
        _historyBuffers.AllocBuffer(ShaderKeywordManager.COLOR_TEXTURE,
          (system, i) => system.Alloc(size => InternalRes, colorFormat: GraphicsFormat.R16G16B16A16_SFloat,
            filterMode: FilterMode.Bilinear, name: "ColorTex"), 2);
        _historyBuffers.AllocBuffer(ShaderKeywordManager.GBUFFER_0_TEXTURE,
          (system, i) =>system.Alloc(size => InternalRes, colorFormat: GraphicsFormat.R16G16B16A16_SFloat, name:"Gbuffer0"), 1);
        _historyBuffers.AllocBuffer(ShaderKeywordManager.GBUFFER_1_TEXTURE,
          (system, i) =>system.Alloc(size => InternalRes, colorFormat: GraphicsFormat.R16G16_UNorm, name:"Gbuffer1"), 1);
        _historyBuffers.AllocBuffer(ShaderKeywordManager.GBUFFER_2_TEXTURE,
          (system, i) =>system.Alloc(size => InternalRes, colorFormat: GraphicsFormat.R16G16B16A16_SFloat, name:"Gbuffer2"), 1);
        _historyBuffers.AllocBuffer(ShaderKeywordManager.GBUFFER_3_TEXTURE,
          (system, i) => system.Alloc(size => InternalRes, colorFormat: GraphicsFormat.R32G32B32A32_SFloat, name: "Gbuffer3"), 1);
        _historyBuffers.AllocBuffer(ShaderKeywordManager.GDEPTH_TEXTURE,
          (system, i)=>system.Alloc(size=>InternalRes, colorFormat: GraphicsFormat.None, depthBufferBits: DepthBits.Depth24, name:"GDepth"), 1);
        int hizSize = Mathf.NextPowerOfTwo(Mathf.Max(Screen.width, Screen.height));
        _historyBuffers.AllocBuffer(ShaderKeywordManager.HIZ_BUFFER_TEXTURE,
          (system, i) =>system.Alloc(hizSize, hizSize, colorFormat: GraphicsFormat.R16_SFloat,useMipMap:true, autoGenerateMips:false, filterMode: FilterMode.Point, name: "HizBuffer"), 1);
        _historyBuffers.AllocBuffer(ShaderKeywordManager.SHADOW_MASK_TEXTURE,
          (system, i)=>system.Alloc(Mathf.Max(Mathf.RoundToInt(0.25f * InternalRes.x), 1), Mathf.Max(Mathf.RoundToInt(0.25f * InternalRes.y), 1), colorFormat: GraphicsFormat.R8_UNorm, name: "ShadowMask"), 1);
        _historyBuffers.AllocBuffer(ShaderKeywordManager.SHADOW_STRENGTH_TEXTURE,
          (system, i)=>system.Alloc(InternalRes, colorFormat: GraphicsFormat.R8_UNorm, name: "ShadowStrength"), 1);

        InitScreenIndependentBuffers();
      }

      internal void InitScreenIndependentBuffers() {
        _historyBuffers.AllocBuffer(ShaderKeywordManager.SHADOW_0_TEXTURE,
          (system, i)=>system.Alloc(shadowMapResolution, shadowMapResolution, colorFormat: GraphicsFormat.None, depthBufferBits: DepthBits.Depth24, name:"SHADOW0"), 1);
        _historyBuffers.AllocBuffer(ShaderKeywordManager.SHADOW_1_TEXTURE,
          (system, i)=>system.Alloc(shadowMapResolution, shadowMapResolution, colorFormat: GraphicsFormat.None, depthBufferBits: DepthBits.Depth24, name:"SHADOW1"), 1);
        _historyBuffers.AllocBuffer(ShaderKeywordManager.SHADOW_2_TEXTURE,
          (system, i)=>system.Alloc(shadowMapResolution, shadowMapResolution, colorFormat: GraphicsFormat.None, depthBufferBits: DepthBits.Depth24, name:"SHADOW2"), 1);
        _historyBuffers.AllocBuffer(ShaderKeywordManager.SHADOW_3_TEXTURE,
          (system, i)=>system.Alloc(shadowMapResolution, shadowMapResolution, colorFormat: GraphicsFormat.None, depthBufferBits: DepthBits.Depth24, name:"SHADOW3"), 1);
      }

      internal void GetBuffers()
      {
        _historyBuffers.SwapAndSetReferenceSize(OutputRes.x, OutputRes.y);

        _displayTex = _historyBuffers.GetFrameRT(ShaderKeywordManager.DISPLAY_TEXTURE, 0);
        _colorTex = _historyBuffers.GetFrameRT(ShaderKeywordManager.COLOR_TEXTURE, 0);
        gbuffers[0] = _historyBuffers.GetFrameRT(ShaderKeywordManager.GBUFFER_0_TEXTURE, 0);
        gbuffers[1] = _historyBuffers.GetFrameRT(ShaderKeywordManager.GBUFFER_1_TEXTURE, 0);
        gbuffers[2] = _historyBuffers.GetFrameRT(ShaderKeywordManager.GBUFFER_2_TEXTURE, 0);
        gbuffers[3] = _historyBuffers.GetFrameRT(ShaderKeywordManager.GBUFFER_3_TEXTURE, 0);
        gdepth = _historyBuffers.GetFrameRT(ShaderKeywordManager.GDEPTH_TEXTURE, 0);
        hizBuffer = _historyBuffers.GetFrameRT(ShaderKeywordManager.HIZ_BUFFER_TEXTURE, 0);
        shadowMask = _historyBuffers.GetFrameRT(ShaderKeywordManager.SHADOW_MASK_TEXTURE, 0);
        shadowStrength = _historyBuffers.GetFrameRT(ShaderKeywordManager.SHADOW_STRENGTH_TEXTURE, 0);
        shadowTextures[0] = _historyBuffers.GetFrameRT(ShaderKeywordManager.SHADOW_0_TEXTURE, 0);
        shadowTextures[1] = _historyBuffers.GetFrameRT(ShaderKeywordManager.SHADOW_1_TEXTURE, 0);
        shadowTextures[2] = _historyBuffers.GetFrameRT(ShaderKeywordManager.SHADOW_2_TEXTURE, 0);
        shadowTextures[3] = _historyBuffers.GetFrameRT(ShaderKeywordManager.SHADOW_3_TEXTURE, 0);

        _prevColorTex = _historyBuffers.GetFrameRT(ShaderKeywordManager.COLOR_TEXTURE, 1);
      }

      internal void ResetBufferSize() {
        _historyBuffers.ResetReferenceSize(OutputRes.x, OutputRes.y);
      }
   
      protected override void UpdateRenderScale(bool outputChanged = true) {
        
        base.UpdateRenderScale(outputChanged);
        if (outputChanged) {
          ResetFrameHistory();
          ResetBufferSize();
        }
      }

      public override void Dispose() {
        base.Dispose();
        if (_historyBuffers != null) {
          _historyBuffers.ReleaseAll();
          _historyBuffers.Dispose();
        }
      }
    }
}

