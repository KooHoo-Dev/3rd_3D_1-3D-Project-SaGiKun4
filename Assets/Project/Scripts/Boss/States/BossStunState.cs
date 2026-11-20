namespace Study3D.FpsGame.Boss.States
{
    // 기절: 마법이 파훼되면 들어옵니다. (Boss.Stun)
    // - 기절 시간(Boss.StunTime) 동안 Dizzy 모션을 반복하며 아무것도 하지 않습니다.
    // - 기절 중에는 경직 기준선을 지나도 경직되지 않습니다. (이미 무방비 상태라서)
    public class BossStunState : BossState
    {
        private float timer;

        public BossStunState(Boss boss) : base(boss) { }

        public override bool CanStagger
        {
            get { return false; }
        }

        protected override void OnEnter()
        {
            timer = Boss.StunTime;
            Boss.PlayAnim(Boss.StunAnim);
        }

        protected override void OnTick(float deltaTime)
        {
            KeepAnim(Boss.StunAnim);

            timer -= deltaTime;
            if (timer > 0f)
                return;

            Boss.ReturnToMove();
        }
    }
}
