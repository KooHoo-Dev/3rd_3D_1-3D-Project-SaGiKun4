using UnityEngine;

namespace Study3D.FpsGame.Boss
{
    // BossRunSkill
    // : 달리기 (기존 기획의 도약). 플레이어가 너무 붙으면 방 중앙 방향으로 정해진 거리만큼 달려서 빠집니다. (우선순위 3)
    // - 플레이어가 벽을 등지고 있을 때 보스가 플레이어를 계속 벽 쪽으로 미는 상황을 풀기 위한 패턴입니다.
    // - 쓸 수 있는 조건: 쿨타임(10초)이 끝났고, 플레이어가 발동 거리(일반 공격 거리보다 짧음) 안에 있을 때
    //   일반 공격이 우선순위가 높으므로, 일반 공격이 쿨타임일 때 나옵니다.
    // - 방향: 시작하는 순간 보스 → 방 중앙 방향으로 고정합니다. 이미 중앙이어도 거리는 그대로 달립니다.
    //   (중앙에 딱 서 있어서 방향을 정할 수 없으면 플레이어 반대 방향으로 달림)
    // - 정해진 거리를 다 달렸거나, 기둥, 벽, 방해물에 막히면 그 자리에서 멈추고 끝납니다. (Boss.MoveStraight)
    // - 피해는 없습니다.
    public class BossRunSkill : BossSkill
    {
        [Header("조건")]
        [Tooltip("플레이어가 이 거리 안에 있으면 달려서 빠집니다")]
        [SerializeField] private float triggerRange = 1.5f;

        [Header("달리기")]
        [Tooltip("달리는 거리 (고정)")]
        [SerializeField] private float runDistance = 8f;
        [SerializeField] private float runSpeed = 12f;

        [Header("Ref")]
        [Tooltip("방 중앙 위치. 보스는 소환되는 프리팹이라 보스방(BossRoom)이 소환할 때 넣어 줍니다")]
        [SerializeField] private Transform roomCenter;

        private Vector3 runDirection;
        private float movedDistance;

        protected override float DefaultCoolTime
        {
            get { return 10f; }
        }

        // 보스방이 보스를 소환할 때 자기 방의 중앙을 넣어 줍니다.
        public void SetRoomCenter(Transform center)
        {
            roomCenter = center;
        }

        protected override bool CheckCondition(Boss boss)
        {
            if (roomCenter == null || boss.HasTarget == false)
                return false;

            return boss.DistanceToPlayer <= triggerRange;
        }

        protected override void OnBegin(Boss boss)
        {
            runDirection = GetRunDirection(boss);
            movedDistance = 0f;
            boss.PlayAnim(Boss.RunAnim);
        }

        protected override void OnExecute(Boss boss, float deltaTime)
        {
            boss.LookAtDirection(runDirection, deltaTime);
            boss.KeepAnim(Boss.RunAnim);

            float step = Mathf.Min(runSpeed * deltaTime, runDistance - movedDistance);
            bool moved = boss.MoveStraight(runDirection, step);
            movedDistance += step;

            // 막혔거나 다 달렸으면 끝
            if (moved == false || movedDistance >= runDistance)
            {
                FinishSkill();
            }
        }

        // 보스 → 방 중앙 방향 (수평). 중앙에 서 있으면 플레이어 반대 방향입니다.
        private Vector3 GetRunDirection(Boss boss)
        {
            Vector3 toCenter = roomCenter.position - boss.transform.position;
            toCenter.y = 0f;
            if (toCenter.sqrMagnitude > 0.01f)
            {
                return toCenter.normalized;
            }

            Vector3 awayFromPlayer = boss.transform.position - Player.Player.LocalPlayer.transform.position;
            awayFromPlayer.y = 0f;
            if (awayFromPlayer.sqrMagnitude > 0.0001f)
            {
                return awayFromPlayer.normalized;
            }

            return boss.transform.forward;
        }
    }
}
