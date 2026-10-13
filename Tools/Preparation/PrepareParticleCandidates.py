from pathlib import Path
from datetime import datetime,timezone
import json,hashlib
p=Path(__file__).resolve().parents[2]
out=p/'Builds/URPMigration'/('ParticleCandidates-'+datetime.now(timezone.utc).strftime('%Y%m%dT%H%M%S%fZ'));out.mkdir()
old=Path('D:/Work/Liminal/Platform/Upgrade/Limapps/168_Big_Top_Blast/Carnival/Assets/Shaders/Source168')
builtin=Path('D:/Work/Liminal/Platform/Upgrade/Tools/UnityBuiltin6000.7.0b2/Includes/DefaultResourcesExtra')
pbr=Path(json.loads((p/'Tools/URPMigration/PbrCandidate-current.json').read_text())['stage'])
rows=[]
def sha(x):return hashlib.sha256(x.read_bytes()).hexdigest()
def write(name,text,source,semantics):
 f=out/name;f.write_text(text,encoding='utf-8');rows.append({'candidate':str(f),'candidateSHA256':sha(f),'sourcePath':str(source),'sourceSHA256':sha(source),'changes':semantics})
base=(old/'LegacyParticlePremultiply.shader').read_text()
write('LegacyParticlePremultiply.shader',base.replace('/168/','/49/'),old/'LegacyParticlePremultiply.shader',['Owned name only; inherited source math/states require49 native GPU/Android checks.'])
for name,blend,formula in [('LegacyParticleAdditive.shader','SrcAlpha One','half4 col=2.0*i.color*_TintColor*SAMPLE_TEXTURE2D(_MainTex,sampler_MainTex,i.uv);col.a=saturate(col.a);return col;'),('LegacyParticleAlpha.shader','SrcAlpha OneMinusSrcAlpha','half4 col=2.0*i.color*SAMPLE_TEXTURE2D(_MainTex,sampler_MainTex,i.uv);col.a=saturate(col.a);return col;')]:
 s=base.replace('LegacyParticlePremultiply',name.removesuffix('.shader')).replace('/168/','/49/').replace('Blend One OneMinusSrcAlpha','Blend '+blend)
 s=s.replace('Properties {','Properties { _TintColor("Tint Color",Color)=(0.5,0.5,0.5,0.5)')
 s=s.replace('float4 _MainTex_ST;float _InvFade;','float4 _MainTex_ST;float _InvFade;half4 _TintColor;')
 if 'Alpha' in name:s=s.replace('o.color=v.color;','o.color=v.color*_TintColor;')
 s=s.replace('return i.color*SAMPLE_TEXTURE2D(_MainTex,sampler_MainTex,i.uv)*i.color.a;',formula)
 write(name,s,builtin/('Particle Add.shader' if 'Additive' in name else 'Particle Alpha Blend.shader'),['Source factor2/tint placement, saturate alpha, exact shared RGB blend/Depth/Cull. Authored49fog is disabled; runtime fog changes not supported by this bounded draft.'])
