namespace Study3D.FpsGame.Enemy.States
{
    // 이동: 플레이어 방향으로 달려갑니다.
    // - 판단 순서: 쓸 수 있는 스킬 → 공격 거리 안이면 공격 → 감지 거리를 벗어나면(맞은 적이 없을 때만) 아이들 → 추격 이동
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

            // 쓸 수 있는 스킬이 있으면 공격보다 먼저 씁니다. (거리, 시야 같은 조건은 스킬마다 CanUse 에서 판단)
            if (Enemy.TryPickSkill())
            {
                Enemy.ChangeState<EnemySkillState>();
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
