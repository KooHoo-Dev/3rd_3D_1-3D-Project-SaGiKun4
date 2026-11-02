using System.Collections.Generic;
using Study3D.FpsGame.Deck;
using UnityEngine;

namespace Study3D.FpsGame.Player
{
    // LocalPlayer - 덱
    // : 시작 덱을 Inspector에서 받아 CardDeck을 만들고, 드로우/선택/획득을 외부에 열어줍니다.
    // - 시작 카드는 카드 표(cards.tsv)의 ID로 적고, 시작할 때 CardFactory가 카드 객체로 만듭니다. (같은 ID도 한 장씩 따로 만듦)
    // - 카드 선택 입력(휠, 숫자 1~6)은 PlayerController가 받아서 여기 함수들을 호출합니다.
    public partial class LocalPlayer
    {
        [Header("덱 (카드 표의 ID)")]
        [Tooltip("시작 시 지급받는 기본 카드")]
        [SerializeField] private List<int> startDeckIds = new List<int>();
        [Tooltip("시작 시 강화 전용 보관함에 들어가는 강화 전용 카드 (드랍 시스템 전 테스트용)")]
        [SerializeField] private List<int> startModifierIds = new List<int>();
        [SerializeField] private int handLimit = 6; // 시작 손패 최대 장수 (런타임 변경은 Deck.HandLimit)

        public CardDeck Deck { get; private set; }

        // 시작 덱을 만들고, 강화 전용 카드는 강화 전용 보관함으로 넣습니다.
        private void CreateDeck()
        {
            Deck = new CardDeck(CreateCards(startDeckIds), handLimit);
            foreach (CardData card in CreateCards(startModifierIds))
            {
                Deck.Acquire(card);
            }
        }

        // ID 목록을 카드 객체 목록으로 만듭니다. 표에 없는 ID는 경고를 남기고 건너뜁니다.
        private static List<CardData> CreateCards(List<int> cardIds)
        {
            List<CardData> cards = new List<CardData>(cardIds.Count);
            foreach (int cardId in cardIds)
            {
                CardData card = CardFactory.Create(cardId);
                if (card != null)
                {
                    cards.Add(card);
                }
            }
            return cards;
        }

        [ContextMenu("카드 드로우")]
        public bool DrawCard()
        {
            return Deck.Draw();
        }

        // 손패를 모두 버리고 HandLimit장이 될 때까지 다시 드로우합니다.
        public void RefillHand()
        {
            Deck.DiscardHand();
            Deck.FillHand();
        }

        // 게임 중 카드 획득: 손패에 빈칸이 있으면 손패로, 없으면 버린 카드로 들어갑니다. (강화 전용 카드는 강화 전용 보관함으로)
        public void AcquireCard(CardData card)
        {
            Deck.Acquire(card);
        }

        public void SelectCard(int handIndex)
        {
            Deck.Select(handIndex);
        }

        // 선택을 step칸 옮깁니다. (+1 = 오른쪽, -1 = 왼쪽, 끝에서는 반대쪽으로 돌아감)
        public void SelectNextCard(int step)
        {
            Deck.SelectNext(step);
        }

        // 손패에서 T 종류의 카드를 처음 찾은 한 장을 선택합니다. 손패에 없으면 false를 반환합니다.
        public bool SelectCard<T>() where T : CardData
        {
            for (int i = 0; i < Deck.Hand.Count; i++)
            {
                if (Deck.Hand[i] is not T) continue;

                Deck.Select(i);
                return true;
            }

            Debug.Log($"[Deck] 손패에 {typeof(T).Name} 카드가 없습니다.");
            return false;
        }
    }
}
