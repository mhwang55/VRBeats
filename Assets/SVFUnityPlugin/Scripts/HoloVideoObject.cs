//#define ENABLE_NATIVE_LOGGING
//#define USE_ASYNC // Uncomment for using OpenAsync/CloseAsync in UWP
using System;
using System.Collections;
using System.IO;
#if (NET_4_6 || USE_ASYNC)
using System.Threading;
using System.Threading.Tasks;
#endif
using UnityEngine;
using UnityEngine.Rendering;

[RequireComponent(typeof(BoxCollider), typeof(MeshRenderer), typeof(MeshFilter))]
public class HoloVideoObject : MonoBehaviour, IDisposable
{
    // settings available in Unity editor
    public TargetDevices targetDevice = 0;
    [Tooltip("Use relative path from streaming assets or full path")]
    public string Url;
    public bool ShouldAutoPlay;
    [SerializeField] private float _audioVolume = 1.0f;
    public float AudioVolume
    {
        get
        {
            return _audioVolume;
        }
        set
        {
            _audioVolume = value;
            if (pluginInterop != null)
            {
                pluginInterop.SetAudioVolume(value);
            }
        }
    }

    public Vector3 audioSourceOffset;
    public bool flipHandedness = false;
    [SerializeField] public float _clockScale = 1.0f;
    public float ClockScale
    {
        get
        {
            if (pluginInterop != null)
            {
                return pluginInterop.GetClockScale();
            }
            return 0.0f;
        }
        set
        {
            _clockScale = value;
            if (pluginInterop != null)
            {
                pluginInterop.SetClockScale(value);
            }
        }
    }

    public uint textureLoadTimeout = 50;

    public enum RenderingMode { SVFDraw, UnityDraw }

    public RenderingMode renderingMode = RenderingMode.SVFDraw;
    public bool useGPU = true;
    public bool computeNormals = true;

    public SVFOpenInfo Settings = new SVFOpenInfo()
    {
        AudioDisabled = false,
        AutoLooping = true,
        RenderViaClock = true,
        OutputNormals = true,
        PerformCacheCleanup = false,
        StartDownloadOnOpen = false,
        UseFrameCache = false,
        PlaybackRate = 1.0f,
        UserCacheLocation = null,
        AudioDeviceId = null,
        UseHWDecode = true,
        UseHWTexture = true,
        lockHWTextures = false,
        forceSoftwareClock = false,
        RenderLastFramesTransparent = true,
        HRTFCutoffDistance = float.MaxValue,
        HRTFGainDistance = 1.0f,
        HRTFMinGain = -10.0f,
        HRTFMaxGain = 12.0f
    };

    [Tooltip("We usually read max number of vertices from SVF file, as part of SVFFileInfo."
     + " However, if we fail to load it from file, we default to this value")]
    public uint DefaultMaxVertexCount = 15000;
    public uint DefaultMaxIndexCount = 45000; // see comment above

    private Bounds localSpaceBounds = new Bounds();
    public Bounds BoundingBox
    {
        get { return HVCollider.bounds; }
    }
    public Bounds maximalWCSBounds = new Bounds();

    // Bounding box mesh (with no material) required so that this will survive culling correctly
    private MeshFilter meshFilter;
    private MeshRenderer meshRenderer;
    private BoxCollider HVCollider;
    private Material material;

    private AudioListener listener = null;

    protected SVFUnityPluginInterop pluginInterop = null;
    protected bool isInitialized = false;
    protected SVFOpenInfo openInfo;
    public SVFFileInfo fileInfo = new SVFFileInfo();
    [SerializeField] private UnityConfig unityConfig;
    protected SVFFrameInfo lastFrameInfo = new SVFFrameInfo();

    protected CameraViews cameraViews = new CameraViews();

