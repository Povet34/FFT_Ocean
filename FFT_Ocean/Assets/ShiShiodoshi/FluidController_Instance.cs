using UnityEngine;
using System.Collections.Generic;

public class FluidController_Instance : MonoBehaviour
{
    public ComputeShader fluidShader;
    public Mesh waterMesh;
    public Material waterMat;
    public float spawnInterval = 0.1f; // ���� ���� ����

    ComputeBuffer particleBuffer;
    List<FluidParticle> activeParticles = new List<FluidParticle>(); // Ȱ��ȭ�� ���� ����Ʈ
    Matrix4x4[] matrices;
    const int INSTANCE_LIMIT = 1023; // �� ���� ������ ������ �ִ� �ν��Ͻ� ��

    float spawnTimer = 0f; // ���� ���� Ÿ�̸�

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
        matrices = new Matrix4x4[INSTANCE_LIMIT]; // �������� �ʿ��� ��Ʈ���� �迭 �ʱ�ȭ
        particleBuffer = new ComputeBuffer(1024 * 1024, System.Runtime.InteropServices.Marshal.SizeOf(typeof(FluidParticle)));
    }

    void Update()
    {
        // ���� ���� Ÿ�̸� ������Ʈ
        spawnTimer += Time.deltaTime;
        if (spawnTimer >= spawnInterval)
        {
            // ���ο� ���� ����
            FluidParticle particle = new FluidParticle
            {
                position = new Vector3(0, 2f, 0), // ������
                velocity = new Vector3(
                    UnityEngine.Random.Range(1f, 1.5f),
                    UnityEngine.Random.Range(-0.5f, 0.5f),
                    UnityEngine.Random.Range(-0.5f, 0.5f)
                ), // �ʱ� �ӵ�
                mass = 1f,
                lifetime = 2f, // ���� ����
                isActive = 1 // Ȱ��ȭ
            };
            activeParticles.Add(particle); // Ȱ��ȭ�� ���� ����Ʈ�� �߰�
            spawnTimer = 0f; // Ÿ�̸� �ʱ�ȭ
        }

        // Ȱ��ȭ�� ���� ������Ʈ �� ��Ȱ��ȭ ó��
        for (int i = activeParticles.Count - 1; i >= 0; i--)
        {
            FluidParticle particle = activeParticles[i];

            // ���� �ε����� ����
            if (particle.position.y <= 0.1f)
            {
                // ũ�Ⱑ ����� ū ��쿡�� �п�
                if (particle.mass > 0.25f)
                {
                    for (int j = 0; j < 4; j++)
                    {
                        FluidParticle newParticle = new FluidParticle
                        {
                            position = particle.position, // ���� ��ġ
                            velocity = Quaternion.Euler(0, j * 90, 0) * particle.velocity * 0.25f, // �� �������� ����
                            mass = particle.mass * 0.25f, // ������ 1/4
                            lifetime = particle.lifetime, // ������ ����
                            isActive = 1 // Ȱ��ȭ
                        };
                        activeParticles.Add(newParticle);
                    }
                }
                continue;
            }

            // ������ ���� ���ڴ� ����
            if (particle.lifetime <= 0)
            {
                activeParticles.RemoveAt(i);
                continue;
            }

            // ���� ����
            particle.lifetime -= Time.deltaTime;
            activeParticles[i] = particle;
        }

        // ComputeBuffer ������Ʈ
        particleBuffer.SetData(activeParticles.ToArray());

        // ComputeShader ����
        int kernel = fluidShader.FindKernel("CSSetGravity");
        fluidShader.SetFloat("deltaTime", Time.deltaTime);
        fluidShader.SetBuffer(kernel, "particles", particleBuffer);

        if (activeParticles.Count > 0)
            fluidShader.Dispatch(kernel, Mathf.CeilToInt((float)activeParticles.Count / 64), 1, 1);

        // ComputeShader���� ������Ʈ�� �����͸� ������
        FluidParticle[] updatedParticles = new FluidParticle[activeParticles.Count];
        particleBuffer.GetData(updatedParticles);

        // activeParticles ����Ʈ ������Ʈ
        for (int i = 0; i < activeParticles.Count; i++)
        {
            activeParticles[i] = updatedParticles[i];
        }

        // ��ġ�� Matrix4x4�� ��ȯ
        for (int i = 0; i < activeParticles.Count && i < INSTANCE_LIMIT; i++)
        {
            matrices[i] = Matrix4x4.TRS(activeParticles[i].position, Quaternion.identity, Vector3.one * activeParticles[i].mass);
        }

        // 1023���� ������ DrawMeshInstanced ȣ��
        for (int i = 0; i < activeParticles.Count; i += INSTANCE_LIMIT)
        {
            int batchCount = Mathf.Min(INSTANCE_LIMIT, activeParticles.Count - i);
            Graphics.DrawMeshInstanced(waterMesh, 0, waterMat, matrices, batchCount, null, UnityEngine.Rendering.ShadowCastingMode.Off);
        }
    }

    void OnDestroy()
    {
        particleBuffer?.Release();
    }
}