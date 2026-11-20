using Study3D.FpsGame.Player;
using UnityEngine;

namespace Study3D.FpsGame.Boss
{
    // Boss.AI
    // : 보스 상태들이 공통으로 쓰는 기능 모음 입니다.
    // - 거리 판정, 이동(추격, 공전, 직진), 회전, 애니메이터 상태 재생과 종료 판정
    // - 보스방은 기둥 몇 개만 있는 사각형 방이라 길찾기 없이 직진합니다.
    //   대신 이동할 때마다 몸 크기의 구(SphereCast)로 앞을 확인해서 기둥, 벽, 방해물을 뚫고 지나가지 않게 합니다.
    //   - 추격: 막히면 막힌 면을 따라 미끄러지듯 이동 (기둥 모서리를 돌아감)
    //   - 공전: 막히면 도는 방향을 반대로 바꿈
    //   - 직진(스킬용): 막히면 그 자리에서 멈추고 false
    // - 애니메이터 파라미터 없이 상태 이름으로 직접 재생(CrossFade)합니다.
    public partial class Boss
    {
        public static readonly int IdleAnim = Animator.StringToHash("IdleBattle");
        public static readonly int RunAnim = Animator.StringToHash("RunFWD");
        public static readonly int WalkLeftAnim = Animator.StringToHash("WalkLeft");
        public static readonly int WalkRightAnim = Animator.StringToHash("WalkRight");
        public static readonly int HitAnim = Animator.StringToHash("GetHit");
        public static readonly int StunAnim = Animator.StringToHash("Dizzy");
        public static readonly int DieAnim = Animator.StringToHash("Die");

        private const float CastSkin = 0.05f; // 막힌 면에서 띄울 거리 (다음 이동 때 같은 면에 바로 걸리지 않도록)

        [Header("AI")]
        [Tooltip("전투 시작 전, 플레이어가 이 거리 안에 들어오면 전투를 시작합니다. 0 이면 보스방(BossRoom)이 시작할 때까지 기다립니다")]
        [SerializeField] private float detectRange = 0f;
        [Tooltip("원거리 공격 사거리. 이보다 멀면 추격하고, 안이면 플레이어 주위를 공전합니다")]
        [SerializeField] private float rangedRange = 12f;
        [Tooltip("공전 중 플레이어가 사거리 + 이 값보다 멀어지면 다시 추격합니다 (경계에서 상태가 계속 바뀌지 않도록)")]
        [SerializeField] private float orbitExitMargin = 1f;
        [SerializeField] private float moveSpeed = 5f;
        [Tooltip("공전할 때 옆으로 걷는 속도")]
        [SerializeField] private float orbitSpeed = 2f;
        [Tooltip("초당 회전 각도")]
        [SerializeField] private float turnSpeed = 360f;
        [SerializeField] private float animFadeTime = 0.1f;

        [Header("이동 막힘 확인")]
        [Tooltip("몸 반경. 이 크기의 구로 앞을 확인합니다")]
        [SerializeField] private float bodyRadius = 0.6f;
        [Tooltip("발밑에서 확인용 구의 중심까지 높이. 바닥에 걸리지 않도록 몸 반경보다 높게 둡니다")]
        [SerializeField] private float castHeight = 1f;

        [Header("Ref")]
        [SerializeField] private Animator animator;

        public float RangedRange => rangedRange;
        public float BodyRadius => bodyRadius;

        // 공전 방향. +1 = 플레이어를 바라본 채 오른쪽, -1 = 왼쪽
        public int OrbitSign { get; private set; } = 1;

        // 살아있는 플레이어가 있을 때만 쫓거나 공격합니다.
        public bool HasTarget
        {
            get
            {
                LocalPlayer player = Player.Player.LocalPlayer;
                return player != null && player.IsDead == false;
            }
        }

