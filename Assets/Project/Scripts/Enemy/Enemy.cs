using System;
using Jay.FSM;
using Study3D.FpsGame.Enemy.States;
using UnityEngine;

namespace Study3D.FpsGame.Enemy
{
    // Enemy
    // : 카드에 맞을 수 있는 가장 기본 적 입니다.
    // - CombatSystem에 자기 Collider를 등록해야 SendDamage가 이 적을 찾을 수 있습니다.
    // - 행동은 FSM으로 처리합니다. (Idle → Chase → Attack, 어느 상태든 피격 시 Hit, HP 0이면 Die)
    // - 이동, 거리 계산, 애니메이션 재생 같은 상태들이 쓰는 기능은 Enemy.AI.cs에 있습니다.
    // - 오브젝트 풀에서 재사용되므로, 켜질 때마다(OnEnable) 체력과 상태를 처음으로 되돌립니다.
    //   (풀이 없으면 Destroy로 사라집니다)
    public partial class Enemy : MonoBehaviour, IDamageable, IObjectPoolItem
    {
        [SerializeField] private int maxHp = 30;
        [SerializeField] private Collider mainCollider;

        public Collider MainCollider => mainCollider;
        public GameObject GameObject => gameObject;
        public Health Health { get; private set; }

        // 풀에서 꺼낸 적은 풀이 Key를 넣어줍니다. 씬에 직접 둔 적은 null 입니다.
        public string Key { get; set; }

        // 한 번이라도 맞았으면 감지 거리와 상관없이 플레이어를 쫓습니다.
        public bool IsAggro { get; private set; }

        // 사망 애니메이션이 끝나 사라지기 직전에 호출됩니다. (방 스포너가 남은 적 수를 셀 때 사용)
        public event Action<Enemy> Despawned;

        private StateMachine<EnemyState> stateMachine;
        private bool isRegistered;
        private bool needsStart; // 켜진 뒤 첫 Update에서 첫 상태로 시작 (OnEnable에서는 애니메이터가 아직 준비되지 않았을 수 있음)

        // 등장 도착 지점. 방 스포너가 켜기 전에 정해주면 등장 상태로 시작합니다.
        private bool hasEntrance;
        private Vector3 entranceTarget;

        #region 유니티 함수들

        private void Awake()
        {
            InitializeAI();

            stateMachine = new StateMachine<EnemyState>(new EnemyState[]
            {
                new EnemyIdleState(this),
                new EnemyEnterState(this),
                new EnemyChaseState(this),
                new EnemyAttackState(this),
                new EnemyHitState(this),
                new EnemyDieState(this),
            });
        }

        private void OnEnable()
        {
            Health = new Health(maxHp);
            IsAggro = false;
            mainCollider.enabled = true;
            needsStart = true;
            Register();
        }

        // 씬에 처음부터 있던 적은 OnEnable 때 CombatSystem이 아직 없을 수 있어서 한 번 더 시도합니다.
        private void Start()
        {
            Register();
        }

        private void Update()
        {
            if (needsStart)
            {
                needsStart = false;
                if (hasEntrance) stateMachine.ChangeState<EnemyEnterState>();
                else stateMachine.ChangeState<EnemyIdleState>();
            }

            stateMachine.Tick(Time.deltaTime);
        }

        private void OnDisable()
        {
            hasEntrance = false;
            Unregister();
        }

        #endregion

        #region 상태 전환

        public void ChangeState<T>() where T : EnemyState
        {
            stateMachine.ChangeState<T>();
        }

        #endregion

        #region 등장

        // 켜기 전에 호출하면 이 지점까지 걸어 들어온 뒤 추격합니다.
        public void SetEntrance(Vector3 target)
        {
            hasEntrance = true;
            entranceTarget = target;
        }

        // 도착 지점으로 한 걸음 이동합니다. 도착하면 true
        public bool MoveToEntrance(float deltaTime)
        {
            return hasEntrance == false || MoveTowards(entranceTarget, deltaTime);
        }

        // 등장이 끝나면 감지 거리와 상관없이 플레이어를 쫓습니다.
        public void FinishEntrance()
        {
            hasEntrance = false;
            IsAggro = true;
        }

        #endregion

        #region 게임 이벤트 처리

        public void TakeDamage(CombatEvent combatEvent)
        {
            if (Health.IsDead) return; // 이미 죽는 중이면 무시

            Health.TakeDamage(combatEvent.Damage);
            IsAggro = true;
            Debug.Log($"[Enemy] {name} 피격 -{combatEvent.Damage} (HP {Health.HP}/{Health.MaxHP})");

            if (Health.IsDead)
            {
                stateMachine.ChangeState<EnemyDieState>();
                return;
            }

            // 등장 중에는 피격 모션 없이 계속 걸어옵니다. (맵 밖에서 멈추면 길찾기 없이 쫓아오게 됨)
            if (stateMachine.CurrentState is EnemyEnterState) return;

            // 피격 중에 또 맞으면 피격 모션을 처음부터 다시 재생합니다.
            if (stateMachine.CurrentState is EnemyHitState hitState) hitState.Enter();
            else stateMachine.ChangeState<EnemyHitState>();
        }

        public void TakeHeal(HealEvent healEvent)
        {
            Health.Heal(healEvent.Heal);
        }

        // 죽는 순간 호출됩니다. 더 이상 카드에 맞지 않게 합니다.
        // (칼 판정은 Die 애니메이션으로 넘어가면 애니메이션이 꺼줍니다)
        public void OnDeath()
        {
            mainCollider.enabled = false;
            Unregister();
        }

        // 사망 애니메이션이 끝나면 호출됩니다.
        public void Despawn()
        {
            Despawned?.Invoke(this);
            ReturnToPool();
        }

        // 풀에서 꺼낸 적은 풀로 돌려보내고, 씬에 직접 둔 적은 Destroy 합니다.
        public void ReturnToPool()
        {
            if (Key != null && ObjectPoolManager.Instance != null) ObjectPoolManager.Instance.ReturnToPool(this);
            else Destroy(gameObject);
        }

        #endregion

        private void Register()
        {
            if (isRegistered || CombatSystem.Instance == null) return;
            CombatSystem.Instance.RegisterMonster(this);
            isRegistered = true;
        }

        private void Unregister()
        {
            if (isRegistered == false) return;
            if (CombatSystem.Instance != null) CombatSystem.Instance.UnregisterMonster(mainCollider);
            isRegistered = false;
        }
    }
}
