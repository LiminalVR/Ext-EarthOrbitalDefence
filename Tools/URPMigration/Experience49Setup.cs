using System;
using System.Linq;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.XR.Management;
using UnityEditor.XR.Management.Metadata;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.XR.Management;
using UnityEngine.XR.OpenXR;

public static class Experience49Setup
{
    public static string ConfigureQuest()
    {
        if (EditorApplication.isPlaying) throw new Exception("Stop Play mode first.");
        var general = XRGeneralSettingsPerBuildTarget.XRGeneralSettingsForBuildTarget(BuildTargetGroup.Android);
        if (general == null || general.Manager == null) throw new Exception("Android XR settings are absent.");
        var manager = general.Manager;
        var serialized = new SerializedObject(manager);
        serialized.FindProperty("m_Loaders").ClearArray();
        serialized.ApplyModifiedPropertiesWithoutUndo();
        if (!XRPackageMetadataStore.AssignLoader(manager, "UnityEngine.XR.OpenXR.OpenXRLoader", BuildTargetGroup.Android))
            throw new Exception("Could not assign OpenXR loader.");
        var settings = OpenXRSettings.GetSettingsForBuildTargetGroup(BuildTargetGroup.Android);
        foreach (var feature in settings.GetFeatures()) {
            var name = feature.GetType().Name;
            feature.enabled = name == "MetaQuestFeature" || name == "OculusTouchControllerProfile" ||
                name == "MetaQuestTouchPlusControllerProfile" || name == "HandTracking" || name == "HandInteractionProfile";
            EditorUtility.SetDirty(feature);
        }
        PlayerSettings.SetScriptingBackend(NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);
        PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
        PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.Android, false);
        PlayerSettings.SetGraphicsAPIs(BuildTarget.Android, new[] { GraphicsDeviceType.OpenGLES3 });
        EditorUtility.SetDirty(manager); EditorUtility.SetDirty(settings);
        SaveProjectAssets();
        return new JObject { ["loaders"]=new JArray(manager.activeLoaders.Select(l=>l.GetType().Name)),
            ["features"]=new JArray(settings.GetFeatures().Where(f=>f.enabled).Select(f=>f.GetType().Name)) }.ToString();
    }

    static void SaveProjectAssets() {
        // A local SDK is shared by several editors. Never flush package asset changes.
        foreach(var asset in Resources.FindObjectsOfTypeAll<UnityEngine.Object>())
            if(EditorUtility.IsDirty(asset) && AssetDatabase.GetAssetPath(asset).StartsWith("Assets/",System.StringComparison.Ordinal))
                AssetDatabase.SaveAssetIfDirty(asset);
    }
}
