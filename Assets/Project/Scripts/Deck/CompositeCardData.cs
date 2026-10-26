using System.Collections.Generic;
using UnityEngine;

namespace Study3D.FpsGame.Deck
{
    // 조합 카드: 여러 강화 카드를 한 장으로 합친 카드 입니다. (카드1 + 카드2 → 카드3)
    // - 강화 스택에는 한 장(1칸)으로 쌓이지만, 발사체에는 재료 카드들이 순서대로 실립니다.
    // - 재료 카드는 각자 자기 Timing에 발동하므로, 이 카드 자체의 Timing은 쓰지 않습니다.
    // - 조합 카드를 재료로 넣어도 됩니다. (안쪽 재료까지 순서대로 펼쳐집니다)
    [CreateAssetMenu(menuName = "Card/Composite Card", fileName = "CompositeCard")]
    public class CompositeCardData : CardData
    {
        [SerializeField] private List<CardData> parts = new List<CardData>();

        public override CardType Type => CardType.Enhance;
        public IReadOnlyList<CardData> Parts => parts;

        public override void CollectCards(List<CardData> cards)
        {
            foreach (CardData part in parts)
            {
                if (part == null || part == this) continue;
                part.CollectCards(cards);
            }
        }

        // 게임 중 카드를 합칠 때 사용합니다. (에셋이 아니라 런타임에만 존재하는 카드가 만들어집니다)
        public static CompositeCardData Create(string cardName, params CardData[] parts)
        {
            CompositeCardData card = CreateInstance<CompositeCardData>();
            card.name = cardName;
            card.CardName = cardName;
            card.parts.AddRange(parts);
            return card;
        }
    }
}
