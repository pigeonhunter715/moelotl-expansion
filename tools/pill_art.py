"""Original ivory pill geometry and simplified traditional motifs, without borrowed pixels."""
import math
import numpy as np
from PIL import Image, ImageDraw
from pendant_art import curve

# Stable file identifiers remain unchanged for saves and Def references.
SPECS = [
 ('Warming','暖脉丹','#F3ECDD','#B84A32','flame','A vermilion flame marking with one tall pointed curved tongue and two shorter side tongues, one ivory curved slit inside.'),
 ('Cooling','清暑丹','#F3ECDD','#286B8B','water','Three broad indigo blue horizontal flowing wave lines, smooth parallel curves, generous ivory gaps, no splashes.'),
 ('Industry','百工丹','#F3ECDD','#95652B','knot','An ochre rounded square interlaced ribbon knot, clear over and under crossings, a compact open center, few thick strokes.'),
 ('Recovery','回气丹','#F3ECDD','#397559','cloud','A muted green horizontally flowing cloud marking, rounded connected lobes with two short trailing curved strokes, generous ivory gaps, no radial arrangement.'),
 ('Combat','临战丹','#F3ECDD','#814735','tiger','Three dark red brown tapered tiger stripe markings, one downward central wedge and two outward angled side strokes, no animal face or lettering.'),
 ('Dispel','镇煞丹','#F3ECDD','#395E69','sword','A small dark teal straight sword shaped marking, one pointed blade, one short crossguard, two short curved strokes beside it.'),
 ('Capacity','纳煞丹','#F3ECDD','#6D5289','vessel','A dark violet outline of a closed narrow necked vessel with a broad rounded belly and a small rim, ivory interior, no liquid level.'),
 ('Purify','涤煞丹','#F3ECDD','#3E587D','meander','A closed dark blue circular boundary band with four short right angle inward meanders, a large empty ivory center, no numbers or gauge.'),
 ('Gather','聚煞丹','#F3ECDD','#814B78','hooks','Three plum purple hooked comma shaped cloud curls arranged around a small ivory center, all tips curving inward, thick separated strokes.'),
 ('Release','归真丹','#F3ECDD','#3D6262','duality','A compact circular motif split by a smooth S curve into two interlocking teardrop shapes, one dark teal and one ivory, each with a small contrasting dot, no outer symbols.'),
 ('Rescue','安魂丹','#F3ECDD','#856082','lotus','A muted mauve lotus marking with three broad pointed round upright petals, open symmetrical silhouette, no elaborate stamens or circular frame.'),
]
PREFIX=('A single milky ivory round medicinal pellet, smooth waxy surface, clear charcoal outer contour, '
        'broad soft shading, small white highlight on the upper right, resting on folded beige paper, '
        'slightly elevated front view, simple hand drawn 2D inventory icon. ')
SUFFIX=(' The colored motif is a flat surface marking following the sphere, crisp broad strokes, '
        'most of the pellet stays ivory. Isolated on flat magenta background.')
NEGATIVE=('photograph, photorealism, metal, jewelry, glowing aura, text, letters, watermark, '
          'scenery, face, eyes on a face, gritty noise, cracks, colored pellet body, extra pills, '
          'complex background, floating symbol, raised badge, sticker border')


