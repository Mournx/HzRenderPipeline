using System;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;

namespace HzRenderPipeline.Runtime.Cameras {
    public abstract class CameraRenderer : IDisposable
    {

        public static HzRenderPipelineSettings settings => HzRenderPipeline.settings;
        public Camera camera;
        public HzCameraType cameraType;

        public Vector2Int InternalRes => _internalRes;

        public Vector2Int OutputRes {
            get => _outputRes;
            set {
                if (_outputRes == value) return;
                _outputRes = value;
                UpdateRenderScale();
            }
        }

        public Vector2 Ratio {
            get => _ratio;
            set {
                if (_ratio == value || value.x > 1 || value.y > 1) return;
                _ratio = value;
                UpdateRenderScale(false);
            }
        }

        public bool IsOnFirstFrame => _frameNum == 1; // default is 0, and we start at 1.
        

        protected Vector2Int _outputRes;
        protected Vector2Int _internalRes;
        protected Vector2 _ratio;
        protected Vector2Int _lastRenderOutputRes;
        protected Vector2 _lastRenderRatio;

        protected ScriptableRenderContext _context; // Not persistent
        protected CommandBuffer _cmd;  // current active
        protected CullingResults _cullingResults;

        protected int _frameNum;
        protected float _nearPlane;
        protected float _farPlane;
        protected float _aspect;
        protected float _fov;
        protected float _verticalFovTan;
        protected float3 _cameraPosWS;
        protected float3 _cameraFwdWS;
        protected float3 _cameraUpWS;
        protected float3 _cameraRightWS;
        protected Matrix4x4 _frustumCornersWS;
        protected Matrix4x4 _prevFrustumCornersWS;
        protected Vector2 _curJitter;
        protected Vector2[] _jitterPatterns = new Vector2[8];
        protected Matrix4x4 _vpMatrix;
        protected Matrix4x4 _vpMatrixInv;
        protected Matrix4x4 _vpMatrixPrev;  // nonjittered 
        protected Matrix4x4 _vpMatrixInvPrev;// nonjittered
        protected Matrix4x4 _nonjitterVPMatrix;
        protected Matrix4x4 _nonjitterVPMatrixInv;

        public CameraRenderer(Camera camera)
        {
            this.camera = camera;
            camera.forceIntoRenderTexture = true;
            

            for (var i = 0; i < (int)settings.taaSettings.jitterNum; i++)
                _jitterPatterns[i] = new Vector2(HaltonSequence.Get((i & 1023) + 1, 2) - .5f, HaltonSequence.Get((i & 1023) + 1, 3) - .5f);
        }

        public abstract void Setup();
        public abstract void Render(ScriptableRenderContext context);

        public virtual void PreUpdate() {
            _frameNum++;
            _lastRenderRatio = Ratio;
            _lastRenderOutputRes = OutputRes;
            _aspect = camera.aspect;
            _nearPlane = camera.nearClipPlane;
            _farPlane = camera.farClipPlane;
            _fov = camera.fieldOfView;
            _verticalFovTan = Mathf.Tan(.5f * Mathf.Deg2Rad * _fov);
        }

        public virtual void PostUpdate() {
            _vpMatrixPrev = _nonjitterVPMatrix;
            _vpMatrixInvPrev = _nonjitterVPMatrixInv;
        }

        public virtual void ResetFrameHistory() {
            _frameNum = 0;
        }

        public void Submit() => _context.Submit();

        protected virtual void UpdateRenderScale(bool outputChanged = true)
        {
            _internalRes =  Vector2Int.CeilToInt(OutputRes * Ratio);
            _internalRes.x = Mathf.Max(_internalRes.x, 1);
            _internalRes.y = Mathf.Max(_internalRes.y, 1);
        }

        public void SetResolutionAndRatio(int w, int h, float x, float y)
        {
            _outputRes = new Vector2Int(w, h);
            _ratio = new Vector2(x, y);

            var outputChanged = _lastRenderOutputRes != _outputRes;
            
            UpdateRenderScale(outputChanged);
        }

        #region Command Buffer Utils
        
        public void DisposeCommandBuffer() {
            if (_cmd != null) {
                CommandBufferPool.Release(_cmd);
                _cmd = null;
            }
        }
        
         public void SetRenderTarget(RTHandle colorBuffer, bool clear = false) {
            _cmd.SetRenderTarget(colorBuffer, 0, CubemapFace.Unknown, 0);
            CoreUtils.SetViewport(_cmd, colorBuffer);
            if (clear) ClearRenderTarget(RTClearFlags.All);
        }

