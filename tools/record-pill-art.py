"""Record selected pill-v2 workflows and verify their provenance before publication."""
from pathlib import Path
import hashlib,json,shutil
from PIL import Image
import numpy as np
ROOT=Path(__file__).resolve().parents[1]

def main():
 import argparse
 p=argparse.ArgumentParser(description=__doc__);p.add_argument('--source',type=Path,required=True);a=p.parse_args()
 import pill_art
 target=ROOT/'docs/art/pills-v2';target.mkdir(parents=True,exist_ok=True)
 for spec in pill_art.SPECS:
  name='MX_'+spec[0]+'Pill';folder=a.source
  paths={k:folder/(name+suffix) for k,suffix in [('raw','_raw.png'),('guide','_guide.png'),('final','.png'),('api','_api.json'),('gui','_workflow.json')]}
  assert all(p.exists() for p in paths.values()),name
  manifest=folder/('manifest-'+name+'.json');entry=json.loads(manifest.read_text(encoding='utf-8'))[0]
  wf=json.loads(paths['api'].read_text(encoding='utf-8'));sampler=wf['6']['inputs']
  assert hashlib.sha256(paths['raw'].read_bytes()).hexdigest()==entry['sha256']
  for key,field in [('guide','guide_sha256'),('final','final_sha256')]:
   if field in entry:assert hashlib.sha256(paths[key].read_bytes()).hexdigest()==entry[field],(name,field)
  assert sampler['seed']==entry['seed'] and sampler['denoise']==entry['denoise']
  for key in ['guide','final']:
   im=Image.open(paths[key]);assert im.mode=='RGBA';assert im.size==((1024,1024) if key=='guide' else (512,512))
  alpha=np.array(Image.open(paths['final']).getchannel('A'));assert not np.concatenate([alpha[0],alpha[-1],alpha[:,0],alpha[:,-1]]).any()
  assert alpha.max()==255 and 0.07<(alpha>0).mean()<0.7
  gui=json.loads(paths['gui'].read_text(encoding='utf-8'));nodes=gui['nodes']
  for i,n in enumerate(nodes):
   x,y=n['pos'];w,h=n['size']
   for m in nodes[i+1:]:
    mx,my=m['pos'];mw,mh=m['size'];assert x+w<=mx or mx+mw<=x or y+h<=my or my+mh<=y
  for key,suffix in [('api','-api.json'),('gui','-workflow.json')]:shutil.copy2(paths[key],target/(name+suffix))
  entry.update({'version':2,'checkpoint':wf['1']['inputs']['ckpt_name'],'steps':sampler['steps'],'cfg':sampler['cfg'],'sampler':sampler['sampler_name'],'scheduler':sampler['scheduler'],'negative_prompt':wf['5']['inputs']['text'],'guide_source':'tools/pill_art.py','guide_sha256':hashlib.sha256(paths['guide'].read_bytes()).hexdigest(),'final_sha256':hashlib.sha256(paths['final'].read_bytes()).hexdigest(),'api_workflow':'pills-v2/'+name+'-api.json','gui_workflow':'pills-v2/'+name+'-workflow.json'})
  manifest.write_text(json.dumps([entry],ensure_ascii=False,indent=2),encoding='utf-8')
  print('PASS',name,'RGBA / source hashes / sampler / workflow layout')

if __name__=='__main__':main()
