using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[ExecuteAlways]
public class InstanceDebug : MonoBehaviour
{
    public InstanceData data;
    public ComputeShader computeShader;
    public Camera camera;

    public bool culling = false;
  
    private void Update()
    {
        if (camera == null) camera = Camera.main;
        if (computeShader == null) computeShader = Resources.Load<ComputeShader>("Shader/InstanceCulling");
        
        if(culling)
            InstanceDraw.Draw(data, camera, computeShader);
        else InstanceDraw.Draw(data);
    }

    private void OnDisable()
    {
        data.argsBuffer?.Release();
        data.argsBuffer = null;
        data.matrixBuffer?.Release();
        data.matrixBuffer = null;
        data.validMatrixBuffer?.Release();
        data.validMatrixBuffer = null;
    }
}
