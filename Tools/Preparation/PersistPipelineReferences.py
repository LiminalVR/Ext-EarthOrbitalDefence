from pathlib import Path
from datetime import datetime,timezone
import json,re,hashlib,shutil
p=Path(__file__).resolve().parents[2]
native=p/'Builds/URPMigration/ScopedConfigure-20261003T015818031Z/report.json'; d=json.loads(native.read_text())
guid,local=d['pipelineIdentity'].split('/');assert local=='11400000'
meta=(p/'Assets/URPMigration/Source49/Pipeline/Source49URP.asset.meta').read_text();assert re.search(r'^guid: '+guid+'$',meta,re.M)
out=p/'Builds/URPMigration'/('PipelineDiskPersistence-'+datetime.now(timezone.utc).strftime('%Y%m%dT%H%M%S%fZ'));out.mkdir()
def sha(f):return hashlib.sha256(f.read_bytes()).hexdigest()
rows=[]
for name in ['GraphicsSettings.asset','QualitySettings.asset']:
 path=p/'ProjectSettings'/name; before=path.read_bytes();shutil.copy2(path,out/name)
 ptr=('{fileID: '+local+', guid: '+guid+', type: 2}').encode()
 if name=='GraphicsSettings.asset':
  needle=b'm_CustomRenderPipeline: {fileID: 0}';assert before.count(needle)==1
  after=before.replace(needle,b'm_CustomRenderPipeline: '+ptr)
  assert after.replace(b'm_CustomRenderPipeline: '+ptr,needle)==before
 else:
  assert b'customRenderPipeline:' not in before
  lines=before.splitlines(keepends=True);assert sum(x.lstrip().startswith(b'name: ') for x in lines)==6
  output=[];count=0
  for line in lines:
   if line.startswith(b'    excludedTargetPlatforms:'):
    newline=b'\r\n' if line.endswith(b'\r\n') else b'\n';output.append(b'    customRenderPipeline: '+ptr+newline);count+=1
   output.append(line)
  assert count==6;after=b''.join(output)
  assert b''.join(x for x in after.splitlines(keepends=True) if not x.startswith(b'    customRenderPipeline:'))==before
 path.write_bytes(after)
 rows.append({'path':str(path),'beforeSHA256':hashlib.sha256(before).hexdigest(),'afterSHA256':sha(path),'pipelineGUID':guid,'pipelineLocalID':int(local),'exactPPtrChanges':1 if name=='GraphicsSettings.asset' else 6,'allOtherBytesPreserved':True})
report={'utc':datetime.now(timezone.utc).isoformat(),'sourceNativeReport':str(native),'sourceNativeReportSHA256':sha(native),'rows':rows,'reason':'InternalEditorUtility.SaveToSerializedFileAndForget did not persist actual disk settings despite native PPtrs. Exact byte patches preserve original schema and all source flags/settings; native/disk cold proof remains pending.'}
(out/'report.json').write_text(json.dumps(report,indent=2));print(json.dumps({'report':str(out/'report.json'),'sha256':sha(out/'report.json')}))
