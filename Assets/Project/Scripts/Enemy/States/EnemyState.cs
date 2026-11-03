using Jay.FSM;
using UnityEngine;

namespace Study3D.FpsGame.Enemy.States
{
    // EnemyState
    // : 적 FSM 상태들의 부모 입니다.
    // - 애니메이터는 상태를 바꾼 다음 프레임에야 새 상태 정보를 알려주므로,
    //   들어온 프레임에는 애니메이션 종료 판정을 하지 않습니다.
    public abstract class EnemyState : IUpdatableState
    {
        protected Enemy Enemy { get; }

        private int enterFrame;

        protected EnemyState(Enemy enemy)
        {
            Enemy = enemy;
        }

        // true 면 이 상태에서는 맞아도 경직(Hit 상태)으로 넘어가지 않습니다. 체력은 그대로 깎입니다. (Enemy.TakeDamage)
        public virtual bool IgnoreHitReaction
        {
            get { return false; }
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
            return Time.frameCount > enterFrame && Enemy.IsAnimEnded(stateHash);
        }

        protected void KeepAnim(int stateHash)
        {
            if (Time.frameCount > enterFrame) Enemy.KeepAnim(stateHash);
        }
    }
}
