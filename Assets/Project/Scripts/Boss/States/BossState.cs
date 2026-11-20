using Jay.FSM;
using UnityEngine;

namespace Study3D.FpsGame.Boss.States
{
    // BossState
    // : 보스 FSM 상태들의 부모 입니다. (EnemyState 와 같은 구조)
    // - 애니메이터는 상태를 바꾼 다음 프레임에야 새 상태 정보를 알려주므로,
    //   들어온 프레임에는 애니메이션 종료 판정을 하지 않습니다.
    public abstract class BossState : IUpdatableState
    {
        protected Boss Boss { get; }

        private int enterFrame;

        protected BossState(Boss boss)
        {
            Boss = boss;
        }

        // false 면 이 상태에서는 경직 기준선을 지나도 경직되지 않습니다. (기절, 사망)
        public virtual bool CanStagger
        {
            get { return true; }
        }

        public void Enter()
        {
            enterFrame = Time.frameCount;
            OnEnter();
        }

        public void Exit()
        {
            OnExit();
        }

        public void Tick(float deltaTime)
        {
            OnTick(deltaTime);
        }

        protected virtual void OnEnter() { }
        protected virtual void OnExit() { }
        protected virtual void OnTick(float deltaTime) { }

        protected bool IsAnimEnded(int stateHash)
        {
            return Time.frameCount > enterFrame && Boss.IsAnimEnded(stateHash);
        }

        protected void KeepAnim(int stateHash)
        {
            if (Time.frameCount > enterFrame)
            {
                Boss.KeepAnim(stateHash);
            }
        }
    }
}
