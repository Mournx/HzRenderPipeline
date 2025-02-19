using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class moon : MonoBehaviour
{
    public Transform Target;
    public float SelfSpeed = 1.0f;
    public float RotateSpeed = 1.0f;
    private float distance;
    Vector3 dir;

    void Start()
    {
        dir = transform.position - Target.position;

        distance = Vector3.Distance(transform.position, Target.position);
    }

    void Update()
    {
        transform.position = Target.position + dir.normalized * distance;
        
        transform.RotateAround(Target.position, Vector3.up, RotateSpeed);
        
        dir = transform.position - Target.position;

        this.transform.Rotate(Vector3.up * SelfSpeed, Space.World);
    }
}
