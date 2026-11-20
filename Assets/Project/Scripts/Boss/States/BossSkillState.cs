namespace Study3D.FpsGame.Boss.States
{
    // 스킬: 추격/공전 중에 고른 스킬(Boss.CurrentSkill)을 실행합니다.
    // - 스킬이 끝나면 거리에 따라 추격이나 공전으로 돌아갑니다.
    // - 끝나기 전에 다른 상태로 나가면(경직, 기절, 사망) 스킬을 취소합니다. 취소하면 쿨타임이 돌지 않습니다. (BossSkill)
    public class BossSkillState : BossState
    {
        public BossSkillState(Boss boss) : base(boss) { }

        protected override void OnEnter()
        {
            Boss.CurrentSkill.Begin(Boss);
        }

        protected override void OnTick(float deltaTime)
        {
            BossSkill skill = Boss.CurrentSkill;
            skill.Execute(Boss, deltaTime);

            // 스킬이 실행 중에 직접 다른 상태로 바꿨으면(마법 파훼 → 기절 등) 이미 이 상태를 나갔으므로 손대지 않습니다.
            if (Boss.CurrentSkill != skill)
                return;

            if (skill.IsFinish == false)
                return;

            Boss.ReturnToMove();
        }

        protected override void OnExit()
        {
            BossSkill skill = Boss.CurrentSkill;
            if (skill != null && skill.IsFinish == false)
            {
                skill.Cancel(Boss);
            }
            Boss.ClearSkill();
        }
    }
}
