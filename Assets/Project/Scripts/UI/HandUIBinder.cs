using System;
using System.Collections;
using System.Collections.Generic;
using Study3D.FpsGame.Deck;
using TMPro;
using UnityEngine;

namespace Study3D.FpsGame.UI
{
    // HandUIBinder
    // : LocalPlayer의 덱(손패)을 HandView에 연결합니다. HandView와 같은 HandArea에 붙입니다.
    // - 손패 순서와 화면 슬롯 순서를 똑같이 유지합니다. (shownSlots[i] = Deck.Hand[i])
    // - 슬롯(Slot_XX)은 UIBuilder가 미리 만들어 둔 것을 켜고 끄며 재사용합니다.
    //   슬롯 = 카드 UI(Card_XX) + 사라짐 파티클 (+ 등장 플래시). 모두 형제라서 카드를 꺼도 파티클은 계속 재생됩니다.
    //   슬롯마다 카드 UI, 파티클, 글자 참조는 UIBuilder가 조립하면서 slotViews에 넣어 줍니다. (이름으로 찾지 않음)
    // - 덱 이벤트만 받아서 따라가므로, 덱을 바꾸는 쪽(LocalPlayer)은 UI를 몰라도 됩니다.
    // - 드로우한 카드는 카드 UI에 CardAppearEffect가 있으면 등장 연출을 재생합니다. (없으면 바로 보임)
    // - 사용한 카드는 CardAppearEffect가 있으면 등장 때와 같은 노이즈로 카드 자체를 태워 없앱니다. (사라짐 파티클은 쓰지 않음)
    //   없으면 파티클을 재생하고, 재생 시간이 hideCardTime이 되면 카드 UI만 끕니다.
    //   다 사라지면 슬롯을 끄고 OnCardVanished로 알립니다. (이때 자리가 비워집니다)
    // - 재장전을 시작하면 남은 손패 전체에 같은 사라짐 연출을 재생합니다.
    [RequireComponent(typeof(HandView))]
    public class HandUIBinder : MonoBehaviour
    {
        // 슬롯 하나의 참조들 (UIBuilder가 채움)
        [Serializable]
        public class SlotView
        {
            public RectTransform slot;
            public GameObject card;
            [Tooltip("비우면 등장/노이즈 사라짐 연출 없음")]
            public CardAppearEffect appear;
            [Tooltip("비우면 사라짐 파티클 없음")]
            public ParticleSystem vanishEffect;
            public TMP_Text titleText;
            public TMP_Text typeText;
        }

        [SerializeField] private List<SlotView> slotViews = new List<SlotView>();

        [Header("사라짐 연출")]
        [Tooltip("사라짐 파티클의 재생 시간(Playback Time)이 이 값이 되면 카드를 끕니다 (0.2 ~ 0.3초 권장)")]
        [SerializeField, Min(0f)] private float hideCardTime = 0.25f;

        // 사라짐 파티클이 모두 끝나서 카드가 완전히 사라졌을 때 (사라진 슬롯)
        public event Action<RectTransform> OnCardVanished;

        private HandView handView;
        private CardDeck deck;

        // 지금 손패를 보여주고 있는 슬롯들 (손패 순서와 같음)
        private readonly List<RectTransform> shownSlots = new List<RectTransform>();
        private readonly Dictionary<RectTransform, SlotParts> slotParts = new Dictionary<RectTransform, SlotParts>();
        // 사라지는 중인 슬롯. 끝날 때까지 새 카드로 재사용하지 않습니다.
        private readonly HashSet<RectTransform> vanishingSlots = new HashSet<RectTransform>();

        // 슬롯 하위의 카드 UI와 사라짐 파티클 (파티클이 없는 슬롯은 카드가 바로 꺼집니다)
        private class SlotParts
        {
            public GameObject Card;
            public CardAppearEffect Appear;
            public ParticleSystem Effect;
            public ParticleSystem[] EffectSystems;
            public TMP_Text TitleText;
            public TMP_Text TypeText;
        }

        public IReadOnlyList<SlotView> SlotViews => slotViews;

#if UNITY_EDITOR
        // UIBuilder가 조립 직후 호출합니다.
        public void EditorSetup(List<SlotView> views)
        {
            slotViews = views;
        }
#endif

        private void Awake()
        {
            handView = GetComponent<HandView>();
        }

