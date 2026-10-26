using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 손패 연출 전용 뷰. 선택 여부는 판단하지 않고, 외부에서 넘겨준 인덱스대로 배치만 한다.
/// UIBuilder가 만든 HandArea에 붙인다. 자식 중 이름이 "Slot_"으로 시작하는 오브젝트를 카드 한 장의 자리로 사용한다.
/// (슬롯 = 카드 UI + 사라짐 파티클. 배치·회전·확대는 슬롯이 받으므로 둘이 항상 같은 자세를 가진다)
/// </summary>
public class HandView : MonoBehaviour
{
    private const string SlotPrefix = "Slot_";
    // 슬롯 한 개가 차지하는 정렬 칸 수. 카드 UI가 첫 칸, 슬롯 하위 파티클이 나머지 칸을 쓴다.
    private const int SortingStep = 10;

    [SerializeField] private HandLayoutSettings layout = new HandLayoutSettings();

    [Tooltip("목표 자세로 따라가는 속도 (클수록 빠름)")]
    [SerializeField, Min(1f)] private float followSpeed = 14f;

    // 화면에 보이는 손패 순서 (0 = 왼쪽 끝). 사라지는 중인 카드도 자리를 차지한다.
    private readonly List<RectTransform> hand = new List<RectTransform>();
    private readonly List<RectTransform> allCards = new List<RectTransform>();
    private readonly List<CardPose> targets = new List<CardPose>();
    // 사라지는 중인 카드: 자리는 남기되 그 자리에 멈춰 있고, 선택 인덱스 계산에서는 빠진다.
    private readonly HashSet<RectTransform> leavingCards = new HashSet<RectTransform>();
    // 사라지는 중인 카드를 뺀 순서 기준의 선택 인덱스 (= 덱 손패 인덱스)
    private int selectedIndex = -1;

    // 카드별 그리기 순서 설정. 파티클은 UI가 아니라 형제 순서를 따르지 않으므로,
    // 카드마다 중첩 Canvas를 두고 형제 순서를 그대로 정렬 순서로 옮겨 적는다.
    private readonly Dictionary<RectTransform, CardSorting> sortings = new Dictionary<RectTransform, CardSorting>();

    private class CardSorting
    {
        public Canvas Canvas;
        public ParticleSystemRenderer[] Renderers;
        public int[] Offsets; // 카드 정렬 순서 기준 파티클의 순서 (파티클끼리의 원래 앞뒤 관계 유지)
    }

    /// <summary>사라지는 중인 카드를 뺀 손패 장수</summary>
    public int Count => hand.Count - CountLeavingInHand();
    public int SelectedIndex => selectedIndex;
    /// <summary>미리 만들어 둔 슬롯 전체 (켜짐/꺼짐 포함, 이름 순)</summary>
    public IReadOnlyList<RectTransform> AllCards => allCards;

    private void Awake()
    {
        foreach (Transform child in transform)
        {
            if (child.name.StartsWith(SlotPrefix)) allCards.Add((RectTransform)child);
        }
        foreach (RectTransform card in allCards) sortings.Add(card, CreateSorting(card));
        Refresh(true);
    }

    private static CardSorting CreateSorting(RectTransform card)
    {
        if (!card.TryGetComponent(out Canvas canvas)) canvas = card.gameObject.AddComponent<Canvas>();

        // 슬롯 하위 파티클은 카드 UI 바로 위에 그려진다
        ParticleSystemRenderer[] renderers = card.GetComponentsInChildren<ParticleSystemRenderer>(true);
        int minOrder = int.MaxValue;
        foreach (ParticleSystemRenderer r in renderers) minOrder = Mathf.Min(minOrder, r.sortingOrder);

        int[] offsets = new int[renderers.Length];
        for (int i = 0; i < renderers.Length; i++)
            offsets[i] = Mathf.Clamp(renderers[i].sortingOrder - minOrder + 1, 1, SortingStep - 1);

        return new CardSorting { Canvas = canvas, Renderers = renderers, Offsets = offsets };
    }

    /// <summary>index 번째 카드를 선택 상태로 보여준다. 범위를 벗어나면 선택 해제. (사라지는 중인 카드는 세지 않음)</summary>
    public void SetSelected(int index)
    {
        int next = index >= 0 && index < Count ? index : -1;
        if (next == selectedIndex) return;
        selectedIndex = next;
        RecalculateTargets();
    }

    public void ClearSelection() => SetSelected(-1);

    /// <summary>
    /// 카드를 켜거나 끈 뒤 호출한다. 꺼진 카드는 손패에서 빠지고, 새로 켜진 카드는 오른쪽 끝에 붙는다.
    /// (사라지는 중인 카드는 꺼져 있어도 EndLeaving 전까지 자리를 유지한다)
    /// </summary>
    /// <param name="snap">true면 애니메이션 없이 즉시 배치</param>
    public void Refresh(bool snap = false)
    {
        hand.RemoveAll(card => !card.gameObject.activeSelf && !leavingCards.Contains(card));
        foreach (RectTransform card in allCards)
        {
            if (card.gameObject.activeSelf && !hand.Contains(card)) hand.Add(card);
        }

        if (selectedIndex >= Count) selectedIndex = -1;
        RecalculateTargets();

        if (snap)
        {
            for (int i = 0; i < hand.Count; i++)
            {
                if (leavingCards.Contains(hand[i])) continue;
                HandLayout.Apply(hand[i], targets[i]);
            }
        }
    }