for name in ['ParticlesStandardUnlitRGB.shader','ParticlesLitSourceRGBA.shader']:
 s=(old/name).read_text().replace('/168/','/49/').replace('Source168','Source49')
 s=s.replace('clip(alpha - (cutoff + offset));','clip(alpha - (cutoff + offset) + 0.0001);')
 s=s.replace('HLSLPROGRAM','HLSLPROGRAM\n#if defined(SHADER_API_D3D11)\n#define PREFER_HALF 0\n#endif')
 forward='ParticlesUnlitForwardPass.hlsl' if 'Unlit' in name else 'ParticlesLitForwardPass.hlsl'
 original=p/'Library/PackageCache/com.unity.render-pipelines.universal@df9561ad1d75/Shaders/Particles'/forward
 h=original.read_text()
 h=h.replace('#include "ParticlesUnlitInput.hlsl"','#include "Packages/com.unity.render-pipelines.universal/Shaders/Particles/ParticlesUnlitInput.hlsl"')
 needle='    InitializeInputData(input, surfaceData' if 'Unlit' in name else '    InputData inputData;'
 # Source emission is also faded, independently of albedo alpha.
 fading='''#if defined(_FADING_ON)
    surfaceData.emission *= CameraFade(CAMERA_NEAR_FADE,CAMERA_INV_FADE_DISTANCE,particleParams.projectedPosition);
#endif
#if defined(_SOFTPARTICLES_ON)
    surfaceData.emission *= SoftParticles(SOFT_PARTICLE_NEAR_FADE,SOFT_PARTICLE_INV_FADE_DISTANCE,particleParams);
#endif
'''
 pos=h.index(needle);h=h[:pos]+fading+h[pos:]
 if 'Unlit' not in name:
  s=s.replace('#pragma multi_compile_fragment _ _ADDITIONAL_LIGHTS','#pragma multi_compile _ _ADDITIONAL_LIGHTS')
  pos=h.index('    output.positionWS.xyz = vertexInput.positionWS;');h=h[:pos]+'''    output.vertexSH=SHEvalLinearL2(normalInput.normalWS,unity_SHBr,unity_SHBg,unity_SHBb,unity_SHC);
#if defined(_ADDITIONAL_LIGHTS)
    // Original Standard Surface is noforwardadd: additional point diffuse travels per vertex.
    uint pointCount=min(GetAdditionalLightsCount(),4u);
    for(uint pointIndex=0;pointIndex<pointCount;++pointIndex){
        Light lamp=GetAdditionalLight(pointIndex,vertexInput.positionWS);
        uint nativeIndex=GetPerObjectLightIndex(pointIndex);
        #if USE_STRUCTURED_BUFFER_FOR_LIGHT_DATA
        float3 delta=_AdditionalLightsBuffer[nativeIndex].position.xyz-vertexInput.positionWS;
        float inverseRangeSqr=_AdditionalLightsBuffer[nativeIndex].attenuation.x;
        #else
        float3 delta=_AdditionalLightsPosition[nativeIndex].xyz-vertexInput.positionWS;
        float inverseRangeSqr=_AdditionalLightsAttenuation[nativeIndex].x;
        #endif
        // Native49source uniform readback gives unity_4LightAtten0.x=.01 for actual range50:
        // 25/2500. Match Shade4PointLights reciprocal, without URP range-edge fade.
        float vertexAtten=rcp(1.0+max(dot(delta,delta),0.000001)*25.0*inverseRangeSqr);
        output.vertexSH+=lamp.color*vertexAtten*saturate(dot(normalInput.normalWS,lamp.direction));
    }
#endif
'''+h[pos:]
  start=h.index('    URP_LIGHT_ACCUM4 color = UniversalFragmentPBR');end=h.index('    color.rgb =',start)
  repl='''    Source49Surface source=(Source49Surface)0;
    source.Albedo=surfaceData.albedo;source.Normal=half3(0,0,1);source.Emission=surfaceData.emission;source.Metallic=surfaceData.metallic;source.Smoothness=surfaceData.smoothness;source.Occlusion=surfaceData.occlusion;source.Alpha=surfaceData.alpha;
    Source49Varyings native=(Source49Varyings)0;
    native.positionWS=inputData.positionWS;native.positionCS=input.clipPos;native.normalWS=inputData.normalWS;
    native.tangentWS=half4(normalize(cross(abs(inputData.normalWS.y)<0.99?half3(0,1,0):half3(1,0,0),inputData.normalWS)),1);
    native.eyeVec=half4(-inputData.viewDirectionWS,0);native.vertexSH=input.vertexSH;
    URP_LIGHT_ACCUM4 color=half4(Source49Evaluate(source,native),surfaceData.alpha);
'''
  h=h[:start]+repl+h[end:]
  common=(pbr/'Source49PBRCommon.hlsl').read_text().replace('#if defined(_ADDITIONAL_LIGHTS)','#if defined(_ADDITIONAL_LIGHTS) && !defined(SOURCE49_PARTICLE_NO_FORWARDADD)')
  owncommon=out/'Source49ParticlePBRCommon.hlsl';owncommon.write_text(common,encoding='utf-8')
  rows.append({'candidate':str(owncommon),'candidateSHA256':sha(owncommon),'sourcePath':str(pbr/'Source49PBRCommon.hlsl'),'sourceSHA256':sha(pbr/'Source49PBRCommon.hlsl'),'changes':['Disable additional fragment loop only for original source noforwardadd Surface; main/BRDF2/GI/reflection untouched.']})
  h=h.replace('#include "ParticlesLitInput.hlsl"','#include "Packages/com.unity.render-pipelines.universal/Shaders/Particles/ParticlesLitInput.hlsl"\n#define SOURCE49_PARTICLE_NO_FORWARDADD 1\n#include "'+owncommon.as_posix()+'"')
 hpath=out/('Source49'+forward);hpath.write_text(h,encoding='utf-8')
 rows.append({'candidate':str(hpath),'candidateSHA256':sha(hpath),'sourcePath':str(original),'sourceSHA256':sha(original),'changes':['Own source emission fade and Lit-only installed-source BRDF2/mixed per-vertex L2. No material/art scalar changes.']})
 s=s.replace('"Packages/com.unity.render-pipelines.universal/Shaders/Particles/'+forward+'"','"'+hpath.as_posix()+'"')
 write(name,s,old/name,['Source hard clip +0.0001 threshold, RGB-only StandardUnlit and sharedRGBA Surface blends, AlphaToMaskOff; own49nativeBRDF2 for Surface and emission fade; no GUI normalization.'])
manifest={'utc':datetime.now(timezone.utc).isoformat(),'candidateOnly':True,'rows':rows,'pbrManifest':str(pbr/'manifest.json'),'limits':['No production Assets/import/settings/material mutations.','Source scene fog off, no lightmaps/additional active points; no generalized acceptance for unused families.','Distortion uses URP opaque texture instead of Built-in GrabPass; transparent-before-effect equivalence must be treated separately.','Native49 actual particle/alpha/depth/MSAA/GLES proofs pending.']}
(out/'manifest.json').write_text(json.dumps(manifest,indent=2));(p/'Tools/URPMigration/ParticleCandidates-current.json').write_text(json.dumps({'stage':str(out)},indent=2));print(json.dumps({'manifest':str(out/'manifest.json'),'sha256':sha(out/'manifest.json')}))
