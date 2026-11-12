# Blender headless: import a Varco GLB, strip the "AI render" look, render game sprites.
# usage: blender -b --factory-startup -P toon.py -- <glb> <outdir> <config.json>
#  1. texture -> k-means, then merge clusters that are only baked light/shadow of each other,
#     then a mode filter: flat colour areas, no clay noise, no baked gradients
#  2. flat colour + one hard cel band, no specular
#  3. face (eyes / mouth / cheeks) redrawn as ink glyphs at 3D anchor points (raycast from a reference view)
#  4. thick ink silhouette outline + inverted-hull inner lines, flat ground shadow, fitted into the old sprite box
import bpy, sys, json, math, os
import numpy as np
from mathutils import Vector, Matrix
from bpy_extras.object_utils import world_to_camera_view

argv = sys.argv[sys.argv.index("--") + 1:]
GLB, OUT = argv[0], argv[1]
CFG = json.load(open(argv[2], encoding="utf-8"))
if not os.path.isabs(CFG.get("old_dir", "")):   # relative to the config file
    CFG["old_dir"] = os.path.normpath(os.path.join(os.path.dirname(os.path.abspath(argv[2])), CFG.get("old_dir", "")))
os.makedirs(OUT, exist_ok=True)
INK = np.array([0x2b, 0x22, 0x33], np.float32) / 255
RES = CFG.get("res", 1024)

def hexc(h): return np.array([int(h[i:i + 2], 16) for i in (1, 3, 5)], np.float32) / 255
def lin(c): c = np.asarray(c, np.float32); return np.where(c <= 0.04045, c / 12.92, ((c + 0.055) / 1.055) ** 2.4)

bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.gltf(filepath=GLB)
obj = [o for o in bpy.data.objects if o.type == 'MESH'][0]
bpy.context.view_layer.objects.active = obj; obj.select_set(True)
bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
bpy.ops.object.mode_set(mode='EDIT'); bpy.ops.mesh.select_all(action='SELECT')
bpy.ops.mesh.remove_doubles(threshold=0.0005); bpy.ops.object.mode_set(mode='OBJECT')

vs = np.array([v.co[:] for v in obj.data.vertices])
mn, mx = vs.min(0), vs.max(0)
obj.data.transform(Matrix.Translation((-(mn[0] + mx[0]) / 2, -(mn[1] + mx[1]) / 2, -mn[2])))
obj.data.transform(Matrix.Scale(1.0 / ((mx - mn).max() if CFG.get("norm") == "max" else (mx[2] - mn[2])), 4))
obj.data.update()
sc = bpy.context.scene
turn = bpy.data.objects.new("turn", None); sc.collection.objects.link(turn)
roll = bpy.data.objects.new("roll", None); sc.collection.objects.link(roll)
obj.parent = turn; turn.parent = roll

# ---------- 1. flat texture ----------
mat = obj.material_slots[0].material; nt = mat.node_tree
bsdf = next(n for n in nt.nodes if n.type == 'BSDF_PRINCIPLED')
img = bsdf.inputs['Base Color'].links[0].from_node.image
W, H = img.size
px = np.empty(W * H * 4, np.float32); img.pixels.foreach_get(px)
px = px.reshape(-1, 4)[:, :3].copy()           # sRGB values
K = CFG.get("k", 8)
rng = np.random.default_rng(1)
sample = px[rng.choice(len(px), 80000, replace=False)]
cent = [sample[0]]
for _ in range(K - 1):
    d = np.min(((sample[:, None] - np.array(cent)[None]) ** 2).sum(-1), 1); cent.append(sample[d.argmax()])
cent = np.array(cent)
for _ in range(30):
    lab = ((sample[:, None] - cent[None]) ** 2).sum(-1).argmin(1)
    for k in range(K):
        if (lab == k).any(): cent[k] = sample[lab == k].mean(0)
lab = np.empty(len(px), np.int32)
for i in range(0, len(px), 1 << 20):
    ch = px[i:i + (1 << 20)]; lab[i:i + (1 << 20)] = ((ch[:, None] - cent[None]) ** 2).sum(-1).argmin(1)
