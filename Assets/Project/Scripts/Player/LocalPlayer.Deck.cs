using System.Collections.Generic;
using Study3D.FpsGame.Deck;
using UnityEngine;

namespace Study3D.FpsGame.Player
{
    // LocalPlayer - 덱
    // : 시작 덱을 Inspector에서 받아 CardDeck을 만들고, 드로우/선택/획득을 외부에 열어줍니다.
    // - 카드 선택 입력(휠, 숫자 1~6)은 PlayerController가 받아서 여기 함수들을 호출합니다.
    public partial class LocalPlayer
    {
        [Header("덱")]
        [SerializeField] private List<CardData> startDeck = new List<CardData>(); // 시작 시 지급받는 기본 카드
        [SerializeField] private List<CardData> startModifiers = new List<CardData>(); // 시작 시 강화 전용 보관함에 들어가는 강화 전용 카드 (드랍 시스템 전 테스트용)
        [SerializeField] private int handLimit = 6; // 시작 손패 최대 장수 (런타임 변경은 Deck.HandLimit)

        public CardDeck Deck { get; private set; }

        [ContextMenu("카드 드로우")]
        public bool DrawCard()
        {
            bool drawn = Deck.Draw();
            if (drawn) Debug.Log($"[Deck] 드로우: {Deck.Hand[Deck.Hand.Count - 1].CardName} (손패 {Deck.Hand.Count}장, 남은 카드 {Deck.DrawPile.Count}장)");
            return drawn;
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
                Debug.Log($"[Deck] 선택: {Deck.SelectedCard.CardName} (손패 {i + 1}번째)");
                return true;
            }

            Debug.Log($"[Deck] 손패에 {typeof(T).Name} 카드가 없습니다.");
            return false;
        }
    }
}
