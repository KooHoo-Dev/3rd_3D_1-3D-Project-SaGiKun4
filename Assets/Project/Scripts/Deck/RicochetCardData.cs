using Study3D.FpsGame.Weapon.Cards;
using UnityEngine;

namespace Study3D.FpsGame.Deck
{
    // 반사 반응 카드: 부딪혔을 때 소멸하지 않고 맞은 면 기준으로 튕겨나갑니다. (충돌 시점용)
    // - 기본 대상은 벽입니다. Target에 적을 넣으면 적에게도 튕기고, 그만큼 횟수를 씁니다.
    // - 횟수를 다 썼으면 결정권을 가져가지 않으므로, 뒤에 쌓인 반응 카드가 이어서 맡을 수 있습니다.
    // - 횟수는 Effect 배율을 받습니다.
    [CreateAssetMenu(menuName = "Card/Ricochet Card", fileName = "RicochetCard")]
    public class RicochetCardData : ResponseCardData
    {
        [SerializeField] private int bounceCount = 1; // 이 카드 한 장으로 튕겨나갈 수 있는 횟수

        protected override ContactTarget DefaultTarget => ContactTarget.World;

        public override int CreateCount(CardMultiplier multiplier)
        {
            return CeilMultiply(bounceCount, multiplier.Effect);
        }

        protected override void OnHit(CardContext context)
        {
            if (context.Count <= 0)
                return;
            if (TryRespond(context) == false)
                return;

            context.Count--;
            Vector3 reflected = Vector3.Reflect(context.Forward, context.Normal);
            context.Projectile.Redirect(context.Point, context.Normal, reflected);
        }
    }
}
