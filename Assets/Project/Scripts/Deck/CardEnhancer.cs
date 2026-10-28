using UnityEngine;

namespace Study3D.FpsGame.Deck
{
    // CardEnhancer
    // : 카드 강화 규칙 입니다. 덱의 카드 2장을 한 장으로 합칩니다. (첫 번째 카드 → 두 번째 카드 순서로 발동)
    // - 어디서 강화하는지(UI, 강화 오브젝트 등)는 모르고, 덱 전체 번호 2개만 받습니다. (CardDeck의 손패 → 뽑을 카드 → 버린 카드 순)
    // - 합쳐진 카드는 첫 번째 카드 자리에 들어가고, 두 번째 카드는 덱에서 완전히 사라집니다.
    // - 강화된 카드를 다시 재료로 쓸 수 있습니다. (CompositeCardData 안쪽 재료까지 순서대로 펼쳐집니다)
    // - 강화 카드끼리만 합칠 수 있습니다. 공격 카드는 재료로 쓸 수 없습니다.
    public static class CardEnhancer
    {
        private const string ENHANCED_MARK = "+"; // 강화된 카드 표시 (프로토타입: 이름 뒤에 붙임)

        // 강화하지 못했으면 이유를 로그로 남기고 false를 반환합니다.
        public static bool TryEnhance(CardDeck deck, int firstIndex, int secondIndex)
        {
            if (firstIndex == secondIndex)
            {
                Debug.Log("[Enhance] 같은 카드끼리는 강화할 수 없습니다.");
                return false;
            }

            CardData first = deck.GetCard(firstIndex);
            CardData second = deck.GetCard(secondIndex);
            if (first == null || second == null)
            {
                Debug.LogWarning("[Enhance] 고른 카드를 덱에서 찾지 못했습니다.");
                return false;
            }
            if (first.Type == CardType.Attack || second.Type == CardType.Attack)
            {
                Debug.Log("[Enhance] 공격 카드는 강화에 쓸 수 없습니다.");
                return false;
            }

            CompositeCardData combined = CompositeCardData.Create(first.CardName + ENHANCED_MARK, first, second);
            // 바꾸기를 먼저 해야 second를 뺄 때 당겨지는 번호를 신경 쓰지 않아도 됩니다.
            deck.Replace(firstIndex, combined);
            deck.Remove(secondIndex);
            Debug.Log($"[Enhance] {first.CardName} + {second.CardName} → {combined.CardName}");
            return true;
        }
    }
}
