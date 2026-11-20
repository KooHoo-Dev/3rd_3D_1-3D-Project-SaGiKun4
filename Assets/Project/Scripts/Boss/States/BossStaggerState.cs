namespace Study3D.FpsGame.Boss.States
{
    // 경직: 체력이 경직 기준선(70%, 30%, 10%)을 지날 때 한 번씩 들어옵니다. (Boss.TakeDamage)
    // - 피격 모션이 끝나면 거리에 따라 추격이나 공전으로 돌아갑니다.
    // - 쓰던 스킬은 스킬 상태를 나갈 때 취소되고, 쿨타임이 돌지 않아 다시 사용됩니다.
    public class BossStaggerState : BossState
    {
        public BossStaggerState(Boss boss) : base(boss) { }

        protected override void OnEnter()
        {
            Boss.PlayAnim(Boss.HitAnim);
        }

        protected override void OnTick(float deltaTime)
        {
            if (IsAnimEnded(Boss.HitAnim) == false)
                return;

            Boss.ReturnToMove();
        }
    }
}
