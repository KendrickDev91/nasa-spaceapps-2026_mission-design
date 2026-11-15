"""Piezas de la nave a ESCALA DE EXHIBICIÓN (~1-2 m) para los sockets del ModularSpacecraftAssembler.
Un FBX por componentId (Assets/_Project/Art/Models/Spacecraft/<ID>.fbx). Piezas internas genéricas:
BAT_Generic, CDH_Generic, ADCS_Generic (Unity las asigna por prefijo).
Ejes: Z arriba. En Unity (x,y,z) = (-x, z, -y) de Blender: las alas crecen hacia +X aquí (= -X en Unity, ala izquierda) y lo que mira a +Y aquí mira a la cámara en Unity.
Instrumentos: base en el origen, crecen hacia +Z. Antenas: abren hacia +Y."""
import math
import os
import sys

sys.path.append(os.path.dirname(__file__))
import mt_common as mt  # noqa: E402

M = {}


def mats():
    M.update({
        "gold": mt.material("SC_MLI_Gold", (0.86, 0.62, 0.18), metallic=0.85, roughness=0.32),
        "silver": mt.material("SC_Silver", (0.78, 0.79, 0.8), metallic=0.85, roughness=0.25),
        "white": mt.material("SC_WhitePaint", (0.9, 0.9, 0.88), roughness=0.55),
        "black": mt.material("SC_Black", (0.04, 0.04, 0.05), roughness=0.5),
        "cells": mt.material("SC_SolarCells", (0.04, 0.07, 0.24), metallic=0.4, roughness=0.18),
        "frame": mt.material("SC_PanelFrame", (0.7, 0.7, 0.72), metallic=0.7, roughness=0.35),
        "alu": mt.material("SC_Aluminum", (0.62, 0.64, 0.67), metallic=0.9, roughness=0.3),
        "lens": mt.material("SC_Lens", (0.05, 0.1, 0.12), metallic=0.2, roughness=0.05),
        "radiator": mt.material("SC_Radiator", (0.95, 0.95, 0.95), roughness=0.2),
        "batt": mt.material("SC_Battery", (0.18, 0.32, 0.45), roughness=0.5),
        "pcb": mt.material("SC_Electronics", (0.1, 0.35, 0.18), roughness=0.5),
        "kapton": mt.material("SC_Kapton", (0.75, 0.42, 0.08), metallic=0.5, roughness=0.4),
        "rosa": mt.material("SC_ROSA_Blanket", (0.08, 0.1, 0.3), metallic=0.3, roughness=0.3),
    })


def done(root, category="Spacecraft"):
    mt.export(root, category, root.name)
    return root


# ───────────── Buses ─────────────

def bus(id_, w, h, d, engine=False):
    r = mt.empty(id_)
    mt.box("Body", (w, d, h), (0, 0, 0), M["gold"], r)
    mt.box("TopDeck", (w * 1.02, d * 1.02, 0.04), (0, 0, h / 2), M["silver"], r)
    mt.box("BottomDeck", (w * 1.02, d * 1.02, 0.04), (0, 0, -h / 2), M["silver"], r)
    mt.box("Radiator", (w * 0.7, 0.02, h * 0.6), (0, -d / 2 - 0.011, 0), M["radiator"], r)
    for sx in (-1, 1):
        for sy in (-1, 1):
            mt.box(f"Edge_{sx}_{sy}", (0.04, 0.04, h), (sx * w / 2, sy * d / 2, 0), M["alu"], r)
    mt.cylinder("AdapterRing", min(w, d) * 0.32, 0.08, -h / 2 - 0.08, M["alu"], r, verts=24)
    if engine:
        mt.engine_bell("ApogeeEngine", 0.16, 0.35, -h / 2 - 0.08, M["silver"], r)
    return done(r)


