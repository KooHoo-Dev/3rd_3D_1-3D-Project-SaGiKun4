using System;
using Study3D.FpsGame.Weapon.Cards;
using UnityEngine;

namespace Study3D.FpsGame.Deck
{
    // 반응 카드가 반응할 충돌 대상 입니다. 여러 개를 함께 고를 수 있습니다.
    [Flags]
    public enum ContactTarget
    {
        Enemy = 1 << 0, // 적
        World = 1 << 1, // 벽, 바닥 등 환경
    }

    // 반응 카드의 공통 부모: 부딪힌 순간 운동량이 어떻게 되는지(유지, 꺾임 등)를 정합니다.
    // - 한 충돌에는 반응 카드 한 장만 결정합니다. Target이 겹치는 반응 카드가 여럿이면 쌓은 순서상 앞의 카드가 이깁니다.
    // - 반응 단계는 조향, 효과 단계보다 먼저 실행됩니다. (CardRole)
    // - Target은 지금은 에셋 인스펙터에서 바꿉니다. (이후 강화 전용 카드로 바꿀 예정)
    public abstract class ResponseCardData : CardData
    {
        [Tooltip("이 카드가 반응할 충돌 대상 (적, 벽, 둘 다)")]
        [SerializeField] private ContactTarget target;

        public override CardType Type => CardType.Enhance;
        public override CardRole Role => CardRole.Response;
        public ContactTarget Target => target;

        // 새 에셋을 만들었을 때 들어갈 카드별 기본 대상 입니다.
        protected abstract ContactTarget DefaultTarget { get; }

        private void Reset()
        {
            target = DefaultTarget;
        }

        // 이번 충돌 대상이 Target에 포함되면 반응 결정권을 가져갑니다.
        // 충돌이 아니거나, 대상이 아니거나, 앞의 반응 카드가 이미 정했으면 false를 반환합니다.
        protected bool TryRespond(CardContext context)
        {
            if (context.Target == null)
                return false;

            ContactTarget contact = context.IsEnemy ? ContactTarget.Enemy : ContactTarget.World;
            bool isTargetContact = (target & contact) != 0;
            if (isTargetContact == false)
                return false;

            return context.Projectile.TryClaimResponse();
        }
    }
}
