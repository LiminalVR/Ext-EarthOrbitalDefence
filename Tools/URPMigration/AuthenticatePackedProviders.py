from pathlib import Path
import json,hashlib,struct,zipfile,datetime,re
p=Path(__file__).resolve().parents[2];stage=Path(json.loads((p/'Tools/URPMigration/PackedScriptProviders-current.json').read_text())['stage']);native=json.loads((stage/'report.json').read_text());dbp=p/'Library/BuildPlayerData/Player/TypeDb-All.json';db=json.loads(dbp.read_text());sdk=next(a for a in db['root'] if a['name']=='Liminal.SDK');assert '/Release/' in sdk['path'].replace('\\','/')
sha=lambda b:hashlib.sha256(b).hexdigest();source=native['nativeCoreRelease'][0]['scripts'];coreByName={r['class']:r for r in source};coreGUID=source[0]['guid'];names={r['className'] for r in sdk['types']};blobPath=p/'Library/PlayerDataCache/Android/Data/globalgamemanagers.assets';blob=blobPath.read_bytes()
def string(b,pos):
 length=struct.unpack_from('<i',b,pos)[0];assert 0<=length<=len(b)-pos-4
 end=pos+4+length;return b[pos+4:end].decode('utf8'),(end+3)&~3
rows=[]
for r in native['rows']:
 if r['sourceAssetGUID']!=coreGUID or r['type']!='UnityEditor.MonoScript':continue
 assert r['packedAsset']=='globalgamemanagers.assets';b=blob[r['offset']:r['offset']+r['packedSize']];script,pos=string(b,0);pos+=20;cls,pos=string(b,pos);ns,pos=string(b,pos);assembly,pos=string(b,pos);assert pos==len(b)
 full=ns+'.'+cls if ns else cls;assert full in coreByName and full in names and assembly=='Liminal.SDK.dll'
 rows.append(dict(nativePackedID=r['packedID'],sourceGUID=coreGUID,sourceLocalID=coreByName[full]['localID'],className=full,assembly=assembly,packedBytesSha256=sha(b),actualTargetProvider=sdk['path'],targetClassPresent=True))
assert len(rows)==33
scene=p/'Assets/Scenes/The_Game.unity';sceneRefs=[int(m[1]) for m in re.finditer(r'm_Script: \{fileID: (-?\d+), guid: '+coreGUID,scene.read_text())];assert len(sceneRefs)>=3
authScene=[]
for localID in sceneRefs:
 match=next(r for r in rows if r['sourceLocalID']==localID);authScene.append(match)
profileEntries=[r for r in native['rows'] if r['sourceAssetPath']=='Assets/Liminal/Resources/LimappConfig.asset'];assert len(profileEntries)==1;pe=profileEntries[0];profileBlob=(p/'Library/PlayerDataCache/Android/Data'/pe['packedAsset']).read_bytes();profile=profileBlob[pe['offset']:pe['offset']+pe['packedSize']];fileID,scriptID=struct.unpack_from('<iq',profile,16);profileName,pos=string(profile,28);profileMapping=next((r for r in rows if int(r['nativePackedID'])==scriptID),None);profileResolved=fileID==1 and profileName=='LimappConfig' and profileMapping is not None and profileMapping['className']=='ExperienceProfile'
apk=Path(native['outputPath'])
with zipfile.ZipFile(apk) as z:
 partNames=[n for n in z.namelist() if n.startswith('assets/bin/Data/globalgamemanagers.assets.split')];parts=sorted(partNames,key=lambda n:int(n.rsplit('split',1)[1]));actual=b''.join(z.read(n) for n in parts) if parts else z.read('assets/bin/Data/globalgamemanagers.assets');assert actual==blob;assert z.read('assets/bin/Data/'+pe['packedAsset'])==profileBlob
report=dict(utc=datetime.datetime.now(datetime.timezone.utc).isoformat(),apk=str(apk),apkSha256=sha(apk.read_bytes()),nativeReport=str(stage/'report.json'),nativeReportSha256=sha((stage/'report.json').read_bytes()),nativeTypeDb=str(dbp),nativeTypeDbSha256=sha(dbp.read_bytes()),actualTargetProvider=sdk['path'],actualTargetProviderSha256=sha(Path(sdk['path']).read_bytes()),actualApkMonoScriptFileMatchesNativeCache=True,actualApkProfileAssetMatchesNativeCache=True,sourceCoreGUID=coreGUID,monoScriptMappings=rows,allCoreSceneScriptBindings=authScene,profileScriptReference=dict(fileID=fileID,scriptID=scriptID,name=profileName,className=profileMapping['className'] if profileMapping else None),profileResolved=profileResolved,pass_=profileResolved,limits=['Core GUID remains serialized source identity; actual packed MonoScript class/assembly resolves to Android Release provider class. No literal GUID-swap callback inferred.','APKs not installed/run on physical Quest; headset behavior remains unverified.'])
(stage/'verification.json').write_text(json.dumps(report,indent=2));print(json.dumps(dict(path=str(stage/'verification.json'),sha256=sha((stage/'verification.json').read_bytes()),mappings=len(rows),sceneClasses=[r['className'] for r in authScene])))
