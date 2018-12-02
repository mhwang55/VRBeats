Introduction 
============

Unity plugin for Video Hologram (HCap) playback.  This contains built binaries of the native plugin libraries
for playing back the HCap file format, called SVF, as well as the C# scripts that directly interface to the native plugin.

This is tested with Unity versions 2017.4 and 2018.1. We expect it to be compatible with earlier versions of Unity 2017 as well as with Unity 5.5 and 5.6, though we don't recommend starting with those versions for new projects.

This plugin supports 32-bit and 64-bit Windows and UWP, as well as Android, macOS and iOS.

##### Android
API version 21 or higher is required, as is support for OpenGL ES 3.0 or higher and the GL\_OES\_EGL\_image\_external extension.

##### iOS
Supports iOS 9.0 and higher and requires support for Metal graphics API.

##### macOS
Supports macOS 10.13 and higher and requires support for Metal graphics API. 

Note that the "Metal Editor Support" option must be enabled in for the plugin to function properly in the Unity Editor on macOS. 


The native plugin is built with Visual Studio 2017 and requires the Microsoft Visual C++ Redistributable for Visual Studio 2017,
which can be obtained from <https://support.microsoft.com/en-us/help/2977003/the-latest-supported-visual-c-downloads>


Unity Plugin Organization
==========================

#### Assets/SVFUnityPlugin

All of this folder's content is what goes into SVFUnityPlugin Unity package. Contains the native Unity plugin
libraries that support playback of Video Hologram content as well as any relevant C# scripts.

- Platforms

  Native libraries for iOS, Android, macOS and [64/32]-bit UWP/Windows apps

- Prefabs/HoloVideoExamplePrefab

  Prefab object created with Scripts/HoloVideoObject.cs and Scripts/SVFUnityPluginInterop.cs.
  Use for a quick start project. Drag and drop in to scene and specify .mp4 path here

- Prefabs/HoloVideoPreviewPrefab

  A version of HoloVideoExamplePrefab that generates a preview in the editor window
  (Requires playing the project once)

- Scripts/HVConductor

   Used for async opening/closing in a linear sequence of videos.

# Using HoloVideo prefab
Drag and drop SVFUnityPlugin/Prefabs/HoloVideoExamplePrefab into the scene and specify the video path under 'Url' field. This may be relative path from *StreamingAssets* directory or absolute path.

Only sequential playback is supported.  

Playback speed may be adjusted by calling SetClockScale function.  Note that changing the clock scale once playback has started does not 
have the same effect as a typical fast-forward or slow-motion control.  The next frame of content to be played is determined by the elapsed 
time since the beginning of playback multiplied by the clock scale.  For example, after one second of playback of 30fps material, setting
the clock scale to 2.0 does not mean that playback will continue at frame 31, but rather frame 60 will be played next.

It is possible to override the shaders used to render the HoloVideo in SVFDraw mode. Point to your .cso shader under HoloVideoObject's Unity Config -> Vertex/Pixel Shader Path. Path should be relative from *StreamingAssets*.

# Asynchronous loading of videos on background thread
Opening a new HoloVideo on the main thread can briefly cause frame drops while it's being initialized.
To mitigate this, we can use HVConductor. This feature will require using .NET 4.6 runtime which is only 
available in Unity 2017 and newer, and is considered experimental. To use it, add the HoloVideoObjects to
a sequence in HVConductor's Sequence List. A sequence will be loaded/unloaded and played together. 
GoToNextSequence() plays next set of videos and starts asynchronously loading the next sequence.

**FOR UWP:** You will need to uncomment *\#define USE_ASYNC* directive at the top of HVConductor.cs and HoloVideoObject.cs,
even if you changed the script runtime to 4.6.

# HoloVideo Object Settings

### General Settings

The following settings are useful:

- **Audio Disabled** - disable playback of any audio tracks embedded in the HoloVideo material.
- **Auto Looping** - restart playback at the beginning of the material after reaching the end.
- **Should Auto Play** - Enable to start the video playback on Start

### Rendering Mode

The plugin supports two modes of rendering: **SVF Draw** and **Unity Draw**.  

- **SVF Draw** - In this mode, the plugin is responsible for rendering the the object by making DirectX11 calls, 
bypassing the normal Unity rendering pipeline.  This is very efficient, especially on devices with limited performance, 
but limits how the object interacts with Unity lighting and rendering.  In particular, normal lighting and shadowing 
won't work, and not all screen space image effects will work correctly.  In this mode, the **Target Device** setting 
must be set correctly: see the *Auto Device Detection* section below.  Some functions are only available in this mode:
these are the **Flip Handedness** setting and the color correction functions on the HoloVideoObject class.

- **Unity Draw** - In this mode, the plugin copies the object data into Unity Mesh and Texture objects, which are then 
rendered by Unity.  This allows the object to work with all Unity rendering effects, including lighting, shadowing and 
all screen space effects.  There is some additional overhead required for the copy.  The **Use GPU** setting determines 
whether the copy of data from SVF to Unity is done by the GPU or the CPU.  Ordinarily using the GPU is much faster, but 
the CPU may be used if the GPU resources are needed for other effects.  The **Compute Normals** setting determines whether 
vertex normals are recomputed while doing this copy; if unchecked, the normals data in the SVF content is passed along unmodified.
In this mode, the color correction functions on the HoloVideoObject don't function; however, there is much more control
over rendering available via the Unity shader that is attached to the object.  In this example, the Unity material is configured
so that the object's texture is used for the emission color of the shader, which provides the same baked-in lighting appearance
as the SVF Draw mode, but any Unity material and/or shader may be used, including custom ones, for different effects.

# Additional Notes

### git LFS
If you're obtaining this directly from the HCap team's git repository, note that our git repo uses the Large File Storage 
(LFS) extension.  Make sure that you have obtained and installed git-lfs before cloning the repo.  
We've had good results with git versions 2.10 and newer, combined with git-lfs versions 1.5.6 or 2.0.1.  If you have an 
older version of git and run into problems during the clone, try updating your git version.

### Building Apps for Windows Mixed Reality and HoloLens
After using Unity to build a Visual Studio solution, you will need to modify the file _Package.appxmanifest_ in that 
solution prior to compiling and deploying on a HoloLens or a Windows Mixed Reality immersive headset. 
Open the file in an XML editor, and add the following to the end of the file, before the last line containing </Package>

```
  <Extensions>
    <Extension Category="windows.activatableClass.inProcessServer">
      <InProcessServer>
        <Path>SVFRT.dll</Path>
        <ActivatableClass ActivatableClassId="SVF.CH264Wrapper" ThreadingModel="both" />
      </InProcessServer>
    </Extension>
  </Extensions>
```

If this step is omitted, the Hologram audio only will play back, nothing visual.

# Current changelog

## 1.2.18 - 2018-08-28

Fixed hang when playing capture with "Auto Looping" on iOS/macOS.

## 1.2.17 - 2018-08-22

Reduced memory footprint on iOS/macOS when playing multiple captures simultaneously.

## 1.2.16 - 2018-08-03

Minor performance improvements

## 1.2.15 - 2018-08-01
IL2CPP scripting backend now works properly in UWP
HoloVideoObject's TextureBuffer is now cleaned up during Close()
HoloVideoObject now calls Close OnDestroy()

## 1.2.14 - 2018-07-23
Removed vertex format version from frame info and added isKeyFrame flag
HoloVideoObject frees up texture memory on Close now

