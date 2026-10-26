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
    public partial class Enemy : MonoBehaviour, IDamageable
    {
        [SerializeField] private int maxHp = 30;
        [SerializeField] private Collider mainCollider;

        public Collider MainCollider => mainCollider;
        public GameObject GameObject => gameObject;
        public Health Health { get; private set; }

        // 한 번이라도 맞았으면 감지 거리와 상관없이 플레이어를 쫓습니다.
        public bool IsAggro { get; private set; }

        private StateMachine<EnemyState> stateMachine;

        #region 유니티 함수들

        private void Awake()
        {
            Health = new Health(maxHp);
            InitializeAI();

            stateMachine = new StateMachine<EnemyState>(new EnemyState[]
            {
                new EnemyIdleState(this),
                new EnemyChaseState(this),
                new EnemyAttackState(this),
                new EnemyHitState(this),
                new EnemyDieState(this),
            });
        }

        private void Start()
        {
            CombatSystem.Instance.RegisterMonster(this);
            stateMachine.ChangeState<EnemyIdleState>();
        }

        private void Update()
        {
            stateMachine.Tick(Time.deltaTime);
        }

        private void OnDestroy()
        {
            if (CombatSystem.Instance != null) CombatSystem.Instance.UnregisterMonster(mainCollider);
        }

        #endregion

        #region 상태 전환

        public void ChangeState<T>() where T : EnemyState
        {
            stateMachine.ChangeState<T>();
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
            if (CombatSystem.Instance != null) CombatSystem.Instance.UnregisterMonster(mainCollider);
        }

        // 사망 애니메이션이 끝나면 호출됩니다.
        // TODO: 오브젝트 풀이 들어오면 Destroy 대신 gameObject.SetActive(false)로 반납합니다.
        public void Despawn()
        {
            Destroy(gameObject);
        }

        #endregion
    }
}
