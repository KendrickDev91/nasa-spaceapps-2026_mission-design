"""Plataforma de lanzamiento (escala real): losa, foso de llamas, montaje, torre de servicio
reticulada de 110 m con brazo de acceso, pararrayos, tanque de agua y postes de luz.
El cohete se coloca en el origen sobre el montaje (base a z = 1.5 m)."""
import math
import os
import sys

sys.path.append(os.path.dirname(__file__))
import mt_common as mt  # noqa: E402


def lattice_tower(name, parent, base_x, base_y, width, height, level, steel, column=0.7, beam=0.35):
    """Torre reticulada (columnas + anillos + diagonales) unida en una sola malla."""
    parts = []
    half = width / 2
    corners = [(-half, -half), (half, -half), (half, half), (-half, half)]
    for i, (cx, cy) in enumerate(corners):
        parts.append(mt.box(f"{name}_Col{i}", (column, column, height), (base_x + cx, base_y + cy, height / 2), steel))
    levels = int(height // level)
    for l in range(1, levels + 1):
        z = l * level
        for i in range(4):
            (ax, ay), (bx, by) = corners[i], corners[(i + 1) % 4]
            mx, my = (ax + bx) / 2, (ay + by) / 2
            horizontal = abs(ay - by) < 1e-6
            size = (width, beam, beam) if horizontal else (beam, width, beam)
            parts.append(mt.box(f"{name}_H{l}_{i}", size, (base_x + mx, base_y + my, z), steel))
            diag_len = math.hypot(width, level)
            angle = math.atan2(level, width)
            d = mt.box(f"{name}_D{l}_{i}", (diag_len, beam * 0.7, beam * 0.7) if horizontal else (beam * 0.7, diag_len, beam * 0.7),
                       (base_x + mx, base_y + my, z - level / 2), steel)
            d.rotation_euler = (0, -angle * (1 if l % 2 else -1), 0) if horizontal else (angle * (1 if l % 2 else -1), 0, 0)
    import bpy
    bpy.ops.object.select_all(action="DESELECT")
    for p in parts:
        p.select_set(True)
    bpy.context.view_layer.objects.active = parts[0]
    bpy.ops.object.transform_apply(location=False, rotation=True, scale=True)
    bpy.ops.object.join()
    tower = bpy.context.active_object
    tower.name = name
    tower.data.name = name
    tower.parent = parent
    return tower


def main():
    mt.reset_scene()
    concrete = mt.material("Pad_Concrete", (0.55, 0.55, 0.52), roughness=0.9)
    trench = mt.material("Pad_Trench", (0.12, 0.11, 0.1), roughness=0.95)
    steel = mt.material("Pad_SteelRed", (0.62, 0.16, 0.1), metallic=0.4, roughness=0.55)
    grey = mt.material("Pad_Steel", (0.45, 0.46, 0.48), metallic=0.6, roughness=0.45)
    white = mt.material("Pad_White", (0.85, 0.85, 0.83), roughness=0.6)
    lamp = mt.material("Pad_Lamp", (1.0, 0.95, 0.8), emission=(1.0, 0.92, 0.75), emission_strength=6.0)
    grass = mt.material("Pad_Ground", (0.24, 0.32, 0.18), roughness=1.0)

    root = mt.empty("LaunchPad")
    mt.box("Ground", (600, 600, 1), (0, 0, -2.5), grass, root)
    mt.box("Slab", (90, 90, 2.0), (0, 0, -1.0), concrete, root)
    mt.box("FlameTrench", (14, 50, 0.4), (0, 20, -0.1), trench, root)
    mt.cylinder("MountRing", 7.0, 1.5, 0.0, grey, root, verts=40)
    mt.cylinder("MountHole", 4.6, 1.52, 0.0, trench, root, verts=40)

    tower = lattice_tower("ServiceTower", root, base_x=-16, base_y=0, width=9, height=110, level=8, steel=steel)
    mt.cylinder("TowerTop", 0.25, 12, 110, grey, root, x=-16)
    arm = mt.empty("CrewAccessArm", root)
    mt.box("Arm_Beam", (10.5, 2.4, 2.6), (-6.6, 0, 74), grey, arm)
    mt.box("Arm_WhiteRoom", (3.0, 3.0, 3.2), (-1.8, 0, 74), white, arm)
    for k, z in enumerate((30, 52, 96)):
        mt.box(f"Umbilical{k}", (9.5, 0.8, 0.8), (-7.0, 1.2, z), grey, root)

    for k, (x, y) in enumerate(((42, 42), (-42, -42), (42, -42))):
        mt.cylinder(f"LightningMast{k}", 0.6, 120, 0.0, grey, root, x=x, y=y, verts=12)
    mt.cylinder("WaterTower_Legs", 3.0, 40, 0.0, grey, root, x=70, y=55, verts=8)
    mt.sphere("WaterTower_Tank", 9.0, (70, 55, 48), white, root)
    for k, (x, y) in enumerate(((40, 0), (-40, 20), (0, -40), (0, 44))):
        mt.cylinder(f"LampPole{k}", 0.3, 18, 0.0, grey, root, x=x, y=y, verts=8)
        mt.box(f"LampHead{k}", (2.2, 2.2, 0.6), (x, y, 18.3), lamp, root)
    _ = tower
    # Tanques esféricos de oxígeno (LOX) e hidrógeno líquido (LH2), como en las plataformas reales, fuera de la losa.
    for name, x, y, r in (("LOX", 66, -30, 7.5), ("LH2", -66, -24, 9.0)):
        for k in range(6):
            a = k * math.pi / 3
            mt.cylinder(f"{name}_Leg{k}", 0.35, r + 1.5, -2.0, grey, root, verts=8, x=x + math.cos(a) * r * 0.7, y=y + math.sin(a) * r * 0.7)
        mt.sphere(f"{name}_Tank", r, (x, y, r - 0.5), white, root)
        sgn = 1 if x > 0 else -1
        x0, x1 = sgn * 45.0, x - sgn * r
        mt.box(f"{name}_Pipe", (abs(x1 - x0), 0.8, 0.8), ((x0 + x1) / 2, y, -1.2), grey, root)
    # Deflector de llamas en el foso, bajo el cohete.
    deflector = mt.box("FlameDeflector", (12, 9, 0.8), (0, 9, -0.6), grey, root)
    deflector.rotation_euler = (math.radians(-25), 0, 0)
    # Cables de los pararrayos (catenaria) entre los mástiles y hacia anclajes en el suelo.
    import mathutils
    def wire(p0, p1, name):
        a, b = mathutils.Vector(p0), mathutils.Vector(p1)
        w = mt.box(name, (0.12, 0.12, (b - a).length), tuple((a + b) / 2), grey, root)
        w.rotation_mode = "QUATERNION"
        w.rotation_quaternion = mathutils.Vector((0, 0, 1)).rotation_difference((b - a).normalized())
    masts = [(42, 42), (-42, -42), (42, -42)]
    for k, (x, y) in enumerate(masts):
        wire((x, y, 119.5), (0, 0, 126), f"Catenary{k}")
        wire((x, y, 119.5), (x * 1.5, y * 1.5, -2), f"Guy{k}")
    # Rampa de acceso (pendiente ~2,3°) desde el camino de orugas hasta la losa elevada.
    ramp = mt.box("AccessRamp", (30, 50.2, 1.0), (0, 70, -1.5), concrete, root)
    ramp.rotation_euler = (-math.atan2(2.0, 50.0), 0, 0)
    mt.box("LaunchControlPost", (3.0, 2.0, 2.4), (24, 40, 1.2), white, root)
    mt.box("LaunchControlScreen", (2.2, 0.1, 1.0), (24, 38.95, 1.7), lamp, root)
    mt.empty("Interact_LaunchPanel", root, location=(24, 37.6, 0.0))
    mt.empty("Sign_PlataformaDeLanzamiento", root, location=(0, 44, 9.0))
    mt.export(root, "LaunchPad", "LaunchPad")
    mt.save_blend("LaunchPad")


if __name__ == "__main__":
    mt.run(main)
