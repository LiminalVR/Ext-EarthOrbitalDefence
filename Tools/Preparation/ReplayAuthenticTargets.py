"""Genuine native consumed response files; fresh local source providers per target."""
from pathlib import Path
from datetime import datetime,timezone
import json,hashlib,subprocess,re,shutil,sys
p=Path(__file__).resolve().parents[2];target=sys.argv[1];assert target in ['Editor','Android']
out=p/'Builds/URPMigration'/('Authentic'+target+'Targets-'+datetime.now(timezone.utc).strftime('%Y%m%dT%H%M%S%fZ'));out.mkdir()
u=Path('C:/Program Files/Unity/Hub/Editor/6000.7.0b2/Editor/Data');sha=lambda f:hashlib.sha256(f.read_bytes()).hexdigest()
dag='1300b0aEDbg.dag' if target=='Editor' else '1300b0aPDev.dag'
names=['LiminalSdk','App000000000049','App000000000049.Editor','Editor'] if target=='Editor' else ['LiminalSdk','App000000000049']
providers={};reports=[]
for name in names:
 native=p/'Library/Bee/artifacts'/dag/(name+'.rsp');assert native.is_file(),str(native)
 lines=native.read_text(encoding='utf-8-sig').splitlines();assert ('-define:UNITY_EDITOR' in lines)==(target=='Editor')
 if target=='Android':assert '-define:UNITY_ANDROID' in lines
 stage=out/name;stage.mkdir();shutil.copy2(native,stage/'native-consumed.rsp');args=[];sources=[];refs=[];uses=[]
 for line in lines:
  if line.startswith('-out:'):line='-out:"'+str(stage/(name+'.dll'))+'"'
  elif line.startswith('-refout:'):line='-refout:"'+str(stage/(name+'.ref.dll'))+'"'
  elif line.startswith('-r:'):
   f=Path(line[3:].strip('"'));f=f if f.is_absolute() else p/f
   if f.name in providers:uses.append({'original':str(f),'fresh':str(providers[f.name])});f=providers[f.name];line='-r:"'+str(f)+'"'
   assert f.is_file(),str(f);refs.append({'path':str(f),'sha256':sha(f)})
  elif line.endswith('.cs"'):
   f=Path(line.strip('"'));f=f if f.is_absolute() else p/f;assert f.is_file(),str(f);sources.append({'path':str(f),'sha256':sha(f)})
  args.append(line)
 assert sources,name
 if name=='App000000000049':assert any(s['path'].endswith('AppController.cs') for s in sources)
 rsp=stage/'replay.rsp';rsp.write_text('\n'.join(args));result=subprocess.run([str(u/'DotNetSdk/dotnet.exe'),str(u/'DotNetSdk/sdk/10.0.303/Roslyn/bincore/csc.dll'),'@'+str(rsp)],cwd=p,capture_output=True,text=True,encoding='utf-8');log=result.stdout+result.stderr;(stage/'compiler.log').write_text(log)
 report={'target':target,'assembly':name,'nativeOptions':str(native),'nativeOptionsSha256':sha(native),'replayOptionsSha256':sha(rsp),'sources':sources,'references':refs,'freshProviderUses':uses,'exitCode':result.returncode,'errors':len(re.findall(r'\berror [A-Z]+\d+:',log)),'warnings':len(re.findall(r'\bwarning [A-Z]+\d+:',log)),'changes':'Only output/refout paths and already fresh source provider paths; exact native source/defines/platform/options/package/precompiled references retained.','outputs':[]}
 if result.returncode==0:
  for suffix in ['.dll','.ref.dll']:
   f=stage/(name+suffix);assert f.is_file();report['outputs'].append({'path':str(f),'sha256':sha(f)});providers[f.name]=f
 (stage/'compile.json').write_text(json.dumps(report,indent=2));reports.append({'target':target,'assembly':name,'path':str(stage/'compile.json'),'sha256':sha(stage/'compile.json'),'exitCode':result.returncode,'errors':report['errors'],'warnings':report['warnings']});print(json.dumps(reports[-1]),flush=True)
 if result.returncode:break
summary={'utc':datetime.now(timezone.utc).isoformat(),'reports':reports,'allPassed':len(reports)==len(names) and all(r['exitCode']==0 for r in reports),'limits':'Matching genuine native target scopes with fresh source providers; Android options only after actual BuildPlayer generated them. No invented references/targetdefines or cached Assets-owned substitution. No hardware acceptance.'}
(out/'summary.json').write_text(json.dumps(summary,indent=2));(p/('Tools/URPMigration/Authentic'+target+'Targets-current.json')).write_text(json.dumps({'stage':str(out),'sha256':sha(out/'summary.json')},indent=2));print(json.dumps({'stage':str(out),'summarySHA256':sha(out/'summary.json'),'allPassed':summary['allPassed']}))
