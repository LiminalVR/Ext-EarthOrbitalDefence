from pathlib import Path
from datetime import datetime, timezone
import json, hashlib, shutil, subprocess, re, os
p=Path(__file__).resolve().parents[2];base=p/'Builds/URPMigration'
out=Path(json.loads((p/'Tools/URPMigration/FinalClosure-current.json').read_text())['stage']);build=out.parent
sha=lambda f:hashlib.sha256(Path(f).read_bytes()).hexdigest()
write=lambda name,data:(out/name).write_text(json.dumps(data,indent=2),encoding='utf-8')
assert subprocess.check_output(['powershell','-NoProfile','-Command',"if ($null -eq (Get-Process -Id 118008 -ErrorAction SilentlyContinue)) { 'absent' } else { 'live' }"],text=True).strip()=='absent'
assert (out/'exit-native-marker.json').is_file()
pre=json.loads((out/'preclose.json').read_text());post=[]
for r in pre['inputs']:
 f=p/r['path']
 if sha(f)==r['sha256'] or r['path'] in ['ProjectSettings/GraphicsSettings.asset','ProjectSettings/UnityConnectSettings.asset']:continue
 saved=out/'PostExit'/r['path'];saved.parent.mkdir(parents=True,exist_ok=True);shutil.copy2(f,saved)
 assert r['path']=='ProjectSettings/ProjectSettings.asset',r['path']
 before=Path(r['preserved']).read_bytes();newline=b'\r\n' if b'\r\n' in before else b'\n';expect=re.sub(br'  preloadedAssets:\r?\n(?:  -[^\n]*\r?\n)+',b'  preloadedAssets: []'+newline,before)
 assert expect==f.read_bytes()
 post.append({'path':r['path'],'beforeSHA256':r['sha256'],'postExitSHA256':sha(f),'preserved':str(saved),'classification':'Official exit persisted exact native postbuild empty preload list. Internet1 retained; all other fields byte-identical.'})
restore=[]
g=p/'ProjectSettings/GraphicsSettings.asset';original=build/'BeforeBuild/ProjectSettings/GraphicsSettings.asset';before=(out/'BeforeExit/ProjectSettings/GraphicsSettings.asset').read_bytes();expect=before.replace(b'm_LightsUseLinearIntensity: 1',b'm_LightsUseLinearIntensity: 0').replace(b'm_LightsUseColorTemperature: 1',b'm_LightsUseColorTemperature: 0');assert expect==original.read_bytes();assert g.read_bytes() in (before,expect)
saved=out/'BeforeScopedCleanup/GraphicsSettings.asset';saved.parent.mkdir(exist_ok=True)
if not saved.exists():saved.write_bytes(before)
assert saved.read_bytes()==before;g.write_bytes(expect);restore.append({'path':'ProjectSettings/GraphicsSettings.asset','beforeSHA256':sha(saved),'afterSHA256':sha(g),'exactPrebuildRestored':True})
c=p/'ProjectSettings/UnityConnectSettings.asset';before=(out/'BeforeExit/ProjectSettings/UnityConnectSettings.asset').read_bytes();assert before.count(b'\n  m_Enabled: 0')==1;expect=before.replace(b'\n  m_Enabled: 0',b'\n  m_Enabled: 1',1);assert c.read_bytes() in (before,expect);saved=out/'BeforeScopedCleanup/UnityConnectSettings.asset'
if not saved.exists():saved.write_bytes(before)
assert saved.read_bytes()==before;c.write_bytes(expect);restore.append({'path':'ProjectSettings/UnityConnectSettings.asset','beforeSHA256':sha(saved),'afterSHA256':sha(c),'originalMasterEnabledRestored':True,'limits':'Caller of native master flag change not authenticated; all generated schema/alias/service-off values retained.'})
ps=(p/'ProjectSettings/ProjectSettings.asset').read_bytes();assert b'  preloadedAssets: []' in ps and b'  ForceInternetPermission: 1' in ps
q=(p/'ProjectSettings/QualitySettings.asset').read_text();assert len(re.findall(r'customRenderPipeline: \{fileID: 11400000, guid: e4b8dd97f2e5c044e9130ba7055a23b7',q))==6
staged=subprocess.check_output(['git','--no-optional-locks','ls-files','--stage','-z'],cwd=p);assert staged==(out/'staging-entries.bin').read_bytes()
originals={r['path']:r for r in pre['inputs']};inputs=[];new=[]
for folder in ['Assets','Packages','ProjectSettings']:
 for f in sorted((p/folder).rglob('*')):
  if not f.is_file():continue
  rel=f.relative_to(p).as_posix();row={'path':rel,'sha256':sha(f),'bytes':f.stat().st_size};inputs.append(row)
  if rel not in originals:
   assert rel=='ProjectSettings/Packages/com.unity.dedicated-server/ContentSelectionSettings.asset',rel
   b=f.read_text();assert 'm_StripRenderComponents: 0' in b and 'm_StripUIComponents: 0' in b and 'm_StripAudioComponents: 0' in b
   saved=out/'PostExit'/rel;saved.parent.mkdir(parents=True,exist_ok=True);shutil.copy2(f,saved);new.append({**row,'preserved':str(saved),'classification':'Native generated dedicated-server ContentSelectionSettings; component/UI/audio strip options all false. No Android source/semantics edits.'})
  elif rel not in ['ProjectSettings/GraphicsSettings.asset','ProjectSettings/UnityConnectSettings.asset','ProjectSettings/ProjectSettings.asset']:assert row['sha256']==originals[rel]['sha256'],rel
