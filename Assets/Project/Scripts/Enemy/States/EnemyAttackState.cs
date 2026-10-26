namespace Study3D.FpsGame.Enemy.States
{
    // 공격: Attack01을 재생합니다.
    // - 칼(Sword) 판정은 Attack01 애니메이션이 BoxCollider를 켜고 꺼서 처리하므로, 여기서는 무기를 모릅니다.
    // - 휘두르기가 끝났을 때 아직 공격 거리 안이면 다시 휘두르고, 아니면 추격합니다.
    public class EnemyAttackState : EnemyState
    {
        public EnemyAttackState(Enemy enemy) : base(enemy) { }

        protected override void OnEnter()
        {
            Enemy.PlayAnim(Enemy.AttackAnim);
        }

        protected override void OnTick(float deltaTime)
        {
            Enemy.LookAtPlayer(deltaTime);

            if (IsAnimEnded(Enemy.AttackAnim) == false) return;

            if (Enemy.HasTarget && Enemy.DistanceToPlayer <= Enemy.AttackRange)
            {
                Enter(); // 처음부터 다시 휘두르기
                return;
            }

            Enemy.ChangeState<EnemyChaseState>();
        }
    }
}
