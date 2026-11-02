using System;
using System.Collections.Generic;
using Study3D.FpsGame.Weapon.Cards;
using UnityEngine;

namespace Study3D.FpsGame.Deck
{
    public enum CardType
    {
        Attack,  // 쌓아둔 강화 카드를 모두 실어서 발사하는 카드
        Enhance, // 강화 스택에 쌓이면서, 자기 자신만 실어서 발사하는 카드
        Modifier // 강화 전용 카드: 드로우되지 않고 강화 전용 보관함에 들어가며, 강화 재료로만 씁니다. (시점 변경 등)
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
        [Tooltip("시점 변경 강화로 바꿀 수 없는 시점 (그 시점에선 의미가 없거나 밸런스를 해치는 경우. 예: 반사의 소멸, 폭발의 진행 중)")]
        [SerializeField] private List<CardTiming> blockedTimings = new List<CardTiming>();
        [Tooltip("카드가 실제로 발동했을 때 재생할 이펙트 프리팹 (비워두면 재생하지 않음)")]
        [SerializeField] private GameObject activateEffect;

        // 시점 변경 등으로 이 복제본이 거친 강화 횟수. 에셋에는 저장하지 않습니다. (런타임 복제본에만 생김)
        [NonSerialized] private int addedLevel;

        public string CardName
        {
            get => cardName;
            protected set => cardName = value;
        }

        public CardTiming Timing => timing;
        public abstract CardType Type { get; }

        // 같은 시점 안에서의 실행 순서를 정합니다. 반응, 조향 카드만 재정의합니다.
        public virtual CardRole Role => CardRole.Effect;

        // 강화 차수: 기본 카드는 1이고, 강화를 한 번 거칠 때마다 1씩 늘어납니다.
        // - 시점 변경: 바뀐 카드 = 원래 카드 + 1
        // - 조합 카드: 재료 카드 차수의 합 (1 + 1 → 2, 2 + 1 → 3. 재료 안에서 거친 강화도 모두 셉니다)
        public virtual int EnhanceLevel
        {
            get { return 1 + addedLevel; }
        }

        // 시점 변경 강화를 받을 수 있는지. 시점 변경 카드 자신처럼 아예 대상이 아닌 카드만 false 입니다.
        // 카드마다 막을 시점은 에셋의 blockedTimings에서 고릅니다. (IsTimingBlocked)
        public virtual bool CanChangeTiming => true;

        // 시점 변경 강화로 이 시점이 될 수 없는지
        public bool IsTimingBlocked(CardTiming newTiming)
        {
            return blockedTimings.Contains(newTiming);
        }

        // 시점만 newTiming으로 바꾼 복제 카드를 만듭니다. (원본 에셋은 모든 덱이 공유하므로 직접 바꾸지 않습니다)
        // 런타임에만 존재하는 카드가 만들어집니다.
        public virtual CardData WithTiming(CardTiming newTiming, string newName)
        {
            CardData copy = CreateEnhancedCopy(newName);
            copy.timing = newTiming;
            return copy;
        }

        // 반응 대상만 newTarget으로 바꾼 복제 카드를 만듭니다. 대상을 바꿀 카드(반응 카드)가 아니면 null 입니다.
        // - 반응 카드는 자기를 복제하고, 조합 카드는 안쪽 첫 반응 카드만 바꾼 조합 카드를 새로 만듭니다.
        public virtual CardData WithTarget(ContactTarget newTarget, string newName)
        {
            return null;
        }

        // 강화로 값을 바꿀 복제본을 만듭니다. 이름을 바꾸고 강화 차수를 1 올립니다.
        protected CardData CreateEnhancedCopy(string newName)
        {
            CardData copy = Instantiate(this);
            copy.name = newName;
            copy.cardName = newName;
            copy.addedLevel = addedLevel + 1;
            return copy;
        }

