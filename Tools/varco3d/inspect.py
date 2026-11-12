import bpy, sys
argv = sys.argv[sys.argv.index("--") + 1:]
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.gltf(filepath=argv[0])
for o in bpy.data.objects:
    print("OBJ", o.name, o.type, tuple(round(v, 3) for v in o.dimensions), tuple(round(v, 3) for v in o.location),
          len(o.data.polygons) if o.type == 'MESH' else '')
for m in bpy.data.materials:
    nodes = [n.type for n in m.node_tree.nodes] if m.use_nodes else []
    print("MAT", m.name, nodes)
for im in bpy.data.images:
    print("IMG", im.name, im.size[:])