def motif_mask(kind):
    """Large simple silhouettes with negative-space gaps survive inventory downsampling."""
    mask=Image.new('L',(1024,1024));d=ImageDraw.Draw(mask)
    cx,cy=500,533
    def pts(points):return [(round(cx+x),round(cy+y)) for x,y in points]
    def line(points,width=24,fill=255):d.line(pts(points),fill=fill,width=width,joint='curve')
    def poly(points,fill=255):d.polygon(pts(points),fill=fill)
    def ellipse(box,fill=None,outline=None,width=1):d.ellipse((cx+box[0],cy+box[1],cx+box[2],cy+box[3]),fill=fill,outline=outline,width=width)
    def rect(box,radius=0,fill=None,outline=None,width=1):d.rounded_rectangle((cx+box[0],cy+box[1],cx+box[2],cy+box[3]),radius=radius,fill=fill,outline=outline,width=width)
    if kind=='flame':
        poly(curve((0,113),((-66,117),(-108,80),(-94,20)),((-90,0),(-72,-17),(-71,-42)),((-60,-19),(-46,0),(-42,18)),((-34,-40),(12,-54),(9,-123)),((57,-91),(71,-48),(59,-8)),((81,-22),(88,-42),(88,-58)),((117,-1),(111,62),(68,94)),((45,109),(23,115),(0,113))))
        poly(curve((-9,82),((-42,60),(-31,26),(-12,6)),((-14,36),(18,41),(13,69)),((7,82),(2,87),(-9,82))),0)
    elif kind=='water':
        for y in [-67,0,67]:
            line(curve((-118,y+12),((-82,y-30),(-51,y-30),(-12,y)),((31,y+31),(65,y+26),(114,y-18))),25)
    elif kind=='knot':
        rect((-110,-52,110,52),29,outline=255,width=23)
        rect((-52,-110,52,110),29,outline=255,width=23)
        # Alternate over/under at the four intersections, preserving ribbon continuity.
        for x,y,horizontal in [(-41,-41,True),(41,-41,False),(41,41,True),(-41,41,False)]:
            if horizontal:
                line([(x-23,y),(x+23,y)],37,0);line([(x-25,y),(x+25,y)],23)
            else:
                line([(x,y-23),(x,y+23)],37,0);line([(x,y-25),(x,y+25)],23)
    elif kind=='cloud':
        line(curve((-110,27),((-139,-4),(-104,-46),(-68,-34)),((-65,-103),(18,-111),(35,-54)),((76,-77),(115,-45),(98,-12)),((128,7),(107,42),(73,41)),((32,40),(2,34),(-28,37))),24)
        line(curve((-90,76),((-40,98),(15,77),(56,79)),((74,80),(83,89),(99,92))),23)
        line(curve((-66,20),((-61,-5),(-28,-6),(-16,10))),19)
    elif kind=='tiger':
        poly(curve((-23,-113),((-6,-98),(8,-102),(24,-113)),((21,-59),(12,-8),(0,36)),((-13,-10),(-23,-66),(-23,-113))))
        for sign in [-1,1]:
            poly([(sign*x,y) for x,y in curve((29,-15),((62,-22),(89,-51),(116,-78)),((115,-28),(90,9),(68,38)),((50,57),(43,85),(39,108)),((24,71),(15,34),(29,-15)))])
    elif kind=='sword':
        poly([(0,-125),(-25,-83),(-18,48),(18,48),(25,-83)])
        line([(-61,51),(61,51)],23);line([(0,58),(0,104)],22);ellipse((-18,94,18,120),fill=255)
        for sign in [-1,1]:line([(sign*x,y) for x,y in curve((83,3),((64,-12),(75,-42),(95,-37)))],18)
    elif kind=='vessel':
        path=curve((-33,-109),((-26,-77),(-27,-57),(-55,-33)),((-123,-5),(-105,76),(-67,99)),((-32,118),(34,118),(67,99)),((105,76),(123,-5),(55,-33)),((27,-57),(26,-77),(33,-109)))
        line(path,25);line([(-49,-109),(49,-109)],24)
        line([(-35,-67),(35,-67)],20)
    elif kind=='meander':
        ellipse((-116,-116,116,116),outline=255,width=24)
        for angle in [0,90,180,270]:
            a=math.radians(angle)
            points=[(-20,-105),(-20,-67),(22,-67),(22,-39),(-3,-39)]
            line([(x*math.cos(a)-y*math.sin(a),x*math.sin(a)+y*math.cos(a)) for x,y in points],19)
    elif kind=='hooks':
        hook=curve((-38,-106),((16,-126),(79,-96),(75,-54)),((73,-15),(19,-9),(14,-40)),((10,-57),(25,-68),(39,-62)))
        for angle in [0,120,240]:
            a=math.radians(angle)
            line([(x*math.cos(a)-y*math.sin(a),x*math.sin(a)+y*math.cos(a)) for x,y in hook],25)
    elif kind=='duality':
        r=108
        ellipse((-r,-r,r,r),outline=255,width=14)
        # Two halves formed geometrically; ivory removes one small lobe from the dark half.
        d.pieslice((cx-r,cy-r,cx+r,cy+r),90,270,fill=255)
        ellipse((-54,-108,54,0),fill=255)
        ellipse((-54,0,54,108),fill=0)
        ellipse((-13,-67,13,-41),fill=0);ellipse((-13,41,13,67),fill=255)
    elif kind=='lotus':
        for sign in [-1,1]:
            leaf=curve((sign*19,76),((sign*38,9),(sign*77,-25),(sign*113,-47)),((sign*119,23),(sign*90,88),(sign*19,76)))
            poly(leaf)
        poly(curve((0,62),((-52,10),(-39,-56),(0,-115)),((39,-56),(52,10),(0,62))))
        # Separate petals with ivory channels, rather than a solid purple blob.
        line(curve((-14,74),((-19,50),(-28,29),(-39,9))),12,0)
        line(curve((14,74),((19,50),(28,29),(39,9))),12,0)
        line(curve((-83,111),((-29,133),(29,133),(83,111))),18)
    else:raise ValueError(kind)
    return mask


def guide(spec):
    _,_,_,accent,kind,_=spec
    im=Image.new('RGBA',(1024,1024));d=ImageDraw.Draw(im);ink='#302f32'
    # Same original folded-paper composition, with slightly stronger contact separation.
    d.polygon([(155,670),(579,508),(865,703),(430,858)],fill='#d4ccb9',outline=ink,width=14)
    d.polygon([(170,672),(435,829),(430,848)],fill='#a69e91')
    d.polygon([(451,810),(679,669),(837,704)],fill='#b4aea0')
    d.ellipse((339,660,704,747),fill='#8c877b')
    d.ellipse((307,316,709,721),fill=ink)
    yy,xx=np.mgrid[:1024,:1024];inside=((xx-508)/188)**2+((yy-515)/190)**2<=1
    t=np.clip((yy-380)/315,0,1)*.74+np.clip((480-xx)/210,0,1)*.16
    top=np.array([249,244,231]);bottom=np.array([197,187,170])
    colors=np.clip(top[None,None,:]*(1-t[:,:,None])+bottom[None,None,:]*t[:,:,None],0,255).astype('uint8')
    arr=np.array(im);arr[inside,:3]=colors[inside];arr[inside,3]=255;im=Image.fromarray(arr)
    # The pigment inherits gentle spherical shading; every gap reveals the actual body.
    pigment=np.array(tuple(bytes.fromhex(accent[1:])),float)
    lighting=np.clip(1.04-(yy-410)/850+(xx-500)/2200,.78,1.10)
    tint=Image.fromarray(np.clip(pigment[None,None,:]*lighting[:,:,None],0,255).astype('uint8')).convert('RGBA')
    mask=motif_mask(kind);tint.putalpha(mask);im.alpha_composite(tint)
    d=ImageDraw.Draw(im);d.ellipse((610,369,635,414),fill='#fffdf2');d.ellipse((638,431,649,444),fill='#fffdf2')
    return im
