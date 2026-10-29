# =====================================================================================================
#  FARM CHAOS - chibi "figurine" character for Blender 4.x  (no add-ons, no external assets)
#
#  Run: Blender > Scripting > New > paste (or open this file) > Run Script.   It starts from a clean scene.
#
#  What it makes
#   * super-deformed chibi: oversized round head, tiny body, short arms/legs, pastel toy shading
#   * HAIR (built from custom swept meshes - no spheres / blobs):
#       - Scalp cap ..... a smooth shell copied from the skull, cut with a curved hairline so the face is open
#       - Locks ......... every bang / side lock / back lock is ONE swept mesh: a spline spine laid over the
#                         skull, a flat lens-shaped cross-section, widest just after the root, tapering to a
#                         single point; roots start inside the cap so nothing floats; tips curl gently inward
#       - 4 hairstyles .. Bob (hero), Long, Pigtails (twin tails) and Spiky (short) - same lock system
#   * face: two black oval eyes with white highlights + a tiny curved mouth; set_expression("happy") etc.
#   * rig + 6 animations (Idle/Walk/Run/Jump/Wave/Cheer), 5 hats, materials named for Unity recolouring
#   * soft studio lighting, camera, renders: three-quarter / front / side (+ back) into Art/renders/
#   * exports Assets/Models/Chibi/Chibi.fbx for the Unity project (face features stay Blender-only;
#     Unity swaps face pictures on the "Face" shell instead)
#
#  Options (edit below): VIEW = "3Q" | "FRONT" | "SIDE" | "BACK" | "ALL",   HERO_HAIR, EXPRESSION
# =====================================================================================================
import bpy, bmesh, math, os
from mathutils import Matrix, Vector

VIEW = "ALL"                 # which render(s) to make; "FRONT" is handy for checking symmetry
HERO_HAIR = "Bob"            # hairstyle shown in the renders: Bob / Long / Pigtails / Spiky
EXPRESSION = "smile"         # smile / happy / surprised / sleepy
DO_RENDER, DO_EXPORT = True, True

PROJECT = r"C:\Users\shere\Farm-Chaos-2026-10-22_16-48-36"
FBX_PATH = os.path.join(PROJECT, "Assets", "Models", "Chibi", "Chibi.fbx")
BLEND_PATH = os.path.join(PROJECT, "Art", "Chibi.blend")
RENDER_DIR = os.path.join(PROJECT, "Art", "renders")

# ----------------------------------------------------------------------------------- clean scene
if bpy.context.object and bpy.context.object.mode != 'OBJECT':
    bpy.ops.object.mode_set(mode='OBJECT')
for o in list(bpy.data.objects): bpy.data.objects.remove(o, do_unlink=True)
for coll in (bpy.data.meshes, bpy.data.armatures, bpy.data.materials, bpy.data.actions, bpy.data.cameras, bpy.data.lights):
    for d in list(coll): coll.remove(d)
scene = bpy.context.scene
scene.render.fps = 30
col = scene.collection

# ----------------------------------------------------------------------------------- materials
def hexc(h):
    h = h.lstrip('#'); c = [int(h[i:i + 2], 16) / 255 for i in (0, 2, 4)]
    return tuple(((x + 0.055) / 1.055) ** 2.4 if x > 0.04045 else x / 12.92 for x in c)   # sRGB -> linear

def set_input(bsdf, names, value):
    for n in names:
        if n in bsdf.inputs: bsdf.inputs[n].default_value = value; return

def mat(name, hexcol, rough=0.5, sss=0.0, coat=0.0, sheen=0.0, emit=0.0, alpha=1.0):
    m = bpy.data.materials.new(name)
    m.use_nodes = True
    rgb = hexc(hexcol)
    m.diffuse_color = (*rgb, 1)
    b = m.node_tree.nodes.get("Principled BSDF")
    b.inputs["Base Color"].default_value = (*rgb, 1)
    b.inputs["Roughness"].default_value = rough
    set_input(b, ["Subsurface Weight", "Subsurface"], sss)
    if sss: set_input(b, ["Subsurface Radius"], (0.6, 0.3, 0.2))
    set_input(b, ["Coat Weight", "Clearcoat"], coat)
    set_input(b, ["Sheen Weight", "Sheen"], sheen)
    if emit:
        set_input(b, ["Emission Color", "Emission"], (*rgb, 1)); set_input(b, ["Emission Strength"], emit)
    if alpha < 1.0:
        b.inputs["Alpha"].default_value = alpha
        if hasattr(m, "surface_render_method"): m.surface_render_method = 'DITHERED'
        if hasattr(m, "blend_method"): m.blend_method = 'HASHED'
    return m

M = {
    # recolourable in Unity (by name)
    "Skin": mat("Skin", "#FFE6DA", 0.55, sss=0.15), "Shirt": mat("Shirt", "#FFFFFF", 0.7, sheen=0.3),
    "Outfit": mat("Outfit", "#A9D3F5", 0.75, sheen=0.4), "Shoes": mat("Shoes", "#8A5A50", 0.3, coat=0.6),
    "Hair": mat("Hair", "#7B4F47", 0.45, coat=0.1), "Accent": mat("Accent", "#F6A9BD", 0.5),
    "Socks": mat("Socks", "#FFFFFF", 0.8), "HatMain": mat("HatMain", "#F6B9C8", 0.7, sheen=0.4),
    "HatAccent": mat("HatAccent", "#FFFFFF", 0.6),
    "Face": mat("Face", "#FFFFFF", 1.0, alpha=0.0),            # invisible here; Unity puts face pictures on it
    "FixedWhite": mat("FixedWhite", "#FFFFFF"), "FixedRed": mat("FixedRed", "#E0505C"),
    "FixedOrange": mat("FixedOrange", "#F5A623"), "FixedInk": mat("FixedInk", "#2E2A3A"),
    "FixedLeaf": mat("FixedLeaf", "#7FC98A"), "FixedYellow": mat("FixedYellow", "#FFE07A"),
    # Blender-only face features
    "EyeBlack": mat("EyeBlack", "#1D1820", 0.12, coat=1.0), "EyeShine": mat("EyeShine", "#FFFFFF", 0.2, emit=2.0),
    "Mouth": mat("Mouth", "#8E3E4C", 0.4), "Blush": mat("Blush", "#F7A3B5", 0.8, alpha=0.65),
    "Stage": mat("Stage", "#A8C8EE", 0.9),
}

