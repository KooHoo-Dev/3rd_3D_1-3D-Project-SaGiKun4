using System;
using System.Collections.Generic;
using System.Linq;
using Study3D.FpsGame.Deck;

namespace Study3D.FpsGame.Weapon
{
    // EnhanceStack
    // : Weapon이 보관하는 강화 카드 목록 입니다.
    // - 최대 MAX_COUNT장까지만 쌓고, 가득 차면 새로 들어온 강화는 무시합니다.
    // - 제한은 카드 장수 기준입니다. 조합 카드는 효과가 여러 개여도 1칸만 차지합니다.
    // - 강화는 일회성이라 공격 카드로 한 번 사용하면 Clear() 합니다.
    // - 쌓인 수가 바뀌면 OnChanged를 부릅니다. (크로스헤어 강화 링 UI가 받음)
    public class EnhanceStack
    {
        public const int MAX_COUNT = 3;

        private readonly List<CardData> enhances = new List<CardData>();

        public int Count => enhances.Count;

        public event Action OnChanged;

        // 가득 차서 쌓지 못했으면 false를 반환합니다.
        public bool Add(CardData enhance)
        {
            if (enhances.Count >= MAX_COUNT) return false;
            enhances.Add(enhance);
            NotifyChanged();
            return true;
        }

        // 들어온 순서대로 발사체에 실을 카드를 담습니다. (조합 카드는 재료 카드들로 펼쳐집니다)
        public void CollectCards(List<CardData> cards)
        {
            for (int i = 0; i < enhances.Count; i++)
                enhances[i].CollectCards(cards);
        }

        // 공격할 때마다 불리므로, 이미 비어 있으면 알리지 않습니다.
        public void Clear()
        {
            if (enhances.Count == 0)
                return;

            enhances.Clear();
            NotifyChanged();
        }

        private void NotifyChanged()
        {
            if (OnChanged != null)
                OnChanged.Invoke();
        }

        public override string ToString() => $"[{string.Join(", ", enhances.Select(e => e.CardName))}]";
    }
}
