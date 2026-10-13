from pathlib import Path
from datetime import datetime, timezone
import json, hashlib, shutil, subprocess, difflib, re, struct
import yaml

p=Path(__file__).resolve().parents[2]
seal=p/'Builds/URPMigration/ProductionStateSeal-20261003T021848049502Z'
d=json.loads((seal/'report.json').read_text())
sha=lambda f:hashlib.sha256(f.read_bytes()).hexdigest()
assert subprocess.check_output(['powershell','-NoProfile','-Command',"if ($null -eq (Get-Process -Id 145120 -ErrorAction SilentlyContinue)) { 'absent' } else { 'live' }"],text=True).strip()=='absent'
out=p/'Builds/URPMigration'/('ProductionClosureClassified-'+datetime.now(timezone.utc).strftime('%Y%m%dT%H%M%S%fZ'));out.mkdir()
dirty=json.loads((seal/'persistent-dirty.json').read_text())
scope=json.loads((p/'Builds/URPMigration/ReachableScope-20261003T010706071Z/report.json').read_text())
def paths(x):
 if isinstance(x,dict):
  for k,v in x.items():
   if k=='path' and isinstance(v,str):yield v
   yield from paths(v)
 elif isinstance(x,list):
  for v in x:yield from paths(v)
scoped=set(paths(scope))
def load(f):
 text=f.read_text(encoding='utf-8-sig')
 text=re.sub(r'^%.*\n','',text,flags=re.M)
 text=re.sub(r'^--- !u!\d+ &(-?\d+)\n',r'---\n',text,flags=re.M)
 return list(yaml.safe_load_all(text))
def native_equal(a,b):
 if isinstance(a,dict) and isinstance(b,dict):return a.keys()==b.keys() and all(native_equal(v,b[k]) for k,v in a.items())
 if isinstance(a,list) and isinstance(b,list):return len(a)==len(b) and all(native_equal(x,y) for x,y in zip(a,b))
 if isinstance(a,(int,float)) and isinstance(b,(int,float)):return struct.pack('<f',a)==struct.pack('<f',b)
 if isinstance(a,str) and isinstance(b,int) and a.isdigit():return int(a)==b
 return a==b
