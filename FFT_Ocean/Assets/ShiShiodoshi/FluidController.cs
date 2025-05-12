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
        public float lifetime; // 수명 추가
    }

    void Start()
    {
        particles = new FluidParticle[particleCount];
        matrices = new Matrix4x4[particleCount];

        Vector2 startPosition = new Vector2(0f, 2f); // 시작점 고정

        for (int i = 0; i < particleCount; i++)
        {
            particles[i] = new FluidParticle
            {
                position = startPosition,
                velocity = new Vector2(Random.Range(-1f, 1f), Random.Range(2f, 5f)), // 초기 속도를 위쪽 방향으로 설정
                mass = 1f,
                lifetime = 5f // 초기 수명 설정
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
        fluidShader.Dispatch(kernel, Mathf.CeilToInt((float)particleCount / 64), 1, 1); // 스레드 그룹 계산

        // ComputeShader에서 업데이트된 데이터를 가져옴
        particleBuffer.GetData(particles);

        // 위치를 Matrix4x4로 변환
        for (int i = 0; i < particleCount; i++)
        {
            if (particles[i].lifetime > 0) // 수명이 남아있는 입자만 렌더링
            {
                Vector3 pos = new Vector3(particles[i].position.x, particles[i].position.y, 0f);
                matrices[i] = Matrix4x4.TRS(pos, Quaternion.identity, Vector3.one * 0.1f);
            }
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