        // 높이 차이는 무시한 수평 거리 입니다.
        public float DistanceToPlayer
        {
            get
            {
                if (HasTarget == false)
                    return float.MaxValue;

                return GetFlatDirectionToPlayer().magnitude;
            }
        }

        public bool IsPlayerInDetectRange()
        {
            return detectRange > 0f && DistanceToPlayer <= detectRange;
        }

        // 공전을 시작할 거리인지 (추격 → 공전)
        public bool IsInOrbitRange()
        {
            return DistanceToPlayer <= rangedRange;
        }

        // 공전을 그만둘 거리인지 (공전 → 추격)
        public bool IsOutOfOrbitRange()
        {
            return DistanceToPlayer > rangedRange + orbitExitMargin;
        }

        private void InitializeAI()
        {
            if (animator == null)
            {
                animator = GetComponent<Animator>();
            }

            // 이동은 코드로 처리하므로 애니메이션의 루트 모션은 끕니다.
            animator.applyRootMotion = false;
        }

        #region 이동

        private Vector3 GetFlatDirectionToPlayer()
        {
            Vector3 dir = Player.Player.LocalPlayer.transform.position - transform.position;
            dir.y = 0f;
            return dir;
        }

        // 플레이어 쪽으로 몸을 돌리며 이동합니다. 기둥에 막히면 기둥 면을 따라 미끄러집니다.
        public void MoveToPlayer(float deltaTime)
        {
            Vector3 dir = GetFlatDirectionToPlayer();
            if (dir.sqrMagnitude < 0.0001f)
                return;

            dir.Normalize();
            LookAt(dir, deltaTime);
            MoveWithSlide(dir, moveSpeed * deltaTime);
        }

        // 플레이어를 바라본 채 플레이어 주위를 한 방향으로 돕니다. 막히면 방향을 반대로 바꿉니다.
        public void Orbit(float deltaTime)
        {
            Vector3 toPlayer = GetFlatDirectionToPlayer();
            if (toPlayer.sqrMagnitude < 0.0001f)
                return;

            toPlayer.Normalize();
            LookAt(toPlayer, deltaTime);

            // 플레이어 방향과 수직인 방향 = 원의 접선 (위쪽 축과 외적. +1 이면 바라본 기준 오른쪽)
            Vector3 tangent = Vector3.Cross(Vector3.up, toPlayer) * OrbitSign;
            bool moved = MoveStraight(tangent, orbitSpeed * deltaTime);
            if (moved == false)
            {
                OrbitSign = -OrbitSign;
            }
        }

        // 정해진 방향(수평)으로 distance 만큼 직진합니다. 막히면 막힌 곳 앞까지만 가고 false
        public bool MoveStraight(Vector3 direction, float distance)
        {
            direction.y = 0f;
            if (direction.sqrMagnitude < 0.0001f || distance <= 0f)
                return true;

            direction.Normalize();
            RaycastHit hit;
            if (CastBody(direction, distance, out hit))
            {
                float movable = Mathf.Max(0f, hit.distance - CastSkin);
                transform.position += direction * movable;
                return false;
            }

            transform.position += direction * distance;
            return true;
        }

        // 막히면 막힌 면을 따라 남은 거리만큼 한 번 더 이동해 봅니다.
        private void MoveWithSlide(Vector3 direction, float distance)
        {
            RaycastHit hit;
            if (CastBody(direction, distance, out hit) == false)
            {
                transform.position += direction * distance;
                return;
            }

            float movable = Mathf.Max(0f, hit.distance - CastSkin);
            transform.position += direction * movable;

            Vector3 slide = Vector3.ProjectOnPlane(direction, hit.normal);
            slide.y = 0f;
            MoveStraight(slide, distance - movable);
        }

        // 몸 크기의 구를 direction 으로 distance 만큼 밀어 환경(벽, 기둥, 방해물)에 막히는지 확인합니다.
        private bool CastBody(Vector3 direction, float distance, out RaycastHit hit)
        {
            Vector3 origin = transform.position + Vector3.up * castHeight;
            int mask = CombatSystem.Instance.EnvironmentMask;
            return Physics.SphereCast(origin, bodyRadius, direction, out hit, distance + CastSkin, mask, QueryTriggerInteraction.Ignore);
        }

