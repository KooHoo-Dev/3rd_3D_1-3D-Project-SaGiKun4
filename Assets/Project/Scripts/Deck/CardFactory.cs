using Study3D.FpsGame.Data;
using UnityEngine;

namespace Study3D.FpsGame.Deck
{
    // 카드 동작 종류 (cards.tsv의 Kind 칸). 종류마다 카드 클래스가 하나씩 있습니다.
    public enum CardKind
    {
        Attack,
        Split,
        Explosion,
        Pierce,
        Ricochet,
        Transfer,
        Homing,
        Timing, // 시점 변경 (강화 전용)
        Target, // 대상 변경 (강화 전용)
        Spec,   // 스펙 강화 (강화 전용)
    }

    // CardFactory
    // : 카드 표의 ID로 카드 객체를 만듭니다. 표의 Kind 칸을 보고 맞는 카드 클래스를 고릅니다.
    // - 새 카드 종류를 추가하려면 CardKind에 이름을 넣고, 카드 클래스를 만들고, 아래 switch에 한 줄을 추가합니다.
    public static class CardFactory
    {
        // 표에 없는 ID면 null
        public static CardData Create(int cardId)
        {
            CardDef def = DataManager.Instance.Cards.Get(cardId);
            if (def == null)
            {
                Debug.LogWarning($"[CardFactory] 카드 표에 ID {cardId}가 없습니다.");
                return null;
            }
            return Create(def);
        }

        public static CardData Create(CardDef def)
        {
            switch (def.Kind)
            {
                case CardKind.Attack:
                    return new AttackCardData(def);
                case CardKind.Split:
                    return new SplitCardData(def);
                case CardKind.Explosion:
                    return new ExplosionCardData(def);
                case CardKind.Pierce:
                    return new PierceCardData(def);
                case CardKind.Ricochet:
                    return new RicochetCardData(def);
                case CardKind.Transfer:
                    return new TransferCardData(def);
                case CardKind.Homing:
                    return new HomingCardData(def);
                case CardKind.Timing:
                    return new TimingCardData(def);
                case CardKind.Target:
                    return new TargetCardData(def);
                case CardKind.Spec:
                    return new SpecCardData(def);
                default:
                    Debug.LogWarning($"[CardFactory] {def.Kind} 종류의 카드 클래스가 없습니다. (카드 {def.ID})");
                    return null;
            }
        }
    }
}
