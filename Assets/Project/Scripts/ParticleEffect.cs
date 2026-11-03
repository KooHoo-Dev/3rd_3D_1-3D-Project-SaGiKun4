using System;
using System.Security;
using UnityEngine;

namespace Study3D.FpsGame
{
    public class ParticleEffect : MonoBehaviour, IObjectPoolItem
    {
        [SerializeField] private ParticleSystem particle;

        public string Key { get; set; }
        public GameObject GameObject => gameObject;

        private void Awake()
        {
            // EffectSpawner가 런타임에 붙인 경우처럼 Inspector에서 연결하지 않았으면, 가장 위의 파티클을 찾아 씁니다.
            if (particle == null)
                particle = GetComponentInChildren<ParticleSystem>();
        }

        private void Update()
        {
            // 게임이 멈춰 있으면 파티클도 멈춥니다. (자식 파티클 포함)
            // 이벤트 대신 매 프레임 확인하는 이유: 켜지는 순간 playOnAwake 로 재생이 시작되므로, 정지 중에 꺼낸 이펙트도 여기서 확실히 멈춥니다.
            if (GameTime.IsPaused)
            {
                if (particle.isPaused == false)
                    particle.Pause(true);
                return;
            }

            if (particle.isPaused)
                particle.Play(true);

            // 파티클이 집(ObjectPool)으로 돌아가는 시기는
            // 재생이 끝났을때 입니다.
            // 자식 파티클까지 모두 끝나야 끝난 것으로 봅니다. (여러 파티클로 이루어진 이펙트가 중간에 잘리지 않도록)
            if (particle.IsAlive(true) == false)
                ReturnToPool();
        }

        // 파티클의 Stop Action이 Disable이면 끝나는 순간 유니티가 먼저 꺼 버려서 Update가 돌지 않습니다.
        // 스스로 꺼진 경우(activeSelf == false) 풀로 돌아가게 합니다. (이미 풀에 있으면 풀이 무시합니다)
        // 씬이 끝나며 파괴되거나 부모가 꺼진 경우(activeSelf는 그대로 true)는 반환하지 않습니다.
        private void OnDisable()
        {
            if (gameObject.activeSelf)
                return;

            ReturnToPool();
        }

        // 인터페이스에 ReturnToPool이 있는 이유는
        // 솔직히 까먹지말라고 정의해 놓은겁니다.
        // 씬이 끝나면서 풀 매니저가 먼저 사라졌거나, 풀 없이 만든 이펙트(Key 없음)는 반환하지 않습니다.
        public void ReturnToPool()
        {
            if (Key == null || ObjectPoolManager.Instance == null)
                return;

            ObjectPoolManager.Instance.ReturnToPool(this);
        }
        
        // particle을 제어하는 함수들.
        private void StudyParticle()
        {
            // ParticleSystem이 부착되어있는 GameObject가 활성화 되면
            // 바로 재생을 하는지 여부 입니다.
            bool playOnAwake = particle.main.playOnAwake;
            
            // 재생이 종료되면, 다시 시작할지 여부 입니다.
            // 켜져있으면 반복 재생 됩니다.
            bool isLoop = particle.main.loop;
            
            particle.Play(); // 파티클을 재생시키는 함수입니다.
            
            // 파티클이 현재 재생중인지 알 수 있는 프로퍼티 입니다.
            // 보통 isPlaying이 false가 되면 뭔가를 처리하는게 많습니다.
            bool isPlaying = particle.isPlaying;
           
            particle.Pause(); // 파티클 재생을 일시 정지할때 사용합니다.
            
            // 파티클이 현재 일시정지중인지 알 수 있는 프로퍼티 입니다.
            bool isPaused = particle.isPaused;
            
            particle.Stop(); // 파티클 재생을 멈추게 하는 함수 입니다.
        }

    }
}