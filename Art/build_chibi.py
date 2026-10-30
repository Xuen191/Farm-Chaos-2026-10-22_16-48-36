# Farm Chaos - chibi player builder for Blender 4.5
# Run in Blender: Scripting tab > New > paste > Run Script (Alt+P).
# Builds a cute chibi (big head, little body, arms + legs), a simple rig, 3 hairstyles, 4 hats,
# 6 animations (Idle, Walk, Run, Jump, Wave, Cheer) and exports Chibi.fbx into the Unity project.
# Customisable in Unity by material name: Skin, Shirt, Outfit, Shoes, Hair, Accent, HatMain, HatAccent, Face.
import bpy, bmesh, math, os
from mathutils import Matrix, Vector

PROJECT = r"C:\Users\shere\Farm-Chaos-2026-10-22_16-48-36"
FBX_PATH = os.path.join(PROJECT, "Assets", "Models", "Chibi", "Chibi.fbx")
BLEND_PATH = os.path.join(PROJECT, "Art", "Chibi.blend")      # outside Assets so Unity ignores it

# ------------------------------------------------------------------ clean scene
bpy.ops.object.mode_set(mode='OBJECT') if bpy.context.object and bpy.context.object.mode != 'OBJECT' else None
for o in list(bpy.data.objects): bpy.data.objects.remove(o, do_unlink=True)
for coll in (bpy.data.meshes, bpy.data.armatures, bpy.data.materials, bpy.data.actions):
    for d in list(coll):
        if d.users == 0 or coll is bpy.data.actions: coll.remove(d)
scene = bpy.context.scene
scene.render.fps = 30
col = scene.collection

# ------------------------------------------------------------------ materials (colours are just defaults; Unity recolours)
def mat(name, rgb):
    m = bpy.data.materials.get(name) or bpy.data.materials.new(name)
    m.diffuse_color = (*rgb, 1)
    m.use_nodes = True
    bsdf = m.node_tree.nodes.get("Principled BSDF")
    if bsdf:
        bsdf.inputs["Base Color"].default_value = (*rgb, 1)
        bsdf.inputs["Roughness"].default_value = 0.6
    return m

def hexc(h):
    h = h.lstrip('#'); c = [int(h[i:i+2], 16) / 255 for i in (0, 2, 4)]
    return tuple(((x + 0.055) / 1.055) ** 2.4 if x > 0.04045 else x / 12.92 for x in c)   # sRGB -> linear

M = {n: mat(n, hexc(c)) for n, c in [
    ("Skin", "#FFE3D3"), ("Shirt", "#FFFFFF"), ("Socks", "#FFFFFF"), ("Outfit", "#8EC5F0"), ("Shoes", "#A87B5C"),
    ("Hair", "#7A5240"), ("Accent", "#F4B8C1"), ("Face", "#FFD9C2"),
    ("HatMain", "#E9CF7A"), ("HatAccent", "#F28FB1"),
    ("FixedWhite", "#FFFFFF"), ("FixedRed", "#E0505C"), ("FixedOrange", "#F5A623"), ("FixedInk", "#2E2A3A"),
    ("FixedLeaf", "#7FC98A"), ("FixedYellow", "#FFE07A")]}

# ------------------------------------------------------------------ geometry helpers (bmesh)
R_HEAD = 0.30
HEAD_C = Vector((0, 0, 0.88))
HEAD_S = Vector((1.12, 1.0, 0.98))

def sphere(bm, centre, radii, mat_index, seg=24, rings=16):
    ret = bmesh.ops.create_uvsphere(bm, u_segments=seg, v_segments=rings, radius=1.0)
    vs = ret['verts']
    for v in vs: v.co = Vector((v.co.x * radii[0], v.co.y * radii[1], v.co.z * radii[2])) + centre
    return vs, faces_of(vs, mat_index)

def capsule(bm, a, b, r, mat_index, seg=16, rings=12):
    """Rounded stick from point a to point b."""
    a, b = Vector(a), Vector(b)
    L = (b - a).length
    ret = bmesh.ops.create_uvsphere(bm, u_segments=seg, v_segments=rings, radius=r)
    vs = ret['verts']
    for v in vs:
        v.co.z += L / 2 if v.co.z >= 0 else -L / 2
    rot = (b - a).normalized().to_track_quat('Z', 'Y').to_matrix().to_4x4()
    mtx = Matrix.Translation((a + b) / 2) @ rot
    for v in vs: v.co = mtx @ v.co
    return vs, faces_of(vs, mat_index)

