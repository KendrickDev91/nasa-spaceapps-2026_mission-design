"""Planos 2D tipo arquitectura (láminas azules) del centro espacial, renderizados en vista superior ortográfica
desde los MISMOS modelos FBX que usa el juego. Cinco láminas: conjunto, Centro de Control, edificio de ensamblaje
y sala limpia, plataforma de lanzamiento y sitio lunar. Salida: docs/planos/*.png y Assets/_Project/Art/Plans/*.png.

Las plantas de edificios usan un plano de corte horizontal (como en arquitectura): la cámara ortográfica se coloca a
la altura del corte mirando hacia abajo, así techos y cubiertas quedan fuera del dibujo. Relleno azul plano y aristas
(silueta, borde y pliegue) dibujadas con Freestyle."""
import math
import os
import random
import shutil
import sys
from datetime import date

import bpy

sys.path.append(os.path.dirname(__file__))
import mt_common as mt  # noqa: E402

PLANS_DOCS = os.path.join(mt.ROOT, "docs", "planos")
PLANS_UNITY = os.path.join(mt.ROOT, "Assets", "_Project", "Art", "Plans")
BLUE = (0.035, 0.12, 0.29)
FILL = (0.05, 0.16, 0.36)
INK = (0.82, 0.9, 1.0)
W, H = 2048, 1448
ANNOT = {"z": 200.0}   # altura de las anotaciones (justo bajo el plano de corte)


def clear():
    mt.reset_scene()
    for c in list(bpy.data.curves):
        bpy.data.curves.remove(c)
    for ls in bpy.context.scene.view_layers[0].freestyle_settings.linesets:
        ls.collection = None
    if "Annot" in bpy.data.collections:
        bpy.data.collections.remove(bpy.data.collections["Annot"])
    annot = bpy.data.collections.new("Annot")
    bpy.context.scene.collection.children.link(annot)


def _annot(obj):
    for c in list(obj.users_collection):
        c.objects.unlink(obj)
    bpy.data.collections["Annot"].objects.link(obj)
    return obj


def _is_annot(obj):
    return any(c.name == "Annot" for c in obj.users_collection)


def flat_material(name, rgb):
    """Color plano (emisión pura): el resultado no depende de la iluminación."""
    m = mt.material(name, (0, 0, 0), emission=rgb, emission_strength=1.0)
    m.diffuse_color = (*rgb, 1.0)
    return m


def import_fbx(category, name, offset=(0, 0, 0), hide=()):
    path = os.path.join(mt.FBX_DIR, category, name + ".fbx")
    before = set(bpy.data.objects)
    bpy.ops.import_scene.fbx(filepath=path)
    new = [o for o in bpy.data.objects if o not in before]
    roots = [o for o in new if o.parent is None]
    for r in roots:
        r.location = (r.location[0] + offset[0], r.location[1] + offset[1], r.location[2] + offset[2])
    for o in new:
        if any(h in o.name for h in hide):
            o.hide_render = True
    return new


def ink_material():
    return flat_material("PLAN_Ink", INK)


def text(body, x, y, size=2.0, align="CENTER"):
    cu = bpy.data.curves.new("PlanText", "FONT")
    cu.body = body
    cu.size = size
    cu.align_x = align
    cu.align_y = "CENTER"
    obj = bpy.data.objects.new("PlanText", cu)
    bpy.data.collections["Annot"].objects.link(obj)
    obj.location = (x, y, ANNOT["z"])
    obj.data.materials.append(ink_material())
    return obj


def line(x0, y0, x1, y1, width):
    length = math.hypot(x1 - x0, y1 - y0)
    obj = mt.box("PlanLine", (length, width, 0.01), ((x0 + x1) / 2, (y0 + y1) / 2, ANNOT["z"] - 0.02), ink_material())
    obj.rotation_euler = (0, 0, math.atan2(y1 - y0, x1 - x0))
    return _annot(obj)


def dimension(x0, y0, x1, y1, label, offset, scale):
    """Línea de cota con marcas y texto, desplazada 'offset' perpendicularmente."""
    ang = math.atan2(y1 - y0, x1 - x0)
    nx, ny = -math.sin(ang) * offset, math.cos(ang) * offset
    w = scale * 0.0018
    line(x0 + nx, y0 + ny, x1 + nx, y1 + ny, w)
    for (px, py) in ((x0, y0), (x1, y1)):
        line(px + nx * 0.6, py + ny * 0.6, px + nx * 1.4, py + ny * 1.4, w)
    text(label, (x0 + x1) / 2 + nx * 1.6, (y0 + y1) / 2 + ny * 1.6, size=scale * 0.018)


