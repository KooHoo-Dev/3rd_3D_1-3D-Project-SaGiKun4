using System.Collections.Generic;
using Study3D.FpsGame.Weapon.Cards;
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

        // 재료 카드 차수의 합 입니다. 합친 강화 1번이 더해지는 셈입니다. (1 + 1 → 2)
        public override int EnhanceLevel
        {
            get
            {
                int level = 0;
                foreach (CardData part in parts)
                {
                    if (IsUsablePart(part) == false)
                        continue;

                    level += part.EnhanceLevel;
                }
                return Mathf.Max(1, level);
            }
        }

        // 본체(펼친 첫 카드)의 설명만 씁니다. 강화가 늘어도 설명칸이 넘치지 않도록, 나머지 강화 카드는 메뉴 화면에서 태그로 보여 줍니다.
        public override string Description
        {
            get
            {
                List<CardData> cards = new List<CardData>();
                CollectCards(cards);
                if (cards.Count == 0)
                    return string.Empty;

                return cards[0].Description;
            }
        }

        public override void CollectCards(List<CardData> cards)
        {
            foreach (CardData part in parts)
            {
                if (IsUsablePart(part) == false)
                    continue;

                part.CollectCards(cards);
            }
        }

        // 시점 변경은 맨 첫 재료 카드에만 적용합니다. (첫 재료가 조합 카드면 그 안의 첫 재료까지 내려갑니다)
        // 나머지 재료는 그대로 공유하고, 조합 카드 자체는 새로 만듭니다.
        public override CardData WithTiming(CardTiming newTiming, string newName)
        {
            List<CardData> newParts = new List<CardData>(parts);
            int first = FindFirstUsablePartIndex(newParts);
            if (first >= 0)
            {
                newParts[first] = newParts[first].WithTiming(newTiming, newParts[first].CardName);
            }
            return Create(newName, newParts.ToArray());
        }

        // 대상 변경은 발동 순서상 첫 반응 카드에만 적용합니다. (재료가 조합 카드면 그 안까지 찾아 내려갑니다)
        // 반응 카드가 없으면 null 입니다. 나머지 재료는 그대로 공유하고, 조합 카드 자체는 새로 만듭니다.
        public override CardData WithTarget(ContactTarget newTarget, string newName)
        {
            for (int i = 0; i < parts.Count; i++)
            {
                if (IsUsablePart(parts[i]) == false)
                    continue;

                CardData changed = parts[i].WithTarget(newTarget, parts[i].CardName);
                if (changed == null)
                    continue;

                List<CardData> newParts = new List<CardData>(parts);
                newParts[i] = changed;
                return Create(newName, newParts.ToArray());
            }
            return null;
        }

        // 비어 있거나 자기 자신인 재료는 건너뜁니다.
        private bool IsUsablePart(CardData part)
        {
            return part != null && part != this;
        }

        private int FindFirstUsablePartIndex(List<CardData> partList)
        {
            for (int i = 0; i < partList.Count; i++)
            {
                if (IsUsablePart(partList[i]))
                    return i;
            }
            return -1;
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
