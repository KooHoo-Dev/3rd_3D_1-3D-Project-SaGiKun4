using Study3D.FpsGame.Weapon.Cards;
using UnityEngine;

namespace Study3D.FpsGame.Deck
{
    // 관통 반응 카드: 부딪힌 뒤에도 소멸하지 않고 운동량을 유지한 채 통과합니다. (충돌 시점용)
    // - 기본 대상은 적입니다. Target에 벽을 넣으면 벽도 통과합니다. (맵 밖으로 나간 발사체는 최대 수명이 끝냅니다)
    // - 횟수 제한 없이 계속 관통합니다. 그래서 같은 대상을 노리는 반응 카드를 관통 뒤에 쌓으면 그 카드는 발동하지 않습니다.
    [CreateAssetMenu(menuName = "Card/Pierce Card", fileName = "PierceCard")]
    public class PierceCardData : ResponseCardData
    {
        protected override ContactTarget DefaultTarget => ContactTarget.Enemy;

        protected override void OnHit(CardContext context)
        {
            if (TryRespond(context) == false)
                return;

            context.Projectile.PassThrough();
        }
    }
}
