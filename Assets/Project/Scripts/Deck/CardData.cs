using System.Collections.Generic;
using Study3D.FpsGame.Weapon.Cards;
using UnityEngine;

namespace Study3D.FpsGame.Deck
{
    public enum CardType
    {
        Attack,  // 쌓아둔 강화 카드를 모두 실어서 발사하는 카드
        Enhance  // 강화 스택에 쌓이면서, 자기 자신만 실어서 발사하는 카드
    }

    // CardData
    // : 덱에 들어가는 카드 한 장의 정의 입니다. (날아가는 발사체 Projectile과는 다른 개념)
    // - 발사체에 실리면 정해진 시점(Timing)에 그 시점의 함수가 실행됩니다.
    // - 4개 시점 함수 중 필요한 것만 재정의하고, 어느 시점에 발동할지는 에셋의 Timing으로 고릅니다.
    // - ScriptableObject는 모든 발사체가 공유하므로 런타임 상태를 두지 않습니다. 상태는 CardContext.Count에 둡니다.
    public abstract class CardData : ScriptableObject
    {
        [SerializeField] private string cardName;
        [SerializeField] private CardTiming timing;
        [Tooltip("카드가 실제로 발동했을 때 재생할 이펙트 프리팹 (비워두면 재생하지 않음)")]
        [SerializeField] private GameObject activateEffect;

        public string CardName
        {
            get => cardName;
            protected set => cardName = value;
        }

        public CardTiming Timing => timing;
        public abstract CardType Type { get; }

        // 발사체에 실릴 카드들을 순서대로 담습니다. 일반 카드는 자기 자신을, 조합 카드는 재료 카드들을 담습니다.
        public virtual void CollectCards(List<CardData> cards) => cards.Add(this);

        // 발사체에 실릴 때 이 카드 몫 런타임 값(CardContext.Count)의 처음 값 입니다. (남은 관통 수 등)
        public virtual int CreateCount(CardMultiplier multiplier) => 0;

        // 발사체가 Timing에 맞춰 호출합니다.
        public void Activate(CardContext context)
        {
            switch (timing)
            {
                case CardTiming.Shoot: OnShoot(context); break;
                case CardTiming.Flying: OnFlying(context); break;
                case CardTiming.Hit: OnHit(context); break;
                case CardTiming.Destroy: OnDestroyed(context); break;
            }
        }

        protected virtual void OnShoot(CardContext context) { }
        protected virtual void OnFlying(CardContext context) { }
        protected virtual void OnHit(CardContext context) { }
        protected virtual void OnDestroyed(CardContext context) { } // (OnDestroy는 Unity 메시지 이름이라 피합니다)

        // 발동 이펙트를 발동 위치에 재생합니다. 크기는 Effect 배율을 따릅니다.
        // - 발동 시점이 와도 아무 일도 하지 않는 경우(벽에 닿은 관통 카드 등)가 있어서, 자동으로 재생하지 않고 카드가 실제로 동작했을 때 직접 호출합니다.
        protected void PlayEffect(CardContext context) => PlayEffect(context, context.Multiplier.Effect);

        // 크기를 직접 정해서 재생합니다. (폭발 반경에 맞추는 경우 등)
        protected void PlayEffect(CardContext context, float scale)
        {
            EffectSpawner.Play(activateEffect, context.Point, Quaternion.identity, scale);
        }

        // 실수 수치에 배율을 곱한 정수 값. 올림 처리합니다.
        protected static int CeilMultiply(int value, float multiplier) => Mathf.CeilToInt(value * multiplier);
    }
}
