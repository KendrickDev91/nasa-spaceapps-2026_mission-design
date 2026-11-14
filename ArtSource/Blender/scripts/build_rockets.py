"""Cohetes del catálogo a escala real (metros), con etapas separables:
   LV_<ID>
     ├─ Stage1   (primera etapa + motores; en SLS/Ariane es la etapa central)
     ├─ Boosters (propulsores laterales, si tiene)
     ├─ Stage2   (etapa superior)
     └─ Fairing  (carenado o, en SLS, Orion + torre de escape)
Referencias visuales: fotos/diagramas de recursos (SLS etiquetado, Falcon 9).
"""
import math
import os
import sys

sys.path.append(os.path.dirname(__file__))
import mt_common as mt  # noqa: E402


def mats():
    return {
        "white": mt.material("LV_White", (0.92, 0.92, 0.9), roughness=0.45),
        "black": mt.material("LV_Black", (0.05, 0.05, 0.06), roughness=0.6),
        "grey": mt.material("LV_DarkGrey", (0.22, 0.22, 0.24), metallic=0.5, roughness=0.4),
        "engine": mt.material("LV_Engine", (0.3, 0.27, 0.25), metallic=0.8, roughness=0.35),
        "orange": mt.material("LV_SLS_Orange", (0.80, 0.42, 0.14), roughness=0.7),
        "silver": mt.material("LV_Silver", (0.75, 0.76, 0.78), metallic=0.8, roughness=0.3),
        "blue": mt.material("LV_AccentBlue", (0.08, 0.2, 0.55), roughness=0.5),
    }


def falcon9(m):
    root = mt.empty("LV_FALCON9")
    s1 = mt.empty("Stage1", root)
    mt.cylinder("S1_Body", 1.83, 41.2, 0.0, m["white"], s1)
    mt.cylinder("S1_Interstage", 1.83, 6.7, 41.2, m["black"], s1)
    mt.cylinder("S1_Octaweb", 1.9, 0.6, 0.0, m["grey"], s1)
    for k, x, y in mt.ring_of(8, 1.15):
        mt.engine_bell(f"S1_Engine{k}", 0.42, 1.3, 0.05, m["engine"], s1, x, y)
    mt.engine_bell("S1_EngineCenter", 0.42, 1.3, 0.05, m["engine"], s1)
    for k, x, y in mt.ring_of(4, 1.95, math.pi / 4):
        leg = mt.box(f"S1_Leg{k}", (0.35, 0.35, 9.0), (x, y, 4.8), m["black"], s1)
        leg.rotation_euler = (0, 0, math.atan2(y, x))
        mt.box(f"S1_GridFin{k}", (0.15, 1.2, 1.5), (x * 1.08, y * 1.08, 46.8), m["grey"], s1).rotation_euler = (0, 0, math.atan2(y, x))
    s2 = mt.empty("Stage2", root)
    mt.cylinder("S2_Body", 1.83, 6.5, 47.9, m["white"], s2)
    mt.engine_bell("S2_MVac", 1.15, 3.2, 47.9, m["engine"], s2)
    f = mt.empty("Fairing", root)
    mt.cylinder("F_Adapter", 1.83, 1.0, 54.4, m["white"], f, radius_top=2.6)
    mt.cylinder("F_Body", 2.6, 6.0, 55.4, m["white"], f)
    mt.ogive("F_Nose", 2.6, 7.6, 61.4, m["white"], f)
    return root


def electron(m):
    root = mt.empty("LV_ELECTRON")
    s1 = mt.empty("Stage1", root)
    mt.cylinder("S1_Body", 0.6, 12.1, 0.0, m["black"], s1, verts=24)
    for k, x, y in mt.ring_of(8, 0.38):
        mt.engine_bell(f"S1_Engine{k}", 0.12, 0.45, 0.02, m["engine"], s1, x, y)
    mt.engine_bell("S1_EngineCenter", 0.12, 0.45, 0.02, m["engine"], s1)
    s2 = mt.empty("Stage2", root)
    mt.cylinder("S2_Body", 0.6, 2.4, 12.1, m["black"], s2, verts=24)
    mt.cylinder("S2_Band", 0.61, 0.25, 14.25, m["white"], s2, verts=24)
    f = mt.empty("Fairing", root)
    mt.cylinder("F_Body", 0.6, 1.2, 14.5, m["black"], f, verts=24)
    mt.ogive("F_Nose", 0.6, 2.3, 15.7, m["black"], f, verts=24)
    return root


