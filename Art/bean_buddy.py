# =====================================================================================================
#  FARM CHAOS - "Bean Buddy": the player character (a silly bean with a giant head and huge round eyes)
#  Blender 4.x Python script - no add-ons, no downloads, no image textures. Everything is procedural.
#
#  HOW TO RUN (beginner version)
#   1. Open Blender, File > New > General.
#   2. Click the "Scripting" tab at the top.
#   3. In the Text Editor (middle), click "Open", pick this file (or New + paste it in).
#   4. Press the Run button (the ▶ triangle), or Alt+P with the mouse over the text.
#   5. You get: Art/BeanBuddy.blend, renders in Art/renders, and the game model
#      Assets/Models/Chibi/Chibi.fbx (then in Unity: Tools > Farm Chaos > 22. Build Chibi Player).
#   Running it again replaces the old Bean Buddy - it never makes duplicates.
#
#  HOW IT'S BUILT
#   * Head/body/clothes/hats are "lathe" shapes: a 2D side outline spun around an axis -> clean rings of
#     polygons and smooth toy-like silhouettes.
#   * EYES/BROWS/MOUTH/BLUSH are "decals" laid onto the head surface: each eye is perfectly round layers
#     (outline < white < pupil < shade < one highlight) sharing a soft glossy bulge, edges tucked into the head.
#   * EMOTIONS live in the eyebrows + mouth: 9 brow/mouth pairs (Brow_Happy + Mouth_Happy, ...). The game
#     shows one pair at a time; the big round eyes always stay the same.
#   * RIG: each body part is a rigid toy piece riding on one bone (hips, spine, head, arms, hands, legs) -
#     perfect for a wobbly, floppy, ragdoll-ish walk. HoldPoint_L / HoldPoint_R sit in the hands for
#     holding items later.
#   * ANIMATIONS: Idle / Walk / Run / Jump / Wave / Cheer with floppy follow-through (head and arms lag
#     behind the body), plus a Carry pose for holding things with both arms.
#   * STORY INTRO acting: Sleep / SleepSprawl (lying on the floor), WakeUp (pops up), HotFan (fanning,
#     sweaty), Panic (arms flailing), Dive (out of the window) and Pose (hero pose for the player cards).
#   * HATS: Bucket (new) + the five game hats (Straw, Cap, Chicken, FlowerCrown, Beret) refitted to the bean.
# =====================================================================================================
import bpy, bmesh, math, os
from mathutils import Matrix, Vector

EXPRESSION = "Smile"   # Smile, Happy, Surprised, Angry, Sad, Sleepy, Wink, Determined, Love
HERO_HAIR = "Bob"      # Bob, Long, Pigtails, Spiky, or "None"
HERO_HAT = "Bucket"    # Bucket, Straw, Cap, Chicken, FlowerCrown, Beret, or "None"
VIEW = "ALL"           # "3Q", "FRONT" (symmetry check), "SIDE", "BACK" or "ALL"
DO_RENDER = True
DO_FACE_SHEET = False  # also render a close-up of every expression (Art/renders/bean_face_*.png)
DO_EXPORT = True       # write the Unity model
DO_ACT_SHEET = True    # render a few frames of each story-intro acting animation (Art/renders/bean_act_*.png)
CLEAR_WHOLE_SCENE = globals().get("CLEAR_WHOLE_SCENE", False)   # True = delete EVERYTHING first

PROJECT = r"C:\Users\shere\Farm-Chaos-2026-10-22_16-48-36"
RENDER_DIR = os.path.join(PROJECT, "Art", "renders")
BLEND_PATH = os.path.join(PROJECT, "Art", "BeanBuddy.blend")
FBX_PATH = os.path.join(PROJECT, "Assets", "Models", "Chibi", "Chibi.fbx")

FACES = ["Smile", "Happy", "Surprised", "Angry", "Sad", "Sleepy", "Wink", "Determined", "Love"]
HAT_NAMES = ["Bucket", "Straw", "Cap", "Chicken", "FlowerCrown", "Beret"]
HAIR_NAMES = ["Bob", "Long", "Pigtails", "Spiky"]
# game moves + the story-intro "acting" moves (Sleep ... Pose)
ACTION_NAMES = ["Idle", "Walk", "Run", "Jump", "Wave", "Cheer", "Carry", "Fall",
                "Sleep", "SleepSprawl", "WakeUp", "HotFan", "Panic", "Dive", "Pose"]

# ----------------------------------------------------------------------------------- 0. clean up
if bpy.context.object and bpy.context.object.mode != 'OBJECT':
    bpy.ops.object.mode_set(mode='OBJECT')

def remove_collection(name):
    c = bpy.data.collections.get(name)
    if c is None: return
    for child in list(c.children): remove_collection(child.name)
    for o in list(c.objects): bpy.data.objects.remove(o, do_unlink=True)
    bpy.data.collections.remove(c)

if CLEAR_WHOLE_SCENE:
    for o in list(bpy.data.objects): bpy.data.objects.remove(o, do_unlink=True)
else:
    remove_collection("Bean_Buddy"); remove_collection("Bean_Studio")
    for n in ("Cube", "Light", "Camera"):                      # Blender's default start-up objects
        o = bpy.data.objects.get(n)
        if o: bpy.data.objects.remove(o, do_unlink=True)
for coll in (bpy.data.meshes, bpy.data.materials, bpy.data.lights, bpy.data.cameras, bpy.data.armatures):
    for d in list(coll):
        if d.users == 0 or d.name.startswith("Bean_"): coll.remove(d)
for a in list(bpy.data.actions):
    if a.name in ACTION_NAMES or a.name.startswith("Bean_"): bpy.data.actions.remove(a)

scene = bpy.context.scene
scene.render.fps = 30
def new_collection(name, parent):
    c = bpy.data.collections.new(name); parent.children.link(c); return c
C_ROOT = new_collection("Bean_Buddy", scene.collection)
C_BODY = new_collection("Bean_Body", C_ROOT)
C_FACE = new_collection("Bean_Face", C_ROOT)
C_CLOTH = new_collection("Bean_Clothing", C_ROOT)
C_HAT = new_collection("Bean_Hats", C_ROOT)
C_HAIR = new_collection("Bean_Hair", C_ROOT)
C_STUDIO = new_collection("Bean_Studio", scene.collection)

# ----------------------------------------------------------------------------------- 1. materials
# Names matter: the game recolours "Skin", "Outfit", "HatMain" and "HatAccent"; "Fixed..." never change.
def hexc(h):
    h = h.lstrip('#'); c = [int(h[i:i + 2], 16) / 255 for i in (0, 2, 4)]
    return tuple(((x + 0.055) / 1.055) ** 2.4 if x > 0.04045 else x / 12.92 for x in c)   # sRGB -> linear

def set_input(bsdf, names, value):
    for n in names:
        if n in bsdf.inputs: bsdf.inputs[n].default_value = value; return

def mat(name, hexcol, rough=0.5, sss=0.0, coat=0.0, sheen=0.0, emit=0.0, alpha=1.0):
    old = bpy.data.materials.get(name)
    if old is not None: bpy.data.materials.remove(old)
    m = bpy.data.materials.new(name)
    m.use_nodes = True
    rgb = hexc(hexcol); m.diffuse_color = (*rgb, 1)
    b = m.node_tree.nodes.get("Principled BSDF")
    b.inputs["Base Color"].default_value = (*rgb, 1)
    b.inputs["Roughness"].default_value = rough
    set_input(b, ["Subsurface Weight", "Subsurface"], sss)
    if sss: set_input(b, ["Subsurface Radius"], (0.6, 0.35, 0.25))
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
    "Skin": mat("Skin", "#FFEEE2", 0.55, sss=0.12),
    "Outfit": mat("Outfit", "#C8B6F2", 0.85, sheen=0.5),            # the sweatshirt (lilac)
    "HatMain": mat("HatMain", "#FFD27E", 0.75, sheen=0.3),          # butter yellow
    "HatAccent": mat("HatAccent", "#F59AB0", 0.6),                  # pink band
    "Hair": mat("Hair", "#7B4F47", 0.45, coat=0.1),                 # recoloured in the game
    "Accent": mat("Accent", "#F6A9BD", 0.5),                        # hair clips / ties
    "FixedEyeRim": mat("FixedEyeRim", "#2A2230", 0.4),
    "FixedEyeWhite": mat("FixedEyeWhite", "#FFFFFF", 0.35, coat=0.25, emit=0.25),
    "FixedPupil": mat("FixedPupil", "#120F16", 0.15, coat=0.7),
    "FixedPupilShade": mat("FixedPupilShade", "#57515F", 0.2, coat=0.5),
    "FixedHighlight": mat("FixedHighlight", "#FFFFFF", 0.2, emit=1.5),
    "FixedBrow": mat("FixedBrow", "#3A2A33", 0.5),
    "FixedMouth": mat("FixedMouth", "#4E2C38", 0.45),
    "FixedTongue": mat("FixedTongue", "#F28A99", 0.45),
    "FixedBlush": mat("FixedBlush", "#F7A1B6", 0.8, alpha=0.8),
    "FixedWhite": mat("FixedWhite", "#FFFFFF"), "FixedRed": mat("FixedRed", "#E0505C"),
    "FixedOrange": mat("FixedOrange", "#F5A623"), "FixedInk": mat("FixedInk", "#2E2A3A"),
    "FixedLeaf": mat("FixedLeaf", "#7FC98A"), "FixedYellow": mat("FixedYellow", "#FFE07A"),
    "Ground": mat("Bean_Ground", "#BCD6F2", 0.95),
}

# ----------------------------------------------------------------------------------- 2. helpers
HC = Vector((0, 0, 0.655))            # head centre
HR = Vector((0.335, 0.30, 0.295))     # head radii: wide, a bit squashed = cute (head is ~60% of the height)

def head_normal(p):
    q = p - HC
    return Vector((q.x / HR.x ** 2, q.y / HR.y ** 2, q.z / HR.z ** 2)).normalized()

