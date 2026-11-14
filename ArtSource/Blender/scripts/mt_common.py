"""Utilidades comunes para generar los modelos de MISSION TRADE-OFF en Blender (modo headless).

Convenciones:
- 1 unidad = 1 metro en cohetes, plataforma y sala de control (escala real).
- Las piezas de la nave van a "escala de exhibición" (~1-2 m) para encajar en los sockets del ensamblador.
- Cada modelo es un Empty raíz con hijos nombrados (Stage1, Boosters, Fairing...) para animarlos en Unity.
- Se exporta FBX con ejes de Unity (Y arriba, -Z adelante) y se guarda también el .blend editable.
"""
import math
import os
import sys

import bmesh
import bpy

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", "..", ".."))
FBX_DIR = os.path.join(ROOT, "Assets", "_Project", "Art", "Models")
BLEND_DIR = os.path.join(ROOT, "ArtSource", "Blender", "blend")

_materials = {}


def reset_scene():
    """Vacía la escena (objetos, mallas y materiales) sin recargar preferencias."""
    for obj in list(bpy.data.objects):
        bpy.data.objects.remove(obj, do_unlink=True)
    for coll in (bpy.data.meshes, bpy.data.materials, bpy.data.cameras, bpy.data.lights):
        for block in list(coll):
            coll.remove(block)
    _materials.clear()


def material(name, rgb, metallic=0.0, roughness=0.5, emission=None, emission_strength=0.0):
    """Material Principled reutilizable; Unity URP lo importa como Lit con su color base."""
    key = name
    if key in _materials:
        return _materials[key]
    mat = bpy.data.materials.new(name)
    try:
        mat.use_nodes = True
    except Exception:
        pass
    bsdf = mat.node_tree.nodes.get("Principled BSDF")
    if bsdf:
        bsdf.inputs["Base Color"].default_value = (*rgb, 1.0)
        bsdf.inputs["Metallic"].default_value = metallic
        bsdf.inputs["Roughness"].default_value = roughness
        if emission is not None:
            for label in ("Emission Color", "Emission"):
                if label in bsdf.inputs:
                    bsdf.inputs[label].default_value = (*emission, 1.0)
                    break
            if "Emission Strength" in bsdf.inputs:
                bsdf.inputs["Emission Strength"].default_value = emission_strength
    mat.diffuse_color = (*rgb, 1.0)
    _materials[key] = mat
    return mat


def empty(name, parent=None, location=(0, 0, 0)):
    obj = bpy.data.objects.new(name, None)
    bpy.context.scene.collection.objects.link(obj)
    obj.location = location
    if parent:
        obj.parent = parent
    return obj


def _finish(obj, name, mat, parent, smooth):
    obj.name = name
    obj.data.name = name
    if mat:
        obj.data.materials.clear()
        obj.data.materials.append(mat)
    if parent:
        obj.parent = parent
    if smooth:
        for p in obj.data.polygons:
            p.use_smooth = True
    return obj


def cylinder(name, radius, depth, z0=0.0, mat=None, parent=None, verts=32, x=0.0, y=0.0, smooth=True, radius_top=None):
    """Cilindro (o tronco de cono si radius_top) con la base en z0."""
    if radius_top is None:
        bpy.ops.mesh.primitive_cylinder_add(vertices=verts, radius=radius, depth=depth, location=(x, y, z0 + depth / 2))
    else:
        bpy.ops.mesh.primitive_cone_add(vertices=verts, radius1=radius, radius2=radius_top, depth=depth, location=(x, y, z0 + depth / 2))
    return _finish(bpy.context.active_object, name, mat, parent, smooth)


def ogive(name, radius, length, z0=0.0, mat=None, parent=None, verts=32, segments=12, x=0.0, y=0.0, tip=0.05):
    """Ojiva tangente (punta de cohete/carenado) construida por revolución."""
    mesh = bpy.data.meshes.new(name)
    bm = bmesh.new()
    rings = []
    for s in range(segments + 1):
        t = s / segments
        z = z0 + t * length
        r = max(radius * tip, radius * math.sqrt(max(0.0, 1.0 - t * t)))
        ring = [bm.verts.new((x + r * math.cos(2 * math.pi * k / verts), y + r * math.sin(2 * math.pi * k / verts), z)) for k in range(verts)]
        rings.append(ring)
    for a, b in zip(rings[:-1], rings[1:]):
        for k in range(verts):
            bm.faces.new((a[k], a[(k + 1) % verts], b[(k + 1) % verts], b[k]))
    bm.faces.new(list(reversed(rings[0])))
    bm.faces.new(rings[-1])
    bm.to_mesh(mesh)
    bm.free()
    obj = bpy.data.objects.new(name, mesh)
    bpy.context.scene.collection.objects.link(obj)
    return _finish(obj, name, mat, parent, True)


def box(name, size, location, mat=None, parent=None, rotation=(0, 0, 0)):
    bpy.ops.mesh.primitive_cube_add(size=1, location=location, rotation=rotation)
    obj = bpy.context.active_object
    obj.scale = size
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    return _finish(obj, name, mat, parent, False)