        public void SetRenderTarget(RTHandle colorBuffer, int mipLevel, bool clear = false) {
            _cmd.SetRenderTarget(colorBuffer, mipLevel, CubemapFace.Unknown, 0);
            CoreUtils.SetViewport(_cmd, colorBuffer);
            if (clear) ClearRenderTarget(RTClearFlags.All);
        }

        public void SetRenderTarget(RTHandle colorBuffer, int mipLevel, CubemapFace cubemapFace, bool clear = false) {
            _cmd.SetRenderTarget(colorBuffer, mipLevel, cubemapFace, 0);
            CoreUtils.SetViewport(_cmd, colorBuffer);
            if (clear) ClearRenderTarget(RTClearFlags.All);
        }

        public void SetRenderTarget(RTHandle colorBuffer, int mipLevel, CubemapFace cubemapFace, int depthSlice,
            bool clear = false) {
            _cmd.SetRenderTarget(colorBuffer, mipLevel, cubemapFace, depthSlice);
            CoreUtils.SetViewport(_cmd, colorBuffer);
            if (clear) ClearRenderTarget(RTClearFlags.All);
        }

        public void SetRenderTarget(RTHandle colorBuffer, RTHandle depthBuffer, bool clear = false) {
            _cmd.SetRenderTarget(colorBuffer, depthBuffer, 0, CubemapFace.Unknown, 0);
            CoreUtils.SetViewport(_cmd, colorBuffer);
            if (clear) ClearRenderTarget(RTClearFlags.All);
        }

        public void SetRenderTarget(RTHandle colorBuffer, RTHandle depthBuffer, int mipLevel, bool clear = false) {
            _cmd.SetRenderTarget(colorBuffer, depthBuffer, mipLevel, CubemapFace.Unknown, 0);
            CoreUtils.SetViewport(_cmd, colorBuffer);
            if (clear) ClearRenderTarget(RTClearFlags.All);
        }

        public void SetRenderTarget(RTHandle colorBuffer, RTHandle depthBuffer, int mipLevel, CubemapFace cubemapFace,
            bool clear = false) {
            _cmd.SetRenderTarget(colorBuffer, depthBuffer, mipLevel, cubemapFace, 0);
            CoreUtils.SetViewport(_cmd, colorBuffer);
            if (clear) ClearRenderTarget(RTClearFlags.All);
        }

        public void SetRenderTarget(RTHandle colorBuffer, RTHandle depthBuffer, int mipLevel, CubemapFace cubemapFace,
            int depthSlice, bool clear = false) {
            _cmd.SetRenderTarget(colorBuffer, depthBuffer, mipLevel, cubemapFace, depthSlice);
            CoreUtils.SetViewport(_cmd, colorBuffer);
            if (clear) ClearRenderTarget(RTClearFlags.All);
        }

        public void SetRenderTarget(RTHandle colorBuffer, RenderBufferLoadAction colorLoad,
            RenderBufferStoreAction colorStore, RTHandle depthBuffer, RenderBufferLoadAction depthLoad,
            RenderBufferStoreAction depthStore, bool clear = false) {
            _cmd.SetRenderTarget(colorBuffer, colorLoad, colorStore, depthBuffer, depthLoad, depthStore);
            CoreUtils.SetViewport(_cmd, colorBuffer);
            if (clear) ClearRenderTarget(RTClearFlags.All);
        }

        public void SetRenderTarget(RTHandle[] colorBuffers, RTHandle depthBuffer, bool clear = false) {
            _cmd.SetRenderTarget(HzRPUtils.RTHandlesToRTIs(colorBuffers), depthBuffer, 0, CubemapFace.Unknown, 0);
            CoreUtils.SetViewport(_cmd, colorBuffers[0]);
        }

        public void SetRenderTarget(RTHandle[] colorBuffers, RTHandle depthBuffer, int mipLevel, bool clear = false) {
            _cmd.SetRenderTarget(HzRPUtils.RTHandlesToRTIs(colorBuffers), depthBuffer, mipLevel, CubemapFace.Unknown, 0);
            CoreUtils.SetViewport(_cmd, colorBuffers[0]);
        }

        public void SetRenderTarget(RTHandle[] colorBuffers, RTHandle depthBuffer, int mipLevel,
            CubemapFace cubemapFace, bool clear = false) {
            _cmd.SetRenderTarget(HzRPUtils.RTHandlesToRTIs(colorBuffers), depthBuffer, mipLevel, cubemapFace, 0);
            CoreUtils.SetViewport(_cmd, colorBuffers[0]);
        }

