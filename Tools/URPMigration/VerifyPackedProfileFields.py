from pathlib import Path
import json,struct,hashlib
p=Path(__file__).resolve().parents[2];stage=Path(json.loads((p/'Tools/URPMigration/PackedScriptProviders-current.json').read_text())['stage']);native=json.loads((stage/'report.json').read_text());dbp=p/'Library/BuildPlayerData/Player/TypeDb-All.json';db=json.loads(dbp.read_text());sdk=next(a for a in db['root'] if a['name']=='Liminal.SDK');types={t['className']:t for t in sdk['types']};buildStage=Path(json.loads((p/'Tools/URPMigration/FinalAndroid-current.json').read_text())['stage']);jsonp=buildStage/'native-before.json';values=json.loads(json.loads(jsonp.read_text())['profileJSON'])['MonoBehaviour'];pe=next(r for r in native['rows'] if r['sourceAssetPath']=='Assets/Liminal/Resources/LimappConfig.asset');blob=(p/'Library/PlayerDataCache/Android/Data'/pe['packedAsset']).read_bytes()[pe['offset']:pe['offset']+pe['packedSize']];length=struct.unpack_from('<i',blob,28)[0];cursor=(32+length+3)&~3;rows=[]
def read(typename,value,path):
 global cursor
 start=cursor
 if typename=='System.Single':expected=struct.pack('<f',value);n=4
 elif typename in ['System.Int32','System.UInt32']:expected=struct.pack('<i' if typename=='System.Int32' else '<I',value);n=4
 elif typename=='System.Boolean':expected=bytes([1 if value else 0]);n=1
 elif typename in ['UnityEngine.Vector2','UnityEngine.Vector3','UnityEngine.Vector4','UnityEngine.Color']:
  keys={'UnityEngine.Vector2':'xy','UnityEngine.Vector3':'xyz','UnityEngine.Vector4':'xyzw','UnityEngine.Color':'rgba'}[typename];expected=struct.pack('<'+'f'*len(keys),*[value[k] for k in keys]);n=len(expected)
 elif typename=='UnityEngine.Rendering.RenderPipelineAsset':assert value['instanceID']==0;expected=bytes(12);n=12
 elif isinstance(value,dict):
  for f in types[typename]['fieldInfos']:read(f['type'],value[f['name']],path+'.'+f['name'])
  return
 else:assert isinstance(value,int);expected=struct.pack('<i',value);n=4 # Native serialized enum underlying int32.
 actual=blob[cursor:cursor+n];assert actual==expected,(path,cursor,expected.hex(),actual.hex());cursor+=n;cursor=(cursor+3)&~3;rows.append(dict(field=path,type=typename,offset=start,value=value,expectedHex=expected.hex(),actualHex=actual.hex(),success=True))
for f in types['ExperienceProfile']['fieldInfos']:read(f['type'],values[f['name']],f['name'])
assert cursor==len(blob),(cursor,len(blob));(stage/'profile-packed-440bytes.bin').write_bytes(blob)
sha=lambda f:hashlib.sha256(f.read_bytes()).hexdigest();report=dict(nativeTypeDb=str(dbp),nativeTypeDbSha256=sha(dbp),nativeProfileJson=str(jsonp),nativeProfileJsonSha256=sha(jsonp),packedProfileBytes=len(blob),packedProfileSha256=hashlib.sha256(blob).hexdigest(),checks=rows,failedChecks=[],actualFieldsMatch=True,scope='All six ExperienceProfile nested setting families, every leaf, native player TypeDb field order, exact float/int/enum/PPtr/boolean bytes and complete440-byte boundary. Original authored settings preserved; target SDK binding repaired separately.')
(stage/'profile-field-verification.json').write_text(json.dumps(report,indent=2));print(json.dumps(dict(path=str(stage/'profile-field-verification.json'),sha256=sha(stage/'profile-field-verification.json'),checks=len(rows))))
