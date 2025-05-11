using UnityEngine;

public class FluidController : MonoBehaviour
{
    public ComputeShader fluidShader;
    public Mesh cubeMesh;
    public Material cubeMaterial;
    public int particleCount = 10000;

    ComputeBuffer particleBuffer;
    FluidParticle[] particles;
    Matrix4x4[] matrices;
    const int INSTANCE_LIMIT = 1023; // 한 번에 렌더링 가능한 최대 인스턴스 수

    public struct FluidParticle
    {
        public Vector2 position;
        public Vector2 velocity;
        public float mass;
    }

    void Start()
    {
        particles = new FluidParticle[particleCount];
        matrices = new Matrix4x4[particleCount];

        for (int i = 0; i < particleCount; i++)
        {
            particles[i] = new FluidParticle
            {
                position = new Vector2(Random.Range(-5f, 5f), Random.Range(-5f, 5f)),
                velocity = Vector2.zero,
                mass = 1f
            };
        }

        particleBuffer = new ComputeBuffer(particleCount, System.Runtime.InteropServices.Marshal.SizeOf(typeof(FluidParticle)));
        particleBuffer.SetData(particles);
    }

    void Update()
    {
        int kernel = fluidShader.FindKernel("CSMain");
        fluidShader.SetFloat("deltaTime", Time.deltaTime);
        fluidShader.SetBuffer(kernel, "particles", particleBuffer);
        fluidShader.Dispatch(kernel, particleCount / 64, 1, 1);

        particleBuffer.GetData(particles);

        // 위치를 Matrix4x4로 변환
        for (int i = 0; i < particleCount; i++)
        {
            Vector3 pos = new Vector3(particles[i].position.x, particles[i].position.y, 0f);
            matrices[i] = Matrix4x4.TRS(pos, Quaternion.identity, Vector3.one * 0.1f);
        }

        // 1023개씩 나눠서 DrawMeshInstanced 호출
        for (int i = 0; i < particleCount; i += INSTANCE_LIMIT)
        {
            int batchCount = Mathf.Min(INSTANCE_LIMIT, particleCount - i);
            Graphics.DrawMeshInstanced(cubeMesh, 0, cubeMaterial, matrices, batchCount, null, UnityEngine.Rendering.ShadowCastingMode.Off);
        }
    }

    void OnDestroy()
    {
        particleBuffer?.Release();
    }
}