def hit_head(origin, direction):
    """Where a ray first meets the head (an ellipsoid)."""
    o = Vector(((origin.x - HC.x) / HR.x, (origin.y - HC.y) / HR.y, (origin.z - HC.z) / HR.z))
    d = Vector((direction.x / HR.x, direction.y / HR.y, direction.z / HR.z))
    a = d.dot(d); b = 2 * o.dot(d); c = o.dot(o) - 1
    disc = max(b * b - 4 * a * c, 0.0)
    t = (-b - math.sqrt(disc)) / (2 * a)
    return origin + direction * t

def face_frame(x, z, forward_bias=0.55):
    """A spot on the face seen from the front: its surface point, facing direction f, and right/up axes."""
    p = hit_head(Vector((x, -2.0, z)), Vector((0, 1, 0)))
    f = (head_normal(p) * (1 - forward_bias) + Vector((0, -1, 0)) * forward_bias).normalized()
    right = Vector((0, 0, 1)).cross(f).normalized()      # +X when f faces the camera (-Y)
    up = f.cross(right).normalized()
    return p, f, right, up

def lathe(bm, prof, seg, sy=1.0, closed=False, mi_of=None):
    """Spin a side outline [(radius, height)] around the local Z axis. sy squashes the depth (Y)."""
    rings = []
    for r, z in prof:
        if r < 1e-6: rings.append([bm.verts.new((0, 0, z))])
        else: rings.append([bm.verts.new((r * math.cos(2 * math.pi * k / seg), r * math.sin(2 * math.pi * k / seg) * sy, z)) for k in range(seg)])
    pairs = list(zip(range(len(rings)), range(1, len(rings))))
    if closed: pairs.append((len(rings) - 1, 0))
    for i, j in pairs:
        A, B = rings[i], rings[j]
        mi = mi_of(i, prof[i], prof[j]) if mi_of else 0
        fs = []
        if len(A) == 1 and len(B) == 1: continue
        if len(A) == 1:
            for k in range(seg): fs.append(bm.faces.new((A[0], B[(k + 1) % seg], B[k])))
        elif len(B) == 1:
            for k in range(seg): fs.append(bm.faces.new((A[k], A[(k + 1) % seg], B[0])))
        else:
            for k in range(seg): fs.append(bm.faces.new((A[k], A[(k + 1) % seg], B[(k + 1) % seg], B[k])))
        for fc in fs: fc.material_index = mi
    return [v for r in rings for v in r]

def transform_verts(vs, mtx):
    for v in vs: v.co = mtx @ v.co

def ellipsoid(bm, centre, radii, seg=24, rings=16):
    vs = bmesh.ops.create_uvsphere(bm, u_segments=seg, v_segments=rings, radius=1.0)['verts']
    for v in vs: v.co = Vector((v.co.x * radii[0], v.co.y * radii[1], v.co.z * radii[2])) + Vector(centre)
    return vs

def capsule(bm, a, b, r, seg=16, rings=12):
    a, b = Vector(a), Vector(b); L = (b - a).length
    vs = bmesh.ops.create_uvsphere(bm, u_segments=seg, v_segments=rings, radius=r)['verts']
    for v in vs: v.co.z += L / 2 if v.co.z >= 0 else -L / 2
    transform_verts(vs, Matrix.Translation((a + b) / 2) @ (b - a).normalized().to_track_quat('Z', 'Y').to_matrix().to_4x4())
    return vs

def axis_matrix(origin, direction):
    return Matrix.Translation(Vector(origin)) @ Vector(direction).normalized().to_track_quat('Z', 'Y').to_matrix().to_4x4()

def new_obj(name, bm, mats, coll, origin=(0, 0, 0), bone=None, recalc=True, smooth=True):
    """bmesh -> object. The mesh is shifted so the object's pivot sits at 'origin' (its joint).
    bone: the rig bone this rigid part rides on (attached once the rig exists)."""
    origin = Vector(origin)
    for v in bm.verts: v.co -= origin
    if recalc: bmesh.ops.recalc_face_normals(bm, faces=bm.faces[:])
    me = bpy.data.meshes.new("Bean_" + name)
    bm.normal_update(); bm.to_mesh(me); bm.free()
    for m in mats: me.materials.append(M[m])
    for p in me.polygons: p.use_smooth = smooth
    ob = bpy.data.objects.new(name, me); coll.objects.link(ob)
    ob.location = origin
    if bone: RIDES[ob.name] = bone
    return ob

RIDES = {}   # object name -> bone name

# ---- decals: round (or any convex) shapes laid onto the head --------------------------------------
def decal(bm, frame, outline, lift, mi=0, bulge_centre=None, bulge_r=0.0, bulge_h=0.0, rings=6, tuck=0.004):
    """
    outline: list of (u, v) points (in metres, around the frame's spot) - a convex shape.
    lift: how far above the head surface this layer sits (layers stack by lift).
    bulge_*: a shared dome (used by the eye layers so they look glossy).
    tuck: the edge folds this far back toward the head, so the decal never looks paper-thin.
    """
    p0, f, right, up = frame
    cu = sum(u for u, v in outline) / len(outline); cv = sum(v for u, v in outline) / len(outline)
    def place(u, v, extra):
        q = p0 + right * u + up * v + f * 0.5
        h = hit_head(q, -f)
        b = 0.0
        if bulge_centre is not None and bulge_r > 0:
            d = math.hypot(u - bulge_centre[0], v - bulge_centre[1]) / bulge_r
            b = bulge_h * max(0.0, 1 - d * d)
        return h + head_normal(h) * (lift + b + extra)
    centre = bm.verts.new(place(cu, cv, 0))
    ring_vs = []
    for i in range(1, rings + 1):
        s = i / rings
        ring_vs.append([bm.verts.new(place(cu + (u - cu) * s, cv + (v - cv) * s, 0)) for u, v in outline])
    ring_vs.append([bm.verts.new(place(u, v, -tuck)) for u, v in outline])      # tucked edge
    n = len(outline); fs = []
    for k in range(n): fs.append(bm.faces.new((centre, ring_vs[0][k], ring_vs[0][(k + 1) % n])))
    for A, B in zip(ring_vs, ring_vs[1:]):
        for k in range(n): fs.append(bm.faces.new((A[k], B[k], B[(k + 1) % n], A[(k + 1) % n])))
    # make the faces point out of the head
    fs[0].normal_update()
    if fs[0].normal.dot(f) < 0: bmesh.ops.reverse_faces(bm, faces=fs)
    for fc in fs: fc.material_index = mi
    return fs

def circle(cu, cv, r, n=48, sx=1.0, sy=1.0):
    return [(cu + r * sx * math.cos(2 * math.pi * k / n), cv + r * sy * math.sin(2 * math.pi * k / n)) for k in range(n)]

def stroke(bm, frame, pts_uv, radius, lift, mi=0, samples=24, ring_n=8):
    """A soft round line drawn on the face (mouths)."""
    p0, f, right, up = frame
    def place(u, v):
        h = hit_head(p0 + right * u + up * v + f * 0.5, -f)
        return h + head_normal(h) * lift
    P = [Vector((u, v, 0)) for u, v in pts_uv]
    P = [P[0] + (P[0] - P[1])] + P + [P[-1] + (P[-1] - P[-2])]
    segs = len(P) - 3; path = []
    for i in range(samples):
        t = i / (samples - 1) * segs; k = min(int(t), segs - 1); s = t - k
        p0_, p1, p2, p3 = P[k], P[k + 1], P[k + 2], P[k + 3]
        c = 0.5 * ((2 * p1) + (-p0_ + p2) * s + (2 * p0_ - 5 * p1 + 4 * p2 - p3) * s * s + (-p0_ + 3 * p1 - 3 * p2 + p3) * s ** 3)
        path.append(place(c.x, c.y))
    rings = []
    for i, p in enumerate(path):
        T = (path[min(i + 1, samples - 1)] - path[max(i - 1, 0)]).normalized()
        N = head_normal(p); N = (N - T * N.dot(T)).normalized(); B = T.cross(N)
        rings.append([bm.verts.new(p + (B * math.cos(2 * math.pi * j / ring_n) + N * math.sin(2 * math.pi * j / ring_n)) * radius) for j in range(ring_n)])
    fs = []
    for A, B2 in zip(rings, rings[1:]):
        for j in range(ring_n): fs.append(bm.faces.new((A[j], A[(j + 1) % ring_n], B2[(j + 1) % ring_n], B2[j])))
    fs.append(bm.faces.new(list(reversed(rings[0])))); fs.append(bm.faces.new(rings[-1]))
    for fc in fs: fc.material_index = mi
    return fs

def sph(bm, centre, radii, mi=0, seg=20, rings=12):
    vs = ellipsoid(bm, centre, radii, seg, rings)
    for f in set(f for v in vs for f in v.link_faces): f.material_index = mi
    return vs

def cyl(bm, base, r1, r2, depth, mi=0, direction=(0, 0, 1), seg=24):
    d = Vector(direction).normalized()
    mtx = Matrix.Translation(Vector(base) + d * depth / 2) @ d.to_track_quat('Z', 'Y').to_matrix().to_4x4()
    vs = bmesh.ops.create_cone(bm, cap_ends=True, cap_tris=False, segments=seg, radius1=r1, radius2=r2, depth=depth, matrix=mtx)['verts']
    for f in set(f for v in vs for f in v.link_faces): f.material_index = mi
    return vs

# ----------------------------------------------------------------------------------- 3. body (rigid toy parts)
BODY_SY = 0.86                                        # a bit flatter front-to-back = bean
bm = bmesh.new()
lathe(bm, [(0, 0.07), (0.06, 0.073), (0.105, 0.088), (0.138, 0.115), (0.156, 0.155), (0.16, 0.205), (0.152, 0.26),
           (0.133, 0.31), (0.108, 0.35), (0.086, 0.385), (0.07, 0.41), (0, 0.43)], 28, BODY_SY)
new_obj("Body", bm, ["Skin"], C_BODY, origin=(0, 0, 0.2), bone="spine")

bm = bmesh.new(); ellipsoid(bm, HC, HR, 36, 26)
new_obj("Head", bm, ["Skin"], C_BODY, origin=(0, 0, 0.38), bone="head")

