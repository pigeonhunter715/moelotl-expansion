"""Original shape guides + local SDXL img2img. Requires Pillow, NumPy, a running ComfyUI.

No third-party images are inputs. Run --help for local paths and generation options.
"""
import argparse
import hashlib
import io
import json
import math
from pathlib import Path
import time
import urllib.parse
import urllib.request

import numpy as np
from PIL import Image, ImageDraw, ImageFilter, ImageFont

import pendant_art

ROOT = Path(__file__).resolve().parents[1]
# Stable order also controls seeds. Names are file identifiers, never model prompts.
SPECS = [
    ('Warming', '暖脉丹', '#cf6642', '#ffe2a0', 'grain', 'warm terracotta red sphere, three fine curved ochre streaks, smooth waxy surface'),
    ('Cooling', '清暑丹', '#78bfd1', '#e4f6e6', 'vein', 'pale cyan sphere, thin branching ivory veins, cool opaque polished surface'),
    ('Industry', '百工丹', '#cfad57', '#48664a', 'leaf', 'honey ochre sphere, simple dark green leaf-shaped mark, smooth dense surface'),
    ('Recovery', '回气丹', '#469d7e', '#e2efb0', 'spiral', 'deep jade green sphere, one pale yellow curled stripe, softly translucent surface'),
    ('Combat', '临战丹', '#b94842', '#edc778', 'chevron', 'brick red sphere, two short angular gold streaks, smooth lacquered surface'),
    ('Dispel', '镇煞丹', '#864e43', '#eadbb4', 'band', 'dark reddish brown sphere, thin ivory curved band with a short vertical line, smooth opaque surface'),
    ('Capacity', '纳煞丹', '#65547e', '#b9c4d3', 'arcs', 'dark muted violet sphere, three nested silver gray arcs, dense dark surface'),
    ('Purify', '涤煞丹', '#e7d699', '#b48036', 'wave', 'pale ivory gold sphere, two flowing ochre curved stripes, creamy smooth surface'),
    ('Gather', '聚煞丹', '#875487', '#c2a0cf', 'spiral', 'muted plum purple sphere, a lavender inward spiral stripe, opaque polished surface'),
    ('Release', '归真丹', '#a5b6bb', '#526379', 'split', 'blue gray sphere, a single dark blue S-shaped curved stripe, smooth satin surface'),
    ('Rescue', '安魂丹', '#d79589', '#fff1cb', 'petals', 'peach pink sphere, three small ivory petal-shaped marks, softly glazed surface'),
]
NEGATIVE = 'photograph, photorealism, metallic filigree, text, letters, watermark, frame, scenery, gritty noise, extra objects'
STYLE = ' Simple hand drawn 2D object illustration, heavy charcoal contour, broad flat shading, small white highlight on upper right, readable smooth silhouette, slight elevated view. Isolated on flat magenta background.'


def rgb(h):
    return tuple(bytes.fromhex(h.lstrip('#')))


def guide(spec, pendant=False):
    """Draw fresh 1024 px geometry; compositing uses its own alpha, not a borrowed mask."""
    im = Image.new('RGBA', (1024, 1024))
    d = ImageDraw.Draw(im)
    ink = '#29272c'
    if pendant:
        return pendant_art.guide()
    _,_,base,accent,motif,_ = spec
    # A fresh irregular folded paper shape, not traced from the reference.
    d.polygon([(155,670),(579,508),(865,703),(430,858)],fill='#d4ccb9',outline=ink,width=14)
    d.polygon([(170,672),(435,829),(430,848)],fill='#a69e91')
    d.polygon([(451,810),(679,669),(837,704)],fill='#b4aea0')
    d.ellipse((365,665,671,746),fill='#918c81')
    box=(342,318,674,720)
    d.ellipse(box,fill=ink)
    yy,xx=np.mgrid[0:1024,0:1024]
    inside=((xx-508)/151)**2+((yy-519)/186)**2<=1
    light=np.clip(0.94+0.23*(518-yy)/186+0.09*(xx-508)/151,0.60,1.18)
    arr=np.array(im);shade=np.clip(np.array(rgb(base))[None,None,:]*light[:,:,None],0,255).astype('uint8')
    arr[inside,:3]=shade[inside];arr[inside,3]=255;im=Image.fromarray(arr);d=ImageDraw.Draw(im)
    def line(points,width=9): d.line(points,fill=accent,width=width,joint='curve')
    if motif=='grain':
        for off in (0,44,83):line([(411+off,382),(391+off,436),(424+off,497),(408+off,543),(445+off,599),(455+off,655)],6)
    elif motif=='vein':
        line([(469,380),(495,452),(472,512),(510,574),(487,653)],8)
        line([(491,450),(545,420),(563,386)],6);line([(476,515),(416,541),(399,590)],6)
    elif motif=='leaf':
        d.polygon([(448,603),(421,526),(454,460),(541,431),(565,509),(529,568)],fill=accent)
        line([(445,583),(535,454)],6)
    elif motif=='spiral':
        pts=[]
        for t in np.linspace(0,2.1*math.pi,100):
            r=103*(1-t/(2.5*math.pi));pts.append((int(508+r*math.cos(t)),int(528+r*math.sin(t))))
        line(pts,10)
    elif motif=='chevron':
        line([(421,482),(493,538),(585,458)],17);line([(440,567),(504,611),(566,558)],13)
    elif motif=='band':
        d.arc((365,392,649,591),0,180,fill=accent,width=14);line([(508,451),(508,612)],11)
    elif motif=='arcs':
        for inset in (0,34,67):d.arc((398+inset,410+inset,613-inset,649-inset),65,295,fill=accent,width=10)
    elif motif=='wave':
        for off in (0,70):line([(398,480+off),(449,457+off),(504,490+off),(553,478+off),(598,449+off)],13)
    elif motif=='split':
        line([(528,390),(468,432),(464,474),(539,534),(551,584),(492,645)],20)
    elif motif=='petals':
        for b in [(487,438,528,548),(437,500,486,577),(532,500,581,577)]:d.ellipse(b,fill=accent)
    d.ellipse((565,384,600,463),fill='#fff4db')
    d.ellipse((595,477,611,494),fill='#fff6e7')
    return im


