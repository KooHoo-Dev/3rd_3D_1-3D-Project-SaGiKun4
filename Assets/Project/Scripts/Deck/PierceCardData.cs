using Study3D.FpsGame.Weapon.Cards;
using UnityEngine;

namespace Study3D.FpsGame.Deck
{
    // 관통 반응 카드: 부딪힌 뒤에도 소멸하지 않고 운동량을 유지한 채 통과합니다. (충돌 시점용)
    // - 기본 대상은 적입니다. Target에 구조물(Prop)을 넣으면 상자, 기둥 등도 통과합니다.
    // - 맵(World: 벽, 바닥)은 뚫으면 맵 밖으로 나가므로 Target에 넣어도 통과하지 않습니다. (맵에서는 뒤의 반사 카드 등이 반응)
    // - 횟수 제한 없이 계속 관통합니다. 그래서 같은 대상을 노리는 반응 카드를 관통 뒤에 쌓으면 그 카드는 발동하지 않습니다.
    [CreateAssetMenu(menuName = "Card/Pierce Card", fileName = "PierceCard")]
    public class PierceCardData : ResponseCardData
    {
        protected override ContactTarget DefaultTarget => ContactTarget.Enemy;

        // 맵(World)은 관통할 수 없습니다.
        protected override ContactTarget AllowedTargets
        {
            get { return ContactTarget.Enemy | ContactTarget.Prop; }
        }

        protected override void OnHit(CardContext context)
        {
            if (TryRespond(context) == false)
                return;

            context.Projectile.PassThrough();
        }
        
        public override string Description {
            get { return $"{TargetText(Target)} 충돌시, \n꿰뚫고 지나갑니다."; }
        }

        // 실제 대상(Target)으로 적습니다. (나중에 대상이 바뀌어도 설명이 따라가도록)
        // 대상은 강화로 바뀌는 값이라 파란색으로 감싸고, 조사(와/과)는 기본색으로 둡니다.
        private static string TargetText(ContactTarget target)
        {
            switch (target)
            {
                case ContactTarget.Enemy:
                    return ValueText("적") + "과";
                case ContactTarget.Prop:
                    return ValueText("구조체") + "와";
                case ContactTarget.Enemy | ContactTarget.Prop:
                    return ValueText("적, 구조체") + "와";
                default:
                    return ValueText("구조체") + "와";
            }
        }
    }
}