count = np.bincount(lab, minlength=K)
# merge clusters that differ only by baked light (same chromaticity, brightness ratio > 0.72)
chrom = cent / (cent.sum(1, keepdims=True) + 1e-6); lum = cent.mean(1)
parent = list(range(K))
for b in np.argsort(count):
    best = None
    for a in np.argsort(-count):
        if a == b or count[a] <= count[b] or parent[a] != a: continue
        r = min(lum[a], lum[b]) / max(lum[a], lum[b])
        if np.abs(chrom[a] - chrom[b]).sum() < CFG.get("chroma_tol", 0.07) and r > CFG.get("lum_ratio", 0.72):
            best = a; break
    if best is not None: parent[b] = best
def root_of(k):
    while parent[k] != k: k = parent[k]
    return k
remap = np.array([root_of(k) for k in range(K)])
lab = remap[lab]
# optional colour overrides: snap a cluster to a game palette colour
pal = cent.copy()
for k in range(K):
    if CFG.get("snap"):
        cands = np.array([hexc(h) for h in CFG["snap"]])
        dd = np.abs(cands - pal[k]).sum(1)
        if dd.min() < CFG.get("snap_tol", 0.35): pal[k] = cands[dd.argmin()]
for fr, to in CFG.get("remap", []):   # explicit recolour: cluster nearest `fr` becomes `to`
    used = np.unique(lab); k = used[np.abs(cent[used] - hexc(fr)).sum(1).argmin()]; pal[k] = hexc(to)
# mode filter on the label map (removes speckles); box sums via cumsum
L = lab.reshape(H, W); r = CFG.get("mode_r", 4)
best = np.full((H, W), -1.0, np.float32); out_l = L.copy()
for k in np.unique(L):
    m = (L == k).astype(np.float32)
    c = np.pad(m, ((r + 1, r), (r + 1, r)), mode='edge').cumsum(0).cumsum(1)
    s = c[2 * r + 1:, 2 * r + 1:] - c[:-2 * r - 1, 2 * r + 1:] - c[2 * r + 1:, :-2 * r - 1] + c[:-2 * r - 1, :-2 * r - 1]
    upd = s > best; best[upd] = s[upd]; out_l[upd] = k
flatpx = pal[out_l.ravel()]
print("CLUSTERS", [("#%02x%02x%02x" % tuple((pal[k] * 255).astype(int)), int((out_l == k).sum())) for k in np.unique(out_l)])
flat = bpy.data.images.new("flat", W, H, alpha=False)
flat.pixels.foreach_set(np.concatenate([flatpx, np.ones((len(flatpx), 1), np.float32)], 1).ravel())
flat.pack()

# ---------- 2. toon material ----------
nt.nodes.clear()
o = nt.nodes.new('ShaderNodeOutputMaterial')
t = nt.nodes.new('ShaderNodeTexImage'); t.image = flat; t.interpolation = 'Closest'
dif = nt.nodes.new('ShaderNodeBsdfDiffuse'); s2r = nt.nodes.new('ShaderNodeShaderToRGB')
ramp = nt.nodes.new('ShaderNodeValToRGB'); ramp.color_ramp.interpolation = 'CONSTANT'
sh = CFG.get("shade", 0.86)
ramp.color_ramp.elements[0].color = (sh, sh * 0.96, min(1, sh * 1.06), 1)
ramp.color_ramp.elements[1].position = CFG.get("band", 0.10); ramp.color_ramp.elements[1].color = (1, 1, 1, 1)
mul = nt.nodes.new('ShaderNodeMix'); mul.data_type = 'RGBA'; mul.blend_type = 'MULTIPLY'; mul.inputs['Factor'].default_value = 1
em = nt.nodes.new('ShaderNodeEmission')
nt.links.new(dif.outputs[0], s2r.inputs[0]); nt.links.new(s2r.outputs['Color'], ramp.inputs['Fac'])
nt.links.new(t.outputs['Color'], mul.inputs['A']); nt.links.new(ramp.outputs['Color'], mul.inputs['B'])
nt.links.new(mul.outputs['Result'], em.inputs['Color']); nt.links.new(em.outputs[0], o.inputs['Surface'])
ink = bpy.data.materials.new("ink"); ink.use_nodes = True; ink.use_backface_culling = True
n2 = ink.node_tree; n2.nodes.clear()
e2 = n2.nodes.new('ShaderNodeEmission'); e2.inputs['Color'].default_value = (*lin(INK), 1)
o2 = n2.nodes.new('ShaderNodeOutputMaterial'); n2.links.new(e2.outputs[0], o2.inputs['Surface'])
obj.data.materials.append(ink)
if CFG.get("hull", 0) > 0:
    sol = obj.modifiers.new("outline", 'SOLIDIFY')
    sol.thickness = CFG["hull"]; sol.offset = 1; sol.use_flip_normals = True; sol.material_offset = 1; sol.use_rim = False
