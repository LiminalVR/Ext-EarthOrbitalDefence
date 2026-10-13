using System;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEditor.Rendering;
using UnityEngine;
using UnityEngine.Rendering;
public static class Native49PbrCompile {
 static string Hash(string p)=>BitConverter.ToString(System.Security.Cryptography.SHA256.Create().ComputeHash(File.ReadAllBytes(p))).Replace("-","").ToLowerInvariant();
 public static string Run(){
  if(EditorApplication.isPlayingOrWillChangePlaymode||EditorApplication.isCompiling||EditorApplication.isUpdating)throw new Exception("Stopped ready source required");
  var root=JObject.Parse(File.ReadAllText("Tools/URPMigration/PbrCandidate-current.json")).Value<string>("stage");var path=root+"/LitSourceProperties.shader";var scope=Directory.GetDirectories("Builds/URPMigration","ReachableScope-*").OrderBy(x=>x).Last()+"/report.json";var doc=JObject.Parse(File.ReadAllText(scope));
  var sourceMats=doc.Descendants().OfType<JObject>().Where(m=>m.Value<string>("shader")?.StartsWith("Standard")==true&&m["keywords"]!=null).GroupBy(m=>m.Value<string>("guid")+"/"+m.Value<long>("localID")).Select(g=>g.First()).ToArray();
  var sets=sourceMats.Select(m=>{
   var keys=((JArray)m["keywords"]).Values<string>().Select(k=>k=="_METALLICGLOSSMAP"||k=="_SPECGLOSSMAP"?"_METALLICSPECGLOSSMAP":k).ToList();
   if(m.Value<string>("shader")=="Standard (Specular setup)")keys.Add("_SPECULAR_SETUP");
   if(keys.Contains("_ALPHAPREMULTIPLY_ON")||keys.Contains("_ALPHABLEND_ON"))keys.Add("_SURFACE_TYPE_TRANSPARENT");keys.Remove("_ALPHABLEND_ON");keys.Add("EVALUATE_SH_MIXED");keys.Add("_MAIN_LIGHT_SHADOWS");return keys.Distinct().OrderBy(k=>k).ToArray();
  }).GroupBy(k=>string.Join("|",k)).Select(g=>g.First()).ToArray();
  var stage="Builds/URPMigration/PbrNativeCompile-"+DateTime.UtcNow.ToString("yyyyMMddTHHmmssfffZ");Directory.CreateDirectory(stage);File.Copy("Tools/URPMigration/Native49PbrCompile.cs",stage+"/Native49PbrCompile.cs");var rows=new JArray();Shader shader=null;
  try{shader=ShaderUtil.CreateShaderAsset(File.ReadAllText(path),false);shader.hideFlags=HideFlags.HideAndDontSave;var pass=ShaderUtil.GetShaderData(shader).GetSubshader(0).GetPass(0);File.WriteAllText(stage+"/native-forward-source.hlsl",pass.SourceCode);
   foreach(var keys in sets)foreach(var xr in new[]{new string[0],new[]{"INSTANCING_ON","STEREO_MULTIVIEW_ON"},new[]{"INSTANCING_ON","STEREO_INSTANCING_ON"}})foreach(var platform in new[]{ShaderCompilerPlatform.D3D,ShaderCompilerPlatform.GLES3x})foreach(var type in new[]{ShaderType.Vertex,ShaderType.Fragment}){
    var all=keys.Concat(xr).ToArray();var result=pass.CompileVariant(type,all,platform,BuildTarget.Android,GraphicsTier.Tier3,true);var bytes=result.ShaderData??Array.Empty<byte>();var output="program-"+rows.Count.ToString("D3")+".bin";File.WriteAllBytes(stage+"/"+output,bytes);rows.Add(new JObject{["shader"]=shader.name,["pass"]=pass.Name,["platform"]=platform.ToString(),["stage"]=type.ToString(),["keywords"]=new JArray(all),["success"]=result.Success,["hasStage"]=pass.HasShaderStage(type),["bytes"]=bytes.Length,["output"]=output,["messages"]=new JArray(result.Messages.Select(m=>new JObject{["severity"]=m.severity.ToString(),["message"]=m.message,["file"]=m.file,["line"]=m.line}))});
   }
  }finally{if(shader!=null)UnityEngine.Object.DestroyImmediate(shader);File.WriteAllText(stage+"/report.json",new JObject{["utc"]=DateTime.UtcNow.ToString("o"),["pid"]=System.Diagnostics.Process.GetCurrentProcess().Id,["candidatePath"]=path,["candidateSHA256"]=Hash(path),["sourceScope"]=Path.GetFullPath(scope),["sourceScopeSHA256"]=Hash(scope),["sourceMaterialIdentities"]=new JArray(sourceMats.Select(m=>new JObject{["path"]=m["path"],["guid"]=m["guid"],["localID"]=m["localID"],["sourceKeywords"]=m["keywords"]})),["uniqueMaterialSets"]=sets.Length,["rows"]=rows,["failed"]=rows.Count(r=>!(bool)r["success"]||!(bool)r["hasStage"]||(int)r["bytes"]==0),["limits"]="Native in-memory candidate forward-pass D3D/GLES3x Android/Tier3 compilation of actual reachable Standard/specular keyword sets plus XR multiview/stereo instancing. Source flags and baked provider are separate GPU proofs; persistent cold ordinary Main and actual BuildPlayer/Quest remain pending. Fog is authored disabled; no unrelated lightmap/additional-light branch acceptance."}.ToString());}
  if(rows.Any(r=>!(bool)r["success"]||!(bool)r["hasStage"]||(int)r["bytes"]==0))throw new Exception("Native candidate compile failed; retained "+stage);return Path.GetFullPath(stage+"/report.json");
 }
}
