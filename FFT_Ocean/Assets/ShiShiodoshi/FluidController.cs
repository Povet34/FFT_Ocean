using UnityEngine;

public class FluidController : MonoBehaviour
{
    public ComputeShader fluidShader;
    public Mesh cubeMesh;
    public Material cubeMaterial;
    public int particleCount = 10000;
    public float spawnInterval = 0.1f; // 입자 생성 간격

    ComputeBuffer particleBuffer;
    FluidParticle[] particles;
    Matrix4x4[] matrices;
    const int INSTANCE_LIMIT = 1023; // 한 번에 렌더링 가능한 최대 인스턴스 수

    float spawnTimer = 0f; // 입자 생성 타이머
    int nextParticleIndex = 0; // 다음 활성화할 입자의 인덱스

    public struct FluidParticle
    {
        public Vector3 position;
        public Vector3 velocity;
        public float mass;
        public float lifetime; // 수명 추가
        public int isActive; // 활성화 여부 (0: 비활성, 1: 활성)
    }

    void Start()
    {
        particles = new FluidParticle[particleCount];
        matrices = new Matrix4x4[particleCount];

        Vector3 startPosition = new Vector3(0f, 2f, 0f); // 시작점 고정

        for (int i = 0; i < particleCount; i++)
        {
            particles[i] = new FluidParticle
            {
                position = startPosition,
                velocity = Vector3.zero, // 초기 속도는 0
                mass = 1f,
                lifetime = 0f, // 초기 수명은 0
                isActive = 0 // 비활성 상태로 시작
            };
        }

        particleBuffer = new ComputeBuffer(particleCount, System.Runtime.InteropServices.Marshal.SizeOf(typeof(FluidParticle)));
        particleBuffer.SetData(particles);
    }

    void Update()
    {
        // 입자 생성 타이머 업데이트
        spawnTimer += Time.deltaTime;
        if (spawnTimer >= spawnInterval && nextParticleIndex < particleCount)
        {
            // 새로운 입자 활성화
            particles[nextParticleIndex].position = new Vector3(0f, 2f, 0f); // 시작점
            particles[nextParticleIndex].velocity = new Vector3(Random.Range(1f, 1.5f), Random.Range(-0.5f, 0.5f), Random.Range(-0.5f, 0.5f)); // 초기 속도
            particles[nextParticleIndex].lifetime = 2f; // 수명 설정
            particles[nextParticleIndex].isActive = 1; // 활성화
            nextParticleIndex++;
            spawnTimer = 0f; // 타이머 초기화
        }

        // ComputeShader에 데이터 전달
        int kernel = fluidShader.FindKernel("CSMain");
        fluidShader.SetFloat("deltaTime", Time.deltaTime);
        fluidShader.SetBuffer(kernel, "particles", particleBuffer);
        particleBuffer.SetData(particles); // CPU 데이터를 GPU로 전달
        fluidShader.Dispatch(kernel, Mathf.CeilToInt((float)particleCount / 64), 1, 1);

        // ComputeShader에서 업데이트된 데이터를 가져옴
        particleBuffer.GetData(particles);

        // 위치를 Matrix4x4로 변환
        for (int i = 0; i < particleCount; i++)
        {
            if (particles[i].isActive == 1 && particles[i].lifetime > 0) // 활성화된 입자만 렌더링
            {
                matrices[i] = Matrix4x4.TRS(particles[i].position, Quaternion.identity, Vector3.one * 0.1f);
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