# ----------------------------------------------------------------------------------- geometry helpers
R_HEAD = 0.30
HEAD_C = Vector((0, 0, 0.88))
HEAD_S = Vector((1.12, 1.0, 0.98))       # slightly wide, slightly squashed = cute
D = math.radians

def faces_of(vs, mi):
    fs = set(f for v in vs if v.is_valid for f in v.link_faces)
    for f in fs: f.material_index = mi; f.smooth = True
    return fs

def weight(bm, vs, gi):
    dl = bm.verts.layers.deform.verify()
    for v in vs:
        if v.is_valid: v[dl][gi] = 1.0

def sphere(bm, centre, radii, mi, seg=24, rings=16, rot=None):
    vs = bmesh.ops.create_uvsphere(bm, u_segments=seg, v_segments=rings, radius=1.0)['verts']
    for v in vs:
        p = Vector((v.co.x * radii[0], v.co.y * radii[1], v.co.z * radii[2]))
        v.co = (rot @ p if rot else p) + Vector(centre)
    faces_of(vs, mi)
    return vs

def capsule(bm, a, b, r, mi, seg=16, rings=12):
    a, b = Vector(a), Vector(b); L = (b - a).length
    vs = bmesh.ops.create_uvsphere(bm, u_segments=seg, v_segments=rings, radius=r)['verts']
    for v in vs: v.co.z += L / 2 if v.co.z >= 0 else -L / 2
    mtx = Matrix.Translation((a + b) / 2) @ (b - a).normalized().to_track_quat('Z', 'Y').to_matrix().to_4x4()
    for v in vs: v.co = mtx @ v.co
    faces_of(vs, mi)
    return vs

def cone(bm, base, r1, r2, depth, mi, direction=(0, 0, 1), seg=16):
    d = Vector(direction).normalized()
    mtx = Matrix.Translation(Vector(base) + d * depth / 2) @ d.to_track_quat('Z', 'Y').to_matrix().to_4x4()
    vs = bmesh.ops.create_cone(bm, cap_ends=True, cap_tris=False, segments=seg, radius1=r1, radius2=r2, depth=depth, matrix=mtx)['verts']
    faces_of(vs, mi)
    return vs

def ring(bm, zc, x, r, depth, mi, seg=20):
    return cone(bm, (x, 0, zc - depth / 2), r, r, depth, mi, (0, 0, 1), seg)

def hdir(az, el):
    """Direction from the head centre. az: degrees round from the front (-Y) toward the character's left (+X); el: up."""
    a, e = D(az), D(el)
    return Vector((math.sin(a) * math.cos(e), -math.cos(a) * math.cos(e), math.sin(e)))

def S(az, el, grow=1.0):
    """Point on (or off, grow>1) the head surface."""
    d = hdir(az, el)
    return HEAD_C + Vector((d.x * R_HEAD * HEAD_S.x, d.y * R_HEAD * HEAD_S.y, d.z * R_HEAD * HEAD_S.z)) * grow

def face_pt(xf, zf, grow=1.0):
    """Point on the front of the face from fractions of the head's half-width (xf) and half-height (zf)."""
    yy = -math.sqrt(max(0.02, 1 - xf * xf - zf * zf))
    return HEAD_C + Vector((xf * R_HEAD * HEAD_S.x, yy * R_HEAD * HEAD_S.y, zf * R_HEAD * HEAD_S.z)) * grow

def face_frame(xf, zf):
    """Rotation whose local Y is the head-surface normal at that face point, X horizontal."""
    yy = -math.sqrt(max(0.02, 1 - xf * xf - zf * zf))
    n = Vector((xf / HEAD_S.x, yy / HEAD_S.y, zf / HEAD_S.z)).normalized()
    x = Vector((0, 0, 1)).cross(n).normalized() * -1      # points to the character's left (+X) when facing -Y
    if x.x < 0: x = -x
    z = x.cross(n).normalized()
    if z.z < 0: z = -z
    return Matrix((x, n, z)).transposed()

def catmull(pts, n):
    """Smooth spline through control points, n samples."""
    P = [Vector(p) for p in pts]
    P = [P[0] + (P[0] - P[1])] + P + [P[-1] + (P[-1] - P[-2])]
    segs = len(P) - 3; out = []
    for i in range(n):
        t = i / (n - 1) * segs; k = min(int(t), segs - 1); u = t - k
        p0, p1, p2, p3 = P[k], P[k + 1], P[k + 2], P[k + 3]
        out.append(0.5 * ((2 * p1) + (-p0 + p2) * u + (2 * p0 - 5 * p1 + 4 * p2 - p3) * u * u + (-p0 + 3 * p1 - 3 * p2 + p3) * u ** 3))
    return out

def sweep(bm, pts, width, thick, mi=0, samples=26, ring_n=10, tip=True, up_hint=None):
    """
    THE HAIR LOCK BUILDER.  Sweeps a flat lens cross-section along a smooth spine.
      width(t), thick(t): size along the lock (t=0 root .. 1 tip)
      tip=True collapses the last ring to a single point -> gently pointed end
      the cross-section's flat side always faces away from the head (or from up_hint), so locks lie on the skull
    """
    path = catmull(pts, samples)
    rings, verts = [], []
    for i, p in enumerate(path):
        t = i / (samples - 1)
        T = (path[min(i + 1, samples - 1)] - path[max(i - 1, 0)]).normalized()
        out = (p - (up_hint if up_hint is not None else HEAD_C))
        N = (out - T * out.dot(T)).normalized()
        B = T.cross(N).normalized(); N = B.cross(T).normalized()
        if tip and i == samples - 1:
            v = bm.verts.new(p); rings.append([v]); verts.append(v); continue
        w, th = max(width(t), 0.002), max(thick(t), 0.002)
        rv = []
        for j in range(ring_n):
            a = 2 * math.pi * j / ring_n
            c, s = math.cos(a), math.sin(a)
            # lens profile: flat-ish top & bottom, soft edges
            q = p + B * (c * w / 2) + N * (math.copysign(abs(s) ** 0.8, s) * th / 2)
            v = bm.verts.new(q); rv.append(v); verts.append(v)
        rings.append(rv)
    for a_ring, b_ring in zip(rings, rings[1:]):
        if len(b_ring) == 1:
            for j in range(ring_n): bm.faces.new((a_ring[j], a_ring[(j + 1) % ring_n], b_ring[0]))
        else:
            for j in range(ring_n): bm.faces.new((a_ring[j], a_ring[(j + 1) % ring_n], b_ring[(j + 1) % ring_n], b_ring[j]))
    bm.faces.new(list(reversed(rings[0])))                     # root cap (buried in the scalp)
    if not tip: bm.faces.new(rings[-1])
    faces_of(verts, mi)
    return verts

