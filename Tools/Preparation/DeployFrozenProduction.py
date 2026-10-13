from pathlib import Path
from datetime import datetime,timezone
import json,hashlib,shutil
p=Path(__file__).resolve().parents[2]
packet=Path(json.loads((p/'Tools/URPMigration/ProductionCandidate-current.json').read_text())['stage'])
manifest=packet/'manifest.json'; d=json.loads(manifest.read_text())
def sha(f):return hashlib.sha256(f.read_bytes()).hexdigest()
assert sha(manifest)=='1ca0d44a87bd1b9eb971cabb4de595769957a19191cd7d99d02fb2ba46382490'
for r in d['preservedInputs']:assert sha(p/r['path'])==r['sha256'],r['path']
for r in d['productionFiles']:assert sha(Path(r['candidate']))==r['candidateSHA256'],r['candidate']
out=p/'Builds/URPMigration'/('ProductionDeployment-'+datetime.now(timezone.utc).strftime('%Y%m%dT%H%M%S%fZ'));out.mkdir()
rows=[]
for r in d['productionFiles']:
 dst=p/r['deployPath'];before=sha(dst) if dst.exists() else None
 if dst.exists():
  saved=out/'Before'/r['deployPath'];saved.parent.mkdir(parents=True,exist_ok=True);shutil.copy2(dst,saved)
 if r['deployPath'].startswith('Assets/'):
  assert before is None or r['deployPath']=='Assets/Scripts/AppController.cs',r['deployPath']
  dst.parent.mkdir(parents=True,exist_ok=True);shutil.copy2(r['candidate'],dst)
 else:assert before==r['candidateSHA256'],r['deployPath']
 rows.append({'path':r['deployPath'],'beforeSHA256':before,'afterSHA256':sha(dst),'sourceCandidate':r['candidate']})
for name in ['Materials','Pipeline']:(p/'Assets/URPMigration/Source49'/name).mkdir(parents=True,exist_ok=True)
receipt={'utc':datetime.now(timezone.utc).isoformat(),'packet':str(manifest),'packetSHA256':sha(manifest),'rows':rows,'state':'Exact disk deployment only; native import/compile/effective/render/player not yet accepted.','noSharedEdits':True}
(out/'deployment.json').write_text(json.dumps(receipt,indent=2))
(p/'Tools/URPMigration/ProductionDeployment-current.json').write_text(json.dumps({'stage':str(out)},indent=2))
print(json.dumps({'report':str(out/'deployment.json'),'sha256':sha(out/'deployment.json'),'deployedAssets':sum(r['path'].startswith('Assets/') for r in rows)}))
