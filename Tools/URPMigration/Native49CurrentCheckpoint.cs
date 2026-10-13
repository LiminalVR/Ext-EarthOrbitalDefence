using System;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Object=UnityEngine.Object;
public static class Native49CurrentCheckpoint {
 public static string Inspect(){
  if(EditorApplication.isPlayingOrWillChangePlaymode||EditorApplication.isCompiling||EditorApplication.isUpdating)throw new Exception("Stopped ready required");
  var stage="Builds/URPMigration/NativeCurrentCheckpoint-"+DateTime.UtcNow.ToString("yyyyMMddTHHmmssfffZ");Directory.CreateDirectory(stage);
  var dirty=new JArray(Resources.FindObjectsOfTypeAll<Object>().Where(o=>EditorUtility.IsPersistent(o)&&EditorUtility.IsDirty(o)).Select(o=>new JObject{["name"]=o.name,["type"]=o.GetType().AssemblyQualifiedName,["path"]=AssetDatabase.GetAssetPath(o),["json"]=EditorJsonUtility.ToJson(o)}));
  var settings=new JArray();foreach(var path in new[]{"ProjectSettings/GraphicsSettings.asset","ProjectSettings/QualitySettings.asset"})settings.Add(new JObject{["path"]=path,["native"]=new JArray(AssetDatabase.LoadAllAssetsAtPath(path).Select(o=>new JObject{["type"]=o.GetType().AssemblyQualifiedName,["json"]=EditorJsonUtility.ToJson(o)}))});
  var scene=UnityEngine.SceneManagement.SceneManager.GetActiveScene();
  var report=new JObject{["utc"]=DateTime.UtcNow.ToString("o"),["pid"]=System.Diagnostics.Process.GetCurrentProcess().Id,["playing"]=EditorApplication.isPlaying,["compiling"]=EditorApplication.isCompiling,["pipeline"]=GraphicsSettings.currentRenderPipeline?.name,["scene"]=scene.path,["sceneDirty"]=scene.isDirty,["linear"]=GraphicsSettings.lightsUseLinearIntensity,["temperature"]=GraphicsSettings.lightsUseColorTemperature,["dirty"]=dirty,["settings"]=settings,["saveAPI"]=new JArray(typeof(Unsupported).GetMethods(System.Reflection.BindingFlags.Static|System.Reflection.BindingFlags.Public|System.Reflection.BindingFlags.NonPublic).Where(m=>m.Name.Contains("SaveToSerializedFile")).Select(m=>m.ToString())),["modules"]=new JArray(AppDomain.CurrentDomain.GetAssemblies().Where(a=>new[]{"App000000000049","Liminal.SDK","LiminalSdk","Editor"}.Contains(a.GetName().Name)).Select(a=>new JObject{["name"]=a.GetName().Name,["location"]=a.Location,["mvid"]=a.ManifestModule.ModuleVersionId.ToString()})),["limits"]="Read-only persistent dirty JSON/native settings/module/API checkpoint. No scene/assets/settings save, import, dirty clearing, gameplay or global write."};
  File.WriteAllText(stage+"/report.json",report.ToString());return Path.GetFullPath(stage+"/report.json");
 }
}
