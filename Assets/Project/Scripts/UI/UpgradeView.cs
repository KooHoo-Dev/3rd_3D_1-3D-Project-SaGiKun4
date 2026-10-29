using System;
using System.Collections.Generic;
using Study3D.FpsGame.Deck;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Study3D.FpsGame.UI
{
    // UpgradeView
    // : 카드 강화 화면의 선택 칸(왼쪽)과 카드 목록(오른쪽)을 연결합니다. UpgradeUIBuilder가 조립하면서 참조를 넣어 줍니다. (이름으로 찾지 않음)
    // - 처음엔 첫 번째 칸이 선택되어 있고, 목록의 카드를 누르면 선택된 칸에 들어갑니다.
    //   넣은 뒤에는 비어 있는 첫 칸으로 선택이 넘어갑니다. (빈 칸이 없으면 그대로)
    // - 선택 칸을 누르면 그 칸이 선택되어, 다음에 누른 목록 카드로 바꿀 수 있습니다.
    // - 이미 다른 칸에 들어 있는 카드를 고르면 그 칸에서 빼고 선택된 칸으로 옮깁니다. (같은 카드가 두 칸에 들어가지 않음)
    // - 선택된 칸 연출(파티클)은 OnSelectedSlotChanged / SelectedSlotIndex / GetSlotRect 로 붙이면 됩니다.
    // - 목록은 LocalPlayer 덱이 가진 모든 카드(손패 → 뽑을 카드 → 버린 카드 → 강화 전용 보관함 순)입니다. 화면이 켜질 때마다 다시 읽습니다.
    public class UpgradeView : MonoBehaviour
    {
        // 카드 한 장의 글자/그림 참조
        [Serializable]
        public class CardView
        {
            public GameObject card;
            public Image artImage;
            public TMP_Text titleText;
            public TMP_Text typeText;
            public TMP_Text descText;
        }

        // 왼쪽 선택 칸
        [Serializable]
        public class SlotView
        {
            public Button button;
            [Tooltip("카드가 없을 때 보이는 회색 박스")]
            public GameObject emptyBox;
            public CardView card;
        }

        // 오른쪽 목록의 카드
        [Serializable]
        public class ListItemView
        {
            public Button button;
            public CardView card;
        }

        [SerializeField] private List<SlotView> slotViews = new List<SlotView>();
        [SerializeField] private List<ListItemView> listItems = new List<ListItemView>();
        [SerializeField] private Button confirmButton;
        [SerializeField] private Button cancelButton;

        // 선택된 칸이 바뀌었을 때 (칸 번호)
        public event Action<int> OnSelectedSlotChanged;
        // 칸에 들어간 카드가 바뀌었을 때 (칸 번호, 목록 번호 / 비면 -1)
        public event Action<int, int> OnSlotCardChanged;

        public int SelectedSlotIndex { get; private set; }
        public int SlotCount => slotViews.Count;
        public Button ConfirmButton => confirmButton;
        public Button CancelButton => cancelButton;

        // 칸마다 들어 있는 목록 번호 (-1 = 빈 칸)
        private int[] slotItems;
        // 목록에 보여 주고 있는 카드 (목록 번호 순)
        private readonly List<CardData> shownCards = new List<CardData>();
        private bool started;

#if UNITY_EDITOR
        // UpgradeUIBuilder가 조립 직후 호출합니다.
        public void EditorSetup(List<SlotView> slots, List<ListItemView> items, Button confirm, Button cancel)
        {
            slotViews = slots;
            listItems = items;
            confirmButton = confirm;
            cancelButton = cancel;
        }
#endif

        private void Awake()
        {
            slotItems = new int[slotViews.Count];
            for (int i = 0; i < slotItems.Length; i++) slotItems[i] = -1;

            for (int i = 0; i < slotViews.Count; i++)
            {
                int slot = i;
                slotViews[i].button.onClick.AddListener(() => SelectSlot(slot));
            }
            for (int i = 0; i < listItems.Count; i++)
            {
                int item = i;
                listItems[i].button.onClick.AddListener(() => OnListItemClicked(item));
            }

            ClearSlots();
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
            if (started) RefreshFromDeck();
        }

        // LocalPlayer 덱의 모든 카드를 목록에 넣습니다. (칸은 비워짐)
        public void RefreshFromDeck()
        {
            if (Player.Player.LocalPlayer == null)
            {
                Debug.LogWarning("[UpgradeView] LocalPlayer가 없어 덱 카드를 불러오지 못했습니다.");
                return;
            }

            CardDeck deck = Player.Player.LocalPlayer.Deck;
            var cards = new List<CardData>(deck.TotalCount);
            cards.AddRange(deck.Hand);
            cards.AddRange(deck.DrawPile);
            cards.AddRange(deck.DiscardPile);
            cards.AddRange(deck.ModifierPile);
            SetCards(cards);
        }

        // 칸에 들어 있는 카드 (비면 null)
        public CardData GetSlotCard(int slot) => slotItems[slot] >= 0 ? shownCards[slotItems[slot]] : null;

        // 선택된 칸의 위치 (파티클을 붙일 자리)
        public RectTransform GetSlotRect(int slot) => (RectTransform)slotViews[slot].button.transform;

        // 칸에 들어 있는 목록 번호 (비면 -1)
        public int GetSlotItemIndex(int slot) => slotItems[slot];

        public bool IsSlotFilled(int slot) => slotItems[slot] >= 0;

        public void SelectSlot(int slot)
        {
            if (slot < 0 || slot >= slotViews.Count || slot == SelectedSlotIndex) return;
            SelectedSlotIndex = slot;
            OnSelectedSlotChanged?.Invoke(slot);
        }

        // 모든 칸을 비우고 첫 번째 칸을 선택합니다.
        public void ClearSlots()
        {
            for (int i = 0; i < slotViews.Count; i++) SetSlot(i, -1);
            SelectedSlotIndex = 0;
            OnSelectedSlotChanged?.Invoke(0);
        }

        // 목록에 보여 줄 카드를 넣습니다. 남는 목록 칸은 끕니다. (칸은 비워짐)
        public void SetCards(IReadOnlyList<CardData> cards)
        {
            if (cards.Count > listItems.Count)
                Debug.LogWarning($"[UpgradeView] 목록 칸이 부족합니다. ({cards.Count}장 중 {listItems.Count}장만 표시, 빌더의 List Card Count를 늘리세요)");

            shownCards.Clear();
            for (int i = 0; i < listItems.Count; i++)
            {
                bool active = i < cards.Count;
                listItems[i].button.gameObject.SetActive(active);
                if (!active) continue;

                shownCards.Add(cards[i]);
                CardView view = listItems[i].card;
                if (view.titleText != null)
                {
                    view.titleText.text = cards[i].CardName;
                    view.titleText.color = Color.white;
                }
                if (view.typeText != null) view.typeText.text = cards[i].Type.ToString();
                // 카드 설명 데이터가 아직 없어 비워 둡니다
                if (view.descText != null) view.descText.text = string.Empty;
            }
            ClearSlots();
        }

        private void OnListItemClicked(int item)
        {
            int selected = SelectedSlotIndex;
            if (slotItems[selected] == item) return;

            // 다른 칸에 들어 있으면 그 칸에서 뺀다
            for (int i = 0; i < slotItems.Length; i++)
                if (i != selected && slotItems[i] == item) SetSlot(i, -1);

            SetSlot(selected, item);

            int empty = Array.IndexOf(slotItems, -1);
            if (empty >= 0) SelectSlot(empty);
        }

        private void SetSlot(int slot, int item)
        {
            slotItems[slot] = item;
            SlotView view = slotViews[slot];
            bool filled = item >= 0;

            if (view.emptyBox != null) view.emptyBox.SetActive(!filled);
            view.card.card.SetActive(filled);
            if (filled) CopyCard(listItems[item].card, view.card);

            OnSlotCardChanged?.Invoke(slot, item);
        }

        private static void CopyCard(CardView from, CardView to)
        {
            if (from.titleText != null && to.titleText != null)
            {
                to.titleText.text = from.titleText.text;
                to.titleText.color = from.titleText.color;
            }
            if (from.typeText != null && to.typeText != null) to.typeText.text = from.typeText.text;
            if (from.descText != null && to.descText != null) to.descText.text = from.descText.text;
            if (from.artImage != null && to.artImage != null) to.artImage.color = from.artImage.color;
        }
    }
}