def sls(m):
    root = mt.empty("LV_SLS_B1")
    core = mt.empty("Stage1", root)
    mt.cylinder("Core_Body", 4.2, 64.6, 0.0, m["orange"], core, verts=40)
    mt.cylinder("Core_Base", 4.3, 1.5, 0.0, m["grey"], core, verts=40)
    for k, x, y in mt.ring_of(4, 2.0, math.pi / 4):
        mt.engine_bell(f"Core_RS25_{k}", 1.2, 3.2, 0.1, m["engine"], core, x, y)
    boosters = mt.empty("Boosters", root)
    for side in (-1, 1):
        bx = side * (4.2 + 1.86 + 0.25)
        tag = "L" if side < 0 else "R"
        mt.cylinder(f"SRB_{tag}_Body", 1.86, 47.0, 0.0, m["white"], boosters, verts=28, x=bx)
        mt.cylinder(f"SRB_{tag}_Band", 1.88, 1.2, 20.0, m["black"], boosters, verts=28, x=bx)
        mt.ogive(f"SRB_{tag}_Nose", 1.86, 7.0, 47.0, m["white"], boosters, verts=28, x=bx)
        mt.engine_bell(f"SRB_{tag}_Nozzle", 1.6, 2.6, 0.1, m["engine"], boosters, x=bx)
    upper = mt.empty("Stage2", root)
    mt.cylinder("LVSA", 4.2, 8.7, 64.6, m["white"], upper, verts=40, radius_top=2.6)
    mt.cylinder("ICPS", 2.6, 5.0, 73.3, m["orange"], upper, verts=32)
    payload = mt.empty("Fairing", root)
    mt.cylinder("Orion_ESM", 2.5, 4.0, 78.3, m["silver"], payload, verts=32)
    mt.cylinder("Orion_Capsule", 2.5, 3.3, 82.3, m["white"], payload, verts=32, radius_top=1.0)
    mt.cylinder("LAS_Shroud", 1.0, 2.2, 85.6, m["white"], payload, verts=24, radius_top=0.55)
    mt.cylinder("LAS_Tower", 0.55, 6.0, 87.8, m["white"], payload, verts=16, radius_top=0.2)
    mt.cylinder("LAS_Spike", 0.2, 3.5, 93.8, m["grey"], payload, verts=12, radius_top=0.03)
    return root


def ariane64(m):
    root = mt.empty("LV_ARIANE64")
    core = mt.empty("Stage1", root)
    mt.cylinder("Core_Body", 2.7, 30.0, 0.0, m["white"], core, verts=36)
    mt.cylinder("Core_Band", 2.72, 1.0, 27.0, m["blue"], core, verts=36)
    mt.engine_bell("Core_Vulcain", 1.1, 2.6, 0.1, m["engine"], core)
    boosters = mt.empty("Boosters", root)
    for k, x, y in mt.ring_of(4, 2.7 + 1.5 + 0.15, math.pi / 4):
        mt.cylinder(f"P120C_{k}_Body", 1.5, 23.0, 0.0, m["white"], boosters, verts=24, x=x, y=y)
        mt.ogive(f"P120C_{k}_Nose", 1.5, 4.0, 23.0, m["white"], boosters, verts=24, x=x, y=y)
        mt.engine_bell(f"P120C_{k}_Nozzle", 1.15, 2.0, 0.1, m["engine"], boosters, x=x, y=y)
    upper = mt.empty("Stage2", root)
    mt.cylinder("Upper_Body", 2.7, 11.6, 30.0, m["white"], upper, verts=36)
    f = mt.empty("Fairing", root)
    mt.cylinder("F_Body", 2.7, 6.4, 41.6, m["white"], f, verts=36)
    mt.ogive("F_Nose", 2.7, 14.0, 48.0, m["white"], f, verts=36)
    return root


def main():
    mt.reset_scene()
    m = mats()
    builders = [(falcon9, "LV_Falcon9"), (electron, "LV_Electron"), (sls, "LV_SLS_Block1"), (ariane64, "LV_Ariane64")]
    for i, (build, file_name) in enumerate(builders):
        root = build(m)
        mt.export(root, "Rockets", file_name)
        root.location = (i * 25.0, 0, 0)   # separados en el .blend para editarlos cómodamente
    mt.save_blend("Rockets")


if __name__ == "__main__":
    mt.run(main)
