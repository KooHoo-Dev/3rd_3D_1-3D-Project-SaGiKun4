using System;
using Jay.FSM;
using Study3D.FpsGame.Boss.States;
using Study3D.FpsGame.Drop;
using UnityEngine;

namespace Study3D.FpsGame.Boss
{
    // Boss
    // : 보스방에 혼자 있는 보스 입니다. (Enemy 와 같은 구조지만, 행동이 많이 달라 상태를 따로 둡니다)
    // - 플레이어가 보스방에 들어오면 보스방(BossRoom)이 소환하고, 바닥에서 올라오며 등장합니다. (Appear)
    // - CombatSystem 에 자기 Collider 를 등록해야 카드가 이 보스를 맞힐 수 있습니다.
    // - 행동은 FSM 으로 처리합니다.
    //   등장 / 대기 → 추격(원거리 사거리 밖) ↔ 공전(사거리 안) → 스킬 / 경직 / 기절 / 사망
    // - 슈퍼아머: 맞아도 경직되지 않습니다. 대신 체력이 경직 기준선(70%, 30%, 10%)을 지날 때마다 한 번씩 경직됩니다.
    //   경직으로 끊긴 스킬은 쿨타임이 돌지 않아서 경직이 끝나면 다시 쓰게 됩니다. (BossSkill)
    // - 받는 피해 배율(DamageTakenRate)과 피해 알림(DamageReceived)은 마법 스킬이 씁니다.
    // - 이동, 거리 계산, 애니메이션 재생 같은 상태들이 쓰는 기능은 Boss.AI.cs 에 있습니다.
    public partial class Boss : MonoBehaviour, IDamageable
    {
        [SerializeField] private int maxHp = 3000;
        [SerializeField] private Collider mainCollider;

        [Header("경직")]
        [Tooltip("체력 비율이 이 값들 이하로 내려갈 때마다 한 번씩 경직됩니다 (큰 값부터)")]
        [SerializeField] private float[] staggerHpRatios = { 0.7f, 0.3f, 0.1f };
        [Tooltip("기절 시간 (초). 마법이 파훼되면 기절합니다")]
        [SerializeField] private float stunTime = 5f;

        [Header("등장")]
        [Tooltip("소환될 때 이만큼 바닥 아래에서 시작해 소환 위치까지 올라옵니다 (몸 높이보다 크게)")]
        [SerializeField] private float appearDepth = 10f;
        [Tooltip("다 올라오는 데 걸리는 시간 (초)")]
        [SerializeField] private float appearTime = 2f;

        [Header("드랍 - 카드")]
        [Tooltip("카드 보상의 선택지를 정하는 표 (비워두면 떨어뜨리지 않음)")]
        [SerializeField] private CardDropTable dropTable;

        [Header("드랍 - 골드")]
        [Tooltip("흩뿌릴 골드 프리팹 (비워두면 떨어뜨리지 않음)")]
        [SerializeField] private CurrencyPickup goldPrefab;
        [SerializeField] private int goldCount = 30;

        [Header("소리")]
        [Tooltip("맞았을 때 이 중 하나를 무작위로 재생합니다 (비워두면 소리 없음)")]
        [SerializeField] private AudioManager.Sfx[] hitSounds;
        [Tooltip("죽을 때 이 중 하나를 무작위로 재생합니다 (비워두면 소리 없음)")]
        [SerializeField] private AudioManager.Sfx[] dieSounds;

        public Collider MainCollider => mainCollider;
        public GameObject GameObject => gameObject;
        public Health Health { get; private set; }

        public float StunTime => stunTime;
        public float AppearTime => appearTime;

        // 등장이 끝났을 때 서 있을 위치 (소환 위치)
        public Vector3 AppearEndPosition { get; private set; }

        // 전투가 시작됐는지. 시작 전에는 대기 상태에서 움직이지 않습니다.
        public bool IsBattleStarted { get; private set; }

        // 받는 피해에 곱하는 배율. 마법 대기 중에는 1/3 로 바뀝니다.
        public float DamageTakenRate { get; set; } = 1f;

        // 지금 사용 중인 스킬. 스킬 상태에서만 값이 있습니다.
        public BossSkill CurrentSkill { get; private set; }