def frame(cx, cy, scale, sheet, title, subtitle, n, total):
    """Marco, cajetín, escala gráfica y norte. 'scale' = ancho visible en metros."""
    aspect = H / W
    hw, hh = scale / 2, scale * aspect / 2
    w = scale * 0.0022
    m = scale * 0.015
    for (a, b, c, d) in ((-hw + m, -hh + m, hw - m, -hh + m), (hw - m, -hh + m, hw - m, hh - m),
                         (hw - m, hh - m, -hw + m, hh - m), (-hw + m, hh - m, -hw + m, -hh + m)):
        line(cx + a, cy + b, cx + c, cy + d, w)
    # Cajetín (abajo a la derecha) con fondo opaco para que se lea sobre el dibujo.
    bw, bh = scale * 0.30, scale * 0.10
    bx, by = cx + hw - m - bw, cy - hh + m
    _annot(mt.box("PlanBox", (bw, bh, 0.01), (bx + bw / 2, by + bh / 2, ANNOT["z"] - 0.06), flat_material("PLAN_Blue", BLUE)))
    for (a, b, c, d) in ((bx, by + bh, bx + bw, by + bh), (bx, by, bx, by + bh), (bx, by + bh * 0.55, bx + bw, by + bh * 0.55)):
        line(a, b, c, d, w)
    text("MISSION TRADE-OFF · Centro espacial", bx + bw / 2, by + bh * 0.8, scale * 0.0125)
    text(title, bx + bw / 2, by + bh * 0.66, scale * 0.012)
    text(f"Lámina {n}/{total} · {sheet} · {date.today():%d/%m/%Y}", bx + bw / 2, by + bh * 0.34, scale * 0.0095)
    text("Escala gráfica · cotas en metros", bx + bw / 2, by + bh * 0.14, scale * 0.0085)
    # Título grande arriba a la izquierda, sobre una banda opaca.
    _annot(mt.box("PlanTitleBand", (scale * 0.62, m * 4.4, 0.01), (cx - hw + m + scale * 0.31, cy + hh - m * 3.5, ANNOT["z"] - 0.06),
                  flat_material("PLAN_Blue", BLUE)))
    text(title.upper(), cx - hw + m * 2, cy + hh - m * 2.6, scale * 0.024, align="LEFT")
    text(subtitle, cx - hw + m * 2, cy + hh - m * 4.6, scale * 0.013, align="LEFT")
    # Escala gráfica (abajo a la izquierda).
    step = 10 ** math.floor(math.log10(scale / 8))
    sx, sy = cx - hw + m * 2, cy - hh + m * 3
    for k in range(5):
        line(sx + k * step, sy, sx + (k + 1) * step, sy, w * (2.5 if k % 2 == 0 else 1.0))
        text(f"{int(k * step)}", sx + k * step, sy - scale * 0.016, scale * 0.009)
    text(f"{int(5 * step)} m", sx + 5 * step, sy - scale * 0.016, scale * 0.009)
    # Norte.
    nxp, nyp = cx + hw - m * 4, cy + hh - m * 5
    line(nxp, nyp - scale * 0.03, nxp, nyp + scale * 0.03, w * 1.5)
    line(nxp, nyp + scale * 0.03, nxp - scale * 0.012, nyp + scale * 0.012, w * 1.5)
    line(nxp, nyp + scale * 0.03, nxp + scale * 0.012, nyp + scale * 0.012, w * 1.5)
    text("N", nxp, nyp + scale * 0.045, scale * 0.016)


def _camera(cx, cy, scale, cut):
    cam_data = bpy.data.cameras.new("PlanCam")
    cam_data.type = "ORTHO"
    cam_data.ortho_scale = scale
    cam_data.clip_start = 0.01
    cam_data.clip_end = 2000
    cam = bpy.data.objects.new("PlanCam", cam_data)
    bpy.context.scene.collection.objects.link(cam)
    cam.location = (cx, cy, cut)
    cam.rotation_euler = (0, 0, 0)
    bpy.context.scene.camera = cam


def _set_engine(names):
    for engine in names:
        try:
            bpy.context.scene.render.engine = engine
            return
        except TypeError:
            continue


