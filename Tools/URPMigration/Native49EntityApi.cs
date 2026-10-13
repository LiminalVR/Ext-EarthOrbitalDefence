using System;using System.Linq;using System.Reflection;using Newtonsoft.Json.Linq;using UnityEngine;using UnityEditor;
public static class Native49EntityApi {
 public static string Inspect()=>new JObject{["constructors"]=new JArray(typeof(EntityId).GetConstructors(BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic).Select(m=>m.ToString())),["members"]=new JArray(typeof(EntityId).GetMethods(BindingFlags.Static|BindingFlags.Public).Select(m=>m.ToString())),["fields"]=new JArray(typeof(EntityId).GetFields(BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance).Select(m=>m.ToString())),["editor"]=new JArray(typeof(EditorUtility).GetMethods().Where(m=>m.Name=="EntityIdToObject").Select(m=>m.ToString()))}.ToString();
}
