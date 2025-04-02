using System;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;
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
        
        private RenderTexture gdepth;
        private RenderTexture[] gbuffers = new RenderTexture[4];
        private RenderTargetIdentifier[] gbufferID = new RenderTargetIdentifier[4];
        private RenderTexture hizBuffer;

        //阴影设置
        public int shadowMapResolution = 1024;
        private CSM csm;
        private RenderTexture[] shadowTextures = new RenderTexture[4];
        private RenderTexture shadowMask;
        private RenderTexture shadowStrength;

        private ClusterLight clusterLight;
        
        public GameCameraRenderer(Camera camera) : base(camera)
        {
            cameraType = HzCameraType.Game;
            _rendererDesc = "Render Game (" + camera.name + ")";

            gdepth = new RenderTexture(Screen.width, Screen.height, 24, RenderTextureFormat.Depth,
              RenderTextureReadWrite.Linear);
            gbuffers[0] = new RenderTexture(Screen.width, Screen.height, 0, RenderTextureFormat.ARGB32,
              RenderTextureReadWrite.Linear);
            gbuffers[1] = new RenderTexture(Screen.width, Screen.height, 0, RenderTextureFormat.ARGB2101010,
              RenderTextureReadWrite.Linear);
            gbuffers[2] = new RenderTexture(Screen.width, Screen.height, 0, RenderTextureFormat.ARGB64,
              RenderTextureReadWrite.Linear);
            gbuffers[3] = new RenderTexture(Screen.width, Screen.height, 0, RenderTextureFormat.ARGBFloat,
              RenderTextureReadWrite.Linear);
            for (int i = 0; i < 4; i++)
              gbufferID[i] = gbuffers[i];

            //Hi-z buffer
            int hizSize = Mathf.NextPowerOfTwo(Mathf.Max(Screen.width, Screen.height));
            hizBuffer = new RenderTexture(hizSize, hizSize, 0, RenderTextureFormat.RHalf);
            hizBuffer.autoGenerateMips = false;
            hizBuffer.useMipMap = true;
            hizBuffer.filterMode = FilterMode.Point;

            //创建阴影贴图
            shadowMask = new RenderTexture(Screen.width / 4, Screen.height / 4, 0, RenderTextureFormat.R8,
              RenderTextureReadWrite.Linear);
            shadowStrength = new RenderTexture(Screen.width, Screen.height, 0, RenderTextureFormat.R8,
              RenderTextureReadWrite.Linear);
            for (int i = 0; i < 4; i++)
              shadowTextures[i] = new RenderTexture(shadowMapResolution, shadowMapResolution, 24, RenderTextureFormat.Depth,
                RenderTextureReadWrite.Linear);

            csm = new CSM();
            clusterLight = new ClusterLight();
            
            InitBuffers();
            InitComputeBuffers();
        }
        public override void Setup()
        {
            //设置TAA
            var jitterNum = (int) settings.taaSettings.jitterNum;
            var frameNumCycled = _frameNum % jitterNum;
			
            var frameParams = new Vector4(_frameNum, jitterNum,  frameNumCycled, frameNumCycled / (float) jitterNum);
            _cmd.SetGlobalVector("_FrameParams", frameParams);
			
            _curJitter = _jitterPatterns[frameNumCycled];
            _curJitter *= settings.taaSettings.jitterSpread;
			
            var taaJitter = new Vector4(_curJitter.x, _curJitter.y, _curJitter.x / InternalRes.x, _curJitter.y / InternalRes.y);
            _cmd.SetGlobalVector("_JitterParams", taaJitter);

            if (_enableTaa && settings.taaSettings.enabled) {
                ConfigureProjectionMatrix(_curJitter);
            } else camera.ResetProjectionMatrix();

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
            _cmd.SetGlobalFloat("_screenWidth", Screen.width);
            _cmd.SetGlobalFloat("_screenHeight", Screen.height);
            _cmd.SetGlobalTexture("_noiseTex", settings.blueNoiseTex);
            _cmd.SetGlobalFloat("_noiseTexResolution", settings.blueNoiseTex.width);

            //gbuffer
            _cmd.SetGlobalTexture("_gdepth", gdepth);
            _cmd.SetGlobalTexture("_hizBuffer", hizBuffer);
            for (int i = 0; i < 4; i++)
              _cmd.SetGlobalTexture("_GT" + i, gbuffers[i]);

    
            _cmd.SetGlobalTexture("_GlobalEnvMapDiffuse", settings.globalEnvMapDiffuse);
            _cmd.SetGlobalTexture("_GlobalEnvMapSpecular", settings.globalEnvMapSpecular);
            _cmd.SetGlobalFloat("_GlobalEnvMapRotation", settings.globalEnvMapRotation);
            _cmd.SetGlobalFloat("_SkyboxMipLevel", settings.skyboxMipLevel);
            _cmd.SetGlobalFloat("_SkyboxIntensity", settings.skyboxIntensity);
            _cmd.SetGlobalTexture("_PreintegratedDGFLut", settings.brdfLut);


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
            afterLastPass?.Invoke();
            _context.Submit();
        }

        public void InitBuffers()
        {
            
        }

        public void InitComputeBuffers()
        {
            
        }
        
        void GbufferPass() {
          BeginSample("gbufferDraw");
          _context.SetupCameraProperties(camera);
          CommandBuffer cmd = new CommandBuffer();
          cmd.name = "gbuffer";

          cmd.SetRenderTarget(gbufferID, gdepth);
          cmd.ClearRenderTarget(true, true, Color.red);
          _context.ExecuteCommandBuffer(cmd);
          cmd.Clear();

          camera.TryGetCullingParameters(out var cullingParameters);
          var cullingResults = _context.Cull(ref cullingParameters);

          ShaderTagId shaderTagId = new ShaderTagId("gbuffer");
          SortingSettings sortingSettings = new SortingSettings(camera);
          DrawingSettings drawingSettings = new DrawingSettings(shaderTagId, sortingSettings);
          FilteringSettings filteringSettings = FilteringSettings.defaultValue;

          _context.DrawRenderers(cullingResults, ref drawingSettings, ref filteringSettings);
          _context.Submit();

          EndSample("gbufferDraw");
        }

        void LightPass() {
          BeginSample("lightingPass");

          CommandBuffer cmd = new CommandBuffer();
          cmd.name = "lightpass";

          Material mat = new Material(Shader.Find("HzRP/lightpass"));
          cmd.Blit(gbufferID[0], BuiltinRenderTextureType.CameraTarget, mat);
          _context.ExecuteCommandBuffer(cmd);
          cmd.Clear();

          _context.Submit();
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

            CommandBuffer cmd = new CommandBuffer();
            cmd.name = "shadowmap" + level;

            _context.SetupCameraProperties(camera);
            cmd.SetRenderTarget(shadowTextures[level]);
            cmd.ClearRenderTarget(true, true, Color.clear);
            _context.ExecuteCommandBuffer(cmd);
            cmd.Clear();

            camera.TryGetCullingParameters(out var cullingParameters);
            var cullingResults = _context.Cull(ref cullingParameters);

            ShaderTagId shaderTagId = new ShaderTagId("depthOnly");
            SortingSettings sortingSettings = new SortingSettings(camera);
            DrawingSettings drawingSettings = new DrawingSettings(shaderTagId, sortingSettings);
            FilteringSettings filteringSettings = FilteringSettings.defaultValue;

            _context.DrawRenderers(cullingResults, ref drawingSettings, ref filteringSettings);
            _context.Submit();
          }

          csm.RevertMainCameraSettings(ref camera);

          EndSample("ShadowCastingPass");
        }

      //阴影计算 pass, 输出阴影强度图
      void ShadowMappingPass() {
        BeginSample("ShadowMapping Pass");

        CommandBuffer cmd = new CommandBuffer();
        cmd.name = "shadowmappingpass";

        RenderTexture tempTex1 = RenderTexture.GetTemporary(Screen.width / 4, Screen.height / 4, 0,
          RenderTextureFormat.R8, RenderTextureReadWrite.Linear);
        RenderTexture tempTex2 = RenderTexture.GetTemporary(Screen.width / 4, Screen.height / 4, 0,
          RenderTextureFormat.R8, RenderTextureReadWrite.Linear);
        RenderTexture tempTex3 = RenderTexture.GetTemporary(Screen.width, Screen.height, 0, RenderTextureFormat.R8,
          RenderTextureReadWrite.Linear);

        if (settings.csmSettings.usingShadowMask)
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

        _context.ExecuteCommandBuffer(cmd);
        cmd.Clear();
        _context.Submit();
        
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

        CommandBuffer cmd = new CommandBuffer();
        cmd.name = "instance gbuffer";
        cmd.SetRenderTarget(gbufferID, gdepth);

        // Draw Instance
        ComputeShader computeShader = Resources.Load<ComputeShader>("Shader/InstanceCulling");
        for (int i = 0; i < instanceDatas.Length; i++)
          InstanceDraw.Draw(instanceDatas[i], Camera.main, computeShader, _vpMatrixPrev, hizBuffer, ref cmd);

        _context.ExecuteCommandBuffer(cmd);
        cmd.Clear();
        _context.Submit();

        EndSample("InstanceDraw Pass");
      }
*/
      void HizPass()
      {
        BeginSample("Hiz Pass");

        CommandBuffer cmd = new CommandBuffer();
        cmd.name = "hiz pass";

        int size = hizBuffer.width;
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
        cmd.Blit(gdepth, mips[0]);
        for (int i = 1; i < mips.Length; i++)
          cmd.Blit(mips[i - 1], mips[i], mat);

        for (int i = 0; i < mips.Length; i++)
        {
          cmd.CopyTexture(mips[i], 0, 0, hizBuffer, 0, i);
          RenderTexture.ReleaseTemporary(mips[i]);
        }

        _context.ExecuteCommandBuffer(cmd);
        cmd.Clear();
        _context.Submit();
        
        EndSample("Hiz Pass");
      }

      protected override void UpdateRenderScale(bool outputChanged = true) {
        base.UpdateRenderScale(outputChanged);
        if (outputChanged) {
          ResetFrameHistory();
        }
      }
    }
}

