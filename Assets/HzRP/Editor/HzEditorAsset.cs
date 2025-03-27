using UnityEditor;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
namespace HzRP.Editor {
    [CreateAssetMenu(fileName = "HzRP Editor Asset", menuName = "Hz Render Pipeline / HzRP Editor Asset", order = 0)]
    public class HzEditorAsset : ScriptableObject
    {
        [Range(128, 1024)] public int iblLutResolution = 1024;
        public GraphicsFormat iblLutFormat = GraphicsFormat.R16G16B16A16_UNorm;
        public ComputeShader iblLutGenerateShader;

        [MenuItem("Hz RP/Syste/Log System Info")]
        public static void LogSystemInfo()
        {
            Debug.Log("Uses Reversed ZBuffer: " + SystemInfo.usesReversedZBuffer);
            Debug.Log("Supports Instancing: " + SystemInfo.supportsInstancing);
            Debug.Log("Supports Async Compute: " + SystemInfo.supportsAsyncCompute);
            Debug.Log("Supports Conservative Raster: " + SystemInfo.supportsConservativeRaster);
            Debug.Log("Supports Geometry Shaders: " + SystemInfo.supportsGeometryShaders);
            Debug.Log("Supports Compute Shaders: " + SystemInfo.supportsComputeShaders);
            Debug.Log("Supports Tessellation Shaders: " + SystemInfo.supportsTessellationShaders);
            Debug.Log("Supports Graphics Fence: " + SystemInfo.supportsGraphicsFence);
            Debug.Log("Copy Texture Support: " + SystemInfo.copyTextureSupport);
            Debug.Log("Supports Vibration: " + SystemInfo.supportsVibration);
        }
    }
}

