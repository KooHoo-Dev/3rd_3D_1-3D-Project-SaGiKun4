using Study3D.FpsGame.Player;
using UnityEngine;

namespace Study3D.FpsGame.Enemy
{
    // Enemy.AI
    // : FSM 상태들이 공통으로 쓰는 기능 모음 입니다.
    // - 거리 판정, 플레이어 방향으로 이동/회전, 애니메이터 상태 재생과 종료 판정
    // - 애니메이터 파라미터 없이 상태 이름으로 직접 재생(CrossFade)합니다.
    public partial class Enemy
    {
        public static readonly int IdleAnim = Animator.StringToHash("IdleBattle");
        public static readonly int RunAnim = Animator.StringToHash("RunFWD");
        public static readonly int AttackAnim = Animator.StringToHash("Attack01");
        public static readonly int HitAnim = Animator.StringToHash("GetHit");
        public static readonly int DieAnim = Animator.StringToHash("Die");

        [Header("AI")]
        [Tooltip("플레이어가 이 거리 안에 들어오면 쫓아갑니다")]
        [SerializeField] private float detectRange = 30f;
        [Tooltip("플레이어가 이 거리 안에 들어오면 공격합니다")]
        [SerializeField] private float attackRange = 3f;
        [SerializeField] private float moveSpeed = 5f;
        [Tooltip("초당 회전 각도")]
        [SerializeField] private float turnSpeed = 720f;
        [SerializeField] private float animFadeTime = 0.1f;

        [Header("Ref")]
        [SerializeField] private Animator animator;

        public float DetectRange => detectRange;
        public float AttackRange => attackRange;

        // 외부 길찾기 (없으면 플레이어로 직진)
        private IEnemyMover mover;

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
        public float DistanceToPlayer => HasTarget ? GetFlatDirectionToPlayer().magnitude : float.MaxValue;

        private void InitializeAI()
        {
            if (animator == null) animator = GetComponent<Animator>();

            // 이동은 코드로 처리하므로 애니메이션의 루트 모션은 끕니다.
            animator.applyRootMotion = false;
        }

        #region 이동

        // 추격 이동을 외부 길찾기에 맡깁니다. null 이면 다시 직진으로 돌아갑니다.
        public void SetMover(IEnemyMover enemyMover)
        {
            mover = enemyMover;
        }

        private Vector3 GetFlatDirectionToPlayer()
        {
            Vector3 dir = Player.Player.LocalPlayer.transform.position - transform.position;
            dir.y = 0f;
            return dir;
        }

        // 플레이어 방향으로 몸을 돌리며 앞으로 이동합니다.
        public void MoveToPlayer(float deltaTime)
        {
            // 외부 길찾기가 연결돼 있으면 이동은 맡기고, 이동한 방향으로 몸만 돌립니다.
            if (mover != null)
            {
                Vector3 moved = mover.Move(deltaTime, moveSpeed);
                if (moved.sqrMagnitude > 0.0001f) LookAt(moved.normalized, deltaTime);
                return;
            }

            Vector3 dir = GetFlatDirectionToPlayer();
            if (dir.sqrMagnitude < 0.0001f) return;

            dir.Normalize();
            LookAt(dir, deltaTime);
            transform.position += dir * (moveSpeed * deltaTime);
        }

        // 지점 쪽으로 몸을 돌리며 직진합니다. (높이는 그대로) 도착하면 true
        public bool MoveTowards(Vector3 point, float deltaTime)
        {
            Vector3 toPoint = point - transform.position;
            toPoint.y = 0f;
            float distance = toPoint.magnitude;
            float step = moveSpeed * deltaTime;
            if (distance <= step)
            {
                transform.position += toPoint;
                return true;
            }

            Vector3 direction = toPoint / distance;
            LookAt(direction, deltaTime);
            transform.position += direction * step;
            return false;
        }

        // 제자리에서 플레이어 쪽으로 몸만 돌립니다.
        public void LookAtPlayer(float deltaTime)
        {
            if (HasTarget == false) return;

            Vector3 dir = GetFlatDirectionToPlayer();
            if (dir.sqrMagnitude < 0.0001f) return;

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

        // 반복 재생하는 상태(Idle, Run)용 입니다.
        // 컨트롤러에 남아있는 Exit Time 전이로 다른 상태로 흘러가면 다시 되돌립니다.
        public void KeepAnim(int stateHash)
        {
            AnimatorStateInfo info = animator.IsInTransition(0)
                ? animator.GetNextAnimatorStateInfo(0)
                : animator.GetCurrentAnimatorStateInfo(0);

            if (info.shortNameHash != stateHash) animator.CrossFadeInFixedTime(stateHash, animFadeTime, 0);
        }

        // 게임이 멈추면 애니메이터도 멈춥니다.
        // 칼 판정(Attack01이 콜라이더를 켜고 끔)과 애니메이션 종료 판정이 애니메이터 시간으로 돌기 때문에, 이동만 멈추면 정지 중에도 공격이 들어갑니다.
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

        // 해당 상태의 애니메이션을 한 번 끝까지 재생했는지 확인합니다.
        public bool IsAnimEnded(int stateHash)
        {
            if (animator.IsInTransition(0)) return false;

            AnimatorStateInfo info = animator.GetCurrentAnimatorStateInfo(0);
            return info.shortNameHash == stateHash && info.normalizedTime >= 1f;
        }

        #endregion
    }
}
