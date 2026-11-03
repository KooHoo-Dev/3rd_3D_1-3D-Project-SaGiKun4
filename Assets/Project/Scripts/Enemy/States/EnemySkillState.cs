namespace Study3D.FpsGame.Enemy.States
{
    // 스킬: 추격 중에 고른 스킬(Enemy.CurrentSkill)을 실행합니다. (2D 프로젝트 Attack() 의 스킬 실행 부분)
    // - 스킬이 끝나면 추격으로 돌아갑니다. (대상이 없으면 아이들)
    // - 끝나기 전에 다른 상태로 나가면(사망, 경직) 스킬을 취소합니다. 취소해도 쿨타임은 시작됩니다.
    // - 스킬의 IgnoreHit 이 켜져 있으면 맞아도 경직되지 않습니다. (Enemy.TakeDamage)
    public class EnemySkillState : EnemyState
    {
        public EnemySkillState(Enemy enemy) : base(enemy) { }

        public override bool IgnoreHitReaction
        {
            get
            {
                if (Enemy.CurrentSkill == null)
                    return false;

                return Enemy.CurrentSkill.IgnoreHit;
            }
        }

        protected override void OnEnter()
        {
            Enemy.CurrentSkill.Begin(Enemy);
        }

        protected override void OnTick(float deltaTime)
        {
            EnemySkill skill = Enemy.CurrentSkill;
            skill.Execute(Enemy, deltaTime);
            if (skill.IsFinish == false)
                return;

            if (Enemy.HasTarget)
            {
                Enemy.ChangeState<EnemyChaseState>();
            }
            else
            {
                Enemy.ChangeState<EnemyIdleState>();
            }
        }

        protected override void OnExit()
        {
            EnemySkill skill = Enemy.CurrentSkill;
            if (skill != null && skill.IsFinish == false)
            {
                skill.Cancel(Enemy);
            }
            Enemy.ClearSkill();
        }
    }
}
