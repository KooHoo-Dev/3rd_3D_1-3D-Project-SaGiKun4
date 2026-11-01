using System;
using System.Collections.Generic;
using Study3D.FpsGame.Deck;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Study3D.FpsGame.UI
{
    // CardListView
    // : 메뉴 화면 오른쪽의 카드 목록입니다. 강화/인벤토리/옵션 화면이 같이 씁니다. UpgradeUIBuilder가 조립하면서 참조를 넣어 줍니다. (이름으로 찾지 않음)
    // - 목록은 LocalPlayer 덱이 가진 모든 카드(손패 → 뽑을 카드 → 버린 카드 → 강화 전용 보관함 순)입니다. 켜질 때마다 다시 읽습니다.
    // - 정렬 메뉴로 보여 주는 순서만 바꿉니다. 밖으로는 항상 덱 번호로 알려 줍니다. (정렬과 상관없이 같은 카드를 가리킴)
    // - 카드를 누르면 OnCardClicked(덱 번호)만 알립니다. 누른 뒤 무엇을 할지는 왼쪽 패널이 정합니다.
    // - 왼쪽 패널이 SetDimFilter로 고를 수 없는 카드를 알려 주면 흐리게 표시하고 클릭을 막습니다. (정렬을 바꿔도 유지)
    public class CardListView : MonoBehaviour
    {
        // 목록 정렬 방식 (정렬 메뉴의 순서와 같음)
        public enum SortMode
        {
            Acquired, // 획득순 (덱 순서 그대로)
            Name,     // 이름순 (가나다)
        }

        // 목록의 카드
        [Serializable]
        public class ItemView
        {
            public Button button;
            public CardView card;
        }

        [SerializeField] private List<ItemView> items = new List<ItemView>();
        [SerializeField] private TMP_Dropdown sortDropdown;
        [Tooltip("고를 수 없는 카드의 투명도 (배경이 어두워서 낮출수록 어둡게 보임)")]
        [SerializeField, Range(0f, 1f)] private float dimAlpha = 0.35f;

        // 목록 카드를 눌렀을 때 (덱 번호)
        public event Action<int> OnCardClicked;
        // 덱을 다시 읽었을 때 (이전 덱 번호는 더 이상 맞지 않음)
        public event Action OnCardsRefreshed;

        public SortMode CurrentSortMode { get; private set; }

        // 덱의 카드 (덱 번호 순)
        private readonly List<CardData> deckCards = new List<CardData>();
        // 목록 위치마다 보여 주는 덱 번호 (정렬 결과)
        private readonly List<int> displayOrder = new List<int>();
        // 목록 카드마다 흐리게 만들 CanvasGroup (items와 같은 순서)
        private readonly List<CanvasGroup> itemGroups = new List<CanvasGroup>();
        // true를 돌려준 카드는 흐리게 표시합니다. null이면 모두 원래대로
        private Func<CardData, bool> dimFilter;
        private bool started;

#if UNITY_EDITOR
        // UpgradeUIBuilder가 조립 직후 호출합니다.
        public void EditorSetup(List<ItemView> listItems, TMP_Dropdown sort)
        {
            items = listItems;
            sortDropdown = sort;
        }
#endif

        private void Awake()
        {
            for (int i = 0; i < items.Count; i++)
            {
                int item = i;
                items[i].button.onClick.AddListener(() => OnItemClicked(item));

                GameObject itemObject = items[i].button.gameObject;
                itemGroups.Add(GetOrAddCanvasGroup(itemObject));
            }

            if (sortDropdown != null)
            {
                sortDropdown.onValueChanged.AddListener(OnSortDropdownChanged);
            }
        }

        // LocalPlayer.Awake에서 덱이 만들어진 뒤라야 하므로 처음 한 번은 Start에서 읽습니다.
        private void Start()
        {
            started = true;
            RefreshFromDeck();
        }

        // 다시 켜질 때마다 그 사이 바뀐 덱을 반영합니다.
        private void OnEnable()
        {
            if (started)
            {
                RefreshFromDeck();
            }
        }

        // 목록 위치(item)의 카드를 눌렀을 때 덱 번호로 바꿔 알립니다.
        private void OnItemClicked(int item)
        {
            if (OnCardClicked != null)
            {
                OnCardClicked.Invoke(displayOrder[item]);
            }
        }

        private void OnSortDropdownChanged(int value)
        {
            SetSortMode((SortMode)value);
        }

        private static CanvasGroup GetOrAddCanvasGroup(GameObject target)
        {
            CanvasGroup group;
            if (!target.TryGetComponent(out group))
            {
                group = target.AddComponent<CanvasGroup>();
            }
            return group;
        }

        // 덱 번호의 카드 (없으면 null)
        public CardData GetCard(int deckIndex)
        {
            if (deckIndex < 0 || deckIndex >= deckCards.Count)
                return null;

            return deckCards[deckIndex];
        }

        // 덱 번호의 카드를 그리고 있는 목록 카드
        public CardView GetCardView(int deckIndex)
        {
            int item = displayOrder.IndexOf(deckIndex);
            return items[item].card;
        }

        public void RefreshFromDeck()
        {
            if (Player.Player.LocalPlayer == null)
            {
                Debug.LogWarning("[CardListView] LocalPlayer가 없어 덱 카드를 불러오지 못했습니다.");
                return;
            }

            CardDeck deck = Player.Player.LocalPlayer.Deck;
            List<CardData> cards = new List<CardData>(deck.TotalCount);
            cards.AddRange(deck.Hand);
            cards.AddRange(deck.DrawPile);
            cards.AddRange(deck.DiscardPile);
            cards.AddRange(deck.ModifierPile);
            SetCards(cards);
        }

        // 덱 카드(덱 번호 순)를 넣고 현재 정렬로 목록을 그립니다.
        public void SetCards(IReadOnlyList<CardData> cards)
        {
            if (cards.Count > items.Count)
            {
                Debug.LogWarning($"[CardListView] 목록 칸이 부족합니다. ({cards.Count}장 중 {items.Count}장만 표시, 빌더의 List Card Count를 늘리세요)");
            }

            deckCards.Clear();
            for (int i = 0; i < cards.Count && i < items.Count; i++)
            {
                deckCards.Add(cards[i]);
            }

            SortAndShow();

            if (OnCardsRefreshed != null)
            {
                OnCardsRefreshed.Invoke();
            }
        }

        // 목록 순서만 바꿉니다. 덱 번호는 그대로입니다.
        public void SetSortMode(SortMode mode)
        {
            if (mode == CurrentSortMode)
                return;

            CurrentSortMode = mode;
            if (sortDropdown != null)
            {
                sortDropdown.SetValueWithoutNotify((int)mode);
            }
            SortAndShow();
        }

        private void SortAndShow()
        {
            BuildDisplayOrder();
            ShowItems();
            ApplyDim();
        }

        // 현재 정렬 방식으로 목록 위치마다 보여 줄 덱 번호를 정합니다.
        private void BuildDisplayOrder()
        {
            displayOrder.Clear();
            for (int i = 0; i < deckCards.Count; i++)
            {
                displayOrder.Add(i);
            }

            if (CurrentSortMode == SortMode.Name)
            {
                displayOrder.Sort(CompareByName);
            }
        }

        // 한글 음절은 유니코드 순서가 가나다순입니다. 이름이 같으면 획득순을 유지합니다
        private int CompareByName(int a, int b)
        {
            int byName = string.CompareOrdinal(deckCards[a].CardName, deckCards[b].CardName);
            if (byName != 0)
                return byName;

            return a.CompareTo(b);
        }

        // 정렬된 순서대로 목록 카드를 그립니다. 남는 칸은 끕니다.
        private void ShowItems()
        {
            for (int i = 0; i < items.Count; i++)
            {
                bool active = i < displayOrder.Count;
                items[i].button.gameObject.SetActive(active);

                if (active)
                {
                    items[i].card.Show(deckCards[displayOrder[i]]);
                }
            }
        }

        // 고를 수 없는 카드를 정합니다. (null이면 모두 원래대로) 정렬을 바꾸거나 덱을 다시 읽어도 유지됩니다.
        public void SetDimFilter(Func<CardData, bool> filter)
        {
            dimFilter = filter;
            ApplyDim();
        }

        private void ApplyDim()
        {
            for (int i = 0; i < displayOrder.Count && i < itemGroups.Count; i++)
            {
                bool dim = dimFilter != null && dimFilter(deckCards[displayOrder[i]]);

                if (dim)
                {
                    itemGroups[i].alpha = dimAlpha;
                }
                else
                {
                    itemGroups[i].alpha = 1f;
                }
                // interactable을 끄면 버튼이 비활성 색으로도 바뀌므로, 클릭만 막습니다
                itemGroups[i].blocksRaycasts = !dim;
            }
        }
    }
}
