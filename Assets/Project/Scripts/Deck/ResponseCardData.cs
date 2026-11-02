using System;
using Study3D.FpsGame.Data;
using Study3D.FpsGame.Weapon.Cards;
using UnityEngine;

namespace Study3D.FpsGame.Deck
{
    // 반응 카드가 반응할 충돌 대상 입니다. 여러 개를 함께 고를 수 있습니다.
    [Flags]
    public enum ContactTarget
    {
        Enemy = 1 << 0, // 적
        World = 1 << 1, // 맵의 벽, 바닥
        Prop = 1 << 2,  // 맵 안의 구조물 (상자, 기둥 등)
    }

    // 반응 카드의 공통 부모: 부딪힌 순간 운동량이 어떻게 되는지(유지, 꺾임 등)를 정합니다.
    // - 한 충돌에는 반응 카드 한 장만 결정합니다. Target이 겹치는 반응 카드가 여럿이면 쌓은 순서상 앞의 카드가 이깁니다.
    // - 반응 단계는 조향, 효과 단계보다 먼저 실행됩니다. (CardRole)
    // - 기본 Target은 카드 표의 Target 칸에서 읽고, 게임 중에는 대상 변경 강화 카드(TargetCardData)로 바꿉니다. (복제본에만 덮어씀)
    public abstract class ResponseCardData : CardData
    {
        protected ResponseCardData(CardDef def) : base(def)
        {
        }

        // 대상 변경 강화로 바뀐 대상 (없으면 표의 값). 다른 강화로 복제될 때도 MemberwiseClone이 그대로 옮깁니다.
        private bool hasTargetOverride;
        private ContactTarget targetOverride;

        public override CardType Type => CardType.Enhance;
        public override CardRole Role => CardRole.Response;

        // 실제로 반응하는 대상: 고른 대상 중 카드가 허용하는 것만
        public ContactTarget Target
        {
            get { return ChosenTarget & AllowedTargets; }
        }

        // 고른 대상 (강화로 바뀐 값, 없으면 표의 값)
        private ContactTarget ChosenTarget
        {
            get
            {
                if (hasTargetOverride)
                    return targetOverride;
                if (Def == null)
                    return 0;

                return Def.Target;
            }
        }

        // 이 카드가 반응할 수 있는 대상의 범위. 기본은 전부이고, 관통처럼 맵(World)을 빼야 하는 카드만 재정의합니다.
        // (나중에 대상 변경 강화 카드가 생겨도 이 범위를 넘을 수 없습니다)
        protected virtual ContactTarget AllowedTargets
        {
            get { return ContactTarget.Enemy | ContactTarget.World | ContactTarget.Prop; }
        }

        // newTarget을 고르면 실제로 반응하게 될 대상 (카드가 허용하지 않는 대상은 빠짐)
        public ContactTarget GetAppliedTarget(ContactTarget newTarget)
        {
            return newTarget & AllowedTargets;
        }

        // 대상만 바꾼 복제본을 만듭니다. (원래 카드는 다른 곳에서도 쓸 수 있으므로 직접 바꾸지 않습니다)
        public override CardData WithTarget(ContactTarget newTarget, string newName)
        {
            ResponseCardData copy = (ResponseCardData)CreateEnhancedCopy(newName);
            copy.hasTargetOverride = true;
            copy.targetOverride = newTarget;
            return copy;
        }

        // 이번 충돌 대상이 Target에 포함되면 반응 결정권을 가져갑니다.
        // 충돌이 아니거나, 대상이 아니거나, 앞의 반응 카드가 이미 정했으면 false를 반환합니다.
        protected bool TryRespond(CardContext context)
        {
            if (context.Target == null)
                return false;

            ContactTarget contact = ToContactTarget(context.Contact);
            bool isTargetContact = (Target & contact) != 0;
            if (isTargetContact == false)
                return false;

            return context.Projectile.TryClaimResponse();
        }

        // 부딪힌 대상의 종류를 반응 대상 값으로 바꿉니다. (반응할 대상이 아니면 0)
        private static ContactTarget ToContactTarget(ContactKind contact)
        {
            switch (contact)
            {
                case ContactKind.Enemy:
                    return ContactTarget.Enemy;
                case ContactKind.World:
                    return ContactTarget.World;
                case ContactKind.Prop:
                    return ContactTarget.Prop;
                default:
                    return 0;
            }
        }
    }
}
