using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

[CustomEditor(typeof(InstanceData))]
public class InstanceDataEditor : Editor
{
    public override void OnInspectorGUI()
    {
      base.OnInspectorGUI();
      var instanceData = (InstanceData)target;
      if (GUILayout.Button("Generate InstanceData randomly", GUILayout.Height(40)))
        instanceData.GenerateRandomData();
    }
}
