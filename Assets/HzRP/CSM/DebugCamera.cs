using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[ExecuteAlways]
public class DebugCamera : MonoBehaviour
{
    private CSM csm;
    public CSMSettings csmSettings;

    void Update()
    {
        Camera mainCam = Camera.main;

        Light light = RenderSettings.sun;
        Vector3 lightDir = light.transform.rotation * Vector3.forward;

        if (csm == null) csm = new CSM();
        csm.Update(mainCam, lightDir, csmSettings);
        csm.DebugDraw();
    }
}
