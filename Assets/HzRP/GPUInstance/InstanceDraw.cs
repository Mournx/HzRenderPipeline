using System.Collections;
using System.Collections.Generic;
using System.Data.Common;
using UnityEngine;
using UnityEngine.Rendering;

public class InstanceDraw
{
   public static void Init(InstanceData data)
   {
      if (data.matrixBuffer != null && data.validMatrixBuffer != null && data.argsBuffer != null) return;

      int mat4x4Size = 4 * 4 * 4;
      data.matrixBuffer = new ComputeBuffer(data.instanceCount, mat4x4Size);
      data.validMatrixBuffer = new ComputeBuffer(data.instanceCount, mat4x4Size, ComputeBufferType.Append);
      data.argsBuffer = new ComputeBuffer(5, sizeof(uint), ComputeBufferType.IndirectArguments);
      
      data.matrixBuffer.SetData(data.mats);

      uint[] args = new uint[5] { 0, 0, 0, 0, 0 };
      if (data.instanceMesh != null)
      {
         args[0] = (uint)data.instanceMesh.GetIndexCount(data.subMeshIndex);
         args[1] = (uint)0;
         args[2] = (uint)data.instanceMesh.GetIndexStart(data.subMeshIndex);
         args[3] = (uint)data.instanceMesh.GetBaseVertex(data.subMeshIndex);
      }
      data.argsBuffer.SetData(args);
   }

   public static Vector4[] CovertBoundsToVectorArray(Bounds bounds)
   {
      Vector4[] boundingBox = new Vector4[8];
      boundingBox[0] = new Vector4(bounds.min.x, bounds.min.y, bounds.min.z, 1);
      boundingBox[1] = new Vector4(bounds.max.x, bounds.max.y, bounds.max.z, 1);
      boundingBox[2] = new Vector4(boundingBox[0].x, boundingBox[0].y, boundingBox[1].z, 1);
      boundingBox[3] = new Vector4(boundingBox[0].x, boundingBox[1].y, boundingBox[0].z, 1);
      boundingBox[4] = new Vector4(boundingBox[1].x, boundingBox[0].y, boundingBox[0].z, 1);
      boundingBox[5] = new Vector4(boundingBox[0].x, boundingBox[1].y, boundingBox[1].z, 1);
      boundingBox[6] = new Vector4(boundingBox[1].x, boundingBox[0].y, boundingBox[1].z, 1);
      boundingBox[7] = new Vector4(boundingBox[1].x, boundingBox[1].y, boundingBox[0].z, 1);
      return boundingBox;
   }

   public static void Draw(InstanceData data)
   {
      if (data == null) return;
      Init(data);

      uint[] args = new uint[5] { 0, 0, 0, 0, 0 };
      data.argsBuffer.GetData(args);
      args[1] = (uint)data.instanceCount;
      data.argsBuffer.SetData(args);
      
      data.instanceMaterial.SetBuffer("_validMatrixBuffer", data.matrixBuffer);

      Graphics.DrawMeshInstancedIndirect(data.instanceMesh, data.subMeshIndex, data.instanceMaterial,
         new Bounds(Vector3.zero, new Vector3(100.0f, 100.0f, 100.0f)), data.argsBuffer);
   }
   
   // frustum culling
   public static void Draw(InstanceData data, Camera camera, ComputeShader computeShader)
   {
      if (data == null || camera == null || computeShader == null) return;
      Init(data);

      uint[] args = new uint[5] { 0, 0, 0, 0, 0 };
      data.argsBuffer.GetData(args);
      args[1] = 0;
      data.argsBuffer.SetData(args);
      data.validMatrixBuffer.SetCounterValue(0);

      Plane[] ps = GeometryUtility.CalculateFrustumPlanes(camera);
      Vector4[] planes = new Vector4[6];
      for (int i = 0; i < 6; i++)
         planes[i] = new Vector4(ps[i].normal.x, ps[i].normal.y, ps[i].normal.z, ps[i].distance);

      Vector4[] bounds = CovertBoundsToVectorArray(data.instanceMesh.bounds);

      int kernel = computeShader.FindKernel("CSMain");
      computeShader.SetVectorArray("_planes", planes);
      computeShader.SetVectorArray("_bounds", bounds);
      computeShader.SetInt("_instanceCount", data.instanceCount);
      computeShader.SetBuffer(kernel, "_matrixBuffer", data.matrixBuffer);
      computeShader.SetBuffer(kernel, "_validMatrixBuffer", data.validMatrixBuffer);
      computeShader.SetBuffer(kernel, "_argsBuffer", data.argsBuffer);
      data.instanceMaterial.SetBuffer("_validMatrixBuffer", data.validMatrixBuffer);

      int dispatchNum = (data.instanceCount / 128) + 1;
      computeShader.Dispatch(kernel, dispatchNum, 1, 1);

      Graphics.DrawMeshInstancedIndirect(data.instanceMesh, data.subMeshIndex, data.instanceMaterial,
         new Bounds(Vector3.zero, new Vector3(100.0f, 100.0f, 100.0f)), data.argsBuffer);
   }
   
   public static void Draw(InstanceData data, Camera camera, ComputeShader computeShader, Matrix4x4 vpMatrix, RenderTexture hizBuffer, ref CommandBuffer cmd)
   {
      if (data == null || camera == null || computeShader == null) return;
      Init(data);

      uint[] args = new uint[5] { 0, 0, 0, 0, 0 };
      data.argsBuffer.GetData(args);
      args[1] = 0;
      data.argsBuffer.SetData(args);
      data.validMatrixBuffer.SetCounterValue(0);

      Plane[] ps = GeometryUtility.CalculateFrustumPlanes(camera);
      Vector4[] planes = new Vector4[6];
      for (int i = 0; i < 6; i++)
         planes[i] = new Vector4(ps[i].normal.x, ps[i].normal.y, ps[i].normal.z, ps[i].distance);

      Vector4[] bounds = CovertBoundsToVectorArray(data.instanceMesh.bounds);

      int kernel = computeShader.FindKernel("CSMain");
      computeShader.SetMatrix("_vpMatrix", vpMatrix);
      computeShader.SetVectorArray("_planes", planes);
      computeShader.SetVectorArray("_bounds", bounds);
      computeShader.SetInt("_size", hizBuffer.width);
      computeShader.SetInt("_instanceCount", data.instanceCount);
      computeShader.SetBuffer(kernel, "_matrixBuffer", data.matrixBuffer);
      computeShader.SetBuffer(kernel, "_validMatrixBuffer", data.validMatrixBuffer);
      computeShader.SetBuffer(kernel, "_argsBuffer", data.argsBuffer);
      computeShader.SetTexture(kernel, "_hizBuffer", hizBuffer);
      data.instanceMaterial.SetBuffer("_validMatrixBuffer", data.validMatrixBuffer);

      int dispatchNum = (data.instanceCount / 128) + 1;
      computeShader.Dispatch(kernel, dispatchNum, 1, 1);

      cmd.DrawMeshInstancedIndirect(data.instanceMesh, data.subMeshIndex, data.instanceMaterial,
         -1, data.argsBuffer);
   }
}
