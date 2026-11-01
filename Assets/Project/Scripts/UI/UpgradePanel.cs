using System;
using System.Collections.Generic;
using Study3D.FpsGame.Deck;
using UnityEngine;
using UnityEngine.UI;

namespace Study3D.FpsGame.UI
{
    // UpgradePanel
    // : 메뉴 화면 왼쪽의 강화 패널입니다. 선택 칸 2개 + 강화 버튼 + 비우기 버튼. UpgradeUIBuilder가 조립하면서 참조를 넣어 줍니다. (이름으로 찾지 않음)
    // - 카드는 오른쪽 공통 목록(CardListView)에서 고릅니다. 이 패널이 켜져 있는 동안만 목록 클릭을 받습니다.
    // - 처음엔 첫 번째 칸이 선택되어 있고, 목록의 카드를 누르면 선택된 칸에 들어갑니다.
    //   넣은 뒤에는 비어 있는 첫 칸으로 선택이 넘어갑니다. (빈 칸이 없으면 그대로)
    // - 선택 칸을 누르면 그 칸이 선택되어, 다음에 누른 목록 카드로 바꿀 수 있습니다.
    // - 이미 다른 칸에 들어 있는 카드를 고르면 그 칸에서 빼고 선택된 칸으로 옮깁니다. (같은 카드가 두 칸에 들어가지 않음)
    // - 비우기 버튼은 모든 칸을 비우고 첫 번째 칸을 선택합니다.
    // - 반대쪽 칸(선택되지 않은 칸)에 카드가 있으면, 그 카드와 시점 변경이 안 되는 목록 카드를 흐리게 표시합니다.
    //   (예: 반사 카드가 있으면 소멸 시점 변경 카드가 흐려짐. 판단은 CardEnhancer.IsTimingBlocked)
    // - 강화 버튼: 두 칸이 모두 찼고 강화에 성공하면 OnEnhanced를 알립니다. (화면을 닫는 것은 MenuScreen)
    // - 선택된 칸 연출(파티클)은 OnSelectedSlotChanged / SelectedSlotIndex / GetSlotRect 로 붙이면 됩니다.
    public class UpgradePanel : MonoBehaviour
    {
        // 왼쪽 선택 칸
        [Serializable]
        public class SlotView
        {
            public Button button;
            [Tooltip("카드가 없을 때 보이는 회색 박스")]
            public GameObject emptyBox;
            public CardView card;
        }

        [SerializeField] private CardListView cardList;
        [SerializeField] private List<SlotView> slotViews = new List<SlotView>();
        [SerializeField] private Button confirmButton;
        [SerializeField] private Button clearButton;

        // 선택된 칸이 바뀌었을 때 (칸 번호)
        public event Action<int> OnSelectedSlotChanged;
        // 칸에 들어간 카드가 바뀌었을 때 (칸 번호, 덱 번호 / 비면 -1)
        public event Action<int, int> OnSlotCardChanged;
        // 강화에 성공했을 때
        public event Action OnEnhanced;

        public int SelectedSlotIndex { get; private set; }
        public int SlotCount => slotViews.Count;

        // 칸마다 들어 있는 덱 번호 (-1 = 빈 칸)
        private int[] slotItems;
        // 흐림 판단 기준이 되는 반대쪽 칸의 카드 (UpdateDim에서 정함)
        private CardData otherSlotCard;

#if UNITY_EDITOR
        // UpgradeUIBuilder가 조립 직후 호출합니다.
        public void EditorSetup(CardListView list, List<SlotView> slots, Button confirm, Button clear)
        {
            cardList = list;
            slotViews = slots;
            confirmButton = confirm;
            clearButton = clear;
        }
#endif

        private void Awake()
        {
            slotItems = new int[slotViews.Count];
            for (int i = 0; i < slotItems.Length; i++)
            {
                slotItems[i] = -1;
            }

            for (int i = 0; i < slotViews.Count; i++)
            {
                int slot = i;
                slotViews[i].button.onClick.AddListener(() => SelectSlot(slot));
            }
            confirmButton.onClick.AddListener(Confirm);
            clearButton.onClick.AddListener(ClearSlots);
        }

        // 켜질 때마다 빈 칸에서 시작합니다. 목록이 덱을 다시 읽으면 덱 번호가 바뀔 수 있으므로 그때도 비웁니다.
        private void OnEnable()
        {
            cardList.OnCardClicked += OnCardClicked;
            cardList.OnCardsRefreshed += ClearSlots;
            ClearSlots();
        }

        private void OnDisable()
        {
            cardList.OnCardClicked -= OnCardClicked;
            cardList.OnCardsRefreshed -= ClearSlots;
            // 목록은 다른 패널(인벤토리 등)도 같이 쓰므로 흐림을 풀어 둡니다
            cardList.SetDimFilter(null);
        }

