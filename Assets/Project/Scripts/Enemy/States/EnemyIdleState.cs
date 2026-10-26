namespace Study3D.FpsGame.Enemy.States
{
    // 아이들: 아무것도 하지 않다가 플레이어가 감지 거리 안에 들어오면 추격합니다.
    // (피격으로 인한 추격은 Hit 상태가 끝날 때 넘어갑니다)
    public class EnemyIdleState : EnemyState
    {
        public EnemyIdleState(Enemy enemy) : base(enemy) { }

        protected override void OnEnter()
        {
            Enemy.PlayAnim(Enemy.IdleAnim);
        }

        protected override void OnTick(float deltaTime)
        {
            KeepAnim(Enemy.IdleAnim);

            if (Enemy.HasTarget == false) return;
            if (Enemy.IsAggro || Enemy.DistanceToPlayer <= Enemy.DetectRange)
            {
                Enemy.ChangeState<EnemyChaseState>();
            }
        }
    }
}