def _world(rgb):
    scene = bpy.context.scene
    if scene.world is None:
        scene.world = bpy.data.worlds.new("PlanWorld")
    scene.world.color = rgb
    try:
        scene.world.use_nodes = True
    except Exception:
        pass
    if scene.world.node_tree:
        bg = next((n for n in scene.world.node_tree.nodes if n.type == "BACKGROUND"), None)
        if bg:
            bg.inputs[0].default_value = (*rgb, 1.0)
            bg.inputs[1].default_value = 1.0


def _save(file_name):
    scene = bpy.context.scene
    scene.render.resolution_x, scene.render.resolution_y = W, H
    scene.render.resolution_percentage = 100
    scene.render.film_transparent = False
    os.makedirs(PLANS_DOCS, exist_ok=True)
    os.makedirs(PLANS_UNITY, exist_ok=True)
    out = os.path.join(PLANS_DOCS, file_name + ".png")
    scene.render.filepath = out
    bpy.ops.render.render(write_still=True)
    shutil.copyfile(out, os.path.join(PLANS_UNITY, file_name + ".png"))
    print(f"[MT] PLANO -> {out}")


def render(file_name, cx, cy, scale, cut=600.0):
    """Planta arquitectónica con plano de corte a la altura 'cut'."""
    scene = bpy.context.scene
    _set_engine(("BLENDER_EEVEE", "BLENDER_EEVEE_NEXT"))
    scene.view_settings.view_transform = "Standard"
    _world(BLUE)
    fill = flat_material("PLAN_Fill", FILL)
    for o in bpy.data.objects:
        if o.type == "MESH" and not _is_annot(o):
            o.data.materials.clear()
            o.data.materials.append(fill)
    scene.render.use_freestyle = True
    scene.render.line_thickness_mode = "ABSOLUTE"
    scene.render.line_thickness = 1.3
    vl = scene.view_layers[0]
    vl.use_freestyle = True
    fs = vl.freestyle_settings
    fs.crease_angle = math.radians(140)
    ls = fs.linesets[0] if len(fs.linesets) else fs.linesets.new("Plan")
    ls.select_by_visibility = True
    ls.select_by_edge_types = True
    ls.select_by_collection = True
    ls.collection = bpy.data.collections["Annot"]
    ls.collection_negation = "EXCLUSIVE"
    ls.select_silhouette = True
    ls.select_border = True
    ls.select_crease = True
    ls.linestyle.color = INK
    ls.linestyle.thickness = 1.3
    _camera(cx, cy, scale, cut)
    _save(file_name)


def render_relief(file_name, cx, cy, scale):
    """Relieve sombreado (Workbench) para el sitio lunar, en tonos azules y anotaciones claras."""
    scene = bpy.context.scene
    _set_engine(("BLENDER_WORKBENCH", "BLENDER_WORKBENCH_NEXT"))
    scene.render.use_freestyle = False
    scene.view_settings.view_transform = "Standard"
    _world(BLUE)
    sh = scene.display.shading
    sh.light = "STUDIO"
    sh.color_type = "MATERIAL"
    sh.show_xray = False
    sh.show_cavity = True
    sh.show_object_outline = False
    for o in bpy.data.objects:
        if o.type == "MESH" and not _is_annot(o):
            for m in o.data.materials:
                if m:
                    m.diffuse_color = (0.2, 0.3, 0.46, 1.0)
    _camera(cx, cy, scale, 600.0)
    _save(file_name)


def sheet_site():
    clear()
    ANNOT["z"] = 200.0
    import_fbx("Campus", "Campus_Site")
    import_fbx("ControlRoom", "ControlRoom")
    import_fbx("Campus", "ControlCenter_Shell")
    import_fbx("Campus", "AssemblyBuilding", offset=(0, -160, 0))
    import_fbx("LaunchPad", "LaunchPad", offset=(0, -420, 0), hide=("Ground", "Lamp"))
    cx, cy, s = 0, -215, 820
    frame(cx, cy, s, "Plano de conjunto", "Plano de conjunto del centro espacial", "Recorrido del jugador: Centro de Control → ensamblaje → plataforma", 1, 5)
    for label, x, y in (("CENTRO DE CONTROL", 70, -5), ("EDIFICIO DE ENSAMBLAJE", 95, -160), ("PLATAFORMA DE LANZAMIENTO", 120, -400),
                        ("Carretera de servicio", 48, -90), ("Camino de orugas", 52, -300), ("Estacionamiento", -36, -72), ("Laguna", 150, -260)):
        text(label, x, y, s * 0.012)
    dimension(-60, -20, -60, -132, "112 m", -10, s)
    dimension(-60, -188, -60, -375, "187 m", -10, s)
    render("Lamina_01_Plano_de_conjunto", cx, cy, s)


