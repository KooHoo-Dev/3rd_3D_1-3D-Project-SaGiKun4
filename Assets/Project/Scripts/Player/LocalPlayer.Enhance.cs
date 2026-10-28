using Study3D.FpsGame.Deck;
using UnityEngine;

namespace Study3D.FpsGame.Player
{
    // LocalPlayer - 카드 강화 (프로토타입)
    // : 손패의 카드 2장을 순서대로 골라 한 장으로 합칩니다. (첫 번째 카드 → 두 번째 카드 순서로 발동)
    // - B키로 강화 상태에 들어가고, 강화 상태에서는 숫자키가 카드 선택 대신 강화할 카드를 고릅니다.
    // - 합쳐진 카드는 첫 번째 카드 자리에 들어가고, 두 번째 카드는 덱에서 완전히 사라집니다.
    // - 강화된 카드를 다시 재료로 쓸 수 있습니다. (CompositeCardData 안쪽 재료까지 순서대로 펼쳐집니다)
    // - 강화 카드끼리만 합칠 수 있습니다. 공격 카드는 재료로 쓸 수 없습니다.
    // - 한 번 강화하면 강화 상태가 끝납니다. B키를 다시 누르면 취소합니다.
    public partial class LocalPlayer
    {
        private const string ENHANCED_MARK = "+"; // 강화된 카드 표시 (프로토타입: 이름 뒤에 붙임)

        public bool IsEnhancing { get; private set; }

        // 첫 번째로 고른 카드. 고르는 사이 손패가 바뀔 수 있어서 자리와 카드를 같이 기억합니다.
        private int enhanceFirstIndex = -1;
        private CardData enhanceFirstCard;

        public void ToggleEnhanceMode()
        {
            IsEnhancing = !IsEnhancing;
            ClearEnhanceChoice();
            Debug.Log(IsEnhancing
                ? "[Enhance] 강화 상태: 숫자키로 첫 번째 카드 → 두 번째 카드를 고르세요. (B = 취소)"
                : "[Enhance] 강화 상태를 취소했습니다.");
        }

        // 강화 상태에서 숫자키로 손패 handIndex번째 카드를 고릅니다.
        public void ChooseEnhanceCard(int handIndex)
        {
            if (IsEnhancing == false) return;
            if (handIndex < 0 || handIndex >= Deck.Hand.Count) return;

            CardData card = Deck.Hand[handIndex];
            if (card.Type == CardType.Attack)
            {
                Debug.Log($"[Enhance] 공격 카드({card.CardName})는 강화에 쓸 수 없습니다.");
                return;
            }

            // 첫 번째 카드가 그사이 사용되거나 자리가 바뀌었으면 처음부터 다시 고릅니다.
            if (enhanceFirstCard != null &&
                (enhanceFirstIndex >= Deck.Hand.Count || Deck.Hand[enhanceFirstIndex] != enhanceFirstCard))
            {
                Debug.Log("[Enhance] 첫 번째 카드가 손패에서 바뀌어 선택을 초기화했습니다.");
                ClearEnhanceChoice();
            }

            if (enhanceFirstCard == null)
            {
                enhanceFirstIndex = handIndex;
                enhanceFirstCard = card;
                Debug.Log($"[Enhance] 첫 번째 카드: {card.CardName} (손패 {handIndex + 1}번째)");
                return;
            }

            if (handIndex == enhanceFirstIndex)
            {
                Debug.Log("[Enhance] 같은 카드는 두 번째 카드로 고를 수 없습니다.");
                return;
            }

            Combine(enhanceFirstIndex, handIndex);
            IsEnhancing = false;
            ClearEnhanceChoice();
        }

        // first 자리의 카드를 (first → second) 조합 카드로 바꾸고, second 카드는 덱에서 없앱니다.
        private void Combine(int firstIndex, int secondIndex)
        {
            CardData first = Deck.Hand[firstIndex];
            CardData second = Deck.Hand[secondIndex];
            CompositeCardData combined = CompositeCardData.Create(first.CardName + ENHANCED_MARK, first, second);

            // 바꾸기를 먼저 해야 second를 뺄 때 당겨지는 자리를 신경 쓰지 않아도 됩니다.
            Deck.ReplaceInHand(firstIndex, combined);
            Deck.RemoveFromHand(secondIndex);
            Debug.Log($"[Enhance] {first.CardName} + {second.CardName} → {combined.CardName}");
        }

        private void ClearEnhanceChoice()
        {
            enhanceFirstIndex = -1;
            enhanceFirstCard = null;
        }
    }
}