def cone(bm, base, r1, r2, depth, mat_index, direction=(0, 0, 1), seg=16):
    d = Vector(direction).normalized()
    rot = d.to_track_quat('Z', 'Y').to_matrix().to_4x4()
    mtx = Matrix.Translation(Vector(base) + d * depth / 2) @ rot
    ret = bmesh.ops.create_cone(bm, cap_ends=True, cap_tris=False, segments=seg, radius1=r1, radius2=r2, depth=depth, matrix=mtx)
    vs = ret['verts']
    return vs, faces_of(vs, mat_index)

def faces_of(vs, mat_index):
    fs = set(f for v in vs for f in v.link_faces)
    for f in fs: f.material_index = mat_index; f.smooth = True
    return fs

def weight(bm, vs, group_index):
    dl = bm.verts.layers.deform.verify()
    for v in vs: v[dl][group_index] = 1.0

def head_point(nx, ny, nz, grow=1.0):
    """Point on the head surface from a unit direction (nx,ny,nz)."""
    d = Vector((nx, ny, nz)).normalized()
    return HEAD_C + Vector((d.x * R_HEAD * HEAD_S.x, d.y * R_HEAD * HEAD_S.y, d.z * R_HEAD * HEAD_S.z)) * grow

def new_obj(name, bm, mats):
    me = bpy.data.meshes.new(name)
    bm.normal_update()
    bm.to_mesh(me); bm.free()
    for m in mats: me.materials.append(M[m])
    ob = bpy.data.objects.new(name, me)
    col.objects.link(ob)
    return ob

# ------------------------------------------------------------------ armature
BONES = {   # name: (head, tail, parent)
    "hips":  ((0, 0, 0.36), (0, 0, 0.46), None),
    "spine": ((0, 0, 0.46), (0, 0, 0.58), "hips"),
    "head":  ((0, 0, 0.58), (0, 0, 0.90), "spine"),
    "arm.L": ((0.13, 0, 0.555), (0.19, 0, 0.38), "spine"),
    "arm.R": ((-0.13, 0, 0.555), (-0.19, 0, 0.38), "spine"),
    "leg.L": ((0.062, 0, 0.37), (0.065, 0, 0.05), "hips"),
    "leg.R": ((-0.062, 0, 0.37), (-0.065, 0, 0.05), "hips"),
}
arm_data = bpy.data.armatures.new("Chibi_Rig")
arm = bpy.data.objects.new("Chibi_Rig", arm_data)
col.objects.link(arm)
bpy.context.view_layer.objects.active = arm
arm.select_set(True)
bpy.ops.object.mode_set(mode='EDIT')
for name, (h, t, p) in BONES.items():
    eb = arm_data.edit_bones.new(name)
    eb.head, eb.tail = Vector(h), Vector(t)
    eb.align_roll(Vector((0, -1, 0)))   # local Z = front (-Y), so +X rotation = swing forward
    if p: eb.parent = arm_data.edit_bones[p]
bpy.ops.object.mode_set(mode='OBJECT')
GROUPS = list(BONES.keys())
G = {n: i for i, n in enumerate(GROUPS)}

def ring(bm, z_centre, x, r, depth, mi, seg=20):
    return cone(bm, (x, 0, z_centre - depth / 2), r, r, depth, mi, (0, 0, 1), seg)

# ------------------------------------------------------------------ body (one skinned mesh) - art-toy proportions
BODY_MATS = ["Skin", "Shirt", "Outfit", "Shoes", "Accent", "Face", "Socks"]
MI = {n: i for i, n in enumerate(BODY_MATS)}
bm = bmesh.new()
bm.verts.layers.deform.verify()   # make the layers first: adding a layer later invalidates existing verts
bm.loops.layers.uv.verify()
# big round head + little ears
vs, _ = sphere(bm, HEAD_C, R_HEAD * HEAD_S, MI["Skin"], 40, 30); weight(bm, vs, G["head"])
for sx in (1, -1):
    vs, _ = sphere(bm, head_point(sx, 0.05, -0.12, 0.97), (0.04, 0.03, 0.055), MI["Skin"], 12, 8); weight(bm, vs, G["head"])
