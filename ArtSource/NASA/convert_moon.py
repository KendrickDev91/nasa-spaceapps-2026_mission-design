"""Convierte el CGI Moon Kit de NASA (LRO) a texturas para Unity.
uso: python convert_moon.py <color.tif> <ldem.tif> <carpeta_salida>
"""
import sys
from PIL import Image

Image.MAX_IMAGE_PIXELS = None
color_path, ldem_path, out = sys.argv[1], sys.argv[2], sys.argv[3]

color = Image.open(color_path)
print("color", color.mode, color.size)
color = color.convert("RGB")
if color.size != (4096, 2048):
    color = color.resize((4096, 2048), Image.LANCZOS)
color.save(out + "/Moon_LRO_Albedo_4k.jpg", quality=92, optimize=True)

ldem = Image.open(ldem_path)
print("ldem", ldem.mode, ldem.size)
ldem = ldem.convert("F").resize((2048, 1024), Image.BICUBIC)
lo, hi = ldem.getextrema()
print("elevacion km", lo, hi)
scale = 255.0 / (hi - lo)
height = ldem.point(lambda v: (v - lo) * scale).convert("L")
height.save(out + "/Moon_LRO_Height_2k.png", optimize=True)
print("ok")
