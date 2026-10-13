from pathlib import Path
from datetime import datetime, timezone
import json, hashlib, shutil, subprocess, re, difflib
p=Path(__file__).resolve().parents[2]
out=Path(json.loads((p/'Tools/URPMigration/FinalClosure-current.json').read_text())['stage'])
build=out.parent
sha=lambda f:hashlib.sha256(Path(f).read_bytes()).hexdigest()
write=lambda name,data:(out/name).write_text(json.dumps(data,indent=2),encoding='utf-8')
native=json.loads((p/'Builds/URPMigration/NativeCurrentCheckpoint-20261003T031720552Z/report.json').read_text())
assert native['pid']==118008 and not native['playing'] and not native['sceneDirty'] and not native['compiling']
assert not [r for r in native['dirty'] if r['path'].startswith('Assets/') and ('Material,' in r['type'] or 'GameObject,' in r['type'])]
audit=p/'Builds/URPMigration/NativeEffectiveAudit-20261003T031757588Z/report.json';a=json.loads(audit.read_text());assert a['failed']==0 and len(a['checks'])==1427
pre=json.loads((build/'prebuild.json').read_text());post=build/'PostbuildState-20261003T031033384030Z';pd=json.loads((post/'report.json').read_text())
np=json.loads((out/'native-postbuild.json').read_text());assert np['preloadedAssets']==[] and np['forceInternetPermission']
def normalize(x):
 if isinstance(x,dict):return {k:normalize(v) for k,v in x.items() if k not in ['m_Name','m_OverrideState']}
 if isinstance(x,list):return [normalize(v) for v in x]
 return x
vol=[]
for c in np['defaultVolumeComponents']:
 equal=normalize(c['current'])==normalize(c['constructorDefault']);assert equal,c['type'];vol.append({'type':c['type'],'constructorDefaultValuesEqual':equal})
pkg=p/'Library/PackageCache'
callers=[pkg/'com.unity.render-pipelines.universal@df9561ad1d75/Editor/BuildProcessors/URPPreprocessBuild.cs',pkg/'com.unity.render-pipelines.core@015d2e136bda/Editor/Volume/VolumeProfileUtils.cs',pkg/'com.unity.render-pipelines.core@015d2e136bda/Runtime/RenderPipeline/RenderPipelineGraphicsSettingsContainer.cs',pkg/'com.unity.render-pipelines.universal@df9561ad1d75/Editor/ShaderBuildPreprocessor.cs',pkg/'com.unity.xr.management@b9db81ae24ef/Editor/XRBuildProcessorHelper.cs',pkg/'com.unity.xr.management@b9db81ae24ef/Editor/XRGeneralBuildProcessor.cs',Path('D:/Work/Liminal/Platform/LiminalSdk/Liminal-SDK-Unity-Package/Assets/Liminal-SDK/SDK/Editor/Builder/AndroidInternetPermissionBuildStep.cs')]
classes={
 'Assets/DefaultVolumeProfile.asset':('Retain generated native defaults','Installed URPPreprocessBuild.EnsureVolumeProfile adds missing overrides. All23 component parameter values equal actual native constructor defaults, excluding name/override-state metadata; source profile was empty. No tuned artwork.'),
 'Assets/UniversalRenderPipelineGlobalSettings.asset':('Retain generated native schema/cache','Installed RenderPipelineGraphicsSettingsContainer.OnBeforeSerialize populates runtime stripped settings only during BuildPlayer. 2D geometry-generator registered schema additions preserved; no authored render-setting compensation.'),
 'Assets/URPMigration/Source49/Pipeline/Source49URP.asset':('Retain native shader prefilter cache','Installed ShaderBuildPreprocessor.GatherShaderFeatures updates prefilter cache. Actual diff restricted to m_Prefilter fields; all authored light/renderer/render parameters unchanged.'),
 'ProjectSettings/GraphicsSettings.asset':('Restore exact prebuild after official PID absence','Only lightsUseLinearIntensity/ColorTemperature0/0 became URP1/1. Runtime owner camera scope already restores source0/0 per camera; no source light/art changes. Exact owned pipeline reference retained.'),
 'ProjectSettings/ProjectSettings.asset':('Restore only transient preload list after official PID absence; retain Internet1','XR preprocess adds provider settings and postprocess removes them. Actual native postbuild API already empty while disk retains two build-time PPtrs. SDK AndroidInternetPermissionBuildStep explicitly forces Internet1 for normal runtime-loaded limapp networking.'),
 'ProjectSettings/MultiplayerManager.asset':('Retain generated native schema','Empty stripping map now empty arrays1/2; obsolete false deployment fields absent. No source gameplay assembly/platform constraint changes.'),
 'ProjectSettings/UnityConnectSettings.asset':('Restore only original master Enabled1 after official PID absence; retain schema','Master1 became0 with unauthenticated caller; preserved as diagnostic rather than assumed necessary. Other service flags remain source-off, native diagnostics alias/default URLs retained; no cloud service activation requested.')}