def taper(w0, belly=0.25, power=0.85):
    """Hair-lock width: a little fuller just after the root, then a long taper to a point."""
    return lambda t: w0 * (1 - t) ** power * (1 + belly * math.sin(math.pi * min(1.0, t * 1.6)))

def new_obj(name, bm, mats, parent=None, subsurf=0):
    me = bpy.data.meshes.new(name)
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces[:])
    bm.normal_update(); bm.to_mesh(me); bm.free()
    for m in mats: me.materials.append(M[m])
    ob = bpy.data.objects.new(name, me); col.objects.link(ob)
    for p in me.polygons: p.use_smooth = True
    if subsurf:
        mod = ob.modifiers.new("Smooth", 'SUBSURF'); mod.levels = subsurf; mod.render_levels = subsurf + 1
    if parent is not None: attach(ob, parent)
    return ob

# ----------------------------------------------------------------------------------- armature (simple, game-ready)
BONES = {
    "hips":  ((0, 0, 0.36), (0, 0, 0.46), None),
    "spine": ((0, 0, 0.46), (0, 0, 0.58), "hips"),
    "head":  ((0, 0, 0.58), (0, 0, 0.90), "spine"),
    "arm.L": ((0.13, 0, 0.555), (0.19, 0, 0.38), "spine"),
    "arm.R": ((-0.13, 0, 0.555), (-0.19, 0, 0.38), "spine"),
    "leg.L": ((0.062, 0, 0.37), (0.065, 0, 0.05), "hips"),
    "leg.R": ((-0.062, 0, 0.37), (-0.065, 0, 0.05), "hips"),
}
arm_data = bpy.data.armatures.new("Chibi_Rig")
arm = bpy.data.objects.new("Chibi_Rig", arm_data); col.objects.link(arm)
bpy.context.view_layer.objects.active = arm; arm.select_set(True)
bpy.ops.object.mode_set(mode='EDIT')
for name, (h, t, p) in BONES.items():
    eb = arm_data.edit_bones.new(name); eb.head, eb.tail = Vector(h), Vector(t)
    eb.align_roll(Vector((0, -1, 0)))
    if p: eb.parent = arm_data.edit_bones[p]
bpy.ops.object.mode_set(mode='OBJECT')
GROUPS = list(BONES.keys()); G = {n: i for i, n in enumerate(GROUPS)}

def attach(ob, parent):
    """Parent to the head bone (str 'head') or to another object, keeping the world position."""
    world = ob.matrix_world.copy()
    if parent == "head":
        ob.parent = arm; ob.parent_type = 'BONE'; ob.parent_bone = "head"
    else:
        ob.parent = parent
    bpy.context.view_layer.update()
    ob.matrix_world = world

def group(name):
    e = bpy.data.objects.new(name, None); col.objects.link(e); e.empty_display_size = 0.05
    attach(e, "head")
    return e

# ----------------------------------------------------------------------------------- body (one skinned mesh)
BODY_MATS = ["Skin", "Shirt", "Outfit", "Shoes", "Accent", "Face", "Socks"]
MI = {n: i for i, n in enumerate(BODY_MATS)}
bm = bmesh.new(); bm.verts.layers.deform.verify(); bm.loops.layers.uv.verify()
weight(bm, sphere(bm, HEAD_C, R_HEAD * HEAD_S, MI["Skin"], 48, 36), G["head"])                 # big round head
weight(bm, capsule(bm, (0, 0, 0.54), (0, 0, 0.62), 0.04, MI["Skin"]), G["spine"])             # neck
weight(bm, sphere(bm, (0, 0, 0.50), (0.115, 0.092, 0.1), MI["Shirt"]), G["spine"])            # tee
for side, sx in (("L", 1), ("R", -1)):
    weight(bm, sphere(bm, (sx * 0.122, 0, 0.545), (0.058, 0.055, 0.05), MI["Shirt"], 16, 12), G["arm." + side])   # puff sleeve
weight(bm, sphere(bm, (0, 0, 0.43), (0.125, 0.1, 0.08), MI["Outfit"]), G["hips"])             # overall seat
weight(bm, sphere(bm, (0, -0.075, 0.50), (0.075, 0.03, 0.065), MI["Outfit"], 16, 10), G["spine"])   # bib
for sx in (1, -1):
    weight(bm, capsule(bm, (sx * 0.05, -0.088, 0.53), (sx * 0.075, -0.03, 0.605), 0.012, MI["Accent"], 8, 6), G["spine"])
    weight(bm, capsule(bm, (sx * 0.075, 0.03, 0.605), (sx * 0.05, 0.088, 0.48), 0.012, MI["Accent"], 8, 6), G["spine"])
    weight(bm, sphere(bm, (sx * 0.05, -0.104, 0.528), (0.016, 0.01, 0.014), MI["Accent"], 10, 6), G["spine"])
