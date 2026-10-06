# 캡처 자르기 · 나란히 붙이기(Windows Python + Pillow).
#   shots.py crop <in.png> <out.png> <x> <y> <w> <h> [배율]   dev.sh rect 결과를 그대로 넘긴다. 배율은 정수 확대(nearest)
#   shots.py pair <out.png> <a.png> <b.png> [...]              같은 높이로 맞춰 가로로 붙이고 아래에 파일 이름을 적는다
import sys
from PIL import Image, ImageDraw

def crop(src, dst, x, y, w, h, scale=1):
    im = Image.open(src).convert("RGBA")
    x, y, w, h, scale = (int(float(v)) for v in (x, y, w, h, scale))
    im = im.crop((max(0, x), max(0, y), min(im.width, x + w), min(im.height, y + h)))
    if scale > 1:
        im = im.resize((im.width * scale, im.height * scale), Image.NEAREST)
    im.save(dst)

def pair(dst, *srcs):
    ims = [Image.open(s).convert("RGBA") for s in srcs]
    h = max(i.height for i in ims)
    ims = [i if i.height == h else i.resize((i.width * h // i.height, h), Image.NEAREST) for i in ims]
    gap, label = 12, 28
    out = Image.new("RGBA", (sum(i.width for i in ims) + gap * (len(ims) - 1), h + label), (32, 32, 32, 255))
    draw, x = ImageDraw.Draw(out), 0
    for s, i in zip(srcs, ims):
        out.paste(i, (x, 0))
        draw.text((x + 4, h + 6), s.replace("\\", "/").split("/")[-1], fill=(230, 230, 230, 255))
        x += i.width + gap
    out.save(dst)

if __name__ == "__main__":
    {"crop": crop, "pair": pair}[sys.argv[1]](*sys.argv[2:])