    /// <summary>
    /// 사라지기 시작한 카드를 표시한다. 선택 연출(상승·확대)을 즉시 풀고 맨 뒤로 보낸 뒤 그 자리에 멈춘다.
    /// 선택 인덱스에서는 바로 빠지고, 자리는 EndLeaving을 부를 때까지 비워 두지 않는다.
    /// </summary>
    public void BeginLeaving(RectTransform card)
    {
        int viewIndex = hand.IndexOf(card);
        if (viewIndex < 0 || !leavingCards.Add(card)) return;

        // 선택되지 않은 자세(-1)로 즉시 되돌린다
        HandLayout.Apply(card, HandLayout.Calculate(viewIndex, hand.Count, -1, layout));

        if (selectedIndex >= Count) selectedIndex = Count - 1;
        RecalculateTargets();
    }

    /// <summary>사라짐이 끝난 카드의 자리를 비운다. (카드는 먼저 꺼져 있어야 손패에서 빠진다)</summary>
    public void EndLeaving(RectTransform card)
    {
        if (!leavingCards.Remove(card)) return;
        Refresh();
    }

    private void RecalculateTargets()
    {
        int selectedViewIndex = ToViewIndex(selectedIndex);

        targets.Clear();
        for (int i = 0; i < hand.Count; i++)
        {
            targets.Add(HandLayout.Calculate(i, hand.Count, selectedViewIndex, layout));
            hand[i].SetSiblingIndex(i);
        }

        // 선택 카드는 맨 위에 그린다
        if (selectedViewIndex >= 0) hand[selectedViewIndex].SetAsLastSibling();

        // 사라지는 중인 카드는 다른 카드를 가리지 않도록 맨 뒤에 그린다
        foreach (RectTransform card in hand)
        {
            if (leavingCards.Contains(card)) card.SetAsFirstSibling();
        }

        ApplySorting();
    }

    // 위에서 정한 형제 순서를 그대로 정렬 순서로 옮긴다. 카드 하위 파티클은 자기 카드 바로 위에 그려진다.
    private void ApplySorting()
    {
        Canvas parentCanvas = GetSortingCanvas();
        int baseOrder = parentCanvas.sortingOrder + 1;

        foreach (RectTransform card in hand)
        {
            CardSorting sorting = sortings[card];
            int order = baseOrder + card.GetSiblingIndex() * SortingStep;

            sorting.Canvas.overrideSorting = true;
            sorting.Canvas.sortingLayerID = parentCanvas.sortingLayerID;
            sorting.Canvas.sortingOrder = order;

            for (int i = 0; i < sorting.Renderers.Length; i++)
            {
                sorting.Renderers[i].sortingLayerID = parentCanvas.sortingLayerID;
                sorting.Renderers[i].sortingOrder = order + sorting.Offsets[i];
            }
        }
    }

    // HandArea를 실제로 그리는 캔버스 (정렬 순서를 직접 가진 가장 가까운 캔버스)
    private Canvas GetSortingCanvas()
    {
        Canvas canvas = transform.GetComponentInParent<Canvas>();
        while (!canvas.isRootCanvas && !canvas.overrideSorting)
            canvas = canvas.transform.parent.GetComponentInParent<Canvas>();
        return canvas;
    }

    // 사라지는 중인 카드를 뺀 인덱스를 화면 hand 인덱스로 바꾼다. 없으면 -1.
    private int ToViewIndex(int index)
    {
        if (index < 0) return -1;

        for (int i = 0; i < hand.Count; i++)
        {
            if (leavingCards.Contains(hand[i])) continue;
            if (index-- == 0) return i;
        }
        return -1;
    }

    private int CountLeavingInHand()
    {
        int count = 0;
        foreach (RectTransform card in hand)
        {
            if (leavingCards.Contains(card)) count++;
        }
        return count;
    }

    private void Update()
    {
        float k = 1f - Mathf.Exp(-followSpeed * Time.deltaTime);
        for (int i = 0; i < hand.Count; i++)
        {
            RectTransform card = hand[i];
            // 사라지는 중인 카드는 이펙트가 그 자리에서 재생되도록 움직이지 않는다
            if (leavingCards.Contains(card)) continue;

            CardPose target = targets[i];

            card.anchoredPosition = Vector2.Lerp(card.anchoredPosition, target.position, k);
            float angle = Mathf.LerpAngle(card.localEulerAngles.z, target.angle, k);
            card.localEulerAngles = new Vector3(0f, 0f, angle);
            card.localScale = Vector3.one * Mathf.Lerp(card.localScale.x, target.scale, k);
        }
    }

    private void OnValidate()
    {
        if (layout.selectedScale > layout.SafeSelectedScale)
            Debug.LogWarning($"[HandView] 선택 배율 {layout.selectedScale:0.00} → {layout.SafeSelectedScale:0.00} 로 제한됩니다 (화면 중앙 침범 방지)");
    }
}