        // 피해를 받을 때마다 배율을 곱하기 전 피해량을 알립니다. (마법이 받은 피해를 기록할 때 사용)
        public event Action<int> DamageReceived;

        // 체력이 바뀔 때 알립니다. (현재 HP, 최대 HP) 보스 HP바가 사용합니다.
        public event Action<int, int> HpChanged;

        // 죽는 순간 알립니다. (보스방이 클리어를 처리할 때 사용)
        public event Action<Boss> Died;

        // 어느 보스든 전투가 시작되면 알립니다. (HUD 의 보스 HP바가 표시할 보스를 받을 때 사용)
        // 보스는 맵 생성 때 만들어지는 보스방 안에 있어서 HUD 가 인스펙터로 연결할 수 없기 때문에 static 입니다.
        public static event Action<Boss> BattleStarted;

        private StateMachine<BossState> stateMachine;
        private BossSkill[] skills;
        private int nextStaggerIndex; // 다음에 확인할 경직 기준선 번호
        private bool isRegistered;
        private bool needsStart; // 켜진 뒤 첫 Update에서 첫 상태로 시작 (OnEnable에서는 애니메이터가 아직 준비되지 않았을 수 있음)
        private bool needsAppear; // 첫 상태를 대기 대신 등장으로 시작 (Appear)

        #region 유니티 함수들

        private void Awake()
        {
            InitializeAI();

            stateMachine = new StateMachine<BossState>(new BossState[]
            {
                new BossIdleState(this),
                new BossAppearState(this),
                new BossChaseState(this),
                new BossOrbitState(this),
                new BossSkillState(this),
                new BossStaggerState(this),
                new BossStunState(this),
                new BossDieState(this),
            });

            // 이 프리팹에 붙어 있는 스킬만 사용합니다. 컴포넌트 순서가 우선순위 입니다.
            skills = GetComponents<BossSkill>();
        }

        private void OnEnable()
        {
            Health = new Health(maxHp);
            IsBattleStarted = false;
            DamageTakenRate = 1f;
            nextStaggerIndex = 0;
            mainCollider.enabled = true;
            needsStart = true;
            needsAppear = false;
            Register();

            GameTime.PausedChanged += ApplyGamePause;
            ApplyGamePause(GameTime.IsPaused);
        }

        // 씬에 처음부터 있던 보스는 OnEnable 때 CombatSystem이 아직 없을 수 있어서 한 번 더 시도합니다.
        private void Start()
        {
            Register();
        }

        private void Update()
        {
            if (GameTime.IsPaused)
                return;

            if (needsStart)
            {
                needsStart = false;
                if (needsAppear)
                {
                    stateMachine.ChangeState<BossAppearState>();
                }
                else
                {
                    stateMachine.ChangeState<BossIdleState>();
                }
            }

            stateMachine.Tick(GameTime.DeltaTime);
        }

        private void OnDisable()
        {
            Unregister();
            GameTime.PausedChanged -= ApplyGamePause;
        }

        #endregion

        #region 상태 전환

        public void ChangeState<T>() where T : BossState
        {
            stateMachine.ChangeState<T>();
        }

        // 지금 위치를 소환 위치로 삼아, 바닥 아래로 내려간 뒤 올라오며 등장합니다. (보스방이 소환 직후 부름)
        // 다 올라오면 전투가 시작됩니다. (BossAppearState)
        public void Appear()
        {
            AppearEndPosition = transform.position;
            transform.position = AppearEndPosition + Vector3.down * appearDepth;
            needsAppear = true;
        }

        // 전투를 시작합니다. (등장이 끝나면 부름. 대기 상태에서 플레이어가 감지 거리에 들어와도 시작됨)
        public void StartBattle()
        {
            if (IsBattleStarted)
                return;

            IsBattleStarted = true;
            PickRandomOrbitSign();

            if (BattleStarted != null)
            {
                BattleStarted.Invoke(this);
            }
        }

        // 기절 상태로 넘어갑니다. (마법이 파훼됐을 때 마법 스킬이 부름)
        public void Stun()
        {
            if (Health.IsDead)
                return;

            stateMachine.ChangeState<BossStunState>();
        }

