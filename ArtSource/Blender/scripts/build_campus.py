"""Centro espacial caminable (escala real, metros). Tres modelos más el terreno:
  ControlCenter_Shell  vestíbulo + fachada que envuelve la sala de control (ControlRoom.fbx)
  AssemblyBuilding     nave alta tipo VAB con sala limpia: SLS apilándose, plataformas, grúa puente
  Campus_Site          terreno, carreteras, camino de orugas y estacionamiento
Convenciones de anclas (Unity las lee por nombre):
  Door_X_<n> / Door_Y_<n>   puerta automática en una pared paralela a X o a Y (3 m de ancho)
  Interact_<id>             punto de interacción (tecla E)
  Sign_<texto>              rótulo 3D (el texto lo pone Unity)
  StackMount                dónde Unity coloca el cohete en ensamblaje
Disposición en Unity: Centro de Control en z=0, ensamblaje en z=+160, plataforma en z=+420
(en Blender: y = −z_Unity)."""
import math
import os
import sys

sys.path.append(os.path.dirname(__file__))
import mt_common as mt  # noqa: E402

M = {}


def mats():
    M.update({
        "concrete": mt.material("CC_Concrete", (0.62, 0.62, 0.6), roughness=0.9),
        "facade": mt.material("CC_Facade", (0.85, 0.86, 0.88), roughness=0.6),
        "facade_dark": mt.material("CC_FacadeDark", (0.24, 0.27, 0.32), roughness=0.6),
        "glass": mt.material("CC_Glass", (0.2, 0.35, 0.5), metallic=0.5, roughness=0.1),
        "floor": mt.material("CC_LobbyFloor", (0.75, 0.75, 0.73), roughness=0.4),
        "desk": mt.material("CC_Reception", (0.18, 0.3, 0.45), roughness=0.5),
        "light": mt.material("CC_LightPanel", (1, 1, 1), emission=(0.95, 0.97, 1.0), emission_strength=2.5),
        "epoxy": mt.material("VAB_CleanFloor", (0.9, 0.91, 0.92), roughness=0.25),
        "metal": mt.material("VAB_WallPanel", (0.55, 0.58, 0.6), metallic=0.5, roughness=0.5),
        "yellow": mt.material("VAB_SafetyYellow", (0.95, 0.75, 0.08), roughness=0.5),
        "steel": mt.material("VAB_Steel", (0.32, 0.34, 0.36), metallic=0.7, roughness=0.4),
        "curtain": mt.material("VAB_CleanroomCurtain", (0.92, 0.95, 0.98), roughness=0.3),
        "white": mt.material("VAB_White", (0.92, 0.92, 0.9), roughness=0.5),
        "grass": mt.material("SITE_Grass", (0.26, 0.36, 0.18), roughness=1.0),
        "asphalt": mt.material("SITE_Asphalt", (0.16, 0.16, 0.17), roughness=0.9),
        "gravel": mt.material("SITE_Gravel", (0.62, 0.58, 0.5), roughness=1.0),
        "paint": mt.material("SITE_RoadPaint", (0.95, 0.9, 0.6), roughness=0.6),
        "water": mt.material("SITE_Water", (0.12, 0.28, 0.4), metallic=0.3, roughness=0.15),
    })


