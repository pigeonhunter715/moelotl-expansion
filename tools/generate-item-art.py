"""Original shape guides + local SDXL img2img. Requires Pillow, NumPy, a running ComfyUI.

No third-party images are inputs. Run --help for local paths and generation options.
"""
import argparse
import hashlib
import io
import json
from pathlib import Path
import time
import urllib.parse
import urllib.request

from PIL import Image, ImageFilter

import pendant_art
import pill_art

ROOT = Path(__file__).resolve().parents[1]
# Stable order also controls seeds. Names are file identifiers, never model prompts.
SPECS = pill_art.SPECS

def guide(spec, pendant=False):
    """Draw fresh 1024 px geometry; compositing uses its own alpha, not a borrowed mask."""
    return pendant_art.guide() if pendant else pill_art.guide(spec)


def api(path, payload=None):
    request=urllib.request.Request(ARGS.url+path, data=None if payload is None else json.dumps(payload).encode(),headers={'Content-Type':'application/json'})
    with urllib.request.urlopen(request,timeout=60) as response:return json.load(response)


def workflow(name, prompt, seed):
    return {
        '1':{'class_type':'CheckpointLoaderSimple','inputs':{'ckpt_name':'sd_xl_base_1.0.safetensors'}},
        '2':{'class_type':'LoadImage','inputs':{'image':name+'.png'}},
        '3':{'class_type':'VAEEncode','inputs':{'pixels':['2',0],'vae':['1',2]}},
        '4':{'class_type':'CLIPTextEncode','inputs':{'clip':['1',1],'text':prompt}},
        '5':{'class_type':'CLIPTextEncode','inputs':{'clip':['1',1],'text':pill_art.NEGATIVE}},
        '6':{'class_type':'KSampler','inputs':{'model':['1',0],'positive':['4',0],'negative':['5',0],'latent_image':['3',0],'seed':seed,'steps':30,'cfg':5.5,'sampler_name':'dpmpp_2m','scheduler':'karras','denoise':0.28}},
        '7':{'class_type':'VAEDecode','inputs':{'samples':['6',0],'vae':['1',2]}},
        '8':{'class_type':'SaveImage','inputs':{'images':['7',0],'filename_prefix':'MoeLotl_OriginalGuides/'+name}},
    }


def gui_graph(wf):
    """Export core-node GUI workflow from the exact API inputs used for sampling."""
    outputs={'1':[('MODEL','MODEL'),('CLIP','CLIP'),('VAE','VAE')],'2':[('IMAGE','IMAGE'),('MASK','MASK')],'3':[('LATENT','LATENT')],'4':[('CONDITIONING','CONDITIONING')],'5':[('CONDITIONING','CONDITIONING')],'6':[('LATENT','LATENT')],'7':[('IMAGE','IMAGE')],'8':[]}
    nodes=[];links=[]
    for nid,n in wf.items():
        inputs=[];widgets=[]
        for key,value in n['inputs'].items():
            if isinstance(value,list):
                src,slot=value;typ=outputs[src][slot][1];lid=len(links)+1
                links.append([lid,int(src),slot,int(nid),len(inputs),typ]);inputs.append({'name':key,'type':typ,'link':lid})
            else:
                widgets.append(value)
                if key=='seed':widgets.append('fixed')
        nodes.append({'id':int(nid),'type':n['class_type'],'pos':[0,0],'size':[380,240],'flags':{},'order':int(nid)-1,'mode':0,'inputs':inputs,'outputs':[{'name':label,'type':typ,'links':[]} for label,typ in outputs[nid]],'properties':{'Node name for S&R':n['class_type']},'widgets_values':widgets})
    for lid,src,slot,*rest in links:nodes[src-1]['outputs'][slot]['links'].append(lid)
    g={'last_node_id':8,'last_link_id':len(links),'nodes':nodes,'links':links,'groups':[],'config':{},'extra':{},'version':0.4}
    # Deterministic columns; separated vertical lanes prevent overlaps.
    places={1:(0,0),2:(0,400),3:(550,800),4:(550,0),5:(550,400),6:(1100,0),7:(1650,0),8:(2200,0)}
    for n in nodes:
        n['pos']=places[n['id']];n['size']=[400,360 if n['id'] in (4,5) else 320]
        if n['id'] in (2,8):n['size']=[400,540]
    for title,ids in [('Model and original guide',[1,2]),('Surface description and encoding',[3,4,5]),('SDXL restrained repaint',[6]),('Decode',[7]),('Save generated image',[8])]:
        block=[n for n in nodes if n['id'] in ids]
        left=min(n['pos'][0] for n in block)-20;top=min(n['pos'][1] for n in block)-70
        right=max(n['pos'][0]+n['size'][0] for n in block)+20;bottom=max(n['pos'][1]+n['size'][1] for n in block)+20
        g['groups'].append({'title':title,'bounding':[left,top,right-left,bottom-top],'color':'#44605b','font_size':22,'flags':{}})
    for i,n in enumerate(nodes):
        x,y=n['pos'];w,h=n['size']
        for m in nodes[i+1:]:
            mx,my=m['pos'];mw,mh=m['size']
            assert x+w<=mx or mx+mw<=x or y+h<=my or my+mh<=y,'Overlapping workflow nodes'
    return g


