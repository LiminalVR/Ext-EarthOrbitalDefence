using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.Rendering;
using UnityEditor.Rendering.Universal.ShaderGUI;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using Object=UnityEngine.Object;
public static class Native49PbrGpu {
 static JArray C(Color c)=>new JArray(c.r,c.g,c.b,c.a);
 public static Material Transfer(Material source){
  var m=new Material(source){hideFlags=HideFlags.HideAndDontSave};var s=source.shader;
  var up=MaterialUpgrader.FetchAllUpgradersForPipeline(typeof(UniversalRenderPipelineAsset)).Where(u=>u.OldShaderPath==s.name&&!u.NewShaderPath.Contains("/2D/")).ToList();if(up.Count==0)throw new Exception("No installed3D upgrader "+s.name);
  if(s.name=="Autodesk Interactive"){
   var name=source.GetFloat("_Mode")==1?"AutodeskInteractiveMasked":source.GetFloat("_Mode")>=2?"AutodeskInteractiveTransparent":"AutodeskInteractive";
   m.shader=AssetDatabase.LoadAllAssetsAtPath("Packages/com.unity.render-pipelines.universal/Shaders/AutodeskInteractive/"+name+".shadergraph").OfType<Shader>().Single();up.Single().Convert(source,m);m.shaderKeywords=Array.Empty<string>();m.renderQueue=source.renderQueue;return m;
  }
  MaterialUpgrader.Upgrade(m,up,MaterialUpgrader.UpgradeFlags.None);
  bool std=s.name=="Standard"||s.name=="Standard (Specular setup)";
  if(std){bool map=source.IsKeywordEnabled("_SMOOTHNESS_TEXTURE_ALBEDO_CHANNEL_A")||source.IsKeywordEnabled(s.name=="Standard"?"_METALLICGLOSSMAP":"_SPECGLOSSMAP");m.SetFloat("_Smoothness",source.GetFloat(map?"_GlossMapScale":"_Glossiness"));}
  if(s.name=="Particles/Standard Surface")m.SetFloat("_Smoothness",source.GetFloat("_Glossiness"));
  foreach(string p in new[]{"_Metallic","_EmissionColor"}){int a=s.FindPropertyIndex(p),b=m.shader.FindPropertyIndex(p);if(a<0||b<0||QualitySettings.activeColorSpace!=ColorSpace.Linear)continue;var af=s.GetPropertyFlags(a);var bf=m.shader.GetPropertyFlags(b);if(s.GetPropertyType(a)==ShaderPropertyType.Color&&(af&ShaderPropertyFlags.HDR)==0&&(bf&ShaderPropertyFlags.HDR)!=0)m.SetColor(p,source.GetColor(p).linear);else if((af&ShaderPropertyFlags.Gamma)!=0&&(bf&ShaderPropertyFlags.Gamma)==0)m.SetFloat(p,Mathf.GammaToLinearSpace(source.GetFloat(p)));}
  if(std&&source.GetFloat("_Mode")==2){m.SetFloat("_Blend",0);m.SetFloat("_BlendModePreserveSpecular",0);}
  if(std&&source.GetFloat("_Mode")==3){m.SetFloat("_Blend",0);m.SetFloat("_BlendModePreserveSpecular",1);}
  bool emission=source.IsKeywordEnabled("_EMISSION");if(emission)m.globalIlluminationFlags|=(MaterialGlobalIlluminationFlags)8;else m.globalIlluminationFlags&=~(MaterialGlobalIlluminationFlags)8;
  if(s.name.StartsWith("Particles/"))BaseShaderGUI.SetMaterialKeywords(m,s.name=="Particles/Standard Surface"?LitGUI.SetMaterialKeywords:null,ParticleGUI.SetMaterialKeywords);else BaseShaderGUI.SetMaterialKeywords(m,LitGUI.SetMaterialKeywords);
  if(emission)m.EnableKeyword("_EMISSION");else m.DisableKeyword("_EMISSION");
  if(source.HasProperty("_SrcBlend")&&m.HasProperty("_SrcBlendAlpha"))m.SetFloat("_SrcBlendAlpha",source.GetFloat("_SrcBlend"));if(source.HasProperty("_DstBlend")&&m.HasProperty("_DstBlendAlpha"))m.SetFloat("_DstBlendAlpha",source.GetFloat("_DstBlend"));
  m.renderQueue=source.renderQueue;return m;
 }
 public static string Capture(){
  if(EditorApplication.isPlayingOrWillChangePlaymode||EditorApplication.isCompiling||GraphicsSettings.currentRenderPipeline!=null)throw new Exception("StoppedoriginalBuiltInrequired");
  var stage="Builds/URPMigration/PbrGpu-"+DateTime.UtcNow.ToString("yyyyMMddTHHmmssfffZ");Directory.CreateDirectory(stage);File.Copy("Tools/URPMigration/Native49PbrGpu.cs",stage+"/Native49PbrGpu.cs");
  var plan=JObject.Parse(File.ReadAllText("Tools/URPMigration/TransportPlan-current.json")).Value<string>("stage");var doc=JObject.Parse(File.ReadAllText(plan+"/plan.json"));
  var entries=((JArray)doc["materialIdentities"]).OfType<JObject>().Where(r=>new[]{"Standard","Standard (Specular setup)","Particles/Standard Surface"}.Contains(r.Value<string>("shader"))).ToArray();
  var active=SceneManager.GetActiveScene();var dirty=active.isDirty;var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Additive);SceneManager.SetActiveScene(active);
  var go=GameObject.CreatePrimitive(PrimitiveType.Sphere);go.hideFlags=HideFlags.HideAndDontSave;go.layer=31;SceneManager.MoveGameObjectToScene(go,scene);var renderer=go.GetComponent<Renderer>();
  var cameraGo=new GameObject("49stocktypedGPUcamera"){hideFlags=HideFlags.HideAndDontSave};SceneManager.MoveGameObjectToScene(cameraGo,scene);var camera=cameraGo.AddComponent<Camera>();camera.enabled=false;camera.stereoTargetEye=StereoTargetEyeMask.None;camera.cullingMask=1<<31;camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.08f,.12f,.16f,.6f);camera.orthographic=true;camera.orthographicSize=.65f;camera.transform.position=new Vector3(0,0,-5);camera.nearClipPlane=.1f;camera.farClipPlane=20;camera.allowHDR=true;
  var originalLight=Object.FindObjectsByType<Light>(FindObjectsInactive.Include).Single(l=>l.gameObject.scene==active);var lightGo=new GameObject("49sourceDirectionalClone"){hideFlags=HideFlags.HideAndDontSave};SceneManager.MoveGameObjectToScene(lightGo,scene);var light=lightGo.AddComponent<Light>();EditorUtility.CopySerialized(originalLight,light);light.transform.rotation=originalLight.transform.rotation;light.cullingMask=1<<31;lightGo.SetActive(originalLight.gameObject.activeInHierarchy);
  var data=ScriptableObject.CreateInstance<UniversalRendererData>();data.hideFlags=HideFlags.HideAndDontSave;data.renderingMode=RenderingMode.Forward;var pipeline=UniversalRenderPipelineAsset.Create(data);pipeline.hideFlags=HideFlags.HideAndDontSave;pipeline.msaaSampleCount=1;pipeline.supportsHDR=true;pipeline.supportsCameraDepthTexture=true;
  var oldD=GraphicsSettings.defaultRenderPipeline;var oldQ=QualitySettings.renderPipeline;var oldR=RenderTexture.active;var oldL=GraphicsSettings.lightsUseLinearIntensity;var oldT=GraphicsSettings.lightsUseColorTemperature;var rt=new RenderTexture(256,256,24,RenderTextureFormat.ARGBFloat);camera.targetTexture=rt;var rows=new JArray();var errors=new JArray();var mats=new List<Material>();var candidatePath=JObject.Parse(File.ReadAllText("Tools/URPMigration/PbrCandidate-current.json")).Value<string>("stage");var candidate=ShaderUtil.CreateShaderAsset(File.ReadAllText(candidatePath+"/LitSourceProperties.shader"),false);
  Application.LogCallback log=(m,s,t)=>{if(t==LogType.Error||t==LogType.Exception||t==LogType.Assert)errors.Add(new JObject{["message"]=m,["stack"]=s});};Application.logMessageReceived+=log;
  try{foreach(var entry in entries){var source=AssetDatabase.LoadAllAssetsAtPath(entry.Value<string>("path")).OfType<Material>().Single(m=>{string g;long id;AssetDatabase.TryGetGUIDAndLocalFileIdentifier(m,out g,out id);return g==entry.Value<string>("guid")&&id==entry.Value<long>("localID");});Color[] reference=null;
   foreach(var mode in new[]{"Source00","Stock00","Candidate00"}){
    var mat=mode=="Source00"?new Material(source){hideFlags=HideFlags.HideAndDontSave}:Transfer(source);if(mode=="Candidate00"&&source.shader.name.StartsWith("Standard")){
     int queue=mat.renderQueue;mat.shader=candidate;if(source.HasProperty("_Metallic"))mat.SetFloat("_Metallic",source.GetFloat("_Metallic"));mat.SetColor("_EmissionColor",source.GetColor("_EmissionColor"));mat.SetTexture("_MainTex",source.GetTexture("_MainTex"));mat.SetTextureScale("_MainTex",source.GetTextureScale("_MainTex"));mat.SetTextureOffset("_MainTex",source.GetTextureOffset("_MainTex"));mat.SetColor("_Color",source.GetColor("_Color"));BaseShaderGUI.SetMaterialKeywords(mat,LitGUI.SetMaterialKeywords);if(source.IsKeywordEnabled("_EMISSION"))mat.EnableKeyword("_EMISSION");mat.renderQueue=queue;
    }mats.Add(mat);renderer.sharedMaterial=mat;GraphicsSettings.defaultRenderPipeline=mode=="Source00"?null:pipeline;QualitySettings.renderPipeline=mode=="Source00"?null:pipeline;GraphicsSettings.lightsUseLinearIntensity=mode=="Stock11";GraphicsSettings.lightsUseColorTemperature=mode=="Stock11";
    camera.Render();RenderTexture.active=rt;var tex=new Texture2D(256,256,TextureFormat.RGBAFloat,false,true);try{tex.ReadPixels(new Rect(0,0,256,256),0,0);tex.Apply();var colors=tex.GetPixels();var file=stage+"/"+rows.Count.ToString("D3")+"-"+mode+".png";var png=new Texture2D(256,256,TextureFormat.RGBA32,false,false);try{png.SetPixels(colors);png.Apply();File.WriteAllBytes(file,png.EncodeToPNG());}finally{Object.DestroyImmediate(png);}
     var row=new JObject{["sourceIdentity"]=entry,["mode"]=mode,["image"]=Path.GetFullPath(file),["materialJSON"]=EditorJsonUtility.ToJson(mat),["linearFlag"]=GraphicsSettings.lightsUseLinearIntensity,["temperatureFlag"]=GraphicsSettings.lightsUseColorTemperature,["effectiveQueue"]=mat.renderQueue,["sampleRGBA"]=new JArray(new[]{new Vector2Int(80,80),new Vector2Int(176,80),new Vector2Int(80,176),new Vector2Int(176,176),new Vector2Int(128,128)}.Select(v=>new JObject{["xy"]=new JArray(v.x,v.y),["rgba"]=C(tex.GetPixel(v.x,v.y))})),["shaderMessages"]=new JArray(ShaderUtil.GetShaderMessages(mat.shader).Select(e=>new JObject{["severity"]=e.severity.ToString(),["message"]=e.message})),["maxRGBReadback"]=colors.Max(v=>Math.Max(v.r,Math.Max(v.g,v.b)))};
     if(reference==null)reference=colors;else{double total=0;float max=0,alpha=0;for(int i=0;i<colors.Length;i++){var d=Math.Max(Math.Abs(colors[i].r-reference[i].r),Math.Max(Math.Abs(colors[i].g-reference[i].g),Math.Abs(colors[i].b-reference[i].b)));total+=d;max=Math.Max(max,d);alpha=Math.Max(alpha,Math.Abs(colors[i].a-reference[i].a));}row["meanRGB"]=total/colors.Length;row["maxRGB"]=max;row["maxAlpha"]=alpha;}rows.Add(row);
    }finally{Object.DestroyImmediate(tex);}
   }
  }}finally{QualitySettings.renderPipeline=oldQ;GraphicsSettings.defaultRenderPipeline=oldD;GraphicsSettings.lightsUseLinearIntensity=oldL;GraphicsSettings.lightsUseColorTemperature=oldT;RenderTexture.active=oldR;camera.targetTexture=null;rt.Release();Object.DestroyImmediate(rt);foreach(var m in mats)Object.DestroyImmediate(m);Object.DestroyImmediate(candidate);Object.DestroyImmediate(go);Object.DestroyImmediate(cameraGo);Object.DestroyImmediate(lightGo);Object.DestroyImmediate(pipeline);Object.DestroyImmediate(data);EditorSceneManager.CloseScene(scene,true);SceneManager.SetActiveScene(active);Application.logMessageReceived-=log;
   File.WriteAllText(stage+"/report.json",new JObject{["utc"]=DateTime.UtcNow.ToString("o"),["pid"]=System.Diagnostics.Process.GetCurrentProcess().Id,["rows"]=rows,["errors"]=errors,["restores"]=new JObject{["defaultPipeline"]=GraphicsSettings.defaultRenderPipeline==oldD,["qualityPipeline"]=QualitySettings.renderPipeline==oldQ,["activeScene"]=SceneManager.GetActiveScene()==active,["sceneDirty"]=active.isDirty==dirty,["activeRT"]=RenderTexture.active==oldR,["lightFlags"]=GraphicsSettings.lightsUseLinearIntensity==oldL&&GraphicsSettings.lightsUseColorTemperature==oldT},["limits"]="Shortsupplementary sphere/actualtypedmaterial textures/source-directionalclone with own scene ambient. TrueSource0/0 versusstock1/1 andstock0/0; no sourceBRDF/reflectionfallback inferred. Warm sequential transient pipeline proof, not coldproduction/scene/gameplay/particlegeometry acceptance. Source artifacts/assets/settings/metas untouched. Candidate sourceBRDF2/normal/mixedSH plus ownAsteroidparallax; sourceReflectionOFF unbound draft unused current14Standard/spec. No coldproduction acceptance."}.ToString());
  }
  return Path.GetFullPath(stage+"/report.json");
 }
}