assert len(inputs)==1950
write('closure.json',{'utc':datetime.now(timezone.utc).isoformat(),'pid':118008,'actualProcessAbsent':True,'exitRequest':str(out/'exit-request.json'),'nativeExitMarker':str(out/'exit-native-marker.json'),'preclose':str(out/'preclose.json'),'precloseSHA256':sha(out/'preclose.json'),'postExitChanges':post,'scopedCleanup':restore,'generatedNewFiles':new,'currentInputs':inputs,'stagingEntriesUnchanged':True,'limits':['Official EditorApplication.Exit(0) only; no forcekill, SaveAssets or broad dirty clearing. Source0/0/masterCloud1 restored only after actual absence. Generated default-volume/prefilter/URP schema retained; SDK-required Internet1 preserved.','No material/shader/scene/script/meta changed during build or official exit. All historical/failed source/render/packet stages retained. Physical Quest and unaided device/host separate.']})
# Route exact same-mesh source reconstruction/current production captures; preserve original pixels.
ast=base/'ActualAsteroid-20261003T025642008Z';ad=json.loads((ast/'report.json').read_text());pairs=out/'SupplementaryAsteroidPairs';pairs.mkdir();pairRows=[]
for r in ad['rows']:
 pipeline='Source' if r['mode']=='SourceOriginal' else 'URP';src=Path(r['image']);target=pairs/pipeline/('view-'+str(r['view'])+'.png');target.parent.mkdir(exist_ok=True);os.link(src,target);assert sha(src)==sha(target);pairRows.append({'pipeline':pipeline,'originalPath':str(src),'galleryAlias':str(target),'sha256':sha(src),'view':r['view'],'method':'Same-volume hardlink; original PNG pixels unchanged'})