def cubesat(id_, w, h, d):
    r = mt.empty(id_)
    mt.box("Body", (w * 0.94, d * 0.94, h * 0.97), (0, 0, 0), M["black"], r)
    for sx in (-1, 1):
        for sy in (-1, 1):
            mt.box(f"Rail_{sx}_{sy}", (0.05, 0.05, h), (sx * (w / 2 - 0.025), sy * (d / 2 - 0.025), 0), M["alu"], r)
    rows = int(round(h / 0.25))
    for k in range(rows):
        z = -h / 2 + (k + 0.5) * h / rows
        mt.box(f"CellsFront{k}", (w * 0.8, 0.01, h / rows * 0.8), (0, d / 2 + 0.005, z), M["cells"], r)
    return done(r)


# ───────────── Energía ─────────────

def wing(id_, panels, pw, ph, cells_mat, yoke=0.25, rolled=False):
    r = mt.empty(id_)
    mt.box("Yoke", (yoke, 0.03, 0.03), (yoke / 2, 0, 0), M["alu"], r)
    x = yoke
    for k in range(panels):
        cx = x + pw / 2
        mt.box(f"Panel{k}", (pw, 0.025, ph), (cx, 0, 0), cells_mat, r)
        mt.box(f"Frame{k}_T", (pw, 0.035, 0.025), (cx, 0, ph / 2), M["frame"], r)
        mt.box(f"Frame{k}_B", (pw, 0.035, 0.025), (cx, 0, -ph / 2), M["frame"], r)
        for line in range(1, 4):
            mt.box(f"CellLine{k}_{line}", (pw * 0.96, 0.03, 0.006), (cx, 0, -ph / 2 + line * ph / 4), M["frame"], r)
        x += pw + 0.04
    if rolled:
        mt.cylinder("Roll", 0.07, ph * 1.05, -ph * 0.525, M["silver"], r, verts=16, x=x + 0.05)
    return done(r)


def body_panels(id_):
    r = mt.empty(id_)
    mt.box("Panel", (0.75, 0.02, 0.55), (0, 0, 0), M["cells"], r)
    mt.box("Frame", (0.79, 0.015, 0.59), (0, 0.006, 0), M["frame"], r)
    return done(r)


def rosa(id_):
    r = mt.empty(id_)
    mt.box("Boom", (1.8, 0.04, 0.04), (0.9, 0, 0), M["alu"], r)
    mt.box("Blanket", (1.65, 0.015, 0.6), (0.95, 0.02, 0), M["rosa"], r)
    for k in range(10):
        mt.box(f"Rib{k}", (0.01, 0.02, 0.6), (0.15 + k * 0.165, 0.02, 0), M["frame"], r)
    mt.cylinder("EndRoll", 0.06, 0.66, -0.33, M["silver"], r, verts=16, x=1.82)
    return done(r)


def battery(id_, s=1.0):
    r = mt.empty(id_)
    mt.box("Pack", (0.32 * s, 0.22 * s, 0.2 * s), (0, 0, 0.1 * s), M["batt"], r)
    for k in range(3):
        mt.box(f"Strap{k}", (0.33 * s, 0.23 * s, 0.015 * s), (0, 0, 0.04 * s + k * 0.06 * s), M["alu"], r)
    return done(r)


# ───────────── Comunicaciones ─────────────

def uhf(id_):
    r = mt.empty(id_)
    mt.box("Base", (0.12, 0.12, 0.05), (0, 0, 0), M["alu"], r)
    for k, (dx, dy) in enumerate(((1, 0), (-1, 0), (0, 1), (0, -1))):
        size = (0.6, 0.012, 0.012) if dx else (0.012, 0.6, 0.012)
        mt.box(f"Whip{k}", size, (dx * 0.33, dy * 0.33, 0.02), M["silver"], r)
    return done(r)


def patch(id_, size):
    r = mt.empty(id_)
    mt.box("Ground", (size, 0.02, size), (0, 0, 0), M["alu"], r)
    mt.box("Patch", (size * 0.6, 0.02, size * 0.6), (0, 0.015, 0), M["kapton"], r)
    return done(r)


