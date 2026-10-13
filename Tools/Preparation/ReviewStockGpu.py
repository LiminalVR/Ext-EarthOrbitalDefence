from pathlib import Path
from datetime import datetime,timezone
import json,hashlib,sys
from PIL import Image,ImageDraw
p=Path(__file__).resolve().parents[2]
stage=Path(sys.argv[1]) if len(sys.argv)>1 else p/'Builds/URPMigration/StockGpu-20261003T002251521Z';r=json.loads((stage/'report.json').read_text())
assert not (stage/'review-summary.json').exists(), 'Immutable review already exists'
summary=[]
for k in range(0,len(r['rows']),3):
 rows=r['rows'][k:k+3];summary.append({'name':rows[0]['sourceIdentity']['name'],'path':rows[0]['sourceIdentity']['path'],'shader':rows[0]['sourceIdentity']['shader'],'secondMode':rows[1]['mode'],'thirdMode':rows[2]['mode'],'secondMean':rows[1]['meanRGB'],'thirdMean':rows[2]['meanRGB'],'secondMax':rows[1]['maxRGB'],'thirdMax':rows[2]['maxRGB'],'sourcePeak':rows[0]['maxRGBReadback'],'thirdPeak':rows[2]['maxRGBReadback']})
pages=[]
for start in range(0,len(summary),6):
 entries=summary[start:start+6];image=Image.new('RGB',(840,len(entries)*290),(28,28,28));draw=ImageDraw.Draw(image)
 for row,e in enumerate(entries):
  draw.text((8,row*290+2),e['name']+' | '+' / '.join(m['mode'] for m in r['rows'][(start+row)*3:(start+row+1)*3]),fill='white')
  for col,mode in enumerate(r['rows'][(start+row)*3:(start+row+1)*3]):image.paste(Image.open(mode['image']).convert('RGB'),(col*280+8,row*290+25))
 path=stage/('contact-'+str(len(pages)+1)+'.jpg');image.save(path,quality=93);pages.append(str(path))
record={'utc':datetime.now(timezone.utc).isoformat(),'rawReport':str(stage/'report.json'),'rawSHA256':hashlib.sha256((stage/'report.json').read_bytes()).hexdigest(),'renders':len(r['rows']),'errors':r['errors'],'restores':r['restores'],'summary':summary,'contacts':pages,'limits':r['limits']}
(stage/'review-summary.json').write_text(json.dumps(record,indent=2));print(json.dumps(record,indent=2))