    public delegate void OnOpenEvent(HoloVideoObject sender, string url);
    public delegate void OnFrameInfoEvent(HoloVideoObject sender, SVFFrameInfo frameInfo);
    public delegate void OnRenderEvent(HoloVideoObject sender);
    public delegate void OnEndOfStream(HoloVideoObject sender);
    public delegate void OnFatalErrorEvent(HoloVideoObject sender);

    public OnOpenEvent OnOpenNotify = null;
    public OnFrameInfoEvent OnUpdateFrameInfoNotify = null;
    public OnRenderEvent OnRenderCallback = null;
    public OnFatalErrorEvent OnFatalError = null;
    public OnEndOfStream OnEndOfStreamNotify = null; // derived class should register for this event

    private Coroutine UnityBufferCoroutine;
#if NET_4_6
    private static Thread mainThread = Thread.CurrentThread;
#endif

    private Logger logger = new Logger(Debug.unityLogger.logHandler);
    private const string TAG = "HoloVideoObject";
    private bool wasPlaying = false;
    private bool ShouldPauseAfterPlay = false;
    [Tooltip("Usually okay to leave at 1, but try 2 if your first frame's drawing a blank")]
    public uint PauseFrameID = 1;

#if UNITY_ANDROID && !UNITY_EDITOR
    private static AndroidJavaObject javaPlugin;
    private static readonly object javaPluginLock = new object();
#endif

    void Awake()
    {
        HVCollider = GetComponent<BoxCollider>();
        meshFilter = GetComponent<MeshFilter>();
        meshRenderer = GetComponent<MeshRenderer>();
        material = meshRenderer.material;

#if UNITY_ANDROID && !UNITY_EDITOR
        lock (javaPluginLock)
        {
            if (javaPlugin == null)
            {
                var unityActivityClass = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
                AndroidJavaObject unityActivity = unityActivityClass.GetStatic<AndroidJavaObject>("currentActivity");
                javaPlugin = new AndroidJavaObject("com.SVFUnityPlugin.JavaPlugin", new object[] { unityActivity });
            }
        }
#endif
    }

    void Start()
    {
        if (Application.platform == RuntimePlatform.IPhonePlayer || Application.platform == RuntimePlatform.Android ||
            Application.platform == RuntimePlatform.OSXPlayer || Application.platform == RuntimePlatform.OSXEditor)
        {
            renderingMode = RenderingMode.UnityDraw;
            logger.LogWarning(TAG, string.Format("'{0}' plugin requires UnityDraw render mode, forcing it on.", Application.platform));
        }
        if (ShouldAutoPlay)
        {
            Open(Url);
            Play();
        }
    }

    private IEnumerator FillUnityBuffers()
    {
        while (true)
        {
            yield return new WaitForEndOfFrame();
            if (isInitialized)
            {
                pluginInterop.IssueUnityRenderModePluginEvent();
            }
        }
    }

    void OnRenderObject()
    {
        if (isInitialized)
        {
            // Camera update necessary here in case object is out of view.
            if (renderingMode == RenderingMode.SVFDraw)
            {
                UpdateCamera();
            }
            HandleOnRender();
        }
    }

    public void OnEnable()
    {
        if (isInitialized)
        {
            pluginInterop.CleanupCommandBuffers();
        }
        OnUpdateFrameInfoNotify += PauseOnFrame;
    }

    public void OnDisable()
    {
        OnUpdateFrameInfoNotify -= PauseOnFrame;
    }

    public void UpdateCamera()
    {
        var cam = Camera.current;
        if (cam == null)
        {
            return;
        }
        CameraView camView = cameraViews.Find(cam, targetDevice, flipHandedness);
        if (null != camView)
        {
            camView.Update(cam, transform.localToWorldMatrix);

            if (null != pluginInterop)
            {
                SVFCameraView svfCamView = cameraViews[cam].InteropCamView;
                pluginInterop.SetCameraView(ref svfCamView);
            }
        }
    }

