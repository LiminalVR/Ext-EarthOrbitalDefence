"""Project/PID-authenticated official Pipeline commands; credentials remain private."""
from pathlib import Path
from datetime import datetime, timezone
import json, sys, urllib.request, urllib.error
project = Path(__file__).resolve().parents[2]
entry = sys.argv[1]
lease = json.loads((project/'Tools/URPMigration/SourceSession-current.json').read_text(encoding='utf-8-sig'))
port = json.loads((project/'Library/Pipeline/.unity-pipeline-port').read_text(encoding='utf-8-sig'))
assert port['pid'] == lease['pid'] and Path(port['projectPath']).resolve() == project.resolve()
argv = ['run_script','--file','Tools/URPMigration/'+entry.split('.')[0]+'.cs','--entry',entry] if '.' in entry else [entry]
request = urllib.request.Request('http://127.0.0.2:'+str(port['port'])+'/api/exec',data=json.dumps({'argv':argv}).encode(),headers={'Authorization':'Bearer '+port['evalToken'],'Content-Type':'application/json'})
opener = urllib.request.build_opener(urllib.request.ProxyHandler({}))
out = project/'Builds/URPMigration/Commands';out.mkdir(exist_ok=True)
path = out/(datetime.now(timezone.utc).strftime('%Y%m%dT%H%M%S%fZ')+'-official-loopback-'+entry+'.json')
try:
    with opener.open(request, timeout=40) as response: result = json.load(response)
except urllib.error.HTTPError as error:
    body=error.read().decode('utf-8',errors='replace').replace(port['evalToken'],'[redacted]')
    result={'success':False,'transportError':type(error).__name__,'message':str(error),'httpStatus':error.code,'httpBody':body,'pid':lease['pid'],'entry':entry,'limits':'Preserved transport error body; not native completion or authorization to retry mutations.'}
except Exception as error:
    result={'success':False,'transportError':type(error).__name__,'message':str(error),'pid':lease['pid'],'entry':entry,'limits':'Transport timeout/failure is not native command completion. No mutation retry implied.'}
path.write_text(json.dumps({'route':'Official project/PID-authenticated127.0.0.2 HttpListener','response':result},indent=2),encoding='utf-8')
print(json.dumps({'path':str(path),'response':result},indent=2))
raise SystemExit(1 if result.get('success') is False or (result.get('result') or {}).get('success') is False else 0)