def wall_with_door(prefix, parent, length, height, thickness, center, along_x, door_w=3.0, door_h=3.0, mat=None, door_offset=0.0):
    """Pared con un hueco de puerta; deja un Empty Door_<eje>_<prefix> en el centro del hueco."""
    cx, cy = center
    side_a = length / 2 + door_offset - door_w / 2
    side_b = length / 2 - door_offset - door_w / 2
    if along_x:
        mt.box(f"{prefix}_A", (side_a, thickness, height), (cx - length / 2 + side_a / 2, cy, height / 2), mat, parent)
        mt.box(f"{prefix}_B", (side_b, thickness, height), (cx + length / 2 - side_b / 2, cy, height / 2), mat, parent)
        mt.box(f"{prefix}_Top", (door_w, thickness, height - door_h), (cx + door_offset, cy, door_h + (height - door_h) / 2), mat, parent)
        mt.empty(f"Door_X_{prefix}", parent, location=(cx + door_offset, cy, door_h / 2))
    else:
        mt.box(f"{prefix}_A", (thickness, side_a, height), (cx, cy - length / 2 + side_a / 2, height / 2), mat, parent)
        mt.box(f"{prefix}_B", (thickness, side_b, height), (cx, cy + length / 2 - side_b / 2, height / 2), mat, parent)
        mt.box(f"{prefix}_Top", (thickness, door_w, height - door_h), (cx, cy + door_offset, door_h + (height - door_h) / 2), mat, parent)
        mt.empty(f"Door_Y_{prefix}", parent, location=(cx, cy + door_offset, door_h / 2))


def control_center():
    """Vestíbulo de 26 × 10 m detrás de la sala de control (la sala va en ControlRoom.fbx, 26 × 20 m)."""
    root = mt.empty("ControlCenter_Shell")
    W, H = 26.0, 7.0
    y0, y1 = -20.0, -10.0                       # el vestíbulo va de y=-20 (fachada) a y=-10 (pared de la sala)
    mt.box("Lobby_Floor", (W, 10, 0.2), (0, (y0 + y1) / 2, -0.1), M["floor"], root)
    mt.box("Lobby_Ceiling", (W, 10, 0.2), (0, (y0 + y1) / 2, H + 0.1), M["facade"], root)
    wall_with_door("Entrance", root, W, H, 0.4, (0, y0), along_x=True, door_w=3.0, mat=M["facade"])
    mt.box("Lobby_Wall_L", (0.4, 10, H), (-W / 2, (y0 + y1) / 2, H / 2), M["facade"], root)
    mt.box("Lobby_Wall_R", (0.4, 10, H), (W / 2, (y0 + y1) / 2, H / 2), M["facade"], root)
    # Fachada: franja de vidrio y marquesina sobre la entrada.
    mt.box("Glass_Band", (W - 6, 0.1, 1.6), (0, y0 - 0.25, 4.8), M["glass"], root)
    mt.box("Canopy", (8, 4, 0.3), (0, y0 - 2.0, 3.6), M["facade_dark"], root)
    for sx in (-1, 1):
        mt.cylinder(f"CanopyPost_{sx}", 0.15, 3.6, 0.0, M["steel"], root, verts=8, x=sx * 3.6, y=y0 - 3.7)
    mt.box("Roof_Room", (W + 0.8, 20.8, 0.5), (0, 0, H + 0.45), M["facade_dark"], root)   # techo exterior sobre la sala
    mt.box("Reception", (5, 1.2, 1.1), (6, -14.5, 0.55), M["desk"], root)
    mt.box("PlansTable", (3.2, 2.0, 0.9), (-8, -15, 0.45), M["desk"], root)
    mt.box("PlansTableTop", (3.4, 2.2, 0.05), (-8, -15, 0.92), M["light"], root)
    for k in range(3):
        mt.box(f"LobbyLight{k}", (4, 1.2, 0.05), (-8 + k * 8, -15, H - 0.03), M["light"], root)
    mt.empty("Interact_Plans", root, location=(-8, -13.4, 0.0))
    mt.empty("Interact_Reception", root, location=(6, -13.2, 0.0))
    mt.empty("Sign_CentroDeControl", root, location=(0, y0 - 0.3, H - 0.8))
    mt.empty("SpawnPoint", root, location=(0, y0 - 12.0, 0.0))
    mt.export(root, "Campus", "ControlCenter_Shell")
    return root