    // Whenever any camera will render us, add a command buffer to do the work on it
    public void OnWillRenderObject()
    {
        if (renderingMode != RenderingMode.SVFDraw)
        {
            return;
        }

        if (isInitialized)
        {
            var cam = Camera.current;
            if (cam == null)
            {
                return;
            }

            CameraView camView = cameraViews.Find(cam, targetDevice, flipHandedness);
            if (null != camView && null != pluginInterop)
            {
                pluginInterop.SetCommandBufferOnCamera(cam, camView.cameraId);
            }

            // This is the right time to give SVF the camera info for this frame.
            UpdateCamera();
        }
    }

    // 3D Audio Support
    private AudioListener Listener()
    {
        if (listener == null)
        {
            AudioListener[] listeners = FindObjectsOfType<AudioListener>();
            foreach (var li in listeners)
            {
                if (li.isActiveAndEnabled)
                {
                    listener = li;
                    break;
                }
            }
        }
        return listener;
    }

    protected void Update3DAudio()
    {
        if (pluginInterop != null && pluginInterop.IsPlaying())
        {
            var li = Listener();
            if (li != null)
            {
                Vector3 pos = li.transform.InverseTransformPoint(transform.position + audioSourceOffset);
                pluginInterop.SetHCapObjectAudio3DPosition(pos.x, pos.y, pos.z);
            }
        }
    }

    public void Update()
    {
        if (isInitialized)
        {
            Update3DAudio();
            if (pluginInterop.GetHCapObjectFrameInfo(ref lastFrameInfo))
            {
                SVFFrameInfo adjustedFrameInfo = lastFrameInfo;
                if (renderingMode == RenderingMode.SVFDraw)
                {
                    var cam = Camera.current;
                    if (cam == null)
                    {
                        return;
                    }
                    CameraView camView = cameraViews.Find(cam, targetDevice, flipHandedness);
                    if ((null != camView) && camView.isFlipHand)
                    {
                        adjustedFrameInfo.maxX = -lastFrameInfo.minX;
                        adjustedFrameInfo.minX = -lastFrameInfo.maxX;
                    }
                }
                if ((adjustedFrameInfo.maxX - adjustedFrameInfo.minX) > 0.0f ||
                    (adjustedFrameInfo.maxY - adjustedFrameInfo.minY) > 0.0f ||
                    (adjustedFrameInfo.maxZ - adjustedFrameInfo.minZ) > 0.0f)
                {
                    Vector3 min = new Vector3((float) adjustedFrameInfo.minX, (float) adjustedFrameInfo.minY, (float) adjustedFrameInfo.minZ);
                    Vector3 max = new Vector3((float) adjustedFrameInfo.maxX, (float) adjustedFrameInfo.maxY, (float) adjustedFrameInfo.maxZ);
                    localSpaceBounds.SetMinMax(min, max);

                    // File info did not contain bounds, then use the first frame's bounding box
                    if ((fileInfo.maxX - fileInfo.minX) <= 0.0f &&
                        (fileInfo.maxY - fileInfo.minY) <= 0.0f &&
                        (fileInfo.maxZ - fileInfo.minZ) <= 0.0f &&
                        adjustedFrameInfo.frameId <= 1)
                    {
                        Debug.LogWarning("Capture did not contain bounding box info, using the first frame's bounding box");
                        Vector3 worldMin = transform.localToWorldMatrix * new Vector4(min.x, min.y, min.z, 1.0f);
                        Vector3 worldMax = transform.localToWorldMatrix * new Vector4(max.x, max.y, max.z, 1.0f);
                        maximalWCSBounds.SetMinMax(worldMin, worldMax);
                    }

                    HVCollider.center = localSpaceBounds.center;
                    HVCollider.size = localSpaceBounds.size;
                    meshFilter.mesh.bounds = localSpaceBounds;
                }

                UpdateUnityBuffers();
                HandleOnUpdateFrameInfo(adjustedFrameInfo);
            }
            else
            {
                logger.Log("GetHCapObjectFrameInfo returned false");
            }

            if (lastFrameInfo.isEOS)
            {
                if (null != OnEndOfStreamNotify)
                {
                    OnEndOfStreamNotify(this);
                }
            }
#if ENABLE_NATIVE_LOGGING
            string[] trace = pluginInterop.GetTrace();
            if(null != trace)
            {
                foreach(var line in trace)
                {
                    logger.Log(line);
                }
            }
#endif
        }
    }


