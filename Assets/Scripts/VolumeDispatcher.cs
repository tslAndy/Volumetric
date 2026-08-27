using System.Linq;
using System.Runtime.InteropServices;
using UnityEngine;

public class Dispatcher : MonoBehaviour
{
    [Header("Values")]
    public float step;

    [Range(0.0f, 1.0f)]
    public float schlick_k;

    [Range(0.0f, 10.0f)]
    public float scatter;

    [Header("Other")]
    public Vector2Int resolution;
    public ComputeShader shader;
    public Camera cam;
    public Material blitMat;
    public NoiseDispatcher noiseDispatcher;

    private RenderTexture texture;
    private int kernelId;

    private ComputeBuffer lightsBuffer;

    // Start is called before the first frame update
    void Start()
    {
        kernelId = shader.FindKernel("CSMain");

        texture = new RenderTexture(resolution.x, resolution.y, 0) { enableRandomWrite = true };
        texture.Create();

        shader.SetTexture(kernelId, "result", texture);
        shader.SetTexture(kernelId, "noise", noiseDispatcher.noise);

        LightData[] lightData = FindObjectsByType<Light>(FindObjectsSortMode.None)
            .Where(x => x.type == LightType.Point)
            .Select(x => new LightData(
                x.transform.position,
                new Vector3(x.color.r, x.color.g, x.color.b),
                x.intensity
            ))
            .ToArray();

        lightsBuffer = new ComputeBuffer(lightData.Length, Marshal.SizeOf<LightData>());
        lightsBuffer.SetData(lightData);
        shader.SetBuffer(kernelId, "lights", lightsBuffer);

        Matrix4x4 clip = new Matrix4x4
        {
            m00 = 2.0f / resolution.x,
            m11 = 2.0f / resolution.y,
            m03 = -1.0f,
            m13 = -1.0f,
            m22 = 1.0f,
            m33 = 1.0f,
        };

        Matrix4x4 mat = cam.cameraToWorldMatrix * cam.projectionMatrix.inverse * clip;
        shader.SetMatrix("cameraMatrix", mat);
        shader.SetVector("cameraPosition", cam.transform.position);
        shader.SetInts("resolution", resolution.x, resolution.y);

        shader.SetFloat("boxSize", noiseDispatcher.boxSize);
        shader.SetInt("boxResolution", noiseDispatcher.boxResolution);

        shader.SetFloat("step", step);
    }

    // Update is called once per frame
    void Update()
    {
        // can be moved to start, was moved here for setup simplicity
        shader.SetFloat("schlick_k", schlick_k);
        shader.SetFloat("scatter", scatter);

        shader.Dispatch(kernelId, resolution.x / 8, resolution.y / 8, 1);
    }

    void OnRenderImage(RenderTexture _, RenderTexture destination)
    {
        Graphics.Blit(texture, destination, blitMat);
    }

    void OnDisable()
    {
        lightsBuffer.Dispose();
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct LightData
    {
        public Vector3 pos;
        public Vector3 color;
        public float intensity;

        public LightData(Vector3 pos, Vector3 color, float intensity)
        {
            this.pos = pos;
            this.color = color;
            this.intensity = intensity;
        }
    }
}
