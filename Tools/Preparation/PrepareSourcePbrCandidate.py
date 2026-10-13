from pathlib import Path
from datetime import datetime,timezone
import json,hashlib
p=Path(__file__).resolve().parents[2]
old=Path('D:/Work/Liminal/Platform/Upgrade/Limapps/167_Accelerate/Builds/URPMigration/FinalProduction-20261003T001958508199Z')
src=old/'Production/Assets/Shaders/Source167/Stock'
out=p/'Builds/URPMigration'/('PbrCandidate-'+datetime.now(timezone.utc).strftime('%Y%m%dT%H%M%S%fZ'));out.mkdir()
rows=[]
names=['LitSourceProperties.shader','Source167StockForward.hlsl','Source167PBRCommon.hlsl','Source167NativeBRDF2.hlsl','SourceStandardDirectBRDF.hlsl']
for name in names:
 text=(src/name).read_text(encoding='utf-8-sig')
 text=text.replace('Source167','Source49').replace('Liminal/167/','Liminal/49/').replace('_AccelerateSourceIndirectSpecColor','_EarthOrbitalSourceIndirectSpecColor')
 text=text.replace('Assets/Shaders/Source49/Stock',out.as_posix())
 if name=='Source167PBRCommon.hlsl':
  begin=text.index('  #if defined(UNITY_LIGHTMAP_DLDR_ENCODING)')
  end=text.index('  #endif',begin)+len('  #endif')
  text=text[:begin]+text[end:]
  text=text.replace('float4 lightmapUV:TEXCOORD7;','float4 lightmapUV:TEXCOORD7;half3 parallaxViewDirTS:TEXCOORD8;')
  needle='o.eyeVec=half4(o.positionWS-_WorldSpaceCameraPos.xyz,o.positionWS.y);return o;'
  assert needle in text
  replacement='o.eyeVec=half4(o.positionWS-_WorldSpaceCameraPos.xyz,o.positionWS.y);half3 binormal=cross(v.normalOS,v.tangentOS.xyz)*v.tangentOS.w;half3x3 rotation=half3x3(v.tangentOS.xyz,binormal,v.normalOS);o.parallaxViewDirTS=mul(rotation,TransformWorldToObject(_WorldSpaceCameraPos.xyz)-v.positionOS.xyz);return o;'
  text=text.replace(needle,replacement)
 if name=='Source167StockForward.hlsl':
  needle=' SurfaceData surface;InitializeStandardLitSurfaceData(uv,surface);'
  assert needle in text
  replace='''
 #if defined(_PARALLAXMAP)
 // Installed source UnityStandardInput.Parallax samples green then the source one-step offset.
 half h=SAMPLE_TEXTURE2D(_ParallaxMap,sampler_ParallaxMap,uv).g;
 h=h*_Parallax-_Parallax/2.0;
 half3 v=normalize(i.parallaxViewDirTS);v.z+=0.42;
 uv+=h*(v.xy/v.z);
 #endif
 SurfaceData surface;InitializeStandardLitSurfaceData(uv,surface);'''
  text=text.replace(needle,replace)
 dest=out/name.replace('Source167','Source49');dest.write_text(text,encoding='utf-8')
 rows.append({'originalPath':str(src/name),'originalSHA256':hashlib.sha256((src/name).read_bytes()).hexdigest(),'candidate':str(dest),'candidateSHA256':hashlib.sha256(dest.read_bytes()).hexdigest()})
doc={'utc':datetime.now(timezone.utc).isoformat(),'candidateOnly':True,'source167Manifest':str(old/'manifest.json'),'source167ManifestSHA256':hashlib.sha256((old/'manifest.json').read_bytes()).hexdigest(),'rows':rows,'changes':['Owned49name/prefix/include locators only except narrow49parallax adaptation and removal167DLDR4.59scaling.','Source UnityStandard tangent object-space view pervertex and green-channel one-step parallax for reachable Asteroid keyword; actual owned mesh check pending.','No source167bakedDC,sourceSun,sceneconstant,profile or lightflag provider copied.'],'limits':['Native source selectorprobe BRDF2 authenticated separately; originalStandard compiledvariant disassembly notclaimed.','All14Standard/spec identities have reflections enabled; _EarthOrbitalSourceIndirectSpecColor declared unbound draft and reflection-off branch notaccepted.','Lightmap transport/coldfirstdraw/fog/XR/instance proof must use49actualcontext.','No assets/meta/material/settings/source mutations; outsideAssets draft only.']}
(out/'manifest.json').write_text(json.dumps(doc,indent=2));(p/'Tools/URPMigration/PbrCandidate-current.json').write_text(json.dumps({'stage':str(out)},indent=2))
helper=(p/'Tools/URPMigration/Native49StockGpu.cs').read_text().replace('Native49StockGpu','Native49PbrGpu').replace('StockGpu-','PbrGpu-')
helper=helper.replace('"Autodesk Interactive","Particles/Standard Surface"','"Particles/Standard Surface"')
helper=helper.replace('var mats=new List<Material>();','var mats=new List<Material>();var candidatePath=JObject.Parse(File.ReadAllText("Tools/URPMigration/PbrCandidate-current.json")).Value<string>("stage");var candidate=ShaderUtil.CreateShaderAsset(File.ReadAllText(candidatePath+"/LitSourceProperties.shader"),false);')
helper=helper.replace('"Source00","Stock11","Stock00"','"Source00","Stock00","Candidate00"')
needle='mats.Add(mat);renderer.sharedMaterial=mat;'
assert needle in helper
helper=helper.replace(needle,'''if(mode=="Candidate00"&&source.shader.name.StartsWith("Standard")){
     int queue=mat.renderQueue;mat.shader=candidate;mat.SetFloat("_Metallic",source.GetFloat("_Metallic"));mat.SetColor("_EmissionColor",source.GetColor("_EmissionColor"));mat.SetTexture("_MainTex",source.GetTexture("_MainTex"));mat.SetTextureScale("_MainTex",source.GetTextureScale("_MainTex"));mat.SetTextureOffset("_MainTex",source.GetTextureOffset("_MainTex"));mat.SetColor("_Color",source.GetColor("_Color"));BaseShaderGUI.SetMaterialKeywords(mat,LitGUI.SetMaterialKeywords);if(source.IsKeywordEnabled("_EMISSION"))mat.EnableKeyword("_EMISSION");mat.renderQueue=queue;
    }mats.Add(mat);renderer.sharedMaterial=mat;''')
helper=helper.replace('Object.DestroyImmediate(go);','Object.DestroyImmediate(candidate);Object.DestroyImmediate(go);')
helper=helper.replace('["maxRGBReadback"]=','["shaderMessages"]=new JArray(ShaderUtil.GetShaderMessages(mat.shader).Select(e=>new JObject{["severity"]=e.severity.ToString(),["message"]=e.message})),["maxRGBReadback"]=')
helper=helper.replace('Source artifacts/assets/settings/metas untouched.','Source artifacts/assets/settings/metas untouched. Candidate sourceBRDF2/normal/mixedSH plus ownAsteroidparallax; sourceReflectionOFF unbound draft unused current14Standard/spec. No coldproduction acceptance.')
(p/'Tools/URPMigration/Native49PbrGpu.cs').write_text(helper)
print(json.dumps({'manifest':str(out/'manifest.json'),'sha256':hashlib.sha256((out/'manifest.json').read_bytes()).hexdigest(),'helper':str(p/'Tools/URPMigration/Native49PbrGpu.cs')},indent=2))