        // 칸에 들어 있는 카드 (비면 null)
        public CardData GetSlotCard(int slot)
        {
            return cardList.GetCard(slotItems[slot]);
        }

        // 선택된 칸의 위치 (파티클을 붙일 자리)
        public RectTransform GetSlotRect(int slot)
        {
            return (RectTransform)slotViews[slot].button.transform;
        }

        // 칸에 들어 있는 덱 번호 (비면 -1)
        public int GetSlotDeckIndex(int slot)
        {
            return slotItems[slot];
        }

        public bool IsSlotFilled(int slot)
        {
            return slotItems[slot] >= 0;
        }

        public void SelectSlot(int slot)
        {
            if (slot < 0 || slot >= slotViews.Count)
                return;

            if (slot == SelectedSlotIndex)
                return;

            SelectedSlotIndex = slot;
            UpdateDim();

            if (OnSelectedSlotChanged != null)
            {
                OnSelectedSlotChanged.Invoke(slot);
            }
        }

        // 모든 칸을 비우고 첫 번째 칸을 선택합니다.
        public void ClearSlots()
        {
            for (int i = 0; i < slotViews.Count; i++)
            {
                SetSlot(i, -1);
            }
            SelectedSlotIndex = 0;
            UpdateDim();

            if (OnSelectedSlotChanged != null)
            {
                OnSelectedSlotChanged.Invoke(0);
            }
        }

        // 반대쪽 칸의 카드와 시점 변경이 안 되는 목록 카드를 흐리게 합니다. 반대쪽 칸이 비면 모두 원래대로.
        // (강화 칸은 2개라서 반대쪽 칸 = 선택되지 않은 칸)
        private void UpdateDim()
        {
            if (slotViews.Count != 2)
                return;

            int otherSlot = 1 - SelectedSlotIndex;
            otherSlotCard = GetSlotCard(otherSlot);

            if (otherSlotCard == null)
            {
                cardList.SetDimFilter(null);
            }
            else
            {
                cardList.SetDimFilter(IsBlockedWithOtherSlot);
            }
        }

        // 목록 카드가 반대쪽 칸의 카드와 시점 변경이 안 되면 true (흐리게 표시)
        private bool IsBlockedWithOtherSlot(CardData card)
        {
            return CardEnhancer.IsTimingBlocked(card, otherSlotCard);
        }

        private void OnCardClicked(int deckIndex)
        {
            int selected = SelectedSlotIndex;
            if (slotItems[selected] == deckIndex)
                return;

            RemoveFromOtherSlots(deckIndex, selected);
            SetSlot(selected, deckIndex);
            SelectFirstEmptySlot();
        }

        // 다른 칸에 들어 있으면 그 칸에서 뺀다
        private void RemoveFromOtherSlots(int deckIndex, int selected)
        {
            for (int i = 0; i < slotItems.Length; i++)
            {
                if (i != selected && slotItems[i] == deckIndex)
                {
                    SetSlot(i, -1);
                }
            }
        }

        // 비어 있는 첫 칸을 선택합니다. (빈 칸이 없으면 그대로)
        private void SelectFirstEmptySlot()
        {
            int empty = Array.IndexOf(slotItems, -1);
            if (empty >= 0)
            {
                SelectSlot(empty);
            }
        }

        private void SetSlot(int slot, int deckIndex)
        {
            slotItems[slot] = deckIndex;
            SlotView view = slotViews[slot];
            bool filled = deckIndex >= 0;

            if (view.emptyBox != null)
            {
                view.emptyBox.SetActive(!filled);
            }
            view.card.card.SetActive(filled);
            if (filled)
            {
                view.card.CopyFrom(cardList.GetCardView(deckIndex));
            }

            UpdateDim();

            if (OnSlotCardChanged != null)
            {
                OnSlotCardChanged.Invoke(slot, deckIndex);
            }
        }

        // 두 칸이 모두 찼고 강화에 성공하면 알립니다. 아니면 그대로 둡니다.
        private void Confirm()
        {
            int firstIndex = slotItems[0];
            int secondIndex = slotItems[1];
            if (firstIndex < 0 || secondIndex < 0)
            {
                Debug.Log("[Enhance] 카드 두 장을 모두 골라야 강화할 수 있습니다.");
                return;
            }

            bool enhanced = CardEnhancer.TryEnhance(Player.Player.LocalPlayer.Deck, firstIndex, secondIndex);
            if (enhanced && OnEnhanced != null)
            {
                OnEnhanced.Invoke();
            }
        }
    }
}
