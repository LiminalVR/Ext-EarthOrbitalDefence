using System;using System.IO;using Newtonsoft.Json.Linq;using UnityEditor;using UnityEngine;
public static class Native49RestoreTextureST {
 public static string Restore(){
  if(EditorApplication.isPlayingOrWillChangePlaymode||EditorApplication.isCompiling||EditorApplication.isUpdating)throw new Exception("Stopped ready required");
  const string path="Assets/PM_VoyagerTurretPack/Materials/low poly uvs_1001_AlbedoTransparency.mat";const string field="_DetailAlbedoMap";
  if(AssetDatabase.AssetPathToGUID(path)!="4d1bbf0dac892fb4eaf4f532efc7274a")throw new Exception("Wrong source GUID");var mat=AssetDatabase.LoadAssetAtPath<Material>(path);
  if(mat.shader.name!="Liminal/49/URP/Lit Source Properties"||mat.GetTextureOffset(field)!=new Vector2(0,1)||mat.GetTexture(field)!=null||mat.IsKeywordEnabled("_DETAIL_MULX2"))throw new Exception("Exact recorded metadata-only transfer delta required");
  var stage="Builds/URPMigration/TextureSTRestore-"+DateTime.UtcNow.ToString("yyyyMMddTHHmmssfffZ");Directory.CreateDirectory(stage);File.Copy(path,stage+"/before.mat");File.Copy(path+".meta",stage+"/before.mat.meta");var before=EditorJsonUtility.ToJson(mat);
  mat.SetTextureOffset(field,Vector2.zero);EditorUtility.SetDirty(mat);AssetDatabase.SaveAssetIfDirty(mat);
  File.WriteAllText(stage+"/report.json",new JObject{["utc"]=DateTime.UtcNow.ToString("o"),["pid"]=System.Diagnostics.Process.GetCurrentProcess().Id,["path"]=path,["field"]=field,["beforeJSON"]=before,["afterJSON"]=EditorJsonUtility.ToJson(mat),["expected"]=new JArray(0,0),["reason"]="Restore exact original source ST. Installed upgrader produced detail offsetY1 while source texture is null/detail keyword inactive. Metadata correction, no visible art scalar tuning or source gameplay changes."}.ToString());return Path.GetFullPath(stage+"/report.json");
 }
}