for side, sx in (("L", 1), ("R", -1)):
    b = G["leg." + side]
    weight(bm, capsule(bm, (sx * 0.062, 0, 0.42), (sx * 0.064, 0, 0.335), 0.056, MI["Outfit"], 16, 10), b)
    weight(bm, ring(bm, 0.322, sx * 0.064, 0.064, 0.03, MI["Shirt"]), b)                      # rolled cuff
    weight(bm, capsule(bm, (sx * 0.064, 0, 0.33), (sx * 0.065, 0, 0.14), 0.034, MI["Skin"], 14, 10), b)
    weight(bm, capsule(bm, (sx * 0.065, 0, 0.165), (sx * 0.065, 0, 0.065), 0.04, MI["Socks"], 14, 10), b)
    weight(bm, ring(bm, 0.168, sx * 0.065, 0.046, 0.016, MI["Socks"]), b)
    weight(bm, ring(bm, 0.145, sx * 0.065, 0.042, 0.01, MI["Accent"]), b)
    weight(bm, sphere(bm, (sx * 0.066, -0.024, 0.038), (0.052, 0.078, 0.038), MI["Shoes"], 18, 12), b)
    weight(bm, capsule(bm, (sx * 0.035, -0.04, 0.07), (sx * 0.097, -0.04, 0.07), 0.009, MI["Shoes"], 8, 6), b)
for side, sx in (("L", 1), ("R", -1)):
    b = G["arm." + side]
    weight(bm, capsule(bm, (sx * 0.148, 0, 0.53), (sx * 0.182, 0, 0.405), 0.03, MI["Skin"], 14, 10), b)
    weight(bm, sphere(bm, (sx * 0.188, -0.005, 0.385), (0.036, 0.033, 0.038), MI["Skin"], 14, 10), b)
# face shell for Unity (front-on UVs; Unity swaps the expression pictures here)
fvs = bmesh.ops.create_uvsphere(bm, u_segments=48, v_segments=36, radius=1.0)['verts']
bmesh.ops.delete(bm, geom=[v for v in fvs if v.co.y > -0.25], context='VERTS')
keep = [v for v in fvs if v.is_valid]
uvl = bm.loops.layers.uv.verify(); FACE_W = 0.8
for v in keep: v.co = HEAD_C + Vector((v.co.x * R_HEAD * HEAD_S.x, v.co.y * R_HEAD * HEAD_S.y, v.co.z * R_HEAD * HEAD_S.z)) * 1.006
for f in set(f for v in keep for f in v.link_faces):
    f.material_index = MI["Face"]; f.smooth = True
    for l in f.loops:
        p = l.vert.co - HEAD_C
        l[uvl].uv = (0.5 + p.x / (2 * FACE_W * R_HEAD * HEAD_S.x), 0.5 + p.z / (2 * FACE_W * R_HEAD * HEAD_S.z))
weight(bm, keep, G["head"])
body = new_obj("Chibi_Body", bm, BODY_MATS)
for n in GROUPS: body.vertex_groups.new(name=n)
body.parent = arm
body.modifiers.new("Rig", 'ARMATURE').object = arm

# ----------------------------------------------------------------------------------- face features (Blender)
EYE_X, EYE_Z = 0.33, -0.12      # same placement as the Unity face pictures

def eye_ovals(bm, scale=1.0):
    for sx in (-1, 1):
        R = face_frame(sx * EYE_X, EYE_Z)
        c = face_pt(sx * EYE_X, EYE_Z, 1.0)
        sphere(bm, c, (0.033 * scale, 0.012, 0.046 * scale), 0, 20, 14, R)                                  # black oval
        sphere(bm, c + R @ Vector((-0.011 * scale, 0.009, 0.017 * scale)), (0.011 * scale, 0.004, 0.013 * scale), 1, 12, 8, R)   # big shine
        sphere(bm, c + R @ Vector((0.012 * scale, 0.009, -0.017 * scale)), (0.005, 0.003, 0.005), 1, 8, 6, R)                    # tiny shine

def face_line(bm, pts_frac, w, th, mi=0):
    pts = [face_pt(x, z, 1.014) for x, z in pts_frac]
    sweep(bm, pts, lambda t: w, lambda t: th, mi, samples=14, ring_n=8, tip=False)

face_objs = {}
bm = bmesh.new(); eye_ovals(bm); face_objs["Face_Eyes_Open"] = new_obj("Face_Eyes_Open", bm, ["EyeBlack", "EyeShine"], "head")
bm = bmesh.new(); eye_ovals(bm, 1.18); face_objs["Face_Eyes_Big"] = new_obj("Face_Eyes_Big", bm, ["EyeBlack", "EyeShine"], "head")
bm = bmesh.new()
for sx in (-1, 1):   # ^ ^ happy arcs
    face_line(bm, [(sx * EYE_X + u * 0.065, EYE_Z - 0.01 + 0.04 * (1 - u * u)) for u in (-1, -0.5, 0, 0.5, 1)], 0.016, 0.01)
face_objs["Face_Eyes_Happy"] = new_obj("Face_Eyes_Happy", bm, ["EyeBlack"], "head")
bm = bmesh.new()
for sx in (-1, 1):   # sleepy closed lids
    face_line(bm, [(sx * EYE_X + u * 0.065, EYE_Z - 0.02 - 0.03 * (1 - u * u)) for u in (-1, -0.5, 0, 0.5, 1)], 0.014, 0.009)
face_objs["Face_Eyes_Closed"] = new_obj("Face_Eyes_Closed", bm, ["EyeBlack"], "head")
bm = bmesh.new(); face_line(bm, [(u * 0.05, -0.42 - 0.02 * (1 - u * u)) for u in (-1, -0.5, 0, 0.5, 1)], 0.011, 0.008)
face_objs["Face_Mouth_Smile"] = new_obj("Face_Mouth_Smile", bm, ["Mouth"], "head")
bm = bmesh.new(); sphere(bm, face_pt(0, -0.44, 1.0), (0.03, 0.009, 0.022), 0, 16, 10, face_frame(0, -0.44))
face_objs["Face_Mouth_Open"] = new_obj("Face_Mouth_Open", bm, ["Mouth"], "head")
bm = bmesh.new(); sphere(bm, face_pt(0, -0.44, 1.0), (0.016, 0.009, 0.02), 0, 14, 10, face_frame(0, -0.44))
face_objs["Face_Mouth_O"] = new_obj("Face_Mouth_O", bm, ["Mouth"], "head")
bm = bmesh.new()
for sx in (-1, 1): sphere(bm, face_pt(sx * 0.56, -0.3, 1.0), (0.05, 0.006, 0.026), 0, 16, 8, face_frame(sx * 0.56, -0.3))
new_obj("Face_Blush", bm, ["Blush"], "head")

