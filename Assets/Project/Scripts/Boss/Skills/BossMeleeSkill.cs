using Study3D.FpsGame.Enemy;
using Study3D.FpsGame.Player;
using UnityEngine;

namespace Study3D.FpsGame.Boss
{
    // BossMeleeSkill
    // : 일반 공격. 플레이어가 충분히 가까우면 지팡이를 휘둘러 피해를 주고 밀어냅니다. (우선순위 1)
    // - 쓸 수 있는 조건: 쿨타임(3초)이 끝났고, 플레이어가 공격 거리 안에 있을 때
    // - Attack01 을 재생하고, 재생 진행도가 판정 구간(hitStart ~ hitEnd)에 있는 동안만 지팡이 판정을 켭니다.
    //   (EvilMage 의 원본 애니메이션에는 판정을 켜고 끄는 키가 없어서 코드가 직접 켭니다)
    // - 휘두르기 전(판정 구간 전)까지는 플레이어를 바라보며 방향을 맞추고, 그 뒤로는 방향을 고정합니다.
    // - 피해는 지팡이의 EnemyWeapon 이 줍니다. (피해량은 EnemyWeapon 에서 정함)
    //   맞혔다는 알림(HitPlayer)을 받으면 보스 → 플레이어 방향으로 플레이어를 밀어냅니다. (LocalPlayer.Knockback)
    // - 애니메이션이 끝나면 스킬이 끝나고 쿨타임이 시작됩니다.
    public class BossMeleeSkill : BossSkill
    {
        private static readonly int AttackAnim = Animator.StringToHash("Attack01");

        [Header("조건")]
        [Tooltip("플레이어가 이 거리 안에 있으면 휘두릅니다")]
        [SerializeField] private float attackRange = 3f;

        [Header("판정")]
        [Tooltip("Attack01 재생 진행도(0~1)가 이 값이 되면 지팡이 판정을 켭니다")]
        [Range(0f, 1f)]
        [SerializeField] private float hitStart = 0.3f;
        [Tooltip("Attack01 재생 진행도(0~1)가 이 값을 넘으면 지팡이 판정을 끕니다")]
        [Range(0f, 1f)]
        [SerializeField] private float hitEnd = 0.6f;

        [Header("넉백")]
        [Tooltip("밀려나는 거리")]
        [SerializeField] private float knockbackDistance = 10f;
        [Tooltip("밀려나는 시간 (초)")]
        [SerializeField] private float knockbackDuration = 1.5f;

        [Header("Ref")]
        [Tooltip("지팡이에 붙인 공격 판정. 피해량은 이 EnemyWeapon 에서 정합니다")]
        [SerializeField] private EnemyWeapon staffWeapon;

        private Collider staffCollider;
        private Boss owner;

        protected override float DefaultCoolTime
        {
            get { return 3f; }
        }

        private void Awake()
        {
            staffCollider = staffWeapon.GetComponent<Collider>();
            owner = GetComponent<Boss>();
        }

        protected override void OnEnable()
        {
            base.OnEnable();
            SetStaffColliderEnabled(false);
            staffWeapon.HitPlayer += OnStaffHitPlayer;
        }

        private void OnDisable()
        {
            staffWeapon.HitPlayer -= OnStaffHitPlayer;
        }

        protected override bool CheckCondition(Boss boss)
        {
            if (boss.HasTarget == false)
                return false;

            return boss.DistanceToPlayer <= attackRange;
        }

        protected override void OnBegin(Boss boss)
        {
            SetStaffColliderEnabled(false);
            boss.PlayAnim(AttackAnim);
        }

        protected override void OnExecute(Boss boss, float deltaTime)
        {
            float progress = boss.GetAnimProgress(AttackAnim);

            // 휘두르기 전까지는 플레이어 쪽으로 몸을 돌립니다.
            if (progress < hitStart)
            {
                boss.LookAtPlayer(deltaTime);
            }

            bool isHitTime = progress >= hitStart && progress <= hitEnd;
            SetStaffColliderEnabled(isHitTime);

            // 휘두르기가 끝났으므로 남은 모션이 끊겨도 다시 휘두르지 않습니다.
            if (progress > hitEnd)
            {
                StartCoolTime();
            }

            if (boss.IsAnimEnded(AttackAnim) == false)
                return;

            SetStaffColliderEnabled(false);
            FinishSkill();
        }

        // 경직, 사망 등으로 끊기면 판정을 끕니다.
        protected override void OnCancel(Boss boss)
        {
            SetStaffColliderEnabled(false);
        }

        // 지팡이가 플레이어를 맞히면 보스 → 플레이어 방향으로 밀어냅니다.
        private void OnStaffHitPlayer(EnemyWeapon weapon)
        {
            LocalPlayer player = Player.Player.LocalPlayer;
            if (player == null)
                return;

            Vector3 direction = player.transform.position - owner.transform.position;
            player.Knockback(direction, knockbackDistance, knockbackDuration);
        }

        private void SetStaffColliderEnabled(bool isEnabled)
        {
            if (staffCollider != null && staffCollider.enabled != isEnabled)
            {
                staffCollider.enabled = isEnabled;
            }
        }
    }
}