ARM_DIR = {s: Vector((s * 0.74, 0.0, -0.67)).normalized() for s in (-1, 1)}
SHOULDER = {s: Vector((s * 0.15, 0.0, 0.325)) for s in (-1, 1)}
WRIST = {s: SHOULDER[s] + ARM_DIR[s] * 0.11 for s in (-1, 1)}
SIDES = ((1, "L"), (-1, "R"))
for s, side in SIDES:
    a, d = SHOULDER[s], ARM_DIR[s]
    bm = bmesh.new(); capsule(bm, a, WRIST[s], 0.034, 14, 10)
    new_obj("Arm_" + side, bm, ["Skin"], C_BODY, origin=a, bone="arm." + side)
    bm = bmesh.new(); vs = ellipsoid(bm, (0, 0, 0), (0.041, 0.037, 0.044), 18, 12)
    transform_verts(vs, axis_matrix(WRIST[s] + d * 0.02, d))
    new_obj("Hand_" + side, bm, ["Skin"], C_BODY, origin=WRIST[s], bone="hand." + side)

HIP = {s: Vector((s * 0.066, 0, 0.12)) for s in (-1, 1)}
ANKLE = {s: Vector((s * 0.071, -0.004, 0.05)) for s in (-1, 1)}
for s, side in SIDES:
    bm = bmesh.new(); capsule(bm, HIP[s], ANKLE[s], 0.046, 16, 10)
    new_obj("Leg_" + side, bm, ["Skin"], C_BODY, origin=HIP[s], bone="leg." + side)
    bm = bmesh.new(); ellipsoid(bm, (s * 0.073, -0.016, 0.034), (0.056, 0.072, 0.036), 20, 12)
    new_obj("Foot_" + side, bm, ["Skin"], C_BODY, origin=ANKLE[s], bone="leg." + side)

# ----------------------------------------------------------------------------------- 4. sweatshirt (a real shell)
bm = bmesh.new()
outer = [(0.095, 0.398), (0.13, 0.385), (0.165, 0.36), (0.19, 0.325), (0.205, 0.28), (0.212, 0.23), (0.212, 0.19),
         (0.207, 0.168), (0.196, 0.152), (0.18, 0.14)]
inner = [(0.165, 0.142), (0.175, 0.17), (0.195, 0.2), (0.197, 0.24), (0.19, 0.285), (0.174, 0.325), (0.15, 0.357), (0.118, 0.378), (0.083, 0.39)]
lathe(bm, outer + inner, 32, BODY_SY, closed=True)
new_obj("Sweatshirt", bm, ["Outfit"], C_CLOTH, origin=(0, 0, 0.2), bone="spine")
for s, side in SIDES:
    bm = bmesh.new()
    vs = lathe(bm, [(0.06, -0.02), (0.064, 0.02), (0.062, 0.05), (0.058, 0.068), (0.053, 0.078), (0.044, 0.08),
                    (0.04, 0.07), (0.044, 0.04), (0.046, -0.02)], 20, 1.0, closed=True)
    transform_verts(vs, axis_matrix(SHOULDER[s], ARM_DIR[s]))
    new_obj("Sleeve_" + side, bm, ["Outfit"], C_CLOTH, origin=SHOULDER[s], bone="arm." + side)

# ----------------------------------------------------------------------------------- 5. face
EYE_X, EYE_Z, EYE_R = 0.13, 0.605, 0.11       # eyes a touch lower than before -> room for eyebrows
EYE_LAYERS = [  # name, material, outline, lift above the head
    ("Rim", "FixedEyeRim", lambda: circle(0, 0, EYE_R + 0.012), 0.0010),
    ("White", "FixedEyeWhite", lambda: circle(0, 0, EYE_R), 0.0016),
    ("Pupil", "FixedPupil", lambda: circle(0, -0.006, 0.084), 0.0022),
    ("PupilShade", "FixedPupilShade", lambda: circle(0, -0.036, 0.053), 0.0027),
    ("Highlight", "FixedHighlight", lambda: circle(-0.03, 0.03, 0.03), 0.0032),      # ONE highlight
]
for s, side in SIDES:
    fr = face_frame(s * EYE_X, EYE_Z)
    for name, m, shape, lift in EYE_LAYERS:
        bm = bmesh.new()
        decal(bm, fr, shape(), lift, bulge_centre=(0, 0), bulge_r=EYE_R + 0.012, bulge_h=0.007,
              tuck=0.0012 if name != "Rim" else 0.005)
        new_obj(f"Eye_{side}_{name}", bm, [m], C_FACE, origin=fr[0], bone="head", recalc=False)

for s, side in SIDES:
    fr = face_frame(s * 0.205, 0.488)
    bm = bmesh.new(); decal(bm, fr, circle(0, 0, 0.044, 32, 1.0, 0.5), 0.0008, tuck=0.002)
    new_obj("Blush_" + side, bm, ["FixedBlush"], C_FACE, origin=fr[0], bone="head", recalc=False)

# ---- EMOTIONS: eyebrows (inner, middle, outer height for the left & right brow) + a mouth
BROW_X, BROW_HALF = 0.135, 0.042
BROWS = {
    "Smile":      ((0.752, 0.758, 0.752), (0.752, 0.758, 0.752)),
    "Happy":      ((0.760, 0.772, 0.762), (0.760, 0.772, 0.762)),
    "Surprised":  ((0.772, 0.786, 0.772), (0.772, 0.786, 0.772)),
    "Angry":      ((0.736, 0.752, 0.774), (0.736, 0.752, 0.774)),
    "Sad":        ((0.776, 0.762, 0.743), (0.776, 0.762, 0.743)),
    "Sleepy":     ((0.744, 0.746, 0.741), (0.744, 0.746, 0.741)),
    "Wink":       ((0.758, 0.770, 0.762), (0.742, 0.745, 0.742)),   # cheeky: one brow up, one down
    "Determined": ((0.740, 0.751, 0.764), (0.740, 0.751, 0.764)),
    "Love":       ((0.770, 0.778, 0.768), (0.770, 0.778, 0.768)),
}
for face, sides in BROWS.items():
    bm = bmesh.new()
    for (s, side), (zi, zm, zo) in zip(SIDES, sides):
        fr = face_frame(s * BROW_X, zm)
        ui = -s * BROW_HALF                                        # inner end points toward the nose
        thick = 0.0095 if face in ("Angry", "Determined") else 0.0082
        stroke(bm, fr, [(ui, zi - zm), (ui * 0.45, (zi + zm) / 2 - zm + 0.002), (0, 0), (-ui * 0.5, (zm + zo) / 2 - zm + 0.002), (-ui, zo - zm)],
               thick, 0.0025, samples=20)
    new_obj("Brow_" + face, bm, ["FixedBrow"], C_FACE, origin=(0, -0.25, 0.76), bone="head", recalc=False)

MOUTH_Z = 0.478
mfr = face_frame(0, MOUTH_Z)
LINE = 0.0058
def m_smile(bm): stroke(bm, mfr, [(-0.034, 0.004), (-0.017, -0.011), (0.0, 0.002), (0.017, -0.011), (0.034, 0.004)], LINE, 0.003)   # :3
def m_happy(bm):
    d = [(-0.034 + 0.068 * k / 12, 0.008) for k in range(13)] + [(0.034 * math.cos(a), 0.008 - 0.036 * math.sin(a)) for a in [math.pi * k / 16 for k in range(1, 16)]]
    decal(bm, mfr, d, 0.0025, mi=0, tuck=0.003)
    decal(bm, mfr, circle(0, -0.016, 0.017, 24, 1.1, 0.6), 0.0035, mi=1, tuck=0.002)
def m_surprised(bm): decal(bm, mfr, circle(0, -0.006, 0.019, 28, 0.85, 1.15), 0.0025, tuck=0.003)
def m_angry(bm): stroke(bm, mfr, [(-0.034, -0.012), (-0.017, 0.002), (0.017, 0.002), (0.034, -0.012)], LINE * 1.15, 0.003)
def m_sad(bm):
    stroke(bm, mfr, [(-0.022, -0.012), (-0.01, -0.002), (0.01, -0.002), (0.022, -0.012)], LINE, 0.003)
    tf = face_frame(0.172, 0.452)                                   # a little tear just under the eye
    tear = [(0.0, 0.032)] + [(0.014 * math.cos(a), -0.002 + 0.015 * math.sin(a))
                             for a in [math.pi * (0.15 - 1.3 * k / 16) for k in range(17)]]
    decal(bm, tf, tear, 0.0024, mi=2, tuck=0.002)
def m_sleepy(bm): stroke(bm, mfr, [(-0.018, 0.0), (-0.006, 0.005), (0.006, -0.005), (0.018, 0.0)], LINE, 0.003)
def m_wink(bm):
    stroke(bm, mfr, [(-0.028, 0.006), (-0.012, -0.006), (0.012, -0.006), (0.028, 0.006)], LINE, 0.003)
    decal(bm, mfr, circle(0.011, -0.014, 0.011, 20, 1.0, 1.1), 0.0034, mi=1, tuck=0.002)   # :P tongue
def m_determined(bm): stroke(bm, mfr, [(-0.028, 0.0), (0.0, -0.003), (0.028, 0.001)], LINE * 1.15, 0.003)
def m_love(bm): stroke(bm, mfr, [(-0.006, 0.016), (0.008, 0.011), (0.0, 0.003), (0.009, -0.004), (-0.005, -0.012)], LINE, 0.003)   # kissy 3
MOUTHS = {"Smile": m_smile, "Happy": m_happy, "Surprised": m_surprised, "Angry": m_angry, "Sad": m_sad,
          "Sleepy": m_sleepy, "Wink": m_wink, "Determined": m_determined, "Love": m_love}
FixedTear = M["FixedTear"] = mat("FixedTear", "#9FD3F7", 0.15, coat=0.8)
for face, fn in MOUTHS.items():
    bm = bmesh.new(); fn(bm)
    new_obj("Mouth_" + face, bm, ["FixedMouth", "FixedTongue", "FixedTear"], C_FACE, origin=mfr[0], bone="head", recalc=False)

