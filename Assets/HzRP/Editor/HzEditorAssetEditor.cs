using System;
using System.IO;
using Unity.VisualScripting;
using UnityEditor;
using UnityEngine;

namespace HzRP.Editor {
    
    [CustomEditor(typeof(HzEditorAsset))]
    public class HzEditorAssetEditor : UnityEditor.Editor {

        private static RenderTexture lut;
        private static string errorText = "";

        public override void OnInspectorGUI()
        {
            base.OnInspectorGUI();

            HzEditorAsset asset = target as HzEditorAsset;
            if (asset == null) return;
            
            GUILayout.Space(20);
			
            GUILayout.BeginHorizontal();

            if (lut && lut.IsCreated() && lut.isReadable && !lut.IsDestroyed()) {
                if (!AssetDatabase.Contains(lut)) {
                    if (GUILayout.Button("Save IBL Lut")) {
                        var path = EditorUtility.SaveFilePanelInProject("Save IBL BRDF Lut", "IBL BRDF Lut.png", "png", "Select a location to save", "Assets/HzRP/Editor/");
                        if (!string.IsNullOrEmpty(path)) {
                            Texture2D save = new Texture2D(lut.width, lut.height);
                            var temp = RenderTexture.active;
                            RenderTexture.active = lut;
                            save.ReadPixels(new Rect(0, 0, lut.width, lut.height), 0, 0);
                            RenderTexture.active = temp;
                            byte[] data = save.EncodeToPNG();
                            File.WriteAllBytes(Path.GetFullPath(path), data);
                            // AssetDatabase.CreateAsset(lut, path);
                            AssetDatabase.ImportAsset(path);
                            Debug.Log("IBL Lut Saved");
                            errorText = "";
                        }
                    } else if (GUILayout.Button("Clear IBL Lut Cache")) {
                        lut.Release();
                        lut = null;
                        errorText = "";
                    }
                }
            }

            bool iblLutError = false;
            if (GUILayout.Button("Generate IBL Lut(s)")) {
                if (asset == null) {
                    iblLutError = true;
                    errorText = "Hz Editor Asset cannot be null!";
                } else if (asset.iblLutResolution < 128) {
                    iblLutError = true;
                    errorText = "IBL Lut Resolution cannot be smaller than 128!";
                } else if (asset.iblLutResolution > 1024) {
                    iblLutError = true;
                    errorText = "IBL Lut Resolution cannot be larger than 1024!";
                } else if (asset.iblLutGenerateShader == null) {
                    iblLutError = true;
                    errorText = "IBL Lut Generation Shader cannot be null!";
                }else {
                    var lutShader = asset.iblLutGenerateShader;
                    int kernel = lutShader.FindKernel("GenerateIBLLut");

                    if (lut != null) {
                        lut.Release();
                        lut = null;
                    }

                    var desc = new RenderTextureDescriptor(asset.iblLutResolution, asset.iblLutResolution, asset.iblLutFormat, 0) {
                        enableRandomWrite = true,
                        useMipMap = false,
                        sRGB = false
                    };

                    lut = new RenderTexture(desc);
                    lut.Create();

                    int tX = Mathf.CeilToInt(asset.iblLutResolution / 8f);
                    int tY = Mathf.CeilToInt(asset.iblLutResolution / 8f);

                    lutShader.SetFloat("_Width", asset.iblLutResolution);
                    lutShader.SetFloat("_Height", asset.iblLutResolution);
                    lutShader.SetTexture(kernel, "_ResultLut", lut);

                    Debug.Log("Start generating IBL Lut");
                    errorText = "";

                    try {
                        lutShader.Dispatch(kernel, tX, tY, 1);
                    }
                    catch (Exception e) {
                        lut.Release();
                        lut = null;
                        iblLutError = true;
                        errorText = e.Message;
                    }
                    
                    Debug.Log("Finish generating IBL Lut");
                }
            }
        }
    }
}

