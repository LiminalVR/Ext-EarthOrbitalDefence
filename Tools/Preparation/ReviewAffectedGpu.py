from pathlib import Path
from datetime import datetime,timezone
import json,hashlib,sys
from PIL import Image,ImageDraw
p=Path(__file__).resolve().parents[2];stage=Path(sys.argv[1]);r=json.loads((stage/'report.json').read_text());assert not (stage/'review.json').exists()
image=Image.new('RGB',(1120,580),(28,28,28));draw=ImageDraw.Draw(image)
summary=[]
for k in range(2):
 rows=r['rows'][k*4:k*4+4];draw.text((8,k*290+2),rows[0]['sourceIdentity']['name']+' | '+' / '.join(m['mode'] for m in rows),fill='white')
 for i,row in enumerate(rows):
  image.paste(Image.open(row['image']).convert('RGB'),(i*280+8,k*290+25));summary.append({'name':row['sourceIdentity']['name'],'mode':row['mode'],'meanRGB':row.get('meanRGB'),'maxRGB':row.get('maxRGB'),'peak':row['maxRGBReadback'],'linear':row['linearFlag'],'temperature':row['temperatureFlag'],'image':row['image'],'imageSHA256':hashlib.sha256(Path(row['image']).read_bytes()).hexdigest()})
contact=stage/'contact.jpg';image.save(contact,quality=94)
doc={'utc':datetime.now(timezone.utc).isoformat(),'rawReport':str(stage/'report.json'),'rawSHA256':hashlib.sha256((stage/'report.json').read_bytes()).hexdigest(),'summary':summary,'errors':r['errors'],'restores':r['restores'],'contact':str(contact),'correctionToRawLimits':'Supplementary stopped actual sphere with authenticated normal-Starter activated cloned directional light; normal-play LightingContext-20261003T004055337Z proves1active light. Camera-begin scope guaranteesactual0/0 flags. ShaderUtil.CompilePass(material,0,forceSync:true) beforeFIRST draw, native API reflectionauthenticated inLightingContext-004027805. Originalfailedin-PlayEditorSceneManager.NewScene commandretained; no authscene/light changes. These are true source/stock/candidate0/0 rows, NOT1/1 or coldproduction. All prior firstdraw failuresretained; synchronous variant preparation removestransient instability inthisnarrowfixture, notproofcoldimportedproduction.','limits':['Authentic source equations, materials, textures, native normal/parallax and unmodified light values; no artwork tuning.','Source/native/candidate actual mesh and wholecontext/fog/depth/XR/nativeGLES/currentcoldplayer remain open.','All14Standard/spec current identities have reflectionenabled; reflectedcube branch exercised, reflection-off provider notbound/accepted.']}
(stage/'review.json').write_text(json.dumps(doc,indent=2));print(json.dumps({'review':str(stage/'review.json'),'sha256':hashlib.sha256((stage/'review.json').read_bytes()).hexdigest(),'contact':str(contact),'summary':summary},indent=2))
