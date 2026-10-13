using System;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
public static class Native49AutodeskResolution {
 public static string Inspect(){
  string path="Packages/com.unity.render-pipelines.universal/Shaders/AutodeskInteractive/AutodeskInteractive.shadergraph";
  var all=AssetDatabase.LoadAllAssetsAtPath(path);var found=Shader.Find("Universal Render Pipeline/Autodesk Interactive/AutodeskInteractive");
  var type=typeof(UniversalRenderPipelineAsset).Assembly.GetType("UnityEngine.Rendering.Universal.UniversalRenderPipelineEditorShaders");
  var method=typeof(GraphicsSettings).GetMethods().Single(m=>m.Name=="TryGetRenderPipelineSettings"&&m.IsGenericMethodDefinition);object[] args={null};bool exists=type!=null&&(bool)method.MakeGenericMethod(type).Invoke(null,args);
  var shader=exists?type.GetProperty("autodeskInteractiveShader").GetValue(args[0]) as Shader:null;
  var r=new JObject{["utc"]=DateTime.UtcNow.ToString("o"),["graphicsDevice"]=SystemInfo.graphicsDeviceType.ToString(),["pipelineNull"]=GraphicsSettings.currentRenderPipeline==null,["settingsType"]=type==null?null:type.FullName,["nativeSettingsFound"]=exists,["settingsShader"]=shader==null?null:shader.name,["ShaderFind"]=found==null?null:found.name,["shaderGraphPath"]=path,["nativeAssetObjects"]=new JArray(all.Select(a=>new JObject{["name"]=a.name,["type"]=a.GetType().FullName,["path"]=AssetDatabase.GetAssetPath(a)})),["limits"]="Read-onlyresourceauthentication. Initialnativeupgrader error provesresource-resolution failure, not absentgeneratedgraph orShader.Findfailure."};var stage="Builds/URPMigration/AutodeskResolution-"+DateTime.UtcNow.ToString("yyyyMMddTHHmmssfffZ");Directory.CreateDirectory(stage);File.WriteAllText(stage+"/report.json",r.ToString());return r.ToString();
 }
}
