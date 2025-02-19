using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

public class ClusterLight
{
   public static int numClusterX = 16;
   public static int numClusterY = 16;
   public static int numClusterZ = 16;
   public static int maxNumLights = 1024;
   public static int maxNumLightsPerCluster = 128;

   private static int lightSize = 32;
   struct PointLight
   {
      public Vector3 color;
      public float intensity;
      public Vector3 position;
      public float radius;
   };

   private static int clusterBoxSize = 8 * 3 * 4;
   struct ClusterBox
   {
      public Vector3 p0, p1, p2, p3, p4, p5, p6, p7;
   };

   private static int indexSize = sizeof(int) * 2;
   struct LightIndex
   {
      public int count;
      public int start;
   };

   private ComputeShader clusterGenerateCS;
   private ComputeShader lightAssignCS;

   public ComputeBuffer clusterBuffer; //簇列表
   public ComputeBuffer lightBuffer;  //存储灯光对应信息
   public ComputeBuffer lightAssignBuffer; //存储光源ID
   public ComputeBuffer assignTable; //存储对该cluster产生影响的光源集合
   

   public ClusterLight()
   {
      int numClusters = numClusterX * numClusterY * numClusterZ;

      clusterBuffer = new ComputeBuffer(numClusters, clusterBoxSize);
      lightBuffer = new ComputeBuffer(maxNumLights, lightSize);
      lightAssignBuffer = new ComputeBuffer(numClusters * maxNumLightsPerCluster, sizeof(uint));
      assignTable = new ComputeBuffer(numClusters, indexSize);

      clusterGenerateCS = Resources.Load<ComputeShader>("Shader/ClusterGenerate");
      lightAssignCS = Resources.Load<ComputeShader>("Shader/LightAssign");
   }

   ~ClusterLight()
   {
      clusterBuffer.Release(); clusterBuffer = null;
      lightBuffer.Release(); lightBuffer = null;
      lightAssignBuffer.Release(); lightAssignBuffer = null;
      assignTable.Release(); assignTable = null;
   }

   public void ClusterGenerate(Camera camera)
   {
      Matrix4x4 viewMatrix = camera.worldToCameraMatrix;
      Matrix4x4 viewMatrixInv = viewMatrix.inverse;
      Matrix4x4 projMatrix = GL.GetGPUProjectionMatrix(camera.projectionMatrix, false);
      Matrix4x4 vpMatrix = projMatrix * viewMatrix;
      Matrix4x4 vpMatrixInv = vpMatrix.inverse;
      
      clusterGenerateCS.SetFloat("_near", camera.nearClipPlane);
      clusterGenerateCS.SetFloat("_far", camera.farClipPlane);
      clusterGenerateCS.SetFloat("_fov", camera.fieldOfView);
      clusterGenerateCS.SetFloat("_numClusterX", numClusterX);
      clusterGenerateCS.SetFloat("_numClusterY", numClusterY);
      clusterGenerateCS.SetFloat("_numClusterZ", numClusterZ);
      clusterGenerateCS.SetMatrix("_viewMatrix",viewMatrix);
      clusterGenerateCS.SetMatrix("_viewMatrixInv", viewMatrixInv);
      clusterGenerateCS.SetMatrix("_vpMatrix", vpMatrix);
      clusterGenerateCS.SetMatrix("_vpMatrixInv", vpMatrixInv);

      int kernel = clusterGenerateCS.FindKernel("ClusterGenerate");
      clusterGenerateCS.SetBuffer(kernel, "_clusterBuffer", clusterBuffer);
      clusterGenerateCS.Dispatch(kernel, numClusterZ, 1, 1);
   }

   public void UpdateLightBuffer(Light[] lights)
   {
      PointLight[] pointLights = new PointLight[maxNumLights];
      int count = 0;

      for (int i = 0; i < lights.Length; i++)
      {
         if (lights[i].type != LightType.Point) continue;

         PointLight pl;
         pl.color = new Vector3(lights[i].color.r, lights[i].color.g, lights[i].color.b);
         pl.intensity = lights[i].intensity;
         pl.position = lights[i].transform.position;
         pl.radius = lights[i].range;

         pointLights[count++] = pl;
      }
      lightBuffer.SetData(pointLights);
      
      lightAssignCS.SetInt("_numLights", count);
   }
   