EXPRESSIONS = {
    "smile":     ("Face_Eyes_Open", "Face_Mouth_Smile"),
    "happy":     ("Face_Eyes_Happy", "Face_Mouth_Open"),
    "surprised": ("Face_Eyes_Big", "Face_Mouth_O"),
    "sleepy":    ("Face_Eyes_Closed", "Face_Mouth_Smile"),
}
def set_expression(name):
    """Swap eye + mouth pieces. Add new expressions by modelling another pair and listing it above."""
    show = EXPRESSIONS[name]
    for n, ob in face_objs.items():
        on = n in show
        ob.hide_render = not on; ob.hide_viewport = not on

# ----------------------------------------------------------------------------------- HAIR
HAIR_CAP = 1.045      # scalp cap sits just off the skull
LOCK_THICK = 0.045

def scalp_cap(name, parent, back_el=-58, side_el=-40, hair_front=30):
    """Smooth skull-hugging cap with a curved hairline (lower in the middle, higher at the temples)."""
    bm = bmesh.new()
    vs = bmesh.ops.create_uvsphere(bm, u_segments=48, v_segments=36, radius=1.0)['verts']
    def keep(c):
        az = math.degrees(math.atan2(c.x, -c.y)); el = math.degrees(math.asin(max(-1, min(1, c.z))))
        a = abs(az)
        if a < 64 and el < hair_front + 10 * (a / 64) ** 2: return False     # face window with a soft curve
        bottom = side_el + (back_el - side_el) * min(1, max(0, (a - 70) / 70))
        return el > bottom
    bmesh.ops.delete(bm, geom=[v for v in vs if not keep(v.co)], context='VERTS')
    left = [v for v in vs if v.is_valid]
    for v in left: v.co = HEAD_C + Vector((v.co.x * R_HEAD * HEAD_S.x, v.co.y * R_HEAD * HEAD_S.y, v.co.z * R_HEAD * HEAD_S.z)) * HAIR_CAP
    faces_of(left, 0)
    return new_obj(name, bm, ["Hair"], parent, subsurf=1)

def bangs(bm, length=1.0, flick=0.0):
    """Fringe: 6 wide, soft clumps with a gentle centre part. Each lock is fuller in the middle and ends
    in one gently pointed tip; sides come lower so the bangs frame the face."""
    us = (-1.0, -0.6, -0.2, 0.2, 0.6, 1.0)
    for u in us:
        az = u * 52
        end_el = (14 - 8 * abs(u) ** 1.5) + (1 - length) * 22
        w = 0.17 - 0.025 * abs(u)
        part = math.copysign(4, u)                 # tips lean away from the centre part
        pts = [S(az * 0.25, 76, 0.985), S(az * 0.7, 54, 1.07), S(az * 0.95, 32, 1.085),
               S(az * 1.0 + part * 0.5, end_el + 7, 1.07), S(az * 1.0 + part + u * flick * 20, end_el, 1.045 + flick * 0.08)]
        sweep(bm, pts, taper(w, 0.35, 1.05), lambda t: LOCK_THICK * (1 - 0.55 * t), 0)

def side_locks(bm, length=1.0):
    """Cheek-framing locks in front of the ears + a slimmer lock just inside them."""
    for sx in (-1, 1):
        drop = -42 - (length - 1) * 60
        sweep(bm, [S(sx * 70, 52, 0.985), S(sx * 82, 24, 1.085), S(sx * 81, -12, 1.085), S(sx * 74, drop, 1.06), S(sx * 64, drop - 7, 1.035)],
              taper(0.12, 0.2), lambda t: LOCK_THICK * (1 - 0.5 * t), 0)
        sweep(bm, [S(sx * 56, 58, 0.985), S(sx * 61, 30, 1.08), S(sx * 63, 4, 1.08), S(sx * 60, -22, 1.055), S(sx * 55, -27, 1.035)],
              taper(0.075, 0.2), lambda t: LOCK_THICK * 0.8 * (1 - 0.5 * t), 0)

def back_locks(bm, end_el=-54, flick=-0.03, extra_drop=0.0, n=8, az0=80, az1=280):
    """Broad locks from the crown, hugging the skull down the sides/back; a bob curls IN at the nape
    (negative flick). extra_drop lets long hair continue below the head."""
    for i in range(n):
        az = az0 + (az1 - az0) * i / (n - 1)
        pts = [S(az, 66, 0.985), S(az, 38, 1.07), S(az, 4, 1.08), S(az, (end_el + 4) * 0.65, 1.075), S(az, end_el, 1.07 + flick)]
        if extra_drop > 0:
            last = pts[-1]; pts[-1] = S(az, end_el, 1.07)
            pts += [last + Vector((0, 0.015, -extra_drop * 0.5)), last + Vector((0, 0.0, -extra_drop))]
        sweep(bm, pts, taper(0.22, 0.12, 0.8), lambda t: LOCK_THICK * 1.1 * (1 - 0.5 * t), 0, samples=30)

def ahoge(bm):
    """A single curved 'antenna' lock on top - very figurine."""
    top = S(12, 86, 0.99)
    pts = [top, top + Vector((0.01, -0.02, 0.07)), top + Vector((0.05, -0.04, 0.12)), top + Vector((0.1, -0.02, 0.12)), top + Vector((0.115, 0.01, 0.09))]
    sweep(bm, pts, taper(0.05, 0.3, 0.9), lambda t: 0.02 * (1 - 0.6 * t), 0, samples=22, up_hint=top + Vector((0.05, 0.2, 0.0)))

def twin_tail(bm, sx):
    """Big tapered tail from the side of the head, with a bulge mid-way like a figurine pigtail."""
    root = S(sx * 112, 22, 0.99)
    p1 = S(sx * 114, 20, 1.2)
    pts = [root, p1, p1 + Vector((sx * 0.07, 0.03, -0.16)), p1 + Vector((sx * 0.1, 0.03, -0.36)), p1 + Vector((sx * 0.07, 0.0, -0.52)), p1 + Vector((sx * 0.03, -0.03, -0.6))]
    sweep(bm, pts, lambda t: 0.16 * (0.5 + 0.8 * math.sin(math.pi * min(1, t * 1.05))) * (1 - t) ** 0.55,
          lambda t: 0.1 * (0.5 + 0.7 * math.sin(math.pi * min(1, t * 1.05))) * (1 - t) ** 0.55, 0, samples=32, ring_n=12,
          up_hint=HEAD_C + Vector((sx * 0.6, 0.6, 0.2)))
    return p1, (pts[2] - p1).normalized()

