using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// For breakable objects whose model is made of several overlapping parts (e.g. the item chest: body, lid, bands,
/// lock merged into one mesh). OpenFracture can only slice a closed, single-piece mesh reliably; slicing
/// overlapping parts can hang or crash. Right before the object breaks, its mesh is swapped for a plain box of the
/// same size, and that box is what gets fractured. The box keeps up to two materials: sides + bottom use the
/// first, the top uses the second (like the chest body and lid).
/// </summary>
[RequireComponent(typeof(Fracture))]
public class FractureProxyMesh : MonoBehaviour
{
    private static readonly Dictionary<(Vector3, Vector3, int), Mesh> Cache = new Dictionary<(Vector3, Vector3, int), Mesh>();

    private void Awake()
    {
        Fracture fracture = GetComponent<Fracture>();
        if (fracture != null && fracture.callbackOptions != null && fracture.callbackOptions.onFracture != null)
        {
            // onFracture runs just before the mesh is sliced
            fracture.callbackOptions.onFracture.AddListener((_, __, ___) => SwapToBox());
        }
    }

    private void SwapToBox()
    {
        MeshFilter filter = GetComponent<MeshFilter>();
        if (filter == null || filter.sharedMesh == null) { return; }

        Mesh source = filter.sharedMesh;
        int surfaces = Mathf.Clamp(source.subMeshCount, 1, 2);
        Bounds b = source.bounds;
        var key = (b.center, b.size, surfaces);
        if (!Cache.TryGetValue(key, out Mesh box) || box == null)
        {
            box = BuildBox(b, surfaces);
            Cache[key] = box;
        }
        filter.sharedMesh = box;
    }

    private static Mesh BuildBox(Bounds b, int surfaces)
    {
        Vector3 lo = b.min, hi = b.max;
        var verts = new List<Vector3>(24);
        var normals = new List<Vector3>(24);
        var uvs = new List<Vector2>(24);
        var topTris = new List<int>(6);
        var otherTris = new List<int>(30);

        // Each face: outward normal + its 4 corners in order around the face
        void Face(Vector3 normal, Vector3 c0, Vector3 c1, Vector3 c2, Vector3 c3, bool isTop)
        {
            int s = verts.Count;
            verts.Add(c0); verts.Add(c1); verts.Add(c2); verts.Add(c3);
            uvs.Add(new Vector2(0, 0)); uvs.Add(new Vector2(1, 0)); uvs.Add(new Vector2(1, 1)); uvs.Add(new Vector2(0, 1));
            for (int i = 0; i < 4; i++) { normals.Add(normal); }

            // Unity draws a triangle's front when cross(b - a, c - a) points outward
            bool outward = Vector3.Dot(Vector3.Cross(c1 - c0, c2 - c0), normal) > 0f;
            List<int> list = surfaces == 2 && isTop ? topTris : otherTris;
            if (outward) { list.AddRange(new[] { s, s + 1, s + 2, s, s + 2, s + 3 }); }
            else { list.AddRange(new[] { s, s + 2, s + 1, s, s + 3, s + 2 }); }
        }

        Face(Vector3.up, new Vector3(lo.x, hi.y, lo.z), new Vector3(hi.x, hi.y, lo.z), new Vector3(hi.x, hi.y, hi.z), new Vector3(lo.x, hi.y, hi.z), true);
        Face(Vector3.down, new Vector3(lo.x, lo.y, lo.z), new Vector3(hi.x, lo.y, lo.z), new Vector3(hi.x, lo.y, hi.z), new Vector3(lo.x, lo.y, hi.z), false);
        Face(Vector3.forward, new Vector3(lo.x, lo.y, hi.z), new Vector3(hi.x, lo.y, hi.z), new Vector3(hi.x, hi.y, hi.z), new Vector3(lo.x, hi.y, hi.z), false);
        Face(Vector3.back, new Vector3(lo.x, lo.y, lo.z), new Vector3(hi.x, lo.y, lo.z), new Vector3(hi.x, hi.y, lo.z), new Vector3(lo.x, hi.y, lo.z), false);
        Face(Vector3.right, new Vector3(hi.x, lo.y, lo.z), new Vector3(hi.x, lo.y, hi.z), new Vector3(hi.x, hi.y, hi.z), new Vector3(hi.x, hi.y, lo.z), false);
        Face(Vector3.left, new Vector3(lo.x, lo.y, lo.z), new Vector3(lo.x, lo.y, hi.z), new Vector3(lo.x, hi.y, hi.z), new Vector3(lo.x, hi.y, lo.z), false);

        var mesh = new Mesh { name = "Fracture Box" };
        mesh.SetVertices(verts);
        mesh.SetNormals(normals);
        mesh.SetUVs(0, uvs);
        mesh.subMeshCount = surfaces;
        if (surfaces == 2)
        {
            mesh.SetTriangles(otherTris, 0);
            mesh.SetTriangles(topTris, 1);
        }
        else
        {
            otherTris.AddRange(topTris);
            mesh.SetTriangles(otherTris, 0);
        }
        mesh.RecalculateBounds();
        mesh.RecalculateTangents();
        return mesh;
    }
}
