using System.Collections.Generic;
using Study3D.FpsGame.Deck;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Study3D.FpsGame.UI
{
    // InventoryPanel
    // : 메뉴 화면 왼쪽의 인벤토리 패널입니다. 오른쪽 공통 목록(CardListView)에서 누른 카드를 크게 보여 줍니다. UpgradeUIBuilder가 조립하면서 참조를 넣어 줍니다. (이름으로 찾지 않음)
    // - 일반 카드는 한 장, 강화(조합) 카드는 강화에 쓰인 카드들을 발사 순서대로 가로 목록에 나열합니다. (CollectCards로 안쪽 재료까지 펼침)
    // - 강화 재료 수에 제한이 없으므로 가로 스크롤 목록이고, 카드 칸은 템플릿을 복제해 필요한 만큼만 늘립니다. (늘린 칸은 재사용)
    // - 켜질 때와 목록이 덱을 다시 읽을 때는 아무 카드도 고르지 않은 상태로 돌아갑니다.
    public class InventoryPanel : MonoBehaviour
    {
        [SerializeField] private CardListView cardList;
        [SerializeField] private ScrollRect scroll;
        [Tooltip("복제한 카드 칸이 들어가는 가로 목록")]
        [SerializeField] private RectTransform content;
        [Tooltip("복제할 카드 칸 (꺼 둠)")]
        [SerializeField] private CardViewItem itemTemplate;
        [Tooltip("고른 카드 이름")]
        [SerializeField] private TMP_Text titleText;
        [Tooltip("카드를 고르지 않았을 때 보이는 안내")]
        [SerializeField] private GameObject emptyHint;

        private readonly List<CardViewItem> items = new List<CardViewItem>();
        private readonly List<CardData> cards = new List<CardData>();

#if UNITY_EDITOR
        // UpgradeUIBuilder가 조립 직후 호출합니다.
        public void EditorSetup(CardListView list, ScrollRect scrollRect, RectTransform listContent, CardViewItem template, TMP_Text title, GameObject hint)
        {
            cardList = list;
            scroll = scrollRect;
            content = listContent;
            itemTemplate = template;
            titleText = title;
            emptyHint = hint;
        }
#endif

        private void OnEnable()
        {
            cardList.OnCardClicked += Show;
            cardList.OnCardsRefreshed += Clear;
            Clear();
        }

        private void OnDisable()
        {
            cardList.OnCardClicked -= Show;
            cardList.OnCardsRefreshed -= Clear;
        }

        // 덱 번호의 카드를 보여 줍니다.
        public void Show(int deckIndex)
        {
            CardData card = cardList.GetCard(deckIndex);
            if (card == null)
                return;

            cards.Clear();
            card.CollectCards(cards);

            ShowItems();
            ShowTitle(card);
            scroll.gameObject.SetActive(true);
            emptyHint.SetActive(false);

            // 목록 폭이 바로 바뀌도록 레이아웃을 먼저 계산한 뒤, 첫 카드부터 보이게 맨 왼쪽으로 돌립니다
            LayoutRebuilder.ForceRebuildLayoutImmediate(content);
            scroll.horizontalNormalizedPosition = 0f;
        }

        // cards를 카드 칸마다 한 장씩 그립니다. 칸이 모자라면 템플릿을 복제해 늘리고, 남는 칸은 끕니다.
        private void ShowItems()
        {
            while (items.Count < cards.Count)
            {
                items.Add(Instantiate(itemTemplate, content, false));
            }

            for (int i = 0; i < items.Count; i++)
            {
                bool active = i < cards.Count;
                items[i].gameObject.SetActive(active);

                if (active)
                {
                    items[i].View.Show(cards[i]);
                }
            }
        }

        // 고른 카드 이름을 적습니다. 강화 카드면 재료 장수도 같이 적습니다.
        private void ShowTitle(CardData card)
        {
            if (cards.Count > 1)
            {
                titleText.text = $"{card.CardName}  (강화 카드 {cards.Count}장)";
            }
            else
            {
                titleText.text = card.CardName;
            }
            titleText.gameObject.SetActive(true);
        }

        // 아무 카드도 고르지 않은 상태로 돌립니다.
        public void Clear()
        {
            foreach (CardViewItem item in items)
            {
                item.gameObject.SetActive(false);
            }
            titleText.gameObject.SetActive(false);
            scroll.gameObject.SetActive(false);
            emptyHint.SetActive(true);
        }
    }
}
