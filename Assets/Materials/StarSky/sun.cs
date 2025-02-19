using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class sun : MonoBehaviour
{
    public float SelfSpeed = 1.0f;
    void Update()
    {
        this.transform.Rotate(Vector3.up * SelfSpeed, Space.World);
    }
}