        public void SetRenderTarget(RTHandle[] colorBuffers, RTHandle depthBuffer, int mipLevel,
            CubemapFace cubemapFace, int depthSlice, bool clear = false) {
            _cmd.SetRenderTarget(HzRPUtils.RTHandlesToRTIs(colorBuffers), depthBuffer, mipLevel, cubemapFace,
                depthSlice);
            CoreUtils.SetViewport(_cmd, colorBuffers[0]);
        }
        
        public void SetRenderTarget(RTHandle refColor, RenderTargetIdentifier[] colorBuffers, RTHandle depthBuffer, bool clear = false) {
            _cmd.SetRenderTarget(colorBuffers, depthBuffer, 0, CubemapFace.Unknown, 0);
            CoreUtils.SetViewport(_cmd, refColor);
        }

        public void SetRenderTarget(RTHandle refColor, RenderTargetIdentifier[] colorBuffers, RTHandle depthBuffer, int mipLevel, bool clear = false) {
            _cmd.SetRenderTarget(colorBuffers, depthBuffer, mipLevel, CubemapFace.Unknown, 0);
            CoreUtils.SetViewport(_cmd, refColor);
        }

        public void SetRenderTarget(RTHandle refColor, RenderTargetIdentifier[] colorBuffers, RTHandle depthBuffer, int mipLevel,
            CubemapFace cubemapFace, bool clear = false) {
            _cmd.SetRenderTarget(colorBuffers, depthBuffer, mipLevel, cubemapFace, 0);
            CoreUtils.SetViewport(_cmd, refColor);
        }

        public void SetRenderTarget(RTHandle refColor, RenderTargetIdentifier[] colorBuffers, RTHandle depthBuffer, int mipLevel,
            CubemapFace cubemapFace, int depthSlice, bool clear = false) {
            _cmd.SetRenderTarget(colorBuffers, depthBuffer, mipLevel, cubemapFace, depthSlice);
            CoreUtils.SetViewport(_cmd, refColor);
        }
        
        public void SetRenderTargetNonAlloc(RTHandle[] colorBuffers, RenderTargetIdentifier[] rts, RTHandle depthBuffer, bool clear = false) {
            HzRPUtils.RTHandlesToRTIsNonAlloc(colorBuffers, ref rts);
            _cmd.SetRenderTarget(rts, depthBuffer, 0, CubemapFace.Unknown, 0);
            CoreUtils.SetViewport(_cmd, colorBuffers[0]);
        }

        public void SetRenderTargetNonAlloc(RTHandle[] colorBuffers, RenderTargetIdentifier[] rts, RTHandle depthBuffer, int mipLevel, bool clear = false) {
            HzRPUtils.RTHandlesToRTIsNonAlloc(colorBuffers, ref rts);
            _cmd.SetRenderTarget(rts, depthBuffer, mipLevel, CubemapFace.Unknown, 0);
            CoreUtils.SetViewport(_cmd, colorBuffers[0]);
        }

        public void SetRenderTargetNonAlloc(RTHandle[] colorBuffers, RenderTargetIdentifier[] rts, RTHandle depthBuffer, int mipLevel,
            CubemapFace cubemapFace, bool clear = false) {
            HzRPUtils.RTHandlesToRTIsNonAlloc(colorBuffers, ref rts);
            _cmd.SetRenderTarget(rts, depthBuffer, mipLevel, cubemapFace, 0);
            CoreUtils.SetViewport(_cmd, colorBuffers[0]);
        }

        public void SetRenderTargetNonAlloc(RTHandle[] colorBuffers, RenderTargetIdentifier[] rts, RTHandle depthBuffer, int mipLevel,
            CubemapFace cubemapFace, int depthSlice, bool clear = false) {
            HzRPUtils.RTHandlesToRTIsNonAlloc(colorBuffers, ref rts);
            _cmd.SetRenderTarget(rts, depthBuffer, mipLevel, cubemapFace, depthSlice);
            CoreUtils.SetViewport(_cmd, colorBuffers[0]);
        }

        public void ClearRenderTarget(RTClearFlags flags) => ClearRenderTarget(flags, Color.black);

        public void ClearRenderTarget(RTClearFlags flags, Color color, float depth = 1f, uint stencil = 0) =>
            _cmd.ClearRenderTarget(flags, color, depth, stencil);

        public void ClearRenderTarget(bool clearColor, bool clearDepth) =>
            ClearRenderTarget(clearColor, clearDepth, Color.black);

        public void ClearRenderTarget(bool clearColor, bool clearDepth, Color color, float depth = 1f) =>
            _cmd.ClearRenderTarget(clearDepth, clearColor, color, depth);

        public void BeginSample(String name) {
#if UNITY_EDITOR || DEBUG
            _cmd.BeginSample(name);
            // ExecuteCommand(); // Don't really have to.
#endif
        }

