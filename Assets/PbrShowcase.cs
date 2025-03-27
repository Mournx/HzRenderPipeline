using UnityEditor;
using UnityEngine;
using System;
using System.Diagnostics;
using UnityEditor.Rendering;

[ExecuteInEditMode]
public class PBRShowcase : MonoBehaviour
{
   [Min(1)] public int num = 11;
   public float interval = 1.2f;
   public Vector3 scale = Vector3.one;
   public Vector3 offset;
   public Vector3 rotation = Vector3.zero;
   public Mesh mesh;
   public Material mat;

   public Color color;
   public bool customizedInput;
   public bool varyMetallic;
   public float defaultMetallic;
   public float finalMetallic;
   public bool varySmoothness;
   public float defaultSmoothness;
   public float finalSmoothness;
   public float[] metallicValues;
   public float[] smoothnessValues;

   private MaterialPropertyBlock _materialPropertyBlock;
   private Matrix4x4[] _matrices;
   private Vector3[] _positions;
   private Vector4[] _colorValues;
   private float[] _metallicValues;
   private float[] _smoothnessValues;

   private MeshRenderer[] _meshRenderers;
   private bool wasPlaying;

   public void Awake()
   {
      Setup();
      GenerateSpheres();
      UpdateMaterialPropertyBlock();
   }

   public void Update()
   {
      if (!EditorApplication.isPlaying)
      {
         Graphics.DrawMeshInstanced(mesh,0,mat,_matrices,num,_materialPropertyBlock);
         wasPlaying = false;
      }
      else
      {
         if (!wasPlaying)
         {
            GenerateSpheres();
            UpdateMaterialPropertyBlock();
         }

         wasPlaying = true;
      }
   }

   private void Setup()
   {
      _colorValues = new Vector4[num];
      _positions = new Vector3[num];
      _metallicValues = new float[num];
      _smoothnessValues = new float[num];
      _matrices = new Matrix4x4[num];

      int mid = num / 2;
      var quat = Quaternion.Euler(rotation);
      for (int i = 0; i < num; i++)
      {
         var pos = offset;
         pos.x += (i - mid) * interval;
         _positions[i] = pos;
         pos += transform.position;
         _matrices[i] = Matrix4x4.TRS(pos, quat, scale);
         _colorValues[i] = color.linear;
         _metallicValues[i] = varyMetallic ? (customizedInput ? metallicValues[i] : Mathf.Lerp(defaultMetallic, finalMetallic, i / (num - 1f))) : defaultMetallic;
         _smoothnessValues[i] = varySmoothness ? (customizedInput ? smoothnessValues[i] : Mathf.Lerp(defaultSmoothness, finalSmoothness, i / (num - 1f))) : defaultSmoothness;
      }

      _materialPropertyBlock = new MaterialPropertyBlock();
      _materialPropertyBlock.SetVectorArray("_AlbedoTint", _colorValues);
      _materialPropertyBlock.SetFloatArray("_Metallic_global", _metallicValues);
      _materialPropertyBlock.SetFloatArray("_Smoothness_global", _smoothnessValues);
   }

   private void GenerateSpheres()
   {
      if (!EditorApplication.isPlaying) return;
      _meshRenderers = new MeshRenderer[num];
      for (var i = 0; i < num; i++)
      {
         var sphere = new GameObject("PBR Sphere" + i)
         {
            transform =
            {
               parent = transform,
               localPosition = _positions[i],
               localRotation = Quaternion.Euler(rotation),
               localScale = scale
            }
         };

         sphere.AddComponent<MeshFilter>().sharedMesh = mesh;
         var r = sphere.AddComponent<MeshRenderer>();
         _meshRenderers[i] = r;
         r.material = mat;
      }
   }

   private void UpdateMaterialPropertyBlock()
   {
      if (!EditorApplication.isPlaying) return;
      if (_meshRenderers == null || _meshRenderers.Length != num) return;
      for (var i = 0; i < num; i++)
      {
         var mpb = new MaterialPropertyBlock();
         mpb.SetVector("_AlbedoTint", _colorValues[i]);
         mpb.SetFloat("_Metallic_global", _metallicValues[i]);
         mpb.SetFloat("_Smoothness_global",_smoothnessValues[i]);
         _meshRenderers[i].SetPropertyBlock(mpb);
      }
   }

   private void OnValidate()
   {
      Setup();
      UpdateMaterialPropertyBlock();
   }
   
#if UNITY_EDITOR
   [Conditional("UNITY_EDITOR")]
   private void OnDrawGizmosSelected()
   {
      if (!enabled) return;

      GUIStyle blackStyle = new GUIStyle
      {
         normal =
         {
            textColor = Color.black
         }
      };

      var metallicArray = _materialPropertyBlock.GetFloatArray("_Metallic_global");
      var smoothnessArray = _materialPropertyBlock.GetFloatArray("_Smoothness_global");
      for (var i = 0; i < _matrices.Length; i++)
      {
         Handles.Label(_matrices[i].GetPosition() + new Vector3(-.5f, 1f, .0f), "M: " + metallicArray[i], blackStyle);
         Handles.Label(_matrices[i].GetPosition() + new Vector3(-.5f, 1.25f, .0f), "S: " + smoothnessArray[i], blackStyle);
      }
   }
#endif
}
