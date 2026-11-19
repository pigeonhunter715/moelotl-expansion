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
GUARD_PROMPT = ('Hand-painted 2D inventory icon of a rounded green jade pendant with a thin soft gold line inset '
                'along its outer bevel. Three visibly separate bamboo stalks of different lengths rise toward the '
                'upper right at slightly different angles, overlapping in shallow jade relief. Each stalk has bamboo '
                'joints and a few narrow pointed leaves. Two small '
                'curled cloud engravings flank the bamboo in the open green areas. Polished emerald jade, clear dark '
                'silhouette, pierced suspension ear, burgundy cord and one silk tassel, flat magenta background.')
GUARD_NEGATIVE = ('text, letters, watermark, Greek key, geometric fret, dot inlay, gold lattice, thick metal frame, '
                  'extra stalks, opposed stems, busy ornament, flower, logo, gold leaves, photographic background')


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


def guide(include_face=True):
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
    if not include_face:
        return im
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
    image = guide(include_face=False)
    draw = ImageDraw.Draw(image)

    rim = curve((409, 311), ((456, 293), (567, 295), (603, 311)),
                ((639, 322), (649, 356), (653, 387)),
                ((669, 446), (657, 515), (665, 580)),
                ((675, 643), (657, 686), (632, 702)),
                ((592, 725), (424, 727), (386, 702)),
                ((355, 681), (352, 642), (360, 581)),
                ((369, 516), (357, 435), (372, 376)),
                ((381, 340), (388, 323), (409, 311)))
    draw.line(rim, fill='#66764b', width=10, joint='curve')
    draw.line(rim, fill='#c6a768', width=6, joint='curve')

    def relief(points, color, width):
        draw.line([(x + 3, y + 3) for x, y in points], fill='#356a55', width=width + 3, joint='curve')
        draw.line(points, fill=color, width=width, joint='curve')

    for x, y in ((395, 397), (590, 493)):
        scroll = curve((x, y + 19), ((x + 18, y + 16), (x + 12, y - 3), (x + 32, y - 6)),
                       ((x + 52, y - 11), (x + 62, y + 4), (x + 53, y + 13)),
                       ((x + 44, y + 23), (x + 30, y + 16), (x + 36, y + 7)),
                       ((x + 39, y + 4), (x + 44, y + 4), (x + 46, y + 8)))
        relief(scroll, '#9bc39a', 8)

    def leaf(base, tip, width, color):
        bx, by = base
        tx, ty = tip
        dx, dy = tx - bx, ty - by
        length = np.hypot(dx, dy)
        nx, ny = -dy * width / length, dx * width / length
        mx, my = bx + 0.55 * dx, by + 0.55 * dy
        shape = curve(base, ((mx + nx, my + ny), (tx + nx / 2, ty + ny / 2), tip),
                      ((tx - nx / 2, ty - ny / 2), (mx - nx, my - ny), base))
        draw.polygon([(x + 3, y + 3) for x, y in shape], fill='#376c56')
        draw.polygon(shape, fill=color)
        draw.line((base, tip), fill='#bbd4a2', width=2)

    def stalk(joints, leaves, color, width):
        relief(joints, color, width)
        for (x, y), (nx, ny) in zip(joints[1:-1], joints[2:]):
            length = np.hypot(nx - x, ny - y)
            dx, dy = (ny - y) * width * 0.72 / length, (x - nx) * width * 0.72 / length
            draw.line(((x - dx, y - dy), (x + dx, y + dy)), fill='#406f52', width=5)
        for base, tip in leaves:
            leaf(base, tip, width, color)

    # Stagger the intersections while all three stalks rise toward the upper right.
    stalk([(493, 672), (501, 591), (511, 518), (533, 415)],
          [((529, 434), (478, 397)), ((501, 591), (569, 555))], '#8db985', 12)
    stalk([(399, 666), (445, 615), (479, 570), (519, 525)],
          [((445, 615), (410, 566)), ((479, 570), (436, 532))], '#a2c995', 10)
    stalk([(432, 679), (472, 591), (508, 506), (549, 422), (587, 337)],
          [((549, 422), (522, 365)), ((567, 381), (627, 351)), ((527, 467), (579, 438))], '#b0d39b', 15)
    return image
