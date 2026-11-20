using System;
using Jay.FSM;
using Study3D.FpsGame.Drop;
using Study3D.FpsGame.Enemy.States;
using UnityEngine;

namespace Study3D.FpsGame.Enemy
{
    // Enemy
    // : 카드에 맞을 수 있는 가장 기본 적 입니다.
    // - CombatSystem에 자기 Collider를 등록해야 SendDamage가 이 적을 찾을 수 있습니다.
    // - 행동은 FSM으로 처리합니다. (Idle → Chase → Attack, 어느 상태든 피격 시 Hit, HP 0이면 Die)
    //   프리팹에 EnemySkill 컴포넌트가 붙어 있으면 추격 중에 쓸 수 있는 스킬을 골라 Skill 상태로 넘어갑니다.
    // - 이동, 거리 계산, 애니메이션 재생 같은 상태들이 쓰는 기능은 Enemy.AI.cs에 있습니다.
    // - 오브젝트 풀에서 재사용되므로, 켜질 때마다(OnEnable) 체력과 상태를 처음으로 되돌립니다.
    //   (풀이 없으면 Destroy로 사라집니다)
    public partial class Enemy : MonoBehaviour, IDamageable, IObjectPoolItem
    {
        [SerializeField] private int maxHp = 30;
        [SerializeField] private Collider mainCollider;

        [Header("드랍 - 카드")]
        [Tooltip("카드 보상의 선택지를 정하는 표 (비워두면 떨어뜨리지 않음)")]
        [SerializeField] private CardDropTable dropTable;
        [Range(0f, 1f)]
        [SerializeField] private float cardDropChance = 1f;

        [Header("드랍 - 골드")]
        [Tooltip("흩뿌릴 골드 프리팹. 개당 값과 흩뿌림·흡수 값은 프리팹이 정합니다 (비워두면 떨어뜨리지 않음)")]
        [SerializeField] private CurrencyPickup goldPrefab;
        [Range(0f, 1f)]
        [SerializeField] private float goldDropChance = 0.5f;
        [SerializeField] private int goldMinCount = 1; // 당첨되면 이 범위에서 랜덤 개수
        [SerializeField] private int goldMaxCount = 5;

        [Header("소리")]
        [Tooltip("맞았을 때 이 중 하나를 무작위로 재생합니다 (비워두면 소리 없음)")]
        [SerializeField] private AudioManager.Sfx[] hitSounds;
        [Tooltip("죽을 때 이 중 하나를 무작위로 재생합니다 (비워두면 소리 없음)")]
        [SerializeField] private AudioManager.Sfx[] dieSounds;

        public Collider MainCollider => mainCollider;
        public GameObject GameObject => gameObject;
        public Health Health { get; private set; }

        // 풀에서 꺼낸 적은 풀이 Key를 넣어줍니다. 씬에 직접 둔 적은 null 입니다.
        public string Key { get; set; }

        // 한 번이라도 맞았으면 감지 거리와 상관없이 플레이어를 쫓습니다.
        public bool IsAggro { get; private set; }

        // 사망 애니메이션이 끝나 사라지기 직전에 호출됩니다. (방 스포너가 남은 적 수를 셀 때 사용)
        public event Action<Enemy> Despawned;

        // 지금 사용 중인 스킬. 스킬 상태에서만 값이 있습니다.
        public EnemySkill CurrentSkill { get; private set; }

