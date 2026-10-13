using System;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Object=UnityEngine.Object;
public static class Native49SourceBodies {
 static JArray V(Vector3 v)=>new JArray(v.x,v.y,v.z);
 public static string Capture(){
  if(!EditorApplication.isPlaying||GraphicsSettings.currentRenderPipeline!=null)throw new Exception("ActualsourcePlayrequired");
  var enemies=Object.FindObjectsByType<Enemy>(FindObjectsInactive.Exclude).GroupBy(e=>e.name).Select(g=>g.First()).ToArray();
  if(enemies.Length==0)throw new Exception("Actualrealspawnsrequired");
  var c=Camera.main;if(c==null)throw new Exception("ActualMainrequired");var pos=c.transform.position;var rot=c.transform.rotation;var pause=EditorApplication.isPaused;var target=c.targetTexture;var stereo=c.stereoTargetEye;var aspect=c.aspect;var near=c.nearClipPlane;var far=c.farClipPlane;var active=RenderTexture.active;
  var stage="Builds/URPMigration/SourceBodies-"+DateTime.UtcNow.ToString("yyyyMMddTHHmmssfffZ");Directory.CreateDirectory(stage);var rows=new JArray();
  var rt=new RenderTexture(640,480,24,RenderTextureFormat.ARGB32);var tex=new Texture2D(640,480,TextureFormat.RGBA32,false);
  try{EditorApplication.isPaused=true;rt.Create();foreach(var e in enemies){
   var meshes=e.GetComponentsInChildren<Renderer>().Where(r=>r.enabled&&r.gameObject.activeInHierarchy&&!(r is ParticleSystemRenderer)).ToArray();if(meshes.Length==0)continue;
   var bounds=meshes[0].bounds;foreach(var r in meshes.Skip(1))bounds.Encapsulate(r.bounds);float radius=Mathf.Max(.1f,bounds.extents.magnitude);float distance=radius/Mathf.Sin(c.fieldOfView*.5f*Mathf.Deg2Rad)*1.45f;
   c.transform.position=bounds.center+new Vector3(1,.5f,-1).normalized*distance;c.transform.LookAt(bounds.center);c.nearClipPlane=Mathf.Min(near,Mathf.Max(.01f,distance*.01f));c.farClipPlane=Mathf.Max(far,distance*5);c.aspect=640f/480;c.stereoTargetEye=StereoTargetEyeMask.None;c.targetTexture=rt;c.Render();RenderTexture.active=rt;tex.ReadPixels(new Rect(0,0,640,480),0,0);tex.Apply();string file=stage+"/"+rows.Count.ToString("D2")+"-"+e.name.Replace('/','_')+".png";File.WriteAllBytes(file,tex.EncodeToPNG());
   rows.Add(new JObject{["path"]=file,["name"]=e.name,["id"]=e.GetEntityId().ToString(),["time"]=Time.time,["actualSourceSpawn"]=true,["cameraPosition"]=V(c.transform.position),["cameraRotation"]=new JArray(c.transform.rotation.x,c.transform.rotation.y,c.transform.rotation.z,c.transform.rotation.w),["fov"]=c.fieldOfView,["near"]=c.nearClipPlane,["far"]=c.farClipPlane,["mask"]=c.cullingMask,["boundsCenter"]=V(bounds.center),["boundsSize"]=V(bounds.size),["renderers"]=new JArray(meshes.Select(r=>new JObject{["name"]=r.name,["entityID"]=r.GetEntityId().ToString(),["worldScale"]=V(r.transform.lossyScale),["materials"]=new JArray(r.sharedMaterials.Select(m=>m==null?null:m.shader.name))}))});
  }}finally{c.transform.SetPositionAndRotation(pos,rot);c.targetTexture=target;c.stereoTargetEye=stereo;c.aspect=aspect;c.nearClipPlane=near;c.farClipPlane=far;RenderTexture.active=active;EditorApplication.isPaused=pause;Object.DestroyImmediate(tex);rt.Release();Object.DestroyImmediate(rt);}
  var report=new JObject{["utc"]=DateTime.UtcNow.ToString("o"),["rows"]=rows,["restores"]=new JObject{["cameraPose"]=c.transform.position==pos&&c.transform.rotation==rot,["target"]=c.targetTexture==target,["stereo"]=c.stereoTargetEye==stereo,["aspect"]=c.aspect==aspect,["near"]=c.nearClipPlane==near,["far"]=c.farClipPlane==far,["activeRT"]=RenderTexture.active==active,["pause"]=EditorApplication.isPaused==pause},["limits"]="Supplementaryactualspawnbodyframing, samepausednativeMaincamera/context. No cloned/inventedenemies/materials/lights, no gameplayevents. Camera temporarilymovesandrestores; notnaturalplayerpose coverage."};File.WriteAllText(stage+"/report.json",report.ToString());return new JObject{["path"]=stage+"/report.json",["images"]=rows.Count}.ToString();
 }
}
