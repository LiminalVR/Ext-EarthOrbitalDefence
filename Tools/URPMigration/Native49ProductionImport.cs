using System;using System.IO;using System.Linq;using Newtonsoft.Json.Linq;using UnityEditor;using UnityEngine.Rendering;
public static class Native49ProductionImport {
 public static string ImportExact(){
  if(EditorApplication.isPlayingOrWillChangePlaymode||EditorApplication.isCompiling||EditorApplication.isUpdating||GraphicsSettings.currentRenderPipeline!=null)throw new Exception("Stopped ready original source required");
  var deployment=JObject.Parse(File.ReadAllText("Tools/URPMigration/ProductionDeployment-current.json")).Value<string>("stage");
  var packet=JObject.Parse(File.ReadAllText("Tools/URPMigration/ProductionCandidate-current.json")).Value<string>("stage");var files=(JArray)JObject.Parse(File.ReadAllText(packet+"/manifest.json"))["productionFiles"];
  var report=new JObject{["utc"]=DateTime.UtcNow.ToString("o"),["pid"]=System.Diagnostics.Process.GetCurrentProcess().Id,["state"]="Import requested; native reload/loaded module/readiness required",["exactFiles"]=new JArray(files.Values<JObject>().Where(r=>r.Value<string>("deployPath").StartsWith("Assets/")).Select(r=>r["deployPath"]))};
  File.WriteAllText(deployment+"/import-request.json",report.ToString());
  AssetDatabase.StartAssetEditing();try{
   foreach(string folder in new[]{"Assets/Shaders/Source49","Assets/URPMigration/Source49"})AssetDatabase.ImportAsset(folder,ImportAssetOptions.ForceUpdate|ImportAssetOptions.ImportRecursive);
   foreach(JObject row in files){var path=row.Value<string>("deployPath");if(path.StartsWith("Assets/"))AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceUpdate);}
  }finally{AssetDatabase.StopAssetEditing();}
  return Path.GetFullPath(deployment+"/import-request.json");
 }
}
