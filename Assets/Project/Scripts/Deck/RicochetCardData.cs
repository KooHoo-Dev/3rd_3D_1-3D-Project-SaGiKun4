using Study3D.FpsGame.Data;
using Study3D.FpsGame.Weapon.Cards;
using UnityEngine;

namespace Study3D.FpsGame.Deck
{
    // 반사 반응 카드: 부딪혔을 때 소멸하지 않고 맞은 면 기준으로 튕겨나갑니다. (충돌 시점용)
    // - 기본 대상은 맵(벽, 바닥)과 구조물입니다. Target에 적을 넣으면 적에게도 튕기고, 그만큼 횟수를 씁니다.
    // - 횟수를 다 썼으면 결정권을 가져가지 않으므로, 뒤에 쌓인 반응 카드가 이어서 맡을 수 있습니다.
    // - 횟수는 스펙 표의 Count 이고, Effect 배율을 받습니다.
    public class RicochetCardData : ResponseCardData
    {
        public RicochetCardData(CardDef def) : base(def)
        {
        }

        // 이 카드 한 장으로 튕겨나갈 수 있는 횟수
        private int BounceCount
        {
            get { return Spec.Count; }
        }

        public override int CreateCount(CardMultiplier multiplier)
        {
            return CeilMultiply(BounceCount, multiplier.Effect);
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
        
        public override string Description {
            get { return $"카드가 {ValueText(BounceCount)}회 한해 \n{TargetText(Target)} 충돌시 튕겨져 나옵니다"; }
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
