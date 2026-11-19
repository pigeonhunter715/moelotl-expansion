"""Rebuild transparent finals and review sheets from original guides and SDXL outputs.

Usage: python tools/review-item-art.py [--source PATH] [--publish]
Publish only after visually reviewing art-preview.png and art-small.png.
"""
import argparse
import importlib.util
import json
from pathlib import Path

import numpy as np
from PIL import Image, ImageDraw, ImageFilter, ImageFont

ROOT=Path(__file__).resolve().parents[1]


def finish(raw,guide):
    alpha=guide.getchannel('A')
    safe=alpha.filter(ImageFilter.MinFilter(15)).filter(ImageFilter.GaussianBlur(3))
    result=Image.composite(raw,guide,safe)
    result.putalpha(alpha)
    result=result.crop(alpha.getbbox())
    result.thumbnail((448,448),Image.Resampling.LANCZOS)
    canvas=Image.new('RGBA',(512,512))
    canvas.alpha_composite(result,((512-result.width)//2,(512-result.height)//2))
    return canvas


def main():
    p=argparse.ArgumentParser(description=__doc__)
    p.add_argument('--source',type=Path,default=ROOT/'artifacts/art-style/generated')
    p.add_argument('--publish',action='store_true')
    args=p.parse_args()
    spec=importlib.util.spec_from_file_location('art',ROOT/'tools/generate-item-art.py')
    art=importlib.util.module_from_spec(spec);spec.loader.exec_module(art)
    names=[(f'MX_{s[0]}Pill',s[1]) for s in art.SPECS]+[('MX_MedicinePendant','药渡玉佩')]
    if args.publish:
        assert all((args.source/(name+'_raw.png')).exists() and (args.source/(name+'_guide.png')).exists() for name,_ in names),'Incomplete batch'
    font_path=Path('C:/Windows/Fonts/msyh.ttc')
    font=ImageFont.truetype(str(font_path),22) if font_path.exists() else ImageFont.load_default()
    fontsmall=ImageFont.truetype(str(font_path),14) if font_path.exists() else ImageFont.load_default()
    sheet=Image.new('RGB',(1000,870),'#272b32');small=Image.new('RGB',(800,690),'#f1eadb')
    d=ImageDraw.Draw(sheet);ds=ImageDraw.Draw(small)
    for i,(name,label) in enumerate(names):
        rawpath=args.source/(name+'_raw.png');guidepath=args.source/(name+'_guide.png')
        if not rawpath.exists():continue
        raw=Image.open(rawpath).convert('RGBA');guide=Image.open(guidepath).convert('RGBA')
        assert raw.size==guide.size==(1024,1024)
        im=finish(raw,guide);im.save(args.source/(name+'.png'))
        a=np.array(im.getchannel('A'));border=np.concatenate((a[0],a[-1],a[:,0],a[:,-1]))
        assert a.min()==0 and a.max()==255 and not border.any(),name
        assert 0.07<(a>0).mean()<0.7,(name,'alpha occupancy')
        x=i%4*250;y=i//4*290
        thumb=im.resize((224,224),Image.Resampling.LANCZOS);sheet.paste(thumb,(x+13,y+8),thumb)
        d.text((x+18,y+243),label,font=font,fill='white')
        x=i%4*200;y=i//4*230
        for size,dx,dy in [(128,5,3),(64,130,3),(32,145,82)]:
            thumb=im.resize((size,size),Image.Resampling.LANCZOS);small.paste(thumb,(x+dx,y+dy),thumb)
        ds.text((x+10,y+142),label,font=font,fill='#30343b')
        ds.text((x+10,y+175),'128 / 64 / 32 px',font=fontsmall,fill='#555555')
        if args.publish:
            dest=ROOT/'1.6/Textures/Things/Item/MoeLotl';dest.mkdir(parents=True,exist_ok=True)
            im.save(dest/(name+'.png'))
    sheet.save(args.source/'art-preview.png');small.save(args.source/'art-small.png')
    print(args.source/'art-preview.png');print(args.source/'art-small.png')
    if args.publish:
        assert all((args.source/(name+'_raw.png')).exists() for name,_ in names),'Incomplete batch'
        dest=ROOT/'docs/art';dest.mkdir(exist_ok=True)
        sheet.save(dest/'art-preview.png');small.save(dest/'art-small.png')
        entries={}
        for f in sorted(args.source.glob('manifest-*.json')):
            for r in json.loads(f.read_text(encoding='utf-8')):entries[r['name']]=r
        (dest/'generation.json').write_text(json.dumps(list(entries.values()),ensure_ascii=False,indent=2),encoding='utf-8')
        print('Published 12 validated RGBA textures and review sheets.')


if __name__=='__main__':main()
