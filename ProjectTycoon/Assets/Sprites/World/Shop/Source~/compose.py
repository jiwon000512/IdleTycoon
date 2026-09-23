# -*- coding: utf-8 -*-
# 가게 화면 목업(540×960, PPU 80 = 1px:1px): 바닥 타일·벽·아치·진열대·계산대·오븐·손님·말풍선을 ShopBaker 좌표대로 놓는다
# 사용: compose.py <출력.png> floor=<파일> arch=<파일> shelf=<파일> oven=<파일> counter=<파일> bread=<파일> [label=A]
import sys, numpy as np
from PIL import Image, ImageDraw
SRC='C:/project/Tycoon/ProjectTycoon/Assets/Sprites/World/Shop/'
VIS='C:/project/Tycoon/ProjectTycoon/Assets/Resources/Sprites/Visitors/'
out=sys.argv[1]; o=dict(a.split('=',1) for a in sys.argv[2:])
U=80; W=540; TOP=200
rows=[('entrance',2.0),('shelf',2.4),('counter',2.6),('oven',2.0)]
H=TOP+int(sum(h for _,h in rows)*U)+80
img=Image.new('RGBA',(W,H),(122,168,82,255))
floor=Image.open(o['floor']).convert('RGBA')
wall=Image.fromarray((np.asarray(floor).astype(int)*[0.72,0.66,0.62,1]).clip(0,255).astype(np.uint8))
def tile(t,x0,y0,x1,y1):
    for y in range(y0,y1,t.height):
        for x in range(x0,x1,t.width):
            c=t.crop((0,0,min(t.width,x1-x),min(t.height,y1-y))); img.paste(c,(x,y))
def put(path,cx,bottom,scale=1.0):
    im=Image.open(path).convert('RGBA')
    if scale!=1: im=im.resize((max(1,round(im.width*scale)),max(1,round(im.height*scale))),Image.NEAREST)
    img.alpha_composite(im,(cx-im.width//2,bottom-im.height)); return im
y=TOP
# 입구: 잔디 띠 + 흙 띠(벽색) + 아치
eh=int(2.0*U); img.paste((122,168,82,255),(0,y,W,y+40)); tile(wall,0,y+40,W,y+eh); put(o['arch'],W//2,y+eh+8); y+=eh
# 나머지 구역: 바닥 타일 + 양옆 벽 20px
for name,h in rows[1:]:
    hh=int(h*U); tile(floor,0,y,W,y+hh); tile(wall,0,y,20,y+hh); tile(wall,W-20,y,W,y+hh)
    if name=='shelf':
        for side in (-1,1): put(o['shelf'],W//2+int(2.25*U)*side,y+int(1.85*U))
        # 진열대 위 빵 아이콘 + 손님(토끼 왼쪽, 펭귄 오른쪽) + 말풍선
        for side,vis in ((-1,'Rabbit'),(1,'Penguin')):
            sx=W//2+int(2.25*U)*side; put(o['bread'],sx,y+int(1.85*U)-int(0.62*U)+26)
            cx=sx-int(1.05*U)*side; c=put(f'{VIS}{vis}/{vis}.png',cx,y+int(1.85*U)+4)
            b=put('bubble.png',cx,y+int(1.85*U)+4-c.height-6); put(o['bread'],cx,y+int(1.85*U)+4-c.height-6-b.height//2+18,0.7)
    if name=='counter':
        put(o['counter'],W//2,y+int(1.55*U)); put(SRC+'wombat_front.png',W//2,y+int(2.45*U))
        put(f'{VIS}Fox/Fox_BackIdle.png'.replace('_BackIdle.png','.png'),W//2,y+int(0.55*U)+40)
    if name=='oven':
        for side in (-1,1): put(o['oven'],W//2+int(1.55*U)*side,y+int(1.75*U))
    y+=hh
tile(wall,0,y,W,y+40)
if 'label' in o:
    d=ImageDraw.Draw(img); d.rectangle([8,8,60,40],fill=(52,32,32,255)); d.text((18,14),o['label'],fill=(255,255,255,255))
img.save(out); print(out,img.size)
