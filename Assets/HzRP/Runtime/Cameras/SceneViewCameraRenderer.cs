using System;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace HzRenderPipeline.Runtime.Cameras {
#if UNITY_EDITOR
    public class SceneViewCameraRenderer : GameCameraRenderer
    {
        public SceneViewCameraRenderer(Camera camera) : base(camera)
        {
            cameraType = HzCameraType.SceneView;
            _enableTaa = settings.enableTaaInEditor;
            _rendererDesc = "Render Scene View (" + camera.name + ")";
            beforeCull += EmitUIMesh;
            beforePostProcess += DrawPreImageGizmosPass;
            afterLastPass += DrawPostImageGizmosPass;
        }

        public void EmitUIMesh() => ScriptableRenderContext.EmitWorldGeometryForSceneView(camera);
        
        public void DrawPreImageGizmosPass() {
            if (Handles.ShouldRenderGizmos()) {
                _context.DrawGizmos(camera, GizmoSubset.PreImageEffects);
            }
        }

        public void DrawPostImageGizmosPass() {
            if (Handles.ShouldRenderGizmos()) {
                _context.DrawGizmos(camera, GizmoSubset.PostImageEffects);
            }
        }
    }
#else
    public class SceneViewCameraRenderer : GameCameraRenderer{
        public SceneViewCameraRenderer(Camera camera) : base(camera){
            cameraType = HzCameraType.SceneView;
            _enableTaa = settings.enableTaaInEditor;
            _rendererDesc = "Render Scene View (" + camera.name + ")";
        }
    }
#endif
}

