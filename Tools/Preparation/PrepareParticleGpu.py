from pathlib import Path
import sys
p=Path(__file__).resolve().parents[2]
s=(p/'Tools/URPMigration/Native49PbrAffectedGpu.cs').read_text().replace('Native49PbrAffectedGpu','Native49ParticleGpu').replace('PbrAffectedGpu-','ParticleGpu-')
start=s.index('  var plan=');end=s.index('  var active=',start)
s=s[:start]+'''  var scope=Directory.GetDirectories("Builds/URPMigration","ReachableScope-*").OrderBy(x=>x).Last()+"/report.json";var scopeDoc=JObject.Parse(File.ReadAllText(scope));
  var entries=scopeDoc.Descendants().OfType<JObject>().Where(m=>m["nativeJSON"]!=null&&m.Value<string>("shader")!="Skybox/6 Sided"&&!m.Value<string>("shader").StartsWith("Standard")).GroupBy(m=>m.Value<string>("guid")+"/"+m.Value<long>("localID")).Select(g=>g.First()).ToArray();
'''+s[end:]
s=s.replace('GameObject.CreatePrimitive(PrimitiveType.Sphere)','GameObject.CreatePrimitive(PrimitiveType.Quad)')
needle='var renderer=go.GetComponent<Renderer>();'
s=s.replace(needle,needle+'''var mesh=Object.Instantiate(go.GetComponent<MeshFilter>().sharedMesh);mesh.hideFlags=HideFlags.HideAndDontSave;mesh.SetUVs(0,mesh.uv.Select(uv=>new Vector4(uv.x,uv.y,uv.x,uv.y)).ToList());mesh.colors=Enumerable.Repeat(new Color(0.65f,0.8f,0.4f,0.6f),mesh.vertexCount).ToArray();go.GetComponent<MeshFilter>().sharedMesh=mesh;''')
s=s.replace('pipeline.supportsCameraDepthTexture=true;','pipeline.supportsCameraDepthTexture=true;pipeline.supportsCameraOpaqueTexture=true;')
start=s.index('var candidatePath=');end=s.index('  Application.LogCallback',start)
s=s[:start]+'''var particlePath=JObject.Parse(File.ReadAllText("Tools/URPMigration/ParticleCandidates-current.json")).Value<string>("stage");var candidates=new Dictionary<string,Shader>();
  foreach(var pair in new[]{new[]{"Legacy Shaders/Particles/Additive","LegacyParticleAdditive.shader"},new[]{"Legacy Shaders/Particles/Alpha Blended","LegacyParticleAlpha.shader"},new[]{"Legacy Shaders/Particles/Alpha Blended Premultiply","LegacyParticlePremultiply.shader"},new[]{"Particles/Standard Unlit","ParticlesStandardUnlitRGB.shader"},new[]{"Particles/Standard Surface","ParticlesLitSourceRGBA.shader"}}){var shader=ShaderUtil.CreateShaderAsset(File.ReadAllText(particlePath+"/"+pair[1]),false);shader.hideFlags=HideFlags.HideAndDontSave;candidates.Add(pair[0],shader);}
'''+s[end:]
start=s.index('   foreach(var mode');end=s.index('mats.Add(mat);',start)
s=s[:start]+'''   foreach(var distance in new[]{2.5f,1.5f})foreach(var mode in new[]{"Source00","Candidate00"}){
    if(mode=="Source00")reference=null;camera.transform.position=new Vector3(0,0,-distance);
    Material mat;if(mode=="Source00"||!candidates.ContainsKey(source.shader.name))mat=new Material(source){hideFlags=HideFlags.HideAndDontSave};else if(source.shader.name.StartsWith("Legacy")){mat=new Material(source){hideFlags=HideFlags.HideAndDontSave};int queue=source.renderQueue;mat.shader=candidates[source.shader.name];mat.renderQueue=queue;}else{mat=Transfer(source);int queue=source.renderQueue;mat.shader=candidates[source.shader.name];if(source.HasProperty("_Metallic")&&mat.HasProperty("_Metallic"))mat.SetFloat("_Metallic",source.GetFloat("_Metallic"));if(source.shader.name=="Particles/Standard Surface"&&source.GetFloat("_Mode")==2){mat.SetFloat("_BlendModePreserveSpecular",0);BaseShaderGUI.SetMaterialKeywords(mat,LitGUI.SetMaterialKeywords,ParticleGUI.SetMaterialKeywords);}mat.renderQueue=queue;}
    '''+s[end:]
s=s.replace('ShaderUtil.CompilePass(mat,0,true);','for(int pass=0;pass<mat.passCount;pass++)ShaderUtil.CompilePass(mat,pass,true);')
s=s.replace('["mode"]=mode,','["mode"]=mode,["distance"]=distance,["candidateShader"]=mat.shader.name,')
s=s.replace('Object.DestroyImmediate(candidate);','foreach(var shader in candidates.Values)Object.DestroyImmediate(shader);Object.DestroyImmediate(mesh);')
start=s.index('["limits"]=');end=s.index('}.ToString());',start)
s=s[:start]+'''["sourceScope"]=scope,["candidateManifest"]=particlePath+"/manifest.json",["limits"]="Short supplementary controlled actual-material textured quad draws with explicit test vertexColor (.65,.8,.4,.6), UV1/UV2 agreement and two camera distances. Source scene authored directional clone/SH/default reflection/0-0 flags. No actual particle lifecycle/wholeMain/cold production acceptance. Source shaders retained for SDK Reticles/Sprites/UnlitColor compatibility test. Distortion background is a flat clear fixture; opaque texture excludes prior transparent effects. No source settings/material/assets/scene writes. MSAA is1; affected cutout/MSAA follow-up separate."'''+s[end:]
if 'surface' in sys.argv:
 s=s.replace('foreach(var entry in entries)','foreach(var entry in entries.Where(e=>e.Value<string>("shader")=="Particles/Standard Surface"))')
(p/'Tools/URPMigration/Native49ParticleGpu.cs').write_text(s,encoding='utf-8')
print(str(p/'Tools/URPMigration/Native49ParticleGpu.cs'))
