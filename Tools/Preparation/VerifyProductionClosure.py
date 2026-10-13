from pathlib import Path
from datetime import datetime,timezone
import json,hashlib,shutil,subprocess
p=Path(__file__).resolve().parents[2];seal=Path(json.loads((p/'Tools/URPMigration/ProductionSeal-current.json').read_text())['stage']);d=json.loads((seal/'report.json').read_text())
def sha(f):return hashlib.sha256(f.read_bytes()).hexdigest()
check=subprocess.check_output(['powershell','-NoProfile','-Command',"if ($null -eq (Get-Process -Id 145120 -ErrorAction SilentlyContinue)) { 'absent' } else { 'live' }"],text=True).strip();assert check=='absent'
out=p/'Builds/URPMigration'/('ProductionClosure-'+datetime.now(timezone.utc).strftime('%Y%m%dT%H%M%S%fZ'));out.mkdir();changes=[];restores=[]
for r in d['inputs']:
 path=p/r['path'];actual=sha(path)
 if actual==r['sha256']:continue
 saved=out/'PostExit'/r['path'];saved.parent.mkdir(parents=True,exist_ok=True);shutil.copy2(path,saved)
 changes.append({'path':r['path'],'beforeSHA256':r['sha256'],'postExitSHA256':actual,'preservedPostExit':str(saved)})
 # Restore only proven Graphics schema/flag auto-serialization to exact sealed bytes with pipeline retained.
 if r['path']=='ProjectSettings/GraphicsSettings.asset':
  shutil.copy2(r['preserved'],path);restores.append({'path':r['path'],'restoredSHA256':sha(path),'reason':'Exact source-semantic sealed settings preserved current owned pipeline pointer/source0-0 flags before official close. Post-exit bytes immutable separately.'})
for r in d['inputs']:assert sha(p/r['path'])==r['sha256'],r['path']
staged=subprocess.check_output(['git','--no-optional-locks','ls-files','--stage','-z'],cwd=p);assert staged==(seal/'staging-entries.bin').read_bytes()
log=p/'Builds/URPMigration/SourceInteractiveSession-20261002T234401824Z/editor-source-interactive.log';shutil.copy2(log,out/'editor-closed.log')
report={'utc':datetime.now(timezone.utc).isoformat(),'pid':145120,'actualProcessAbsent':True,'seal':str(seal/'report.json'),'sealSHA256':sha(seal/'report.json'),'closeRequest':str(seal/'close-request.json'),'closeRequestSHA256':sha(seal/'close-request.json'),'verifiedInputs':len(d['inputs']),'postExitChanges':changes,'exactRestores':restores,'stagingEntriesUnchanged':True,'log':str(out/'editor-closed.log'),'logSHA256':sha(out/'editor-closed.log'),'limits':['Editor officially closed; no new process launch/forcekill/SaveAssets/commit/push.','Current saved material/pipeline/scene/source/index closure proved; fresh source-free URP native draw/ordinary180s/player remain future gates.']}
(out/'report.json').write_text(json.dumps(report,indent=2));(p/'Tools/URPMigration/ProductionClosure-current.json').write_text(json.dumps({'stage':str(out)},indent=2));print(json.dumps({'report':str(out/'report.json'),'sha256':sha(out/'report.json'),'changes':changes,'restores':restores}))