def make_hairstyle(style):
    grp = group("Hair_" + style)
    if style == "Bob":
        scalp_cap("Hair_Bob_Cap", grp)
        bm = bmesh.new(); bangs(bm); new_obj("Hair_Bob_Bangs", bm, ["Hair"], grp, 1)
        bm = bmesh.new(); side_locks(bm); new_obj("Hair_Bob_SideLocks", bm, ["Hair"], grp, 1)
        bm = bmesh.new(); back_locks(bm); new_obj("Hair_Bob_Back", bm, ["Hair"], grp, 1)
        bm = bmesh.new(); ahoge(bm); new_obj("Hair_Bob_Ahoge", bm, ["Hair"], grp, 1)
        bm = bmesh.new()   # little hair clip (two short bars) - accent colour
        for k in (0, 1):
            capsule(bm, S(-38 + k * 6, 40, 1.1), S(-30 + k * 6, 52, 1.1), 0.01, 0, 6, 4)
        new_obj("Hair_Bob_Clip", bm, ["Accent"], grp)
    elif style == "Long":
        scalp_cap("Hair_Long_Cap", grp, back_el=-62)
        bm = bmesh.new(); bangs(bm); new_obj("Hair_Long_Bangs", bm, ["Hair"], grp, 1)
        bm = bmesh.new(); side_locks(bm, 1.45); new_obj("Hair_Long_SideLocks", bm, ["Hair"], grp, 1)
        bm = bmesh.new(); back_locks(bm, end_el=-62, flick=0.0, extra_drop=0.26); new_obj("Hair_Long_Back", bm, ["Hair"], grp, 1)
    elif style == "Pigtails":
        scalp_cap("Hair_Pigtails_Cap", grp, back_el=-48, side_el=-30)
        bm = bmesh.new(); bangs(bm); new_obj("Hair_Pigtails_Bangs", bm, ["Hair"], grp, 1)
        bm = bmesh.new(); side_locks(bm, 0.9); new_obj("Hair_Pigtails_SideLocks", bm, ["Hair"], grp, 1)
        bm = bmesh.new(); back_locks(bm, end_el=-40, flick=0.0); new_obj("Hair_Pigtails_Back", bm, ["Hair"], grp, 1)
        bm = bmesh.new(); ties = bmesh.new()
        for sx in (-1, 1):
            p, d = twin_tail(bm, sx)
            cone(ties, p - d * 0.015, 0.045, 0.045, 0.03, 0, d, 20)       # hair tie ring
        new_obj("Hair_Pigtails_Tails", bm, ["Hair"], grp, 1)
        new_obj("Hair_Pigtails_Ties", ties, ["Accent"], grp, 1)
    elif style == "Spiky":
        scalp_cap("Hair_Spiky_Cap", grp, back_el=-35, side_el=-20, hair_front=36)
        bm = bmesh.new(); bangs(bm, length=0.55, flick=0.6); new_obj("Hair_Spiky_Bangs", bm, ["Hair"], grp, 1)
        bm = bmesh.new(); back_locks(bm, end_el=-22, flick=0.12, n=8); new_obj("Hair_Spiky_Back", bm, ["Hair"], grp, 1)
        bm = bmesh.new()
        for az in (140, 180, 220, 105, 255):    # crown spikes sweeping up and back
            sweep(bm, [S(az, 74, 0.985), S(az, 76, 1.14), S(az, 68, 1.32)], taper(0.11, 0.2), lambda t: LOCK_THICK * (1 - 0.5 * t), 0, samples=18)
        new_obj("Hair_Spiky_Crown", bm, ["Hair"], grp, 1)
    return grp

hair_groups = {s: make_hairstyle(s) for s in ("Bob", "Long", "Pigtails", "Spiky")}

# ----------------------------------------------------------------------------------- hats (sit on top of the hair)
HAIR_TOP = 1.1
top = HEAD_C.z + R_HEAD * HEAD_S.z * HAIR_TOP
RW = R_HEAD * HEAD_S.x * HAIR_TOP
hats = []
bm = bmesh.new(); sphere(bm, (0.02, 0.01, top - 0.005), (RW * 1.0, RW * 0.94, 0.075), 0, 32, 16); cone(bm, (0.02, 0.01, top + 0.055), 0.012, 0.01, 0.045, 0, (0, 0, 1), 8)
hats.append(new_obj("Hat_Beret", bm, ["HatMain"], "head", 1))
bm = bmesh.new()
cone(bm, (0, 0, top - 0.07), RW + 0.13, RW + 0.13, 0.02, 0, (0, 0, 1), 36); cone(bm, (0, 0, top - 0.06), RW * 0.75, RW * 0.62, 0.13, 0, (0, 0, 1), 28)
cone(bm, (0, 0, top - 0.045), RW * 0.76, RW * 0.72, 0.04, 1, (0, 0, 1), 28)
hats.append(new_obj("Hat_Straw", bm, ["HatMain", "HatAccent"], "head"))
bm = bmesh.new()
vs = bmesh.ops.create_uvsphere(bm, u_segments=28, v_segments=18, radius=1.0)['verts']
bmesh.ops.delete(bm, geom=[v for v in vs if v.co.z < 0.1], context='VERTS')
left = [v for v in vs if v.is_valid]
for v in left: v.co = HEAD_C + Vector((v.co.x * RW * 1.02, v.co.y * R_HEAD * HAIR_TOP * 1.04, v.co.z * R_HEAD * HEAD_S.z * HAIR_TOP * 1.02))
faces_of(left, 0)
sphere(bm, (0, -R_HEAD * HAIR_TOP, HEAD_C.z + 0.11), (0.18, 0.13, 0.012), 0, 16, 6)
hats.append(new_obj("Hat_Cap", bm, ["HatMain", "HatAccent"], "head"))
bm = bmesh.new()
sphere(bm, (0, 0.03, top + 0.05), (0.15, 0.17, 0.11), 0, 20, 14); sphere(bm, (0, -0.11, top + 0.14), (0.085, 0.085, 0.085), 0, 16, 12)
sphere(bm, (0, -0.11, top + 0.235), (0.02, 0.05, 0.035), 1, 10, 6); sphere(bm, (0, -0.2, top + 0.13), (0.03, 0.04, 0.02), 2, 10, 6)
for sx in (1, -1):
    sphere(bm, (sx * 0.045, -0.185, top + 0.16), (0.014, 0.01, 0.016), 3, 8, 6); sphere(bm, (sx * 0.14, 0.04, top + 0.06), (0.03, 0.1, 0.06), 0, 10, 8)
