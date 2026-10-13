from pathlib import Path
from datetime import datetime,timezone
import hashlib,json,shutil,subprocess
p=Path(__file__).resolve().parents[2]
asset=p/'Assets/Liminal/Resources/LimappConfig.asset';meta=Path(str(asset)+'.meta')
old=b'm_Script: {fileID: 481487832, guid: a80148f7c24f27741a9721c40a216ac9, type: 3}'
new=old.replace(b'a80148f7c24f27741a9721c40a216ac9',b'1661fa78af9aece459a3302206a05902')
b=asset.read_bytes();assert b.count(old)==1 and new not in b
stage=p/'Builds/URPMigration'/('ProfileBindingRepair-'+datetime.now(timezone.utc).strftime('%Y%m%dT%H%M%S%fZ'));stage.mkdir()
def sha(f):return hashlib.sha256(Path(f).read_bytes()).hexdigest()
idx=Path(subprocess.run(['git','--no-optional-locks','-C',str(p),'rev-parse','--path-format=absolute','--git-path','index'],check=True,capture_output=True,text=True).stdout.strip())
for f in (asset,meta):shutil.copy2(f,stage/f.name)
shutil.copy2(idx,stage/'git-index-before')
(stage/'tracked-before.patch').write_bytes(subprocess.run(['git','--no-optional-locks','-C',str(p),'diff','--no-ext-diff','--no-textconv','--binary'],check=True,capture_output=True).stdout)
candidate=b.replace(old,new);assert candidate.replace(new,old)==b
(stage/'LimappConfig.candidate.asset').write_bytes(candidate)
r={'utc':datetime.now(timezone.utc).isoformat(),'sourcePath':str(asset),'beforeSHA256':sha(asset),'candidateSHA256':sha(stage/'LimappConfig.candidate.asset'),'metaSHA256':sha(meta),'indexPath':str(idx),'indexSHA256':sha(idx),'localID':481487832,'oldGUID':'a80148f7c24f27741a9721c40a216ac9','newGUID':'1661fa78af9aece459a3302206a05902','allNonScriptBytesIdentical':True,'nativeBefore':'Builds/URPMigration/NativeStartup-20261002T234923148Z/report.json','limits':'Native loaded source profile is null; authenticated Core ExperienceProfile exactGUID/localID/class. OnlyscriptGUID repair authorized. Nativeimport and packedtarget proof remain separate.'}
(stage/'preservation.json').write_text(json.dumps(r,indent=2))
asset.write_bytes(candidate)
assert sha(meta)==r['metaSHA256'] and sha(idx)==r['indexSHA256']
(p/'Tools/URPMigration/ProfileRepair-current.json').write_text(json.dumps({'stage':str(stage)},indent=2))
print(json.dumps({'stage':str(stage),'candidateSHA256':sha(asset)},indent=2))
