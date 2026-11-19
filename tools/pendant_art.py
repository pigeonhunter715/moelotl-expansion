"""Original bamboo jade pendant guide; no museum pixels or traced contours."""
import numpy as np
from PIL import Image, ImageDraw, ImageFilter

PROMPT = ('A small flat thick green jade pendant, softly tapered shoulders and rounded bottom corners, '
          'carved narrow bamboo border, three pointed bamboo leaves in shallow green relief, '
          'one curved diagonal jointed stem, sparse fine warm gold inlaid lines, smooth waxy jade with broad pale green highlights, '
          'small integrated pierced suspension ear, natural curved burgundy braided cord, one short soft burgundy tassel, '
          'front view, restrained hand-painted 2D game item illustration, clear dark outer contour, fine interior carving, '
          'large quiet green areas, isolated on flat magenta background.')
NEGATIVE = ('photograph, photorealism, text, letters, logo, watermark, bottle, stopper, cap, padlock, '
            'square inset panel, central hole, gold frame, metallic body, elaborate filigree, scenery, '
            'extra pendants, extra tassels, fuzzy edges, scratched noisy surface')
GUARD_PROMPT = ('Hand-painted 2D inventory icon of a rounded green jade waist pendant. Preserve the diagonal bamboo '
                'and the deep green shadow along the jade rim. Carve round cloud scrolls into the open corners, a '
                'repeating angular fret band at the top and foot, and tiny leaf veins. Set a few muted gold strokes '
                'into the carving. Soft jade bevels, rich emerald surface, clear large forms, dark ink contour, '
                'pierced suspension ear, burgundy cord and single fan tassel, isolated on a flat magenta field.')


def curve(start, *segments):
    points = [start]
    p0 = np.array(start, float)
    for a,b,c in segments:
        a,b,c = np.array(a,float),np.array(b,float),np.array(c,float)
        for t in np.linspace(0,1,36)[1:]:
            p=(1-t)**3*p0+3*(1-t)**2*t*a+3*(1-t)*t*t*b+t**3*c
            points.append(tuple(p))
        p0=c
    return points


