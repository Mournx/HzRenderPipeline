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
}
