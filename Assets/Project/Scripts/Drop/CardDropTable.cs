using System;
using System.Collections.Generic;
using Study3D.FpsGame.Deck;
using UnityEngine;
using Random = UnityEngine.Random;

namespace Study3D.FpsGame.Drop
{
    // CardDropTable
    // : 몬스터가 죽을 때 떨어뜨리는 카드 보상에 어떤 선택지를 담을지 정하는 표 입니다.
    // - 몬스터 프리팹이 인스펙터로 참조합니다. 몬스터마다 다른 표를 줄 수 있습니다.
    // - 떨어뜨릴 확률은 몬스터(Enemy)가 정합니다.
    // - 선택지는 칸마다 이렇게 정합니다.
    //   1. enhancedChance 확률로 강화 카드: 강화 카드(Enhance) N장을 랜덤으로 합친 조합 카드. N(차수)은 2 ~ 덱 평균 차수(반올림) 사이 랜덤
    //      (덱 평균이 2 미만이면 나오지 않습니다)
    //   2. 아니면 기본 카드: 종류별 가중치(공격 / 강화 / 강화 전용)로 종류를 고르고, 그 종류 안에서 랜덤으로 한 장
    //      같은 기본 카드는 한 선택지에 두 번 나오지 않습니다. 남은 카드가 없는 종류는 가중치 계산에서 빠집니다.
    // - cards에 조합 카드를 넣으면 무시합니다. (기본 카드만 후보)
    [CreateAssetMenu(menuName = "Card/Card Drop Table", fileName = "CardDropTable")]
    public class CardDropTable : ScriptableObject
    {
        [Header("드랍")]
        [SerializeField] private CardDrop dropPrefab;
        [SerializeField] private float dropHeight = 0.5f; // 죽은 위치(발밑)에서 띄우는 높이

        [Header("선택지")]
        [SerializeField] private int optionCount = 3;
        [Range(0f, 1f)]
        [SerializeField] private float enhancedChance = 0.5f; // 칸마다 강화 카드(조합)가 나올 확률

        [Header("기본 카드 종류별 가중치")]
        [SerializeField] private float attackWeight = 30f;
        [SerializeField] private float enhanceWeight = 50f;
        [SerializeField] private float modifierWeight = 20f; // 강화 전용 카드 (시점 변경 등)

        [Tooltip("후보가 되는 기본 카드 (조합 카드는 넣어도 무시)")]
        [SerializeField] private List<CardData> cards = new List<CardData>();

        // position에 보상을 떨어뜨립니다. (확률은 몬스터가 굴린 뒤 부릅니다)
        public void Drop(Vector3 position)
        {
            if (dropPrefab == null)
                return;

            CardDrop drop = Instantiate(dropPrefab, position + Vector3.up * dropHeight, Quaternion.identity);
            drop.Setup(this);
        }

        // deck의 평균 강화 차수를 기준으로 선택지를 만듭니다. 후보가 모자라면 optionCount보다 적을 수 있습니다.
        public List<CardData> CreateOptions(CardDeck deck)
        {
            int maxLevel = (int)Math.Round(deck.AverageEnhanceLevel, MidpointRounding.AwayFromZero);
            List<CardData> remaining = new List<CardData>();
            List<CardData> enhanceCards = new List<CardData>();
            CollectBaseCards(remaining, enhanceCards);

            List<CardData> options = new List<CardData>(optionCount);
            for (int i = 0; i < optionCount; i++)
            {
                if (maxLevel >= 2 && enhanceCards.Count > 0 && Roll(enhancedChance))
                {
                    int level = Random.Range(2, maxLevel + 1);
                    options.Add(CreateEnhancedCard(enhanceCards, level));
                    continue;
                }

                CardData picked = PickBaseCard(remaining);
                if (picked == null)
                    break; // 남은 기본 카드가 없음
                options.Add(picked);
            }

            return options;
        }

        // cards에서 기본 카드만 중복 없이 remaining에 담고, 그중 강화 카드는 enhanceCards에도 담습니다.
        private void CollectBaseCards(List<CardData> remaining, List<CardData> enhanceCards)
        {
            foreach (CardData card in cards)
            {
                if (card == null || card is CompositeCardData || remaining.Contains(card))
                    continue;

                remaining.Add(card);
                if (card.Type == CardType.Enhance)
                    enhanceCards.Add(card);
            }
        }

        // 강화 카드 level장을 랜덤으로 골라 합친 조합 카드. 기본 카드는 1차라서 차수가 정확히 level이 됩니다.
        private static CardData CreateEnhancedCard(List<CardData> enhanceCards, int level)
        {
            CardData[] parts = new CardData[level];
            for (int i = 0; i < level; i++)
            {
                parts[i] = enhanceCards[Random.Range(0, enhanceCards.Count)];
            }
            return CompositeCardData.Create(parts[0].CardName + "+", parts);
        }

        // 종류별 가중치로 종류를 고르고, 그 종류의 남은 카드 중 한 장을 꺼냅니다. (꺼낸 카드는 remaining에서 빠짐)
        private CardData PickBaseCard(List<CardData> remaining)
        {
            float attack = HasType(remaining, CardType.Attack) ? attackWeight : 0f;
            float enhance = HasType(remaining, CardType.Enhance) ? enhanceWeight : 0f;
            float modifier = HasType(remaining, CardType.Modifier) ? modifierWeight : 0f;
            float total = attack + enhance + modifier;
            if (total <= 0f)
                return null;

            float roll = Random.Range(0f, total);
            CardType type = PickTypeByRoll(roll, attack, enhance);

            List<CardData> candidates = FindCardsOfType(remaining, type);
            if (candidates.Count == 0)
                candidates = remaining; // roll이 정확히 total인 경우 등
            CardData picked = candidates[Random.Range(0, candidates.Count)];
            remaining.Remove(picked);
            return picked;
        }

        // 가중치 구간 [공격 | 강화 | 강화 전용] 중 roll이 떨어진 종류
        private static CardType PickTypeByRoll(float roll, float attack, float enhance)
        {
            if (roll < attack)
                return CardType.Attack;
            if (roll < attack + enhance)
                return CardType.Enhance;

            return CardType.Modifier;
        }

        private static List<CardData> FindCardsOfType(List<CardData> cards, CardType type)
        {
            List<CardData> result = new List<CardData>();
            foreach (CardData card in cards)
            {
                if (card.Type == type)
                    result.Add(card);
            }
            return result;
        }

        private static bool HasType(List<CardData> cards, CardType type)
        {
            foreach (CardData card in cards)
            {
                if (card.Type == type)
                    return true;
            }
            return false;
        }

        private static bool Roll(float chance)
        {
            return chance > 0f && Random.value <= chance;
        }
    }
}
