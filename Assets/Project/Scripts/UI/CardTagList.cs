using System.Collections.Generic;
using Study3D.FpsGame.Deck;
using UnityEngine;

namespace Study3D.FpsGame.UI
{
    // CardTagList
    // : 강화된 카드의 오른쪽 위에 옷 태그처럼 강화 카드를 한 장씩 붙여 보여 줍니다. 카드 아래에 두고 CardView.tags로 연결합니다.
    // - 맨 위는 "강화" 머리 태그, 그 아래로 본체 다음 카드부터 발동 순서대로 이름 태그를 붙입니다. (같은 카드도 따로 붙임)
    // - 이름 태그가 maxTags개를 넘으면 마지막 칸에 "+n"으로 남은 장수를 적습니다.
    // - 강화되지 않은 카드(한 장짜리)는 태그 목록을 끕니다.
    // - 태그는 템플릿을 복제해 필요한 만큼만 늘리고, 늘린 태그는 재사용합니다. 세로 배치는 같은 오브젝트의 VerticalLayoutGroup이 맡습니다.
    public class CardTagList : MonoBehaviour
    {
        [Tooltip("복제할 태그 (꺼 둠)")]
        [SerializeField] private CardTag tagTemplate;
        [SerializeField] private string headerText = "강화";
        [Tooltip("머리 태그를 뺀 최대 태그 수. 넘으면 마지막 태그가 +n이 됩니다")]
        [SerializeField, Min(1)] private int maxTags = 6;

        private readonly List<CardTag> tags = new List<CardTag>();

#if UNITY_EDITOR
        // UpgradeUIBuilder가 조립 직후 호출합니다.
        public void EditorSetup(CardTag template)
        {
            tagTemplate = template;
        }
#endif

        // cards: 발동 순서로 펼친 카드 (0번 = 카드 면에 그리는 본체, 1번부터 강화 카드)
        public void Show(List<CardData> cards)
        {
            int enhanceCount = cards.Count - 1;
            if (enhanceCount <= 0)
            {
                gameObject.SetActive(false);
                return;
            }
            gameObject.SetActive(true);

            int nameCount = CountNameTags(enhanceCount);
            int restCount = enhanceCount - nameCount;
            int tagCount = 1 + nameCount;
            if (restCount > 0)
            {
                tagCount++;
            }

            EnsureTags(tagCount);
            tags[0].SetText(headerText);
            for (int i = 0; i < nameCount; i++)
            {
                tags[1 + i].SetText(cards[1 + i].CardName);
            }
            if (restCount > 0)
            {
                tags[tagCount - 1].SetText($"+{restCount}");
            }
            ShowOnly(tagCount);
        }

        // 이름을 적을 태그 수. 다 들어가면 전부, 넘치면 마지막 칸을 +n에 내줍니다.
        private int CountNameTags(int enhanceCount)
        {
            if (enhanceCount <= maxTags)
                return enhanceCount;

            return maxTags - 1;
        }

        // 태그가 모자라면 템플릿을 복제해 늘립니다.
        private void EnsureTags(int count)
        {
            while (tags.Count < count)
            {
                CardTag tag = Instantiate(tagTemplate, transform, false);
                tags.Add(tag);
            }
        }

        // 앞에서부터 count개만 켜고 나머지는 끕니다.
        private void ShowOnly(int count)
        {
            for (int i = 0; i < tags.Count; i++)
            {
                tags[i].gameObject.SetActive(i < count);
            }
        }
    }
}
