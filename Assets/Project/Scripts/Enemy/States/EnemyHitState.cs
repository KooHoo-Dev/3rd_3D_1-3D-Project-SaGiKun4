namespace Study3D.FpsGame.Enemy.States
{
    // 피격: 어떤 상태든 체력이 줄면 들어옵니다. (Enemy.TakeDamage)
    // - 피격 모션이 끝나면 맞은 상대를 쫓아 추격 상태로 넘어갑니다.
    public class EnemyHitState : EnemyState
    {
        public EnemyHitState(Enemy enemy) : base(enemy) { }

        protected override void OnEnter()
        {
            Enemy.PlayAnim(Enemy.HitAnim);
        }

        protected override void OnTick(float deltaTime)
        {
            if (IsAnimEnded(Enemy.HitAnim) == false) return;

            if (Enemy.HasTarget) Enemy.ChangeState<EnemyChaseState>();
            else Enemy.ChangeState<EnemyIdleState>();
        }
    }
}
