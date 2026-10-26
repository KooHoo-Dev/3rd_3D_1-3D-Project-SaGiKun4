using Study3D.FpsGame.Weapon.Cards;
using UnityEngine;

namespace Study3D.FpsGame.Deck
{
    // 반사 강화 카드: 적이나 벽에 부딪혔을 때 소멸하지 않고 맞은 면 기준으로 튕겨나갑니다. (충돌 시점용)
    // - 같은 충돌에서 앞 카드가 이미 진행 방향을 정했으면 횟수를 쓰지 않고 넘어갑니다.
    // - 횟수는 Effect 배율을 받습니다.
    [CreateAssetMenu(menuName = "Card/Ricochet Card", fileName = "RicochetCard")]
    public class RicochetCardData : CardData
    {
        [SerializeField] private int bounceCount = 1; // 이 카드 한 장으로 튕겨나갈 수 있는 횟수

        public override CardType Type => CardType.Enhance;

        public override int CreateCount(CardMultiplier multiplier) => CeilMultiply(bounceCount, multiplier.Effect);

        protected override void OnHit(CardContext context)
        {
            if (context.Count <= 0) return;
            if (context.Projectile.TryLockDirection() == false) return;

            context.Count--;
            context.Projectile.Redirect(context.Point, context.Normal, Vector3.Reflect(context.Forward, context.Normal));
        }
    }
}
