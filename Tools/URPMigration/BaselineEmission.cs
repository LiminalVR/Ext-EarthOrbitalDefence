using System;using System.IO;using System.Linq;using UnityEngine;using UnityEditor;using Newtonsoft.Json.Linq;
public static class BaselineEmission {
 public static string Apply(){
 if(EditorApplication.isPlaying)throw new Exception("Stop Play first.");
 var inventory=JObject.Parse(File.ReadAllText("Builds/URPMigration/BuiltIn/appearance.json"));
 if((string)inventory["colorSpace"]!="Linear")throw new Exception("This mapping was verified for Linear projects only.");
 var changes=new JArray();
 foreach(var entry in inventory["materials"]){
 var shader=(string)entry["shader"];if(shader!="Standard"&&shader!="Standard (Specular setup)")continue;
 var path=(string)entry["path"];if(!path.EndsWith(".mat"))continue;
 var v=entry["properties"]["_EmissionColor"] as JArray;if(v==null)continue;
 var mat=AssetDatabase.LoadAssetAtPath<Material>(path);if(mat==null||!mat.shader.name.StartsWith("Universal Render Pipeline/"))continue;
 var source=Shader.Find(shader);int index=source.FindPropertyIndex("_EmissionColor");
 bool convert=(source.GetPropertyFlags(index)&UnityEngine.Rendering.ShaderPropertyFlags.HDR)==0;
 var authored=new Color((float)v[0],(float)v[1],(float)v[2],(float)v[3]);var expected=convert?authored.linear:authored;
 var before=mat.GetColor("_EmissionColor");if(before==expected)continue;
 mat.SetColor("_EmissionColor",expected);EditorUtility.SetDirty(mat);AssetDatabase.SaveAssetIfDirty(mat);
 changes.Add(new JObject{["path"]=path,["before"]=new JArray(before.r,before.g,before.b,before.a),["expected"]=new JArray(expected.r,expected.g,expected.b,expected.a)});
 }
 File.WriteAllText("Builds/URPMigration/URP/emission-correction.json",changes.ToString());return "Derived emission directly from preserved baseline for "+changes.Count+" materials.";
 }}
