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
    // - 강화 전용 카드(CardType.Modifier)는 드로우되지 않도록 강화 전용 보관함에 따로 둡니다.
    public class CardDeck
    {
        private readonly List<CardData> drawPile = new List<CardData>();
        private readonly List<CardData> hand = new List<CardData>();
        private readonly List<CardData> discardPile = new List<CardData>();
        private readonly List<CardData> modifierPile = new List<CardData>();
        private int selectedIndex;

        public IReadOnlyList<CardData> DrawPile => drawPile;
        public IReadOnlyList<CardData> Hand => hand;
        public IReadOnlyList<CardData> DiscardPile => discardPile;
        public IReadOnlyList<CardData> ModifierPile => modifierPile;
        public int SelectedIndex => selectedIndex;

        public CardData SelectedCard
        {
            get
            {
                if (hand.Count == 0)
                    return null;

                return hand[selectedIndex];
            }
        }

        // 손패 최대 장수. 런타임에 늘리거나 줄일 수 있습니다.
        // 줄였을 때 이미 들고 있는 카드는 버리지 않고, 이후 드로우/획득만 막습니다.
        public int HandLimit { get; set; }
        public bool IsHandFull => hand.Count >= HandLimit;

        public event Action<CardData> OnCardAdded;    // 손패 맨 뒤에 카드가 들어왔을 때
        public event Action<int> OnCardRemoved;       // (빠진 손패 인덱스) 카드를 사용했을 때
        public event Action<int> OnCardChanged;       // (바뀐 손패 인덱스) 같은 자리의 카드가 다른 카드로 바뀌었을 때 (강화 등)
        public event Action OnHandCleared;            // 손패를 모두 버렸을 때
        public event Action<int> OnSelectionChanged;  // (선택 인덱스, 손패가 비면 -1)

        // 강화 전용 카드가 섞여 있으면 강화 전용 보관함으로 보냅니다.
        public CardDeck(IEnumerable<CardData> cards, int handLimit)
        {
            foreach (CardData card in cards)
            {
                if (card.Type == CardType.Modifier)
                {
                    modifierPile.Add(card);
                }
                else
                {
                    drawPile.Add(card);
                }
            }
            HandLimit = handLimit;
        }

        // 손패가 가득 찼으면 뽑지 않고 false를 반환합니다.
        // 뽑을 카드가 없으면 버린 카드를 다시 뽑을 카드로 돌려놓습니다. (랜덤으로 뽑으므로 따로 섞지 않습니다)
        // 그래도 없으면 false를 반환합니다.
        public bool Draw()
        {
            if (IsHandFull)
                return false;

            if (drawPile.Count == 0)
            {
                drawPile.AddRange(discardPile);
                discardPile.Clear();
            }
            if (drawPile.Count == 0)
                return false;

            int index = UnityEngine.Random.Range(0, drawPile.Count);
            CardData card = drawPile[index];
            drawPile.RemoveAt(index);
            AddToHand(card);
            return true;
        }

        // 게임 중 얻은 카드: 손패에 빈칸이 있으면 손패로, 없으면 버린 카드로 넣습니다.
        // 강화 전용 카드는 항상 강화 전용 보관함으로 갑니다.
        public void Acquire(CardData card)
        {
            if (card.Type == CardType.Modifier)
            {
                modifierPile.Add(card);
            }
            else if (IsHandFull)
            {
                discardPile.Add(card);
            }
            else
            {
                AddToHand(card);
            }
        }

        // 손패가 가득 차거나 뽑을 카드가 없을 때까지 드로우합니다.
        public void FillHand()
        {
            while (Draw()) { }
        }

        public void Select(int index)
        {
            if (index < 0 || index >= hand.Count)
                return;

            selectedIndex = index;
            if (OnSelectionChanged != null)
                OnSelectionChanged.Invoke(selectedIndex);
        }

        // 선택을 step칸 옮깁니다. 끝을 넘어가면 반대쪽 끝으로 돌아갑니다.
        public void SelectNext(int step)
        {
            if (hand.Count == 0)
                return;

            // step이 음수여도 0 ~ hand.Count - 1 안으로 돌아오도록 한 번 더 더해서 나눕니다.
            int nextIndex = ((selectedIndex + step) % hand.Count + hand.Count) % hand.Count;
            Select(nextIndex);
        }

        // 선택된 카드를 손패에서 빼서 버린 카드로 보냅니다.
        // 같은 자리에 있던 다음 카드가 자연스럽게 선택됩니다.
        public void DiscardSelected()
        {
            if (hand.Count == 0)
                return;

            int removedIndex = selectedIndex;
            discardPile.Add(hand[removedIndex]);
            hand.RemoveAt(removedIndex);
            ClampSelectedIndex();

            if (OnCardRemoved != null)
                OnCardRemoved.Invoke(removedIndex);
            NotifySelectionAfterRemove();
        }

        // 손패 index번째 카드를 다른 카드로 바꿉니다. 자리와 선택은 그대로 둡니다.
        public void ReplaceInHand(int index, CardData card)
        {
            if (index < 0 || index >= hand.Count)
                return;

            hand[index] = card;
            if (OnCardChanged != null)
                OnCardChanged.Invoke(index);
        }

        // 손패 index번째 카드를 버린 카드로 보내지 않고 덱에서 완전히 없앱니다. (강화 재료 등)
        // 선택된 카드는 자리가 당겨져도 같은 카드가 계속 선택되고, 선택된 카드가 빠지면 같은 자리의 다음 카드가 선택됩니다.
        public void RemoveFromHand(int index)
        {
            if (index < 0 || index >= hand.Count)
                return;

            hand.RemoveAt(index);
            if (index < selectedIndex)
                selectedIndex--;
            ClampSelectedIndex();

            if (OnCardRemoved != null)
                OnCardRemoved.Invoke(index);
            NotifySelectionAfterRemove();
        }

        // 손패가 줄어서 선택 인덱스가 끝을 넘어갔으면 마지막 카드로 당깁니다.
        private void ClampSelectedIndex()
        {
            if (selectedIndex >= hand.Count)
                selectedIndex = Mathf.Max(0, hand.Count - 1);
        }

        // 카드가 빠진 뒤의 선택을 알립니다. 손패가 비었으면 -1
        private void NotifySelectionAfterRemove()
        {
            if (OnSelectionChanged == null)
                return;

            if (hand.Count > 0)
            {
                OnSelectionChanged.Invoke(selectedIndex);
            }
            else
            {
                OnSelectionChanged.Invoke(-1);
            }
        }

        // 덱 카드(손패, 뽑을 카드, 버린 카드)의 평균 강화 차수 입니다. 강화 전용 카드는 세지 않습니다. 카드가 없으면 1
        public float AverageEnhanceLevel
        {
            get
            {
                int count = hand.Count + drawPile.Count + discardPile.Count;
                if (count == 0)
                    return 1f;

                int sum = SumEnhanceLevel(hand) + SumEnhanceLevel(drawPile) + SumEnhanceLevel(discardPile);
                return (float)sum / count;
            }
        }

        private int SumEnhanceLevel(List<CardData> pile)
        {
            int sum = 0;
            foreach (CardData card in pile)
            {
                sum += card.EnhanceLevel;
            }
            return sum;
        }

        // 덱 전체 번호: 손패 → 뽑을 카드 → 버린 카드 → 강화 전용 보관함 순으로 이어 붙인 번호 입니다. (강화 화면 목록과 같은 순서)
        public int TotalCount
        {
            get { return hand.Count + drawPile.Count + discardPile.Count + modifierPile.Count; }
        }

        public CardData GetCard(int totalIndex)
        {
            List<CardData> pile = FindPile(ref totalIndex);
            if (pile == null)
                return null;

            return pile[totalIndex];
        }

        // 덱 전체 번호의 카드를 다른 카드로 바꿉니다. 손패면 OnCardChanged로 알립니다.
        public void Replace(int totalIndex, CardData card)
        {
            List<CardData> pile = FindPile(ref totalIndex);
            if (pile == hand)
            {
                ReplaceInHand(totalIndex, card);
            }
            else if (pile != null)
            {
                pile[totalIndex] = card;
            }
        }

        // 덱 전체 번호의 카드를 덱에서 완전히 없앱니다. 손패면 OnCardRemoved로 알립니다.
        public void Remove(int totalIndex)
        {
            List<CardData> pile = FindPile(ref totalIndex);
            if (pile == hand)
            {
                RemoveFromHand(totalIndex);
            }
            else if (pile != null)
            {
                pile.RemoveAt(totalIndex);
            }
        }

        // 덱 전체 번호가 속한 더미를 찾고, 번호를 그 더미 안의 번호로 바꿉니다. 범위 밖이면 null
        private List<CardData> FindPile(ref int index)
        {
            if (index < 0)
                return null;

            if (index < hand.Count)
                return hand;
            index -= hand.Count;

            if (index < drawPile.Count)
                return drawPile;
            index -= drawPile.Count;

            if (index < discardPile.Count)
                return discardPile;
            index -= discardPile.Count;

            if (index < modifierPile.Count)
                return modifierPile;

            return null;
        }

        // 손패를 모두 버린 카드로 보냅니다.
        public void DiscardHand()
        {
            discardPile.AddRange(hand);
            hand.Clear();
            selectedIndex = 0;

            if (OnHandCleared != null)
                OnHandCleared.Invoke();
            if (OnSelectionChanged != null)
                OnSelectionChanged.Invoke(-1);
        }

        private void AddToHand(CardData card)
        {
            hand.Add(card);
            if (OnCardAdded != null)
                OnCardAdded.Invoke(card);

            // 빈 손패에 처음 들어온 카드는 자동으로 선택된 상태가 됩니다.
            if (hand.Count == 1 && OnSelectionChanged != null)
                OnSelectionChanged.Invoke(selectedIndex);
        }
    }
}