# neck
vs, _ = capsule(bm, (0, 0, 0.54), (0, 0, 0.62), 0.04, MI["Skin"]); weight(bm, vs, G["spine"])
# little t-shirt with puffy sleeves
vs, _ = sphere(bm, Vector((0, 0, 0.50)), (0.115, 0.092, 0.1), MI["Shirt"], 24, 16); weight(bm, vs, G["spine"])
for side, sx in (("L", 1), ("R", -1)):
    vs, _ = sphere(bm, Vector((sx * 0.122, 0, 0.545)), (0.058, 0.055, 0.05), MI["Shirt"], 16, 12); weight(bm, vs, G["arm." + side])
# shorts overalls: seat, bib, pink straps + heart buttons, shorts legs with rolled cuffs
vs, _ = sphere(bm, Vector((0, 0, 0.43)), (0.125, 0.1, 0.08), MI["Outfit"], 24, 16); weight(bm, vs, G["hips"])
vs, _ = sphere(bm, Vector((0, -0.075, 0.50)), (0.075, 0.03, 0.065), MI["Outfit"], 16, 10); weight(bm, vs, G["spine"])
for sx in (1, -1):
    vs, _ = capsule(bm, (sx * 0.05, -0.088, 0.53), (sx * 0.075, -0.03, 0.605), 0.012, MI["Accent"], 8, 6); weight(bm, vs, G["spine"])
    vs, _ = capsule(bm, (sx * 0.075, 0.03, 0.605), (sx * 0.05, 0.088, 0.48), 0.012, MI["Accent"], 8, 6); weight(bm, vs, G["spine"])
    vs, _ = sphere(bm, Vector((sx * 0.05, -0.104, 0.528)), (0.016, 0.01, 0.014), MI["Accent"], 10, 6); weight(bm, vs, G["spine"])
for side, sx in (("L", 1), ("R", -1)):
    b = "leg." + side
    vs, _ = capsule(bm, (sx * 0.062, 0, 0.42), (sx * 0.064, 0, 0.335), 0.056, MI["Outfit"], 16, 10); weight(bm, vs, G[b])
    vs, _ = ring(bm, 0.322, sx * 0.064, 0.064, 0.03, MI["Shirt"]); weight(bm, vs, G[b])      # rolled cuff
    # thin legs, frilly socks with a pink stripe, round Mary-Jane shoes
    vs, _ = capsule(bm, (sx * 0.064, 0, 0.33), (sx * 0.065, 0, 0.14), 0.034, MI["Skin"], 14, 10); weight(bm, vs, G[b])
    vs, _ = capsule(bm, (sx * 0.065, 0, 0.165), (sx * 0.065, 0, 0.065), 0.04, MI["Socks"], 14, 10); weight(bm, vs, G[b])
    vs, _ = ring(bm, 0.168, sx * 0.065, 0.046, 0.016, MI["Socks"]); weight(bm, vs, G[b])      # frill
    vs, _ = ring(bm, 0.145, sx * 0.065, 0.042, 0.01, MI["Accent"]); weight(bm, vs, G[b])      # stripe
    vs, _ = sphere(bm, Vector((sx * 0.066, -0.024, 0.038)), (0.052, 0.078, 0.038), MI["Shoes"], 18, 12); weight(bm, vs, G[b])
    vs, _ = capsule(bm, (sx * 0.035, -0.04, 0.07), (sx * 0.097, -0.04, 0.07), 0.009, MI["Shoes"], 8, 6); weight(bm, vs, G[b])   # strap
# thin arms + round hands
for side, sx in (("L", 1), ("R", -1)):
    b = "arm." + side
    vs, _ = capsule(bm, (sx * 0.148, 0, 0.53), (sx * 0.182, 0, 0.405), 0.03, MI["Skin"], 14, 10); weight(bm, vs, G[b])
    vs, _ = sphere(bm, Vector((sx * 0.188, -0.005, 0.385)), (0.036, 0.033, 0.038), MI["Skin"], 14, 10); weight(bm, vs, G[b])