for p in obj.data.polygons: p.use_smooth = True

for eng in ('BLENDER_EEVEE', 'BLENDER_EEVEE_NEXT'):
    try: sc.render.engine = eng; break
    except TypeError: pass
sc.render.film_transparent = True; sc.view_settings.view_transform = 'Standard'; sc.view_settings.look = 'None'
sc.render.resolution_x = sc.render.resolution_y = RES
sc.render.image_settings.file_format = 'PNG'; sc.render.image_settings.color_mode = 'RGBA'
try: sc.eevee.taa_render_samples = 16
except Exception: pass
sun = bpy.data.lights.new("sun", 'SUN'); sun.energy = 3
so = bpy.data.objects.new("sun", sun); sc.collection.objects.link(so)
so.rotation_euler = (math.radians(55), 0, math.radians(CFG.get("sun_z", -35)))
cam = bpy.data.cameras.new("cam"); cam.type = 'ORTHO'; cam.ortho_scale = CFG.get("ortho", 1.6)
co = bpy.data.objects.new("cam", cam); sc.collection.objects.link(co); sc.camera = co
def place_cam(elev):
    el = math.radians(elev); d = 5
    co.location = (0, -d * math.cos(el), CFG.get("cam_z", 0.5) + d * math.sin(el))
    co.rotation_euler = (math.pi / 2 - el, 0, 0)
def pose(p):
    turn.rotation_euler = (math.radians(p.get("rx", 0)), 0, math.radians(p.get("rz", CFG.get("rz", 0))))
    roll.rotation_euler = (0, math.radians(p.get("ry", 0)), 0)
    roll.scale = (p.get("sx", CFG.get("sx", 1)), p.get("sx", CFG.get("sx", 1)), p.get("sz", CFG.get("sz", 1)))
    roll.location = (0, 0, p.get("lz", 0))
    place_cam(p.get("elev", CFG.get("elev", 12)))
    bpy.context.view_layer.update()

# ---------- 3a. face anchors: raycast from the reference view ----------
pose({"rz": CFG.get("ref_rz", 0), "elev": CFG.get("ref_elev", CFG.get("elev", 12)), "sx": CFG.get("ref_sx", CFG.get("sx", 1))})
dg = bpy.context.evaluated_depsgraph_get()
def ray_at(pxy, ref_res):
    u = pxy[0] / ref_res - 0.5; v = 0.5 - pxy[1] / ref_res
    m = co.matrix_world; right = m.to_3x3() @ Vector((1, 0, 0)); up = m.to_3x3() @ Vector((0, 1, 0)); fwd = m.to_3x3() @ Vector((0, 0, -1))
    org = m.translation + right * u * cam.ortho_scale + up * v * cam.ortho_scale
    hit, loc, nrm, idx, ob, _ = sc.ray_cast(dg, org, fwd)
    if not hit: raise RuntimeError("anchor ray missed at %s" % (pxy,))
    return obj.matrix_world.inverted() @ loc
anchors = {k: ray_at(v, CFG["ref_res"]) for k, v in CFG.get("face", {}).items()}
print("ANCHORS", {k: tuple(round(x, 3) for x in v) for k, v in anchors.items()})

# ---------- image helpers (top-down float RGBA arrays, sRGB) ----------
def load(path):
    im = bpy.data.images.load(path); w, h = im.size
    a = np.empty(w * h * 4, np.float32); im.pixels.foreach_get(a); bpy.data.images.remove(im)
    return a.reshape(h, w, 4)[::-1].copy()
def save(a, path):
    h, w = a.shape[:2]; im = bpy.data.images.new("o", w, h, alpha=True)
    im.pixels.foreach_set(np.ascontiguousarray(a[::-1]).ravel()); im.filepath_raw = path; im.file_format = 'PNG'; im.save()
    bpy.data.images.remove(im)
