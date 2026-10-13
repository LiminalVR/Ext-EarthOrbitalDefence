using System;
using System.IO;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;
public static class Native49ProfileRepair {
 public static string Import(){
  if(EditorApplication.isPlayingOrWillChangePlaymode||EditorApplication.isCompiling)throw new Exception("Stopped/ready required");
  const string path="Assets/Liminal/Resources/LimappConfig.asset";
  var bytes=File.ReadAllBytes(path);var meta=File.ReadAllBytes(path+".meta");
  AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceUpdate|ImportAssetOptions.ForceSynchronousImport);
  var asset=AssetDatabase.LoadMainAssetAtPath(path);if(asset==null)throw new Exception("Profile remainsnull");
  var script=new SerializedObject(asset).FindProperty("m_Script").objectReferenceValue as MonoScript;
  if(script==null||script.GetClass()==null||script.GetClass().FullName!="ExperienceProfile")throw new Exception("Wrongnativeclass");
  AssetDatabase.TryGetGUIDAndLocalFileIdentifier(script,out string guid,out long id);
  if(guid!="1661fa78af9aece459a3302206a05902"||id!=481487832)throw new Exception("Wrongscriptidentity");
  if(!System.Linq.Enumerable.SequenceEqual(bytes,File.ReadAllBytes(path))||!System.Linq.Enumerable.SequenceEqual(meta,File.ReadAllBytes(path+".meta")))throw new Exception("Import alteredprofilebytes");
  var stage=(string)JObject.Parse(File.ReadAllText("Tools/URPMigration/ProfileRepair-current.json"))["stage"];
  var r=new JObject{["utc"]=DateTime.UtcNow.ToString("o"),["pid"]=System.Diagnostics.Process.GetCurrentProcess().Id,["loaded"]=true,["class"]=script.GetClass().AssemblyQualifiedName,["scriptGUID"]=guid,["scriptLocalID"]=id,["assetJSON"]=EditorJsonUtility.ToJson(asset),["assetDirty"]=EditorUtility.IsDirty(asset),["assetAndMetaBytesRetained"]=true,["limitations"]="ExactEditorbinding verified; packedAndroid Releaseprovider/profilefields require actualBuildPlayer."};
  File.WriteAllText(stage+"/native-import.json",r.ToString());return r.ToString();
 }
}