# ----------------------------------------------------------------------------------- 5b. HAIR (4 styles, no blobs)
# Same system as the old chibi: a smooth scalp cap + deliberately shaped locks. Every lock is ONE swept mesh
# (spline spine laid over the skull, flat lens cross-section, fuller near the root, tapering to a point).
# For the bean the fringe is short so it stops ABOVE the eyebrows.
D = math.radians
HAIR_CAP, LOCK_THICK = 1.045, 0.045

def hdir(az, el):
    """az: degrees round from the front toward the character's left (+X); el: degrees up."""
    a, e = D(az), D(el)
    return Vector((math.sin(a) * math.cos(e), -math.cos(a) * math.cos(e), math.sin(e)))

def S(az, el, grow=1.0):
    d = hdir(az, el)
    return HC + Vector((d.x * HR.x, d.y * HR.y, d.z * HR.z)) * grow

def faces_of(vs, mi):
    for f in set(f for v in vs if v.is_valid for f in v.link_faces): f.material_index = mi

def catmull(pts, n):
    P = [Vector(p) for p in pts]
    P = [P[0] + (P[0] - P[1])] + P + [P[-1] + (P[-1] - P[-2])]
    segs = len(P) - 3; out = []
    for i in range(n):
        t = i / (n - 1) * segs; k = min(int(t), segs - 1); u = t - k
        p0, p1, p2, p3 = P[k], P[k + 1], P[k + 2], P[k + 3]
        out.append(0.5 * ((2 * p1) + (-p0 + p2) * u + (2 * p0 - 5 * p1 + 4 * p2 - p3) * u * u + (-p0 + 3 * p1 - 3 * p2 + p3) * u ** 3))
    return out

def sweep(bm, pts, width, thick, mi=0, samples=26, ring_n=10, tip=True, up_hint=None):
    """THE LOCK BUILDER: a flat lens cross-section swept along a smooth spine, flat side facing away from the head."""
    path = catmull(pts, samples); rings, verts = [], []
    for i, p in enumerate(path):
        t = i / (samples - 1)
        T = (path[min(i + 1, samples - 1)] - path[max(i - 1, 0)]).normalized()
        out = p - (up_hint if up_hint is not None else HC)
        N = (out - T * out.dot(T)).normalized(); B = T.cross(N).normalized(); N = B.cross(T).normalized()
        if tip and i == samples - 1:
            v = bm.verts.new(p); rings.append([v]); verts.append(v); continue
        w, th = max(width(t), 0.002), max(thick(t), 0.002); rv = []
        for j in range(ring_n):
            a = 2 * math.pi * j / ring_n; c, s_ = math.cos(a), math.sin(a)
            v = bm.verts.new(p + B * (c * w / 2) + N * (math.copysign(abs(s_) ** 0.8, s_) * th / 2)); rv.append(v); verts.append(v)
        rings.append(rv)
    for A, Bn in zip(rings, rings[1:]):
        if len(Bn) == 1:
            for j in range(ring_n): bm.faces.new((A[j], A[(j + 1) % ring_n], Bn[0]))
        else:
            for j in range(ring_n): bm.faces.new((A[j], A[(j + 1) % ring_n], Bn[(j + 1) % ring_n], Bn[j]))
    bm.faces.new(list(reversed(rings[0])))
    if not tip: bm.faces.new(rings[-1])
    faces_of(verts, mi)
    return verts

def taper(w0, belly=0.25, power=0.85):
    return lambda t: w0 * (1 - t) ** power * (1 + belly * math.sin(math.pi * min(1.0, t * 1.6)))

HAIR_PARENT = {}   # hair piece -> its style group (an empty riding on the head bone)
def hair_piece(name, bm, mats, grp, subsurf=1):
    ob = new_obj(name, bm, mats, C_HAIR, origin=HC, recalc=True)
    if subsurf:
        mod = ob.modifiers.new("Smooth", 'SUBSURF'); mod.levels = subsurf; mod.render_levels = subsurf + 1
    HAIR_PARENT[ob.name] = grp.name
    return ob

def scalp_cap(name, grp, back_el=-58, side_el=-40, hair_front=33):
    """Skull-hugging cap with a curved hairline (the face stays open)."""
    bm = bmesh.new()
    vs = bmesh.ops.create_uvsphere(bm, u_segments=48, v_segments=36, radius=1.0)['verts']
    def keep(c):
        az = math.degrees(math.atan2(c.x, -c.y)); el = math.degrees(math.asin(max(-1, min(1, c.z)))); a = abs(az)
        if a < 64 and el < hair_front + 10 * (a / 64) ** 2: return False
        return el > side_el + (back_el - side_el) * min(1, max(0, (a - 70) / 70))
    bmesh.ops.delete(bm, geom=[v for v in vs if not keep(v.co)], context='VERTS')
    left = [v for v in vs if v.is_valid]
    for v in left: v.co = HC + Vector((v.co.x * HR.x, v.co.y * HR.y, v.co.z * HR.z)) * HAIR_CAP
    faces_of(left, 0)
    return hair_piece(name, bm, ["Hair"], grp)

def bangs(bm, flick=0.0, short=0.0):
    """Short fringe: 6 soft clumps with a centre part, ending above the eyebrows."""
    for u in (-1.0, -0.6, -0.2, 0.2, 0.6, 1.0):
        az = u * 52
        e = 34 - 7 * abs(u) ** 1.5 + short
        w = 0.165 - 0.025 * abs(u)
        part = math.copysign(4, u)
        pts = [S(az * 0.25, 78, 0.985), S(az * 0.7, 62, 1.07), S(az * 0.95, e + 14, 1.085),
               S(az + part * 0.5, e + 5, 1.07), S(az + part + u * flick * 20, e, 1.045 + flick * 0.08)]
        sweep(bm, pts, taper(w, 0.35, 1.05), lambda t: LOCK_THICK * (1 - 0.55 * t), 0)

def side_locks(bm, length=1.0):
    for sx in (-1, 1):
        drop = -42 - (length - 1) * 60
        sweep(bm, [S(sx * 70, 52, 0.985), S(sx * 82, 24, 1.085), S(sx * 81, -12, 1.085), S(sx * 74, drop, 1.06), S(sx * 66, drop - 7, 1.035)],
              taper(0.12, 0.2), lambda t: LOCK_THICK * (1 - 0.5 * t), 0)
        sweep(bm, [S(sx * 58, 58, 0.985), S(sx * 62, 40, 1.08), S(sx * 65, 18, 1.08), S(sx * 64, -6, 1.055), S(sx * 61, -12, 1.035)],
              taper(0.07, 0.2), lambda t: LOCK_THICK * 0.8 * (1 - 0.5 * t), 0)

def back_locks(bm, end_el=-54, flick=-0.03, extra_drop=0.0, n=8, az0=80, az1=280):
    for i in range(n):
        az = az0 + (az1 - az0) * i / (n - 1)
        pts = [S(az, 66, 0.985), S(az, 38, 1.07), S(az, 4, 1.08), S(az, (end_el + 4) * 0.65, 1.075), S(az, end_el, 1.07 + flick)]
        if extra_drop > 0:
            last = pts[-1]; pts[-1] = S(az, end_el, 1.07)
            pts += [last + Vector((0, 0.015, -extra_drop * 0.5)), last + Vector((0, 0.0, -extra_drop))]
        sweep(bm, pts, taper(0.22, 0.12, 0.8), lambda t: LOCK_THICK * 1.1 * (1 - 0.5 * t), 0, samples=30)

def ahoge(bm):
    top = S(12, 86, 0.99)
    pts = [top, top + Vector((0.01, -0.02, 0.07)), top + Vector((0.05, -0.04, 0.12)), top + Vector((0.1, -0.02, 0.12)), top + Vector((0.115, 0.01, 0.09))]
    sweep(bm, pts, taper(0.05, 0.3, 0.9), lambda t: 0.02 * (1 - 0.6 * t), 0, samples=22, up_hint=top + Vector((0.05, 0.2, 0.0)))

def twin_tail(bm, sx):
    root = S(sx * 112, 22, 0.99); p1 = S(sx * 114, 20, 1.2)
    pts = [root, p1, p1 + Vector((sx * 0.07, 0.03, -0.14)), p1 + Vector((sx * 0.1, 0.03, -0.3)), p1 + Vector((sx * 0.07, 0.0, -0.42)), p1 + Vector((sx * 0.03, -0.03, -0.48))]
    sweep(bm, pts, lambda t: 0.15 * (0.5 + 0.8 * math.sin(math.pi * min(1, t * 1.05))) * (1 - t) ** 0.55,
          lambda t: 0.095 * (0.5 + 0.7 * math.sin(math.pi * min(1, t * 1.05))) * (1 - t) ** 0.55, 0, samples=32, ring_n=12,
          up_hint=HC + Vector((sx * 0.6, 0.6, 0.2)))
    return p1, (pts[2] - p1).normalized()

