using UnityEngine;
using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using UnityEngine.Rendering;

public class FluidController_Pooling : MonoBehaviour
{
    public ComputeShader fluidShader;
    public Mesh waterMesh;
    public Material waterMat;
    public float spawnInterval = 0.1f; // ���� ���� ����
    public int poolSize = 5000; // ������Ʈ Ǯ ũ��

    ComputeBuffer particleBuffer;
    List<FluidParticle> activeParticles = new List<FluidParticle>(); // Ȱ��ȭ�� ���� ����Ʈ
    Queue<FluidParticle> particlePool = new Queue<FluidParticle>(); // ��Ȱ��ȭ�� ���� Ǯ
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

        // ������Ʈ Ǯ �ʱ�ȭ
        for (int i = 0; i < poolSize; i++)
        {
            FluidParticle particle = new FluidParticle
            {
                position = Vector3.zero,
                velocity = Vector3.zero,
                mass = 1f,
                lifetime = 0f,
                isActive = 0
            };
            particlePool.Enqueue(particle);
        }

        particleBuffer = new ComputeBuffer(poolSize, System.Runtime.InteropServices.Marshal.SizeOf(typeof(FluidParticle)));
    }

    void Update()
    {
        UpdateParticles();
    }

    void OnDestroy()
    {
        particleBuffer?.Release();
    }

    void UpdateParticles()
    {
        // ���� ���� Ÿ�̸� ������Ʈ
        spawnTimer += Time.deltaTime;
        if (spawnTimer >= spawnInterval && particlePool.Count > 0)
        {
            // Ǯ���� ���ڸ� ������ Ȱ��ȭ
            FluidParticle particle = particlePool.Dequeue();
            particle.position = new Vector3(0, 2f, 0); // ������
            particle.velocity = new Vector3(
                UnityEngine.Random.Range(1f, 1.5f),
                UnityEngine.Random.Range(-0.5f, 0.5f),
                UnityEngine.Random.Range(-0.5f, 0.5f)
            ); // �ʱ� �ӵ�
            particle.mass = 1f;
            particle.lifetime = 2f; // ���� ����
            particle.isActive = 1; // Ȱ��ȭ
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
                        if (particlePool.Count > 0)
                        {
                            FluidParticle newParticle = particlePool.Dequeue();
                            newParticle.position = particle.position; // ���� ��ġ
                            newParticle.velocity = Quaternion.Euler(0, j * 90, 0) * particle.velocity * UnityEngine.Random.Range(0.25f, 0.7f); // �� �������� ����
                            newParticle.mass = particle.mass * 0.25f; // ������ 1/4
                            newParticle.lifetime = UnityEngine.Random.Range(0.5f, 1.1f); // ������ ����
                            newParticle.isActive = 1; // Ȱ��ȭ
                            activeParticles.Add(newParticle);
                        }
                    }
                }
            }

            // ������ ���� ���ڴ� ��Ȱ��ȭ
            if (particle.lifetime <= 0)
            {
                particle.isActive = 0;
                activeParticles.RemoveAt(i);
                particlePool.Enqueue(particle); // Ǯ�� ��ȯ
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

        Graphics.ExecuteCommandBuffer(new CommandBuffer());

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
            matrices[i] = Matrix4x4.TRS(activeParticles[i].position, Quaternion.identity, Vector3.one * (activeParticles[i].mass * 0.25f));
        }

        // 1023���� ������ DrawMeshInstanced ȣ��
        for (int i = 0; i < activeParticles.Count; i += INSTANCE_LIMIT)
        {
            int batchCount = Mathf.Min(INSTANCE_LIMIT, activeParticles.Count - i);
            Graphics.DrawMeshInstanced(waterMesh, 0, waterMat, matrices, batchCount, null, UnityEngine.Rendering.ShadowCastingMode.Off);
        }
    }
}
