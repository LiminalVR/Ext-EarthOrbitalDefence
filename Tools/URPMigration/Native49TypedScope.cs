using System;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
public static class Native49TypedScope {
 static JObject Identity(Material m){if(m==null)return null;string guid;long id;AssetDatabase.TryGetGUIDAndLocalFileIdentifier(m,out guid,out id);return new JObject{["path"]=AssetDatabase.GetAssetPath(m),["name"]=m.name,["guid"]=guid,["localID"]=id,["shader"]=m.shader.name,["dirty"]=EditorUtility.IsDirty(m),["nativeJSON"]=EditorJsonUtility.ToJson(m)};}
 public static string Inspect(){
  if(EditorApplication.isPlayingOrWillChangePlaymode||EditorApplication.isCompiling||GraphicsSettings.currentRenderPipeline!=null)throw new Exception("Stopped source pipeline required");
  string stage="Builds/URPMigration/NativeTypedScope-"+DateTime.UtcNow.ToString("yyyyMMddTHHmmssfffZ");Directory.CreateDirectory(stage);var rows=new JArray();
  Action<string,Renderer> add=(origin,r)=>{rows.Add(new JObject{["origin"]=origin,["name"]=r.name,["type"]=r.GetType().FullName,["entityID"]=r.GetEntityId().ToString(),["activeHierarchy"]=r.gameObject.activeInHierarchy,["enabled"]=r.enabled,["materials"]=new JArray(r.sharedMaterials.Select(Identity)),["trailMaterial"]=r is ParticleSystemRenderer pr?Identity(pr.trailMaterial):null});};
  foreach(var r in UnityEngine.Object.FindObjectsByType<Renderer>(FindObjectsInactive.Include))if(r.gameObject.scene.path=="Assets/Scenes/The_Game.unity")add("actualScene",r);
  foreach(string path in AssetDatabase.GetDependencies("Assets/Scenes/The_Game.unity",true).Where(p=>p.EndsWith(".prefab"))){var root=AssetDatabase.LoadAssetAtPath<GameObject>(path);if(root!=null)foreach(var r in root.GetComponentsInChildren<Renderer>(true))add(path,r);}
  var sh=RenderSettings.ambientProbe;var coeff=new JArray();for(int c=0;c<3;c++){var v=new JArray();for(int i=0;i<9;i++)v.Add(sh[c,i]);coeff.Add(v);}
  var env=new JObject{["skybox"]=RenderSettings.skybox==null?null:AssetDatabase.GetAssetPath(RenderSettings.skybox),["defaultReflectionMode"]=RenderSettings.defaultReflectionMode.ToString(),["reflectionIntensity"]=RenderSettings.reflectionIntensity,["ambientMode"]=RenderSettings.ambientMode.ToString(),["ambientLight"]=new JArray(RenderSettings.ambientLight.r,RenderSettings.ambientLight.g,RenderSettings.ambientLight.b,RenderSettings.ambientLight.a),["ambientProbe"]=coeff,["lightsUseLinearIntensity"]=GraphicsSettings.lightsUseLinearIntensity,["lightsUseColorTemperature"]=GraphicsSettings.lightsUseColorTemperature};
  File.Copy("Tools/URPMigration/Native49TypedScope.cs",stage+"/Native49TypedScope.cs");File.WriteAllText(stage+"/report.json",new JObject{["pid"]=System.Diagnostics.Process.GetCurrentProcess().Id,["renderers"]=rows,["environment"]=env,["scope"]="Actual scene and serialized dependency prefab renderer identities. Imported model submaterials only included when these actual renderer references resolve them. No assignment/save/mutation."}.ToString());return Path.GetFullPath(stage+"/report.json");
 }
}