    private static uint Roundup(uint x, uint multiple)
    {
        if (multiple == 0)
        {
            return x;
        }

        uint remainder = x % multiple;

        if (remainder == 0)
        {
            return x;
        }

        return x + multiple - remainder;
    }

    protected void UpdateUnityBuffers(bool forceNewMesh = false)
    {
        if (renderingMode != RenderingMode.UnityDraw)
        {
            return;
        }

        bool needsBufferUpdate = false;

        Texture2D tex = (Texture2D) meshRenderer.material.mainTexture;
        if (!tex || tex.width != lastFrameInfo.textureWidth || tex.height != lastFrameInfo.textureHeight)
        {
            if (tex)
            {
                Destroy(tex);
            }

            bool mipmaps = false;

            var format = TextureFormat.BGRA32;
            if (Application.platform == RuntimePlatform.Android)
            {
                format = TextureFormat.RGBA32;
            }

            tex = new Texture2D(lastFrameInfo.textureWidth, lastFrameInfo.textureHeight, format, mipmaps);
            tex.Apply();

            meshRenderer.material.mainTexture = tex;
            meshRenderer.material.SetTexture("_EmissionMap", tex);
            //For other shaders, attach texture as map here

            needsBufferUpdate = true;
        }

        Mesh mesh = meshFilter.mesh;
        if (forceNewMesh || mesh.vertexCount < lastFrameInfo.vertexCount ||
            mesh.GetIndexCount(0) < lastFrameInfo.indexCount)
        {
            //Debug.Log("New vertex/index buffers for " + name + " " + lastFrameInfo.vertexCount + " " + lastFrameInfo.indexCount);

            if (pluginInterop != null)
            {
                pluginInterop.ReleaseUnityBuffers();
            }

            // Round up to some reasonable buffer size so that we're not regenerating this for every frame
            uint newVertexCount = Roundup(lastFrameInfo.vertexCount, 5000);

            mesh.Clear();
            mesh.vertices = new Vector3[newVertexCount];
            mesh.normals = new Vector3[newVertexCount];
            mesh.uv = new Vector2[newVertexCount];

            // ...and assure that indices are a multiple of 3
            uint newIndexCount = Roundup(Roundup(lastFrameInfo.indexCount, 5000), 6);

            mesh.triangles = new int[newIndexCount];
            mesh.MarkDynamic();

            needsBufferUpdate = true;
        }

        if (pluginInterop != null)
        {
            if (!pluginInterop.TestUnityBuffersValid())
            {
                Debug.LogWarning("TestUnityBuffersValid -> false");
                needsBufferUpdate = true;
            }

            if (mesh.vertexBufferCount > 0 && needsBufferUpdate)
            {
                //Debug.Log("UUB-set " + mesh.vertexCount + " " + mesh.GetIndexCount(0));
                pluginInterop.SetUnityBuffers(tex.GetNativeTexturePtr(), tex.width, tex.height,
                   mesh.GetNativeVertexBufferPtr(0), mesh.vertexCount,
                   mesh.GetNativeIndexBufferPtr(), (int) mesh.GetIndexCount(0));
            }
        }
    }

    virtual protected void HandleOnUpdateFrameInfo(SVFFrameInfo frameInfo)
    {
        if (null != OnUpdateFrameInfoNotify)
        {
            OnUpdateFrameInfoNotify(this, frameInfo);
        }
    }

    virtual protected void HandleOnOpen(string url)
    {
        if (null != OnOpenNotify)
        {
            OnOpenNotify(this, url);
        }
    }

    virtual protected void HandleOnRender()
    {
        if (null != OnRenderCallback)
        {
            OnRenderCallback(this);
        }
    }

