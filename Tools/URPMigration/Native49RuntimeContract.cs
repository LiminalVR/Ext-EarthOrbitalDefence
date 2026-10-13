using System;
using System.Linq;
using System.Reflection;
using System.IO;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEditor;
using Liminal.SDK.Core;
public static class Native49RuntimeContract{
 public static string Inspect(){
  var t=typeof(ExperienceApp);var flags=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance|BindingFlags.Static;
  var r=new JObject{["utc"]=DateTime.UtcNow.ToString("o"),["type"]=t.AssemblyQualifiedName,["properties"]=new JArray(t.GetProperties(flags).Select(x=>new JObject{["name"]=x.Name,["type"]=x.PropertyType.FullName})),["fields"]=new JArray(t.GetFields(flags).Select(x=>new JObject{["name"]=x.Name,["type"]=x.FieldType.FullName})),["events"]=new JArray(t.GetEvents(flags).Select(x=>new JObject{["name"]=x.Name,["type"]=x.EventHandlerType.FullName})),["methods"]=new JArray(t.GetMethods(flags).Where(m=>m.Name.Contains("End")||m.Name.Contains("Complete")).Select(m=>m.ToString()))};
  var stage="Builds/URPMigration/RuntimeSDKContract-"+DateTime.UtcNow.ToString("yyyyMMddTHHmmssfffZ");Directory.CreateDirectory(stage);File.WriteAllText(stage+"/report.json",r.ToString());return r.ToString();
 }
}