        // 스킬이나 경직이 끝난 뒤 돌아갈 상태로 넘어갑니다. 거리에 따라 추격 또는 공전입니다.
        public void ReturnToMove()
        {
            if (HasTarget == false)
            {
                stateMachine.ChangeState<BossIdleState>();
                return;
            }

            if (IsInOrbitRange())
            {
                stateMachine.ChangeState<BossOrbitState>();
            }
            else
            {
                stateMachine.ChangeState<BossChaseState>();
            }
        }

        #endregion

        #region 스킬

        // 위에서부터(컴포넌트 순서) 지금 쓸 수 있는 스킬을 하나 골라 스킬 상태로 넘어갑니다. 넘어갔으면 true
        public bool TryUseSkill()
        {
            for (int i = 0; i < skills.Length; i++)
            {
                if (skills[i].CanUse(this))
                {
                    CurrentSkill = skills[i];
                    stateMachine.ChangeState<BossSkillState>();
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

        #region 게임 이벤트 처리

        public void TakeDamage(CombatEvent combatEvent)
        {
            if (Health.IsDead)
                return;

            int rawDamage = combatEvent.Damage;
            if (DamageReceived != null)
            {
                DamageReceived.Invoke(rawDamage);
            }

            int damage = Mathf.Max(1, Mathf.RoundToInt(rawDamage * DamageTakenRate));
            Health.TakeDamage(damage);
            NotifyHpChanged();
            StartBattle();

            if (Health.IsDead)
            {
                CombatSystem.Instance.ReportKill(combatEvent.AttackId);
                PlayRandomSound(dieSounds);
                stateMachine.ChangeState<BossDieState>();
                return;
            }

            PlayRandomSound(hitSounds);

            // 슈퍼아머: 경직 기준선을 지났을 때만 경직됩니다.
            if (PassStaggerLine() == false)
                return;

            BossState currentState = stateMachine.CurrentState;
            if (currentState != null && currentState.CanStagger == false)
                return;

            // 경직 중에 또 기준선을 지나면 경직 모션을 처음부터 다시 재생합니다.
            if (stateMachine.IsCurrent<BossStaggerState>())
            {
                currentState.Enter();
                return;
            }

            stateMachine.ChangeState<BossStaggerState>();
        }

        // 이번 피해로 경직 기준선을 하나 이상 지났으면 true. 한 번에 여러 개를 지나도 경직은 한 번입니다.
        private bool PassStaggerLine()
        {
            bool passed = false;
            float hpRatio = (float)Health.HP / Health.MaxHP;
            while (nextStaggerIndex < staggerHpRatios.Length && hpRatio <= staggerHpRatios[nextStaggerIndex])
            {
                nextStaggerIndex++;
                passed = true;
            }
            return passed;
        }

        public void TakeHeal(HealEvent healEvent)
        {
            Health.Heal(healEvent.Heal);
            NotifyHpChanged();
        }

        private void NotifyHpChanged()
        {
            if (HpChanged != null)
            {
                HpChanged.Invoke(Health.HP, Health.MaxHP);
            }
        }

        // 죽는 순간 호출됩니다. 더 이상 카드에 맞지 않게 하고 보상을 떨어뜨립니다.
        public void OnDeath()
        {
            Vector3 bodyCenter = mainCollider.bounds.center;
            mainCollider.enabled = false;
            Unregister();

            if (dropTable != null)
            {
                dropTable.Drop(transform.position);
            }
            if (goldPrefab != null && goldCount > 0)
            {
                CurrencyPickup.Scatter(goldPrefab, goldCount, bodyCenter, transform.position.y);
            }

            if (Died != null)
            {
                Died.Invoke(this);
            }
        }

        // sounds 중 하나를 골라 몸 중앙에서 3D로 재생합니다.
        private void PlayRandomSound(AudioManager.Sfx[] sounds)
        {
            if (sounds == null || sounds.Length == 0)
                return;

            if (AudioManager.Instance == null)
                return;

            int index = UnityEngine.Random.Range(0, sounds.Length);
            AudioManager.Instance.PlaySfxAt(sounds[index], mainCollider.bounds.center);
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
            {
                CombatSystem.Instance.UnregisterMonster(mainCollider);
            }
            isRegistered = false;
        }
    }
}