def sphere(name, radius, location, mat=None, parent=None, segments=32, rings=16):
    bpy.ops.mesh.primitive_uv_sphere_add(segments=segments, ring_count=rings, radius=radius, location=location)
    return _finish(bpy.context.active_object, name, mat, parent, True)


def dish(name, radius, depth, mat=None, parent=None, verts=32, rings=8, location=(0, 0, 0), facing="-Y"):
    """Paraboloide (antena) abierto hacia 'facing' (-Y: hacia la cámara en Unity tras exportar)."""
    mesh = bpy.data.meshes.new(name)
    bm = bmesh.new()
    center = bm.verts.new((0, 0, 0))
    prev = None
    for r_i in range(1, rings + 1):
        rr = radius * r_i / rings
        zz = depth * (rr / radius) ** 2
        ring = [bm.verts.new((rr * math.cos(2 * math.pi * k / verts), rr * math.sin(2 * math.pi * k / verts), zz)) for k in range(verts)]
        if prev is None:
            for k in range(verts):
                bm.faces.new((center, ring[k], ring[(k + 1) % verts]))
        else:
            for k in range(verts):
                bm.faces.new((prev[k], ring[k], ring[(k + 1) % verts], prev[(k + 1) % verts]))
        prev = ring
    bmesh.ops.solidify(bm, geom=bm.faces[:], thickness=radius * 0.02)
    bm.to_mesh(mesh)
    bm.free()
    obj = bpy.data.objects.new(name, mesh)
    bpy.context.scene.collection.objects.link(obj)
    rot = {"-Y": (math.radians(90), 0, 0), "+Y": (math.radians(-90), 0, 0), "+Z": (0, 0, 0), "-Z": (math.radians(180), 0, 0)}[facing]
    obj.rotation_euler = rot
    obj.location = location
    return _finish(obj, name, mat, parent, True)


def engine_bell(name, radius, length, z_top, mat, parent, x=0.0, y=0.0):
    """Tobera: tronco de cono abierto hacia abajo desde z_top."""
    return cylinder(name, radius, length, z0=z_top - length, mat=mat, parent=parent, verts=20, x=x, y=y, radius_top=radius * 0.45)


def ring_of(count, radius, angle0=0.0):
    for k in range(count):
        a = angle0 + 2 * math.pi * k / count
        yield k, radius * math.cos(a), radius * math.sin(a)


def apply_all_transforms(root):
    for obj in [root] + list(root.children_recursive):
        obj.select_set(True)
    bpy.context.view_layer.objects.active = root
    bpy.ops.object.select_all(action="DESELECT")


def collapse_groups(root):
    """Une cada Empty hijo de root (con sus mallas) en UNA malla con el mismo nombre.
    Motivo: el exportador FBX con bake_space_transform rota dos veces los Empties anidados
    (en Unity las mallas aparecerían tumbadas). Las mallas resultantes conservan los materiales."""
    for group in [c for c in root.children if c.type == "EMPTY"]:
        meshes = [o for o in group.children_recursive if o.type == "MESH"]
        if not meshes:
            continue
        bpy.ops.object.select_all(action="DESELECT")
        for o in meshes:
            o.select_set(True)
        bpy.context.view_layer.objects.active = meshes[0]
        bpy.ops.object.parent_clear(type="CLEAR_KEEP_TRANSFORM")
        bpy.ops.object.join()
        joined = bpy.context.active_object
        name = group.name
        bpy.data.objects.remove(group, do_unlink=True)
        joined.name = name
        joined.data.name = name
        joined.parent = root


def export(root, category, file_name):
    """Exporta root (con hijos) a FBX para Unity y guarda el .blend fuente.
    Ejes resultantes en Unity: (x, y, z)_Unity = (-x, z, -y)_Blender → +Y de Blender mira a la cámara."""
    collapse_groups(root)
    out_dir = os.path.join(FBX_DIR, category)
    os.makedirs(out_dir, exist_ok=True)
    os.makedirs(BLEND_DIR, exist_ok=True)
    bpy.ops.object.select_all(action="DESELECT")
    root.select_set(True)
    for child in root.children_recursive:
        child.select_set(True)
    fbx_path = os.path.join(out_dir, file_name + ".fbx")
    bpy.ops.export_scene.fbx(
        filepath=fbx_path,
        use_selection=True,
        object_types={"EMPTY", "MESH"},
        apply_unit_scale=True,
        apply_scale_options="FBX_SCALE_UNITS",
        axis_forward="-Z",
        axis_up="Y",
        bake_space_transform=True,
        mesh_smooth_type="FACE",
        add_leaf_bones=False,
    )
    print(f"[MT] FBX -> {fbx_path}")
    return fbx_path


def save_blend(file_name):
    os.makedirs(BLEND_DIR, exist_ok=True)
    path = os.path.join(BLEND_DIR, file_name + ".blend")
    bpy.ops.wm.save_as_mainfile(filepath=path, compress=True)
    print(f"[MT] BLEND -> {path}")


def run(main):
    try:
        main()
    except Exception as ex:  # noqa: BLE001
        import traceback
        traceback.print_exc()
        print(f"[MT] ERROR: {ex}")
        sys.exit(1)
