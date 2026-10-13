from pathlib import Path
from datetime import datetime,timezone
import hashlib,json,subprocess
p=Path(__file__).resolve().parents[2]
out=p/'Builds/URPMigration'/('SourceRouteSeal-'+datetime.now(timezone.utc).strftime('%Y%m%dT%H%M%S%fZ'));out.mkdir()
route=p/'Builds/URPMigration/SourceRoute-20261003T000144154Z/observation.json';r=json.loads(route.read_text())
def sha(f):return hashlib.sha256(Path(f).read_bytes()).hexdigest()
def rec(f):return {'path':str(f),'bytes':Path(f).stat().st_size,'sha256':sha(f)}
assert r['completed'] and r['sdkComplete'] and r['sdkResult'] and not r['errors']
images=[rec(Path(i['path'])) for i in r['images']]
before=json.loads((p/'Builds/URPMigration/DesktopPreparation-20261002T233926522886Z/review.json').read_text())
changed=[]
for row in before['currentFiles']:
 f=p/row['path']
 if row['path'].startswith('Tools/'):continue
 current=sha(f) if f.is_file() else None
 if current!=row['sha256']:changed.append({'path':row['path'],'before':row['sha256'],'after':current})
idx=Path(before['gitIndex']['path'])
index_audit_path=Path(json.loads((p/'Tools/URPMigration/SourceIndexAndCanvas-current.json').read_text())['stage'])/'report.json'
index_audit=json.loads(index_audit_path.read_text());assert index_audit['stagedEntriesUnchanged'] and sha(idx)==index_audit['currentIndex']['sha256']
cs=[x for x in before['currentFiles'] if x['path'].startswith('Assets/') and x['path'].endswith('.cs')]
assert all(sha(p/x['path'])==x['sha256'] for x in cs)
related=[p/'Builds/URPMigration/SourceSmoke-20261002T235746093Z/observation.json',p/'Builds/URPMigration/NativeTypedScope-20261002T234922585Z/report.json',p/'Builds/URPMigration/NativeSourceAppearance-20261003T000336489Z/appearance.json',p/'Builds/URPMigration/NativeStartup-20261002T234923148Z/report.json',p/'Builds/URPMigration/ProfileBindingRepair-20261002T235014405992Z/native-import.json']
record={'utc':datetime.now(timezone.utc).isoformat(),'sourceOnly':True,'pid':r['pid'],'route':rec(route),'elapsed':r['elapsed'],'authoredGameTime':r['gameTime'],'sdkComplete':r['sdkComplete'],'sdkResult':r['sdkResult'],'observerErrorCount':len(r['errors']),'imageAttempts':len(images),'uniqueImageHashes':len({x['sha256'] for x in images}),'images':images,'relatedEvidence':[rec(x) for x in related],'all26AssetsCSharpUnchanged':True,'indexBytesUnchanged':index_audit['indexBytesUnchanged'],'stagedEntriesUnchanged':True,'indexAudit':rec(index_audit_path),'indexSHA256':sha(idx),'sourceImportAndProfileChanges':changed,'lastGameplaySample':r['samples'][-1],
 'visualReview':{'bodySourceViews':['03-Body-Cube (1)-0.png','04-Body-Cube (1)-1.png','06-Body-StarSparrow2 Variant-0.png','07-Body-StarSparrow2 Variant-1.png','11-Body-Asteroid_01_S 1 Variant-0.png','12-Body-Asteroid_01_S 1 Variant-1.png'],'bodyReview':'Firstviewofeach reviewed; allthree actualspawnbodiesvisiblytextured.11Asteroidincludesrealexplosionandblueparticleeffect/beam. Supplementarycamera-framedviews, originalplayerposerestored.','naturalReview':'Space/earth/turret/roomvisible; firstLaser/Impactlabels do not independentlyestablish visiblebeam. SDKComplete actualblack.','timerCaptureLimit':'17GameTime180 displayed01:00 while recordedCurrentTime180.004; actualUIcache/sourceformat attribution pending narrowproof. Do notclaimsameframetimerappearance fromlabel.','scoreCaptureLimit':'19ScoreDisplay partialstrings after6s fromending (clear5s thenstaggeredtextwrite); expectedwrite-in-progress isplausible, no sourceUIstrings retainedinfullroute. No completefinalscorevisualclaim.'},
 'limits':['Historical source26managedchecks reused unchanged; new nativeSDK/providerclosure separate.','SavedEditorcontrollerpose/publicSDKAutoClicker+ordinaryUIpointerclick simulation; realSphereCast/Kill/healthcollisions followunchangedsource. No unaidedhardwareclaim.','Allthreefamilyevents/firstbodyviews; multiplelaterinstances/lifecycle/effectpoolsourcevariantcoverage pendingnarrowdiagnostics, notprovedbyfilename.','Normalhealthlossobservedfrom300to285 inroute; separateordinaryno-fire branch reached63.7806 end/menu/0shots/NaNaccuracy duringCanvasdiagnostic butdidnotretaincompleteSDKcallback.','No URPconversion/freshURPrender/gameplay/player/Questacceptance yet.']}
(out/'report.json').write_text(json.dumps(record,indent=2),encoding='utf-8')
(p/'Tools/URPMigration/SourceRouteSeal-current.json').write_text(json.dumps({'stage':str(out)},indent=2))
print(json.dumps({'path':str(out/'report.json'),'sha256':sha(out/'report.json'),'images':len(images),'uniqueImageHashes':record['uniqueImageHashes'],'changedProductionInputs':len(changed),'changedPaths':[x['path'] for x in changed]},indent=2))
