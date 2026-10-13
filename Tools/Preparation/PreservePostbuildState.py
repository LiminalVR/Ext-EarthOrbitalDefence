from pathlib import Path
from datetime import datetime,timezone
import json,hashlib,shutil,subprocess
p=Path(__file__).resolve().parents[2];stage=Path(json.loads((p/'Tools/URPMigration/FinalAndroid-current.json').read_text())['stage']);pre=json.loads((stage/'prebuild.json').read_text());assert (stage/'native-terminal.json').is_file() and (stage/'android-build.json').is_file()
sha=lambda f:hashlib.sha256(Path(f).read_bytes()).hexdigest();out=stage/('PostbuildState-'+datetime.now(timezone.utc).strftime('%Y%m%dT%H%M%S%fZ'));out.mkdir();before={r['path']:r for r in pre['inputs']};changes=[];inputs=[]
for folder in ['Assets','Packages','ProjectSettings']:
 for f in sorted((p/folder).rglob('*')):
  if not f.is_file():continue
  rel=f.relative_to(p).as_posix();row={'path':rel,'sha256':sha(f),'bytes':f.stat().st_size};inputs.append(row);old=before.get(rel)
  if old is None or old['sha256']!=row['sha256']:
   saved=out/'After'/rel;saved.parent.mkdir(parents=True,exist_ok=True);shutil.copy2(f,saved);changes.append({**row,'beforeSHA256':old['sha256'] if old else None,'before':old['preserved'] if old else None,'afterPreserved':str(saved)})
for rel,old in before.items():
 if not (p/rel).is_file():changes.append({'path':rel,'beforeSHA256':old['sha256'],'deleted':True})
staged=subprocess.check_output(['git','--no-optional-locks','ls-files','--stage','-z'],cwd=p);assert staged==(stage/'staging-entries.bin').read_bytes();(out/'staging-entries.bin').write_bytes(staged)
raw=json.loads((stage/'android-build.json').read_text());apk=stage/'Experience49-URP.apk';summary={'schema':1,'derivedSummary':True,'sourceReport':str(stage/'android-build.json'),'sourceReportSHA256':sha(stage/'android-build.json'),'result':raw['result'],'errors':raw['errors'],'warnings':raw['warnings'],'duration':raw['duration'],'apk':str(apk),'bytes':apk.stat().st_size if apk.is_file() else 0,'reportedTotalSize':raw['reportedTotalSize'],'apkSHA256':sha(apk) if apk.is_file() else None,'nativeErrorExceptionAssertCount':len(raw['nativeErrors']),'limits':['Actual APK file bytes separate from native build accounting. Original raw report immutable. Physical Quest not run; target providers/profile closure separate.']};(out/'android-build-summary.json').write_text(json.dumps(summary,indent=2))
report={'utc':datetime.now(timezone.utc).isoformat(),'prebuild':str(stage/'prebuild.json'),'prebuildSHA256':sha(stage/'prebuild.json'),'currentInputs':inputs,'changes':changes,'stagingEntriesUnchanged':True,'apk':summary,'limits':['Postbuild disk state preserved before any classification/restoration. No source/assets/settings restoration or SaveAssets by this read-only audit.']};(out/'report.json').write_text(json.dumps(report,indent=2));(p/'Tools/URPMigration/PostbuildState-current.json').write_text(json.dumps({'stage':str(out)},indent=2));print(json.dumps({'report':str(out/'report.json'),'sha256':sha(out/'report.json'),'changed':changes,'buildSummary':summary}))