def hga(id_, radius, mast):
    r = mt.empty(id_)
    m_ = mt.cylinder("Mast", 0.03, mast, 0.0, M["alu"], r, verts=12)
    m_.rotation_euler = (math.radians(90), 0, 0)
    m_.location = (0, mast / 2, 0)
    mt.dish("Dish", radius, radius * 0.35, M["white"], r, location=(0, mast, 0), facing="+Y")
    strut = mt.cylinder("FeedStrut", 0.012, radius * 0.6, 0.0, M["alu"], r, verts=8)
    strut.rotation_euler = (math.radians(90), 0, 0)
    strut.location = (0, mast + radius * 0.3, 0)
    mt.sphere("Feed", radius * 0.07, (0, mast + radius * 0.62, 0), M["black"], r, segments=12, rings=6)
    return done(r)


# ───────────── Instrumentos ─────────────

def modis(id_):
    r = mt.empty(id_)
    mt.box("Housing", (0.5, 0.4, 0.32), (0, 0, 0.16), M["white"], r)
    mirror = mt.cylinder("ScanMirror", 0.11, 0.42, 0.0, M["silver"], r, verts=20)
    mirror.rotation_euler = (0, math.radians(90), 0)
    mirror.location = (0.0, 0.2, 0.2)
    for k in range(4):
        mt.box(f"Fin{k}", (0.02, 0.38, 0.12), (-0.18 + k * 0.12, 0, 0.38), M["radiator"], r)
    return done(r)


def abi(id_):
    r = mt.empty(id_)
    mt.box("Housing", (0.55, 0.5, 0.42), (0, 0, 0.21), M["gold"], r)
    shade = mt.cylinder("Sunshade", 0.15, 0.35, 0.0, M["black"], r, verts=20)
    shade.rotation_euler = (math.radians(90), 0, 0)
    shade.location = (0.1, 0.42, 0.25)
    mt.box("Radiator", (0.5, 0.02, 0.35), (0, -0.26, 0.22), M["radiator"], r)
    return done(r)


def oco2(id_):
    r = mt.empty(id_)
    mt.box("Bench", (0.3, 0.7, 0.28), (0, 0, 0.14), M["white"], r)
    for k in range(3):
        mt.cylinder(f"Aperture{k}", 0.05, 0.06, 0.28, M["lens"], r, verts=16, y=-0.22 + k * 0.22)
    return done(r)


def nisar(id_):
    r = mt.empty(id_)
    mt.box("RadarBox", (0.4, 0.3, 0.25), (0, 0, 0.125), M["gold"], r)
    boom = mt.cylinder("Boom", 0.025, 0.9, 0.25, M["alu"], r, verts=8)
    boom.rotation_euler = (math.radians(-35), 0, 0)
    reflector = mt.dish("Reflector", 0.85, 0.22, M["silver"], r, location=(0, 0.5, 1.0), facing="+Z")
    reflector.rotation_euler = (math.radians(-55), 0, 0)
    return done(r)


def lroc(id_):
    r = mt.empty(id_)
    mt.box("Base", (0.4, 0.3, 0.08), (0, 0, 0.04), M["gold"], r)
    for k, x in enumerate((-0.1, 0.1)):
        mt.cylinder(f"NAC{k}", 0.08, 0.55, 0.08, M["white"], r, verts=20, x=x)
        mt.cylinder(f"NAC{k}_Lens", 0.07, 0.02, 0.63, M["lens"], r, verts=20, x=x)
    mt.box("WAC", (0.1, 0.1, 0.12), (0.0, 0.12, 0.14), M["black"], r)
    return done(r)