def api(path, payload=None):
    request=urllib.request.Request(ARGS.url+path, data=None if payload is None else json.dumps(payload).encode(),headers={'Content-Type':'application/json'})
    with urllib.request.urlopen(request,timeout=60) as response:return json.load(response)


def workflow(name, prompt, seed):
    return {
        '1':{'class_type':'CheckpointLoaderSimple','inputs':{'ckpt_name':'sd_xl_base_1.0.safetensors'}},
        '2':{'class_type':'LoadImage','inputs':{'image':name+'.png'}},
        '3':{'class_type':'VAEEncode','inputs':{'pixels':['2',0],'vae':['1',2]}},
        '4':{'class_type':'CLIPTextEncode','inputs':{'clip':['1',1],'text':prompt+STYLE}},
        '5':{'class_type':'CLIPTextEncode','inputs':{'clip':['1',1],'text':NEGATIVE}},
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
    p.add_argument('--seed',type=int,default=2026111900,help='Base seed; job index is added')
    p.add_argument('--denoise',type=float,default=0.28)
    p.add_argument('--output',type=Path,default=ROOT/'artifacts/art-style/generated')
    ARGS=p.parse_args();out=ARGS.output;out.mkdir(parents=True,exist_ok=True)
    jobs=[(f'MX_{s[0]}Pill',s[1],s,False,s[5]+' resting on a folded beige paper square.') for s in SPECS]
    jobs.append(('MX_MedicinePendant','药渡玉佩',None,True,pendant_art.PROMPT))
    info=api('/object_info');manifest=[]
    for index,(name,label,spec,pendant,prompt) in enumerate(jobs):
        if index<ARGS.start or index>=ARGS.start+ARGS.limit:continue
        g=guide(spec,pendant);g.save(out/(name+'_guide.png'))
        background=Image.new('RGBA',g.size,(255,0,255,255));background.alpha_composite(g)
        background.convert('RGB').save(ARGS.comfy/'input'/(name+'.png'))
        wf=workflow(name,prompt,ARGS.seed+index)
        wf['6']['inputs']['denoise']=ARGS.denoise
        if pendant:
            wf['4']['inputs']['text']=prompt
            wf['5']['inputs']['text']=pendant_art.NEGATIVE
        assert all(n['class_type'] in info for n in wf.values())
        (out/(name+'_api.json')).write_text(json.dumps(wf,ensure_ascii=False,indent=2),encoding='utf-8')
        gui=gui_graph(wf)
        (ARGS.comfy/'user/default/workflows'/('moelotl_original_'+name+'.json')).write_text(json.dumps(gui,ensure_ascii=False,indent=2),encoding='utf-8')
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
        manifest.append({'name':name,'label':label,'seed':ARGS.seed+index,'prompt_id':pid,'prompt':wf['4']['inputs']['text'],'sha256':hashlib.sha256(raw).hexdigest(),'denoise':ARGS.denoise})
        (out/f'manifest-{ARGS.start}.json').write_text(json.dumps(manifest,ensure_ascii=False,indent=2),encoding='utf-8')
        print('DONE',name,flush=True)


if __name__=='__main__':main()
