using UnityEngine;

namespace Study3D.FpsGame.Player
{
    // LocalPlayer - 넉백
    // : 적에게 밀려나는 양을 계산합니다. 실제 이동은 PlayerController 가 CharacterController.Move 로 처리합니다.
    //   (Move 를 한 곳(UpdateMovement)에서만 불러야 바닥 판정이 꼬이지 않기 때문)
    // - Knockback(방향, 거리, 시간): 정해진 시간 동안 정해진 거리만큼 밀려납니다.
    // - 처음에 빠르게 밀리고 점점 느려집니다. (밀리는 속도가 시간에 따라 일정하게 줄어듦)
    //   지금까지 밀린 비율 = 1 - (1 - 경과 비율)²  → 경과 비율 0 이면 0, 1 이면 1
    // - 밀리는 동안에도 조작은 그대로 됩니다. 벽에 닿으면 CharacterController 가 막아 줍니다.
    // - 밀리는 중에 또 맞으면 새 넉백으로 덮어씁니다.
    public partial class LocalPlayer
    {
        private Vector3 knockbackDirection;
        private float knockbackDistance;
        private float knockbackDuration;
        private float knockbackElapsed;

        public bool IsKnockedBack
        {
            get { return knockbackElapsed < knockbackDuration; }
        }

        public void Knockback(Vector3 direction, float distance, float duration)
        {
            direction.y = 0f;
            if (IsDead || direction.sqrMagnitude < 0.0001f || distance <= 0f || duration <= 0f)
                return;

            knockbackDirection = direction.normalized;
            knockbackDistance = distance;
            knockbackDuration = duration;
            knockbackElapsed = 0f;
        }

        // 이번 프레임에 밀려날 이동량을 돌려주고 시간을 진행합니다. 넉백 중이 아니면 0 입니다.
        public Vector3 ConsumeKnockbackStep(float deltaTime)
        {
            if (IsKnockedBack == false)
                return Vector3.zero;

            float before = GetKnockbackMovedRatio(knockbackElapsed);
            knockbackElapsed = Mathf.Min(knockbackElapsed + deltaTime, knockbackDuration);
            float after = GetKnockbackMovedRatio(knockbackElapsed);

            return knockbackDirection * (knockbackDistance * (after - before));
        }

        private float GetKnockbackMovedRatio(float elapsed)
        {
            float remain = 1f - (elapsed / knockbackDuration);
            return 1f - (remain * remain);
        }
    }
}
