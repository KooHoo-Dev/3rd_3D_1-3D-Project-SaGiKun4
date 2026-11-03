using Study3D.FpsGame.Map;
using UnityEngine;

namespace Study3D.FpsGame.Enemy
{
    // DashSkill
    // : 준비 동작 뒤 정해진 지점으로 돌진하는 스킬 입니다. (2D 프로젝트 DashSkill 구조)
    // - 쓸 수 있는 조건: 쿨타임이 끝났고, 플레이어가 최소~최대 거리 안에 있고, 지도 위에서 막힘 없이 보일 때
    // - 준비: 시작하는 순간의 플레이어 위치로 목표와 방향을 고정하고, 그 방향을 바라보며 준비 모션을 재생합니다.
    //   플레이어는 이 준비 동작을 보고 옆으로 피하면 됩니다. (준비 중에 플레이어가 움직여도 목표는 바뀌지 않음)
    // - 돌진: 고정된 방향으로 지도를 따라 직진합니다. (RoomNavAgent.MoveStraight)
    //   아래 중 하나가 되면 그 자리에서 멈추고 끝납니다.
    //   1. 목표 지점 + 여유 거리만큼 이동함   2. 벽, 장애물에 막힘   3. 돌진 판정이 플레이어를 맞힘
    // - 피해는 일반 공격(칼)과 같은 방식으로, 몸 앞쪽에 둔 공격용 콜라이더(EnemyWeapon)가 줍니다.
    //   돌진하는 동안만 켜 두고, 맞혔다는 알림(HitPlayer)을 받으면 다음 프레임에 멈춥니다.
    //   판정이 몸보다 앞에 있으므로 몸이 플레이어를 뚫고 지나가기 전에 먼저 맞습니다.
    // - 스킬 전체(준비 + 돌진) 동안 맞아도 경직되지 않습니다. (EnemySkill 의 ignoreHit)
    public class DashSkill : EnemySkill
    {
        private enum DashPhase
        {
            Charge,
            Dash,
        }

        [Header("조건")]
        [Tooltip("플레이어가 이보다 가까우면 돌진하지 않습니다 (붙어 있을 때는 일반 공격)")]
        [SerializeField] private float minRange = 6f;
        [Tooltip("플레이어가 이보다 멀면 돌진하지 않습니다")]
        [SerializeField] private float maxRange = 15f;

        [Header("준비")]
        [Tooltip("돌진 전 준비 동작 시간 (초). 플레이어가 보고 피할 수 있는 시간")]
        [SerializeField] private float chargeTime = 4f;
        [Tooltip("준비 중 재생할 애니메이터 상태 이름")]
        [SerializeField] private string chargeAnimState = "Taunting";

        [Header("돌진")]
        [SerializeField] private float dashSpeed = 15f;
        [Tooltip("목표 지점을 지나 더 달리는 거리")]
        [SerializeField] private float overshoot = 2f;
        [Tooltip("한 번에 달릴 수 있는 최대 거리")]
        [SerializeField] private float maxDashDistance = 20f;
        [Tooltip("돌진 중 재생할 애니메이터 상태 이름")]
        [SerializeField] private string dashAnimState = "RunFWD";

        [Header("Ref")]
        [Tooltip("같은 오브젝트의 RoomNavAgent (지도 기준 시야 확인, 지도를 따라 직진)")]
        [SerializeField] private RoomNavAgent navAgent;
        [Tooltip("몸 앞쪽에 둔 돌진 공격 판정. 피해량은 이 EnemyWeapon 에서 정합니다")]
        [SerializeField] private EnemyWeapon dashWeapon;

        private int chargeAnimHash;
        private int dashAnimHash;
        private Collider dashCollider;

        private DashPhase phase;
        private float chargeTimer;
        private Vector3 dashDirection;
        private float dashDistance;  // 이번 돌진에서 달릴 거리
        private float movedDistance; // 지금까지 달린 거리
        private bool hasHitPlayer;   // 이번 돌진에서 판정이 플레이어를 맞혔는지

