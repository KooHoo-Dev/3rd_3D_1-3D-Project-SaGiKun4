using Study3D.FpsGame.Weapon.Cards;
using UnityEngine;

namespace Study3D.FpsGame.Deck
{
    // 관통 강화 카드: 적을 맞힌 뒤에도 소멸하지 않고 통과해서 뒤쪽 적까지 맞힐 수 있게 합니다. (충돌 시점용)
    // - 적에게만 적용됩니다. 벽은 관통하지 않습니다.
    // - 같은 충돌에서 앞 카드가 이미 진행 방향을 정했으면 횟수를 쓰지 않고 넘어갑니다.
    //   (관통 2장이면 첫 적은 앞 카드, 다음 적은 뒤 카드가 맡아서 결과적으로 2명을 통과합니다)
    // - 횟수는 Effect 배율을 받습니다.
    [CreateAssetMenu(menuName = "Card/Pierce Card", fileName = "PierceCard")]
    public class PierceCardData : CardData
    {
        [SerializeField] private int pierceCount = 1; // 이 카드 한 장으로 통과할 수 있는 적 수

        public override CardType Type => CardType.Enhance;

        public override int CreateCount(CardMultiplier multiplier) => CeilMultiply(pierceCount, multiplier.Effect);

        protected override void OnHit(CardContext context)
        {
            if (context.IsEnemy == false || context.Count <= 0) return;
            if (context.Projectile.TryLockDirection() == false) return;

            context.Count--;
            context.Projectile.PassThrough();
        }
    }
}
