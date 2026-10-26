namespace Study3D.FpsGame.Enemy.States
{
    // 죽음: 체력이 0이 되면 들어오며, 다른 상태로 나가지 않습니다.
    // - 사망 애니메이션이 끝나면 사라집니다. (Enemy.Despawn)
    public class EnemyDieState : EnemyState
    {
        private bool isDespawned;

        public EnemyDieState(Enemy enemy) : base(enemy) { }

        protected override void OnEnter()
        {
            isDespawned = false;
            Enemy.OnDeath();
            Enemy.PlayAnim(Enemy.DieAnim);
        }

        protected override void OnTick(float deltaTime)
        {
            if (isDespawned || IsAnimEnded(Enemy.DieAnim) == false) return;

            isDespawned = true;
            Enemy.Despawn();
        }
    }
}
