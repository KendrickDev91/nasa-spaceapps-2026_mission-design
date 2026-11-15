"""Miniatura de YouTube (1280×720) y portada vertical (1080×1920) a partir de los planos del rodaje.
uso: python thumbnails.py <Recordings/Plates> <carpeta_salida>
"""
import os
import sys
from PIL import Image, ImageDraw, ImageFilter, ImageFont

plates, out = sys.argv[1], sys.argv[2]
os.makedirs(out, exist_ok=True)
FONT = "C:/Windows/Fonts/impact.ttf"
FONT2 = "C:/Windows/Fonts/ariblk.ttf"
YELLOW, BLUE = (255, 210, 63), (140, 210, 255)


def frame(plate, n):
    return Image.open(os.path.join(plates, plate, f"frame_{n:05d}.jpg")).convert("RGB")


def moon_cutout(img, box):
    """Recorta la Luna (zona brillante y gris) dentro de box y devuelve imagen RGBA con máscara circular suave."""
    x0, y0, x1, y1 = box
    region = img.crop(box)
    g = region.convert("L")
    px = g.load()
    xs, ys = [], []
    for y in range(region.height):
        for x in range(region.width):
            if px[x, y] > 70:
                r, gg, b = region.getpixel((x, y))
                if abs(r - b) < 25 and abs(r - gg) < 25:
                    xs.append(x); ys.append(y)
    cx, cy = sum(xs) / len(xs), sum(ys) / len(ys)
    rad = ((max(xs) - min(xs)) + (max(ys) - min(ys))) / 4
    c = region.crop((int(cx - rad), int(cy - rad), int(cx + rad), int(cy + rad)))
    mask = Image.new("L", c.size, 0)
    ImageDraw.Draw(mask).ellipse((2, 2, c.width - 2, c.height - 2), fill=255)
    c.putalpha(mask.filter(ImageFilter.GaussianBlur(1.5)))
    ImageDraw.Draw(c).ellipse((3, 3, c.width - 4, c.height - 4), outline=(255, 255, 255, 255), width=max(4, c.width // 60))
    return c


def text(d, xy, s, size, fill, font=FONT, stroke=10, anchor="la"):
    f = ImageFont.truetype(font, size)
    d.text(xy, s, font=f, fill=fill, stroke_width=stroke, stroke_fill=(0, 0, 0), anchor=anchor)


moon = moon_cutout(frame("flight_H", 330), (500, 200, 1400, 900))

# ── Miniatura 16:9 ──
bg = frame("splash_H", 250).resize((1280, 720))
shade = Image.new("L", (1280, 720))
sd = ImageDraw.Draw(shade)
for x in range(1280):
    sd.line([(x, 0), (x, 720)], fill=int(max(0, 200 - x * 0.32)))
bg = Image.composite(Image.new("RGB", bg.size, (0, 0, 0)), bg, shade)
m = moon.resize((300, 300))
bg.paste(m, (930, 26), m)
d = ImageDraw.Draw(bg)
text(d, (40, 70), "¿A LA LUNA", 128, YELLOW)
text(d, (40, 215), "Y DE VUELTA?", 128, YELLOW)
text(d, (44, 380), "Diseña la misión", 64, (255, 255, 255), stroke=7)
text(d, (44, 455), "en equipo", 64, (255, 255, 255), stroke=7)
d.rounded_rectangle((36, 585, 660, 680), radius=18, fill=(10, 20, 40))
text(d, (60, 600), "AstroByte", 64, BLUE, font=FONT2, stroke=0)
text(d, (448, 604), "NASA\nSpace Apps", 28, (255, 255, 255), font=FONT2, stroke=0)
bg.save(os.path.join(out, "miniatura_youtube_1280x720.jpg"), quality=92)

# ── Portada vertical 9:16 (TikTok / Reels / Shorts) ──
v = frame("splash_V", 250).resize((1080, 1920))
top = Image.new("L", (1080, 1920))
td = ImageDraw.Draw(top)
for y in range(1920):
    td.line([(0, y), (1080, y)], fill=int(max(0, 190 - y * 0.22)))
v = Image.composite(Image.new("RGB", v.size, (0, 0, 0)), v, top)
m = moon.resize((330, 330))
v.paste(m, (690, 1190), m)
d = ImageDraw.Draw(v)
text(d, (540, 250), "¿A LA LUNA", 150, YELLOW, anchor="mm")
text(d, (540, 420), "Y DE VUELTA?", 150, YELLOW, anchor="mm")
text(d, (540, 560), "Diseña la misión en equipo", 58, (255, 255, 255), stroke=7, anchor="mm")
d.rounded_rectangle((250, 1600, 830, 1720), radius=22, fill=(10, 20, 40))
text(d, (540, 1660), "AstroByte", 76, BLUE, font=FONT2, stroke=0, anchor="mm")
v.save(os.path.join(out, "portada_vertical_1080x1920.jpg"), quality=92)
print("ok")
