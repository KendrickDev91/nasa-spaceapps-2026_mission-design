"""Mapa de normales de la Luna a partir del relieve LOLA (ldem_4.tif, km, float32).
uso: python moon_normals.py <ldem.tif> <salida.png> [exageracion]
Espacio tangente de una esfera UV equirectangular: x = este (u), y = norte (v), z = hacia fuera.
"""
import math
import sys
from PIL import Image

src, out = sys.argv[1], sys.argv[2]
exag = float(sys.argv[3]) if len(sys.argv) > 3 else 5.0
W, H = 2048, 1024
R = 1737.4  # km

img = Image.open(src).convert("F").resize((W, H), Image.BICUBIC)
h = list(img.getdata())

def at(x, y):
    x %= W
    y = min(max(y, 0), H - 1)
    return h[y * W + x]

dy_km = math.pi * R / H
px = bytearray(W * H * 3)
for y in range(H):
    lat = math.pi * (0.5 - (y + 0.5) / H)            # fila 0 = norte
    dx_km = max(2 * math.pi * R * math.cos(lat) / W, dy_km * 0.05)
    for x in range(W):
        dzdx = (at(x + 1, y) - at(x - 1, y)) / (2 * dx_km)
        dzdn = (at(x, y - 1) - at(x, y + 1)) / (2 * dy_km)   # hacia el norte
        nx, ny, nz = -exag * dzdx, -exag * dzdn, 1.0
        inv = 1.0 / math.sqrt(nx * nx + ny * ny + nz * nz)
        i = (y * W + x) * 3
        px[i] = int(round((nx * inv * 0.5 + 0.5) * 255))
        px[i + 1] = int(round((ny * inv * 0.5 + 0.5) * 255))
        px[i + 2] = int(round((nz * inv * 0.5 + 0.5) * 255))

Image.frombytes("RGB", (W, H), bytes(px)).save(out, optimize=True)
print("ok", out)