def guide():
    im=Image.new('RGBA',(1024,1024))
    d=ImageDraw.Draw(im)
    ink='#283c37'
    def line(pts,fill,width):
        d.line(pts,fill=fill,width=width,joint='curve')
    def cord(pts,width=13):
        line(pts,ink,width+6);line(pts,'#8d4546',width)
        line([(x+2,y-1) for x,y in pts],'#b96e60',max(2,width//4))
    # Cord loop is curved, with the rear leg behind the integral jade eye.
    cord(curve((492,245),((450,198),(461,123),(515,111)),((558,102),(552,179),(523,238))),10)
    # Bottom cord and one bound fan of fine silk strands, behind the body.
    cord(curve((513,711),((491,756),(536,758),(519,798))),10)
    fan=curve((510,804),((496,833),(489,872),(472,918)),((492,929),(513,934),(534,929)),((552,922),(562,916),(568,908)),((545,874),(543,833),(532,804)),((523,799),(516,800),(510,804)))
    d.polygon(fan,fill='#873c47');line(fan,ink,7)
    for k in range(7):
        line(curve((512+k*3,816),((511+k*3,852),(485+k*11,894),(480+k*13,920-k%3*3))),['#b56561','#a45156','#663441'][k%3],3)
    d.ellipse((504,784,538,817),fill='#934b48',outline=ink,width=5)
    d.arc((507,788,536,812),10,175,fill='#d1ab65',width=4)
    # Raised integral ear, with genuine transparent opening.
    ear=curve((475,310),((459,277),(463,229),(488,219)),((506,209),(531,214),(547,230)),((565,252),(559,286),(548,310)),((523,318),(498,316),(475,310)))
    d.polygon(ear,fill='#5b9a7d');line(ear,ink,8)
    d.arc((477,228,550,301),195,340,fill='#c1d5a0',width=5)
    d.ellipse((490,235,536,277),fill=(0,0,0,0))
    d.arc((488,233,538,279),0,360,fill='#315648',width=6)
    cord(curve((516,213),((512,226),(506,247),(504,263))),8)
    # Original body silhouette: narrow shoulders, broad abdomen, rounded lower corners.
    body=curve((407,292),((443,276),(568,274),(608,292)),((646,304),(658,337),(667,372)),((686,432),(679,515),(684,572)),((693,638),(678,699),(645,720)),((602,746),(417,746),(377,721)),((340,698),(333,643),(343,575)),((351,510),(339,435),(355,374)),((364,337),(372,308),(407,292)))
    mask=Image.new('L',im.size);ImageDraw.Draw(mask).polygon(body,fill=255)
    yy,xx=np.mgrid[:1024,:1024]
    light=(0.94+0.19*(550-yy)/350+0.13*(xx-490)/210
           +0.07*np.exp(-((xx-560)**2+(yy-375)**2)/18000)
           -0.06*np.exp(-((xx-390)**2+(yy-600)**2)/13000))
    colors=np.clip(np.array([104,163,129])[None,None,:]*light[:,:,None],0,255).astype('uint8')
    surface=Image.fromarray(colors).convert('RGBA');surface.putalpha(mask);im.alpha_composite(surface);d=ImageDraw.Draw(im)
    line(body,ink,10)
    # Quiet jade bevels: a dark lower rim and broad pale upper-right edge.
    line(curve((355,593),((345,655),(365,706),(399,715)),((457,734),(586,733),(635,711)),((665,699),(675,659),(670,622))),'#396d58',16)
    line(curve((414,301),((472,286),(559,287),(605,302)),((638,315),(649,343),(653,375))),'#c5d7ac',9)
    line(curve((659,394),((670,464),(658,514),(667,560))),'#9ec5a0',8)
    # Two slender edge rails with a few bamboo joints, never a central boxed panel.
    rail=curve((396,328),((367,390),(376,446),(368,488)),((360,557),(376,624),(390,673)))
    line(rail,'#467f64',9);line([(x+6,y) for x,y in rail],'#96bc88',5)
    rail2=curve((628,351),((648,417),(637,486),(649,544)),((660,607),(640,650),(622,679)))
    line(rail2,'#3f775f',7);line([(x-5,y-2) for x,y in rail2],'#a4c795',4)
    for x,y in [(370,449),(371,569),(640,465),(649,575)]:
        line(curve((x-9,y),((x-3,y+4),(x+8,y+3),(x+12,y-2))),'#3c7059',5)
        line(curve((x-8,y-5),((x,y-2),(x+7,y-3),(x+10,y-5))),'#b4cf9e',3)
    # Thin gold guide paths along the margin; discontinuous, not a gold lid/frame.
    line(curve((404,342),((431,320),(457,318),(472,321))),'#796739',6)
    line(curve((404,340),((431,318),(457,316),(472,319))),'#d9bd74',4)
    line(curve((426,692),((469,712),(559,710),(598,688))),'#796739',6)
    line(curve((426,689),((469,709),(559,707),(598,685))),'#d6b76b',4)
    # Shallow carved bamboo: diagonal jointed stem and three lance-shaped leaves.
    stem=curve((430,674),((461,613),(477,550),(493,495)),((515,434),(542,389),(570,351)))
    line([(x+4,y+3) for x,y in stem],'#3b725a',14)
    line(stem,'#9fc68e',10);line([(x-2,y-2) for x,y in stem],'#c1d6a5',3)
    for x,y in [(476,551),(513,446),(546,388)]:
        line([(x-9,y-4),(x+9,y+2)],'#416f56',5);line([(x-8,y-7),(x+10,y-1)],'#cfddb0',3)
    leaves=[curve((517,443),((485,430),(431,388),(420,354)),((468,366),(511,399),(517,443))),
            curve((500,490),((521,439),(575,419),(614,422)),((589,461),(541,484),(500,490))),
            curve((466,590),((428,574),(406,527),(407,490)),((443,513),(466,551),(466,590)))]
    for leaf in leaves:
        shifted=[(x+4,y+4) for x,y in leaf]
        d.polygon(shifted,fill='#3c735b');d.polygon(leaf,fill='#94bd85');line(leaf,'#5a916b',3)
        a=leaf[0];tip=leaf[35]
        line(curve(a,((a[0]*.66+tip[0]*.34,a[1]*.66+tip[1]*.34),(a[0]*.33+tip[0]*.67,a[1]*.33+tip[1]*.67),tip)),'#c0d6a2',3)
    # A fine inlay follows part of the stem, not every leaf.
    line(curve((447,645),((460,614),(467,590),(472,572))),'#d7ba75',4)
    # Restrained opening seam at the upper shoulder, not an external cap.
    line(curve((411,310),((460,298),(548,299),(598,311))),'#51846a',3)
    d.ellipse((598,323,610,347),fill='#e4e7be')
    return im


def guard_guide():
    image = guide()
    draw = ImageDraw.Draw(image)

    def incise(points, color='#9bc79b', width=7):
        shadow = [(x, y + 3) for x, y in points]
        draw.line(shadow, fill='#356c57', width=width + 3, joint='curve')
        draw.line(points, fill=color, width=width, joint='curve')

    # Wide scrolls echo the bamboo without crossing its stems or leaves.
    incise(curve((389, 474), ((404, 469), (397, 449), (415, 443)),
                 ((433, 435), (450, 447), (445, 461)),
                 ((442, 472), (424, 474), (421, 462)),
                 ((417, 455), (425, 448), (431, 452))), '#a0c696', 8)
    incise(curve((520, 600), ((539, 584), (564, 599), (579, 580)),
                 ((588, 568), (580, 551), (591, 538)),
                 ((606, 518), (633, 531), (631, 548)),
                 ((630, 559), (608, 563), (604, 549)),
                 ((602, 542), (612, 538), (616, 543))), '#acd39c', 10)
    incise(curve((510, 629), ((536, 607), (578, 628), (601, 601)),
                 ((610, 593), (615, 581), (615, 575))), '#82b58a', 5)

    # Stepped borders and small brass inlays sit inside the jade bevel.
    for x in (465, 500, 535):
        incise([(x, 325), (x, 337), (x + 22, 337), (x + 22, 325), (x + 10, 325)], '#c6b471', 5)
    for x in (464, 500, 536, 572):
        incise([(x, 660), (x, 677), (x + 25, 677), (x + 25, 660), (x + 12, 660)], '#a9c597', 6)
    for x, y in ((449, 336), (570, 334), (447, 676), (608, 667)):
        draw.polygon([(x, y - 6), (x + 6, y), (x, y + 6), (x - 6, y)], fill='#d6b977')
    return image
