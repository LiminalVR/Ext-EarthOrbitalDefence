using System;using System.IO;using System.Linq;using Newtonsoft.Json.Linq;using UnityEditor;using UnityEngine;using UnityEngine.Rendering;
public static class Native49SafeClose {
 static string Hash(string path)=>BitConverter.ToString(System.Security.Cryptography.SHA256.Create().ComputeHash(File.ReadAllBytes(path))).Replace("-","").ToLowerInvariant();
 public static string Schedule(){
  if(System.Diagnostics.Process.GetCurrentProcess().Id!=145120||EditorApplication.isPlayingOrWillChangePlaymode||EditorApplication.isCompiling||EditorApplication.isUpdating)throw new Exception("Exact stopped original49 Editor required");
  var seal=JObject.Parse(File.ReadAllText("Tools/URPMigration/ProductionSeal-current.json")).Value<string>("stage");var report=JObject.Parse(File.ReadAllText(seal+"/report.json"));
  foreach(JObject input in (JArray)report["inputs"])if(Hash(input.Value<string>("path"))!=input.Value<string>("sha256"))throw new Exception("Checkpoint input changed: "+input["path"]);
  var scene=UnityEngine.SceneManagement.SceneManager.GetActiveScene();if(scene.path!="Assets/Scenes/The_Game.unity"||scene.isDirty)throw new Exception("Exact clean Main required");
  var audit=JObject.Parse(File.ReadAllText("Builds/URPMigration/NativeEffectiveAudit-20261003T020905831Z/report.json"));foreach(JObject row in (JArray)audit["materials"]){var path=row["current"].Value<string>("path");foreach(var mat in AssetDatabase.LoadAllAssetsAtPath(path).OfType<Material>())if(EditorUtility.IsDirty(mat))throw new Exception("Preserve unsettled current material before close: "+path);}
  var marker=new JObject{["utc"]=DateTime.UtcNow.ToString("o"),["pid"]=145120,["scene"]=scene.path,["sceneDirty"]=false,["playing"]=false,["checkpointInputsVerified"]=((JArray)report["inputs"]).Count,["sourceLinearFlag"]=GraphicsSettings.lightsUseLinearIntensity,["sourceTemperatureFlag"]=GraphicsSettings.lightsUseColorTemperature,["action"]="One-shot official EditorApplication.update then Exit(0), no SaveAssets/force/process kill. Actual PID absence/post-exit hashes required."};File.WriteAllText(seal+"/close-request.json",marker.ToString());
  EditorApplication.CallbackFunction close=null;close=()=>{EditorApplication.update-=close;EditorApplication.Exit(0);};EditorApplication.update+=close;return Path.GetFullPath(seal+"/close-request.json");
 }
}
