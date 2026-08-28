using UnityEngine;

public class Voxelizer
{
    private int boxResolution; // in voxels
    private float boxSize; // in world coords

    private Vector3[] verts;
    private int[] trigs;

    private const float EPS = 0.001f;

    public Voxelizer(int boxResolution, float boxSize, GameObject model)
    {
        this.boxResolution = boxResolution;
        this.boxSize = boxSize;

        Mesh mesh = model.GetComponent<MeshFilter>().sharedMesh;
        this.verts = mesh.vertices;
        this.trigs = mesh.triangles;

        Matrix4x4 localToWorld = model.transform.localToWorldMatrix;
        for (int i = 0; i < verts.Length; i++)
            verts[i] = localToWorld.MultiplyPoint(verts[i]);
    }

    public int[] GetVoxels()
    {
        int[] voxels = new int[boxResolution * boxResolution * boxResolution / 32];
        Norm[] flags = new Norm[boxResolution * boxResolution * boxResolution];

        float worldToBox = 1.0f / boxSize * boxResolution;
        float boxToWorld = 1.0f / boxResolution * boxSize;

        float rad = 1.73205f * 0.5f * boxToWorld;

        for (int trigId = 0; trigId < trigs.Length; trigId += 3)
        {
            int indA = trigs[trigId];
            int indB = trigs[trigId + 1];
            int indC = trigs[trigId + 2];

            Vector3 a = verts[indA];
            Vector3 b = verts[indB];
            Vector3 c = verts[indC];

            Vector3 norm = Vector3.Normalize(Vector3.Cross(b - a, c - b));
            Norm flag = Vector3.Dot(norm, Vector3.up) > 0.0f ? Norm.Up : Norm.Down;

            Vector3 na = Vector3.Normalize(Vector3.Cross(b - a, norm));
            Vector3 nb = Vector3.Normalize(Vector3.Cross(c - b, norm));
            Vector3 nc = Vector3.Normalize(Vector3.Cross(a - c, norm));

            Vector3Int cmin = Vector3Int.FloorToInt(worldToBox * Vector3.Min(Vector3.Min(a, b), c));
            Vector3Int cmax = Vector3Int.CeilToInt(worldToBox * Vector3.Max(Vector3.Max(a, b), c));

            cmin = Vector3Int.Max(cmin, Vector3Int.zero);
            cmax = Vector3Int.Min(cmax, Vector3Int.one * boxResolution);

            for (int y = cmin.y; y < cmax.y; y++)
            {
                for (int x = cmin.x; x < cmax.x; x++)
                {
                    for (int z = cmin.z; z < cmax.z; z++)
                    {
                        int index = GetMorton(new Vector3Int(x, y, z));

                        Vector3 center = new Vector3(x + 0.5f, y + 0.5f, z + 0.5f) * boxToWorld;
                        Vector3 onTrig = center - norm * Vector3.Dot(center - a, norm);

                        // point too far from trig plane
                        if ((center - onTrig).sqrMagnitude > rad * rad)
                            continue;

                        float sideA = Vector3.Dot(onTrig - a, na);
                        float sideB = Vector3.Dot(onTrig - b, nb);
                        float sideC = Vector3.Dot(onTrig - c, nc);
                        // point inside of triangle
                        if (sideA <= 0.0f && sideB <= 0.0f && sideC <= 0.0f)
                        {
                            voxels[index >> 5] |= 1 << (index & 31);
                            flags[index] |= flag;
                            continue;
                        }

                        // clamp edges

                        Vector3 p1,
                            p2,
                            n;

                        if (sideA >= 0.0f)
                            (p1, p2, n) = (a, b, na);
                        else if (sideB >= 0.0f)
                            (p1, p2, n) = (b, c, nb);
                        else if (sideC >= 0.0f)
                            (p1, p2, n) = (c, a, nc);
                        else
                            continue;

                        onTrig = onTrig - n * Vector3.Dot(onTrig - p1, n);
                        if (Vector3.Dot(onTrig - p1, p2 - p1) < 0.0f)
                            onTrig = p1;
                        else if (Vector3.Dot(p2 - onTrig, p2 - p1) < 0.0f)
                            onTrig = p2;

                        if ((center - onTrig).sqrMagnitude <= rad * rad)
                        {
                            voxels[index >> 5] |= 1 << (index & 31);
                            flags[index] |= flag;
                        }
                    }
                }
            }
        }

        for (int x = 0; x < boxResolution; x++)
        {
            for (int z = 0; z < boxResolution; z++)
            {
                for (int y = 0; y < boxResolution; y++)
                {
                    bool foundDown = false,
                        foundUp = false;

                    for (int ty = y; ty >= 0; ty--)
                    {
                        Norm flag = flags[GetMorton(new Vector3Int(x, ty, z))];
                        if ((flag & Norm.Down) != 0)
                        {
                            foundDown = true;
                            break;
                        }
                    }

                    for (int ty = y; ty < boxResolution; ty++)
                    {
                        Norm flag = flags[GetMorton(new Vector3Int(x, ty, z))];
                        if ((flag & Norm.Up) != 0)
                        {
                            foundUp = true;
                            break;
                        }
                    }

                    if (foundUp && foundDown)
                    {
                        int index = GetMorton(new Vector3Int(x, y, z));
                        voxels[index >> 5] |= 1 << (index & 31);
                    }
                }
            }
        }

        return voxels;
    }

    private static int GetMorton(Vector3Int pos)
    {
        return (GetMorton(pos.x) << 2) | (GetMorton(pos.y) << 1) | GetMorton(pos.z);
    }

    private static int GetMorton(int n)
    {
        return (n & 1)
            | ((n & 2) << 2)
            | ((n & 4) << 4)
            | ((n & 8) << 6)
            | ((n & 16) << 8)
            | ((n & 32) << 10)
            | ((n & 64) << 12)
            | ((n & 128) << 14)
            | ((n & 256) << 16)
            | ((n & 512) << 20);
    }

    private enum Norm : byte
    {
        None = 0,
        Up = 1,
        Down = 2,
    }
}