# face: a thin shell over the front of the head with a front-on UV map (Unity swaps the face picture)
ret = bmesh.ops.create_uvsphere(bm, u_segments=48, v_segments=36, radius=1.0)
fvs = ret['verts']
kill = [v for v in fvs if v.co.y > -0.25]
keep = [v for v in fvs if v.co.y <= -0.25]
bmesh.ops.delete(bm, geom=kill, context='VERTS')
uv = bm.loops.layers.uv.verify()
FACE_W = 0.8    # the face picture spans +-80% of the head's half-width / half-height
for v in keep:
    v.co = HEAD_C + Vector((v.co.x * R_HEAD * HEAD_S.x, v.co.y * R_HEAD * HEAD_S.y, v.co.z * R_HEAD * HEAD_S.z)) * 1.006
fface = set(f for v in keep for f in v.link_faces)
for f in fface:
    f.material_index = MI["Face"]; f.smooth = True
    for l in f.loops:
        p = l.vert.co - HEAD_C
        l[uv].uv = (0.5 + p.x / (2 * FACE_W * R_HEAD * HEAD_S.x), 0.5 + p.z / (2 * FACE_W * R_HEAD * HEAD_S.z))
weight(bm, keep, G["head"])
body = new_obj("Chibi_Body", bm, BODY_MATS)
for n in GROUPS: body.vertex_groups.new(name=n)
body.parent = arm
mod = body.modifiers.new("Rig", 'ARMATURE'); mod.object = arm

# ------------------------------------------------------------------ hair + hats (separate, ride on the head bone)
def bone_child(ob):
    world = ob.matrix_world.copy()
    ob.parent = arm; ob.parent_type = 'BONE'; ob.parent_bone = "head"
    bpy.context.view_layer.update()
    ob.matrix_world = world

HAIR = 1.1   # fluffy hair sits this far out from the head
def hair_cap(bm, bottom=-0.62, back_bottom=-0.7, grow=HAIR, mi=0):
    """Round cap covering top/back/sides, leaving the face open below the forehead."""
    ret = bmesh.ops.create_uvsphere(bm, u_segments=40, v_segments=30, radius=1.0)
    vs = ret['verts']
    def keep(c):
        if c.y < -0.15 and c.z < 0.3 and abs(c.x) < 0.88: return False     # face window
        return c.z > (back_bottom if c.y > 0.2 else bottom)
    bmesh.ops.delete(bm, geom=[v for v in vs if not keep(v.co)], context='VERTS')
    left = [v for v in vs if v.is_valid]
    for v in left:
        v.co = HEAD_C + Vector((v.co.x * R_HEAD * HEAD_S.x, v.co.y * R_HEAD * HEAD_S.y, v.co.z * R_HEAD * HEAD_S.z)) * grow
    faces_of(left, mi)

def bangs(bm, n=7, z=0.34, mi=0, spread=0.62):
    """Thick blunt fringe across the forehead."""
    for i in range(n):
        x = -spread + 2 * spread * i / (n - 1)
        y = -math.sqrt(max(0.05, 1 - x * x - z * z))
        sphere(bm, head_point(x, y, z, HAIR - 0.01), (0.07, 0.045, 0.072), mi, 14, 10)

def side_locks(bm, z=-0.25, length=0.15, mi=0):
    for sx in (1, -1):
        sphere(bm, head_point(sx * 0.9, -0.3, z, HAIR - 0.02), (0.055, 0.065, length), mi, 14, 10)

# Bob (default): fluffy round bob with blunt bangs and cheek-framing locks + a little hair clip
bm = bmesh.new()
hair_cap(bm); bangs(bm); side_locks(bm)
for i in (0, 1):
    capsule(bm, head_point(0.5 + i * 0.08, -0.62, 0.52, HAIR + 0.02), head_point(0.42 + i * 0.08, -0.6, 0.66, HAIR + 0.02), 0.012, 1, 6, 4)
bone_child(new_obj("Hair_Bob", bm, ["Hair", "Accent"]))

