using System;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
public static class Native49Startup
{
    public static string Inspect()
    {
        const string profilePath="Assets/Liminal/Resources/LimappConfig.asset";
        var asset=AssetDatabase.LoadMainAssetAtPath(profilePath);
        var script=asset==null?null:new SerializedObject(asset).FindProperty("m_Script")?.objectReferenceValue as MonoScript;
        var cls=script==null?null:script.GetClass();
        string guid=null;long localID=0;
        if(script!=null)AssetDatabase.TryGetGUIDAndLocalFileIdentifier(script,out guid,out localID);
        var assemblies=new JArray(AppDomain.CurrentDomain.GetAssemblies().Where(a=>a.GetName().Name=="Liminal.SDK"||a.GetName().Name=="Core"||a.GetName().Name=="Release"||a.GetName().Name=="LiminalSdk"||a.GetName().Name=="Editor"||a.GetName().Name.StartsWith("App000000000049")).Select(a=>new JObject{
            ["name"]=a.GetName().Name,["location"]=a.Location,["mvid"]=a.ManifestModule.ModuleVersionId.ToString(),
            ["sdkTypes"]=new JArray(a.GetTypes().Where(t=>t.FullName=="Liminal.SDK.Core.ExperienceApp"||t.FullName=="ExperienceProfile").Select(t=>t.FullName))}));
        var corePath=AssetDatabase.GUIDToAssetPath("1661fa78af9aece459a3302206a05902");
        var releasePath=AssetDatabase.GUIDToAssetPath("a80148f7c24f27741a9721c40a216ac9");
        var mono=new JArray(new[]{corePath,releasePath}.Where(p=>!string.IsNullOrEmpty(p)).SelectMany(p=>AssetDatabase.LoadAllAssetsAtPath(p).OfType<MonoScript>().Where(s=>s.name=="ExperienceProfile"||s.name=="ExperienceApp"||s.name=="VRAvatarHand").Select(s=>{
            AssetDatabase.TryGetGUIDAndLocalFileIdentifier(s,out string g,out long id);
            return new JObject{["assetPath"]=p,["name"]=s.name,["guid"]=g,["localID"]=id,["class"]=s.GetClass()?.AssemblyQualifiedName};})));
        var scenes=new JArray(Enumerable.Range(0,UnityEngine.SceneManagement.SceneManager.sceneCount).Select(i=>UnityEngine.SceneManagement.SceneManager.GetSceneAt(i)).Select(s=>new JObject{["path"]=s.path,["name"]=s.name,["dirty"]=s.isDirty,["loaded"]=s.isLoaded}));
        var result=new JObject{["utc"]=DateTime.UtcNow.ToString("o"),["pid"]=System.Diagnostics.Process.GetCurrentProcess().Id,["project"]=Directory.GetCurrentDirectory(),
            ["playing"]=EditorApplication.isPlaying,["compiling"]=EditorApplication.isCompiling,["unity"]=Application.unityVersion,["target"]=EditorUserBuildSettings.activeBuildTarget.ToString(),
            ["batch"]=Application.isBatchMode,["graphicsAPI"]=SystemInfo.graphicsDeviceType.ToString(),["pipeline"]=GraphicsSettings.currentRenderPipeline==null?null:GraphicsSettings.currentRenderPipeline.name,
            ["lightsLinear"]=GraphicsSettings.lightsUseLinearIntensity,["lightsTemperature"]=GraphicsSettings.lightsUseColorTemperature,["scenes"]=scenes,["assemblies"]=assemblies,
            ["profile"]=new JObject{["path"]=profilePath,["loaded"]=asset!=null,["assetType"]=asset?.GetType().AssemblyQualifiedName,["scriptGUID"]=guid,["scriptLocalID"]=localID,["class"]=cls?.AssemblyQualifiedName,["json"]=asset==null?null:EditorJsonUtility.ToJson(asset)},["sdkMonoScripts"]=mono,
            ["limits"]="Read-only native startup/binding. No scene open/save, SDK/provider changes, gameplay/render/player acceptance."};
        var stage="Builds/URPMigration/NativeStartup-"+DateTime.UtcNow.ToString("yyyyMMddTHHmmssfffZ");Directory.CreateDirectory(stage);File.WriteAllText(stage+"/report.json",result.ToString());
        return new JObject{["path"]=stage+"/report.json",["state"]=result}.ToString();
    }
}