   public void UpdateLightBuffer(VisibleLight[] lights)
   {
      PointLight[] pointLights = new PointLight[maxNumLights];
      int count = 0;

      for (int i = 0; i < lights.Length; i++)
      {
         var light = lights[i].light;
         if (light.type != LightType.Point) continue;

         PointLight pl;
         pl.color = new Vector3(light.color.r, light.color.g, light.color.b);
         pl.intensity = light.intensity;
         pl.position = light.transform.position;
         pl.radius = light.range;

         pointLights[count++] = pl;
      }
      lightBuffer.SetData(pointLights);
      
      lightAssignCS.SetInt("_numLights", count);
   }

   public void LightAssign()
   {
      lightAssignCS.SetInt("_maxNumLightsPerCluster", maxNumLightsPerCluster);
      lightAssignCS.SetFloat("_numClusterX", numClusterX);
      lightAssignCS.SetFloat("_numClusterY", numClusterY);
      lightAssignCS.SetFloat("_numClusterZ", numClusterZ);

      int kernel = lightAssignCS.FindKernel("LightAssign");
      lightAssignCS.SetBuffer(kernel, "_clusterBuffer", clusterBuffer);
      lightAssignCS.SetBuffer(kernel, "_lightBuffer", lightBuffer);
      lightAssignCS.SetBuffer(kernel, "_lightAssignBuffer", lightAssignBuffer);
      lightAssignCS.SetBuffer(kernel, "_assignTable", assignTable);
      
      lightAssignCS.Dispatch(kernel, numClusterZ, 1, 1);
   }

   public void SetShaderParameters()
   {
      Shader.SetGlobalFloat("_numClusterX", numClusterX);
      Shader.SetGlobalFloat("_numClusterY", numClusterY);
      Shader.SetGlobalFloat("_numClusterZ", numClusterZ);
      
      Shader.SetGlobalBuffer("_lightBuffer", lightBuffer);
      Shader.SetGlobalBuffer("_lightAssignBuffer", lightAssignBuffer);
      Shader.SetGlobalBuffer("_assignTable", assignTable);
   }
   
   void DrawBox(ClusterBox box, Color color)
   {
      Debug.DrawLine(box.p0, box.p1, color);   
      Debug.DrawLine(box.p0, box.p2, color);   
      Debug.DrawLine(box.p0, box.p4, color);
      
      Debug.DrawLine(box.p3, box.p1, color);   
      Debug.DrawLine(box.p3, box.p2, color);   
      Debug.DrawLine(box.p3, box.p7, color);
      
      Debug.DrawLine(box.p5, box.p1, color);   
      Debug.DrawLine(box.p5, box.p4, color);   
      Debug.DrawLine(box.p5, box.p7, color);
      
      Debug.DrawLine(box.p6, box.p2, color);   
      Debug.DrawLine(box.p6, box.p4, color);   
      Debug.DrawLine(box.p6, box.p7, color);
   }
   
   public void DebugCluster()
   {
      ClusterBox[] boxes = new ClusterBox[numClusterX * numClusterY * numClusterZ];
      clusterBuffer.GetData(boxes, 0, 0, numClusterX * numClusterY * numClusterZ);

      foreach (var box in boxes)
         DrawBox(box, Color.gray);
   }

   public void DebugLightAssign()
   {
      int numclusters = numClusterX * numClusterY * numClusterZ;

      ClusterBox[] boxes = new ClusterBox[numclusters];
      clusterBuffer.GetData(boxes, 0, 0, numclusters);

      LightIndex[] indices = new LightIndex[numclusters];
      assignTable.GetData(indices, 0, 0, numclusters);

      uint[] assignBuf = new uint[numclusters * maxNumLightsPerCluster];
      lightAssignBuffer.GetData(assignBuf, 0, 0, numclusters * maxNumLightsPerCluster);

      Color[] colors = { Color.red, Color.green, Color.blue, Color.yellow };

      for (int i = 0; i < indices.Length; i++)
      {
         if (indices[i].count > 0)
         {
            uint firstLightId = assignBuf[indices[i].start];
            DrawBox(boxes[i], colors[firstLightId % 4]);
            //Debug.Log(assignBuf[indices[i].start]);
         }
      }
   }
}
