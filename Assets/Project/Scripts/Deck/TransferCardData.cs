using Study3D.FpsGame.Weapon.Cards;
using UnityEngine;

namespace Study3D.FpsGame.Deck
{
    // 전이 조향 카드: 적을 맞힌 순간, 반경 안의 다른 적을 향해 진행 방향을 바꿉니다. (충돌 시점용)
    // - 반사가 지형과 입사각을 이용한다면, 전이는 적의 배치를 이용합니다.
    // - 방금 맞힌 적은 제외하고, 벽에 가려지지 않은 가장 가까운 적을 고릅니다.
    //   (직전 적만 제외하므로 적 둘 사이를 오갈 수 있고, 횟수가 이를 끝냅니다)
    // - 다음 적을 못 찾으면 횟수를 쓰지 않습니다. 관통이 있으면 직진하고, 없으면 소멸합니다.
    // - 횟수는 Effect 배율을 받습니다.
    [CreateAssetMenu(menuName = "Card/Transfer Card", fileName = "TransferCard")]
    public class TransferCardData : SteerCardData
    {
        [SerializeField] private int transferCount = 1; // 이 카드 한 장으로 옮겨갈 수 있는 횟수
        [SerializeField] private float radius = 8.0f;   // 다음 적을 찾는 반경

        public override int CreateCount(CardMultiplier multiplier)
        {
            return CeilMultiply(transferCount, multiplier.Effect);
        }

        protected override void OnHit(CardContext context)
        {
            if (context.IsEnemy == false || context.Count <= 0)
                return;

            Collider next = FindNearestEnemy(context.Center, radius, context.Projectile.LastHitTarget);
            if (next == null)
                return;

            context.Count--;
            context.Projectile.Steer(context, next.bounds.center - context.Center);
        }
    }
}