# Long: bob + long hair falling down the back
bm = bmesh.new()
hair_cap(bm, bottom=-0.7, back_bottom=-0.9); bangs(bm); side_locks(bm, -0.45, 0.24)
sphere(bm, HEAD_C + Vector((0, 0.16, -0.24)), (R_HEAD * HEAD_S.x * 0.9, 0.12, 0.2), 0, 24, 16)
bone_child(new_obj("Hair_Long", bm, ["Hair"]))

# Spiky: short and boyish with pointy bangs
bm = bmesh.new()
hair_cap(bm, bottom=-0.3, back_bottom=-0.45, grow=1.07)
for i in range(6):
    x = -0.55 + 1.1 * i / 5
    d = Vector((x * 0.5, -0.8, -0.35)).normalized()
    cone(bm, head_point(x, -0.75, 0.42, 1.04), 0.06, 0.0, 0.12, 0, d, 10)
for nx, ny, nz in [(0, 0.1, 1), (0.4, 0.2, 0.85), (-0.4, 0.2, 0.85), (0.15, 0.6, 0.75), (-0.15, 0.6, 0.75)]:
    cone(bm, head_point(nx, ny, nz, 1.05), 0.07, 0.0, 0.12, 0, Vector((nx, ny, nz)).normalized(), 10)
bone_child(new_obj("Hair_Spiky", bm, ["Hair"]))

# Pigtails: bangs + two bouncy pigtails with bows
bm = bmesh.new()
hair_cap(bm, bottom=-0.35, back_bottom=-0.5); bangs(bm)
for sx in (1, -1):
    sphere(bm, head_point(sx * 0.98, 0.28, -0.25, 1.22), (0.07, 0.07, 0.16), 0, 16, 12)
    sphere(bm, head_point(sx * 0.95, 0.25, 0.08, 1.12), (0.035, 0.035, 0.035), 1, 10, 8)
bone_child(new_obj("Hair_Pigtails", bm, ["Hair", "Accent"]))

top = HEAD_C.z + R_HEAD * HEAD_S.z * HAIR
RW = R_HEAD * HEAD_S.x * HAIR
# Beret
bm = bmesh.new()
s_vs, _ = sphere(bm, Vector((0.02, 0.01, top - 0.01)), (RW * 1.02, RW * 0.95, 0.075), 0, 32, 16)
cone(bm, (0.02, 0.01, top + 0.05), 0.012, 0.01, 0.045, 0, (0, 0, 1), 8)
bone_child(new_obj("Hat_Beret", bm, ["HatMain"]))
# Straw hat
bm = bmesh.new()
cone(bm, (0, 0, top - 0.07), RW + 0.13, RW + 0.13, 0.02, 0, (0, 0, 1), 36)
cone(bm, (0, 0, top - 0.06), RW * 0.75, RW * 0.62, 0.13, 0, (0, 0, 1), 28)
cone(bm, (0, 0, top - 0.045), RW * 0.76, RW * 0.72, 0.04, 1, (0, 0, 1), 28)
sphere(bm, Vector((RW * 0.72, -0.08, top - 0.03)), (0.04, 0.03, 0.04), 1, 10, 8)
bone_child(new_obj("Hat_Straw", bm, ["HatMain", "HatAccent"]))
# Cap
bm = bmesh.new()
ret = bmesh.ops.create_uvsphere(bm, u_segments=28, v_segments=18, radius=1.0)
vs = ret['verts']; bmesh.ops.delete(bm, geom=[v for v in vs if v.co.z < 0.1], context='VERTS')
left = [v for v in vs if v.is_valid]
for v in left: v.co = HEAD_C + Vector((v.co.x * RW * 1.02, v.co.y * R_HEAD * HAIR * 1.04, v.co.z * R_HEAD * HEAD_S.z * HAIR * 1.02))
faces_of(left, 0)
sphere(bm, Vector((0, -R_HEAD * HAIR * 1.0, HEAD_C.z + 0.11)), (0.18, 0.13, 0.012), 0, 16, 6)
sphere(bm, Vector((0, 0, top + 0.01)), (0.025, 0.025, 0.02), 1, 8, 6)
bone_child(new_obj("Hat_Cap", bm, ["HatMain", "HatAccent"]))
# Chicken hat (a little chicken sitting on your head!)
bm = bmesh.new()
sphere(bm, Vector((0, 0.03, top + 0.05)), (0.15, 0.17, 0.11), 0, 20, 14)
sphere(bm, Vector((0, -0.11, top + 0.14)), (0.085, 0.085, 0.085), 0, 16, 12)
sphere(bm, Vector((0, -0.11, top + 0.235)), (0.02, 0.05, 0.035), 1, 10, 6)
sphere(bm, Vector((0, -0.2, top + 0.13)), (0.03, 0.04, 0.02), 2, 10, 6)
for sx in (1, -1):
    sphere(bm, Vector((sx * 0.045, -0.185, top + 0.16)), (0.014, 0.01, 0.016), 3, 8, 6)
    sphere(bm, Vector((sx * 0.14, 0.04, top + 0.06)), (0.03, 0.1, 0.06), 0, 10, 8)