changes=[]
prior=p/'Builds/URPMigration/ProductionClosure-20261003T022913085268Z'
for r in d['inputs']:
 f=p/r['path']
 archived=prior/'PostExit'/r['path']
 if not archived.exists():
  assert sha(f)==r['sha256'],r['path']
  continue
 saved=out/'PostExit'/r['path'];saved.parent.mkdir(parents=True,exist_ok=True);shutil.copy2(archived,saved)
 assert sha(f) in (r['sha256'],sha(archived)),r['path']
 before=Path(r['preserved']);diff=''.join(difflib.unified_diff(before.read_text(encoding='utf-8-sig').splitlines(True),archived.read_text(encoding='utf-8-sig').splitlines(True),fromfile='sealed',tofile='postExit'))
 diffpath=out/'Diffs'/(r['path']+'.diff');diffpath.parent.mkdir(parents=True,exist_ok=True);diffpath.write_text(diff)
 row={'path':r['path'],'sealedSHA256':r['sha256'],'postExitSHA256':sha(archived),'preservedPostExit':str(saved),'diff':str(diffpath)}
 if r['path'].endswith('.mat'):
  assert r['path'] not in scoped,r['path']
  matches=[x for x in dirty if x['path']==r['path']];assert len(matches)==1
  row['nativeDirtyBeforeExit']=matches[0]
  row['classification']='Inherited unowned material serialized at official Exit; outside exact29 identity scope. Preserve shutdown bytes and restore only exact sealed source bytes.'
  shutil.copy2(before,f);assert sha(f)==r['sha256'];row['action']='Exact sealed byte restore'
 elif r['path']=='ProjectSettings/GraphicsSettings.asset':
  assert sha(f)==r['sha256']
  row['classification']='Native pipeline settings auto-serialization preserved by prior incomplete verifier; exact sealed source0-0 flags and owned URP PPtr restored.'
  row['action']='Exact sealed byte restore already completed'
 elif r['path']=='Assets/XR/XRGeneralSettings.asset':
  old=load(before);new=load(f);assert len(old)==len(new)
  for a,b in zip(old,new):
   aa=a['MonoBehaviour'];bb=b['MonoBehaviour']
   if 'm_LoaderManagerInstance' in aa:aa['m_Manager']=aa.pop('m_LoaderManagerInstance')
   if 'Keys' in aa:
    raw=re.search(r'^  Keys: ([0-9a-f]+)$',before.read_text(),re.M).group(1)
    keys=list(struct.unpack('<'+'i'*(len(raw)//8),bytes.fromhex(raw)))
    aa['m_SettingsPerBuildTarget']=[{'buildTarget':k,'settings':v} for k,v in zip(keys,aa['Values'])];aa['Keys']=None;aa['Values']=[]
   assert aa==bb,(aa,bb)
  row['classification']='Installed XRManagement documented manager alias and obsolete Keys/Values migration; exact provider mappings/settings/PPtrs unchanged after normalization.'
  row['action']='Retain native current schema'
 elif r['path']=='ProjectSettings/QualitySettings.asset':
  aa=load(before)[0]['QualitySettings'];bb=load(f)[0]['QualitySettings'];newfields=[]
  assert len(aa['m_QualitySettings'])==len(bb['m_QualitySettings'])==6
  for a,b in zip(aa['m_QualitySettings'],bb['m_QualitySettings']):
   for oldkey,newkey in [('blendWeights','skinWeights'),('textureQuality','globalTextureMipmapLimit')]:a[newkey]=a.pop(oldkey)
   a.pop('serializedVersion');b=dict(b);b.pop('serializedVersion')
   assert all(b[k]==v for k,v in a.items()),a['name']
   newfields.append({'quality':a['name'],'nativeAddedFields':{k:v for k,v in b.items() if k not in a}})
  q=json.loads(next(x for x in dirty if x['path']==r['path'])['nativeJSON'])['QualitySettings']
  assert len(q['m_QualitySettings'])==6
  # Native pre-exit singleton already contained serialized native schema/defaults.
  assert native_equal(q['m_QualitySettings'],bb['m_QualitySettings'])
  for k,v in aa.items():
   if k=='m_QualitySettings':continue
   if k=='m_PerPlatformDefaultQuality':assert all(bb[k][kk]==vv for kk,vv in v.items())
   else:assert bb[k]==v
  row['classification']='Native quality schema aliases/default fields serialized; every authored quality value and all6 intended pipeline PPtrs retained. Exact post-exit quality rows equal preserved pre-exit native singleton JSON.'
  row['nativeAddedFields']=newfields;row['action']='Retain native current schema'
 else:raise AssertionError('Unclassified shutdown delta '+r['path'])
 row['currentSHA256']=sha(f);changes.append(row)
staged=subprocess.check_output(['git','--no-optional-locks','ls-files','--stage','-z'],cwd=p);assert staged==(seal/'staging-entries.bin').read_bytes()
inputs=[]
for r in d['inputs']:
 f=p/r['path'];actual=sha(f)
 assert actual==r['sha256'] or any(x['path']==r['path'] and x['action']=='Retain native current schema' for x in changes),r['path']
 inputs.append({'path':r['path'],'sha256':actual,'bytes':f.stat().st_size,'sealedSHA256':r['sha256']})
log=p/'Builds/URPMigration/SourceInteractiveSession-20261002T234401824Z/editor-source-interactive.log';shutil.copy2(log,out/'editor-closed.log')
prior=p/'Builds/URPMigration/ProductionClosure-20261003T022913085268Z'
report={'utc':datetime.now(timezone.utc).isoformat(),'pid':145120,'actualProcessAbsent':True,'seal':str(seal/'report.json'),'sealSHA256':sha(seal/'report.json'),'closeRequest':str(seal/'close-request.json'),'closeRequestSHA256':sha(seal/'close-request.json'),'inputs':inputs,'verifiedInputs':len(inputs),'changes':changes,'earlierIncompleteClosure':str(prior),'earlierGraphicsRestore':'Prior verifier preserved postExit GraphicsSettings and restored exact sealed owned URP pipeline/source0-0 bytes. Its subsequent Dust assertion failure is retained.','stagingEntriesUnchanged':True,'log':str(out/'editor-closed.log'),'logSHA256':sha(out/'editor-closed.log'),'limits':['No fresh process or native render since official close. Fresh current schema native settings/owned root scope/disk materials/ordinaryURP180 and Android build remain gates.','13 exact unowned material restores do not change intended25 saved conversions; native schema2 retained only after source value and pre-exit native proof.','No blanket SaveAssets, forcekill, unrelated process manipulation, commit, push or shared SDK changes.']}
(out/'report.json').write_text(json.dumps(report,indent=2));(p/'Tools/URPMigration/ProductionClosure-current.json').write_text(json.dumps({'stage':str(out)},indent=2))
print(json.dumps({'report':str(out/'report.json'),'sha256':sha(out/'report.json'),'inputs':len(inputs),'changes':len(changes)}))
