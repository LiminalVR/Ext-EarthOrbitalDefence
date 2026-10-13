from pathlib import Path
from datetime import datetime,timezone
import json,hashlib
p=Path(__file__).resolve().parents[2]
out=p/'Builds/URPMigration'/('TransportPlan-'+datetime.now(timezone.utc).strftime('%Y%m%dT%H%M%S%fZ'));out.mkdir()
scope=p/'Builds/URPMigration/NativeTypedScope-20261002T234922585Z/report.json';doc=json.loads(scope.read_text())
identities={}
for r in doc['renderers']:
 for i,m in enumerate(r['materials']):
  if not m:continue
  key=(m['guid'],m['localID']);entry=identities.setdefault(key,{**m,'references':[]})
  entry['references'].append({'origin':r['origin'],'renderer':r['name'],'slot':i,'type':r['type']})
 for m in [r.get('trailMaterial')]:
  if m:identities.setdefault((m['guid'],m['localID']),{**m,'references':[]})['references'].append({'origin':r['origin'],'renderer':r['name'],'slot':'trail','type':r['type']})
rows=[]
for m in identities.values():
 j=json.loads(m['nativeJSON'])['Material'];props=j.get('m_SavedProperties',{}); floats={x['first']:x['second'] for x in props.get('m_Floats',[])};colors={x['first']:x['second'] for x in props.get('m_Colors',[])}
 rows.append({**m,'sourceFloats':floats,'sourceColors':colors,'sourceGI':j.get('m_LightmapFlags'),'sourceDoubleSidedGI':j.get('m_DoubleSidedGI'),'sourceKeywords':j.get('m_ValidKeywords'),'sourceCustomQueue':j.get('m_CustomRenderQueue')})
families={}
for m in rows:families.setdefault(m['shader'],[]).append({'name':m['name'],'path':m['path'],'guid':m['guid'],'localID':m['localID']})
record={'utc':datetime.now(timezone.utc).isoformat(),'scope':str(scope),'scopeSHA256':hashlib.sha256(scope.read_bytes()).hexdigest(),'materialIdentities':rows,'shaderFamilies':families,'sourceEnvironment':doc['environment'],'nextGPUProof':['Source true0/0 vs stock URP1/1 vs source-scopedURP0/0 using actual source light and ambient/sky/reflection.','Source Standard/Specular vs installed native Lit plus sourceBRDF2 candidate across actual material textures and preserved active smoothness/Gamma-metallic/emission flags.','Reachable StandardSurface/StandardUnlit/legacyAdditive/Alpha/Premultiply actual alpha+blend+ColorMask+MSAA semantics.','Source runtime LaserRend.material.color writes follow destination MainColor; retain authoredcolors and actual high/lowcharge callback.','Renderers/orderedslots/default+embedded+prefab+importer remaps need exact identity closure beyond material property audit.'],'limits':['Preparation only;32identities/243allAssets count distinct. No URP equivalence or conversion claimed.','Source no-reflection fallback is NOT inferred from168 invariants. Actual49GPU binding/sceneprobe reachability must be measured.','42nullParticleSystemRenderer secondaryslots retained as original until exactserializedclassification.']}
(out/'plan.json').write_text(json.dumps(record,indent=2));(p/'Tools/URPMigration/TransportPlan-current.json').write_text(json.dumps({'stage':str(out)},indent=2))
print(json.dumps({'plan':str(out/'plan.json'),'sha256':hashlib.sha256((out/'plan.json').read_bytes()).hexdigest(),'materials':len(rows),'families':families},indent=2))
