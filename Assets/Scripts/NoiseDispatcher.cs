using UnityEngine;
using UnityEngine.Rendering;

public class NoiseDispatcher : MonoBehaviour
{
    [Header("Values")]
    public float boxSize;
    public int boxResolution;
    public Vector3 noiseVel;

    [Range(0.01f, 10.0f)]
    public float perlinSize,
        worleySize;

    [Range(0.0f, 1.0f)]
    public float worleyPerlinLerp;

    [Header("Other")]
    public GameObject model;
    public ComputeShader shader;

    private ComputeBuffer voxelsBuffer;
    private int kernelId;

    [HideInInspector]
    public RenderTexture noise;

    void Start()
    {
        Voxelizer voxelizer = new Voxelizer(boxResolution, boxSize, model);
        int[] voxels = voxelizer.GetVoxels();
        voxelsBuffer = new ComputeBuffer(voxels.Length, sizeof(int));
        voxelsBuffer.SetData(voxels);

        noise = new RenderTexture(boxResolution, boxResolution, 0)
        {
            dimension = TextureDimension.Tex3D,
            volumeDepth = boxResolution,
            format = RenderTextureFormat.RFloat,
            enableRandomWrite = true,
        };
        noise.Create();

        kernelId = shader.FindKernel("CSMain");
        shader.SetBuffer(kernelId, "voxels", voxelsBuffer);
        shader.SetTexture(kernelId, "noise", noise);
        shader.SetInt("resolution", boxResolution);
    }

    void Update()
    {
        // can be moved to start if not changed during runtime
        shader.SetFloat("invPerlinFreq", 1.0f / perlinSize);
        shader.SetFloat("invWorleyFreq", 1.0f / worleySize);
        shader.SetFloat("worleyPerlinLerp", worleyPerlinLerp);
        shader.SetVector("noiseVel", noiseVel);

        shader.SetFloat("time", Time.time);
        shader.Dispatch(kernelId, boxResolution / 8, boxResolution / 8, boxResolution / 1);
    }

    void OnDrawGizmos()
    {
        Gizmos.DrawWireCube(0.5f * boxSize * Vector3.one, Vector3.one * boxSize);
    }

    void OnDisable()
    {
        voxelsBuffer.Dispose();
    }
}
