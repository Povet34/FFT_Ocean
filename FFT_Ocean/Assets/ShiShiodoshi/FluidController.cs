using UnityEngine;

public class FluidController : MonoBehaviour
{
    public ComputeShader fluidShader;
    public Mesh cubeMesh;
    public Material cubeMaterial;
    public int particleCount = 10000;
    public float spawnInterval = 0.1f; // ���� ���� ����

    ComputeBuffer particleBuffer;
    FluidParticle[] particles;
    Matrix4x4[] matrices;
    const int INSTANCE_LIMIT = 1023; // �� ���� ������ ������ �ִ� �ν��Ͻ� ��

    float spawnTimer = 0f; // ���� ���� Ÿ�̸�
    int nextParticleIndex = 0; // ���� Ȱ��ȭ�� ������ �ε���

    public struct FluidParticle
    {
        public Vector3 position;
        public Vector3 velocity;
        public float mass;
        public float lifetime; // ���� �߰�
        public int isActive; // Ȱ��ȭ ���� (0: ��Ȱ��, 1: Ȱ��)
    }

    void Start()
    {
        particles = new FluidParticle[particleCount];
        matrices = new Matrix4x4[particleCount];

        Vector3 startPosition = new Vector3(0f, 2f, 0f); // ������ ����

        for (int i = 0; i < particleCount; i++)
        {
            particles[i] = new FluidParticle
            {
                position = startPosition,
                velocity = Vector3.zero, // �ʱ� �ӵ��� 0
                mass = 1f,
                lifetime = 0f, // �ʱ� ������ 0
                isActive = 0 // ��Ȱ�� ���·� ����
            };
        }

        particleBuffer = new ComputeBuffer(particleCount, System.Runtime.InteropServices.Marshal.SizeOf(typeof(FluidParticle)));
        particleBuffer.SetData(particles);
    }

    void Update()
    {
        // ���� ���� Ÿ�̸� ������Ʈ
        spawnTimer += Time.deltaTime;
        if (spawnTimer >= spawnInterval && nextParticleIndex < particleCount)
        {
            // ���ο� ���� Ȱ��ȭ
            particles[nextParticleIndex].position = new Vector3(0f, 2f, 0f); // ������
            particles[nextParticleIndex].velocity = new Vector3(Random.Range(1f, 1.5f), Random.Range(-0.5f, 0.5f), Random.Range(-0.5f, 0.5f)); // �ʱ� �ӵ�
            particles[nextParticleIndex].lifetime = 2f; // ���� ����
            particles[nextParticleIndex].isActive = 1; // Ȱ��ȭ
            nextParticleIndex++;
            spawnTimer = 0f; // Ÿ�̸� �ʱ�ȭ
        }

        // ComputeShader�� ������ ����
        int kernel = fluidShader.FindKernel("CSMain");
        fluidShader.SetFloat("deltaTime", Time.deltaTime);
        fluidShader.SetBuffer(kernel, "particles", particleBuffer);
        particleBuffer.SetData(particles); // CPU �����͸� GPU�� ����
        fluidShader.Dispatch(kernel, Mathf.CeilToInt((float)particleCount / 64), 1, 1);

        // ComputeShader���� ������Ʈ�� �����͸� ������
        particleBuffer.GetData(particles);

        // ��ġ�� Matrix4x4�� ��ȯ
        for (int i = 0; i < particleCount; i++)
        {
            if (particles[i].isActive == 1 && particles[i].lifetime > 0) // Ȱ��ȭ�� ���ڸ� ������
            {
                matrices[i] = Matrix4x4.TRS(particles[i].position, Quaternion.identity, Vector3.one * 0.1f);
            }
        }

        // 1023���� ������ DrawMeshInstanced ȣ��
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
