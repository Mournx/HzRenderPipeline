using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[ExecuteAlways]
public class ClusterDebug : MonoBehaviour
{
  private ClusterLight clusterLight;

  private void Update()
  {
    if (clusterLight == null)
      clusterLight = new ClusterLight();

    var lights = FindObjectsOfType(typeof(Light)) as Light[];
    clusterLight.UpdateLightBuffer(lights);
    
    Camera camera = Camera.main;
    clusterLight.ClusterGenerate(camera);

    clusterLight.LightAssign();
    
    clusterLight.DebugCluster();
    clusterLight.DebugLightAssign();
  }
}
