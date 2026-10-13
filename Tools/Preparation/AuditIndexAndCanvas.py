from pathlib import Path
from datetime import datetime,timezone
import hashlib,json,subprocess,os,shutil
p=Path(__file__).resolve().parents[2]
out=p/'Builds/URPMigration'/('SourceIndexAndCanvas-'+datetime.now(timezone.utc).strftime('%Y%m%dT%H%M%S%fZ'));out.mkdir()
def sha(f):return hashlib.sha256(Path(f).read_bytes()).hexdigest()
def rec(f):return {'path':str(f),'bytes':Path(f).stat().st_size,'sha256':sha(f)}
prep=p/'Builds/URPMigration/DesktopPreparation-20261002T233926522886Z'
before=json.loads((prep/'review.json').read_text(encoding='utf-8-sig'))
idx=Path(before['gitIndex']['path']);original=prep/'git-index-before'
shutil.copy2(original,out/'index-original');shutil.copy2(idx,out/'index-current')
rows=[]
for label in ('original','current'):
 env=os.environ.copy();env['GIT_INDEX_FILE']=str(out/('index-'+label))
 result=subprocess.run(['git','--no-optional-locks','-C',str(p),'ls-files','--stage','-z'],env=env,capture_output=True,check=True)
 f=out/(label+'-staged-entries.z');f.write_bytes(result.stdout);rows.append(rec(f))
assert sha(idx)==sha(out/'index-current')
canvas=p/'Builds/URPMigration/CanvasCaptureProof-20261003T001733575Z/report.json'
c=json.loads(canvas.read_text())
record={'utc':datetime.now(timezone.utc).isoformat(),'originalIndex':rec(original),'currentIndex':rec(idx),'preservedIndexes':[rec(out/'index-original'),rec(out/'index-current')],'stagedEntryLists':rows,'indexBytesUnchanged':sha(original)==sha(idx),'stagedEntriesUnchanged':rows[0]['sha256']==rows[1]['sha256'],'indexMutationPerformed':False,'indexDriftCause':'Unknown; identical staged entries would establish semantic staging preservation, not identify the byte-drift writer.','canvasReport':rec(canvas),'canvasImages':[rec(p/r['path']) for r in c['rows']],'canvasTimerText':c['rows'][0]['actualTimerText'],'canvasGameTime':c['rows'][0]['gameTime'],'canvasRestores':c['restores'],'canvasProof':'At actual ordinary game time2.001115s, text and native TextGenerator/CanvasRenderer mesh report02:58/5visiblecharacters/20vertices. ForceUpdateCanvases changes no text or geometry; PNG comparison recorded separately. Earlier 180s capture text cache was not sampled, so no retrospective frame-cache causality claim.','productionUIChanges':False}
(out/'report.json').write_text(json.dumps(record,indent=2))
(p/'Tools/URPMigration/SourceIndexAndCanvas-current.json').write_text(json.dumps({'stage':str(out)},indent=2))
print(json.dumps({'report':str(out/'report.json'),'sha256':sha(out/'report.json'),'indexBytesUnchanged':record['indexBytesUnchanged'],'stagedEntriesUnchanged':record['stagedEntriesUnchanged'],'canvasImageHashes':[r['sha256'] for r in record['canvasImages']]},indent=2))