        private void Awake()
        {
            chargeAnimHash = Animator.StringToHash(chargeAnimState);
            dashAnimHash = Animator.StringToHash(dashAnimState);
            dashCollider = dashWeapon.GetComponent<Collider>();
        }

        protected override void OnEnable()
        {
            base.OnEnable();
            SetDashColliderEnabled(false);
            dashWeapon.HitPlayer += OnDashHitPlayer;
        }

        private void OnDisable()
        {
            dashWeapon.HitPlayer -= OnDashHitPlayer;
        }

        protected override bool CheckCondition(Enemy enemy)
        {
            if (navAgent == null || enemy.HasTarget == false)
                return false;

            float distance = enemy.DistanceToPlayer;
            if (distance < minRange || distance > maxRange)
                return false;

            // 가장 비싼 확인이라 마지막에 합니다. (0.25초마다 한 번만 계산)
            return navAgent.CanSeeGoal();
        }

        protected override void OnBegin(Enemy enemy)
        {
            LockTarget(enemy);

            phase = DashPhase.Charge;
            chargeTimer = chargeTime;
            enemy.PlayAnim(chargeAnimHash);
        }

        protected override void OnExecute(Enemy enemy, float deltaTime)
        {
            if (phase == DashPhase.Charge)
            {
                TickCharge(enemy, deltaTime);
            }
            else
            {
                TickDash(enemy, deltaTime);
            }
        }

        // 사망 등으로 끊기면 판정을 끕니다.
        protected override void OnCancel(Enemy enemy)
        {
            SetDashColliderEnabled(false);
        }

        // 지금 플레이어 위치로 돌진 방향과 거리를 정합니다. (이후 플레이어가 움직여도 바꾸지 않음)
        private void LockTarget(Enemy enemy)
        {
            Vector3 toPlayer = Player.Player.LocalPlayer.transform.position - enemy.transform.position;
            toPlayer.y = 0f;

            dashDirection = toPlayer.normalized;
            dashDistance = Mathf.Min(toPlayer.magnitude + overshoot, maxDashDistance);
            movedDistance = 0f;
        }

        // 준비: 고정한 방향을 바라보며 준비 모션을 재생하다가, 시간이 다 되면 돌진을 시작합니다.
        private void TickCharge(Enemy enemy, float deltaTime)
        {
            enemy.LookAtDirection(dashDirection, deltaTime);
            enemy.KeepAnim(chargeAnimHash);

            chargeTimer -= deltaTime;
            if (chargeTimer > 0f)
                return;

            StartDash(enemy);
        }

        private void StartDash(Enemy enemy)
        {
            phase = DashPhase.Dash;
            hasHitPlayer = false;
            SetDashColliderEnabled(true);
            enemy.PlayAnim(dashAnimHash);
        }

        // 돌진: 한 프레임 이동량만큼 지도를 따라 직진하고, 맞혔거나 막혔거나 다 달렸으면 끝냅니다.
        private void TickDash(Enemy enemy, float deltaTime)
        {
            // 판정은 물리 단계에서 들어오므로, 맞혔다는 알림은 그다음 프레임에 확인합니다.
            if (hasHitPlayer)
            {
                EndDash();
                return;
            }

            enemy.LookAtDirection(dashDirection, deltaTime);
            enemy.KeepAnim(dashAnimHash);

            float step = Mathf.Min(dashSpeed * deltaTime, dashDistance - movedDistance);
            bool moved = navAgent.MoveStraight(dashDirection, step);
            movedDistance += step;

            // 벽에 막혔거나 다 달렸으면 끝
            if (moved == false || movedDistance >= dashDistance)
            {
                EndDash();
            }
        }

        private void EndDash()
        {
            SetDashColliderEnabled(false);
            FinishSkill();
        }

        private void OnDashHitPlayer(EnemyWeapon weapon)
        {
            hasHitPlayer = true;
        }

        private void SetDashColliderEnabled(bool isEnabled)
        {
            if (dashCollider != null)
            {
                dashCollider.enabled = isEnabled;
            }
        }
    }
}