def make_hairstyle(style):
    grp = bpy.data.objects.new("Hair_" + style, None); C_HAIR.objects.link(grp)
    grp.empty_display_size = 0.05; grp.location = HC; RIDES[grp.name] = "head"
    if style == "Bob":
        scalp_cap("Hair_Bob_Cap", grp)
        bm = bmesh.new(); bangs(bm); hair_piece("Hair_Bob_Bangs", bm, ["Hair"], grp)
        bm = bmesh.new(); side_locks(bm); hair_piece("Hair_Bob_SideLocks", bm, ["Hair"], grp)
        bm = bmesh.new(); back_locks(bm); hair_piece("Hair_Bob_Back", bm, ["Hair"], grp)
        bm = bmesh.new(); ahoge(bm); hair_piece("Hair_Bob_Ahoge", bm, ["Hair"], grp)
        bm = bmesh.new()
        for k in (0, 1): capsule(bm, S(-58 + k * 6, 44, 1.1), S(-50 + k * 6, 56, 1.1), 0.01, 6, 4)
        hair_piece("Hair_Bob_Clip", bm, ["Accent"], grp, 0)
    elif style == "Long":
        scalp_cap("Hair_Long_Cap", grp, back_el=-62)
        bm = bmesh.new(); bangs(bm); hair_piece("Hair_Long_Bangs", bm, ["Hair"], grp)
        bm = bmesh.new(); side_locks(bm, 1.45); hair_piece("Hair_Long_SideLocks", bm, ["Hair"], grp)
        bm = bmesh.new(); back_locks(bm, end_el=-62, flick=0.0, extra_drop=0.24); hair_piece("Hair_Long_Back", bm, ["Hair"], grp)
    elif style == "Pigtails":
        scalp_cap("Hair_Pigtails_Cap", grp, back_el=-48, side_el=-30)
        bm = bmesh.new(); bangs(bm); hair_piece("Hair_Pigtails_Bangs", bm, ["Hair"], grp)
        bm = bmesh.new(); side_locks(bm, 0.9); hair_piece("Hair_Pigtails_SideLocks", bm, ["Hair"], grp)
        bm = bmesh.new(); back_locks(bm, end_el=-40, flick=0.0); hair_piece("Hair_Pigtails_Back", bm, ["Hair"], grp)
        bm = bmesh.new(); ties = bmesh.new()
        for sx in (-1, 1):
            p, d = twin_tail(bm, sx)
            cyl(ties, p - d * 0.015, 0.045, 0.045, 0.03, 0, d, 20)
        hair_piece("Hair_Pigtails_Tails", bm, ["Hair"], grp)
        hair_piece("Hair_Pigtails_Ties", ties, ["Accent"], grp)
    elif style == "Spiky":
        scalp_cap("Hair_Spiky_Cap", grp, back_el=-35, side_el=-20, hair_front=36)
        bm = bmesh.new(); bangs(bm, flick=0.6, short=4); hair_piece("Hair_Spiky_Bangs", bm, ["Hair"], grp)
        bm = bmesh.new(); back_locks(bm, end_el=-22, flick=0.12, n=8); hair_piece("Hair_Spiky_Back", bm, ["Hair"], grp)
        bm = bmesh.new()
        for az in (140, 180, 220, 105, 255):
            sweep(bm, [S(az, 74, 0.985), S(az, 76, 1.14), S(az, 68, 1.32)], taper(0.11, 0.2), lambda t: LOCK_THICK * (1 - 0.5 * t), 0, samples=18)
        hair_piece("Hair_Spiky_Crown", bm, ["Hair"], grp)
    return grp

for st in HAIR_NAMES: make_hairstyle(st)

# ----------------------------------------------------------------------------------- 6. hats
HT = 1.1                                   # hats sit over the hair (same fit as the old chibi hats)
TOP = HC.z + HR.z * HT; RW = HR.x * HT
def hat(name, bm, mats, origin): return new_obj("Hat_" + name, bm, mats, C_HAT, origin=origin, bone="head", recalc=True)

# Bucket: one closed shell - domed crown, band, wide droopy brim, underside, inside of the crown
HAT_BASE = Vector((0, 0, 0.84))
hat_outer = [(0, 0.205), (0.11, 0.203), (0.19, 0.195), (0.238, 0.178), (0.264, 0.15), (0.281, 0.105),
             (0.293, 0.062), (0.3, 0.035), (0.305, 0.012), (0.315, 0.0),
             (0.35, -0.03), (0.39, -0.062), (0.425, -0.092), (0.446, -0.11), (0.452, -0.122),
             (0.444, -0.13), (0.42, -0.112), (0.385, -0.083), (0.348, -0.052), (0.322, -0.024),
             (0.298, -0.004), (0.284, 0.04), (0.274, 0.09), (0.257, 0.137), (0.232, 0.168), (0.19, 0.183), (0.0, 0.186)]
def hat_mi(i, a, b): return 1 if i < 9 and 0.012 <= (a[1] + b[1]) / 2 <= 0.066 else 0
bm = bmesh.new(); vs = lathe(bm, hat_outer, 40, 1.0, mi_of=hat_mi)
transform_verts(vs, Matrix.Translation(HAT_BASE) @ Matrix.Rotation(math.radians(-15), 4, 'X') @ Matrix.Rotation(math.radians(3), 4, 'Y') @ Matrix.Scale(1.12, 4))
hat("Bucket", bm, ["HatMain", "HatAccent"], HAT_BASE)

bm = bmesh.new()    # Straw: flat brim, tall crown, band
cyl(bm, (0, 0, TOP - 0.07), RW + 0.13, RW + 0.13, 0.02, 0, seg=40); cyl(bm, (0, 0, TOP - 0.06), RW * 0.75, RW * 0.62, 0.13, 0, seg=32)
cyl(bm, (0, 0, TOP - 0.045), RW * 0.76, RW * 0.72, 0.04, 1, seg=32)
hat("Straw", bm, ["HatMain", "HatAccent"], (0, 0, TOP - 0.07))

bm = bmesh.new()    # Cap: dome over the top of the head + visor (sits above the eyebrows)
vs = bmesh.ops.create_uvsphere(bm, u_segments=32, v_segments=20, radius=1.0)['verts']
bmesh.ops.delete(bm, geom=[v for v in vs if v.co.z < 0.36], context='VERTS')
left = [v for v in vs if v.is_valid]
for v in left: v.co = HC + Vector((v.co.x * RW * 1.06, v.co.y * HR.y * HT * 1.08, v.co.z * HR.z * HT * 1.05))   # roomy enough for the hair
sph(bm, (0, -HR.y * HT * 1.06 - 0.05, HC.z + 0.135), (0.18, 0.13, 0.013), 1, 20, 8)
cap = hat("Cap", bm, ["HatMain", "HatAccent"], (0, 0, HC.z + 0.13))

bm = bmesh.new()    # Chicken sitting on your head
sph(bm, (0, 0.03, TOP + 0.05), (0.15, 0.17, 0.11), 0); sph(bm, (0, -0.11, TOP + 0.14), (0.085, 0.085, 0.085), 0, 16, 12)
sph(bm, (0, -0.11, TOP + 0.235), (0.02, 0.05, 0.035), 1, 10, 6); sph(bm, (0, -0.2, TOP + 0.13), (0.03, 0.04, 0.02), 2, 10, 6)
for sx in (1, -1):
    sph(bm, (sx * 0.045, -0.185, TOP + 0.16), (0.014, 0.01, 0.016), 3, 8, 6); sph(bm, (sx * 0.14, 0.04, TOP + 0.06), (0.03, 0.1, 0.06), 0, 10, 8)
hat("Chicken", bm, ["FixedWhite", "FixedRed", "FixedOrange", "FixedInk"], (0, 0, TOP))

bm = bmesh.new()    # Flower crown
for i in range(16):
    a = 2 * math.pi * i / 16
    p = HC + Vector((math.cos(a) * RW * 0.86, math.sin(a) * HR.y * HT * 0.82, HR.z * HT * 0.58))
    if i % 2 == 0:
        sph(bm, p, (0.045, 0.045, 0.03), 0 if i % 4 == 0 else 1, 10, 8); sph(bm, p + Vector((0, 0, 0.022)), (0.016, 0.016, 0.012), 2, 8, 6)
    else:
        sph(bm, p, (0.05, 0.025, 0.018), 3, 8, 6)
hat("FlowerCrown", bm, ["HatAccent", "HatMain", "FixedYellow", "FixedLeaf"], (0, 0, HC.z + HR.z * HT * 0.58))

bm = bmesh.new()    # Beret: snug band round the head, puffy top slouching over to one side, little stalk
BERET_BASE = Vector((0, 0, 0.84))
beret = [(0.25, 0.0), (0.262, 0.03), (0.30, 0.06), (0.325, 0.09), (0.33, 0.112), (0.31, 0.135), (0.25, 0.155), (0.14, 0.168), (0.0, 0.171),
         (0.0, 0.149), (0.14, 0.14), (0.22, 0.09), (0.245, 0.02)]     # puffier, more dome-like
vs = lathe(bm, beret, 36, 0.92, closed=True)
for v in vs:
    if v.co.z > 0.07: v.co.x += 0.07 * min(1.0, (v.co.z - 0.07) / 0.09)       # slouch
vs2 = cyl(bm, (0.07, 0, 0.165), 0.012, 0.009, 0.04, 0, seg=8)
transform_verts(vs + vs2, Matrix.Translation(HC) @ Matrix.Scale(1.09, 4) @ Matrix.Translation(BERET_BASE - HC))   # sized over the hair
hat("Beret", bm, ["HatMain"], BERET_BASE)

# ----------------------------------------------------------------------------------- 7. rig: every part rides on one bone
BONES = {
    "hips":   ((0, 0, 0.12), (0, 0, 0.2), None),
    "spine":  ((0, 0, 0.2), (0, 0, 0.38), "hips"),
    "head":   ((0, 0, 0.38), (0, 0, 0.95), "spine"),
    "arm.L":  (SHOULDER[1], WRIST[1], "spine"),   "hand.L": (WRIST[1], WRIST[1] + ARM_DIR[1] * 0.06, "arm.L"),
    "arm.R":  (SHOULDER[-1], WRIST[-1], "spine"), "hand.R": (WRIST[-1], WRIST[-1] + ARM_DIR[-1] * 0.06, "arm.R"),
    "leg.L":  (HIP[1], ANKLE[1], "hips"),          "leg.R":  (HIP[-1], ANKLE[-1], "hips"),
}
rig_data = bpy.data.armatures.new("Bean_Rig")
rig = bpy.data.objects.new("Bean_Rig", rig_data); C_ROOT.objects.link(rig)
bpy.context.view_layer.objects.active = rig; rig.select_set(True)
bpy.ops.object.mode_set(mode='EDIT')
for name, (h, t, p) in BONES.items():
    eb = rig_data.edit_bones.new(name); eb.head, eb.tail = Vector(h), Vector(t)
    eb.align_roll(Vector((0, -1, 0)))
    if p: eb.parent = rig_data.edit_bones[p]; eb.use_connect = name.startswith("hand")
