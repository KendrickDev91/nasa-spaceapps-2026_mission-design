"""Exporta el personaje del jugador (avatar Avaturn con esqueleto Mixamo de 52 huesos) a FBX para Unity.
Uso: blender --background --python export_character.py -- <ruta.blend>
Las animaciones de Mixamo (CAMINAR/CORRER/REPOSO/SALTO.fbx) se copian tal cual: usan el mismo esqueleto.

Notas:
- El armature puede estar oculto en el .blend: se muestra y se exporta sin depender de la selección.
- Las texturas del GLB de Avaturn vienen empaquetadas con nombres "*0", "*2"…: se guardan como PNG con nombre
  legible en Art/Characters/Textures (Unity las encuentra junto al FBX) y además se incrustan en el FBX."""
import os
import re
import sys

import bpy

sys.path.append(os.path.dirname(__file__))
import mt_common as mt  # noqa: E402


def save_textures(out_dir):
    tex_dir = os.path.join(out_dir, "Textures")
    os.makedirs(tex_dir, exist_ok=True)
    used = {}
    for mat in bpy.data.materials:
        if not mat.use_nodes or mat.node_tree is None:
            continue
        for node in mat.node_tree.nodes:
            if node.type != "TEX_IMAGE" or node.image is None or node.image.name == "Render Result":
                continue
            img = node.image
            if img.name in used:
                continue
            base = re.sub(r"[^A-Za-z0-9]+", "_", mat.name.replace(".001", "")).strip("_") or "tex"
            socket = next((l.to_socket.name for l in node.outputs[0].links), "Color")
            suffix = re.sub(r"[^A-Za-z]+", "", socket) or "Color"
            name = f"Ken_{base}_{suffix}"
            path = os.path.join(tex_dir, name + ".png")
            img.filepath_raw = path
            img.file_format = "PNG"
            try:
                img.save()
            except RuntimeError:
                img.save_render(path)
            img.name = name
            used[img.name] = path
            print(f"[MT] TEX -> {path}")
    return used


def main():
    source = sys.argv[sys.argv.index("--") + 1]
    bpy.ops.wm.open_mainfile(filepath=source)
    out_dir = os.path.abspath(os.path.join(mt.FBX_DIR, "..", "Characters"))
    os.makedirs(out_dir, exist_ok=True)

    for o in bpy.data.objects:
        o.hide_set(False)
        o.hide_viewport = False
        o.hide_render = o.type not in {"ARMATURE", "MESH"}
    arm = next((o for o in bpy.data.objects if o.type == "ARMATURE"), None)
    if arm is None:
        raise RuntimeError("El .blend no tiene esqueleto (Armature).")
    if arm.animation_data:
        arm.animation_data.action = None
    for pb in arm.pose.bones:   # pose de reposo (T-pose) para crear el avatar Humanoid
        pb.location = (0, 0, 0)
        pb.rotation_quaternion = (1, 0, 0, 0)
        pb.rotation_euler = (0, 0, 0)
        pb.scale = (1, 1, 1)

    save_textures(out_dir)

    path = os.path.join(out_dir, "Player_Ken.fbx")
    bpy.ops.export_scene.fbx(
        filepath=path,
        use_selection=False,
        object_types={"ARMATURE", "MESH"},
        apply_scale_options="FBX_SCALE_ALL",
        axis_forward="-Z",
        axis_up="Y",
        bake_space_transform=False,
        add_leaf_bones=False,
        primary_bone_axis="Y",
        secondary_bone_axis="X",
        armature_nodetype="NULL",
        use_armature_deform_only=True,
        path_mode="COPY",
        embed_textures=True,
        bake_anim=False,
    )
    print(f"[MT] FBX -> {path} · armature {arm.name} ({len(arm.data.bones)} huesos)")


if __name__ == "__main__":
    mt.run(main)
