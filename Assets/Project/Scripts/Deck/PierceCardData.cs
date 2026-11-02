using Study3D.FpsGame.Data;
using Study3D.FpsGame.Weapon.Cards;
using UnityEngine;

namespace Study3D.FpsGame.Deck
{
    // 관통 반응 카드: 부딪힌 뒤에도 소멸하지 않고 운동량을 유지한 채 통과합니다. (충돌 시점용)
    // - 기본 대상은 적입니다. Target에 구조물(Prop)을 넣으면 상자, 기둥 등도 통과합니다.
    // - 맵(World: 벽, 바닥)은 뚫으면 맵 밖으로 나가므로 Target에 넣어도 통과하지 않습니다. (맵에서는 뒤의 반사 카드 등이 반응)
    // - 관통 횟수는 스펙 표의 Count 입니다. 횟수를 다 쓰면 결정권을 가져가지 않으므로, 뒤에 쌓인 반응 카드(반사 등)가 이어서 맡을 수 있습니다.
    // - 횟수는 Effect 배율을 받습니다.
    public class PierceCardData : ResponseCardData
    {
        public PierceCardData(CardDef def) : base(def)
        {
        }

        // 맵(World)은 관통할 수 없습니다.
        protected override ContactTarget AllowedTargets
        {
            get { return ContactTarget.Enemy | ContactTarget.Prop; }
        }

        // 이 카드 한 장으로 통과할 수 있는 횟수
        private int PierceCount
        {
            get { return Spec.Count; }
        }

        public override int CreateCount(CardMultiplier multiplier)
        {
            return CeilMultiply(PierceCount, multiplier.Effect);
        }

        protected override void OnHit(CardContext context)
        {
            if (context.Count <= 0)
                return;
            if (TryRespond(context) == false)
                return;

            context.Count--;
            context.Projectile.PassThrough();
        }

        public override string Description {
            get { return $"카드가 {TargetText(Target)} 충돌시, \n{ValueText(PierceCount)}번 꿰뚫고 지나갑니다."; }
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