def resize(a, w, h):
    # premultiplied area-ish resize through Blender's scaler
    pm = a.copy(); pm[..., :3] *= pm[..., 3:4]
    im = bpy.data.images.new("r", a.shape[1], a.shape[0], alpha=True, float_buffer=True)
    im.pixels.foreach_set(np.ascontiguousarray(pm[::-1]).ravel()); im.scale(w, h)
    b = np.empty(w * h * 4, np.float32); im.pixels.foreach_get(b); bpy.data.images.remove(im)
    b = b.reshape(h, w, 4)[::-1].copy(); al = b[..., 3:4]
    b[..., :3] = np.where(al > 1e-4, b[..., :3] / np.maximum(al, 1e-4), 0); return np.clip(b, 0, 1)
def over(top, bot):
    a = top[..., 3:4] + bot[..., 3:4] * (1 - top[..., 3:4])
    c = (top[..., :3] * top[..., 3:4] + bot[..., :3] * bot[..., 3:4] * (1 - top[..., 3:4])) / np.maximum(a, 1e-5)
    return np.concatenate([c, a], -1)
YY, XX = np.mgrid[0:RES, 0:RES].astype(np.float32)
def seg_dist(p, q):
    px_, py_ = XX - p[0], YY - p[1]; dx, dy = q[0] - p[0], q[1] - p[1]
    tt = np.clip((px_ * dx + py_ * dy) / max(dx * dx + dy * dy, 1e-6), 0, 1)
    return np.hypot(px_ - tt * dx, py_ - tt * dy)
def stroke(a, pts, wid, col, alpha=1.0):
    d = np.full((RES, RES), 1e9, np.float32)
    for i in range(len(pts) - 1): d = np.minimum(d, seg_dist(pts[i], pts[i + 1]))
    cov = np.clip(wid / 2 - d + 0.5, 0, 1)[..., None] * alpha
    a[..., :3] = a[..., :3] * (1 - cov) + col * cov
def disc(a, c, rx, col, ry=None, alpha=1.0):
    ry = ry or rx
    d = (np.hypot((XX - c[0]) / rx, (YY - c[1]) / ry) - 1) * min(rx, ry)
    cov = np.clip(0.5 - d, 0, 1)[..., None] * alpha
    a[..., :3] = a[..., :3] * (1 - cov) + col * cov
def proj(v):
    p = world_to_camera_view(sc, co, obj.matrix_world @ v); return np.array([p.x * RES, (1 - p.y) * RES])

def draw_face(a, kind):
    if "eye_l" not in anchors: return
    el = proj(anchors["eye_l"])
    if "eye_r" in anchors:
        er = proj(anchors["eye_r"]); u = np.linalg.norm(er - el) * CFG.get("eye_r", 0.16)
        eyes = ((el, 1), (er, -1)); mid = (el + er) / 2; sp = mid
    else:  # profile: one eye, size from the distance to another anchor
        er = None; u = np.linalg.norm(el - proj(anchors[CFG["u_from"]])) * CFG.get("eye_r", 0.16)
        eyes = ((el, 1),); mid = el; sp = el + np.array(CFG.get("skin_at", [0, 2.2])) * u
    # cover the model's own eyes with the face colour sampled nearby
    skin = a[int(sp[1]), int(sp[0]), :3].copy()
    for e, _ in eyes: disc(a, e, u * CFG.get("cover", 1.5), skin)
    if "cheek" in CFG:
        ck = hexc(CFG["cheek"])
        for e, sgn in ((el, -1), (er, 1)):
            disc(a, e + np.array([sgn * u * 1.1, u * 2.0]), u * 1.15, ck, u * 0.7, alpha=0.7)
    w = u * 0.85
    if "stripes" in CFG:
        sc_ = hexc(CFG["stripes"]); top = mid - np.array([0, u * CFG.get("stripe_up", 3.4)])
        for i in (-1, 0, 1):
            b = top + np.array([i * u * 0.95, 0]); stroke(a, [b, b + np.array([i * u * 0.15, u * 1.5])], u * 0.62, sc_)
    for e, sgn in eyes:
        if kind == "dot": disc(a, e, u, INK)
        elif kind == "blink": stroke(a, [e + (-1.2 * u, 0), e + (1.2 * u, 0)], w, INK)
        elif kind == "hurt": stroke(a, [e + (-sgn * u, -u), e + (sgn * u, 0), e + (-sgn * u, u)], w, INK)
        elif kind == "ko":
            stroke(a, [e + (-u, -u), e + (u, u)], w, INK); stroke(a, [e + (-u, u), e + (u, -u)], w, INK)
        elif kind == "angry":
            disc(a, e, u, INK); stroke(a, [e + (-sgn * 1.6 * u, -2.1 * u), e + (sgn * 1.2 * u, -1.2 * u)], w * 1.1, INK)
    if "mouth" in anchors and CFG.get("mouth", "w") == "w":
        m = proj(anchors["mouth"]); rr = u * 0.62; pts = []
        for cx in (-rr, rr):
            pts += [m + (cx + rr * math.cos(th), rr * 0.9 * math.sin(th)) for th in np.linspace(math.pi, 0, 9)]
        stroke(a, pts, w * 0.85, INK)

