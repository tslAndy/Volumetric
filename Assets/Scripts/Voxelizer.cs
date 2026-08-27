using UnityEngine;

public class Voxelizer
{
    private int boxResolution; // in voxels
    private float boxSize; // in world coords

    private Vector3[] verts;
    private int[] trigs;
    private Bounds bounds;

    private const float EPS = 0.001f;

    public Voxelizer(int boxResolution, float boxSize, GameObject model)
    {
        this.boxResolution = boxResolution;
        this.boxSize = boxSize;

        this.bounds = model.GetComponent<MeshRenderer>().bounds;

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
        for (int y = 0; y < boxResolution; y++)
        {
            for (int x = 0; x < boxResolution; x++)
            {
                for (int z = 0; z < boxResolution; z++)
                {
                    Vector3Int cpos = new Vector3Int(x, y, z);
                    if (!IsVoxelOccupied(cpos))
                        continue;

                    int index = GetMorton(cpos);
                    int div = index >> 5;
                    int rem = index & 31;
                    voxels[div] |= 1 << rem;
                }
            }
        }
        return voxels;
    }

    private bool IsVoxelOccupied(Vector3Int cpos)
    {
        Vector3 worldPos = (Vector3.one * 0.5f + (Vector3)cpos) / boxResolution * boxSize;

        if (!bounds.Contains(worldPos))
            return false;

        Vector3 dir = Vector3.up;

        int count = 0;
        for (int trigId = 0; trigId < trigs.Length; trigId += 3)
        {
            int indA = trigs[trigId];
            int indB = trigs[trigId + 1];
            int indC = trigs[trigId + 2];

            Vector3 a = verts[indA];
            Vector3 b = verts[indB];
            Vector3 c = verts[indC];

            Vector3 min = Vector3.Min(Vector3.Min(a, b), c);
            Vector3 max = Vector3.Max(Vector3.Max(a, b), c);

            if (
                worldPos.x < min.x
                || worldPos.x > max.x
                || worldPos.z < min.z
                || worldPos.z > max.z
            )
                continue;

            Vector3 surfVec = Vector3.Cross(b - a, c - b);

            float div = Vector3.Dot(dir, surfVec);
            if (Mathf.Abs(div) < EPS)
                continue;

            float t = Vector3.Dot(a - worldPos, surfVec) / div;
            if (t <= EPS)
                continue;

            Vector3 onTrig = worldPos + dir * t;
            float uvA = Vector3.Dot(Vector3.Cross(onTrig - b, a - onTrig), surfVec);
            float uvB = Vector3.Dot(Vector3.Cross(onTrig - a, c - onTrig), surfVec);
            float uvC = Vector3.Dot(Vector3.Cross(onTrig - c, b - onTrig), surfVec);

            if (uvA < 0.0 || uvB < 0.0 || uvC < 0.0)
                continue;

            count++;
        }

        return count % 2 == 1;
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
}
