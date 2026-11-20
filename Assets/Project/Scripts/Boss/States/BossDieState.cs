namespace Study3D.FpsGame.Boss.States
{
    // 죽음: 체력이 0이 되면 들어오며, 다른 상태로 나가지 않습니다.
    // - 보상을 떨어뜨리고 처치를 알린 뒤(Boss.OnDeath), 사망 모션을 재생하고 그대로 남습니다.
    public class BossDieState : BossState
    {
        public BossDieState(Boss boss) : base(boss) { }

        public override bool CanStagger
        {
            get { return false; }
        }

        protected override void OnEnter()
        {
            Boss.OnDeath();
            Boss.PlayAnim(Boss.DieAnim);
        }
    }
}