        // 공전 방향을 무작위로 정합니다. (전투를 시작할 때)
        public void PickRandomOrbitSign()
        {
            if (UnityEngine.Random.value < 0.5f)
            {
                OrbitSign = 1;
            }
            else
            {
                OrbitSign = -1;
            }
        }

        // 제자리에서 플레이어 쪽으로 몸만 돌립니다.
        public void LookAtPlayer(float deltaTime)
        {
            if (HasTarget == false)
                return;

            Vector3 dir = GetFlatDirectionToPlayer();
            if (dir.sqrMagnitude < 0.0001f)
                return;

            LookAt(dir.normalized, deltaTime);
        }

        // 제자리에서 정해진 방향(수평)으로 몸만 돌립니다. (스킬이 고정한 방향을 바라볼 때 사용)
        public void LookAtDirection(Vector3 direction, float deltaTime)
        {
            direction.y = 0f;
            if (direction.sqrMagnitude < 0.0001f)
                return;

            LookAt(direction.normalized, deltaTime);
        }

        private void LookAt(Vector3 dir, float deltaTime)
        {
            Quaternion target = Quaternion.LookRotation(dir);
            transform.rotation = Quaternion.RotateTowards(transform.rotation, target, turnSpeed * deltaTime);
        }

        #endregion

        #region 애니메이션

        // 해당 상태로 부드럽게 넘어갑니다. 이미 그 상태면 처음부터 다시 재생합니다.
        public void PlayAnim(int stateHash)
        {
            if (animator.GetCurrentAnimatorStateInfo(0).shortNameHash == stateHash)
            {
                animator.Play(stateHash, 0, 0f);
                return;
            }

            animator.CrossFadeInFixedTime(stateHash, animFadeTime, 0);
        }

        // 반복 재생하는 상태(Idle, Run, Walk)용 입니다.
        // 컨트롤러에 남아있는 Exit Time 전이로 다른 상태로 흘러가면 다시 되돌립니다.
        public void KeepAnim(int stateHash)
        {
            AnimatorStateInfo info;
            if (animator.IsInTransition(0))
            {
                info = animator.GetNextAnimatorStateInfo(0);
            }
            else
            {
                info = animator.GetCurrentAnimatorStateInfo(0);
            }

            if (info.shortNameHash != stateHash)
            {
                animator.CrossFadeInFixedTime(stateHash, animFadeTime, 0);
            }
        }

        // 게임이 멈추면 애니메이터도 멈춥니다.
        private void ApplyGamePause(bool paused)
        {
            if (paused)
            {
                animator.speed = 0f;
            }
            else
            {
                animator.speed = 1f;
            }
        }

        // 해당 상태의 재생 진행도 (0 = 시작, 1 = 끝). 그 상태가 아니면(넘어가는 중 포함) -1 입니다.
        public float GetAnimProgress(int stateHash)
        {
            if (animator.IsInTransition(0))
            {
                AnimatorStateInfo next = animator.GetNextAnimatorStateInfo(0);
                if (next.shortNameHash == stateHash)
                {
                    return next.normalizedTime;
                }
                return -1f;
            }

            AnimatorStateInfo info = animator.GetCurrentAnimatorStateInfo(0);
            if (info.shortNameHash != stateHash)
                return -1f;

            return info.normalizedTime;
        }

        // 해당 상태의 애니메이션을 한 번 끝까지 재생했는지 확인합니다.
        public bool IsAnimEnded(int stateHash)
        {
            if (animator.IsInTransition(0))
                return false;

            AnimatorStateInfo info = animator.GetCurrentAnimatorStateInfo(0);
            return info.shortNameHash == stateHash && info.normalizedTime >= 1f;
        }

        #endregion
    }
}