        public void EndSample(String name) {
#if UNITY_EDITOR || DEBUG
            _cmd.EndSample(name);
            ExecuteCommand();
#endif
        }

        public void ExecuteCommand(bool clear = true) {
            _context.ExecuteCommandBuffer(_cmd);
            if (clear) _cmd.Clear();
        }

        public void ExecuteCommand(CommandBuffer buffer, bool clear = true) {
            _context.ExecuteCommandBuffer(buffer);
            if (clear) buffer.Clear();
        }

        public void ExecuteCommandAsync(ComputeQueueType queueType, bool clear = true) {
            _context.ExecuteCommandBufferAsync(_cmd, queueType);
            if (clear) _cmd.Clear();
        }

        public void ExecuteCommandAsync(CommandBuffer buffer, ComputeQueueType queueType, bool clear = true) {
            _context.ExecuteCommandBufferAsync(buffer, queueType);
            if (clear) buffer.Clear();
        }

            

        #endregion
        
        public void ConfigureProjectionMatrix(ref Vector2 jitter)
        {
            camera.ResetProjectionMatrix();
            camera.nonJitteredProjectionMatrix = camera.projectionMatrix;
            camera.projectionMatrix = GetJitteredProjectionMatrix(ref jitter);
            camera.useJitteredProjectionMatrixForTransparentRendering = false;
        }

        public Matrix4x4 GetJitteredProjectionMatrix(ref Vector2 jitter)
        {
            Matrix4x4 cameraProj;
            var jitterNum = (int) settings.taaSettings.jitterNum;
            var frameNumCycled = _frameNum % jitterNum;
			
            jitter = _jitterPatterns[frameNumCycled];
            jitter *= settings.taaSettings.jitterSpread;
            cameraProj = camera.orthographic
                ? GetJitteredOrthographicProjectionMatrix(jitter)
                : GetJitteredPerspectiveProjectionMatrix(jitter);
            jitter = new Vector2(jitter.x / InternalRes.x, jitter.y / InternalRes.y);
            return cameraProj;
        }

        public Matrix4x4 GetJitteredOrthographicProjectionMatrix(Vector2 jitter)
        {
            var vertical = camera.orthographicSize;
            var horizontal  = _aspect * vertical;

            jitter.x *= horizontal / (.5f * InternalRes.x);
            jitter.y *= vertical / (.5f * InternalRes.y);

            var left = jitter.x - horizontal;
            var right = jitter.x + horizontal;
            var top = jitter.y + vertical;
            var bottom = jitter.y - vertical;

            return Matrix4x4.Ortho(left, right, bottom, top, _nearPlane, _farPlane);
        }

        public Matrix4x4 GetJitteredPerspectiveProjectionMatrix( Vector2 jitter)
        {
            var vertical = _nearPlane * Mathf.Tan(.5f * Mathf.Deg2Rad * _fov);
            var horizontal = vertical * _aspect;

            jitter.x *= horizontal / (.5f * InternalRes.x);
            jitter.y *= vertical / (.5f * InternalRes.y);

            var proj = camera.projectionMatrix;
            proj.m02 += jitter.x / horizontal;
            proj.m12 += jitter.y / vertical;
            return proj;
        }

        public virtual void Dispose() {
            camera = null;
            DisposeCommandBuffer();
        }
        
        public static CameraRenderer CreateCameraRenderer(Camera camera, HzCameraType type) {
            switch (type) {
                case HzCameraType.Game: return new GameCameraRenderer(camera);
                case HzCameraType.Reflection: return new GameCameraRenderer(camera);
#if UNITY_EDITOR     
                case HzCameraType.SceneView: return new SceneViewCameraRenderer(camera);
                case HzCameraType.Preview: return new SceneViewCameraRenderer(camera);
#endif
                default: throw new InvalidOperationException("Does not support camera type: " + type);
            }
        }

        public static HzCameraType GetCameraType(Camera camera)
        {
            switch (camera.cameraType) {
                case CameraType.Game: return HzCameraType.Game;
                case CameraType.VR: return HzCameraType.VR;
                case CameraType.Reflection: return HzCameraType.Game;
#if UNITY_EDITOR
                case CameraType.SceneView: return HzCameraType.SceneView;
                case CameraType.Preview: return HzCameraType.Preview;
#endif
                default: throw new InvalidOperationException("Does not support camera type: " + camera.cameraType);
            }
        }
    }
    
    public enum HzCameraType
    {
        Game = 1,
        SceneView = 2,
        Preview = 4,
        VR = 8,
        Reflection = 16
    }
}

