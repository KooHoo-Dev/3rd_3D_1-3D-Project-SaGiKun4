using UnityEngine;

namespace Study3D.FpsGame.Boss.States
{
    // 등장: 보스방이 보스를 소환하면 바닥 아래에서 소환 위치까지 올라옵니다. (Boss.Appear)
    // - Boss.Appear 가 보스를 AppearDepth 만큼 아래로 내려 두고, 이 상태가 AppearTime 동안 일정한 속도로 끌어올립니다.
    // - 올라오는 동안에는 Collider 를 꺼서 카드에 맞지 않고, 경직되지도 않습니다.
    // - 다 올라오면 전투를 시작하고(HP바 표시) 거리에 따라 추격이나 공전으로 넘어갑니다.
    public class BossAppearState : BossState
    {
        private Vector3 startPosition;
        private Vector3 endPosition;
        private float elapsed;

        public BossAppearState(Boss boss) : base(boss) { }

        public override bool CanStagger
        {
            get { return false; }
        }

        protected override void OnEnter()
        {
            startPosition = Boss.transform.position;
            endPosition = Boss.AppearEndPosition;
            elapsed = 0f;

            Boss.MainCollider.enabled = false;
            Boss.PlayAnim(Boss.IdleAnim);
        }

        protected override void OnTick(float deltaTime)
        {
            KeepAnim(Boss.IdleAnim);

            elapsed += deltaTime;
            float t = Mathf.Clamp01(elapsed / Mathf.Max(0.01f, Boss.AppearTime));
            Boss.transform.position = Vector3.Lerp(startPosition, endPosition, t);
            if (t < 1f)
                return;

            Boss.StartBattle();
            Boss.ReturnToMove();
        }

        protected override void OnExit()
        {
            Boss.MainCollider.enabled = true;
        }
    }
}
