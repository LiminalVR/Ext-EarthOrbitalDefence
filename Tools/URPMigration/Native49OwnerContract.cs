using System;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;
using Liminal.SDK.Core;
public static class Native49OwnerContract {
 static string H(Transform t)=>t.parent==null?t.name:H(t.parent)+"/"+t.name;
 public static string Inspect(){var stage="Builds/URPMigration/OwnerContract-"+DateTime.UtcNow.ToString("yyyyMMddTHHmmssfffZ");Directory.CreateDirectory(stage);var rows=new JArray(UnityEngine.Object.FindObjectsByType<AppController>(FindObjectsInactive.Include).Select(c=>{var owner=c.GetComponentInParent<ExperienceApp>(true);return new JObject{["controller"]=H(c.transform),["scene"]=c.gameObject.scene.path,["owner"]=owner==null?null:H(owner.transform),["ownerType"]=owner?.GetType().AssemblyQualifiedName,["ownerActive"]=owner!=null&&owner.isActiveAndEnabled,["camerasUnderOwner"]=owner==null?new JArray():new JArray(owner.GetComponentsInChildren<Camera>(true).Select(cam=>H(cam.transform)))};}));var result=new JObject{["utc"]=DateTime.UtcNow.ToString("o"),["playing"]=EditorApplication.isPlaying,["rows"]=rows,["coreModule"]=typeof(ExperienceApp).Module.ModuleVersionId.ToString(),["limits"]="Read-onlyactualAppControllerparentExperienceApp. No globalInstance reflection or fabricatedroot/camera ownership."};File.WriteAllText(stage+"/report.json",result.ToString());return result.ToString();}
}
