using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Object=UnityEngine.Object;
public static class Native49ReachableScope {
 static string H(Transform t)=>t.parent==null?t.name:H(t.parent)+"/"+t.name;
 static JObject ID(Object o){if(o==null)return null;string g;long id;AssetDatabase.TryGetGUIDAndLocalFileIdentifier(o,out g,out id);return new JObject{["path"]=AssetDatabase.GetAssetPath(o),["guid"]=g,["localID"]=id,["type"]=o.GetType().FullName,["name"]=o.name};}
 static JObject M(Material m){if(m==null)return null;var j=ID(m);j["shader"]=m.shader.name;j["doubleSidedGI"]=m.doubleSidedGI;j["giFlags"]=(int)m.globalIlluminationFlags;j["queue"]=m.renderQueue;j["keywords"]=new JArray(m.shaderKeywords);j["nativeJSON"]=EditorJsonUtility.ToJson(m);j["properties"]=new JArray(Enumerable.Range(0,m.shader.GetPropertyCount()).Select(i=>new JObject{["name"]=m.shader.GetPropertyName(i),["type"]=m.shader.GetPropertyType(i).ToString(),["flags"]=m.shader.GetPropertyFlags(i).ToString()}));return j;}
 static JObject R(Renderer r)=>new JObject{["hierarchy"]=H(r.transform),["type"]=r.GetType().FullName,["activeSelf"]=r.gameObject.activeSelf,["activeHierarchy"]=r.gameObject.activeInHierarchy,["enabled"]=r.enabled,["lightmapIndex"]=r.lightmapIndex,["realtimeLightmapIndex"]=r.realtimeLightmapIndex,["lightProbeUsage"]=r.lightProbeUsage.ToString(),["reflectionProbeUsage"]=r.reflectionProbeUsage.ToString(),["slots"]=new JArray(r.sharedMaterials.Select(M)),["trail"]=r is ParticleSystemRenderer p?M(p.trailMaterial):null};
 public static string Inspect(){
  if(EditorApplication.isPlayingOrWillChangePlaymode||EditorApplication.isCompiling||GraphicsSettings.currentRenderPipeline!=null)throw new Exception("Stopped source scene required");
  var stage="Builds/URPMigration/ReachableScope-"+DateTime.UtcNow.ToString("yyyyMMddTHHmmssfffZ");Directory.CreateDirectory(stage);File.Copy("Tools/URPMigration/Native49ReachableScope.cs",stage+"/Native49ReachableScope.cs");
  var roots=new JArray();var seen=new HashSet<GameObject>();
  Action<string,GameObject,string> add=null;add=(origin,root,reason)=>{
   var row=new JObject{["origin"]=origin,["root"]=ID(root),["reason"]=reason};roots.Add(row);if(root==null)return;
   row["renderers"]=new JArray(root.GetComponentsInChildren<Renderer>(true).Select(R));row["scripts"]=new JArray(root.GetComponentsInChildren<MonoBehaviour>(true).Where(c=>c!=null).Select(c=>new JObject{["hierarchy"]=H(c.transform),["type"]=c.GetType().AssemblyQualifiedName,["enabled"]=c.enabled,["activeHierarchy"]=c.gameObject.activeInHierarchy}));
   row["particleLights"]=new JArray(root.GetComponentsInChildren<ParticleSystem>(true).Select(ps=>{var l=ps.lights;return new JObject{["hierarchy"]=H(ps.transform),["enabled"]=l.enabled,["lightPrefab"]=ID(l.light),["lightJSON"]=l.light==null?null:EditorJsonUtility.ToJson(l.light),["prefabEnabled"]=l.light!=null&&l.light.enabled,["prefabActiveSelf"]=l.light!=null&&l.light.gameObject.activeSelf,["ratio"]=l.ratio,["maxLights"]=l.maxLights,["rangeMultiplier"]=l.rangeMultiplier,["intensityMultiplier"]=l.intensityMultiplier,["useParticleColor"]=l.useParticleColor,["sizeAffectsRange"]=l.sizeAffectsRange,["alphaAffectsIntensity"]=l.alphaAffectsIntensity,["particleJSON"]=EditorJsonUtility.ToJson(ps)};}));
   if(!seen.Add(root))return;
   foreach(var e in root.GetComponentsInChildren<Enemy>(true)){add(H(e.transform)+".collisionExplossion",e.collisionExplossion==null?null:e.collisionExplossion.gameObject,"Normal Enemy.Kill collision/laser path");row["projectile"]=ID(e.Projectile);}
  };
  foreach(var sp in Object.FindObjectsByType<SpawnSystem>(FindObjectsInactive.Include)){foreach(var w in sp.HostileObjects){add(H(sp.transform)+".HostileObjects(weight="+w.Weight+")",w.Prefab==null?null:w.Prefab.gameObject,"Actual serialized weighted choice; spawn through normal StartSpawning/SpawnerCoro");}}
  foreach(var f in Object.FindObjectsByType<Fire>(FindObjectsInactive.Include)){add(H(f.transform)+".ExplosionEffect",f.ExplosionEffect,"50 pooled prototypes; normal LaserRaycast calls PlayerEffect(0)");add(H(f.transform)+".FireEffect",f.FireEffect,"10 pooled prototypes; PlayerEffect(1) has no source callsite");add(H(f.transform)+".LaserPrefab",f.playerLaser?.LaserPrefab==null?null:f.playerLaser.LaserPrefab.gameObject,"Normal Fire.Start then source MainColor charge writes");}
  foreach(var health in Object.FindObjectsByType<Health>(FindObjectsInactive.Include))add(H(health.transform)+".explossionPrefab",health.explossionPrefab,"Health.Start prepares source prototype; Health.Damage depletion path uses it. Survival and loss are distinct paths.");
  var source=Object.FindObjectsByType<Renderer>(FindObjectsInactive.Include).Where(r=>r.gameObject.scene.path=="Assets/Scenes/The_Game.unity").ToArray();
  var report=new JObject{["utc"]=DateTime.UtcNow.ToString("o"),["pid"]=System.Diagnostics.Process.GetCurrentProcess().Id,["playing"]=EditorApplication.isPlaying,["pipeline"]=GraphicsSettings.currentRenderPipeline?.name,["roots"]=roots,["sceneRenderers"]=new JArray(source.Select(R)),["skybox"]=M(RenderSettings.skybox),["fog"]=new JObject{["enabled"]=RenderSettings.fog,["mode"]=RenderSettings.fogMode.ToString(),["density"]=RenderSettings.fogDensity,["start"]=RenderSettings.fogStartDistance,["end"]=RenderSettings.fogEndDistance},["currentSceneDirty"]=UnityEngine.SceneManagement.SceneManager.GetActiveScene().isDirty,["limits"]="Read-only actual native weighted-prefab/effect/ordered-slot/property closure. Inactive dependency prototypes and never-called private Enemy.Fire/PlayerEffect(1) are not natural lifecycle acceptance. Null particle-secondary/trail slots retained. No activation/instantiation/save/property/gameplay changes."};
  File.WriteAllText(stage+"/report.json",report.ToString());return Path.GetFullPath(stage+"/report.json");
 }
}