def main():
    global ARGS
    p=argparse.ArgumentParser(description=__doc__)
    p.add_argument('--comfy',type=Path,required=True)
    p.add_argument('--url',default='http://127.0.0.1:8188')
    p.add_argument('--limit',type=int,default=12)
    p.add_argument('--start',type=int,default=0)
    p.add_argument('--only',action='append',help='Optional selected Def names')
    p.add_argument('--seed',type=int,default=2026111900,help='Base seed; job index is added')
    p.add_argument('--denoise',type=float,default=0.28)
    p.add_argument('--guard-pendant',action='store_true',help='Use the cloud-scroll and fretwork pendant guide')
    p.add_argument('--output',type=Path,default=ROOT/'artifacts/art-style/generated')
    ARGS=p.parse_args()
    if not 0<=ARGS.denoise<=1:p.error('--denoise must be between 0 and 1')
    out=ARGS.output;out.mkdir(parents=True,exist_ok=True)
    jobs=[(f'MX_{s[0]}Pill',s[1],s,False,pill_art.PREFIX+s[5]+pill_art.SUFFIX) for s in SPECS]
    jobs.append(('MX_MedicinePendant','药渡玉佩',None,True,pendant_art.PROMPT))
    if ARGS.only and not set(ARGS.only)<={job[0] for job in jobs}:p.error('Unknown --only Def name')
    info=api('/object_info');manifest=[]
    for index,(name,label,spec,pendant,prompt) in enumerate(jobs):
        if index<ARGS.start or index>=ARGS.start+ARGS.limit:continue
        if ARGS.only and name not in ARGS.only:continue
        guard_pendant=pendant and ARGS.guard_pendant
        g=pendant_art.guard_guide() if guard_pendant else guide(spec,pendant)
        g.save(out/(name+'_guide.png'))
        if guard_pendant:prompt=pendant_art.GUARD_PROMPT
        image_name=name+'_guard' if guard_pendant else name
        background=Image.new('RGBA',g.size,(255,0,255,255));background.alpha_composite(g)
        background.convert('RGB').save(ARGS.comfy/'input'/(image_name+'.png'))
        wf=workflow(image_name,prompt,ARGS.seed+index)
        wf['6']['inputs']['denoise']=ARGS.denoise
        wf['4']['inputs']['text']=prompt
        wf['5']['inputs']['text']=pendant_art.NEGATIVE if pendant else pill_art.NEGATIVE
        assert all(n['class_type'] in info for n in wf.values())
        (out/(name+'_api.json')).write_text(json.dumps(wf,ensure_ascii=False,indent=2),encoding='utf-8')
        gui=gui_graph(wf)
        (out/(name+'_workflow.json')).write_text(json.dumps(gui,ensure_ascii=False,indent=2),encoding='utf-8')
        workflow_name=('2026-11-20_moelotl_guard_' if guard_pendant else '2026-11-19_moelotl_v2_')+name+'.json'
        (ARGS.comfy/'user/default/workflows'/workflow_name).write_text(json.dumps(gui,ensure_ascii=False,indent=2),encoding='utf-8')
        pid=api('/prompt',{'prompt':wf,'extra_data':{'extra_pnginfo':{'workflow':gui}}})['prompt_id']
        print('QUEUED',name,pid,flush=True);deadline=time.time()+600
        while time.time()<deadline:
            rec=api('/history/'+pid).get(pid,{})
            if rec.get('status',{}).get('status_str')=='error':raise RuntimeError(rec['status'])
            images=rec.get('outputs',{}).get('8',{}).get('images',[])
            if images:break
            time.sleep(2)
        else:raise TimeoutError(pid)
        item=images[0];query=urllib.parse.urlencode(item)
        with urllib.request.urlopen(ARGS.url+'/view?'+query,timeout=60) as f:raw=f.read()
        (out/(name+'_raw.png')).write_bytes(raw)
        result=Image.open(io.BytesIO(raw)).convert('RGBA')
        # Keep the original silhouette; low-denoise AI paints its interior only.
        # Blend perimeter back to the fresh guide to avoid chroma spill and preserve ink.
        alpha=g.getchannel('A');safe=alpha.filter(ImageFilter.MinFilter(15)).filter(ImageFilter.GaussianBlur(3))
        result=Image.composite(result,g,safe);result.putalpha(alpha)
        result=result.crop(alpha.getbbox())
        result.thumbnail((448,448),Image.Resampling.LANCZOS)
        canvas=Image.new('RGBA',(512,512))
        canvas.alpha_composite(result,((512-result.width)//2,(512-result.height)//2))
        canvas.save(out/(name+'.png'))
        manifest.append({'name':name,'label':label,'seed':ARGS.seed+index,'prompt_id':pid,'prompt':wf['4']['inputs']['text'],'sha256':hashlib.sha256(raw).hexdigest(),'denoise':ARGS.denoise,'version':2,'checkpoint':'sd_xl_base_1.0.safetensors','steps':30,'cfg':5.5,'sampler':'dpmpp_2m','scheduler':'karras','negative_prompt':wf['5']['inputs']['text'],'guide_sha256':hashlib.sha256((out/(name+'_guide.png')).read_bytes()).hexdigest(),'final_sha256':hashlib.sha256((out/(name+'.png')).read_bytes()).hexdigest()})
        if guard_pendant:manifest[-1]['version']=3
        (out/f'manifest-{name}.json').write_text(json.dumps([manifest[-1]],ensure_ascii=False,indent=2),encoding='utf-8')
        print('DONE',name,flush=True)


if __name__=='__main__':main()
