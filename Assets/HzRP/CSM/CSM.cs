using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace HzRenderPipeline.Runtime
{
    public class CSM
    {
        public float[] splts = { 0.07f, 0.13f, 0.25f, 0.55f };

        public float[] orthoWidths = new float[4];

        //主相机视锥体
        private Vector3[] nearCorners = new Vector3[4];
        private Vector3[] farCorners = new Vector3[4];

        private Vector3[] f0_near = new Vector3[4], f0_far = new Vector3[4];
        private Vector3[] f1_near = new Vector3[4], f1_far = new Vector3[4];
        private Vector3[] f2_near = new Vector3[4], f2_far = new Vector3[4];
        private Vector3[] f3_near = new Vector3[4], f3_far = new Vector3[4];

        //主相机视锥体包围盒
        private Vector3[] box0, box1, box2, box3;

        struct MainCameraSettings
        {
            public Vector3 position;
            public Quaternion rotation;
            public float nearClipPlane;
            public float farClipPlane;
            public float aspect;
        };

        private MainCameraSettings cameraSettings;

        //齐次坐标矩阵乘法变换
        Vector3 matTransform(Matrix4x4 m, Vector3 v, float w)
        {
            Vector4 v4 = new Vector4(v.x, v.y, v.z, w);
            v4 = m * v4;
            return new Vector3(v4.x, v4.y, v4.z);
        }

        public void SaveMainCameraSettings(ref Camera camera)
        {
            cameraSettings.position = camera.transform.position;
            cameraSettings.rotation = camera.transform.rotation;
            cameraSettings.nearClipPlane = camera.nearClipPlane;
            cameraSettings.farClipPlane = camera.farClipPlane;
            cameraSettings.aspect = camera.aspect;
            camera.orthographic = true;
        }

        public void RevertMainCameraSettings(ref Camera camera)
        {
            camera.transform.position = cameraSettings.position;
            camera.transform.rotation = cameraSettings.rotation;
            camera.nearClipPlane = cameraSettings.nearClipPlane;
            camera.farClipPlane = cameraSettings.farClipPlane;
            camera.aspect = cameraSettings.aspect;
            camera.orthographic = false;
        }

        // calculate world pos of light view bounding box
        Vector3[] lightSpaceOBB(Vector3[] nearCorners, Vector3[] farCorners, Vector3 lightDir)
        {
            Vector3 min = new Vector3(float.MaxValue, float.MaxValue, float.MaxValue);
            Vector3 max = new Vector3(-float.MaxValue, -float.MaxValue, -float.MaxValue);

            for (int i = 0; i < nearCorners.Length; i++)
            {
                Vector3 point = Quaternion.FromToRotation(lightDir, Vector3.forward) * nearCorners[i];
                min = Vector3.Min(min, point);
                max = Vector3.Max(max, point);
            }

            for (int i = 0; i < farCorners.Length; i++)
            {
                Vector3 point = Quaternion.FromToRotation(lightDir, Vector3.forward) * farCorners[i];
                min = Vector3.Min(min, point);
                max = Vector3.Max(max, point);
            }

            Vector3[] obbCorners =
            {
                min, new Vector3(min.x, min.y, max.z), new Vector3(min.x, max.y, min.z),
                new Vector3(min.x, max.y, max.z), new Vector3(max.x, min.y, min.z), new Vector3(max.x, min.y, max.z),
                new Vector3(max.x, max.y, min.z), max
            };

            for (int i = 0; i < obbCorners.Length; i++)
            {
                obbCorners[i] = Quaternion.FromToRotation(Vector3.forward, lightDir) * obbCorners[i];
            }

            return obbCorners;
        }

        public void Update(Camera mainCam, Vector3 lightDir)
        {
            //获取主相机视锥体
            mainCam.CalculateFrustumCorners(new Rect(0, 0, 1, 1), mainCam.nearClipPlane,
                Camera.MonoOrStereoscopicEye.Mono, nearCorners);
            mainCam.CalculateFrustumCorners(new Rect(0, 0, 1, 1), mainCam.farClipPlane,
                Camera.MonoOrStereoscopicEye.Mono, farCorners);

            for (int i = 0; i < 4; i++)
            {
                nearCorners[i] = mainCam.transform.TransformVector(nearCorners[i]) + mainCam.transform.position;
                farCorners[i] = mainCam.transform.TransformVector(farCorners[i]) + mainCam.transform.position;
            }

            //划分相机视锥体
            for (int i = 0; i < 4; i++)
            {
                Vector3 dir = farCorners[i] - nearCorners[i];

                f0_near[i] = nearCorners[i];
                f0_far[i] = f0_near[i] + dir * splts[0];

                f1_near[i] = f0_far[i];
                f1_far[i] = f1_near[i] + dir * splts[1];

                f2_near[i] = f1_far[i];
                f2_far[i] = f2_near[i] + dir * splts[2];

                f3_near[i] = f2_far[i];
                f3_far[i] = f3_near[i] + dir * splts[3];
            }

            box0 = lightSpaceOBB(f0_near, f0_far, lightDir);
            box1 = lightSpaceOBB(f1_near, f1_far, lightDir);
            box2 = lightSpaceOBB(f2_near, f2_far, lightDir);
            box3 = lightSpaceOBB(f3_near, f3_far, lightDir);

            orthoWidths[0] = Vector3.Magnitude(f0_far[0] - f0_near[0]);
            orthoWidths[1] = Vector3.Magnitude(f1_far[0] - f1_near[0]);
            orthoWidths[2] = Vector3.Magnitude(f2_far[0] - f2_near[0]);
            orthoWidths[3] = Vector3.Magnitude(f3_far[0] - f3_near[0]);
        }

        public void Update(Camera mainCam, Vector3 lightDir, CSMSettings csmSettings)
        {
            //获取主相机视锥体
            mainCam.CalculateFrustumCorners(new Rect(0, 0, 1, 1), mainCam.nearClipPlane,
                Camera.MonoOrStereoscopicEye.Mono, nearCorners);
            mainCam.CalculateFrustumCorners(new Rect(0, 0, 1, 1), csmSettings.maxDistance,
                Camera.MonoOrStereoscopicEye.Mono, farCorners);

            for (int i = 0; i < 4; i++)
            {
                nearCorners[i] = mainCam.transform.TransformVector(nearCorners[i]) + mainCam.transform.position;
                farCorners[i] = mainCam.transform.TransformVector(farCorners[i]) + mainCam.transform.position;
            }

            //划分相机视锥体
            for (int i = 0; i < 4; i++)
            {
                Vector3 dir = farCorners[i] - nearCorners[i];

                f0_near[i] = nearCorners[i];
                f0_far[i] = f0_near[i] + dir * splts[0];

                f1_near[i] = f0_far[i];
                f1_far[i] = f1_near[i] + dir * splts[1];

                f2_near[i] = f1_far[i];
                f2_far[i] = f2_near[i] + dir * splts[2];

                f3_near[i] = f2_far[i];
                f3_far[i] = f3_near[i] + dir * splts[3];
            }

            box0 = lightSpaceOBB(f0_near, f0_far, lightDir);
            box1 = lightSpaceOBB(f1_near, f1_far, lightDir);
            box2 = lightSpaceOBB(f2_near, f2_far, lightDir);
            box3 = lightSpaceOBB(f3_near, f3_far, lightDir);

            orthoWidths[0] = Vector3.Magnitude(f0_far[0] - f0_near[0]);
            orthoWidths[1] = Vector3.Magnitude(f1_far[0] - f1_near[0]);
            orthoWidths[2] = Vector3.Magnitude(f2_far[0] - f2_near[0]);
            orthoWidths[3] = Vector3.Magnitude(f3_far[0] - f3_near[0]);
        }

        public void ConfigCameraToShadowSpace(ref Camera camera, Vector3 lightDir, int level, int resolution)
        {
            var box = new Vector3[8];
            float len = 0;
            if (level == 0)
            {
                box = box0;
                len = Vector3.Magnitude(f0_far[2] - f0_near[0]);
            }

            if (level == 1)
            {
                box = box1;
                len = Vector3.Magnitude(f1_far[2] - f1_near[0]);
            }

            if (level == 2)
            {
                box = box2;
                len = Vector3.Magnitude(f2_far[2] - f2_near[0]);
            }

            if (level == 3)
            {
                box = box3;
                len = Vector3.Magnitude(f3_far[2] - f3_near[0]);
            }

            Vector3 center = (box[2] + box[4]) / 2;
            float w = Vector3.Magnitude(box[0] - box[4]);
            float h = Vector3.Magnitude(box[0] - box[2]);
            //float len = Mathf.Max(w, h);
            float disPerPixel = len / resolution;
            float d = Vector3.Magnitude(box[0] - box[1]);

            //相机坐标转到光源坐标系下取整
            Matrix4x4 toLightViewInv = Matrix4x4.LookAt(Vector3.zero, lightDir, Vector3.up);
            Matrix4x4 toLightView = toLightViewInv.inverse;

            center = matTransform(toLightView, center, 1.0f);
            for (int i = 0; i < 3; i++)
                center[i] = Mathf.Floor(center[i] / disPerPixel) * disPerPixel;
            center = matTransform(toLightViewInv, center, 1.0f);

            camera.transform.rotation = Quaternion.LookRotation(lightDir);
            camera.transform.position = center;
            camera.nearClipPlane = 0;
            camera.farClipPlane = d;
            camera.aspect = 1.0f;
            camera.orthographicSize = len * 0.5f;
        }

        //Debug
        void DrawFrustum(Vector3[] nearCorners, Vector3[] farCorners, Color color)
        {
            for (int i = 0; i < 4; i++)
                Debug.DrawLine(nearCorners[i], farCorners[i], color);

            Debug.DrawLine(nearCorners[0], nearCorners[1], color);
            Debug.DrawLine(nearCorners[0], nearCorners[3], color);
            Debug.DrawLine(nearCorners[2], nearCorners[1], color);
            Debug.DrawLine(nearCorners[2], nearCorners[3], color);
            Debug.DrawLine(farCorners[0], farCorners[1], color);
            Debug.DrawLine(farCorners[0], farCorners[3], color);
            Debug.DrawLine(farCorners[2], farCorners[1], color);
            Debug.DrawLine(farCorners[2], farCorners[3], color);
        }

        void DrawAABB(Vector3[] points, Color color)
        {
            Debug.DrawLine(points[0], points[1], color);
            Debug.DrawLine(points[0], points[2], color);
            Debug.DrawLine(points[0], points[4], color);

            Debug.DrawLine(points[3], points[1], color);
            Debug.DrawLine(points[3], points[2], color);
            Debug.DrawLine(points[3], points[7], color);

            Debug.DrawLine(points[5], points[1], color);
            Debug.DrawLine(points[5], points[7], color);
            Debug.DrawLine(points[5], points[4], color);

            Debug.DrawLine(points[6], points[2], color);
            Debug.DrawLine(points[6], points[7], color);
            Debug.DrawLine(points[6], points[4], color);
        }

        public void DebugDraw()
        {
            DrawFrustum(nearCorners, farCorners, Color.white);
            DrawAABB(box0, Color.yellow);
            DrawAABB(box1, Color.magenta);
            DrawAABB(box2, Color.green);
            DrawAABB(box3, Color.cyan);
        }
    }
}