        // 카드 UI(손패, 메뉴 목록)의 설명 글자 입니다. 카드마다 재정의해서 효과를 적습니다.
        // 시점 문구는 TimingText로 붙이면 시점 변경 강화로 바뀐 시점이 그대로 반영됩니다.
        // 강화나 배율로 바뀔 수 있는 값(수치, 대상 등)은 ValueText로 감싸 파란색으로 보여 줍니다. (TimingText는 이미 감싸져 있음)
        public virtual string Description
        {
            get { return $"{TimingText(timing)}, 효과를 발동합니다."; }
        }

        // 설명에서 바뀔 수 있는 값의 색 (TextMeshPro 리치 텍스트 색 태그)
        private const string VALUE_COLOR = "#5AB4FF";

        // 바뀔 수 있는 값을 파란색으로 감쌉니다. 나머지 고정 문구는 글자 기본색 그대로입니다.
        protected static string ValueText(object value)
        {
            return $"<color={VALUE_COLOR}>{value}</color>";
        }

        // 시점을 설명 문구로 바꿉니다. (예: Destroy → "소멸할 때") 시점은 강화로 바뀌는 값이라 파란색으로 감쌉니다.
        protected static string TimingText(CardTiming cardTiming)
        {
            return ValueText(TimingName(cardTiming));
        }

        private static string TimingName(CardTiming cardTiming)
        {
            switch (cardTiming)
            {
                case CardTiming.Shoot:
                    return "발사할 때";
                case CardTiming.Flying:
                    return "날아가는 동안";
                case CardTiming.Hit:
                    return "충돌할 때";
                case CardTiming.Destroy:
                    return "소멸할 때";
                default:
                    return cardTiming.ToString();
            }
        }

        // 발사체에 실릴 카드들을 순서대로 담습니다. 일반 카드는 자기 자신을, 조합 카드는 재료 카드들을 담습니다.
        public virtual void CollectCards(List<CardData> cards)
        {
            cards.Add(this);
        }

        // 발사체에 실릴 때 이 카드 몫 런타임 값(CardContext.Count)의 처음 값 입니다. (남은 반사 수 등)
        public virtual int CreateCount(CardMultiplier multiplier)
        {
            return 0;
        }

        // 발사체가 Timing에 맞춰 호출합니다.
        public void Activate(CardContext context)
        {
            switch (timing)
            {
                case CardTiming.Shoot:
                    OnShoot(context);
                    break;
                case CardTiming.Flying:
                    OnFlying(context);
                    break;
                case CardTiming.Hit:
                    OnHit(context);
                    break;
                case CardTiming.Destroy:
                    OnDestroyed(context);
                    break;
            }
        }

        protected virtual void OnShoot(CardContext context) { }
        protected virtual void OnFlying(CardContext context) { }
        protected virtual void OnHit(CardContext context) { }
        protected virtual void OnDestroyed(CardContext context) { } // (OnDestroy는 Unity 메시지 이름이라 피합니다)

        // 발동 이펙트를 발동 위치에 재생합니다. 크기는 Effect 배율을 따릅니다.
        // - 발동 시점이 와도 아무 일도 하지 않는 경우(벽에 닿은 관통 카드 등)가 있어서, 자동으로 재생하지 않고 카드가 실제로 동작했을 때 직접 호출합니다.
        protected void PlayEffect(CardContext context)
        {
            PlayEffect(context, context.Multiplier.Effect);
        }

        // 크기를 직접 정해서 재생합니다. (폭발 반경에 맞추는 경우 등)
        protected void PlayEffect(CardContext context, float scale)
        {
            EffectSpawner.Play(activateEffect, context.Point, Quaternion.identity, scale);
        }

        // 실수 수치에 배율을 곱한 정수 값. 올림 처리합니다.
        protected static int CeilMultiply(int value, float multiplier)
        {
            return Mathf.CeilToInt(value * multiplier);
        }
    }
}