    public bool Open(string urlPath)
    {
        if (Open(urlPath, transform.localScale))
        {
            PostOpenMainThreadSetup();
            return true;
        }
        return false;
    }

    // Common entry point for both OpenAsync and Open to have transform data only accessible on main thread
    public bool Open(string urlPath, Vector3 defaultBoundsScale)
    {
        if (!isInitialized)
        {
            if (!Initialize())
            {
                return false;
            }
        }

        // On Android a bare filename means a file in StreamingAssets which can be loaded via Android asset manager API, 
        // and an absolute path will be opened directly, so just pass unmodified 'urlPath' directly through to plugin.
        if (Application.platform == RuntimePlatform.Android)
        {
        }
        else if (!Path.IsPathRooted(urlPath))
        {
            urlPath = Application.streamingAssetsPath + "/" + urlPath;
        }

        bool res = pluginInterop.OpenHCapObject(urlPath, ref Settings);
        if (res)
        {
            Url = urlPath;

            if (pluginInterop.GetHCapObjectFileInfo(ref fileInfo))
            {
                Vector3 min = Vector3.zero;
                Vector3 max = Vector3.zero;
                if ((fileInfo.maxX - fileInfo.minX) > 0.0f ||
                    (fileInfo.maxY - fileInfo.minY) > 0.0f ||
                    (fileInfo.maxZ - fileInfo.minZ) > 0.0f)
                {
                    min = new Vector3((float) fileInfo.minX, (float) fileInfo.minY, (float) fileInfo.minZ);
                    max = new Vector3((float) fileInfo.maxX, (float) fileInfo.maxY, (float) fileInfo.maxZ);
                }
                else // We need some sane bounds even if the hcap file doesn't tell us.
                {
                    min = new Vector3(0.5f / defaultBoundsScale.x, 0.5f / defaultBoundsScale.y, 0.5f / defaultBoundsScale.z);
                    max = new Vector3(1.1f / defaultBoundsScale.x, 1.1f / defaultBoundsScale.y, 1.1f / defaultBoundsScale.z);
                }
                localSpaceBounds.SetMinMax(min, max);
            }
            // Initialize with reasonable numbers if available.  Important for index and vertex buffers to avoid reallocation in the middle of playback
            lastFrameInfo.indexCount = fileInfo.maxIndexCount > DefaultMaxIndexCount ? fileInfo.maxIndexCount : DefaultMaxIndexCount;
            lastFrameInfo.vertexCount = fileInfo.maxVertexCount > DefaultMaxVertexCount ? fileInfo.maxVertexCount : DefaultMaxVertexCount;
            lastFrameInfo.textureWidth = (int) fileInfo.fileWidth;
            lastFrameInfo.textureHeight = (int) fileInfo.fileHeight;

            HandleOnOpen(urlPath);
        }
        else
        {
            logger.LogError(TAG, "HoloVideoObject::Open Error: unable to OpenHCapObject()");
        }
        return res;
    }

#if (NET_4_6 || USE_ASYNC)
    public async void OpenAsync()
    {
        Initialize();
        if (!Path.IsPathRooted(Url))
        {
            Url = Application.streamingAssetsPath + "/" + Url;
        }
        Vector3 defaultBoundsScale = transform.localScale;
        bool wasOpened = await Task.Factory.StartNew(() =>
        {
            return Open(Url, defaultBoundsScale);
        });
        if (wasOpened)
        {
            PostOpenMainThreadSetup();
        }
    }
#endif