        // LocalPlayer.Awake에서 덱이 만들어진 뒤라야 하므로 Start에서 연결합니다.
        private void Start()
        {
            CollectSlotParts();

            if (Player.Player.LocalPlayer == null)
            {
                Debug.LogWarning("[HandUIBinder] LocalPlayer가 없어 손패 UI를 연결하지 못했습니다.");
                return;
            }

            deck = Player.Player.LocalPlayer.Deck;
            deck.OnCardAdded += OnCardAdded;
            deck.OnCardRemoved += OnCardRemoved;
            deck.OnHandCleared += OnHandCleared;
            deck.OnSelectionChanged += OnSelectionChanged;
            Player.Player.LocalPlayer.OnRequestedReload += OnRequestedReload;

            // LocalPlayer.Start가 먼저 돌아 이미 손패를 채웠을 수도 있으니 현재 상태로 한 번 맞춥니다.
            Rebuild();
        }

        private void OnDestroy()
        {
            // 종료나 씬 전환 때 LocalPlayer가 먼저 파괴됐으면 이미 null이라 해제할 대상이 없습니다.
            if (Player.Player.LocalPlayer != null)
                Player.Player.LocalPlayer.OnRequestedReload -= OnRequestedReload;

            if (deck == null) return;
            deck.OnCardAdded -= OnCardAdded;
            deck.OnCardRemoved -= OnCardRemoved;
            deck.OnHandCleared -= OnHandCleared;
            deck.OnSelectionChanged -= OnSelectionChanged;
        }

        #region 덱 이벤트 처리

        // 새 카드는 꺼져 있는 슬롯을 하나 꺼내 켜고, 손패 오른쪽 끝에 붙입니다.
        private void OnCardAdded(CardData card)
        {
            RectTransform slot = GetHiddenSlotOrNull();
            if (slot == null)
            {
                Debug.LogWarning($"[HandUIBinder] 보여줄 슬롯이 부족합니다. (최대 {handView.AllCards.Count}장)");
                return;
            }

            SlotParts parts = slotParts[slot];
            ApplyCardData(parts, card);
            parts.Card.SetActive(true);
            slot.gameObject.SetActive(true);
            shownSlots.Add(slot);
            // 한 장 켤 때마다 Refresh해야 HandView도 켜진 순서대로 오른쪽 끝에 붙여줍니다.
            handView.Refresh();

            // 카드가 켜진 뒤라야 등장 연출(코루틴)을 시작할 수 있습니다.
            if (parts.Appear != null) parts.Appear.Play();
        }

        // 사용한 카드는 사라짐 연출을 시작합니다. 덱 순서와 맞추기 위해 손패 목록에서는 바로 뺍니다.
        private void OnCardRemoved(int index)
        {
            if (index < 0 || index >= shownSlots.Count) return;

            RectTransform slot = shownSlots[index];
            shownSlots.RemoveAt(index);
            StartCoroutine(VanishRoutine(slot));
        }

        private void OnHandCleared()
        {
            HideAll();
            handView.Refresh();
        }

        private void OnSelectionChanged(int index)
        {
            handView.SetSelected(index);
        }

        // 재장전(R키)을 시작하면 남은 손패를 모두 사라짐 연출로 보냅니다. 손패가 비어 있으면 아무 일도 없습니다.
        // - 덱에서 실제로 버리는 건 재장전 완료 시점(OnHandCleared)이고, 여기서는 화면만 먼저 비웁니다.
        // - 완료 시점의 HideAll은 사라지는 중인 슬롯을 건너뛰므로 연출이 끊기지 않습니다.
        private void OnRequestedReload()
        {
            foreach (RectTransform slot in shownSlots) StartCoroutine(VanishRoutine(slot));
            shownSlots.Clear();
        }

        #endregion

        #region 사라짐 연출

