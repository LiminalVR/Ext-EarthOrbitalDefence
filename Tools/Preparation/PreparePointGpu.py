from pathlib import Path
import sys
p=Path(__file__).resolve().parents[2]
s=(p/'Tools/URPMigration/Native49PbrAffectedGpu.cs').read_text().replace('Native49PbrAffectedGpu','Native49PointGpu').replace('PbrAffectedGpu-','PointGpu-')
s=s.replace('new[]{"Assets/StarSparrow/Materials/StarSparrow Red.mat","Assets/ImportedAssets/AsteroidPack/Materials/Asteroid_01_S.mat"}','new[]{"Assets/Materials/Earth4kMaterial.mat"}')
# Use one existing actual material; match by source material name independent of path.
s=s.replace('new[]{"Assets/Materials/Earth4kMaterial.mat"}.Contains(r.Value<string>("path"))','r.Value<string>("name")=="Earth4kMaterial"')
needle='  var data=ScriptableObject.CreateInstance<UniversalRendererData>();'
s=s.replace(needle,'''  var health=Object.FindObjectsByType<Health>(FindObjectsInactive.Include).Single(h=>h.gameObject.scene==active);var effect=Object.Instantiate(health.explossionPrefab);effect.hideFlags=HideFlags.HideAndDontSave;SceneManager.MoveGameObjectToScene(effect,scene);effect.transform.position=new Vector3(0,0,-2);foreach(var t in effect.GetComponentsInChildren<Transform>(true))t.gameObject.layer=31;foreach(var r in effect.GetComponentsInChildren<Renderer>(true))r.forceRenderingOff=true;effect.SetActive(true);var systems=effect.GetComponentsInChildren<ParticleSystem>(true);foreach(var ps in systems){uint seed=ps.randomSeed;ps.useAutoRandomSeed=false;ps.randomSeed=seed;}
  var particleLights=systems.Where(ps=>ps.lights.enabled).ToArray();if(particleLights.Length!=1)throw new Exception("Exact one source Health effect light module required");var culls=new JArray();
'''+needle)
s=s.replace('r.forceRenderingOff=true;','r.forceRenderingOff=r.GetComponent<ParticleSystem>()==null||!r.GetComponent<ParticleSystem>().lights.enabled;')
s=s.replace('pipeline.msaaSampleCount=1;','pipeline.msaaSampleCount=1;if(pipeline.additionalLightsRenderingMode!=LightRenderingMode.PerPixel||pipeline.maxAdditionalLightsCount!=4)throw new Exception("Authentic installed default perPixel/4 required");var pipelineSerialized=new SerializedObject(pipeline);pipelineSerialized.FindProperty("m_LightFalloffMode").intValue=(int)LightFalloffMode.Linear;pipelineSerialized.ApplyModifiedPropertiesWithoutUndo();if(pipeline.lightFalloffMode!=LightFalloffMode.Linear)throw new Exception("Transient authentic linear falloff required");')
s=s.replace('GraphicsSettings.lightsUseColorTemperature=false;}};','''GraphicsSettings.lightsUseColorTemperature=false;if(cam.TryGetCullingParameters(out var parameters)){var cull=context.Cull(ref parameters);culls.Add(new JObject{["phase"]="insideBeginCamera",["lights"]=new JArray(cull.visibleLights.ToArray().Select(l=>new JObject{["type"]=l.lightType.ToString(),["range"]=l.range,["finalColor"]=C(l.finalColor),["matrix"]=l.localToWorldMatrix.ToString(),["objectName"]=l.light==null?null:l.light.name}))});}}};''')
s=s.replace('foreach(var mode in new[]{"Source00","Stock00","Candidate00","Candidate00Repeat"})','foreach(float effectTime in new[]{0.1f,0.5f,1.1f})foreach(var mode in new[]{"Source00","Candidate00"})')
s=s.replace('    var mat=mode==','    if(mode=="Source00"){reference=null;foreach(var ps in systems)ps.Simulate(effectTime,false,true,true);}\n    var mat=mode==')
s=s.replace('["mode"]=mode,','''["mode"]=mode,["effectTime"]=effectTime,["particleLights"]=new JArray(particleLights.Select(ps=>new JObject{["name"]=ps.name,["count"]=ps.particleCount,["alive"]=ps.IsAlive(),["rangeMultiplier"]=ps.lights.rangeMultiplier,["intensityMultiplier"]=ps.lights.intensityMultiplier})),["additionalKeyword"]=Shader.IsKeywordEnabled("_ADDITIONAL_LIGHTS"),["linearFalloffKeyword"]=Shader.IsKeywordEnabled("_LIGHT_FALLOFF_LINEAR"),''')
s=s.replace('Object.DestroyImmediate(candidate);','Object.DestroyImmediate(effect);Object.DestroyImmediate(candidate);')
if 'uniform' in sys.argv:
 s=s.replace('var candidate=ShaderUtil.CreateShaderAsset','var sourceVertexShader=ShaderUtil.CreateShaderAsset(File.ReadAllText("Tools/URPMigration/Source49VertexLightUniform.shader"),false);var sourceVertexUniforms=new JArray();var candidate=ShaderUtil.CreateShaderAsset')
 needle='   }finally{Object.DestroyImmediate(tex);}'
 insert='''   if(mode=="Source00"&&effectTime<1){var reader=new Material(mat){hideFlags=HideFlags.HideAndDontSave};reader.shader=sourceVertexShader;try{renderer.sharedMaterial=reader;for(int probe=0;probe<5;probe++){reader.SetFloat("_Probe",probe);ShaderUtil.CompilePass(reader,0,true);camera.Render();RenderTexture.active=rt;tex.ReadPixels(new Rect(0,0,256,256),0,0);tex.Apply();sourceVertexUniforms.Add(new JObject{["time"]=effectTime,["selector"]=probe,["value"]=C(tex.GetPixel(128,128))});}}finally{renderer.sharedMaterial=mat;Object.DestroyImmediate(reader);}}
'''
 s=s.replace(needle,insert+needle)
 s=s.replace('Object.DestroyImmediate(effect);Object.DestroyImmediate(candidate);','Object.DestroyImmediate(effect);Object.DestroyImmediate(sourceVertexShader);Object.DestroyImmediate(candidate);')
 s=s.replace('["visibleLightCulls"]=culls,','["sourceVertexUniforms"]=sourceVertexUniforms,["visibleLightCulls"]=culls,')
