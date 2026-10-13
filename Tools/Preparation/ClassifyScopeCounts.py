from pathlib import Path
from datetime import datetime,timezone
from collections import Counter,defaultdict
import json,hashlib
p=Path(__file__).resolve().parents[2]
old=p/'Builds/URPMigration/NativeTypedScope-20261002T234922585Z/report.json'
scoped=p/'Builds/URPMigration/ReachableScope-20261003T010706071Z/report.json'
effective=p/'Builds/URPMigration/NativeEffectiveAudit-20261003T020905831Z/report.json'
a=json.loads(old.read_text());b=json.loads(scoped.read_text());e=json.loads(effective.read_text())
def identity(m):return None if m is None else (m['guid'],m['localID'])
def rowkey(origin,name,kind,slots):return (origin,name,kind,tuple(identity(m) for m in slots))
older=Counter(rowkey(r['origin'],r['name'],r['type'],r['materials']) for r in a['renderers'])
scope=Counter();rows=[]
for r in b['sceneRenderers']:
 k=rowkey('actualScene',r['hierarchy'].split('/')[-1],r['type'],r['slots']);scope[k]+=1;rows.append({'origin':'actualScene',**r})
for root in b['roots']:
 if root.get('root') is None or 'renderers' not in root:continue
 for r in root['renderers']:
  origin=root['root']['path'];k=rowkey(origin,r['hierarchy'].split('/')[-1],r['type'],r['slots']);scope[k]+=1;rows.append({'origin':origin,'callsite':root['origin'],**r})
def summarize(counter):
 items=[]
 for (origin,name,kind,slots),count in counter.items():items.append({'origin':origin,'rendererName':name,'type':kind,'orderedSourceIdentities':slots,'occurrences':count,'slots':len(slots)*count,'authoredNullSlots':sum(x is None for x in slots)*count})
 return {'rendererRows':sum(counter.values()),'slots':sum(x['slots'] for x in items),'nullSlots':sum(x['authoredNullSlots'] for x in items),'rows':items}
excluded=summarize(older-scope);included=summarize(scope-older);overlap=summarize(older&scope)
sourceSlots=sum(len(r['slots']) for r in rows);sourceNull=sum(m is None for r in rows for m in r['slots'])
assert len(rows)==len(e['renderers'])==196 and sourceSlots==sum(len(r['slots']) for r in e['renderers'])==257 and sourceNull==43
assert all(c['pass'] for c in e['checks'] if c['field']=='slotCount' or c['field'].startswith('slot'))
def sha(f):return hashlib.sha256(f.read_bytes()).hexdigest()
out=p/'Builds/URPMigration'/('ScopeClassification-'+datetime.now(timezone.utc).strftime('%Y%m%dT%H%M%S%fZ'));out.mkdir()
report={'utc':datetime.now(timezone.utc).isoformat(),'inputs':[{'path':str(f),'sha256':sha(f)} for f in [old,scoped,effective]],'initialBroad':summarize(older),'exactScopedSource':summarize(scope),'overlap':overlap,'broadOnly':excluded,'scopedOnly':included,'currentMatchesExactScopedSource':True,'current':{'rendererRows':196,'orderedSlots':257,'authoredNullSlots':43},'limits':['Broad initial inventory uses actualScene plus every GetDependencies prefab including SDK/inactive base dependencies; scoped source uses actualScene plus original weighted prefab/effect/Health/Laser fields and nested Kill references. Source root repetitions are retained as reference occurrences, not unique object counts.','Multiset comparison authenticates origin path/leaf renderer name/type/full ordered source GUID/localID identities; repeated identical leaves can be aggregated, full scoped hierarchy retained by native source/effective reports.','Net32 fewer rows/35 fewer slots/one extra null is not simply32 excluded rows; additions/repetitions must be accounted for explicitly in broadOnly/scopedOnly. No acceptance is inferred for broad-only inactive dependencies.']}
(out/'report.json').write_text(json.dumps(report,indent=2));print(json.dumps({'report':str(out/'report.json'),'sha256':sha(out/'report.json'),'broad':{k:report['initialBroad'][k] for k in ['rendererRows','slots','nullSlots']},'scoped':{k:report['exactScopedSource'][k] for k in ['rendererRows','slots','nullSlots']},'broadOnly':{k:excluded[k] for k in ['rendererRows','slots','nullSlots']},'scopedOnly':{k:included[k] for k in ['rendererRows','slots','nullSlots']},'originsBroadOnly':sorted(set(r['origin'] for r in excluded['rows']))},indent=2))
