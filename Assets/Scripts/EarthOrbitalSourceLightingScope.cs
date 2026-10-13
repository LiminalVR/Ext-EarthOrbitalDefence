using System;using System.Collections.Generic;using UnityEngine;using UnityEngine.Rendering;using UnityEngine.SceneManagement;
public static class EarthOrbitalSourceLightingScope
{
    public const string ExperienceScene = "Assets/Scenes/The_Game.unity";
    struct Frame { public Camera camera; public bool linear, temperature; public Vector4 indirectSpec; }
    static readonly Stack<Frame> Frames = new Stack<Frame>();
    static readonly HashSet<Camera> Diagnostics = new HashSet<Camera>();
    static readonly HashSet<Transform> ExperienceRoots = new HashSet<Transform>();
    public static int RegisteredExperienceCount => ExperienceRoots.Count;
    public static void RegisterExperience(Transform root) { if(root==null)return; ExperienceRoots.Add(root); Enable(); }
    public static void UnregisterExperience(Transform root) { ExperienceRoots.Remove(root); ExperienceRoots.RemoveWhere(x=>x==null); if(ExperienceRoots.Count==0&&Diagnostics.Count==0)Disable(); }
    static readonly int IndirectSpec=Shader.PropertyToID("_EarthOrbitalSourceIndirectSpecColor");
    static bool subscribed;
    public static int PendingScopes => Frames.Count;
    public static long BeginCount { get; private set; }
    public static long EndCount { get; private set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Initialize() { Enable(); }
#if UNITY_EDITOR
    [UnityEditor.InitializeOnLoadMethod]
    static void InitializeEditor()
    {
        Enable();
        UnityEditor.AssemblyReloadEvents.beforeAssemblyReload -= Disable;
        UnityEditor.AssemblyReloadEvents.beforeAssemblyReload += Disable;
        UnityEditor.EditorApplication.playModeStateChanged -= EditorPlayState;
        UnityEditor.EditorApplication.playModeStateChanged += EditorPlayState;
    }
    static void EditorPlayState(UnityEditor.PlayModeStateChange state)
    {
        if (state == UnityEditor.PlayModeStateChange.ExitingPlayMode) RestoreAll();
        if (state == UnityEditor.PlayModeStateChange.EnteredEditMode) Enable();
    }
#endif
    public static void Enable()
    {
        if (subscribed) return;
        subscribed = true;
        RenderPipelineManager.beginCameraRendering += Begin;
        RenderPipelineManager.endCameraRendering += End;
        RenderPipelineManager.endContextRendering += EndContext;
        SceneManager.sceneUnloaded += SceneUnloaded;
        Application.quitting += Disable;
    }
    public static void RegisterDiagnostic(Camera camera) { Diagnostics.Add(camera); Enable(); }
    public static void UnregisterDiagnostic(Camera camera) { Diagnostics.Remove(camera); if(ExperienceRoots.Count==0&&Diagnostics.Count==0)Disable(); }
    public static void RenderOwnedDiagnostic(Camera camera) { if(camera==null)throw new System.ArgumentNullException(nameof(camera)); RegisterDiagnostic(camera); try { camera.Render(); } finally { UnregisterDiagnostic(camera); } }
    static bool Owned(Camera camera)
    {
        if (camera == null) return false;
        if (Diagnostics.Contains(camera)) return true;
        foreach(var root in ExperienceRoots) if(root!=null&&camera.transform.IsChildOf(root))return true;
#if UNITY_EDITOR
        // Stopped scene probes have no activated runtime owner. Runtime ownership
        // is always authenticated by normal experience activation, never a scene name.
        if(!Application.isPlaying&&camera.gameObject.scene.path==ExperienceScene)return true;
#endif
        // Reflection cameras rendered synchronously inside an owned camera inherit
        // its light scope; unrelated top-level scene cameras remain untouched.
        return Frames.Count != 0;
    }
    static void Begin(ScriptableRenderContext context, Camera camera)
    {
        if (!Owned(camera)) return;
        var frame = new Frame { camera = camera, linear = GraphicsSettings.lightsUseLinearIntensity,
            temperature = GraphicsSettings.lightsUseColorTemperature, indirectSpec=Shader.GetGlobalVector(IndirectSpec) };
        Frames.Push(frame);
        try {
            GraphicsSettings.lightsUseLinearIntensity = false;
            GraphicsSettings.lightsUseColorTemperature = false;
            var dc=EarthOrbitalSourceBakedEnvironment.SkyAmbientDC*RenderSettings.reflectionIntensity;Shader.SetGlobalVector(IndirectSpec,new Vector4(dc.x,dc.y,dc.z,0));

            BeginCount++;
        } catch { Restore(Frames.Pop()); throw; }
    }
    static void End(ScriptableRenderContext context, Camera camera)
    {
        if (Frames.Count == 0 || Frames.Peek().camera != camera) return;
        Restore(Frames.Pop()); EndCount++;
    }
    static void Restore(Frame frame)
    {
        GraphicsSettings.lightsUseLinearIntensity = frame.linear;
        GraphicsSettings.lightsUseColorTemperature = frame.temperature;

        Shader.SetGlobalVector(IndirectSpec,frame.indirectSpec);
    }
    static void RestoreAll() { while (Frames.Count != 0) Restore(Frames.Pop()); }
    static void EndContext(ScriptableRenderContext context, List<Camera> cameras) {
        // A nested mirror render context must not unwind its still-rendering parent.
        // On a missed camera End, unwind only through the deepest frame belonging
        // to this context; an outer context can recover orphaned nested frames.
        int index=0,restoreCount=0;
        foreach(var frame in Frames) { if(cameras.Contains(frame.camera))restoreCount=index+1; index++; }
        while(restoreCount-- > 0 && Frames.Count != 0)Restore(Frames.Pop());
    }
    static void SceneUnloaded(Scene scene)
    {
        ExperienceRoots.RemoveWhere(x=>x==null||x.gameObject.scene==scene);
        Diagnostics.RemoveWhere(x=>x==null||x.gameObject.scene==scene);
        if(ExperienceRoots.Count==0&&Diagnostics.Count==0)Disable();
    }
    public static void Disable()
    {
        RestoreAll(); Diagnostics.Clear(); ExperienceRoots.Clear();
        if (!subscribed) return;
        subscribed = false;
        RenderPipelineManager.beginCameraRendering -= Begin;
        RenderPipelineManager.endCameraRendering -= End;
        RenderPipelineManager.endContextRendering -= EndContext;
        SceneManager.sceneUnloaded -= SceneUnloaded;
        Application.quitting -= Disable;
    }
}