def outline(a, thick):
    m = a[..., 3].copy()
    for i in range(int(thick)):
        n = m.copy()
        n[1:] = np.maximum(n[1:], m[:-1]); n[:-1] = np.maximum(n[:-1], m[1:])
        n[:, 1:] = np.maximum(n[:, 1:], m[:, :-1]); n[:, :-1] = np.maximum(n[:, :-1], m[:, 1:])
        if i % 2:
            n[1:, 1:] = np.maximum(n[1:, 1:], m[:-1, :-1]); n[:-1, :-1] = np.maximum(n[:-1, :-1], m[1:, 1:])
            n[1:, :-1] = np.maximum(n[1:, :-1], m[:-1, 1:]); n[:-1, 1:] = np.maximum(n[:-1, 1:], m[1:, :-1])
        m = n
    base = np.zeros_like(a); base[..., :3] = INK; base[..., 3] = m
    return over(a, base)

def old_box(old):
    al = old[..., 3]; ys, xs = np.where(al > 0.85)
    box = (xs.min(), ys.min(), xs.max() + 1, ys.max() + 1)
    sh = (al > 0.08) & (al < 0.7) & (np.arange(old.shape[0])[:, None] > box[1] + (box[3] - box[1]) * 0.75)
    return box, sh

# ---------- 4. frames ----------
for f in CFG["frames"]:
    pose(f)
    raw = os.path.join(OUT, "_raw_" + f["name"] + ".png"); sc.render.filepath = raw
    bpy.ops.render.render(write_still=True)
    a = load(raw)
    draw_face(a, f.get("face", "dot"))
    if CFG.get("flip"): a = a[:, ::-1].copy()
    ys, xs = np.where(a[..., 3] > 0.02); hgt = ys.max() - ys.min()
    a = outline(a, max(2, round(hgt * CFG.get("line", 0.035))))
    ys, xs = np.where(a[..., 3] > 0.02); a = a[ys.min():ys.max() + 1, xs.min():xs.max() + 1]
    old = load(CFG["old_dir"] + "/" + f.get("old", f["name"]) + ".png")
    box, shmask = old_box(old)
    bw, bh = box[2] - box[0], box[3] - box[1]
    s = min(bh * f.get("fit", CFG.get("fit", 1.0)) / a.shape[0], bw * CFG.get("wfit", 1.15) / a.shape[1])
    nw, nh = max(1, round(a.shape[1] * s)), max(1, round(a.shape[0] * s))
    a = resize(a, nw, nh)
    canvas = np.zeros_like(old)
    cx = (box[0] + box[2]) / 2 + f.get("dx", 0) * bw; by = box[3] - f.get("lift", 0) * bh
    # flat ground shadow (same place as the old sprite's)
    oh, ow = canvas.shape[:2]; gy, gx = np.mgrid[0:oh, 0:ow].astype(np.float32)
    sx_, sy_ = bw * CFG.get("shadow_w", 0.42), bw * CFG.get("shadow_h", 0.075)
    dd = (np.hypot((gx - (box[0] + box[2]) / 2) / sx_, (gy - (box[3] - sy_ * 0.6)) / sy_) - 1) * sy_
    canvas[..., :3] = INK; canvas[..., 3] = np.clip(0.5 - dd, 0, 1) * CFG.get("shadow_a", 0.2)
    x0, y0 = int(round(cx - nw / 2)), int(round(by - nh))
    X0, Y0 = max(x0, 0), max(y0, 0); X1, Y1 = min(x0 + nw, canvas.shape[1]), min(y0 + nh, canvas.shape[0])
    canvas[Y0:Y1, X0:X1] = over(a[Y0 - y0:Y1 - y0, X0 - x0:X1 - x0], canvas[Y0:Y1, X0:X1])
    save(canvas, os.path.join(OUT, f["name"] + ".png"))
    print("SPRITE", f["name"], canvas.shape[1], canvas.shape[0])