bpy.ops.object.mode_set(mode='OBJECT')
GROUPS = list(BONES.keys())

# hold points: where an item goes when the bean holds it
for s, side in SIDES:
    e = bpy.data.objects.new("HoldPoint_" + side, None); C_BODY.objects.link(e)
    e.empty_display_type = 'SPHERE'; e.empty_display_size = 0.03
    e.location = WRIST[s] + ARM_DIR[s] * 0.025 + Vector((0, -0.02, 0))
    RIDES[e.name] = "hand." + side

bpy.context.view_layer.update()
worlds = {n: bpy.data.objects[n].matrix_world.copy() for n in RIDES}
for n, bone in RIDES.items():
    ob = bpy.data.objects[n]; ob.parent = rig; ob.parent_type = 'BONE'; ob.parent_bone = bone
bpy.context.view_layer.update()
for n in RIDES: bpy.data.objects[n].matrix_world = worlds[n]
bpy.context.view_layer.update()
hw = {n: bpy.data.objects[n].matrix_world.copy() for n in HAIR_PARENT}
for n, g in HAIR_PARENT.items(): bpy.data.objects[n].parent = bpy.data.objects[g]
bpy.context.view_layer.update()
for n in HAIR_PARENT: bpy.data.objects[n].matrix_world = hw[n]

# ----------------------------------------------------------------------------------- 8. floppy animations
# Rotations are degrees in each bone's own axes: X = swing forward/back, Z = lift sideways / roll, Y = twist.
D = math.radians
def pose_frame(pose):
    for name in GROUPS:
        pb = rig.pose.bones[name]; pb.rotation_mode = 'XYZ'
        rx, ry, rz, lx, ly, lz = pose.get(name, (0, 0, 0, 0, 0, 0))
        pb.rotation_euler = (D(rx), D(ry), D(rz)); pb.location = (lx, ly, lz)

def make_action(name, frames, pose_fn, samples, loop=True):
    act = bpy.data.actions.new(name); act.use_fake_user = True
    rig.animation_data_create(); rig.animation_data.action = act
    for i in range(samples + (1 if loop else 0)):
        t = (i % samples) / samples if loop else i / (samples - 1)
        f = 1 + round(i * (frames - 1) / samples) if loop else 1 + round(t * (frames - 1))
        pose_frame(pose_fn(t))
        for n in GROUPS:
            pb = rig.pose.bones[n]; pb.keyframe_insert("rotation_euler", frame=f); pb.keyframe_insert("location", frame=f)
    act.frame_range = (1, frames)
    return act

SN = lambda t, k=1, ph=0: math.sin(2 * math.pi * (k * t + ph))
BOB = lambda t: (1 - math.cos(4 * math.pi * t)) / 2           # up twice per stride

def idle(t):   # breathing, lazy head wobble, arms dangling
    return {"hips": (0, 0, 0, 0, 0.006 * SN(t), 0), "spine": (2 * SN(t), 0, 1.5 * SN(t, 1, 0.1), 0, 0, 0),
            "head": (2 * SN(t, 1, 0.3), 2 * SN(t, 1, 0.4), 5 * SN(t, 1, 0.15), 0, 0, 0),
            "arm.L": (3 * SN(t, 1, 0.2), 0, -4 - 4 * SN(t, 1, 0.25), 0, 0, 0), "arm.R": (3 * SN(t, 1, 0.2), 0, 4 + 4 * SN(t, 1, 0.25), 0, 0, 0),
            "hand.L": (6 * SN(t, 1, 0.35), 0, 0, 0, 0, 0), "hand.R": (6 * SN(t, 1, 0.35), 0, 0, 0, 0, 0)}

def walk(t):   # waddle: hips roll + bob, head and arms flop a beat behind
    s = SN(t)
    return {"hips": (0, 7 * s, 8 * s, 0, 0.022 * BOB(t), 0),
            "spine": (6, -4 * s, -6 * SN(t, 1, 0.08), 0, 0, 0),
            "head": (-4 + 5 * SN(t, 2, 0.15), 5 * SN(t, 1, 0.25), 10 * SN(t, 1, 0.2), 0, 0, 0),
            "leg.L": (34 * s, 0, 0, 0, 0, 0), "leg.R": (-34 * s, 0, 0, 0, 0, 0),
            "arm.L": (-40 * SN(t, 1, 0.1), 0, 10 + 10 * SN(t, 2, 0.2), 0, 0, 0), "arm.R": (40 * SN(t, 1, 0.1), 0, -10 - 10 * SN(t, 2, 0.2), 0, 0, 0),
            "hand.L": (-22 * SN(t, 1, 0.25), 0, 0, 0, 0, 0), "hand.R": (22 * SN(t, 1, 0.25), 0, 0, 0, 0, 0)}

def run(t):    # leaning, arms flailing loosely, big head bobble
    s = SN(t)
    return {"hips": (0, 10 * s, 10 * s, 0, 0.045 * BOB(t), 0),
            "spine": (16, -6 * s, -8 * SN(t, 1, 0.1), 0, 0, 0),
            "head": (-12 + 8 * SN(t, 2, 0.15), 6 * SN(t, 1, 0.25), 14 * SN(t, 1, 0.22), 0, 0, 0),
            "leg.L": (55 * s, 0, 0, 0, 0, 0), "leg.R": (-55 * s, 0, 0, 0, 0, 0),
            "arm.L": (-70 * SN(t, 1, 0.12), 0, 30 + 18 * SN(t, 2, 0.2), 0, 0, 0), "arm.R": (70 * SN(t, 1, 0.12), 0, -30 - 18 * SN(t, 2, 0.2), 0, 0, 0),
            "hand.L": (-35 * SN(t, 1, 0.3), 0, 0, 0, 0, 0), "hand.R": (35 * SN(t, 1, 0.3), 0, 0, 0, 0, 0)}

def jump(t):   # crouch, then stretch with arms flung up and legs dangling
    if t < 0.25:
        k = t / 0.25
        return {"hips": (0, 0, 0, 0, -0.035 * k, 0), "spine": (12 * k, 0, 0, 0, 0, 0), "head": (6 * k, 0, 0, 0, 0, 0),
                "leg.L": (25 * k, 0, 0, 0, 0, 0), "leg.R": (25 * k, 0, 0, 0, 0, 0),
                "arm.L": (-20 * k, 0, -15 * k, 0, 0, 0), "arm.R": (-20 * k, 0, 15 * k, 0, 0, 0)}
    k = min(1.0, (t - 0.25) / 0.4); w = math.sin(min(1.0, (t - 0.25) / 0.75) * math.pi * 3) * (1 - k * 0.5)
    return {"hips": (0, 0, 0, 0, -0.035 * (1 - k), 0), "spine": (12 - 18 * k, 0, 0, 0, 0, 0), "head": (6 - 14 * k + 4 * w, 0, 3 * w, 0, 0, 0),
            "leg.L": (25 + 10 * k, 0, 0, 0, 0, 0), "leg.R": (25 - 30 * k, 0, 0, 0, 0, 0),
            "arm.L": (-20 * (1 - k), 0, 100 * k + 12 * w, 0, 0, 0), "arm.R": (-20 * (1 - k), 0, -100 * k - 12 * w, 0, 0, 0),
            "hand.L": (0, 0, 20 * w, 0, 0, 0), "hand.R": (0, 0, -20 * w, 0, 0, 0)}

def wave(t):   # right arm up, floppy hand waving, head tilt
    return {"hips": (0, 0, 3 * SN(t), 0, 0.006 * SN(t, 2), 0), "spine": (0, 0, 4, 0, 0, 0), "head": (0, 0, -9 + 3 * SN(t, 1, 0.2), 0, 0, 0),
            "arm.R": (15, 0, -110 + 12 * SN(t, 2), 0, 0, 0), "hand.R": (0, 0, -30 * SN(t, 2, 0.15), 0, 0, 0),
            "arm.L": (4 * SN(t, 1, 0.2), 0, -5, 0, 0, 0)}

def cheer(t):  # bouncing with both arms pumping, hands flopping
    b = abs(SN(t))
    return {"hips": (0, 0, 0, 0, 0.055 * b, 0), "spine": (-5 * b, 0, 0, 0, 0, 0), "head": (-8 * b + 4 * SN(t, 2, 0.2), 0, 6 * SN(t, 1, 0.2), 0, 0, 0),
            "arm.L": (0, 0, 100 + 15 * SN(t, 2), 0, 0, 0), "arm.R": (0, 0, -100 - 15 * SN(t, 2), 0, 0, 0),
            "hand.L": (0, 0, 25 * SN(t, 2, 0.2), 0, 0, 0), "hand.R": (0, 0, -25 * SN(t, 2, 0.2), 0, 0, 0),
            "leg.L": (10 * b, 0, 0, 0, 0, 0), "leg.R": (-10 * b, 0, 0, 0, 0, 0)}

