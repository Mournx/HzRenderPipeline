using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class earth : MonoBehaviour
{
    public Transform Target;
    public float SelfSpeed = 1.0f;
    public float RotateSpeed = 1.0f;
    void Update()
    {
        this.transform.RotateAround(Target.position, Vector3.up, RotateSpeed);
        this.transform.Rotate(Vector3.up * SelfSpeed, Space.World);
    }
}