def sheet_control():
    clear()
    ANNOT["z"] = 3.4
    import_fbx("ControlRoom", "ControlRoom")
    import_fbx("Campus", "ControlCenter_Shell")
    cx, cy, s = 3, -4.5, 66
    frame(cx, cy, s, "Arquitectura", "Centro de Control de Misión", "Planta (corte a 3,5 m): sala de control con 3 gradas de consolas + vestíbulo", 2, 5)
    for label, x, y in (("PANTALLAS", 0, 11.4), ("GRADA 1", 0, 5.6), ("GRADA 2", 0, 2.1), ("GRADA 3", 0, -1.4),
                        ("SALA DE CONTROL", -10, -7.0), ("VESTÍBULO", 0, -12.2), ("Recepción", 6, -16.3), ("Mesa de planos", -8, -17.2), ("ENTRADA", 0, -21.8)):
        text(label, x, y, s * 0.014)
    dimension(-13, 10, 13, 10, "26 m", 2.6, s)
    dimension(13, 10, 13, -20, "30 m", 2.6, s)
    render("Lamina_02_Centro_de_Control", cx, cy, s, cut=3.5)


def sheet_assembly():
    clear()
    ANNOT["z"] = 11.9
    import_fbx("Campus", "AssemblyBuilding")
    cx, cy, s = 5, -4, 124
    frame(cx, cy, s, "Arquitectura", "Edificio de ensamblaje y sala limpia", "Planta (corte a 12 m): lanzador móvil, plataformas de trabajo y sala limpia", 3, 5)
    for label, x, y in (("LANZADOR MÓVIL", 0, -6), ("Plataformas de trabajo", -21, -16), ("Plataformas de trabajo", 21, -16),
                        ("SALA LIMPIA (Orion)", -20, 16), ("Estanterías", 27, 0), ("PORTÓN 24 × 54 m", 0, -31.5), ("Acceso de personal", 10, 25.5)):
        text(label, x, y, s * 0.012)
    dimension(-32, -28, 32, -28, "64 m", -10, s)
    dimension(32, 28, 32, -28, "56 m", 5, s)
    render("Lamina_03_Ensamblaje_y_sala_limpia", cx, cy, s, cut=12.0)


def sheet_pad():
    clear()
    ANNOT["z"] = 200.0
    import_fbx("LaunchPad", "LaunchPad", hide=("Ground", "Lamp"))
    cx, cy, s = 0, 15, 210
    frame(cx, cy, s, "Arquitectura", "Plataforma de lanzamiento", "Planta: losa, montaje del cohete, torre de servicio, foso de llamas y rampa", 4, 5)
    for label, x, y in (("MONTAJE", 0, -9), ("Torre de servicio 110 m", -18, 9), ("Foso de llamas", 0, 30), ("Pararrayos", 42, 48),
                        ("Tanque de agua", 70, 42), ("RAMPA (camino de orugas)", 0, 62), ("Puesto de control", 24, 33)):
        text(label, x, y, s * 0.011)
    dimension(-45, 45, -45, -45, "90 m", -8, s)
    render("Lamina_04_Plataforma_de_lanzamiento", cx, cy, s)


def sheet_moon():
    clear()
    ANNOT["z"] = 200.0
    import_fbx("Moon", "LunarSurface")
    cx, cy, s = 0, 0, 215
    frame(cx, cy, s, "Modelado lunar", "Sitio de exploración lunar", "Planta: relieve del regolito, cráteres y módulo de aterrizaje (escenario no caminable)", 5, 5)
    rng = random.Random(7)   # mismos cráteres que build_moon.py
    craters = [(rng.uniform(-100, 100), rng.uniform(-100, 100), rng.uniform(3, 18)) for _ in range(38)]
    for i, (x, y, r) in enumerate(sorted(craters, key=lambda c: -c[2])[:6]):
        text(f"Cráter C{i + 1} · Ø {2 * r:.0f} m", x, y + r + 4, s * 0.011)
    text("MÓDULO DE ATERRIZAJE", 0, 8, s * 0.012)
    dimension(-100, -100, 100, -100, "200 m", -6, s)
    render_relief("Lamina_05_Sitio_lunar", cx, cy, s)


def main():
    only = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    for sheet in (sheet_site, sheet_control, sheet_assembly, sheet_pad, sheet_moon):
        if not only or sheet.__name__ in only:
            sheet()


if __name__ == "__main__":
    mt.run(main)
