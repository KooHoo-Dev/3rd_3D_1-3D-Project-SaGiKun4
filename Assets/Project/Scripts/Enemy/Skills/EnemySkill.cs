using UnityEngine;

namespace Study3D.FpsGame.Enemy
{
    // EnemySkill
    // : 적 스킬의 부모 입니다. 적 프리팹에 컴포넌트로 붙인 스킬만 그 적이 사용합니다. (2D 프로젝트의 BirdSkill 구조)
    // - Enemy 가 Awake 에서 붙어 있는 스킬을 모으고, 추격 중에 위에서부터 순서대로 CanUse 를 물어 처음 쓸 수 있는 스킬을 고릅니다.
    //   (우선순위 = 인스펙터의 컴포넌트 순서)
    // - 고른 스킬은 EnemySkillState 가 Begin → 매 프레임 Execute → 끝나면(IsFinish) 추격으로 돌아갑니다.
    // - 쿨타임은 "다시 쓸 수 있는 게임 시각(readyTime)" 을 저장해서 비교합니다.
    //   타이머를 깎는 방식이 아니라서 어떤 상태에 있든, 게임이 멈춰 있든 정확하게 흐릅니다.
    // - 스킬이 끝나든 중간에 끊기든(사망 등) 쿨타임이 시작됩니다.
    public abstract class EnemySkill : MonoBehaviour
    {
        [Tooltip("다시 쓸 수 있을 때까지 걸리는 시간 (초, 게임 시간 기준)")]
        [SerializeField] private float coolTime = 8f;
        [Tooltip("켜면 이 스킬을 쓰는 동안 맞아도 경직되지 않습니다 (체력은 깎임)")]
        [SerializeField] private bool ignoreHit = true;

        private float readyTime;

        public bool IsFinish { get; private set; }

        public bool IgnoreHit
        {
            get { return ignoreHit; }
        }

        // 풀에서 다시 꺼내지면 쿨타임 없이 시작합니다.
        protected virtual void OnEnable()
        {
            readyTime = 0f;
            IsFinish = true;
        }

        // 쿨타임이 끝났고 스킬별 조건(거리, 시야 등)도 맞으면 true
        public bool CanUse(Enemy enemy)
        {
            if (GameTime.Time < readyTime)
                return false;

            return CheckCondition(enemy);
        }

        public void Begin(Enemy enemy)
        {
            IsFinish = false;
            OnBegin(enemy);
        }

        public void Execute(Enemy enemy, float deltaTime)
        {
            if (IsFinish)
                return;

            OnExecute(enemy, deltaTime);
        }

        // 끝나기 전에 상태가 바뀌면(사망, 경직 등) 불립니다. 스킬이 켜 둔 것(판정 등)을 정리합니다.
        public void Cancel(Enemy enemy)
        {
            if (IsFinish)
                return;

            OnCancel(enemy);
            EndSkill();
        }

        // 스킬이 스스로 끝날 때 자식이 부릅니다.
        protected void FinishSkill()
        {
            EndSkill();
        }

        private void EndSkill()
        {
            IsFinish = true;
            readyTime = GameTime.Time + coolTime;
        }

        protected abstract bool CheckCondition(Enemy enemy);
        protected abstract void OnBegin(Enemy enemy);
        protected abstract void OnExecute(Enemy enemy, float deltaTime);

        protected virtual void OnCancel(Enemy enemy) { }
    }
}
