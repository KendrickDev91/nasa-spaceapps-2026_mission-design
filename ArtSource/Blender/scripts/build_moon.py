"""Superficie lunar (200 × 200 m) con regolito ondulado y cráteres, más un módulo de aterrizaje
genérico de 4 patas. Se usan en la vista de operaciones de las misiones lunares."""
import math
import os
import random
import sys

import bmesh
import bpy

sys.path.append(os.path.dirname(__file__))
import mt_common as mt  # noqa: E402


def terrain(name, size=200.0, cells=120, seed=7, mat=None, parent=None):
    rng = random.Random(seed)
    craters = [(rng.uniform(-size / 2, size / 2), rng.uniform(-size / 2, size / 2), rng.uniform(3, 18)) for _ in range(38)]

    def height(x, y):
        h = 0.6 * math.sin(x * 0.045) * math.cos(y * 0.05) + 0.3 * math.sin(x * 0.13 + y * 0.09)
        for cx, cy, r in craters:
            d = math.hypot(x - cx, y - cy) / r
            if d < 1.0:
                h -= 0.35 * r * (1 - d * d) * 0.25          # cuenco
            elif d < 1.4:
                h += 0.12 * r * (1.4 - d) * 0.6            # borde elevado
        return h

    mesh = bpy.data.meshes.new(name)
    bm = bmesh.new()
    step = size / cells
    grid = []
    for j in range(cells + 1):
        row = []
        for i in range(cells + 1):
            x, y = -size / 2 + i * step, -size / 2 + j * step
            row.append(bm.verts.new((x, y, height(x, y))))
        grid.append(row)
    for j in range(cells):
        for i in range(cells):
            bm.faces.new((grid[j][i], grid[j][i + 1], grid[j + 1][i + 1], grid[j + 1][i]))
    bm.to_mesh(mesh)
    bm.free()
    obj = bpy.data.objects.new(name, mesh)
    bpy.context.scene.collection.objects.link(obj)
    for p in obj.data.polygons:
        p.use_smooth = True
    obj.data.materials.append(mat)
    obj.parent = parent
    return obj


def lander(parent):
    gold = mt.material("LND_Foil", (0.85, 0.62, 0.2), metallic=0.85, roughness=0.3)
    silver = mt.material("LND_Silver", (0.78, 0.78, 0.8), metallic=0.85, roughness=0.3)
    root = mt.empty("LunarLander", parent)
    mt.cylinder("Body", 1.6, 1.5, 1.4, gold, root, verts=8)
    mt.cylinder("Deck", 1.7, 0.1, 2.9, silver, root, verts=8)
    mt.engine_bell("DescentEngine", 0.5, 0.8, 1.4, silver, root)
    for k, x, y in mt.ring_of(4, 2.4, math.pi / 4):
        leg = mt.box(f"Leg{k}", (0.12, 0.12, 2.4), (x * 0.8, y * 0.8, 1.0), silver, root)
        leg.rotation_euler = (math.atan2(y, 2.4) * 0.6, -math.atan2(x, 2.4) * 0.6, 0)
        mt.cylinder(f"Pad{k}", 0.35, 0.08, 0.0, silver, root, verts=12, x=x, y=y)
    mt.box("Antenna", (0.05, 0.05, 1.2), (0.6, 0.6, 3.5), silver, root)
    return root


def main():
    mt.reset_scene()
    regolith = mt.material("MOON_Regolith", (0.42, 0.41, 0.4), roughness=1.0)
    root = mt.empty("LunarSurface")
    terrain("Terrain", mat=regolith, parent=root)
    lander(root)
    mt.export(root, "Moon", "LunarSurface")
    mt.save_blend("LunarSurface")


if __name__ == "__main__":
    mt.run(main)
