namespace Study3D.FpsGame.Enemy.States
{
    // 등장: 맵 밖 스폰 위치에서 맵 안쪽 도착 지점(EndPoint)까지 걸어 들어옵니다. (방 스포너로 꺼낸 적만)
    // - 플레이어를 무시하고 도착 지점으로 직진합니다. (맵 밖은 지도가 없어서 길찾기를 쓰지 않음)
    // - 도착하면 어그로를 켜고 추격합니다. (감지 거리와 상관없이 끝까지 쫓아옴)
    // - 등장 중에 맞으면 체력만 깎이고 계속 걸어옵니다. (Enemy.TakeDamage)
    public class EnemyEnterState : EnemyState
    {
        public EnemyEnterState(Enemy enemy) : base(enemy) { }

        protected override void OnEnter()
        {
            Enemy.PlayAnim(Enemy.RunAnim);
        }

        protected override void OnTick(float deltaTime)
        {
            KeepAnim(Enemy.RunAnim);

            if (Enemy.MoveToEntrance(deltaTime) == false)
                return;

            Enemy.FinishEntrance();
            Enemy.ChangeState<EnemyChaseState>();
        }
    }
}