def gedi(id_):
    r = mt.empty(id_)
    mt.box("Housing", (0.5, 0.35, 0.38), (0, 0, 0.19), M["white"], r)
    for k in range(3):
        lens = mt.cylinder(f"Laser{k}", 0.045, 0.05, 0.0, M["lens"], r, verts=16)
        lens.rotation_euler = (math.radians(90), 0, 0)
        lens.location = (-0.15 + k * 0.15, 0.2, 0.25)
    mt.cylinder("Telescope", 0.12, 0.08, 0.38, M["lens"], r, verts=20)
    return done(r)


def small_cam(id_):
    r = mt.empty(id_)
    mt.box("Body", (0.18, 0.18, 0.14), (0, 0, 0.07), M["black"], r)
    mt.cylinder("Lens", 0.06, 0.08, 0.14, M["lens"], r, verts=16)
    return done(r)


def minxss(id_):
    r = mt.empty(id_)
    mt.box("Body", (0.2, 0.2, 0.15), (0, 0, 0.075), M["alu"], r)
    mt.cylinder("Aperture", 0.04, 0.03, 0.15, M["black"], r, verts=16)
    return done(r)


# ───────────── Electrónica interna ─────────────

def computer(id_):
    r = mt.empty(id_)
    mt.box("Chassis", (0.26, 0.2, 0.12), (0, 0, 0.06), M["alu"], r)
    for k in range(5):
        mt.box(f"Fin{k}", (0.26, 0.012, 0.03), (0, -0.08 + k * 0.04, 0.135), M["alu"], r)
    mt.box("Board", (0.2, 0.01, 0.08), (0, -0.105, 0.06), M["pcb"], r)
    return done(r)


def adcs(id_):
    r = mt.empty(id_)
    for k, (x, y) in enumerate(((-0.12, 0), (0.12, 0), (0, 0.12))):
        mt.cylinder(f"Wheel{k}", 0.09, 0.06, 0.0, M["black"], r, verts=20, x=x, y=y)
    mt.cylinder("StarTracker", 0.04, 0.12, 0.06, M["white"], r, verts=16, y=0.12)
    return done(r)


# ───────────── Misión tripulada (tipo Artemis II) ─────────────

def orion(id_):
    """Cápsula Orion sobre el módulo de servicio europeo (escala de exhibición)."""
    r = mt.empty(id_)
    mt.cylinder("ESM", 0.62, 0.85, -0.7, M["white"], r, verts=32)
    mt.cylinder("ESM_Band", 0.63, 0.08, -0.25, M["silver"], r, verts=32)
    mt.engine_bell("ESM_Engine", 0.22, 0.35, -0.7, M["silver"], r)
    mt.cylinder("Adapter", 0.62, 0.12, 0.15, M["black"], r, verts=32)
    mt.cylinder("CrewModule", 0.66, 0.62, 0.27, M["silver"], r, verts=32, radius_top=0.28)
    mt.cylinder("DockingHatch", 0.2, 0.12, 0.89, M["white"], r, verts=20)
    for k, x, y in mt.ring_of(4, 0.42, 0.4):
        mt.box(f"Window{k}", (0.09, 0.05, 0.07), (x, y, 0.55), M["lens"], r)
    return done(r)


def o2o_terminal(id_):
    r = mt.empty(id_)
    mt.box("Mount", (0.2, 0.12, 0.2), (0, 0.06, 0), M["alu"], r)
    dome = mt.sphere("Telescope", 0.14, (0, 0.2, 0), M["white"], r, segments=20, rings=10)
    dome.scale = (1, 1, 1)
    lens = mt.cylinder("Aperture", 0.08, 0.04, 0.0, M["lens"], r, verts=20)
    lens.rotation_euler = (1.5708, 0, 0)
    lens.location = (0, 0.33, 0)
    return done(r)


def camera_cluster(id_):
    r = mt.empty(id_)
    mt.box("Bracket", (0.45, 0.25, 0.05), (0, 0, 0.025), M["alu"], r)
    for k, x in enumerate((-0.15, 0.0, 0.15)):
        mt.box(f"Cam{k}", (0.1, 0.12, 0.1), (x, 0, 0.1), M["black"], r)
        lens = mt.cylinder(f"Lens{k}", 0.035, 0.05, 0.0, M["lens"], r, verts=16)
        lens.rotation_euler = (1.5708, 0, 0)
        lens.location = (x, 0.085, 0.1)
    return done(r)


