// Place in an "Editor" folder.
// Any model whose file name ends with "_anime" (e.g. Hero_anime.fbx) gets
// angle-weighted smoothed normals baked into UV2 (TEXCOORD2) in TANGENT SPACE.
// Tangent space is used so the data deforms correctly with skinning, because
// Unity only skins position/normal/tangent, not custom UV channels.
// The outline pass in AnimeCharacterToon.shader reconstructs the object-space
// smooth normal from the (skinned) tangent frame.
//
// In the model import settings set: Normals = Import, Tangents = Calculate Mikktspace.

using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

public class AnimeSmoothNormalBaker : AssetPostprocessor
{
    const string Suffix = "_anime";

    void OnPostprocessModel(GameObject root)
    {
        string name = Path.GetFileNameWithoutExtension(assetPath);
        if (!name.EndsWith(Suffix, StringComparison.OrdinalIgnoreCase)) return;

        foreach (var mf in root.GetComponentsInChildren<MeshFilter>())
            if (mf.sharedMesh != null) Bake(mf.sharedMesh);

        foreach (var sk in root.GetComponentsInChildren<SkinnedMeshRenderer>())
            if (sk.sharedMesh != null) Bake(sk.sharedMesh);
    }

    static Vector3Int Key(Vector3 v)
    {
        return new Vector3Int(
            Mathf.RoundToInt(v.x * 10000f),
            Mathf.RoundToInt(v.y * 10000f),
            Mathf.RoundToInt(v.z * 10000f));
    }

    static void Bake(Mesh mesh)
    {
        Vector3[] verts = mesh.vertices;
        Vector3[] normals = mesh.normals;
        Vector4[] tangents = mesh.tangents;

        if (normals.Length != verts.Length) { mesh.RecalculateNormals(); normals = mesh.normals; }
        if (tangents.Length != verts.Length) { mesh.RecalculateTangents(); tangents = mesh.tangents; }

        // accumulate angle weighted face normals per unique position
        var sums = new Dictionary<Vector3Int, Vector3>();

        void Add(int idx, Vector3 faceNormal, float angle)
        {
            var k = Key(verts[idx]);
            sums.TryGetValue(k, out Vector3 cur);
            sums[k] = cur + faceNormal * angle;
        }

        for (int s = 0; s < mesh.subMeshCount; s++)
        {
            int[] tris = mesh.GetTriangles(s);
            for (int i = 0; i + 2 < tris.Length; i += 3)
            {
                int a = tris[i], b = tris[i + 1], c = tris[i + 2];
                Vector3 pa = verts[a], pb = verts[b], pc = verts[c];
                Vector3 fn = Vector3.Cross(pb - pa, pc - pa);
                if (fn.sqrMagnitude < 1e-12f) continue;
                fn.Normalize();

                Add(a, fn, Vector3.Angle(pb - pa, pc - pa) * Mathf.Deg2Rad);
                Add(b, fn, Vector3.Angle(pa - pb, pc - pb) * Mathf.Deg2Rad);
                Add(c, fn, Vector3.Angle(pa - pc, pb - pc) * Mathf.Deg2Rad);
            }
        }

        var smoothTS = new List<Vector3>(verts.Length);
        for (int i = 0; i < verts.Length; i++)
        {
            Vector3 n = normals[i];
            Vector3 smooth = n;
            if (sums.TryGetValue(Key(verts[i]), out Vector3 sum) && sum.sqrMagnitude > 1e-12f)
                smooth = sum.normalized;

            Vector3 t = tangents[i];
            Vector3 b = Vector3.Cross(n, t) * tangents[i].w;
            smoothTS.Add(new Vector3(Vector3.Dot(smooth, t), Vector3.Dot(smooth, b), Vector3.Dot(smooth, n)));
        }

        mesh.SetUVs(2, smoothTS);
    }
}
