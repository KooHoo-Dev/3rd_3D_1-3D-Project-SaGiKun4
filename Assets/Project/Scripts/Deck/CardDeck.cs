using System;
using System.Collections.Generic;
using UnityEngine;

namespace Study3D.FpsGame.Deck
{
    // CardDeck
    // : 플레이어가 보유한 카드들을 뽑을 카드 / 손패 / 버린 카드로 나눠서 관리합니다.
    // - 드로우는 뽑을 카드 중 하나를 랜덤으로 꺼내 손패에 넣습니다.
    // - 손패 중 선택된 카드는 항상 1장(손패가 비어 있으면 없음) 입니다.
    // - 손패는 HandLimit장까지만 가질 수 있고, 덱(뽑을 카드)은 제한이 없습니다.
    // - 손패가 바뀔 때마다 이벤트로 알려주므로, UI는 이벤트만 구독해서 따라가면 됩니다.
    public class CardDeck
    {
        private readonly List<CardData> drawPile;
        private readonly List<CardData> hand = new List<CardData>();
        private readonly List<CardData> discardPile = new List<CardData>();
        private int selectedIndex;

        public IReadOnlyList<CardData> DrawPile => drawPile;
        public IReadOnlyList<CardData> Hand => hand;
        public IReadOnlyList<CardData> DiscardPile => discardPile;
        public int SelectedIndex => selectedIndex;
        public CardData SelectedCard => hand.Count > 0 ? hand[selectedIndex] : null;

        // 손패 최대 장수. 런타임에 늘리거나 줄일 수 있습니다.
        // 줄였을 때 이미 들고 있는 카드는 버리지 않고, 이후 드로우/획득만 막습니다.
        public int HandLimit { get; set; }
        public bool IsHandFull => hand.Count >= HandLimit;

        public event Action<CardData> OnCardAdded;    // 손패 맨 뒤에 카드가 들어왔을 때
        public event Action<int> OnCardRemoved;       // (빠진 손패 인덱스) 카드를 사용했을 때
        public event Action OnHandCleared;            // 손패를 모두 버렸을 때
        public event Action<int> OnSelectionChanged;  // (선택 인덱스, 손패가 비면 -1)

        public CardDeck(IEnumerable<CardData> cards, int handLimit)
        {
            drawPile = new List<CardData>(cards);
            HandLimit = handLimit;
        }

        // 손패가 가득 찼으면 뽑지 않고 false를 반환합니다.
        // 뽑을 카드가 없으면 버린 카드를 다시 뽑을 카드로 돌려놓습니다. (랜덤으로 뽑으므로 따로 섞지 않습니다)
        // 그래도 없으면 false를 반환합니다.
        public bool Draw()
        {
            if (IsHandFull) return false;

            if (drawPile.Count == 0)
            {
                drawPile.AddRange(discardPile);
                discardPile.Clear();
            }
            if (drawPile.Count == 0) return false;

            int index = UnityEngine.Random.Range(0, drawPile.Count);
            CardData card = drawPile[index];
            drawPile.RemoveAt(index);
            AddToHand(card);
            return true;
        }

        // 게임 중 얻은 카드: 손패에 빈칸이 있으면 손패로, 없으면 버린 카드로 넣습니다.
        public void Acquire(CardData card)
        {
            if (IsHandFull) discardPile.Add(card);
            else AddToHand(card);
        }

        // 손패가 가득 차거나 뽑을 카드가 없을 때까지 드로우합니다.
        public void FillHand()
        {
            while (Draw()) { }
        }

        public void Select(int index)
        {
            if (index < 0 || index >= hand.Count) return;
            selectedIndex = index;
            OnSelectionChanged?.Invoke(selectedIndex);
        }

        // 선택을 step칸 옮깁니다. 끝을 넘어가면 반대쪽 끝으로 돌아갑니다.
        public void SelectNext(int step)
        {
            if (hand.Count == 0) return;
            Select(((selectedIndex + step) % hand.Count + hand.Count) % hand.Count);
        }

        // 선택된 카드를 손패에서 빼서 버린 카드로 보냅니다.
        // 같은 자리에 있던 다음 카드가 자연스럽게 선택됩니다.
        public void DiscardSelected()
        {
            if (hand.Count == 0) return;

            int removedIndex = selectedIndex;
            discardPile.Add(hand[removedIndex]);
            hand.RemoveAt(removedIndex);
            if (selectedIndex >= hand.Count) selectedIndex = Mathf.Max(0, hand.Count - 1);

            OnCardRemoved?.Invoke(removedIndex);
            OnSelectionChanged?.Invoke(hand.Count > 0 ? selectedIndex : -1);
        }

        // 손패를 모두 버린 카드로 보냅니다.
        public void DiscardHand()
        {
            discardPile.AddRange(hand);
            hand.Clear();
            selectedIndex = 0;

            OnHandCleared?.Invoke();
            OnSelectionChanged?.Invoke(-1);
        }

        private void AddToHand(CardData card)
        {
            hand.Add(card);
            OnCardAdded?.Invoke(card);
            // 빈 손패에 처음 들어온 카드는 자동으로 선택된 상태가 됩니다.
            if (hand.Count == 1) OnSelectionChanged?.Invoke(selectedIndex);
        }
    }
}