    // After opening, set up transform, coroutine, etc. that can only run on main thread
    // Should be called by both OpenAsync and Open
    void PostOpenMainThreadSetup()
    {
#if NET_4_6
        Debug.Assert(Thread.CurrentThread == mainThread);
#endif
        HVCollider.center = localSpaceBounds.center;
        HVCollider.size = localSpaceBounds.size;
        meshFilter.mesh.bounds = localSpaceBounds;

        Vector3 min = localSpaceBounds.min;
        Vector3 max = localSpaceBounds.max;
        Vector3 worldMin = transform.localToWorldMatrix * new Vector4(min.x, min.y, min.z, 1.0f);
        Vector3 worldMax = transform.localToWorldMatrix * new Vector4(max.x, max.y, max.z, 1.0f);
        maximalWCSBounds.SetMinMax(worldMin, worldMax);

        Update3DAudio();
        UpdateUnityBuffers(true);

        if (renderingMode == RenderingMode.UnityDraw)
        {
            if (UnityBufferCoroutine != null)
            {
                StopCoroutine(UnityBufferCoroutine);
            }
            UnityBufferCoroutine = StartCoroutine(FillUnityBuffers());
        }
    }

    public bool Play()
    {
        if (pluginInterop == null)
        {
            return false;
        }
        meshRenderer.enabled = true;
        return pluginInterop.PlayHCapObject();
    }

    public bool Pause()
    {
        if (pluginInterop == null)
        {
            return false;
        }
        return pluginInterop.PauseHCapObject();
    }

    public void DisplayFrame(uint frameToStopOn = 1)
    {
        ShouldPauseAfterPlay = true;
        PauseFrameID = frameToStopOn;
        meshRenderer.enabled = true;
        Rewind();
        Play();
    }

    private void PauseOnFrame(HoloVideoObject sender, SVFFrameInfo frameInfo)
    {
        if (ShouldPauseAfterPlay && frameInfo.frameId >= PauseFrameID)
        {
            Pause();
            ShouldPauseAfterPlay = false;
        }
    }

    public bool Rewind()
    {
        if (pluginInterop == null)
        {
            return false;
        }
        return pluginInterop.RewindHCapObject();
    }

    /// <summary>
    /// Pauses and rewinds the HVO and disables the mesh renderer to keep it hidden.
    /// Keeps the HVO loaded in memory.
    /// </summary>
    /// <returns> Whether both Pause and Rewind was successful </returns>
    public bool Stop()
    {
        if (pluginInterop == null)
        {
            return false;
        }
        meshRenderer.enabled = false;
        return Pause() && Rewind();
    }

    public bool Close()
    {
        isInitialized = false;

        if (meshRenderer)
        {
            meshRenderer.enabled = false;

            Texture2D tex = (Texture2D)meshRenderer.material.mainTexture;
            if (tex != null)
            {
#if UNITY_EDITOR // The Preview needs to hang on to the texture even after Close()
                if (GetComponent<HoloVideoPreview>() == null)
#endif
                    Destroy(tex);
            }
        }

        if (pluginInterop == null)
        {
            return false;
        }
        pluginInterop.CleanupCommandBuffers();
        return pluginInterop.CloseHCapObject();
    }

#if (NET_4_6 || USE_ASYNC)
    public async void CloseAsync()
    {
        isInitialized = false;
        meshRenderer.enabled = false;
        pluginInterop.CleanupCommandBuffers();
        await Task.Factory.StartNew(() =>
        {
            pluginInterop.CloseHCapObject();
        });
    }
#endif

    public void Cleanup()
    {
        isInitialized = false;

        if (pluginInterop != null)
        {
            pluginInterop.Dispose();
            pluginInterop = null;

            Url = "";
            fileInfo = new SVFFileInfo();
            openInfo = new SVFOpenInfo();
            lastFrameInfo = new SVFFrameInfo();
        }
    }

    public SVFPlaybackState GetCurrentState()
    {
        if (pluginInterop == null)
        {
            return SVFPlaybackState.Empty;
        }
        return pluginInterop.GetHCapState();
    }

    public void ApplyCoreSettings()
    {
        if (pluginInterop != null)
        {
#if UNITY_5_5_OR_NEWER
            pluginInterop.SetTextureLoadTimeout(textureLoadTimeout);
            AudioVolume = _audioVolume;
#endif
            ClockScale = _clockScale;
        }
    }

