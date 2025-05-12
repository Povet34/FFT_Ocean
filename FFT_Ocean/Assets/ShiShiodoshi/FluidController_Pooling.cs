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
    public float spawnInterval = 0.1f; // 입자 생성 간격
    public int poolSize = 5000; // 오브젝트 풀 크기

    ComputeBuffer particleBuffer;
    List<FluidParticle> activeParticles = new List<FluidParticle>(); // 활성화된 입자 리스트
    Queue<FluidParticle> particlePool = new Queue<FluidParticle>(); // 비활성화된 입자 풀
    Matrix4x4[] matrices;
    const int INSTANCE_LIMIT = 1023; // 한 번에 렌더링 가능한 최대 인스턴스 수

    float spawnTimer = 0f; // 입자 생성 타이머

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
        matrices = new Matrix4x4[INSTANCE_LIMIT]; // 렌더링에 필요한 매트릭스 배열 초기화

        // 오브젝트 풀 초기화
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
        // 입자 생성 타이머 업데이트
        spawnTimer += Time.deltaTime;
        if (spawnTimer >= spawnInterval && particlePool.Count > 0)
        {
            // 풀에서 입자를 가져와 활성화
            FluidParticle particle = particlePool.Dequeue();
            particle.position = new Vector3(0, 2f, 0); // 시작점
            particle.velocity = new Vector3(
                UnityEngine.Random.Range(1f, 1.5f),
                UnityEngine.Random.Range(-0.5f, 0.5f),
                UnityEngine.Random.Range(-0.5f, 0.5f)
            ); // 초기 속도
            particle.mass = 1f;
            particle.lifetime = 2f; // 수명 설정
            particle.isActive = 1; // 활성화
            activeParticles.Add(particle); // 활성화된 입자 리스트에 추가
            spawnTimer = 0f; // 타이머 초기화
        }

        // 활성화된 입자 업데이트 및 비활성화 처리
        for (int i = activeParticles.Count - 1; i >= 0; i--)
        {
            FluidParticle particle = activeParticles[i];

            // 땅에 부딪히는 조건
            if (particle.position.y <= 0.1f)
            {
                // 크기가 충분히 큰 경우에만 분열
                if (particle.mass > 0.25f)
                {
                    for (int j = 0; j < 4; j++)
                    {
                        if (particlePool.Count > 0)
                        {
                            FluidParticle newParticle = particlePool.Dequeue();
                            newParticle.position = particle.position; // 원본 위치
                            newParticle.velocity = Quaternion.Euler(0, j * 90, 0) * particle.velocity * UnityEngine.Random.Range(0.25f, 0.7f); // 네 방향으로 퍼짐
                            newParticle.mass = particle.mass * 0.25f; // 질량의 1/4
                            newParticle.lifetime = UnityEngine.Random.Range(0.5f, 1.1f); // 동일한 수명
                            newParticle.isActive = 1; // 활성화
                            activeParticles.Add(newParticle);
                        }
                    }
                }
            }

            // 수명이 다한 입자는 비활성화
            if (particle.lifetime <= 0)
            {
                particle.isActive = 0;
                activeParticles.RemoveAt(i);
                particlePool.Enqueue(particle); // 풀에 반환
                continue;
            }

            // 수명 감소
            particle.lifetime -= Time.deltaTime;
            activeParticles[i] = particle;
        }

        // ComputeBuffer 업데이트
        particleBuffer.SetData(activeParticles.ToArray());

        // ComputeShader 실행
        int kernel = fluidShader.FindKernel("CSSetGravity");
        fluidShader.SetFloat("deltaTime", Time.deltaTime);
        fluidShader.SetBuffer(kernel, "particles", particleBuffer);

        if (activeParticles.Count > 0)
            fluidShader.Dispatch(kernel, Mathf.CeilToInt((float)activeParticles.Count / 64), 1, 1);

        Graphics.ExecuteCommandBuffer(new CommandBuffer());

        // ComputeShader에서 업데이트된 데이터를 가져옴
        FluidParticle[] updatedParticles = new FluidParticle[activeParticles.Count];
        particleBuffer.GetData(updatedParticles);

        // activeParticles 리스트 업데이트
        for (int i = 0; i < activeParticles.Count; i++)
        {
            activeParticles[i] = updatedParticles[i];
        }

        // 위치를 Matrix4x4로 변환
        for (int i = 0; i < activeParticles.Count && i < INSTANCE_LIMIT; i++)
        {
            matrices[i] = Matrix4x4.TRS(activeParticles[i].position, Quaternion.identity, Vector3.one * (activeParticles[i].mass * 0.25f));
        }

        // 1023개씩 나눠서 DrawMeshInstanced 호출
        for (int i = 0; i < activeParticles.Count; i += INSTANCE_LIMIT)
        {
            int batchCount = Mathf.Min(INSTANCE_LIMIT, activeParticles.Count - i);
            Graphics.DrawMeshInstanced(waterMesh, 0, waterMat, matrices, batchCount, null, UnityEngine.Rendering.ShadowCastingMode.Off);
        }
    }
}