bone_child(new_obj("Hat_Chicken", bm, ["FixedWhite", "FixedRed", "FixedOrange", "FixedInk"]))
# Flower crown
bm = bmesh.new()
n = 16
for i in range(n):
    a = 2 * math.pi * i / n
    p = HEAD_C + Vector((math.cos(a) * RW * 0.86, math.sin(a) * R_HEAD * HAIR * 0.82, R_HEAD * HEAD_S.z * HAIR * 0.58))
    if i % 2 == 0:
        sphere(bm, p, (0.045, 0.045, 0.03), 0 if i % 4 == 0 else 1, 10, 8)
        sphere(bm, p + Vector((0, 0, 0.022)), (0.016, 0.016, 0.012), 2, 8, 6)
    else:
        sphere(bm, p, (0.05, 0.025, 0.018), 3, 8, 6)
bone_child(new_obj("Hat_FlowerCrown", bm, ["HatAccent", "HatMain", "FixedYellow", "FixedLeaf"]))

# ------------------------------------------------------------------ animations
D = math.radians
def pose_frame(pose):
    for name in GROUPS:
        pb = arm.pose.bones[name]
        pb.rotation_mode = 'XYZ'
        rx, ry, rz, lx, ly, lz = pose.get(name, (0, 0, 0, 0, 0, 0))
        pb.rotation_euler = (D(rx), D(ry), D(rz))
        pb.location = (lx, ly, lz)

def make_action(name, frames, pose_fn, samples, loop=True):
    act = bpy.data.actions.new(name); act.use_fake_user = True
    arm.animation_data_create()
    arm.animation_data.action = act
    for i in range(samples + (1 if loop else 0)):
        t = (i % samples) / samples if loop else i / (samples - 1)
        f = 1 + round(i * (frames - 1) / samples) if loop else 1 + round(t * (frames - 1))
        pose_frame(pose_fn(t))
        for name in GROUPS:
            pb = arm.pose.bones[name]
            pb.keyframe_insert("rotation_euler", frame=f)
            pb.keyframe_insert("location", frame=f)
    act.frame_range = (1, frames)
    return act

S = lambda t, k=1, ph=0: math.sin(2 * math.pi * (k * t + ph))

def idle(t):
    return {"hips": (0, 0, 0, 0, 0.008 * S(t), 0), "spine": (2 * S(t), 0, 0, 0, 0, 0),
            "head": (-2 * S(t, 1, 0.2), 0, 3 * S(t, 1, 0.1), 0, 0, 0),
            "arm.L": (2 * S(t), 0, 6 + 3 * S(t), 0, 0, 0), "arm.R": (2 * S(t), 0, -6 - 3 * S(t), 0, 0, 0)}

def walk(t):
    s = S(t)
    return {"hips": (0, 6 * s, 0, 0, 0.015 * (1 - math.cos(4 * math.pi * t)) / 2, 0), "spine": (4, -4 * s, 0, 0, 0, 0),
            "head": (-3, 3 * s, 0, 0, 0, 0), "leg.L": (32 * s, 0, 0, 0, 0, 0), "leg.R": (-32 * s, 0, 0, 0, 0, 0),
            "arm.L": (-30 * s, 0, 8, 0, 0, 0), "arm.R": (30 * s, 0, -8, 0, 0, 0)}

