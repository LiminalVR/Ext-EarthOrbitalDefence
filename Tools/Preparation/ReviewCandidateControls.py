from pathlib import Path
from datetime import datetime,timezone
import json,hashlib
from PIL import Image,ImageDraw
p=Path(__file__).resolve().parents[2];out=p/'Builds/URPMigration'/('CandidateControlsReview-'+datetime.now(timezone.utc).strftime('%Y%m%dT%H%M%S%fZ'));out.mkdir()
def sha(f):return hashlib.sha256(Path(f).read_bytes()).hexdigest()
def report(name):return json.loads((p/'Builds/URPMigration'/name/'report.json').read_text())
summary=[];images=[]
def pairs_from(stage,accept):
 d=report(stage);pairs=[]
 for i,r in enumerate(d['rows']):
  if r['mode']=='Source00' and accept(r):pairs.append((r,d['rows'][i+1]))
 assert not d['errors'] and all(d['restores'].values())
 summary.append({'report':str(p/'Builds/URPMigration'/stage/'report.json'),'sha256':sha(p/'Builds/URPMigration'/stage/'report.json'),'errors':len(d['errors']),'restores':d['restores'],'pairs':len(pairs)})
 return pairs
def pages(prefix,pairs):
 for k in range(0,len(pairs),4):
  rows=pairs[k:k+4];im=Image.new('RGB',(580,len(rows)*286),(26,26,26));dr=ImageDraw.Draw(im)
  for y,(s,c) in enumerate(rows):
   dr.text((8,y*286+3),s['sourceIdentity']['name']+' | source / candidate | '+str(s.get('effectTime',s.get('distance'))),fill='white')
   for x,r in enumerate((s,c)):
    im.paste(Image.open(r['image']).convert('RGB'),(x*290+8,y*286+24));images.append({'image':r['image'],'sha256':sha(r['image']),'mode':r['mode'],'name':r['sourceIdentity']['name'],'effectTime':r.get('effectTime'),'distance':r.get('distance'),'meanRGB':r.get('meanRGB'),'maxRGB':r.get('maxRGB'),'maxAlpha':r.get('maxAlpha'),'peak':r['maxRGBReadback']})
  im.save(out/(prefix+'-'+str(k//4+1)+'.jpg'),quality=94)
pages('particle-and-kept-source',pairs_from('ParticleGpu-20261003T010452348Z',lambda r:r['distance']==1.5 and r['sourceIdentity']['shader']!='Particles/Standard Surface'))
pages('corrected-surface',pairs_from('ParticleGpu-20261003T010725744Z',lambda r:r['distance']==1.5))
pages('actual-particle-point',pairs_from('PointGpu-20261003T011517760Z',lambda r:True))
pages('surface-vertex-point',pairs_from('PointSurfaceGpu-20261003T012606068Z',lambda r:True))
uniform=p/'Builds/URPMigration/PointSurfaceGpu-20261003T012515449Z/report.json';native=json.loads(uniform.read_text())['sourceVertexUniforms']
gles=p/'Builds/URPMigration/PbrNativeCompile-20261003T005737245Z/report.json';g=json.loads(gles.read_text());subset=[r for r in g['rows'] if r['platform']=='GLES3x'];assert all(r['success'] and r['bytes'] for r in subset)
doc={'utc':datetime.now(timezone.utc).isoformat(),'reports':summary,'images':images,'sourceVertexReadback':{'report':str(uniform),'sha256':sha(uniform),'values':native,'derivation':'Actual source ForwardBase/noForwardAdd proxy per-draw coefficient .01 at range50 equals25/range²; native point position(0,0,-2) and color(8.429932,2.13538957,.0109600937) match URP culling/upload. Own Surface vertex loop reads own URP per-object light indices/range/color and source reciprocal, max4; no borrowed unity_4Light globals.'},'glesSubset':{'rawReport':str(gles),'rawSHA256':sha(gles),'successfulGLESRows':len(subset),'failedD3DAndroidExternalRequests':sum(not r['success'] for r in g['rows'] if r['platform']!='GLES3x'),'classification':'42 relevant GLES nonempty successes;42 D3D+Android external requests false/empty/no messages retained, not a shader-error cause or whole mixed-target success.'},'rawProvenanceCorrections':['Point raw limits mention all effect renderers forceRenderingOff: corrected fixture keeps actual light-emitter renderer visible so its authentic particle light enters native culling; other effect renderers remain isolated.','Source vertex uniform is a native same-renderer ForwardBase one-pass proxy, not disassembly of original Standard Surface variant. Actual Source shader pragma noforwardadd and point-removal/native images independently support transport.','Final Surface reciprocal candidate packet supersedes vertex candidate using URP range-edge fading; source coefficient .01 independently measured.','Initial Surface Fade inherited preserve-specular setting changed source SrcBlend5 to1; corrected transfer preserves Fade SrcAlpha, original art/scalars/alpha. Failed shader-token point stages retained.'],'limits':['Controlled textured quad/sphere fixtures; not natural effect lifecycle or actual Main/cold/player acceptance.','Source180.004/fullSDK ending unchanged; no duplicate route.','Flat clear distortion proof does not cover transparent-before-effect GrabPass differences.','Raw HDR peak/error retained; saturated RGB8 contacts alone do not prove float parity.','Kept Source Sprites/UnlitColor/SDKUI have exact controlled source/URP RGBA; skybox/cold/device/XR still separate.']}
(out/'review.json').write_text(json.dumps(doc,indent=2));print(json.dumps({'review':str(out/'review.json'),'sha256':sha(out/'review.json'),'contacts':[str(f) for f in out.glob('*.jpg')]}))
