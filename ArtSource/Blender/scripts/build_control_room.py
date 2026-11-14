"""Sala de control de misión (escala real, 26 × 20 × 7 m), inspirada en las referencias de Houston/ESA:
pared frontal con pantalla principal + 2 laterales (objetos Screen_* para RenderTexture en Unity),
tres gradas de consolas con monitores emisivos, sillas, techo con paneles de luz y relojes de misión.
Anclas Seat_<fila>_<n> marcan dónde sentar a los personajes de Mixamo. El frente de la sala mira a +Y."""
import os
import sys

sys.path.append(os.path.dirname(__file__))
import mt_common as mt  # noqa: E402


def main():
    mt.reset_scene()
    floor = mt.material("CR_Floor", (0.16, 0.17, 0.2), roughness=0.85)
    wall = mt.material("CR_Wall", (0.32, 0.34, 0.38), roughness=0.9)
    ceiling = mt.material("CR_Ceiling", (0.55, 0.56, 0.58), roughness=0.9)
    light = mt.material("CR_LightPanel", (1, 1, 1), emission=(0.9, 0.95, 1.0), emission_strength=3.0)
    desk = mt.material("CR_Desk", (0.22, 0.3, 0.32), roughness=0.6)
    desktop = mt.material("CR_DeskTop", (0.75, 0.76, 0.74), roughness=0.5)
    monitor = mt.material("CR_MonitorOn", (0.03, 0.12, 0.22), emission=(0.05, 0.2, 0.4), emission_strength=0.6)
    bezel = mt.material("CR_Bezel", (0.05, 0.05, 0.06), roughness=0.4)
    screen = mt.material("CR_ScreenOff", (0.02, 0.03, 0.06), roughness=0.2)
    chair = mt.material("CR_Chair", (0.12, 0.12, 0.14), roughness=0.7)
    clock = mt.material("CR_ClockLED", (0.55, 0.15, 0.05), emission=(1.0, 0.35, 0.1), emission_strength=1.5)
    carpet = mt.material("CR_Step", (0.2, 0.2, 0.24), roughness=0.95)

    root = mt.empty("ControlRoom")
    W, D, H = 26.0, 20.0, 7.0
    mt.box("Floor", (W, D, 0.2), (0, 0, -0.1), floor, root)
    mt.box("Ceiling", (W, D, 0.2), (0, 0, H + 0.1), ceiling, root)
    mt.box("Wall_Front", (W, 0.3, H), (0, D / 2, H / 2), wall, root)
    mt.box("Wall_Back", (W, 0.3, H), (0, -D / 2, H / 2), wall, root)
    mt.box("Wall_Left", (0.3, D, H), (-W / 2, 0, H / 2), wall, root)
    mt.box("Wall_Right", (0.3, D, H), (W / 2, 0, H / 2), wall, root)
    for ix in range(-2, 3):
        for iy in range(-2, 3):
            mt.box(f"Light_{ix}_{iy}", (2.4, 1.2, 0.05), (ix * 5.0, iy * 3.6, H - 0.03), light, root)

    # Pantallas frontales (superficies separadas para RenderTexture).
    mt.box("Screen_Main_Frame", (10.6, 0.25, 4.6), (0, D / 2 - 0.2, 4.2), bezel, root)
    mt.box("Screen_Main", (10.0, 0.05, 4.0), (0, D / 2 - 0.35, 4.2), screen, root)
    for side, x in (("Left", -8.6), ("Right", 8.6)):
        mt.box(f"Screen_{side}_Frame", (5.4, 0.25, 3.4), (x, D / 2 - 0.2, 4.2), bezel, root)
        mt.box(f"Screen_{side}", (5.0, 0.05, 3.0), (x, D / 2 - 0.35, 4.2), screen, root)
    for k in range(6):
        mt.box(f"MissionClock{k}", (1.6, 0.1, 0.4), (-8.75 + k * 3.5, D / 2 - 0.3, 6.6), clock, root)

    # Tres gradas de consolas (la primera más baja, cerca de las pantallas).
    rows = [(4.0, 0.0), (0.5, 0.35), (-3.0, 0.7)]
    for r_i, (y, step) in enumerate(rows):
        if step > 0:
            mt.box(f"Tier{r_i}", (W - 1.0, 3.4, step), (0, y - 0.3, step / 2), carpet, root)
        for c_i in range(4):
            x = -7.5 + c_i * 5.0
            base_z = step
            mt.box(f"Desk_{r_i}_{c_i}", (4.2, 1.0, 0.75), (x, y, base_z + 0.375), desk, root)
            mt.box(f"DeskTop_{r_i}_{c_i}", (4.3, 1.1, 0.05), (x, y, base_z + 0.77), desktop, root)
            for m_i, dx in enumerate((-1.2, 0.0, 1.2)):
                mt.box(f"Monitor_{r_i}_{c_i}_{m_i}", (0.95, 0.05, 0.6), (x + dx, y + 0.3, base_z + 1.15), monitor, root)
                mt.box(f"MonitorBack_{r_i}_{c_i}_{m_i}", (1.0, 0.06, 0.65), (x + dx, y + 0.34, base_z + 1.15), bezel, root)
            for s_i, dx in enumerate((-1.0, 1.0)):
                sx, sy = x + dx, y - 1.0
                mt.box(f"ChairSeat_{r_i}_{c_i}_{s_i}", (0.55, 0.55, 0.1), (sx, sy, base_z + 0.5), chair, root)
                mt.box(f"ChairBack_{r_i}_{c_i}_{s_i}", (0.55, 0.08, 0.6), (sx, sy - 0.28, base_z + 0.85), chair, root)
                mt.cylinder(f"ChairStem_{r_i}_{c_i}_{s_i}", 0.05, 0.45, base_z, chair, root, verts=8, x=sx, y=sy)
                mt.empty(f"Seat_{r_i}_{c_i * 2 + s_i}", root, location=(sx, sy, base_z + 0.5))

    mt.empty("CameraSpot_Back", root, location=(0, -D / 2 + 1.5, 3.4))
    mt.export(root, "ControlRoom", "ControlRoom")
    mt.save_blend("ControlRoom")


if __name__ == "__main__":
    mt.run(main)
