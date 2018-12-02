using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
#if UNITY_2017_2_OR_NEWER
using XRSettings = UnityEngine.XR.XRSettings;
#else
using XRSettings = UnityEngine.VR.VRSettings;
#endif

public enum TargetDevices { AutoDetect, PC, Hololens, AppStore, WindowsMixedReality, Vive, Oculus, PostEffects, Unity55PC };

public class CameraView
{
    public int cameraId
    {
        get; private set;
    }
    public float viewportWidth
    {
        get; private set;
    }
    public float viewportHeight
    {
        get; private set;
    }

    public bool isGameCamera
    {
        get; private set;
    }

    public bool isPreviewCamera
    {
        get; private set;
    }

    public bool isStereoscopic
    {
        get; private set;
    }

    public StereoTargetEyeMask targetEye
    {
        get; private set;
    }

    public Matrix4x4 MVP
    {
        get; private set;
    }

    public bool isLeftHandedness
    {
        get; private set;
    }

    public bool isFlipHand
    {
        get; private set;
    }

    private SVFCameraView _svfCamView;
    public SVFCameraView InteropCamView
    {
        get
        {
            return _svfCamView;
        }
    }

    private Matrix4x4 q1;

    public CameraView(int id, Camera cam, TargetDevices tgt, bool flipHand)
    {
        cameraId = id;
        isFlipHand = flipHand;

        viewportWidth = (float)cam.pixelWidth;
        viewportHeight = (float)cam.pixelHeight;
        isGameCamera = (cam.cameraType == CameraType.Game);
        isPreviewCamera = (cam.name == "Preview Camera");

        q1 = new Matrix4x4();
        q1 = Matrix4x4.identity;
        q1[1, 1] = -1.0f;

        if (tgt == TargetDevices.AutoDetect)
        {
            AutoDetectDevice();
        }
        else
        {
            ConfigureSetupMVP(tgt);
        }

        isStereoscopic = cam.stereoEnabled;
        if (cam.stereoEnabled)
        {
            if (cam.stereoTargetEye != StereoTargetEyeMask.Left && cam.stereoTargetEye != StereoTargetEyeMask.Right)
            {
                Debug.LogError("ERROR: stereo camera must be either Left or Right, cannot be 'None' or 'Both'");
                throw new Exception("Fix the camera settings");
            }
        }
        targetEye = (false == cam.stereoEnabled) ? StereoTargetEyeMask.Left : cam.stereoTargetEye;
        MVP = new Matrix4x4();
        // When isGameCamera is true the plugin does backface culling and front face culling when false.  
        _svfCamView = new SVFCameraView() { cameraId = cameraId, isGameCamera = isLeftHandedness, isStereoscopic = isStereoscopic, viewportWidth = viewportWidth, viewportHeight = viewportHeight, targetEye = targetEye };
    }

    public void AutoDetectDevice()
    {
        TargetDevices tgt = TargetDevices.AutoDetect;
        if (XRSettings.enabled)
        {
            string stDevice = XRSettings.loadedDeviceName.ToLower();
            if (stDevice == "windowsmr")
            {
                tgt = TargetDevices.WindowsMixedReality;
            }
            else if (stDevice == "oculus")
            {
                tgt = TargetDevices.Oculus;
            }
            else if (stDevice == "openvr")
            {
                tgt = TargetDevices.Vive;
            }
        }
        ConfigureSetupMVP(tgt);
    }

    public void ConfigureSetupMVP(TargetDevices tgt)
    {
        switch (tgt)
        {
            case TargetDevices.Vive:
            case TargetDevices.Oculus:
            case TargetDevices.PostEffects:
            case TargetDevices.WindowsMixedReality:
#if UNITY_5_6_OR_NEWER && !UNITY_EDITOR
            case TargetDevices.AutoDetect:
            case TargetDevices.AppStore:
            case TargetDevices.Hololens:
            case TargetDevices.PC:
#endif
                isLeftHandedness = isFlipHand;
                if (isPreviewCamera)
                {
                    isLeftHandedness = !isFlipHand;
                    q1[1, 1] = 1.0f;
                }
                break;

#if !UNITY_5_6_OR_NEWER || UNITY_EDITOR
            case TargetDevices.AutoDetect:
            case TargetDevices.AppStore:
            case TargetDevices.Hololens:
            case TargetDevices.PC:
#endif
            case TargetDevices.Unity55PC:
            default:
                isLeftHandedness = isFlipHand ^ isGameCamera;
                if (isGameCamera)
                {
                    q1[1, 1] = 1.0f;
                }
                break;
        }
    }

    private static Matrix4x4 flipHandMatrix = Matrix4x4.Scale(new Vector3(-1, 1, 1));

    public void Update(Camera cam, Matrix4x4 m)
    {
        viewportWidth = (float)cam.pixelWidth;
        viewportHeight = (float)cam.pixelHeight;
        _svfCamView.viewportWidth = viewportWidth;
        _svfCamView.viewportHeight = viewportHeight;

        Matrix4x4 v = cam.worldToCameraMatrix;
        Matrix4x4 p = GL.GetGPUProjectionMatrix(cam.projectionMatrix, false);

        // handles handedness flip
        Matrix4x4 mf = m;
        if (isFlipHand)
        {
            mf = m * flipHandMatrix;
        }

        //**********************************************************************
        // Apparently there is an issue with Unity when using a custom shader where
        // the output is being treated as a left-handed coordinate system. This is
        // an known bug when using certain rendering effects, but appears to be the same
        // issue when using custom vertex shaders. Flipping the coordinate system (matrix q)
        // so that the final image is flipped upside down seems to fix the issue for
        // scene and preview cameras.
        //**********************************************************************
        MVP = q1 * p * v * mf;

        _svfCamView.m00 = MVP.m00; _svfCamView.m01 = MVP.m01; _svfCamView.m02 = MVP.m02; _svfCamView.m03 = MVP.m03;
        _svfCamView.m10 = MVP.m10; _svfCamView.m11 = MVP.m11; _svfCamView.m12 = MVP.m12; _svfCamView.m13 = MVP.m13;
        _svfCamView.m20 = MVP.m20; _svfCamView.m21 = MVP.m21; _svfCamView.m22 = MVP.m22; _svfCamView.m23 = MVP.m23;
        _svfCamView.m30 = MVP.m30; _svfCamView.m31 = MVP.m31; _svfCamView.m32 = MVP.m32; _svfCamView.m33 = MVP.m33;
    }

}

public class CameraViews : Dictionary<Camera, CameraView>
{
    public CameraView Find(Camera cam, TargetDevices tgt, bool flipHand)
    {
        CameraView view = null;
        if (false == this.ContainsKey(cam))
        {
            view = new CameraView(this.Count(), cam, tgt, flipHand);
            Add(cam, view);
        }
        view = base[cam];
        return view;
    }

    public void FlushAll()
    {
        foreach (var kvp in this)
        {
            kvp.Key.RemoveAllCommandBuffers();
        }
        Clear();
    }
}
