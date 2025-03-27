using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "Hz Render Pipeline/InstanceData")]
public class InstanceData : ScriptableObject
{
   [HideInInspector] public Matrix4x4[] mats;  //变换矩阵

   [HideInInspector] public ComputeBuffer matrixBuffer;       // instance矩阵
   [HideInInspector] public ComputeBuffer validMatrixBuffer;  // GPU剔除后的矩阵
   [HideInInspector] public ComputeBuffer argsBuffer;        // 绘制参数

   [HideInInspector] public int subMeshIndex = 0;
   [HideInInspector] public int instanceCount = 0;

   public Mesh instanceMesh;
   public Material instanceMaterial;

   public Vector3 center = new Vector3(0, 0, 0);
   public int instanceNum = 5000;
   public float minDistance = 5.0f;
   public float maxDistance = 50.0f;
   public float minHeight = -0.5f;
   public float maxHeight = 0.5f;
   
   
   public void GenerateRandomData()
   {
      instanceCount = instanceNum;

      mats = new Matrix4x4[instanceCount];
      for (int i = 0; i < instanceCount; i++)
      {
         float angle = Random.Range(0.0f, Mathf.PI * 2.0f);
         float distance = Mathf.Sqrt(Random.Range(0.0f, 1.0f)) * (maxDistance - minDistance) + minDistance;
         float height = Random.Range(minHeight, maxHeight);

         Vector3 pos = new Vector3(Mathf.Sin(angle) * distance, height, Mathf.Cos(angle) * distance);
         Vector3 dir = pos - center;

         Quaternion q = new Quaternion();
         q.SetLookRotation(dir, new Vector3(0, 1, 0));

         Matrix4x4 m = Matrix4x4.Rotate(q);
         m.SetColumn(3, new Vector4(pos.x, pos.y, pos.z, 1));

         mats[i] = m;
      }
      
      matrixBuffer.Release(); matrixBuffer = null;
      validMatrixBuffer.Release(); validMatrixBuffer = null;
      argsBuffer.Release(); argsBuffer = null;
      
      Debug.Log("Successfully Generate Instance Data.");
   }

   public void CleanData()
   {
      matrixBuffer.Release(); matrixBuffer = null;
      validMatrixBuffer.Release(); validMatrixBuffer = null;
      argsBuffer.Release(); argsBuffer = null;
      
      Debug.Log("Successfully clean Instance Data.");
   }
}
