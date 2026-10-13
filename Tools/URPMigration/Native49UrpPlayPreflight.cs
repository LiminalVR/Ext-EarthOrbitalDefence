using System;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using Liminal.SDK.VR;
using Liminal.SDK.V2;
public static class Native49UrpPlayPreflight {
 static string H(Transform t)=>t.parent==null?t.name:H(t.parent)+"/"+t.name;
 public static string Start(){
  if(EditorApplication.isPlayingOrWillChangePlaymode||EditorApplication.isCompiling||GraphicsSettings.currentRenderPipeline==null)throw new Exception("StoppedcompiledURP required");
  if(SceneManager.GetActiveScene().path!="Assets/Scenes/The_Game.unity"||SceneManager.GetActiveScene().isDirty)throw new Exception("CleanoriginalMainscene required");
  if(AssetDatabase.LoadMainAssetAtPath("Assets/Liminal/Resources/LimappConfig.asset")==null)throw new Exception("Authenticprofilebinding required");
  var stage="Builds/URPMigration/FinalUrpPlayStartup-"+DateTime.UtcNow.ToString("yyyyMMddTHHmmssfffZ");Directory.CreateDirectory(stage);
  File.WriteAllText(stage+"/request.json",new JObject{["utc"]=DateTime.UtcNow.ToString("o"),["pid"]=System.Diagnostics.Process.GetCurrentProcess().Id,["normalPlay"]=true,["sourcePipeline"]=false,["noDeviceSubstitution"]=true,["noPrivateGameCallbacks"]=true,["scene"]=SceneManager.GetActiveScene().path}.ToString());
  File.WriteAllText("Tools/URPMigration/FinalUrpPlayStartup-current.json",new JObject{["stage"]=stage}.ToString());
  var game=typeof(Editor).Assembly.GetType("UnityEditor.GameView");if(game!=null){var window=EditorWindow.GetWindow(game);window.Show();window.Focus();}
  EditorApplication.isPlaying=true;return stage;
 }
 public static string Inspect(){
  if(!EditorApplication.isPlaying)throw new Exception("NormalPlay required");
  var manager=UnityEngine.Object.FindObjectsByType<ExperienceAppManager>(FindObjectsInactive.Include).FirstOrDefault();
  var scripts=UnityEngine.Object.FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include).Where(m=>m!=null&&new[]{"AppController","SpawnSystem","Fire","Health","Starter","ExperienceAppManager","DeviceManager","VRAvatar","ScoreMenu"}.Contains(m.GetType().Name));
  var device=VRDevice.Device;var primary=device?.PrimaryInputDevice;var secondary=device?.SecondaryInputDevice;
  var ray=PlayerAim.GetRayInteractor();
  var r=new JObject{["utc"]=DateTime.UtcNow.ToString("o"),["time"]=Time.time,["frame"]=Time.frameCount,["playing"]=EditorApplication.isPlaying,["batch"]=Application.isBatchMode,["pipeline"]=GraphicsSettings.currentRenderPipeline==null?"Built-in":GraphicsSettings.currentRenderPipeline.name,
   ["device"]=device?.GetType().FullName,["primary"]=primary?.GetType().FullName,["secondary"]=secondary?.GetType().FullName,["rayInteractor"]=ray==null?null:H(ray.transform),["manager"]=manager==null?null:H(manager.transform),["spawnedRig"]=manager==null||manager.SpawnedRig==null?null:H(manager.SpawnedRig.transform),
   ["components"]=new JArray(scripts.Select(m=>new JObject{["type"]=m.GetType().FullName,["hierarchy"]=H(m.transform),["active"]=m.isActiveAndEnabled,["json"]=EditorJsonUtility.ToJson(m)})),
   ["cameras"]=new JArray(UnityEngine.Object.FindObjectsByType<Camera>(FindObjectsInactive.Include).Select(c=>new JObject{["name"]=H(c.transform),["enabled"]=c.isActiveAndEnabled,["main"]=c==Camera.main,["position"]=new JArray(c.transform.position.x,c.transform.position.y,c.transform.position.z),["rotation"]=new JArray(c.transform.rotation.x,c.transform.rotation.y,c.transform.rotation.z,c.transform.rotation.w)})),
   ["limits"]="Shortnormalstartup only; no authoredStart or firing/progression proof. No inputdevice/provider fabricated."};
  var stage="Builds/URPMigration/FinalUrpPlayState-"+DateTime.UtcNow.ToString("yyyyMMddTHHmmssfffZ");Directory.CreateDirectory(stage);File.WriteAllText(stage+"/report.json",r.ToString());return new JObject{["path"]=stage+"/report.json",["device"]=r["device"],["primary"]=r["primary"],["ray"]=r["rayInteractor"],["time"]=Time.time}.ToString();
 }
 public static string Stop(){if(EditorApplication.isPlaying)EditorApplication.isPlaying=false;return "NormalPlaystop requested; awaitactualstopped";}
}