    private bool Initialize()
    {
        try
        {
            if (null != pluginInterop)
            {
                pluginInterop.Dispose();
            }
            UnityConfig.ConvertToRelativePath(unityConfig.VertexShaderPath);
            UnityConfig.ConvertToRelativePath(unityConfig.PixelShaderPath);
            UnityConfig.ConvertToRelativePath(unityConfig.AtlasVertexShaderPath);
            UnityConfig.ConvertToRelativePath(unityConfig.BBoxFrameTexturePath);
            UnityConfig.ConvertToRelativePath(unityConfig.BBoxClipTexturePath);

            pluginInterop = new SVFUnityPluginInterop(unityConfig);
            HCapSettingsInterop hcapSettings = new HCapSettingsInterop()
            {
                defaultMaxVertexCount = DefaultMaxVertexCount,
                defaultMaxIndexCount = DefaultMaxIndexCount,
                allocMemoryMinGb = MemorySettings.MinGbLimitHW,
                allocMemoryMaxGb = MemorySettings.MaxGbLimitHW
            };

#if ENABLE_NATIVE_LOGGING
            pluginInterop.EnableTracing(true, LogLevel.Message);
#endif

            pluginInterop.CreateHCapObject(hcapSettings);

            pluginInterop.CleanupCommandBuffers();
            //foreach (var cam in Camera.allCameras)
            //{
            //    CameraView camView = cameraViews.Find(cam, targetDevice, flipHandedness);
            //    pluginInterop.SetCommandBufferOnCamera(cam, camView.cameraId);
            //}

#if UNITY_5_5_OR_NEWER
            pluginInterop.SetZBufferInverted(true);
            pluginInterop.SetTextureLoadTimeout(textureLoadTimeout);
            AudioVolume = _audioVolume;
#endif
            if (meshRenderer == null)
            {
                meshRenderer = GetComponent<MeshRenderer>();
            }
            if (meshFilter == null)
            {
                meshFilter = GetComponent<MeshFilter>();
            }
            if (HVCollider == null)
            {
                HVCollider = GetComponent<BoxCollider>();
            }

            if (renderingMode == RenderingMode.SVFDraw)
            {
                meshRenderer.receiveShadows = false;
                meshRenderer.shadowCastingMode = ShadowCastingMode.Off;
                meshRenderer.lightProbeUsage = LightProbeUsage.Off;
                meshRenderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
                meshRenderer.materials = new Material[0];

                // Mesh verts don't have to make sense for rendering in this mode, just for culling
                meshFilter.mesh.vertices = new Vector3[8];
                meshFilter.mesh.RecalculateBounds();
            }
            else if (renderingMode == RenderingMode.UnityDraw)
            {
                meshRenderer.material = material;
                meshFilter.mesh.bounds = localSpaceBounds;
                pluginInterop.SetUseGPU(useGPU);
                pluginInterop.SetComputeNormals(computeNormals);
            }

            isInitialized = true;
        }
        catch (Exception ex)
        {
            logger.LogException(ex);
            isInitialized = false;
            return false;
        }
        return true;
    }

    private void OnApplicationPause(bool pause)
    {
        if (pause)
        {
            wasPlaying = GetCurrentState() == SVFPlaybackState.Playing;
            Pause();
        }
        else
        {
            if (wasPlaying)
            {
                Play();
            }
        }
    }

    private void OnApplicationQuit()
    {
        OnDestroy();
    }

    private void OnDestroy()
    {
        Close();
        Cleanup();
    }

    private bool isInstanceDisposed = false;
    protected virtual void Dispose(bool disposing)
    {
        if (!isInstanceDisposed)
        {
            isInstanceDisposed = true;
        }
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    ~HoloVideoObject()
    {
        // Do not change this code. Put cleanup code in Dispose(bool disposing) above.
        Dispose(false);
    }

    public AudioDeviceInfo[] GetAudioDevices()
    {
        return SVFUnityPluginInterop.EnumerateAudioDevices();
    }
}