def dslr(id_):
    r = mt.empty(id_)
    mt.box("Body", (0.16, 0.09, 0.12), (0, 0, 0.06), M["black"], r)
    mt.box("Grip", (0.04, 0.08, 0.1), (-0.07, 0.01, 0.05), M["black"], r)
    lens = mt.cylinder("Zoom", 0.045, 0.2, 0.0, M["black"], r, verts=20)
    lens.rotation_euler = (1.5708, 0, 0)
    lens.location = (0.02, 0.145, 0.06)
    mt.cylinder("Glass", 0.04, 0.01, 0.0, M["lens"], r, verts=20).location = (0.02, 0.25, 0.06)
    return done(r)


def explorer_cams(id_):
    """Kit de 4 cámaras compactas (gran angular, 35 mm, teleobjetivo e ingeniería) sobre una placa."""
    r = mt.empty(id_)
    mt.box("Plate", (0.32, 0.32, 0.03), (0, 0, 0.015), M["alu"], r)
    for k, (x, y, lens_len) in enumerate(((-0.08, -0.08, 0.03), (0.08, -0.08, 0.06), (-0.08, 0.08, 0.12), (0.08, 0.08, 0.04))):
        mt.box(f"Cam{k}", (0.09, 0.09, 0.08), (x, y, 0.07), M["black"], r)
        lens = mt.cylinder(f"Lens{k}", 0.028, lens_len, 0.0, M["lens"], r, verts=16)
        lens.rotation_euler = (1.5708, 0, 0)
        lens.location = (x, y + 0.045 + lens_len / 2, 0.07)
    return done(r)


def main():
    mt.reset_scene()
    mats()
    x = 0.0
    built = [
        bus("BUS_SMALL", 0.7, 0.8, 0.7), bus("BUS_MEDIUM", 1.2, 1.4, 1.2), bus("BUS_LARGE", 1.6, 1.5, 1.6, engine=True),
        cubesat("BUS_CUBESAT_6U", 0.5, 0.75, 0.3), cubesat("BUS_CUBESAT_12U", 0.5, 0.75, 0.5),
        body_panels("SOL_BODY"), wing("SOL_WINGS", 3, 0.55, 0.9, M["cells"]), rosa("SOL_ROSA"),
        wing("SOL_CUBESAT", 2, 0.3, 0.45, M["cells"], yoke=0.08),
        battery("BAT_Generic"), computer("CDH_Generic"), adcs("ADCS_Generic"),
        uhf("ANT_UHF"), patch("ANT_S", 0.3), hga("ANT_X", 0.3, 0.25), hga("ANT_KA", 0.5, 0.35), patch("ANT_S_CUBESAT", 0.16),
        modis("INS_MODIS"), abi("INS_ABI"), oco2("INS_OCO2"), nisar("INS_NISAR"), lroc("INS_LROC"), gedi("INS_GEDI"),
        small_cam("INS_CUBECAM"), minxss("INS_MINXSS"),
        orion("BUS_ORION"), wing("SOL_ESM", 3, 0.5, 0.75, M["cells"], yoke=0.3), o2o_terminal("ANT_O2O"), patch("ANT_ORION_S", 0.22),
        camera_cluster("INS_ORION_CAMERAS"), dslr("INS_CREW_CAMERAS"), explorer_cams("INS_EXPLORER_CAMS"), cubesat("INS_ATENEA", 0.24, 0.34, 0.24),
    ]
    for root in built:               # separados en el .blend para editarlos
        root.location = (x, 0, 0)
        x += 2.5
    mt.save_blend("Spacecraft_Parts")


if __name__ == "__main__":
    mt.run(main)