def assembly_building():
    """Nave de ensamblaje vertical (64 × 56 × 60 m) con portón gigante hacia la plataforma (−Y)."""
    root = mt.empty("AssemblyBuilding")
    W, D, H = 64.0, 56.0, 60.0
    mt.box("CleanFloor", (W, D, 0.2), (0, 0, -0.1), M["epoxy"], root)
    mt.box("Roof", (W + 1, D + 1, 1.0), (0, 0, H + 0.5), M["facade_dark"], root)
    # Fachada frontal (−Y) con portón de 24 × 54 m abierto para que salga el cohete.
    gate_w, gate_h = 24.0, 54.0
    side = (W - gate_w) / 2
    mt.box("Front_L", (side, 0.8, H), (-(gate_w + side) / 2, -D / 2, H / 2), M["facade"], root)
    mt.box("Front_R", (side, 0.8, H), ((gate_w + side) / 2, -D / 2, H / 2), M["facade"], root)
    mt.box("Front_Top", (gate_w, 0.8, H - gate_h), (0, -D / 2, gate_h + (H - gate_h) / 2), M["facade"], root)
    for sx in (-1, 1):
        mt.box(f"GateTrack_{sx}", (1.0, 1.4, gate_h), (sx * (gate_w / 2 + 0.6), -D / 2 - 0.6, gate_h / 2), M["steel"], root)
    wall_with_door("Back", root, W, H, 0.8, (0, D / 2), along_x=True, door_w=3.0, door_h=3.2, mat=M["facade"], door_offset=10.0)
    mt.box("Side_L", (0.8, D, H), (-W / 2, 0, H / 2), M["metal"], root)
    mt.box("Side_R", (0.8, D, H), (W / 2, 0, H / 2), M["metal"], root)
    for k in range(6):   # franjas verticales oscuras como en la nave de referencia
        mt.box(f"Stripe{k}", (2.0, 0.2, H * 0.9), (-25 + k * 10, D / 2 + 0.5, H * 0.45), M["facade_dark"], root)

    # Lanzador móvil (base) donde se apila el cohete; Unity coloca el SLS en StackMount.
    mt.box("MobileLauncherBase", (26, 20, 8), (0, -6, 4), M["steel"], root)
    mt.empty("StackMount", root, location=(0, -6, 8.0))
    # Plataformas de trabajo a ambos lados del cohete en tres niveles.
    for level, z in enumerate((18.0, 34.0, 50.0)):
        for sx in (-1, 1):
            mt.box(f"Platform_{level}_{sx}", (8, 16, 0.6), (sx * 11.5, -6, z), M["yellow"], root)
            mt.box(f"PlatformRail_{level}_{sx}", (0.1, 16, 1.1), (sx * 7.6, -6, z + 0.85), M["yellow"], root)
            mt.box(f"PlatformBeam_{level}_{sx}", (2.0, 2.0, z), (sx * 26, -6, z / 2), M["steel"], root)
            mt.box(f"PlatformArm_{level}_{sx}", (11, 1.2, 1.2), (sx * 20, -6, z - 0.5), M["steel"], root)
    # Grúa puente amarilla que apila las etapas.
    for sx in (-1, 1):
        mt.box(f"CraneRail_{sx}", (1.2, D - 2, 1.5), (sx * 30, 0, 55.5), M["steel"], root)
    mt.box("CraneBridge", (60, 3.0, 2.5), (0, -6, 56.5), M["yellow"], root)
    mt.box("CraneTrolley", (5, 5, 2.2), (0, -6, 54.2), M["yellow"], root)
    mt.cylinder("CraneHook", 0.25, 10, 43.0, M["steel"], root, verts=8, y=-6)
    # Sala limpia para la cápsula (cortinas blancas, flujo laminar).
    cx, cy = -20.0, 16.0
    for k, ((ox, oy), (sw, sd)) in enumerate((((-7, 0), (0.2, 14)), ((7, 0), (0.2, 14)), ((0, 7), (14, 0.2)))):
        mt.box(f"CleanroomCurtain_{k}", (sw, sd, 9.0), (cx + ox, cy + oy, 4.5), M["curtain"], root)
    mt.box("CleanroomCeiling", (14.4, 14.4, 0.4), (cx, cy, 9.2), M["light"], root)
    mt.box("CapsuleStand", (4, 4, 1.5), (cx, cy, 0.75), M["steel"], root)
    mt.cylinder("CapsuleMock", 2.5, 3.3, 1.5, M["white"], root, verts=32, radius_top=1.0, x=cx, y=cy)
    mt.box("CleanroomSignPost", (0.1, 0.1, 2.0), (cx + 6, cy - 7.5, 1.0), M["steel"], root)
    # Estanterías y montacargas.
    for k in range(5):
        mt.box(f"Rack{k}", (2.0, 8.0, 6.0), (W / 2 - 3, 20 - k * 9, 3.0), M["steel"], root)
        mt.box(f"RackCrates{k}", (1.8, 7.6, 1.2), (W / 2 - 3, 20 - k * 9, 1.6), M["yellow"], root)
    for k, (x, y) in enumerate(((14, 12), (-8, -20), (18, -16))):
        mt.box(f"Forklift{k}", (1.3, 2.6, 1.6), (x, y, 0.8), M["yellow"], root)
    # Marcas de seguridad en el piso y luces.
    for k in range(-1, 2):
        mt.box(f"FloorLine{k}", (0.15, D - 4, 0.01), (k * 16, 0, 0.005), M["yellow"], root)
    for ix in range(-2, 3):
        for iy in range(-2, 3):
            mt.box(f"HighBayLight_{ix}_{iy}", (2.5, 2.5, 0.2), (ix * 12, iy * 10, H - 0.2), M["light"], root)
    mt.empty("Interact_AssemblyKiosk", root, location=(cx + 6.5, cy - 9.0, 0.0))
    mt.empty("Interact_StackInfo", root, location=(10.0, -19.0, 0.0))
    mt.empty("Sign_EdificioDeEnsamblaje", root, location=(10, D / 2 + 0.6, 6.0))
    mt.empty("Sign_SalaLimpia", root, location=(cx, cy - 7.4, 9.8))
    mt.export(root, "Campus", "AssemblyBuilding")
    return root