start=s.index('["limits"]=');end=s.index('}.ToString());',start)
s=s[:start]+'''["visibleLightCulls"]=culls,["limits"]="Supplementary actual Health.explossionPrefab clone, unchanged particle light modules/prefab properties, deterministic sampled native ParticleSystem.Simulate at0.1/0.5/1.1 seconds. Not a new ordinary Health.Damage/depletion route; prior63.78source loss reused. Effect renderers forceRenderingOff to isolate real native particle-light transport; any missing cull/light is a failed fixture, not lighting parity. Controlled Earth-source-material sphere under authored Starter directional clone/source0-0 vs own URP BRDF2/0-0. No game callbacks/health/timer writes/assets/settings changes. Actual Main/cold/player/lifecycle visual gates remain separate."'''+s[end:]
if 'uniform' in sys.argv:s=s.replace('["visibleLightCulls"]=culls,','["sourceVertexUniforms"]=sourceVertexUniforms,["visibleLightCulls"]=culls,')
if 'surface' in sys.argv:
 s=s.replace('r.Value<string>("name")=="Earth4kMaterial"','r.Value<string>("shader")=="Particles/Standard Surface"').replace('new[]{0.1f,0.5f,1.1f}','new[]{0.1f,1.1f}')
 s=s.replace('candidatePath+"/LitSourceProperties.shader"','JObject.Parse(File.ReadAllText("Tools/URPMigration/ParticleCandidates-current.json")).Value<string>("stage")+"/ParticlesLitSourceRGBA.shader"')
 s=s.replace('source.shader.name.StartsWith("Standard")','source.shader.name=="Particles/Standard Surface"')
 s=s.replace('mat.SetColor("_EmissionColor",source.GetColor("_EmissionColor"));','')
 s=s.replace('BaseShaderGUI.SetMaterialKeywords(mat,LitGUI.SetMaterialKeywords);','if(source.GetFloat("_Mode")==2)mat.SetFloat("_BlendModePreserveSpecular",0);BaseShaderGUI.SetMaterialKeywords(mat,LitGUI.SetMaterialKeywords,ParticleGUI.SetMaterialKeywords);')
 s=s.replace('Native49PointGpu','Native49PointSurfaceGpu').replace('PointGpu-','PointSurfaceGpu-')
 (p/'Tools/URPMigration/Native49PointSurfaceGpu.cs').write_text(s,encoding='utf-8');print(str(p/'Tools/URPMigration/Native49PointSurfaceGpu.cs'));raise SystemExit(0)
(p/'Tools/URPMigration/Native49PointGpu.cs').write_text(s,encoding='utf-8');print(str(p/'Tools/URPMigration/Native49PointGpu.cs'))
