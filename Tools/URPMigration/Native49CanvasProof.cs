using System;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.Rendering;
using UnityEditor;
using Object=UnityEngine.Object;
public static class Native49CanvasProof {
 static JObject TextState(Text t){
  var mesh=new Mesh();int? canvasVertexCount=null;
  try{var methods=typeof(CanvasRenderer).GetMethods(System.Reflection.BindingFlags.Public|System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance).Where(m=>m.Name=="GetMesh").ToArray();var method=methods.FirstOrDefault(m=>m.GetParameters().Length==1&&m.GetParameters()[0].ParameterType==typeof(Mesh));if(method!=null){method.Invoke(t.canvasRenderer,new object[]{mesh});canvasVertexCount=mesh.vertexCount;}else{var noArgs=methods.FirstOrDefault(m=>m.GetParameters().Length==0);if(noArgs!=null){var actual=noArgs.Invoke(t.canvasRenderer,null) as Mesh;if(actual!=null)canvasVertexCount=actual.vertexCount;}}}
  finally{Object.DestroyImmediate(mesh);}
  var gen=t.cachedTextGenerator;
  return new JObject{["name"]=t.name,["text"]=t.text,["textLength"]=t.text.Length,["generatorCharacterCount"]=gen.characterCount,["generatorVisibleCharacterCount"]=gen.characterCountVisible,["generatorVerts"]=gen.verts.Count,["generatorLines"]=gen.lines.Count,["canvasMeshVertices"]=canvasVertexCount,["enabled"]=t.isActiveAndEnabled,["font"]=t.font==null?null:t.font.name};
 }
 public static string Click(){
  if(!EditorApplication.isPlaying||GraphicsSettings.currentRenderPipeline!=null)throw new Exception("NormalsourcePlayrequired");
  var app=Object.FindObjectsByType<AppController>(FindObjectsInactive.Exclude).Single();if(app.CurrentTime!=0)throw new Exception("BeforeUIStartrequired");
  var button=Object.FindObjectsByType<Button>(FindObjectsInactive.Exclude).Single(b=>Enumerable.Range(0,b.onClick.GetPersistentEventCount()).Any(i=>b.onClick.GetPersistentMethodName(i)=="StartTimer"));
  ExecuteEvents.Execute(button.gameObject,new PointerEventData(EventSystem.current){button=PointerEventData.InputButton.Left},ExecuteEvents.pointerClickHandler);return "NormalUIpointerclickissued; nofire/pose/timer/scorewrites";
 }
 public static string ClickAndTimerProof(){
  Click();var app=Object.FindObjectsByType<AppController>(FindObjectsInactive.Exclude).Single();var request="Builds/URPMigration/TimedCanvasRequest-"+DateTime.UtcNow.ToString("yyyyMMddTHHmmssfffZ");Directory.CreateDirectory(request);
  File.WriteAllText(request+"/request.json",new JObject{["utc"]=DateTime.UtcNow.ToString("o"),["ordinaryUIStart"]=true,["requestedGameTime"]=2,["shortDiagnostic"]=true}.ToString());
  EditorApplication.CallbackFunction observe=null;observe=()=>{if(!EditorApplication.isPlaying){EditorApplication.update-=observe;return;}if(app.CurrentTime<2)return;EditorApplication.update-=observe;try{File.WriteAllText(request+"/result.json",Capture());}catch(Exception ex){File.WriteAllText(request+"/failure.json",new JObject{["error"]=ex.ToString()}.ToString());}finally{EditorApplication.isPlaying=false;}};EditorApplication.update+=observe;
  return request;
 }
 public static string Capture(){
  if(!EditorApplication.isPlaying||GraphicsSettings.currentRenderPipeline!=null)throw new Exception("NormalsourcePlayrequired");
  var app=Object.FindObjectsByType<AppController>(FindObjectsInactive.Exclude).Single();var c=Camera.main;var paused=EditorApplication.isPaused;var target=c.targetTexture;var active=RenderTexture.active;var stereo=c.stereoTargetEye;var aspect=c.aspect;
  var stage="Builds/URPMigration/CanvasCaptureProof-"+DateTime.UtcNow.ToString("yyyyMMddTHHmmssfffZ");Directory.CreateDirectory(stage);var rt=new RenderTexture(800,450,24,RenderTextureFormat.ARGB32);var tex=new Texture2D(800,450,TextureFormat.RGBA32,false);var rows=new JArray();
  try{EditorApplication.isPaused=true;rt.Create();c.targetTexture=rt;c.stereoTargetEye=StereoTargetEyeMask.None;c.aspect=800f/450;
   foreach(bool sync in new[]{false,true,false}){var before=new JArray(Object.FindObjectsByType<Text>(FindObjectsInactive.Exclude).Select(TextState));if(sync)Canvas.ForceUpdateCanvases();c.Render();RenderTexture.active=rt;tex.ReadPixels(new Rect(0,0,800,450),0,0);tex.Apply();var file=stage+"/"+rows.Count+"-"+(sync?"CanvasSync":"OrdinaryRender")+".png";File.WriteAllBytes(file,tex.EncodeToPNG());rows.Add(new JObject{["path"]=file,["canvasSyncRequested"]=sync,["gameTime"]=app.CurrentTime,["actualTimerText"]=app.TextPanel.text,["uiStateBefore"]=before,["uiStateAfter"]=new JArray(Object.FindObjectsByType<Text>(FindObjectsInactive.Exclude).Select(TextState))});}
  }finally{c.targetTexture=target;c.stereoTargetEye=stereo;c.aspect=aspect;RenderTexture.active=active;EditorApplication.isPaused=paused;Object.DestroyImmediate(tex);rt.Release();Object.DestroyImmediate(rt);}
  var r=new JObject{["utc"]=DateTime.UtcNow.ToString("o"),["rows"]=rows,["restores"]=new JObject{["target"]=c.targetTexture==target,["stereo"]=c.stereoTargetEye==stereo,["aspect"]=c.aspect==aspect,["activeRT"]=RenderTexture.active==active,["pause"]=EditorApplication.isPaused==paused},["limits"]="ShortactualsourceUIrender-capturediagnostic. NativeCanvaslayoutsync only; notnewfullsourcegameplay or manufacturedendstate. Original180.004/SDKEndroute unchanged."};File.WriteAllText(stage+"/report.json",r.ToString());return new JObject{["path"]=stage+"/report.json",["gameTime"]=app.CurrentTime,["text"]=app.TextPanel.text}.ToString();
 }
}
