using UnityEngine;

namespace Study3D.FpsGame.Boss
{
    // BossSkill
    // : 보스 스킬의 부모 입니다. 보스 프리팹에 컴포넌트로 붙인 스킬만 사용합니다. (EnemySkill 과 같은 구조)
    // - Boss 가 Awake 에서 붙어 있는 스킬을 모으고, 추격/공전 중에 위에서부터 순서대로 CanUse 를 물어 처음 쓸 수 있는 스킬을 고릅니다.
    //   (우선순위 = 인스펙터의 컴포넌트 순서)
    // - 고른 스킬은 BossSkillState 가 Begin → 매 프레임 Execute → 끝나면(IsFinish) 추격으로 돌아갑니다.
    // - 쿨타임은 "다시 쓸 수 있는 게임 시각(readyTime)" 을 저장해서 비교합니다.
    // - EnemySkill 과 다른 점: 끝까지 사용해야(FinishSkill) 쿨타임이 시작됩니다.
    //   경직 등으로 중간에 끊기면(Cancel) 쿨타임이 돌지 않으므로, 다음 판단 때 우선순위대로 다시 골라집니다.
    //   단, 핵심 동작(발사, 던지기 등)이 이미 나간 뒤라면 자식이 StartCoolTime 으로 그때부터 쿨타임을 흘립니다.
    public abstract class BossSkill : MonoBehaviour
    {
        [Tooltip("다시 쓸 수 있을 때까지 걸리는 시간 (초, 게임 시간 기준). 끝까지 사용한 뒤부터 흐릅니다")]
        [SerializeField] private float coolTime = 10f;

        private float readyTime;
        private bool isCoolTimeStarted; // 이번 사용에서 쿨타임을 이미 시작했는지 (StartCoolTime)

        public bool IsFinish { get; private set; }

        // 컴포넌트를 처음 붙였을 때 들어갈 쿨타임. 스킬마다 기획 값으로 재정의합니다.
        protected virtual float DefaultCoolTime
        {
            get { return 10f; }
        }

        // 에디터에서 컴포넌트를 처음 붙이거나 Reset 할 때 불립니다.
        private void Reset()
        {
            coolTime = DefaultCoolTime;
        }

        // 풀에서 다시 꺼내지면 쿨타임 없이 시작합니다.
        protected virtual void OnEnable()
        {
            readyTime = 0f;
            IsFinish = true;
        }

        // 쿨타임이 끝났고 스킬별 조건(거리 등)도 맞으면 true
        public bool CanUse(Boss boss)
        {
            if (GameTime.Time < readyTime)
                return false;

            return CheckCondition(boss);
        }

        public void Begin(Boss boss)
        {
            IsFinish = false;
            isCoolTimeStarted = false;
            OnBegin(boss);
        }

        public void Execute(Boss boss, float deltaTime)
        {
            if (IsFinish)
                return;

            OnExecute(boss, deltaTime);
        }

        // 끝나기 전에 상태가 바뀌면(경직, 사망 등) 불립니다. 스킬이 켜 둔 것(판정 등)을 정리합니다.
        // 쿨타임은 시작하지 않습니다.
        public void Cancel(Boss boss)
        {
            if (IsFinish)
                return;

            OnCancel(boss);
            IsFinish = true;
        }

        // 스킬이 끝까지 사용됐을 때 자식이 부릅니다. 이때부터 쿨타임이 흐릅니다.
        protected void FinishSkill()
        {
            IsFinish = true;
            StartCoolTime();
        }

        // 스킬의 핵심 동작(카드 발사, 돌 던지기 등)이 이미 나갔을 때 자식이 부릅니다.
        // 그 뒤에 남은 모션이 경직으로 끊겨도 같은 스킬을 한 번 더 쓰지 않도록, 이 시점부터 쿨타임을 흘립니다.
        // (한 번 사용에 한 번만 시작합니다. 끝날 때 FinishSkill 이 다시 시작하지 않음)
        protected void StartCoolTime()
        {
            if (isCoolTimeStarted)
                return;

            isCoolTimeStarted = true;
            readyTime = GameTime.Time + coolTime;
        }

        protected abstract bool CheckCondition(Boss boss);
        protected abstract void OnBegin(Boss boss);
        protected abstract void OnExecute(Boss boss, float deltaTime);

        protected virtual void OnCancel(Boss boss) { }
    }
}
