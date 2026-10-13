using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using Object=UnityEngine.Object;
public static class Native49UniformGpu {
 public static string Capture(){
  if(EditorApplication.isPlayingOrWillChangePlaymode||GraphicsSettings.currentRenderPipeline!=null)throw new Exception("StoppedoriginalSourceRequired");
  var stage="Builds/URPMigration/UniformGpu-"+DateTime.UtcNow.ToString("yyyyMMddTHHmmssfffZ");Directory.CreateDirectory(stage);File.Copy("Tools/URPMigration/Native49UniformGpu.cs",stage+"/Native49UniformGpu.cs");
  var active=SceneManager.GetActiveScene();bool dirty=active.isDirty;var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Additive);SceneManager.SetActiveScene(active);
  var go=GameObject.CreatePrimitive(PrimitiveType.Quad);go.hideFlags=HideFlags.HideAndDontSave;go.layer=31;SceneManager.MoveGameObjectToScene(go,scene);var renderer=go.GetComponent<Renderer>();
  var originalLight=Object.FindObjectsByType<Light>(FindObjectsInactive.Include).Single(l=>l.gameObject.scene==active);var lightGo=new GameObject("49sourceUniformLight"){hideFlags=HideFlags.HideAndDontSave};SceneManager.MoveGameObjectToScene(lightGo,scene);var light=lightGo.AddComponent<Light>();EditorUtility.CopySerialized(originalLight,light);light.transform.rotation=originalLight.transform.rotation;light.cullingMask=1<<31;lightGo.SetActive(originalLight.gameObject.activeInHierarchy);
  var cgo=new GameObject("49SourceUniformCamera"){hideFlags=HideFlags.HideAndDontSave};SceneManager.MoveGameObjectToScene(cgo,scene);var c=cgo.AddComponent<Camera>();c.enabled=false;c.stereoTargetEye=StereoTargetEyeMask.None;c.cullingMask=1<<31;c.clearFlags=CameraClearFlags.SolidColor;c.backgroundColor=Color.clear;c.orthographic=true;c.orthographicSize=.6f;c.transform.position=new Vector3(0,0,-5);c.nearClipPlane=.1f;c.farClipPlane=20;c.allowHDR=true;
  var data=ScriptableObject.CreateInstance<UniversalRendererData>();data.hideFlags=HideFlags.HideAndDontSave;var pipeline=UniversalRenderPipelineAsset.Create(data);pipeline.hideFlags=HideFlags.HideAndDontSave;pipeline.msaaSampleCount=1;pipeline.supportsHDR=true;
  var oldD=GraphicsSettings.defaultRenderPipeline;var oldQ=QualitySettings.renderPipeline;var oldR=RenderTexture.active;var oldL=GraphicsSettings.lightsUseLinearIntensity;var oldT=GraphicsSettings.lightsUseColorTemperature;var rt=new RenderTexture(32,32,24,RenderTextureFormat.ARGBFloat);c.targetTexture=rt;var rows=new JArray();var shaders=new List<Shader>();var mats=new List<Material>();var errors=new JArray();Application.LogCallback log=(m,s,t)=>{if(t==LogType.Error||t==LogType.Exception||t==LogType.Assert)errors.Add(new JObject{["message"]=m,["stack"]=s});};Application.logMessageReceived+=log;
  try{foreach(string mode in new[]{"Source00","URP11","URP00"}){
   bool source=mode=="Source00";GraphicsSettings.defaultRenderPipeline=source?null:pipeline;QualitySettings.renderPipeline=source?null:pipeline;GraphicsSettings.lightsUseLinearIntensity=mode=="URP11";GraphicsSettings.lightsUseColorTemperature=mode=="URP11";
   light.color=originalLight.color;light.intensity=originalLight.intensity;light.colorTemperature=originalLight.colorTemperature;light.useColorTemperature=originalLight.useColorTemperature;
   foreach(var uniform in source?new[]{"_LightColor0","unity_SHAr","unity_SHBr","unity_IndirectSpecColor","unity_SpecCube0_HDR","selector"}:new[]{"_MainLightColor","unity_SHAr","unity_SHBr","_GlossyEnvironmentColor","unity_SpecCube0_HDR"}){
    var expr=uniform=="selector"?"float4(BRDF,0,0,1)":"float4("+uniform+".rgb,"+(uniform.Contains("SH")||uniform.Contains("HDR")?uniform+".a":"1")+")";
    string vertex=source?"UnityObjectToClipPos(v.xyz)":"TransformObjectToHClip(v.xyz)";
    string header=source?"#include \"UnityCG.cginc\"\n#include \"UnityPBSLighting.cginc\"\n#if defined(UNITY_PBS_USE_BRDF3)\n#define BRDF 3\n#elif defined(UNITY_PBS_USE_BRDF2)\n#define BRDF 2\n#elif defined(UNITY_PBS_USE_BRDF1)\n#define BRDF 1\n#else\n#define BRDF 0\n#endif":"#include \"Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl\"\n#include \"Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl\"";
    string shaderText="Shader \"Hidden/49/Uniform/"+mode+uniform+"\"{SubShader{Tags{"+(source?"":"\"RenderPipeline\"=\"UniversalPipeline\"")+"}Pass{Tags{\"LightMode\"=\""+(source?"ForwardBase":"UniversalForward")+"\"}Cull Off ZWrite Off "+(source?"CGPROGRAM":"HLSLPROGRAM")+"\n#pragma target 3.0\n#pragma vertex vert\n#pragma fragment frag\n"+header+"\nfloat4 vert(float4 v:POSITION):SV_POSITION{return "+vertex+";}float4 frag():SV_Target{return "+expr+";}\n"+(source?"ENDCG":"ENDHLSL")+"}}}";
    File.WriteAllText(stage+"/"+mode+uniform+".shader",shaderText);var shader=ShaderUtil.CreateShaderAsset(shaderText,false);shaders.Add(shader);var m=new Material(shader){hideFlags=HideFlags.HideAndDontSave};mats.Add(m);renderer.sharedMaterial=m;c.Render();RenderTexture.active=rt;var tex=new Texture2D(1,1,TextureFormat.RGBAFloat,false,true);try{tex.ReadPixels(new Rect(16,16,1,1),0,0);tex.Apply();var color=tex.GetPixel(0,0);rows.Add(new JObject{["mode"]=mode,["uniform"]=uniform,["rgba"]=new JArray(color.r,color.g,color.b,color.a),["linearFlag"]=GraphicsSettings.lightsUseLinearIntensity,["temperatureFlag"]=GraphicsSettings.lightsUseColorTemperature,["shaderErrors"]=new JArray(ShaderUtil.GetShaderMessages(shader).Where(e=>e.severity.ToString()=="Error").Select(e=>e.message))});}finally{Object.DestroyImmediate(tex);}
   }
  }}finally{QualitySettings.renderPipeline=oldQ;GraphicsSettings.defaultRenderPipeline=oldD;GraphicsSettings.lightsUseLinearIntensity=oldL;GraphicsSettings.lightsUseColorTemperature=oldT;RenderTexture.active=oldR;c.targetTexture=null;rt.Release();Object.DestroyImmediate(rt);foreach(var m in mats)Object.DestroyImmediate(m);foreach(var s in shaders)Object.DestroyImmediate(s);Object.DestroyImmediate(go);Object.DestroyImmediate(cgo);Object.DestroyImmediate(lightGo);Object.DestroyImmediate(pipeline);Object.DestroyImmediate(data);EditorSceneManager.CloseScene(scene,true);SceneManager.SetActiveScene(active);Application.logMessageReceived-=log;
   File.WriteAllText(stage+"/report.json",new JObject{["utc"]=DateTime.UtcNow.ToString("o"),["rows"]=rows,["errors"]=errors,["restores"]=new JObject{["pipeline"]=GraphicsSettings.defaultRenderPipeline==oldD&&QualitySettings.renderPipeline==oldQ,["lightFlags"]=GraphicsSettings.lightsUseLinearIntensity==oldL&&GraphicsSettings.lightsUseColorTemperature==oldT,["scene"]=SceneManager.GetActiveScene()==active&&active.isDirty==dirty,["activeRT"]=RenderTexture.active==oldR},["limits"]="Actual native per-draw32bitfloat uniforms on source scene ambient+authored cloned directional light. Source selectorprobe uses sameinstalledUnityPBSLighting/target3/tier path, not direct disassembly of originalStandard compiledvariant. Transient sequential warm URP flag/light refresh diagnostic; no coldproduction proof. No constant/color compensation."}.ToString());
  }return Path.GetFullPath(stage+"/report.json");
 }
}