rows=[]
for r in pd['changes']:
 f=p/r['path'];assert sha(f)==r['sha256'];before=Path(r['before']);diff=''.join(difflib.unified_diff(before.read_text(encoding='utf-8-sig').splitlines(True),f.read_text(encoding='utf-8-sig').splitlines(True),fromfile='prebuild',tofile='postbuild'))
 if r['path'].endswith('Source49URP.asset'):assert all('m_Prefilter' in l for l in diff.splitlines() if l[:1] in ['+','-'] and not l.startswith(('+++','---')))
 if r['path']=='ProjectSettings/GraphicsSettings.asset':assert f.read_bytes().replace(b'm_LightsUseLinearIntensity: 1',b'm_LightsUseLinearIntensity: 0').replace(b'm_LightsUseColorTemperature: 1',b'm_LightsUseColorTemperature: 0')==before.read_bytes()
 df=out/'ConfigDiffs'/(r['path']+'.diff');df.parent.mkdir(parents=True,exist_ok=True);df.write_text(diff)
 rows.append({**r,'actionAfterAbsence':classes[r['path']][0],'classification':classes[r['path']][1],'diff':str(df)})
write('generated-config-classification.json',{'utc':datetime.now(timezone.utc).isoformat(),'postbuildReport':str(post/'report.json'),'postbuildReportSHA256':sha(post/'report.json'),'changes':rows,'nativeDefaults':vol,'nativePreloads':np['preloadedAssets'],'callers':[{'path':str(f),'sha256':sha(f)} for f in callers],'limits':['Actual build and generated bytes retained independently of source-state cleanup. Cloud master change cause unresolved; no functional shader/art/global compensation.']})
inputs=[]
for folder in ['Assets','Packages','ProjectSettings']:
 for f in sorted((p/folder).rglob('*')):
  if not f.is_file():continue
  rel=f.relative_to(p).as_posix();copy=out/'BeforeExit'/rel;copy.parent.mkdir(parents=True,exist_ok=True);shutil.copy2(f,copy);inputs.append({'path':rel,'sha256':sha(f),'bytes':f.stat().st_size,'preserved':str(copy)})
assert len(inputs)==1949
index=Path(subprocess.check_output(['git','rev-parse','--git-path','index'],cwd=p,text=True).strip());index=index if index.is_absolute() else p/index;shutil.copy2(index,out/'index-before');staged=subprocess.check_output(['git','--no-optional-locks','ls-files','--stage','-z'],cwd=p);assert staged==(build/'staging-entries.bin').read_bytes();(out/'staging-entries.bin').write_bytes(staged)
write('persistent-dirty.json',native['dirty']);shutil.copy2(p/'Builds/URPMigration/NativeCurrentCheckpoint-20261003T031720552Z/report.json',out/'native-checkpoint.json')
refs=['AuthenticEditorTargets-20261003T024821327950Z/summary.json','AuthenticAndroidTargets-20261003T031033219359Z/summary.json']
verified={};scopes=[]
def verify(f,expected):
 f=Path(f);assert sha(f)==expected,str(f);verified[str(f.resolve())]=expected
for rel in refs:
 f=p/'Builds/URPMigration'/rel;summary=json.loads(f.read_text());assert summary['allPassed']
 verify(f,sha(f))
 for row in summary['reports']:
  verify(row['path'],row['sha256']);c=json.loads(Path(row['path']).read_text());assert c['exitCode']==0 and c['errors']==0
  verify(c['nativeOptions'],c['nativeOptionsSha256'])
  for kind in ['sources','references','outputs']:
   items=c[kind];items=items if isinstance(items,list) else list(items.values())
   for x in items:
    if isinstance(x,dict) and x.get('path') and x.get('sha256'):verify(x['path'],x['sha256'])
  scopes.append({'assembly':c['assembly'],'target':c['target'],'compile':row['path'],'sha256':row['sha256'],'errors':0,'freshProviderUses':c['freshProviderUses']})
write('current-source-provider-proof.json',{'utc':datetime.now(timezone.utc).isoformat(),'scopes':scopes,'verifiedFiles':[{'path':k,'sha256':v} for k,v in sorted(verified.items())],'limits':['Genuine native target options/fresh source providers; all report source/reference/output paths rehashed now. Caches/package options remain genuine installed version; no physical Quest proof.']})
shutil.copy2(post/'android-build-summary.json',out/'android-build-summary.json')
write('preclose.json',{'utc':datetime.now(timezone.utc).isoformat(),'pid':118008,'inputs':inputs,'nativeCheckpoint':str(out/'native-checkpoint.json'),'effectiveAudit':str(audit),'effectiveAuditSHA256':sha(audit),'dirtyCount':len(native['dirty']),'sceneClean':True,'materialsPrefabsClean':True,'indexSHA256':sha(out/'index-before'),'stagingEntriesUnchanged':True,'generatedConfigClassification':str(out/'generated-config-classification.json'),'limits':['Preclose source and native diagnostics preserved; official exit/actual absence/postexit classification still separate. No blanket saves/dirty clearing/shared edits.']})
print(json.dumps({'preclose':str(out/'preclose.json'),'sha256':sha(out/'preclose.json'),'inputs':len(inputs),'volumeConstructorDefaults':len(vol),'verifiedTargetFiles':len(verified),'scopes':len(scopes)}))