hats.append(new_obj("Hat_Chicken", bm, ["FixedWhite", "FixedRed", "FixedOrange", "FixedInk"], "head"))
bm = bmesh.new()
for i in range(16):
    a = 2 * math.pi * i / 16
    p = HEAD_C + Vector((math.cos(a) * RW * 0.86, math.sin(a) * R_HEAD * HAIR_TOP * 0.82, R_HEAD * HEAD_S.z * HAIR_TOP * 0.58))
    if i % 2 == 0:
        sphere(bm, p, (0.045, 0.045, 0.03), 0 if i % 4 == 0 else 1, 10, 8); sphere(bm, p + Vector((0, 0, 0.022)), (0.016, 0.016, 0.012), 2, 8, 6)
    else:
        sphere(bm, p, (0.05, 0.025, 0.018), 3, 8, 6)
hats.append(new_obj("Hat_FlowerCrown", bm, ["HatAccent", "HatMain", "FixedYellow", "FixedLeaf"], "head"))

# ----------------------------------------------------------------------------------- animations (game)
def pose_frame(pose):
    for name in GROUPS:
        pb = arm.pose.bones[name]; pb.rotation_mode = 'XYZ'
        rx, ry, rz, lx, ly, lz = pose.get(name, (0, 0, 0, 0, 0, 0))
        pb.rotation_euler = (D(rx), D(ry), D(rz)); pb.location = (lx, ly, lz)

def make_action(name, frames, pose_fn, samples, loop=True):
    act = bpy.data.actions.new(name); act.use_fake_user = True
    arm.animation_data_create(); arm.animation_data.action = act
    for i in range(samples + (1 if loop else 0)):
        t = (i % samples) / samples if loop else i / (samples - 1)
        f = 1 + round(i * (frames - 1) / samples) if loop else 1 + round(t * (frames - 1))
        pose_frame(pose_fn(t))
        for name in GROUPS:
            pb = arm.pose.bones[name]; pb.keyframe_insert("rotation_euler", frame=f); pb.keyframe_insert("location", frame=f)
    act.frame_range = (1, frames)
    return act

SN = lambda t, k=1, ph=0: math.sin(2 * math.pi * (k * t + ph))
def idle(t): return {"hips": (0, 0, 0, 0, 0.008 * SN(t), 0), "spine": (2 * SN(t), 0, 0, 0, 0, 0), "head": (-2 * SN(t, 1, 0.2), 0, 3 * SN(t, 1, 0.1), 0, 0, 0),
                     "arm.L": (2 * SN(t), 0, 6 + 3 * SN(t), 0, 0, 0), "arm.R": (2 * SN(t), 0, -6 - 3 * SN(t), 0, 0, 0)}
def walk(t):
    s = SN(t)
    return {"hips": (0, 6 * s, 0, 0, 0.015 * (1 - math.cos(4 * math.pi * t)) / 2, 0), "spine": (4, -4 * s, 0, 0, 0, 0), "head": (-3, 3 * s, 0, 0, 0, 0),
            "leg.L": (32 * s, 0, 0, 0, 0, 0), "leg.R": (-32 * s, 0, 0, 0, 0, 0), "arm.L": (-30 * s, 0, 8, 0, 0, 0), "arm.R": (30 * s, 0, -8, 0, 0, 0)}
def run(t):
    s = SN(t)
    return {"hips": (0, 10 * s, 0, 0, 0.03 * (1 - math.cos(4 * math.pi * t)) / 2, 0), "spine": (14, -6 * s, 0, 0, 0, 0), "head": (-10, 4 * s, 0, 0, 0, 0),
            "leg.L": (52 * s, 0, 0, 0, 0, 0), "leg.R": (-52 * s, 0, 0, 0, 0, 0), "arm.L": (-62 * s, 0, 12, 0, 0, 0), "arm.R": (62 * s, 0, -12, 0, 0, 0)}
def jump(t):
    if t < 0.25:
        k = t / 0.25
        return {"hips": (0, 0, 0, 0, -0.04 * k, 0), "spine": (10 * k, 0, 0, 0, 0, 0), "leg.L": (20 * k, 0, 0, 0, 0, 0), "leg.R": (20 * k, 0, 0, 0, 0, 0),
                "arm.L": (-25 * k, 0, 10, 0, 0, 0), "arm.R": (-25 * k, 0, -10, 0, 0, 0)}
    k = min(1, (t - 0.25) / 0.35)
    return {"hips": (0, 0, 0, 0, -0.04 * (1 - k), 0), "spine": (10 - 15 * k, 0, 0, 0, 0, 0), "head": (-5 * k, 0, 0, 0, 0, 0),
            "leg.L": (20 + 20 * k, 0, 0, 0, 0, 0), "leg.R": (20 - 5 * k, 0, 0, 0, 0, 0),
            "arm.L": (-25 * (1 - k), 0, 10 + 125 * k, 0, 0, 0), "arm.R": (-25 * (1 - k), 0, -10 - 125 * k, 0, 0, 0)}