        // 0. 선택 연출 해제, 맨 뒤로
        // → (노이즈) 1. 카드를 노이즈대로 태움 → 2. 다 타면 카드 UI 끄기
        // → (파티클) 1. 파티클 재생 → 2. 재생 시간이 hideCardTime이 되면 카드 UI 끄기 → 3. 파티클이 모두 끝날 때까지 대기
        // → 슬롯을 끄고 알림
        private IEnumerator VanishRoutine(RectTransform slot)
        {
            vanishingSlots.Add(slot);
            // 상승·확대를 먼저 풀고 맨 뒤로 보냅니다. 파티클은 슬롯 하위라 풀린 자세와 순서를 그대로 따릅니다.
            handView.BeginLeaving(slot);

            SlotParts parts = slotParts[slot];
            if (parts.Appear != null && parts.Appear.CanVanish && parts.Card.activeInHierarchy)
            {
                parts.Appear.PlayVanish();
                while (parts.Appear.IsVanishing) yield return null;
                // 다 탄 상태에서 끄므로 번쩍임 없이 사라지고, 꺼질 때 카드는 원래 머티리얼로 돌아갑니다.
                parts.Card.SetActive(false);
            }
            else if (parts.Effect != null)
            {
                parts.Effect.gameObject.SetActive(true);
                parts.Effect.Play(true);

                while (parts.Effect.time < hideCardTime && IsFinished(parts) == false) yield return null;
                parts.Card.SetActive(false);

                while (IsFinished(parts) == false) yield return null;
                StopEffect(parts);
            }
            else
            {
                parts.Card.SetActive(false);
            }

            slot.gameObject.SetActive(false);
            vanishingSlots.Remove(slot);
            handView.EndLeaving(slot);
            OnCardVanished?.Invoke(slot);
        }

        // 슬롯마다 넣어 둔 참조를 정리해 둡니다. 파티클은 꺼 둡니다.
        // (켜 둔 채로 두면 Play On Awake 때문에 슬롯이 켜질 때마다, 즉 드로우할 때마다 재생됩니다)
        private void CollectSlotParts()
        {
            foreach (SlotView view in slotViews)
            {
                if (view.slot == null || view.card == null)
                {
                    Debug.LogWarning("[HandUIBinder] 슬롯이나 카드 UI가 비어 있는 칸이 있습니다. UIBuilder로 다시 조립하세요.");
                    continue;
                }

                SlotParts parts = new SlotParts
                {
                    Card = view.card,
                    Appear = view.appear,
                    Effect = view.vanishEffect,
                    TitleText = view.titleText,
                    TypeText = view.typeText,
                };
                if (parts.Effect != null)
                {
                    parts.EffectSystems = parts.Effect.GetComponentsInChildren<ParticleSystem>(true);
                    StopEffect(parts);
                }
                slotParts.Add(view.slot, parts);
            }
        }

        private static void StopEffect(SlotParts parts)
        {
            parts.Effect.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            parts.Effect.gameObject.SetActive(false);
        }

        // 반복(Looping)하지 않는 파티클이 모두 끝났는지 확인합니다.
        // 반복 파티클은 스스로 끝나지 않으므로 기다리지 않습니다.
        private static bool IsFinished(SlotParts parts)
        {
            foreach (ParticleSystem system in parts.EffectSystems)
            {
                if (system.main.loop) continue;
                if (system.IsAlive(false)) return false;
            }
            return true;
        }

        #endregion

        // 현재 손패 그대로 화면을 다시 만듭니다.
        private void Rebuild()
        {
            HideAll();
            handView.Refresh(true);

            foreach (CardData card in deck.Hand) OnCardAdded(card);
            handView.SetSelected(deck.Hand.Count > 0 ? deck.SelectedIndex : -1);
        }

        // 손패를 모두 끕니다. 사라지는 중인 슬롯은 연출이 마저 처리합니다.
        private void HideAll()
        {
            foreach (RectTransform slot in handView.AllCards)
            {
                if (vanishingSlots.Contains(slot)) continue;
                slot.gameObject.SetActive(false);
            }
            shownSlots.Clear();
        }

        private RectTransform GetHiddenSlotOrNull()
        {
            foreach (RectTransform slot in handView.AllCards)
            {
                if (slot.gameObject.activeSelf || vanishingSlots.Contains(slot) || !slotParts.ContainsKey(slot)) continue;
                return slot;
            }
            return null;
        }

        // 카드 UI의 글자를 카드 데이터로 바꿉니다.
        private static void ApplyCardData(SlotParts parts, CardData card)
        {
            if (parts.TitleText != null) parts.TitleText.text = card.CardName;
            if (parts.TypeText != null) parts.TypeText.text = card.Type.ToString();
        }
    }
}
