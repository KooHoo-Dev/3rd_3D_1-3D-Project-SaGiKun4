namespace Study3D.FpsGame.Boss.States
{
    // 대기: 전투가 시작되기 전, 또는 플레이어가 없을 때 제자리에 서 있습니다.
    // - 보스방이 전투를 시작하거나(Boss.StartBattle), 플레이어가 감지 거리에 들어오면 전투를 시작합니다.
    public class BossIdleState : BossState
    {
        public BossIdleState(Boss boss) : base(boss) { }

        protected override void OnEnter()
        {
            Boss.PlayAnim(Boss.IdleAnim);
        }

        protected override void OnTick(float deltaTime)
        {
            KeepAnim(Boss.IdleAnim);

            if (Boss.HasTarget == false)
                return;

            if (Boss.IsBattleStarted == false)
            {
                if (Boss.IsPlayerInDetectRange() == false)
                    return;

                Boss.StartBattle();
            }

            Boss.ReturnToMove();
        }
    }
}