def campus_site():
    """Terreno de 500 × 700 m con carretera, camino de orugas, estacionamiento y laguna."""
    root = mt.empty("Campus_Site")
    # 3 cm por debajo de los pisos interiores (cota 0) para evitar parpadeo (z-fighting) del césped.
    mt.box("Ground", (520, 760, 1.0), (0, -230, -0.53), M["grass"], root)
    # Carretera del Centro de Control al edificio de ensamblaje (en Blender va hacia −Y).
    mt.box("Road", (9, 120, 0.06), (0, -78, 0.03), M["asphalt"], root)
    for k in range(10):
        mt.box(f"RoadDash{k}", (0.25, 5, 0.07), (0, -25 - k * 11.5, 0.035), M["paint"], root)
    mt.box("Parking", (40, 26, 0.06), (-36, -40, 0.03), M["asphalt"], root)
    for k in range(8):
        mt.box(f"ParkingLine{k}", (0.2, 5.5, 0.07), (-52 + k * 4.5, -36, 0.035), M["paint"], root)
    # Camino de orugas (dos carriles de grava) del portón de ensamblaje a la rampa de la plataforma.
    for sx in (-1, 1):
        mt.box(f"Crawlerway_{sx}", (12, 200, 0.08), (sx * 10, -290, 0.04), M["gravel"], root)
    mt.box("Lagoon", (120, 160, 0.05), (150, -260, 0.02), M["water"], root)
    mt.export(root, "Campus", "Campus_Site")
    return root


def main():
    mt.reset_scene()
    mats()
    control_center()
    a = assembly_building()
    a.location = (0, -160, 0)
    s = campus_site()
    mt.save_blend("Campus")
    _ = s


if __name__ == "__main__":
    mt.run(main)