def wave(t): return {"hips": (0, 0, 0, 0, 0.006 * SN(t, 2), 0), "head": (0, 0, -8, 0, 0, 0), "arm.R": (15, 0, -150 + 18 * SN(t, 2), 0, 0, 0), "arm.L": (0, 0, 6, 0, 0, 0)}
def cheer(t):
    b = abs(SN(t))
    return {"hips": (0, 0, 0, 0, 0.06 * b, 0), "spine": (-4 * b, 0, 0, 0, 0, 0), "head": (-8 * b, 0, 0, 0, 0, 0),
            "arm.L": (0, 0, 150 + 12 * SN(t, 2), 0, 0, 0), "arm.R": (0, 0, -150 - 12 * SN(t, 2), 0, 0, 0), "leg.L": (8 * b, 0, 0, 0, 0, 0), "leg.R": (-8 * b, 0, 0, 0, 0, 0)}

bpy.context.view_layer.objects.active = arm
bpy.ops.object.mode_set(mode='POSE')
for a in [("Idle", 60, idle, 8, True), ("Walk", 24, walk, 8, True), ("Run", 16, run, 8, True), ("Jump", 18, jump, 7, False), ("Wave", 30, wave, 8, True), ("Cheer", 24, cheer, 8, True)]:
    make_action(a[0], a[1], a[2], a[3], a[4])
pose_frame({})
bpy.ops.object.mode_set(mode='OBJECT')
arm.animation_data.action = bpy.data.actions["Idle"]
scene.frame_start, scene.frame_end = 1, 60
scene.frame_set(1)
for b in arm.pose.bones: b.rotation_euler = (0, 0, 0); b.location = (0, 0, 0)
arm.animation_data.action = None          # rest pose for the renders

# ----------------------------------------------------------------------------------- export to Unity
def all_hair_objects(style):
    return [o for o in bpy.data.objects if o.name.startswith("Hair_" + style)]

if DO_EXPORT:
    os.makedirs(os.path.dirname(FBX_PATH), exist_ok=True)
    bpy.ops.object.select_all(action='DESELECT')
    for o in bpy.data.objects:
        o.hide_set(False)
        if o.type in ('ARMATURE', 'MESH', 'EMPTY') and not o.name.startswith("Face_"): o.select_set(True)
    arm.animation_data.action = bpy.data.actions["Idle"]
    bpy.ops.export_scene.fbx(
        filepath=FBX_PATH, use_selection=True, object_types={'ARMATURE', 'MESH', 'EMPTY'}, use_mesh_modifiers=True,
        apply_scale_options='FBX_SCALE_ALL', axis_forward='-Z', axis_up='Y', add_leaf_bones=False,
        primary_bone_axis='Y', secondary_bone_axis='X', mesh_smooth_type='FACE', use_armature_deform_only=False,
        bake_anim=True, bake_anim_use_all_actions=True, bake_anim_use_nla_strips=False,
        bake_anim_force_startend_keying=True, bake_anim_simplify_factor=0.0)
    arm.animation_data.action = None
    for b in arm.pose.bones: b.rotation_euler = (0, 0, 0); b.location = (0, 0, 0)

# ----------------------------------------------------------------------------------- show only the hero look
for s, g in hair_groups.items():
    for o in all_hair_objects(s):
        o.hide_render = s != HERO_HAIR; o.hide_set(s != HERO_HAIR)
for h in hats: h.hide_render = True; h.hide_set(True)
set_expression(EXPRESSION)

# ----------------------------------------------------------------------------------- studio: stage, soft lights, camera
bpy.ops.mesh.primitive_plane_add(size=8, location=(0, 0, 0)); stage = bpy.context.object; stage.name = "Stage"
stage.data.materials.append(M["Stage"])
world = scene.world or bpy.data.worlds.new("World"); scene.world = world; world.use_nodes = True
bg = world.node_tree.nodes.get("Background")
bg.inputs[0].default_value = (*hexc("#A8C8EE"), 1); bg.inputs[1].default_value = 0.55

def area_light(name, loc, size, power, color=(1, 1, 1)):
    ld = bpy.data.lights.new(name, 'AREA'); ld.size = size; ld.energy = power; ld.color = color
    ob = bpy.data.objects.new(name, ld); col.objects.link(ob); ob.location = loc
    ob.rotation_euler = (Vector((0, 0, 0.7)) - Vector(loc)).to_track_quat('-Z', 'Y').to_euler()
    return ob
area_light("Key", (-1.6, -2.2, 2.4), 3.0, 60, (1.0, 0.96, 0.92))
area_light("Fill", (2.2, -1.6, 1.2), 3.0, 22, (0.9, 0.94, 1.0))
area_light("Rim", (0.6, 2.4, 2.0), 2.0, 45, (1.0, 0.92, 0.95))

cam_data = bpy.data.cameras.new("Cam"); cam_data.lens = 70
cam = bpy.data.objects.new("Camera", cam_data); col.objects.link(cam); scene.camera = cam
TARGET = Vector((0, 0, 0.62))
def aim(az, el=12, dist=3.2):
    d = hdir(az, el)
    cam.location = TARGET + d * dist
    cam.rotation_euler = (TARGET - cam.location).to_track_quat('-Z', 'Y').to_euler()

# ----------------------------------------------------------------------------------- render
for eng in ("BLENDER_EEVEE_NEXT", "BLENDER_EEVEE"):
    try: scene.render.engine = eng; break
    except TypeError: pass
try: scene.view_settings.view_transform = 'Standard'
except TypeError: pass
scene.render.resolution_x, scene.render.resolution_y = 900, 1100
if hasattr(scene, "eevee"):
    try: scene.eevee.taa_render_samples = 48
    except AttributeError: pass

VIEWS = {"3Q": -35, "FRONT": 0, "SIDE": -90, "BACK": 180}
if DO_RENDER:
    os.makedirs(RENDER_DIR, exist_ok=True)
    for name, az in VIEWS.items():
        if VIEW != "ALL" and VIEW != name: continue
        aim(az)
        scene.render.filepath = os.path.join(RENDER_DIR, f"chibi_{name.lower()}.png")
        bpy.ops.render.render(write_still=True)
aim(VIEWS["3Q"] if VIEW == "ALL" else VIEWS.get(VIEW, -35))

os.makedirs(os.path.dirname(BLEND_PATH), exist_ok=True)
bpy.ops.wm.save_as_mainfile(filepath=BLEND_PATH)
print("FARM CHAOS FIGURINE DONE")
