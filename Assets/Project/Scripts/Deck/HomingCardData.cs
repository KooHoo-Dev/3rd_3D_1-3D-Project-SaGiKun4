using Study3D.FpsGame.Weapon.Cards;
using UnityEngine;

namespace Study3D.FpsGame.Deck
{
    // 유도 조향 카드: 날아가는 동안 가까운 적을 향해 제한된 속도로 방향을 틉니다. (진행 중 시점용)
    // - 전이가 적중 뒤에 다음 적을 고른다면, 유도는 적중 전에 경로를 보정합니다.
    // - 직전에 맞힌 적은 쫓지 않습니다. 벽에 부딪히면 기록이 비워지므로 반사 뒤에는 같은 적을 다시 쫓을 수 있습니다.
    // - 쫓을 적이 없을 때만 searchInterval 프레임마다 찾습니다. (발사체마다 매 프레임 찾지 않도록)
    // - 회전 속도는 Effect 배율을 받습니다.
    //   회전 속도에 비해 발사체가 너무 빠르면 목표 주위를 맴돌 수 있습니다. 끝나지 않으면 발사체의 최대 수명이 끝냅니다.
    [CreateAssetMenu(menuName = "Card/Homing Card", fileName = "HomingCard")]
    public class HomingCardData : SteerCardData
    {
        [SerializeField] private float radius = 15.0f;      // 쫓을 적을 찾는 반경
        [SerializeField] private float turnSpeed = 360.0f;  // 초당 회전 각도
        [SerializeField] private int searchInterval = 5;    // 쫓을 적이 없을 때 다시 찾기까지의 프레임 수
        [SerializeField] private float loseRange = 1.5f;    // 쫓던 적이 radius의 이 배수보다 멀어지면 놓칩니다

        // Count: 다음 탐색까지 남은 프레임 수. 처음엔 0이라 발사하자마자 찾습니다.

        protected override void OnFlying(CardContext context)
        {
            Projectile projectile = context.Projectile;
            Collider target = projectile.SteerTarget;

            if (IsChasing(projectile, target, context.Center) == false)
            {
                target = SearchNewTarget(context, projectile);
                if (target == null)
                    return;
            }

            TurnToward(context, projectile, target);
        }

        // 쫓던 적을 놓쳤으면 searchInterval 프레임마다 새 적을 찾습니다. 기다리는 중이거나 못 찾았으면 null
        private Collider SearchNewTarget(CardContext context, Projectile projectile)
        {
            projectile.SteerTarget = null;
            if (context.Count > 0)
            {
                context.Count--;
                return null;
            }

            context.Count = searchInterval;
            Collider target = FindNearestEnemy(context.Center, radius, projectile.LastHitTarget);
            if (target == null)
                return null;

            projectile.SteerTarget = target;
            return target;
        }

        // 제한된 회전 속도로 target을 향해 진행 방향을 틉니다.
        private void TurnToward(CardContext context, Projectile projectile, Collider target)
        {
            float maxRadians = turnSpeed * context.Multiplier.Effect * Mathf.Deg2Rad * Time.deltaTime;
            Vector3 direction = Vector3.RotateTowards(context.Forward, target.bounds.center - context.Center, maxRadians, 0f);
            projectile.Steer(context, direction);
        }

        // 쫓던 적을 계속 쫓을 수 있는지: 살아 있고(Collider가 켜져 있음), 직전에 맞힌 적이 아니고, 너무 멀어지지 않았는지
        private bool IsChasing(Projectile projectile, Collider target, Vector3 origin)
        {
            if (target == null || target.enabled == false || target.gameObject.activeInHierarchy == false)
                return false;
            if (CombatSystem.Instance.GetMonsterOrNull(target) == projectile.LastHitTarget)
                return false;

            float maxRange = radius * loseRange;
            return (target.bounds.center - origin).sqrMagnitude <= maxRange * maxRange;
        }
    }
}
