namespace Study3D.FpsGame.Boss.States
{
    // 추격: 원거리 사거리 밖에 있는 플레이어 쪽으로 달려갑니다.
    // - 판단 순서: 쓸 수 있는 스킬 → 사거리 안이면 공전 → 추격 이동
    public class BossChaseState : BossState
    {
        public BossChaseState(Boss boss) : base(boss) { }

        protected override void OnEnter()
        {
            Boss.PlayAnim(Boss.RunAnim);
        }

        protected override void OnTick(float deltaTime)
        {
            KeepAnim(Boss.RunAnim);

            if (Boss.HasTarget == false)
            {
                Boss.ChangeState<BossIdleState>();
                return;
            }

            if (Boss.TryUseSkill())
                return;

            if (Boss.IsInOrbitRange())
            {
                Boss.ChangeState<BossOrbitState>();
                return;
            }

            Boss.MoveToPlayer(deltaTime);
        }
    }
}