def fall(t):   # earthquake! wobble, topple onto its back, legs kicking, then pop back up
    E = lambda x: x * x * (3 - 2 * x)
    if t < 0.12:                                   # stumble
        k = t / 0.12
        return {"hips": (-6 * k, 0, 10 * SN(k, 1.5), 0, 0, 0), "head": (8 * SN(k, 1.5, 0.2), 0, -12 * SN(k, 1.5, 0.15), 0, 0, 0),
                "arm.L": (-20 * k, 0, 40 * k, 0, 0, 0), "arm.R": (-20 * k, 0, -40 * k, 0, 0, 0)}
    if t < 0.32:                                   # topple backwards
        k = E((t - 0.12) / 0.2)
        return {"hips": (-6 - 80 * k, 0, 10 * (1 - k), 0, 0.17 * k, 0), "spine": (-6 * k, 0, 0, 0, 0, 0), "head": (14 * k, 0, 0, 0, 0, 0),
                "leg.L": (45 * k, 0, 0, 0, 0, 0), "leg.R": (35 * k, 0, 0, 0, 0, 0),
                "arm.L": (-20 + 70 * k, 0, 40 + 50 * k, 0, 0, 0), "arm.R": (-20 + 70 * k, 0, -40 - 50 * k, 0, 0, 0)}
    if t < 0.72:                                   # lying there, flailing, a little bounce
        k = (t - 0.32) / 0.4; b = math.exp(-6 * k) * math.sin(k * 30)
        return {"hips": (-86 + 4 * b, 0, 3 * SN(k, 2), 0, 0.17, 0), "spine": (-6, 0, 0, 0, 0, 0), "head": (14 + 6 * SN(k, 3, 0.1), 0, 8 * SN(k, 2, 0.2), 0, 0, 0),
                "leg.L": (45 + 25 * SN(k, 4), 0, 0, 0, 0, 0), "leg.R": (35 - 25 * SN(k, 4), 0, 0, 0, 0, 0),
                "arm.L": (50 + 20 * SN(k, 3), 0, 90 + 15 * SN(k, 3, 0.2), 0, 0, 0), "arm.R": (50 - 20 * SN(k, 3), 0, -90 - 15 * SN(k, 3, 0.2), 0, 0, 0),
                "hand.L": (0, 0, 30 * SN(k, 4), 0, 0, 0), "hand.R": (0, 0, -30 * SN(k, 4), 0, 0, 0)}
    k = E((t - 0.72) / 0.28); o = math.sin(k * math.pi) * 10           # spring back up with a little overshoot
    return {"hips": (-86 * (1 - k) + o, 0, 0, 0, 0.17 * (1 - k), 0), "spine": (-6 * (1 - k), 0, 0, 0, 0, 0), "head": (14 * (1 - k) - o, 0, 0, 0, 0, 0),
            "leg.L": (45 * (1 - k), 0, 0, 0, 0, 0), "leg.R": (35 * (1 - k), 0, 0, 0, 0, 0),
            "arm.L": (50 * (1 - k), 0, 90 * (1 - k) + o * 2, 0, 0, 0), "arm.R": (50 * (1 - k), 0, -90 * (1 - k) - o * 2, 0, 0, 0)}

def carry(t):  # both arms forward to hold something in front (for later)
    return {"hips": (0, 0, 0, 0, 0.005 * SN(t), 0), "spine": (-3, 0, 0, 0, 0, 0), "head": (2 * SN(t, 1, 0.2), 0, 3 * SN(t, 1, 0.15), 0, 0, 0),
            "arm.L": (75, 0, -22, 0, 0, 0), "arm.R": (75, 0, 22, 0, 0, 0)}


# ---- story-intro acting (lying poses rest on the floor, like the Fall clip's lying part)
LIE = 0.17
def sleep(t):  # fast asleep on its back: slow breathing, head lolled, arms flopped out
    return {"hips": (-86, 0, 2 * SN(t), 0, LIE + 0.004 * SN(t), 0), "spine": (-6 + 3 * SN(t), 0, 0, 0, 0, 0),
            "head": (10, 0, 18 + 3 * SN(t, 1, 0.2), 0, 0, 0),
            "arm.L": (10, 0, 55 + 4 * SN(t, 1, 0.1), 0, 0, 0), "arm.R": (10, 0, -55 - 4 * SN(t, 1, 0.1), 0, 0, 0),
            "hand.L": (0, 0, 15, 0, 0, 0), "hand.R": (0, 0, -15, 0, 0, 0),
            "leg.L": (0, 0, 8, 0, 0, 0), "leg.R": (0, 0, -8, 0, 0, 0)}

def sleep_sprawl(t):  # messy sleeper: arm flung over its head, one knee up, a little kick now and then
    kick = max(0.0, math.sin(2 * math.pi * t * 2)) ** 6
    return {"hips": (-86, 0, -4 + 2 * SN(t), 0, LIE + 0.004 * SN(t), 0), "spine": (-6 + 3 * SN(t), 0, 0, 0, 0, 0),
            "head": (12, 0, -22 + 3 * SN(t, 1, 0.3), 0, 0, 0),
            "arm.R": (0, 0, -165 + 4 * SN(t, 1, 0.2), 0, 0, 0), "hand.R": (0, 0, -20, 0, 0, 0),
            "arm.L": (25, 0, 35 + 3 * SN(t, 1, 0.1), 0, 0, 0), "hand.L": (10, 0, 0, 0, 0, 0),
            "leg.L": (40 + 18 * kick, 0, 12, 0, 0, 0), "leg.R": (0, 0, -10, 0, 0, 0)}

def wake_up(t):  # twitch... then BOING! pops up from lying down, arms flung out, wobbles to a stand
    E = lambda x: x * x * (3 - 2 * x)
    if t < 0.15:
        k = t / 0.15
        return {"hips": (-86, 0, 0, 0, LIE, 0), "spine": (-6, 0, 0, 0, 0, 0), "head": (10, 0, 18 * (1 - k), 0, 0, 0),
                "arm.L": (10, 0, 55, 0, 0, 0), "arm.R": (10, 0, -55, 0, 0, 0), "leg.L": (8 * math.sin(k * 9), 0, 8, 0, 0, 0), "leg.R": (0, 0, -8, 0, 0, 0)}
    if t < 0.5:
        k = E((t - 0.15) / 0.35); up = math.sin(k * math.pi) * 0.07
        return {"hips": (-86 * (1 - k) + 12 * math.sin(k * math.pi), 0, 0, 0, LIE * (1 - k) + up, 0), "spine": (-6 * (1 - k), 0, 0, 0, 0, 0),
                "head": (10 * (1 - k) - 14 * math.sin(k * math.pi), 0, 0, 0, 0, 0),
                "arm.L": (10 * (1 - k), 0, 55 + 75 * k, 0, 0, 0), "arm.R": (10 * (1 - k), 0, -55 - 75 * k, 0, 0, 0),
                "hand.L": (0, 0, 30 * k, 0, 0, 0), "hand.R": (0, 0, -30 * k, 0, 0, 0),
                "leg.L": (30 * math.sin(k * math.pi), 0, 8 * (1 - k), 0, 0, 0), "leg.R": (-20 * math.sin(k * math.pi), 0, -8 * (1 - k), 0, 0, 0)}
    k = E((t - 0.5) / 0.5); w = math.exp(-4 * k) * math.sin(k * 22)
    return {"hips": (6 * w, 0, 5 * w, 0, 0, 0), "spine": (-4 * w, 0, 0, 0, 0, 0), "head": (-6 * w, 0, 14 * w, 0, 0, 0),
            "arm.L": (0, 0, 130 * (1 - k) + 6 + 15 * w, 0, 0, 0), "arm.R": (0, 0, -130 * (1 - k) - 6 - 15 * w, 0, 0, 0),
            "hand.L": (0, 0, 30 * (1 - k) + 20 * w, 0, 0, 0), "hand.R": (0, 0, -30 * (1 - k) - 20 * w, 0, 0, 0)}

def hot_fan(t):  # sooo hot: slouching, panting, fanning its face fast with one floppy hand
    pant = SN(t, 4)
    return {"hips": (0, 0, 3 * SN(t), 0, 0.006 * pant, 0), "spine": (10 + 2 * pant, 0, 3 * SN(t, 1, 0.1), 0, 0, 0),
            "head": (-12 + 3 * pant, 0, 8 * SN(t, 1, 0.2), 0, 0, 0),
            "arm.R": (60, 0, -60 + 25 * SN(t, 4), 0, 0, 0), "hand.R": (0, 0, -50 * SN(t, 4, 0.2), 0, 0, 0),
            "arm.L": (8, 0, -2 + 3 * SN(t, 1, 0.2), 0, 0, 0), "hand.L": (12, 0, 0, 0, 0, 0),
            "leg.L": (0, 0, -4 + 3 * SN(t), 0, 0, 0), "leg.R": (0, 0, 4 - 3 * SN(t), 0, 0, 0)}

def panic(t):  # AAAH! hopping on the spot, arms flapping out to the sides (the head hides arms held up high)
    b = abs(SN(t, 2))
    return {"hips": (0, 6 * SN(t, 2), 0, 0, 0.045 * b, 0), "spine": (-6, 0, 4 * SN(t, 2, 0.1), 0, 0, 0),
            "head": (-10, 0, 12 * SN(t, 2, 0.15), 0, 0, 0),
            "arm.L": (20 * SN(t, 2), 0, 80 + 30 * SN(t, 2), 0, 0, 0), "arm.R": (-20 * SN(t, 2, 0.25), 0, -80 - 30 * SN(t, 2, 0.25), 0, 0, 0),
            "hand.L": (0, 0, 35 * SN(t, 2, 0.2), 0, 0, 0), "hand.R": (0, 0, -35 * SN(t, 2, 0.45), 0, 0, 0),
            "leg.L": (28 * SN(t, 2), 0, 0, 0, 0, 0), "leg.R": (-28 * SN(t, 2), 0, 0, 0, 0, 0)}

def dive(t):  # crouch... then a superman dive (out of the window!), arms out front, legs trailing
    E = lambda x: x * x * (3 - 2 * x)
    if t < 0.3:
        k = E(t / 0.3)
        return {"hips": (0, 0, 0, 0, -0.04 * k, 0), "spine": (14 * k, 0, 0, 0, 0, 0), "head": (8 * k, 0, 0, 0, 0, 0),
                "leg.L": (28 * k, 0, 0, 0, 0, 0), "leg.R": (28 * k, 0, 0, 0, 0, 0),
                "arm.L": (-35 * k, 0, 10 * k, 0, 0, 0), "arm.R": (-35 * k, 0, -10 * k, 0, 0, 0)}
    k = E(min(1.0, (t - 0.3) / 0.3)); f = SN(t, 3) * min(1.0, (t - 0.3) / 0.3)
    return {"hips": (75 * k, 0, 0, 0, -0.04 * (1 - k), 0), "spine": (14 * (1 - k), 0, 0, 0, 0, 0), "head": (8 - 50 * k, 0, 4 * f, 0, 0, 0),
            "arm.L": (-35 + 200 * k, 0, 10 + 4 * f, 0, 0, 0), "arm.R": (-35 + 200 * k, 0, -10 - 4 * f, 0, 0, 0),
            "hand.L": (0, 0, 15 * f, 0, 0, 0), "hand.R": (0, 0, -15 * f, 0, 0, 0),
            "leg.L": (28 - 50 * k + 12 * f, 0, 6 * k, 0, 0, 0), "leg.R": (28 - 50 * k - 12 * f, 0, -6 * k, 0, 0, 0)}