        private StateMachine<EnemyState> stateMachine;
        private EnemySkill[] skills;
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
                new EnemySkillState(this),
            });

            // 이 프리팹에 붙어 있는 스킬만 사용합니다. (없으면 빈 배열 → 지금처럼 접근 → 공격만 함)
            skills = GetComponents<EnemySkill>();
        }

        private void OnEnable()
        {
            Health = new Health(maxHp);
            IsAggro = false;
            mainCollider.enabled = true;
            needsStart = true;
            Register();

            // 정지 중에 풀에서 꺼낸 적도 멈춘 채로 시작합니다.
            GameTime.PausedChanged += ApplyGamePause;
            ApplyGamePause(GameTime.IsPaused);
        }

        // 씬에 처음부터 있던 적은 OnEnable 때 CombatSystem이 아직 없을 수 있어서 한 번 더 시도합니다.
        private void Start()
        {
            Register();
        }

        private void Update()
        {
            // 게임이 멈춰 있으면 상태도 진행하지 않습니다. (애니메이터는 ApplyGamePause 가 멈춤)
            if (GameTime.IsPaused)
                return;

            if (needsStart)
            {
                needsStart = false;
                StartFirstState();
            }

            stateMachine.Tick(GameTime.DeltaTime);
        }

        // 등장 지점을 받았으면 등장 상태로, 아니면 대기 상태로 시작합니다.
        private void StartFirstState()
        {
            if (hasEntrance)
            {
                stateMachine.ChangeState<EnemyEnterState>();
            }
            else
            {
                stateMachine.ChangeState<EnemyIdleState>();
            }
        }

        private void OnDisable()
        {
            hasEntrance = false;
            Unregister();
            GameTime.PausedChanged -= ApplyGamePause;
        }

        #endregion

        #region 상태 전환

        public void ChangeState<T>() where T : EnemyState
        {
            stateMachine.ChangeState<T>();
        }

        #endregion

        #region 스킬

        // 위에서부터(컴포넌트 순서) 지금 쓸 수 있는 스킬을 하나 골라 CurrentSkill 로 정합니다. 골랐으면 true
        public bool TryPickSkill()
        {
            foreach (EnemySkill skill in skills)
            {
                if (skill.CanUse(this))
                {
                    CurrentSkill = skill;
                    return true;
                }
            }
            return false;
        }

        // 스킬 상태가 끝날 때 부릅니다.
        public void ClearSkill()
        {
            CurrentSkill = null;
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
            if (hasEntrance == false)
                return true;

            return MoveTowards(entranceTarget, deltaTime);
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

            if (Health.IsDead)
            {
                // 킬을 알립니다. 이미 죽은 적은 맨 위에서 걸러지므로 적 하나당 한 번만 알립니다.
                CombatSystem.Instance.ReportKill(combatEvent.AttackId);
                PlayRandomSound(dieSounds);
                stateMachine.ChangeState<EnemyDieState>();
                return;
            }

            // 죽는 공격에는 피격 소리 없이 사망 소리만 냅니다.
            PlayRandomSound(hitSounds);

            // 등장 중, 경직을 무시하는 스킬 사용 중에는 피격 모션 없이 하던 행동을 계속합니다. (EnemyState.IgnoreHitReaction)
            EnemyState currentState = stateMachine.CurrentState;
            if (currentState != null && currentState.IgnoreHitReaction)
                return;

            // 피격 중에 또 맞으면 피격 모션을 처음부터 다시 재생합니다.
            if (stateMachine.CurrentState is EnemyHitState hitState) hitState.Enter();
            else stateMachine.ChangeState<EnemyHitState>();
        }

        public void TakeHeal(HealEvent healEvent)
        {
            Health.Heal(healEvent.Heal);
        }

        // 죽는 순간 호출됩니다. 더 이상 카드에 맞지 않게 하고, 카드 보상과 골드를 각자 확률로 떨어뜨립니다.
        // (칼 판정은 Die 애니메이션으로 넘어가면 애니메이션이 꺼줍니다)
        // - 카드 보상은 발밑에 1개, 골드는 몸 중앙에서 흩뿌립니다.
        // - 꺼진 Collider는 bounds가 비어 있으므로 몸 중앙은 끄기 전에 읽어 둡니다.
        public void OnDeath()
        {
            Vector3 bodyCenter = mainCollider.bounds.center;
            mainCollider.enabled = false;
            Unregister();
            DropCard();
            DropGold(bodyCenter);
        }

        private void DropCard()
        {
            if (dropTable == null || Roll(cardDropChance) == false)
                return;

            dropTable.Drop(transform.position);
        }

        // 당첨되면 개수 범위에서 랜덤 개수를 몸 중앙에서 흩뿌립니다.
        private void DropGold(Vector3 bodyCenter)
        {
            if (goldPrefab == null || Roll(goldDropChance) == false)
                return;

            int count = UnityEngine.Random.Range(goldMinCount, Mathf.Max(goldMinCount, goldMaxCount) + 1);
            CurrencyPickup.Scatter(goldPrefab, count, bodyCenter, transform.position.y);
        }

        private static bool Roll(float chance)
        {
            return chance > 0f && UnityEngine.Random.value <= chance;
        }

        // sounds 중 하나를 골라 몸 중앙에서 3D로 재생합니다. (멀수록 작게, 방향 구분)
        private void PlayRandomSound(AudioManager.Sfx[] sounds)
        {
            if (sounds == null || sounds.Length == 0)
                return;

            if (AudioManager.Instance == null)
                return;

            int index = UnityEngine.Random.Range(0, sounds.Length);
            AudioManager.Instance.PlaySfxAt(sounds[index], mainCollider.bounds.center);
        }

        // 사망 애니메이션이 끝나면 호출됩니다.
        public void Despawn()
        {
            if (Despawned != null)
                Despawned.Invoke(this);
            ReturnToPool();
        }

        // 풀에서 꺼낸 적은 풀로 돌려보내고, 씬에 직접 둔 적은 Destroy 합니다.
        public void ReturnToPool()
        {
            if (Key != null && ObjectPoolManager.Instance != null)
            {
                ObjectPoolManager.Instance.ReturnToPool(this);
            }
            else
            {
                Destroy(gameObject);
            }
        }

        #endregion

        private void Register()
        {
            if (isRegistered || CombatSystem.Instance == null)
                return;

            CombatSystem.Instance.RegisterMonster(this);
            isRegistered = true;
        }

        private void Unregister()
        {
            if (isRegistered == false)
                return;

            if (CombatSystem.Instance != null)
                CombatSystem.Instance.UnregisterMonster(mainCollider);
            isRegistered = false;
        }
    }
}
