namespace Study3D.FpsGame.Enemy.States
{
    // 이동: 플레이어 방향으로 달려갑니다.
    // - 공격 거리 안이면 공격, 감지 거리를 벗어나면(맞은 적이 없을 때만) 아이들로 돌아갑니다.
    public class EnemyChaseState : EnemyState
    {
        public EnemyChaseState(Enemy enemy) : base(enemy) { }

        protected override void OnEnter()
        {
            Enemy.PlayAnim(Enemy.RunAnim);
        }

        protected override void OnTick(float deltaTime)
        {
            KeepAnim(Enemy.RunAnim);

            if (Enemy.HasTarget == false)
            {
                Enemy.ChangeState<EnemyIdleState>();
                return;
            }

            float distance = Enemy.DistanceToPlayer;
            if (distance <= Enemy.AttackRange)
            {
                Enemy.ChangeState<EnemyAttackState>();
                return;
            }

            if (Enemy.IsAggro == false && distance > Enemy.DetectRange)
            {
                Enemy.ChangeState<EnemyIdleState>();
                return;
            }

            Enemy.MoveToPlayer(deltaTime);
        }
    }
}