(pairs/'capture-metadata.json').write_text(json.dumps({'utc':datetime.now(timezone.utc).isoformat(),'rows':pairRows,'sourceReference':str(ast/'report.json'),'sourceReferenceSHA256':sha(ast/'report.json'),'actualRuntimeAttribution':str(ast/'attribution.json'),'limits':['Supplementary same authored mesh/material/controlled angle source reconstruction versus persistent production URP; not original natural spawn/rotation pair or first-ever cold draw.','Original all3 natural bodies remain separate unpaired gameplay captures. Source textures/cracks are visibly present on both; numerical HDR/edge residuals retained in original report.']},indent=2))
limits=['Fresh URP-only ordinary survival180.004s/SDKOnComplete(true) and no-fire normal health-depletion93.697s/particlePoint/SDK ending completed0errors. Original180s/source63.78s loss reused; stochastic shot/kill/health/loss clocks differ and are not adjusted.','Normal public SDK AutoClicker/controller poses and genuine UI pointer/SphereCast/collision gameplay are a transient Editor fixture, restored; unaided host/device and physical Quest not tested.','Source/current natural3families body captures are different spawned rotations/effects, unpaired. Exact3-angle asteroid reconstructed fixture is supplementary.','Inherited source timer/partial-score GameView cache quirks retained, actual state/events authenticated separately. Black actualSDK ending is monoscopic Editor evidence; headset compositor not verified.','First two cold actualMainscope draws are authored-black startupfade, scope/provider proof only; meaningful geometry is covered by the subsequent fresh natural run.','Source prefab-context Health.OnValidate assertions during conversion are separate from actualMain/fullruntime0errors; unsupported hardware/audio startup diagnostics remain preserved.','RawHDR/threshold/edge/depth-particle transport limits in controlled shader proofs remain explicit. No wholeframe bit-identity/allunreachable asset parity claimed.','343 build warnings retained; no commit/push/device installation/shared SDK/global changes.']
refs={
 'androidBuild':(out/'android-build-summary.json','Succeeded0errors343warnings; actualAPK128354775bytes'),
 'currentSource':(base/'AuthenticAndroidTargets-20261003T031033219359Z/summary.json','Genuine current Android2 scopes0errors/fresh providers'),
 'editorSource':(base/'AuthenticEditorTargets-20261003T024821327950Z/summary.json','Genuine current Editor4 scopes0errors/fresh providers'),
 'currentProviders':(out/'current-source-provider-proof.json','Rehashed807 current report source/reference/output paths;6 matching scopes'),
 'packedBindings':(build/'PackedScriptProviders-20261003T031035713Z/verification.json','ActualAPK33 SDK scripts/Release provider bindings'),
 'profileSettings':(build/'PackedScriptProviders-20261003T031035713Z/profile-field-verification.json','ActualAPK440 profile bytes/all78 leaf fields'),
 'effectiveMaterials':(base/'NativeEffectiveAudit-20261003T031757588Z/report.json','Postbuild29 identities/1427 checks0failures'),
 'rendererSlots':(base/'ScopeClassification-20261003T021240071749Z/report.json','196 exact source-scoped renderers/257 slots/43 authored nulls'),
 'freshGameplay':(base/'FinalUrpRoute-20261003T024122303Z/observation.json','Fresh ordinary180survival193.724s elapsed/actualSDK ending0errors'),
 'healthLoss':(base/'FinalUrpLossRoute-20261003T025043644Z/observation.json','Normalno-fire healthloss/range50 particlePoint/SDKending0errors'),
 'visualReview':(base/'FinalUrpReview-20261003T024701983301Z/report.json','21 natural captures/3body families; naturalsource/current unpaired'),
 'asteroidProof':(ast/'attribution.json','Actualruntime material +3angle same-mesh source/current cracktexture proof'),
 'asteroidPublication':(pairs/'capture-metadata.json','3 supplementary exact controlled asteroid pairs'),
 'generatedConfig':(out/'generated-config-classification.json','Seven authentic native build/config saves plus explicit Cloud caller limit'),
 'closure':(out/'closure.json','Official118008exit/actualabsence/1950current inputs/source setting cleanup')}
apk=build/'Experience49-URP.apk';assert apk.stat().st_size==128354775 and sha(apk)=='274023078975f3e71136bdaae0178899254d08bded5d45555277c262f4b9d6f9'
report={'schemaVersion':1,'utc':datetime.now(timezone.utc).isoformat(),'stage':'Desktop source/render/fresh gameplay/native Android/profile checks complete; official editor closed; hardware/host gates separate','reports':{k:{'path':str(f.relative_to(base)).replace('\\','/'),'sha256':sha(f),'label':label} for k,(f,label) in refs.items()},'apks':[{'path':str(apk.relative_to(base)).replace('\\','/'),'sha256':sha(apk),'bytes':apk.stat().st_size}],'limits':limits}
write('completion-closed.json',report);report['reports']['completion']={'path':str((out/'completion-closed.json').relative_to(base)).replace('\\','/'),'sha256':sha(out/'completion-closed.json'),'label':'Final desktop checks/current proofs/official closure'}
pointer=base/'current-evidence.json';assert not pointer.exists();pointer.write_text(json.dumps(report,indent=2))
print(json.dumps({'closure':str(out/'closure.json'),'closureSHA256':sha(out/'closure.json'),'completion':str(out/'completion-closed.json'),'completionSHA256':sha(out/'completion-closed.json'),'currentPointer':str(pointer),'currentPointerSHA256':sha(pointer),'currentInputs':len(inputs),'postExitDeltas':len(post),'scopedCleanup':len(restore),'supplementaryPairs':str(pairs/'capture-metadata.json')}))
