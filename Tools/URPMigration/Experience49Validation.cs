using System;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.XR.Management;
using UnityEditor.XR.Management;
using UnityEngine.XR.OpenXR;

public static class Experience49Validation
{
    public static string Audit()
    {
        var materials = Resources.FindObjectsOfTypeAll<Material>()
            .Where(m => !string.IsNullOrEmpty(AssetDatabase.GetAssetPath(m)) && !m.hideFlags.HasFlag(HideFlags.HideAndDontSave))
            .Select(m => new JObject { ["name"] = m.name, ["path"] = AssetDatabase.GetAssetPath(m),
                ["shader"] = m.shader == null ? null : m.shader.name, ["supported"] = m.shader != null && m.shader.isSupported,
                ["errors"] = m.shader == null ? new JArray() : new JArray(ShaderUtil.GetShaderMessages(m.shader)
                    .Where(e => e.severity.ToString() == "Error").Select(e => e.message)) }).ToArray();
        var renderers = UnityEngine.Object.FindObjectsByType<Renderer>(FindObjectsInactive.Include);
        var used = new JArray(renderers.Where(r => r.enabled && r.gameObject.activeInHierarchy).SelectMany(r => r.sharedMaterials).Where(m => m != null).Distinct()
            .Select(m => new JObject { ["name"] = m.name, ["path"] = AssetDatabase.GetAssetPath(m), ["shader"] = m.shader == null ? null : m.shader.name,
                ["supported"] = m.shader != null && m.shader.isSupported }));
        var xr = XRGeneralSettingsPerBuildTarget.XRGeneralSettingsForBuildTarget(BuildTargetGroup.Android);
        var openxr = OpenXRSettings.GetSettingsForBuildTargetGroup(BuildTargetGroup.Android);
        var report = new JObject {
            ["unity"] = Application.unityVersion, ["activeBuildTarget"] = EditorUserBuildSettings.activeBuildTarget.ToString(),
            ["pipeline"] = GraphicsSettings.currentRenderPipeline?.name,
            ["androidGraphics"] = new JArray(PlayerSettings.GetGraphicsAPIs(BuildTarget.Android).Select(a => a.ToString())),
            ["androidBackend"] = PlayerSettings.GetScriptingBackend(NamedBuildTarget.Android).ToString(),
            ["androidArchitectures"] = PlayerSettings.Android.targetArchitectures.ToString(),
            ["androidLoaders"] = new JArray(xr.Manager.activeLoaders.Select(l => l == null ? "MISSING" : l.GetType().Name)),
            ["androidOpenXRFeatures"] = new JArray(openxr.GetFeatures().Where(f => f.enabled).Select(f => f.GetType().Name)),
            ["sceneMaterials"] = used, ["loadedAssetMaterials"] = new JArray(materials),
            ["missingScripts"] = new JArray(UnityEngine.Object.FindObjectsByType<Transform>(FindObjectsInactive.Include)
                .Where(t => GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject) > 0).Select(t => t.name))
        };
        File.WriteAllText("Builds/URPMigration/URP/project-validation.json", report.ToString());
        return "Saved project/XR validation report.";
    }
    public static string ScheduleAndroidBuild()
    {
        if (EditorApplication.isPlaying) throw new Exception("Stop Play mode before building.");
        // Pipeline 0.7's server dispatcher has a 30s limit independent of CLI --timeout.
        // Return the request before starting synchronous BuildPlayer work.
        var startAfter = EditorApplication.timeSinceStartup + 2;
        EditorApplication.CallbackFunction build = null;
        build = () => {
            if (EditorApplication.timeSinceStartup < startAfter) return;
            EditorApplication.update -= build;
            BuildAndroid();
        };
        EditorApplication.update += build;
        return "Android build scheduled. Monitor URP/android-build.json on disk; do not send Pipeline requests while building.";
    }
    public static string BuildAndroid()
    {
        if (EditorApplication.isPlaying) throw new Exception("Stop Play mode before building.");
        var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions {
            scenes = new[] { "Assets/Scenes/The_Game.unity" },
            locationPathName = "Builds/URPMigration/Experience49-URP.apk",
            target = BuildTarget.Android, options = BuildOptions.Development
        });
        var output = new JObject { ["result"] = report.summary.result.ToString(), ["errors"] = report.summary.totalErrors,
            ["warnings"] = report.summary.totalWarnings, ["bytes"] = report.summary.totalSize,
            ["duration"] = report.summary.totalTime.ToString(),
            ["messages"] = new JArray(report.steps.SelectMany(s => s.messages).Where(m => m.type == LogType.Error || m.type == LogType.Exception)
                .Select(m => m.content)) };
        File.WriteAllText("Builds/URPMigration/URP/android-build.json", output.ToString());
        return output.ToString();
    }
}