def pose(t):  # hero pose for the player cards: fist punched out and up, other hand on hip, feet planted wide
    return {"hips": (0, 10, 0, 0, 0.01 * SN(t), 0), "spine": (-4, 0, -4, 0, 0, 0), "head": (0, -6, -10 + 2 * SN(t, 1, 0.2), 0, 0, 0),
            "arm.R": (55, 0, -80 + 5 * SN(t), 0, 0, 0), "hand.R": (0, 0, 0, 0, 0, 0),
            "arm.L": (-10, 0, 32, 0, 0, 0), "hand.L": (0, 0, -60, 0, 0, 0),
            "leg.L": (0, 0, 12, 0, 0, 0), "leg.R": (0, 0, -12, 0, 0, 0)}

bpy.context.view_layer.objects.active = rig
bpy.ops.object.mode_set(mode='POSE')
for a in [("Idle", 60, idle, 12, True), ("Walk", 26, walk, 12, True), ("Run", 16, run, 12, True), ("Jump", 22, jump, 11, False),
          ("Wave", 30, wave, 10, True), ("Cheer", 24, cheer, 10, True), ("Carry", 40, carry, 8, True),
          ("Fall", 54, fall, 28, False),
          ("Sleep", 90, sleep, 10, True), ("SleepSprawl", 90, sleep_sprawl, 16, True), ("WakeUp", 36, wake_up, 24, False),
          ("HotFan", 24, hot_fan, 12, True), ("Panic", 16, panic, 12, True), ("Dive", 24, dive, 16, False),
          ("Pose", 40, pose, 10, True)]:
    make_action(*a)
pose_frame({})
bpy.ops.object.mode_set(mode='OBJECT')
scene.frame_start, scene.frame_end = 1, 60

def rest_pose():
    rig.animation_data.action = None
    for b in rig.pose.bones: b.rotation_euler = (0, 0, 0); b.location = (0, 0, 0)
rest_pose()

# ----------------------------------------------------------------------------------- 9. export to Unity
def character_objects(): return [o for c in (C_BODY, C_FACE, C_CLOTH, C_HAT, C_HAIR) for o in c.objects] + [rig]
if DO_EXPORT:
    os.makedirs(os.path.dirname(FBX_PATH), exist_ok=True)
    bpy.ops.object.select_all(action='DESELECT')
    for o in character_objects(): o.hide_set(False); o.hide_viewport = False; o.select_set(True)
    rig.animation_data.action = bpy.data.actions["Idle"]
    bpy.ops.export_scene.fbx(
        filepath=FBX_PATH, use_selection=True, object_types={'ARMATURE', 'MESH', 'EMPTY'}, use_mesh_modifiers=True,
        apply_scale_options='FBX_SCALE_ALL', axis_forward='-Z', axis_up='Y', add_leaf_bones=False,
        primary_bone_axis='Y', secondary_bone_axis='X', mesh_smooth_type='FACE', use_armature_deform_only=False,
        bake_anim=True, bake_anim_use_all_actions=True, bake_anim_use_nla_strips=False,
        bake_anim_force_startend_keying=True, bake_anim_simplify_factor=0.0)
    rest_pose()
    bpy.ops.object.select_all(action='DESELECT')

# ----------------------------------------------------------------------------------- 10. show the chosen look
def show_look(face, hat_name, hair=None):
    hair = HERO_HAIR if hair is None else hair
    for st in HAIR_NAMES:
        for o in bpy.data.objects:
            if o.name == "Hair_" + st or o.name.startswith("Hair_" + st + "_"):
                hide = st != hair or (hat_name != "None" and (o.name.endswith("_Ahoge") or o.name.endswith("_Crown")))
                o.hide_render = hide; o.hide_set(hide)
    for f in FACES:
        for pre in ("Brow_", "Mouth_"):
            o = bpy.data.objects.get(pre + f)
            if o: o.hide_render = f != face; o.hide_set(f != face)
    for h in HAT_NAMES:
        o = bpy.data.objects.get("Hat_" + h)
        if o: o.hide_render = h != hat_name; o.hide_set(h != hat_name)
show_look(EXPRESSION, HERO_HAT)

# ----------------------------------------------------------------------------------- 11. studio
bm = bmesh.new(); bmesh.ops.create_grid(bm, x_segments=1, y_segments=1, size=4.0)
new_obj("Bean_Ground", bm, ["Ground"], C_STUDIO, recalc=False)
world = scene.world or bpy.data.worlds.new("World"); scene.world = world; world.use_nodes = True
bg = world.node_tree.nodes.get("Background")
bg.inputs[0].default_value = (*hexc("#BCD6F2"), 1); bg.inputs[1].default_value = 0.55

def area_light(name, loc, size, power, color=(1, 1, 1)):
    ld = bpy.data.lights.new("Bean_" + name, 'AREA'); ld.size = size; ld.energy = power; ld.color = color
    ob = bpy.data.objects.new("Bean_" + name, ld); C_STUDIO.objects.link(ob); ob.location = loc
    ob.rotation_euler = (Vector((0, 0, 0.55)) - Vector(loc)).to_track_quat('-Z', 'Y').to_euler()
area_light("Key", (-1.6, -2.2, 2.4), 3.0, 55, (1.0, 0.96, 0.92))
area_light("Fill", (2.2, -1.6, 1.2), 3.0, 20, (0.9, 0.94, 1.0))
area_light("Rim", (0.6, 2.4, 2.0), 2.0, 45, (1.0, 0.92, 0.95))

cam_data = bpy.data.cameras.new("Bean_Camera"); cam_data.lens = 70
cam = bpy.data.objects.new("Bean_Camera", cam_data); C_STUDIO.objects.link(cam); scene.camera = cam
def aim(az, el=10, dist=3.0, target=(0, 0, 0.52)):
    a, e = math.radians(az), math.radians(el); T = Vector(target)
    cam.location = T + Vector((math.sin(a) * math.cos(e), -math.cos(a) * math.cos(e), math.sin(e))) * dist
    cam.rotation_euler = (T - cam.location).to_track_quat('-Z', 'Y').to_euler()

for eng in ("BLENDER_EEVEE_NEXT", "BLENDER_EEVEE"):
    try: scene.render.engine = eng; break
    except TypeError: pass
try: scene.view_settings.view_transform = 'Standard'
except TypeError: pass
if hasattr(scene, "eevee"):
    try: scene.eevee.taa_render_samples = 48
    except AttributeError: pass

# ----------------------------------------------------------------------------------- 12. renders
def shot(path, az, el=10, dist=3.0, target=(0, 0, 0.52), res=(900, 1100)):
    scene.render.resolution_x, scene.render.resolution_y = res
    aim(az, el, dist, target); scene.render.filepath = path
    bpy.ops.render.render(write_still=True)

VIEWS = {"3Q": -35, "FRONT": 0, "SIDE": -90, "BACK": 180}
if DO_RENDER:
    os.makedirs(RENDER_DIR, exist_ok=True)
    for name, az in VIEWS.items():
        if VIEW != "ALL" and VIEW != name: continue
        shot(os.path.join(RENDER_DIR, f"bean_{name.lower()}.png"), az)
    # a frame of the floppy walk, to check the pose
    rig.animation_data.action = bpy.data.actions["Walk"]; scene.frame_set(7)
    shot(os.path.join(RENDER_DIR, "bean_walk.png"), -35)
    rig.animation_data.action = bpy.data.actions["Cheer"]; scene.frame_set(7)
    shot(os.path.join(RENDER_DIR, "bean_cheer.png"), -20)
    rig.animation_data.action = bpy.data.actions["Fall"]; scene.frame_set(28)
    shot(os.path.join(RENDER_DIR, "bean_fall.png"), -60)
    if DO_ACT_SHEET:   # story-intro acting: a few frames of each
        for act, frames, az in (("Sleep", (1,), -60), ("SleepSprawl", (1, 23), -60), ("WakeUp", (1, 12, 18, 36), -40),
                                ("HotFan", (1, 4), -30), ("Panic", (1, 5), -20), ("Dive", (8, 24), -90), ("Pose", (1,), -20)):
            rig.animation_data.action = bpy.data.actions[act]
            for fr in frames:
                scene.frame_set(fr)
                shot(os.path.join(RENDER_DIR, f"bean_act_{act.lower()}_{fr}.png"), az, 14, 3.3, (0, 0, 0.45), (420, 460))
    rest_pose(); scene.frame_set(1)
    if DO_FACE_SHEET:
        show_look(EXPRESSION, "None")
        for f in FACES:
            show_look(f, "None")
            shot(os.path.join(RENDER_DIR, f"bean_face_{f.lower()}.png"), 0, 4, 1.25, (0, 0, 0.62), (420, 420))
        for st in HAIR_NAMES:
            for az, tag in ((-30, "3q"), (150, "back")):
                show_look(EXPRESSION, "None", st)
                shot(os.path.join(RENDER_DIR, f"bean_hair_{st.lower()}_{tag}.png"), az, 10, 1.9, (0, 0, 0.62), (420, 420))
        for h in HAT_NAMES:
            show_look(EXPRESSION, h)
            shot(os.path.join(RENDER_DIR, f"bean_hat_{h.lower()}.png"), -30, 10, 1.9, (0, 0, 0.7), (420, 420))
    show_look(EXPRESSION, HERO_HAT)
    scene.render.resolution_x, scene.render.resolution_y = 900, 1100

aim(VIEWS["3Q"] if VIEW == "ALL" else VIEWS.get(VIEW, -35))
os.makedirs(os.path.dirname(BLEND_PATH), exist_ok=True)
bpy.ops.wm.save_as_mainfile(filepath=BLEND_PATH)
print("BEAN BUDDY DONE")