def run(t):
    s = S(t)
    return {"hips": (0, 10 * s, 0, 0, 0.03 * (1 - math.cos(4 * math.pi * t)) / 2, 0), "spine": (14, -6 * s, 0, 0, 0, 0),
            "head": (-10, 4 * s, 0, 0, 0, 0), "leg.L": (52 * s, 0, 0, 0, 0, 0), "leg.R": (-52 * s, 0, 0, 0, 0, 0),
            "arm.L": (-62 * s, 0, 12, 0, 0, 0), "arm.R": (62 * s, 0, -12, 0, 0, 0)}

def jump(t):
    if t < 0.25:   # crouch
        k = t / 0.25
        return {"hips": (0, 0, 0, 0, -0.04 * k, 0), "spine": (10 * k, 0, 0, 0, 0, 0), "leg.L": (20 * k, 0, 0, 0, 0, 0), "leg.R": (20 * k, 0, 0, 0, 0, 0),
                "arm.L": (-25 * k, 0, 10, 0, 0, 0), "arm.R": (-25 * k, 0, -10, 0, 0, 0)}
    k = min(1, (t - 0.25) / 0.35)   # spring up -> tucked air pose
    return {"hips": (0, 0, 0, 0, -0.04 * (1 - k), 0), "spine": (10 - 15 * k, 0, 0, 0, 0, 0),
            "head": (-5 * k, 0, 0, 0, 0, 0), "leg.L": (20 + 20 * k, 0, 0, 0, 0, 0), "leg.R": (20 - 5 * k, 0, 0, 0, 0, 0),
            "arm.L": (-25 * (1 - k), 0, 10 + 125 * k, 0, 0, 0), "arm.R": (-25 * (1 - k), 0, -10 - 125 * k, 0, 0, 0)}

def wave(t):
    return {"hips": (0, 0, 0, 0, 0.006 * S(t, 2), 0), "head": (0, 0, -8, 0, 0, 0),
            "arm.R": (15, 0, -150 + 18 * S(t, 2), 0, 0, 0), "arm.L": (0, 0, 6, 0, 0, 0)}

def cheer(t):
    b = abs(S(t))
    return {"hips": (0, 0, 0, 0, 0.06 * b, 0), "spine": (-4 * b, 0, 0, 0, 0, 0), "head": (-8 * b, 0, 0, 0, 0, 0),
            "arm.L": (0, 0, 150 + 12 * S(t, 2), 0, 0, 0), "arm.R": (0, 0, -150 - 12 * S(t, 2), 0, 0, 0),
            "leg.L": (8 * b, 0, 0, 0, 0, 0), "leg.R": (-8 * b, 0, 0, 0, 0, 0)}

bpy.context.view_layer.objects.active = arm
bpy.ops.object.mode_set(mode='POSE')
acts = [make_action("Idle", 60, idle, 8), make_action("Walk", 24, walk, 8), make_action("Run", 16, run, 8),
        make_action("Jump", 18, jump, 7, loop=False), make_action("Wave", 30, wave, 8), make_action("Cheer", 24, cheer, 8)]
pose_frame({})
bpy.ops.object.mode_set(mode='OBJECT')
arm.animation_data.action = bpy.data.actions["Idle"]
scene.frame_start, scene.frame_end = 1, 60

# ------------------------------------------------------------------ save + export
os.makedirs(os.path.dirname(FBX_PATH), exist_ok=True)
os.makedirs(os.path.dirname(BLEND_PATH), exist_ok=True)
bpy.ops.object.select_all(action='DESELECT')
for o in bpy.data.objects: o.select_set(True)
bpy.ops.export_scene.fbx(
    filepath=FBX_PATH, use_selection=True, object_types={'ARMATURE', 'MESH'},
    apply_scale_options='FBX_SCALE_ALL', axis_forward='-Z', axis_up='Y',
    add_leaf_bones=False, primary_bone_axis='Y', secondary_bone_axis='X',
    mesh_smooth_type='FACE', use_armature_deform_only=False,
    bake_anim=True, bake_anim_use_all_actions=True, bake_anim_use_nla_strips=False,
    bake_anim_force_startend_keying=True, bake_anim_simplify_factor=0.0)
bpy.ops.wm.save_as_mainfile(filepath=BLEND_PATH)
print("FARM CHAOS CHIBI DONE ->", FBX_PATH)
