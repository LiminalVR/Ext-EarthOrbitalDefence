from pathlib import Path
from datetime import datetime,timezone
import json,hashlib,shutil,subprocess,re
p=Path(__file__).resolve().parents[2]
packet=Path(json.loads((p/'Tools/URPMigration/ProductionCandidate-current.json').read_text())['stage']);d=json.loads((packet/'manifest.json').read_text())
out=p/'Builds/URPMigration'/('ProductionStateSeal-'+datetime.now(timezone.utc).strftime('%Y%m%dT%H%M%S%fZ'));out.mkdir()
def sha(f):return hashlib.sha256(f.read_bytes()).hexdigest()
inputs=[];changes=[]
original={r['path']:r for r in d['preservedInputs']}
for folder in ['Assets','ProjectSettings','Packages']:
 for file in sorted((p/folder).rglob('*')):
  if not file.is_file():continue
  rel=file.relative_to(p).as_posix();saved=out/'Current'/rel;saved.parent.mkdir(parents=True,exist_ok=True);shutil.copy2(file,saved)
  row={'path':rel,'sha256':sha(file),'bytes':file.stat().st_size,'preserved':str(saved)};inputs.append(row)
  before=original.get(rel)
  if before is None or before['sha256']!=row['sha256']:changes.append({'path':rel,'beforeSHA256':None if before is None else before['sha256'],'afterSHA256':row['sha256']})
source=[r for r in d['preservedInputs'] if r['path'].endswith('.cs')]
assert len(source)==26
assert all(sha(p/r['path'])==r['sha256'] for r in source if r['path']!='Assets/Scripts/AppController.cs')
assert sha(p/'Assets/Scripts/AppController.cs')==next(r['candidateSHA256'] for r in d['productionFiles'] if r['deployPath']=='Assets/Scripts/AppController.cs')
pipeline='e4b8dd97f2e5c044e9130ba7055a23b7';g=(p/'ProjectSettings/GraphicsSettings.asset').read_text();q=(p/'ProjectSettings/QualitySettings.asset').read_text()
assert re.search(r'm_CustomRenderPipeline: \{fileID: 11400000, guid: '+pipeline,g)
assert len(re.findall(r'customRenderPipeline: \{fileID: 11400000, guid: '+pipeline,q))==6
refs=['ScopedConversion-20261003T015603487Z/report.json','ScopedConfigure-20261003T015818031Z/report.json','NativeEffectiveAudit-20261003T020905831Z/report.json','PipelineDiskPersistence-20261003T020833973871Z/report.json','ScopeClassification-20261003T021240071749Z/report.json','NativeCurrentCheckpoint-20261003T021649474Z/report.json','NativeStartup-20261003T021451760Z/report.json','TextureSTRestore-20261003T020606875Z/report.json']
evidence=[{'path':str(p/'Builds/URPMigration'/name),'sha256':sha(p/'Builds/URPMigration'/name)} for name in refs]
gitdir=subprocess.check_output(['git','rev-parse','--git-path','index'],cwd=p,text=True).strip();index=Path(gitdir);index=index if index.is_absolute() else p/index
shutil.copy2(index,out/'current-index');staged=subprocess.check_output(['git','--no-optional-locks','ls-files','--stage','-z'],cwd=p);(out/'staging-entries.bin').write_bytes(staged)
assert staged==(packet/'source-stage-entries.bin').read_bytes()
editor=p/'Builds/URPMigration/SourceInteractiveSession-20261002T234401824Z/editor-source-interactive.log';lines=editor.read_text(errors='replace').splitlines()
start=next(i for i,line in enumerate(lines) if '[Tools/URPMigration/Native49ScopedProduction.cs line 163]' in line)
selected=[{'line':i+1,'text':line} for i,line in enumerate(lines) if i>=start and ('Main thread operation timed out after 30000ms' in line or 'AssertionException: AppController must not be null' in line or 'Health.OnValidate' in line)]
(out/'import-diagnostics.json').write_text(json.dumps(selected,indent=2))
native=json.loads((p/'Builds/URPMigration/NativeCurrentCheckpoint-20261003T021649474Z/report.json').read_text());assert native['sceneDirty'] is False and native['playing'] is False
dirty=[]
for i,r in enumerate(native['dirty']):
 path=Path(r['path']);path=p/path if not path.is_absolute() else path
 record={'name':r['name'],'path':r['path'],'type':r['type'],'nativeJSON':r['json']}
 if path.is_file():
  saved=out/'PersistentDirtyBytes'/str(i)/path.name;saved.parent.mkdir(parents=True,exist_ok=True);shutil.copy2(path,saved);record['fileSHA256']=sha(path);record['preserved']=str(saved)
  meta=Path(str(path)+'.meta')
  if meta.is_file():shutil.copy2(meta,Path(str(saved)+'.meta'));record['metaSHA256']=sha(meta)
 dirty.append(record)
(out/'persistent-dirty.json').write_text(json.dumps(dirty,indent=2))
report={'utc':datetime.now(timezone.utc).isoformat(),'pid':145120,'project':str(p),'inputs':inputs,'changedOrNew':changes,'source26':{'unchanged':25,'AppControllerHookSHA256':sha(p/'Assets/Scripts/AppController.cs'),'twoNewOwnedRuntimeFiles':True},'evidence':evidence,'indexSHA256':sha(out/'current-index'),'stagingEntriesUnchanged':True,'settingsDiskVerified':{'graphics':1,'qualities':6,'guid':pipeline,'localID':11400000},'nativeSceneClean':True,'persistentDirtySnapshot':str(out/'persistent-dirty.json'),'limits':['Pre-close current state only; no cold-process/ordinary URP gameplay/player acceptance.','All actual source-scoped29mat/196referenceoccurrences/257slots including43authoredNull pass1427 checks; HDR rounding relative2e-6 explicitly retained.','Import Health.OnValidate assertion is source-prefab Earth controller-null metadata; actual Main Earth points to authored AppController. Runtime source/fullroute already0errors; final current runtime still pending.','Original HTTP400 response body was not retained by old command client; exact native log records main-thread30s timeout, followed by completed29row conversion/errors[] and ready native state.','No broad SaveAssets/dirty clearing/shared SDK edits/commit/push; historical/failing reports intact.','OfficialExit may serialize settings; verify post-absence hashes and restore only authenticated required source semantics before replacement.']}
(out/'report.json').write_text(json.dumps(report,indent=2));(p/'Tools/URPMigration/ProductionSeal-current.json').write_text(json.dumps({'stage':str(out)},indent=2))
print(json.dumps({'report':str(out/'report.json'),'sha256':sha(out/'report.json'),'currentInputs':len(inputs),'changedOrNew':len(changes),'dirtyObjectsPreserved':len(dirty)}))
