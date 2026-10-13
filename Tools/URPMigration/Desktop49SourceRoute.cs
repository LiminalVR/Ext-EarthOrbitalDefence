using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Reflection;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.Rendering;
using Liminal.SDK.V2;
using Liminal.SDK.VR;
using Liminal.SDK.VR.Input;
using Liminal.SDK.Core;
using Object=UnityEngine.Object;
// OutsideAssets observer: real SDK input/controller poses, source UI events and physics.
public static class Desktop49SourceRoute {
 static string stage;static bool brief,finished,createdAuto;static float started,nextSample,nextAim,ending=-1;static int frame=-1,lastShots,lastKills,lastHealth;static AppController app;static Fire fire;static SpawnSystem spawn;static Health health;
 static ExperienceAppManager manager;static AutoClicker auto;static AutoClicker.ClickMode oldMode;static AutoClicker.Hands oldHands;static float oldRate;
 static Transform controller;static Vector3 oldPos;static Quaternion oldRot;static Behaviour[] drivers;static bool[] driverFlags;static EventInfo completeEvent;static Action<bool> onComplete;static bool sdkComplete,sdkResult;static object completeTarget;
 static JArray images,events,samples,errors;static HashSet<string> captured;static Enemy target;
 static string H(Transform t)=>t.parent==null?t.name:H(t.parent)+"/"+t.name;
 static JArray V(Vector3 v)=>new JArray(v.x,v.y,v.z);
 static void Log(string text,string trace,LogType kind){if(kind==LogType.Error||kind==LogType.Exception||kind==LogType.Assert)errors.Add(new JObject{["time"]=Time.time,["kind"]=kind.ToString(),["message"]=text,["stack"]=trace});}
 public static string Short()=>Start(true);
 public static string Full()=>Start(false);
 static string Start(bool shortMode){
  if(!EditorApplication.isPlaying||GraphicsSettings.currentRenderPipeline!=null)throw new Exception("NormalSourcePlayrequired");
  app=Object.FindObjectsByType<AppController>(FindObjectsInactive.Exclude).Single();fire=app.FireController;spawn=app.SpawnSystem;health=Object.FindObjectsByType<Health>(FindObjectsInactive.Exclude).Single();
  if(app.CurrentTime!=0||spawn.Active||fire==null)throw new Exception("AttachbeforeordinaryStart");
  if(VRDevice.Device?.PrimaryInputDevice==null||PlayerAim.GetRayInteractor()==null)throw new Exception("NormalSDKdevice/rayrequired");
  manager=Object.FindObjectsByType<ExperienceAppManager>(FindObjectsInactive.Exclude).Single();if(manager.SpawnedRig==null)throw new Exception("Normalrigrequired");
  var button=Object.FindObjectsByType<Button>(FindObjectsInactive.Exclude).Single(b=>Enumerable.Range(0,b.onClick.GetPersistentEventCount()).Any(i=>b.onClick.GetPersistentMethodName(i)=="StartTimer"));
  stage=Path.GetFullPath("Builds/URPMigration/"+(shortMode?"SourceSmoke-":"SourceRoute-")+DateTime.UtcNow.ToString("yyyyMMddTHHmmssfffZ"));Directory.CreateDirectory(stage);File.Copy("Tools/URPMigration/Desktop49SourceRoute.cs",stage+"/Desktop49SourceRoute.cs");
  brief=shortMode;finished=false;started=Time.time;frame=-1;ending=-1;nextSample=0;nextAim=0;sdkComplete=false;images=new JArray();events=new JArray();samples=new JArray();errors=new JArray();captured=new HashSet<string>();lastShots=0;lastKills=0;lastHealth=health.currentHealth;
  controller=VRDevice.Device.PrimaryInputDevice.Hand==VRInputDeviceHand.Left?manager.SpawnedRig.LeftController:manager.SpawnedRig.RightController;oldPos=controller.localPosition;oldRot=controller.localRotation;drivers=controller.GetComponents<Behaviour>().Where(b=>b.GetType().Name=="TrackedPoseDriver").ToArray();driverFlags=drivers.Select(b=>b.enabled).ToArray();foreach(var d in drivers)d.enabled=false;
  auto=AutoClicker.Instance;createdAuto=auto==null;if(createdAuto)auto=manager.gameObject.AddComponent<AutoClicker>();oldMode=auto.Mode;oldHands=auto.EnabledHands;oldRate=auto.ClickRate;auto.ExperienceAppManager=manager;auto.Mode=AutoClicker.ClickMode.Hold;auto.EnabledHands=AutoClicker.Hands.Both;auto.ClickRate=.2f;
  completeEvent=typeof(ExperienceApp).GetEvent("OnComplete",BindingFlags.Public|BindingFlags.Static|BindingFlags.Instance);completeTarget=completeEvent.AddMethod.IsStatic?null:manager.ExperienceApp;onComplete=ok=>{sdkComplete=true;sdkResult=ok;events.Add(new JObject{["event"]="ActualSDKOnComplete",["time"]=Time.time,["result"]=ok});};completeEvent.AddEventHandler(completeTarget,onComplete);
  Application.logMessageReceived+=Log;EditorApplication.update+=Tick;EditorApplication.playModeStateChanged+=PlayState;
  events.Add(new JObject{["event"]="EditorControllerPoseFixture",["hierarchy"]=H(controller),["savedLocalPosition"]=V(oldPos),["savedLocalRotation"]=new JArray(oldRot.x,oldRot.y,oldRot.z,oldRot.w),["disabledDriverCount"]=drivers.Length});
  Capture("BeforeStart");events.Add(new JObject{["event"]="NormalUIButtonPointerClick",["button"]=H(button.transform),["callbacks"]=new JArray(Enumerable.Range(0,button.onClick.GetPersistentEventCount()).Select(i=>new JObject{["method"]=button.onClick.GetPersistentMethodName(i),["target"]=button.onClick.GetPersistentTarget(i)?.GetType().FullName}))});
  ExecuteEvents.Execute(button.gameObject,new PointerEventData(EventSystem.current){button=PointerEventData.InputButton.Left},ExecuteEvents.pointerClickHandler);
  Capture("StartedUI");Save(false,"Observingordinarysource");return stage;
 }
 static void PlayState(PlayModeStateChange s){if(s==PlayModeStateChange.ExitingPlayMode&&!finished)Finish("NormalPlayexit");}
 static void Tick(){
  if(finished||!EditorApplication.isPlaying||frame==Time.frameCount)return;frame=Time.frameCount;
  try{
   if(app==null){Finish("Experienceobjectsdestroyed");return;}
   Aim();var shots=fire.GetShotsFired();var kills=fire.GetEnemiesKilled();
   if(shots!=lastShots){events.Add(new JObject{["event"]="ActualShot",["time"]=Time.time,["count"]=shots,["target"]=target==null?null:H(target.transform)});if(captured.Add("Laser"))Capture("Laser");lastShots=shots;}
   if(kills!=lastKills){events.Add(new JObject{["event"]="ActualKill",["time"]=Time.time,["count"]=kills});if(captured.Add("Impact"))Capture("Impact");lastKills=kills;}
   if(health!=null&&health.currentHealth!=lastHealth){events.Add(new JObject{["event"]="ActualEarthHealth",["time"]=Time.time,["health"]=health.currentHealth});lastHealth=health.currentHealth;}
   foreach(var enemy in spawn.ActiveEnemies.Where(e=>e!=null)){string family=enemy.name.Replace("(Clone)","").Trim();if(captured.Add("Spawn-"+family)){events.Add(new JObject{["event"]="ActualEnemyFamily",["time"]=Time.time,["name"]=family,["id"]=enemy.GetEntityId().ToString(),["position"]=V(enemy.transform.position)});Capture("Spawn-"+family);if(!brief){BodyCapture(enemy,family,0);BodyCapture(enemy,family,1);}}}
   foreach(int t in new[]{5,15,60,120,180})if(app.CurrentTime>=t&&captured.Add("Time"+t))Capture("GameTime-"+t);
   if(Time.time>=nextSample){samples.Add(State());nextSample=Time.time+1;Save(false,"Observingordinarysource");}
   if(!spawn.Active&&app.CurrentTime>0){if(ending<0){ending=Time.time;Capture("AuthoredEndingStarted");}if(Time.time-ending>=6&&captured.Add("Score"))Capture("ScoreDisplay");}
   if(brief&&Time.time-started>=20){Finish("20-secondsourceinput/framing smoke; nofullrouteacceptance");return;}
   if(!brief&&sdkComplete){Capture("SDKComplete");Finish("ActualSDKOnCompleteobserved");return;}
   if(Time.time-started>220){Finish("220-secondobserverguard; incompleteifSDKnotcomplete");return;}
   if(errors.Count>0){Finish("Actualerrorgate");}
  }catch(Exception ex){errors.Add(new JObject{["time"]=Time.time,["kind"]="ObserverException",["message"]=ex.ToString()});Finish("Observererror");}
 }
 static void Aim(){
  if(controller==null||Camera.main==null)return;controller.position=Camera.main.transform.TransformPoint(new Vector3(.4f,-.35f,.5f));
  if(Time.time<nextAim)return;nextAim=Time.time+.2f;
  var origin=PlayerAim.GetAimOrigin(PlayerAim.GetRayInteractor());
  target=spawn.ActiveEnemies.Where(e=>e!=null&&e.gameObject.activeInHierarchy).OrderBy(e=>Vector3.Distance(origin.position,e.transform.position)).FirstOrDefault();
  if(target!=null){var d=target.transform.position-origin.position;if(d.sqrMagnitude>.001f)controller.rotation=Quaternion.FromToRotation(origin.forward,d.normalized)*controller.rotation;}
  // Periodic released input exercises ordinary recharge, without charge writes.
  auto.EnabledHands=(app.CurrentTime>10&&((int)app.CurrentTime%14)>=11)?AutoClicker.Hands.None:AutoClicker.Hands.Both;
 }
 static JObject State()=>new JObject{["time"]=Time.time,["gameTime"]=app.CurrentTime,["remaining"]=app.CountdownTime,["shots"]=fire.GetShotsFired(),["kills"]=fire.GetEnemiesKilled(),["charge"]=fire.playerLaser.CurrentLaserCharge,["canFire"]=fire.playerLaser.CanFire,["earthHealth"]=health==null?(JToken)JValue.CreateNull():health.currentHealth,["spawnActive"]=spawn.Active,["activeEnemies"]=new JArray(spawn.ActiveEnemies.Where(e=>e!=null).Select(e=>new JObject{["id"]=e.GetEntityId().ToString(),["name"]=e.name,["position"]=V(e.transform.position)})),["laserPositions"]=new JArray(Enumerable.Range(0,fire.playerLaser.LaserRend.positionCount).Select(i=>V(fire.playerLaser.LaserRend.GetPosition(i)))),["sdkComplete"]=sdkComplete,["sdkIsEnding"]=ExperienceApp.IsEnding,["audioVolume"]=AudioListener.volume};
 static void BodyCapture(Enemy enemy,string family,int view){
  var meshes=enemy.GetComponentsInChildren<Renderer>().Where(r=>r.enabled&&r.gameObject.activeInHierarchy&&!(r is ParticleSystemRenderer)).ToArray();if(meshes.Length==0)return;
  var b=meshes[0].bounds;foreach(var r in meshes.Skip(1))b.Encapsulate(r.bounds);var c=Camera.main;var pos=c.transform.position;var rot=c.transform.rotation;var near=c.nearClipPlane;var far=c.farClipPlane;
  try{float radius=Mathf.Max(.1f,b.extents.magnitude);float distance=radius/Mathf.Sin(c.fieldOfView*.5f*Mathf.Deg2Rad)*1.5f;c.transform.position=b.center+(view==0?new Vector3(1,.5f,-1):new Vector3(-1,.3f,1)).normalized*distance;c.transform.LookAt(b.center);c.nearClipPlane=Mathf.Min(near,Mathf.Max(.01f,distance*.01f));c.farClipPlane=Mathf.Max(far,distance*5);Capture("Body-"+family+"-"+view);var row=(JObject)images.Last;row["supplementaryActualBodyFraming"]=true;row["actualEnemyID"]=enemy.GetEntityId().ToString();row["bodyBoundsCenter"]=V(b.center);row["bodyBoundsSize"]=V(b.size);row["meaning"]="Actualfirstnaturallyspawnedfamilybody; temporarycamera-framingonly, no object/light/material/property changes; originalpose/near/far restored.";}
  finally{c.transform.SetPositionAndRotation(pos,rot);c.nearClipPlane=near;c.farClipPlane=far;}
 }
 static void Capture(string label){
  var c=Camera.main;if(c==null)throw new Exception("ActualMaincamerarequired");var old=c.targetTexture;var active=RenderTexture.active;var stereo=c.stereoTargetEye;var aspect=c.aspect;var rt=new RenderTexture(800,450,24,RenderTextureFormat.ARGB32);var tex=new Texture2D(800,450,TextureFormat.RGBA32,false);string file=stage+"/"+images.Count.ToString("D2")+"-"+label.Replace('/','_')+".png";
  try{rt.Create();c.targetTexture=rt;c.stereoTargetEye=StereoTargetEyeMask.None;c.aspect=800f/450;c.Render();RenderTexture.active=rt;tex.ReadPixels(new Rect(0,0,800,450),0,0);tex.Apply();File.WriteAllBytes(file,tex.EncodeToPNG());images.Add(new JObject{["label"]=label,["path"]=file,["time"]=Time.time,["camera"]=H(c.transform),["position"]=V(c.transform.position),["rotation"]=new JArray(c.transform.rotation.x,c.transform.rotation.y,c.transform.rotation.z,c.transform.rotation.w),["fov"]=c.fieldOfView,["near"]=c.nearClipPlane,["far"]=c.farClipPlane,["cullingMask"]=c.cullingMask,["state"]=State()});}
  finally{c.targetTexture=old;c.stereoTargetEye=stereo;c.aspect=aspect;RenderTexture.active=active;Object.DestroyImmediate(tex);rt.Release();Object.DestroyImmediate(rt);}
 }
 static void Save(bool complete,string reason){File.WriteAllText(stage+"/observation.json",new JObject{["utc"]=DateTime.UtcNow.ToString("o"),["pid"]=System.Diagnostics.Process.GetCurrentProcess().Id,["completed"]=complete,["shortMode"]=brief,["reason"]=reason,["elapsed"]=Time.time-started,["gameTime"]=app==null?0:app.CurrentTime,["sdkComplete"]=sdkComplete,["sdkResult"]=sdkResult,["events"]=events,["samples"]=samples,["images"]=images,["errors"]=errors,["limits"]="ActualsourceUIpointerclick/SDKpublicAutoClicker andrealSphereCast/Enemycollisions. SavedEditorcontrollerposes/restoredflags, nofakeSDK/no timer/score/health/Kill/eventwrites. Naturalstochasticimages notmatchedURP. Sourcecamera explicitmono capture. Noheadset/playeracceptance."}.ToString());}
 static void Finish(string reason){if(finished)return;finished=true;EditorApplication.update-=Tick;EditorApplication.playModeStateChanged-=PlayState;Application.logMessageReceived-=Log;completeEvent?.RemoveEventHandler(completeTarget,onComplete);if(controller!=null){controller.localPosition=oldPos;controller.localRotation=oldRot;}for(int i=0;i<drivers.Length;i++)if(drivers[i]!=null)drivers[i].enabled=driverFlags[i];if(auto!=null){if(createdAuto)Object.DestroyImmediate(auto);else{auto.Mode=oldMode;auto.EnabledHands=oldHands;auto.ClickRate=oldRate;}}events.Add(new JObject{["event"]="ControllerFixtureRestored",["poseRestored"]=controller==null||(controller.localPosition==oldPos&&controller.localRotation==oldRot),["driversRestored"]=drivers.Select((d,i)=>d==null||d.enabled==driverFlags[i]).All(v=>v)});Save(!brief&&sdkComplete&&errors.Count==0,reason);}
 public static string Stop(){Finish("Explicitobserverstop");return stage;}
}


