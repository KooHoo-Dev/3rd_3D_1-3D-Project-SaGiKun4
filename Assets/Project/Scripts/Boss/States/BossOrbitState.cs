namespace Study3D.FpsGame.Boss.States
{
    // 공전: 원거리 사거리 안에서 플레이어를 바라본 채 한 방향으로 천천히 돕니다.
    // - 판단 순서: 쓸 수 있는 스킬 → 사거리를 벗어나면 추격 → 공전 이동
    // - 기둥, 벽, 방해물에 막히면 도는 방향을 반대로 바꿉니다. (Boss.Orbit)
    // - 도는 방향에 맞춰 옆걸음 애니메이션(WalkRight / WalkLeft)을 재생합니다.
    public class BossOrbitState : BossState
    {
        public BossOrbitState(Boss boss) : base(boss) { }

        protected override void OnEnter()
        {
            Boss.PlayAnim(GetWalkAnim());
        }

        protected override void OnTick(float deltaTime)
        {
            if (Boss.HasTarget == false)
            {
                Boss.ChangeState<BossIdleState>();
                return;
            }

            if (Boss.TryUseSkill())
                return;

            if (Boss.IsOutOfOrbitRange())
            {
                Boss.ChangeState<BossChaseState>();
                return;
            }

            Boss.Orbit(deltaTime);

            // 막혀서 방향이 바뀌었으면 반대쪽 옆걸음으로 바뀝니다.
            KeepAnim(GetWalkAnim());
        }

        private int GetWalkAnim()
        {
            if (Boss.OrbitSign > 0)
            {
                return Boss.WalkRightAnim;
            }
            return Boss.WalkLeftAnim;
        }
    }
}
