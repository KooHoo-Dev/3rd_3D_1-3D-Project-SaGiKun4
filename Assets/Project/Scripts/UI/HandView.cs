using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 손패 연출 전용 뷰. 선택 여부는 판단하지 않고, 외부에서 넘겨준 인덱스대로 배치만 한다.
/// UIBuilder가 만든 HandArea에 붙인다. UIBuilder가 슬롯 목록을 넣어 주며, 그 슬롯을 카드 한 장의 자리로 사용한다. (이름으로 찾지 않음)
/// (슬롯 = 카드 UI + 사라짐 파티클. 배치·회전·확대는 슬롯이 받으므로 둘이 항상 같은 자세를 가진다)
/// </summary>
public class HandView : MonoBehaviour
{
    // 슬롯 한 개가 차지하는 정렬 칸 수. 카드 UI가 첫 칸, 슬롯 하위 파티클이 나머지 칸을 쓴다.
    private const int SortingStep = 10;

    [Tooltip("카드 한 장의 자리들 (UIBuilder가 채움)")]
    [SerializeField] private List<RectTransform> slots = new List<RectTransform>();

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
    public int Count
    {
        get { return hand.Count - CountLeavingInHand(); }
    }
    public int SelectedIndex => selectedIndex;
    /// <summary>미리 만들어 둔 슬롯 전체 (켜짐/꺼짐 포함, 이름 순)</summary>
    public IReadOnlyList<RectTransform> AllCards => allCards;

    private void Awake()
    {
        foreach (RectTransform slot in slots)
        {
            if (slot != null)
            {
                allCards.Add(slot);
            }
        }

        foreach (RectTransform card in allCards)
        {
            sortings.Add(card, CreateSorting(card));
        }

        Refresh(true);
    }

#if UNITY_EDITOR
    /// <summary>UIBuilder가 조립 직후 슬롯과 배치 값을 넣는다. (배치 값은 복사해서 따로 가짐)</summary>
    public void EditorSetup(List<RectTransform> builtSlots, HandLayoutSettings settings)
    {
        slots = new List<RectTransform>(builtSlots);
        layout = JsonUtility.FromJson<HandLayoutSettings>(JsonUtility.ToJson(settings));
    }
#endif

    private static CardSorting CreateSorting(RectTransform card)
    {
        Canvas canvas;
        if (!card.TryGetComponent(out canvas))
        {
            canvas = card.gameObject.AddComponent<Canvas>();
        }

        // 슬롯 하위 파티클은 카드 UI 바로 위에 그려진다
        ParticleSystemRenderer[] renderers = card.GetComponentsInChildren<ParticleSystemRenderer>(true);
        int minOrder = int.MaxValue;
        foreach (ParticleSystemRenderer particleRenderer in renderers)
        {
            minOrder = Mathf.Min(minOrder, particleRenderer.sortingOrder);
        }

        int[] offsets = new int[renderers.Length];
        for (int i = 0; i < renderers.Length; i++)
        {
            offsets[i] = Mathf.Clamp(renderers[i].sortingOrder - minOrder + 1, 1, SortingStep - 1);
        }

        CardSorting sorting = new CardSorting();
        sorting.Canvas = canvas;
        sorting.Renderers = renderers;
        sorting.Offsets = offsets;
        return sorting;
    }

    /// <summary>index 번째 카드를 선택 상태로 보여준다. 범위를 벗어나면 선택 해제. (사라지는 중인 카드는 세지 않음)</summary>
    public void SetSelected(int index)
    {
        int next = -1;
        if (index >= 0 && index < Count)
        {
            next = index;
        }

        if (next == selectedIndex)
            return;

        selectedIndex = next;
        RecalculateTargets();
    }

    public void ClearSelection()
    {
        SetSelected(-1);
    }

    /// <summary>
    /// 카드를 켜거나 끈 뒤 호출한다. 꺼진 카드는 손패에서 빠지고, 새로 켜진 카드는 오른쪽 끝에 붙는다.
    /// (사라지는 중인 카드는 꺼져 있어도 EndLeaving 전까지 자리를 유지한다)
    /// </summary>
    /// <param name="snap">true면 애니메이션 없이 즉시 배치</param>
    public void Refresh(bool snap = false)
    {
        RemoveHiddenCardsFromHand();
        AddShownCardsToHand();

        if (selectedIndex >= Count)
        {
            selectedIndex = -1;
        }
        RecalculateTargets();

        if (snap)
        {
            SnapToTargets();
        }
    }

    // 꺼진 카드를 손패에서 뺀다. (사라지는 중인 카드는 자리를 유지)
    private void RemoveHiddenCardsFromHand()
    {
        // 뒤에서부터 지워야 남은 카드의 순서가 그대로 유지된다
        for (int i = hand.Count - 1; i >= 0; i--)
        {
            RectTransform card = hand[i];
            if (!card.gameObject.activeSelf && !leavingCards.Contains(card))
            {
                hand.RemoveAt(i);
            }
        }
    }

    // 새로 켜진 카드를 손패 오른쪽 끝에 붙인다.
    private void AddShownCardsToHand()
    {
        foreach (RectTransform card in allCards)
        {
            if (card.gameObject.activeSelf && !hand.Contains(card))
            {
                hand.Add(card);
            }
        }
    }

    // 애니메이션 없이 목표 자세로 바로 옮긴다. (사라지는 중인 카드는 그 자리에 둔다)
    private void SnapToTargets()
    {
        for (int i = 0; i < hand.Count; i++)
        {
            if (leavingCards.Contains(hand[i]))
                continue;

            HandLayout.Apply(hand[i], targets[i]);
        }
    }

    /// <summary>
    /// 사라지기 시작한 카드를 표시한다. 선택 연출(상승·확대)을 즉시 풀고 맨 뒤로 보낸 뒤 그 자리에 멈춘다.
    /// 선택 인덱스에서는 바로 빠지고, 자리는 EndLeaving을 부를 때까지 비워 두지 않는다.
    /// </summary>
    public void BeginLeaving(RectTransform card)
    {
        int viewIndex = hand.IndexOf(card);
        if (viewIndex < 0)
            return;

        // 이미 사라지는 중이면 무시
        if (!leavingCards.Add(card))
            return;

        // 선택되지 않은 자세(-1)로 즉시 되돌린다
        HandLayout.Apply(card, HandLayout.Calculate(viewIndex, hand.Count, -1, layout));

        if (selectedIndex >= Count)
        {
            selectedIndex = Count - 1;
        }
        RecalculateTargets();
    }

    /// <summary>사라짐이 끝난 카드의 자리를 비운다. (카드는 먼저 꺼져 있어야 손패에서 빠진다)</summary>
    public void EndLeaving(RectTransform card)
    {
        if (!leavingCards.Remove(card))
            return;

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
        if (selectedViewIndex >= 0)
        {
            hand[selectedViewIndex].SetAsLastSibling();
        }

        // 사라지는 중인 카드는 다른 카드를 가리지 않도록 맨 뒤에 그린다
        foreach (RectTransform card in hand)
        {
            if (leavingCards.Contains(card))
            {
                card.SetAsFirstSibling();
            }
        }

        ApplySorting();
    }

    // 위에서 정한 형제 순서를 그대로 정렬 순서로 옮긴다. 카드 하위 파티클은 자기 카드 바로 위에 그려진다.
    // 손패는 부모 캔버스보다 아래(음수 쪽)에서 정렬한다. 맨 앞 카드가 -SortingStep, 그 뒤로 -2배, -3배...
    // (같은 순서의 다른 캔버스인 강화 화면 등을 손패가 덮지 않게)
    private void ApplySorting()
    {
        Canvas parentCanvas = GetSortingCanvas();
        int baseOrder = parentCanvas.sortingOrder;
        int siblingCount = transform.childCount;

        foreach (RectTransform card in hand)
        {
            CardSorting sorting = sortings[card];
            int order = baseOrder - (siblingCount - card.GetSiblingIndex()) * SortingStep;

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
        {
            canvas = canvas.transform.parent.GetComponentInParent<Canvas>();
        }
        return canvas;
    }

    // 사라지는 중인 카드를 뺀 인덱스를 화면 hand 인덱스로 바꾼다. 없으면 -1.
    private int ToViewIndex(int index)
    {
        if (index < 0)
            return -1;

        // 사라지는 중이 아닌 카드만 세면서, index 번째 카드가 나오면 그 화면 위치를 돌려준다
        int remaining = index;
        for (int i = 0; i < hand.Count; i++)
        {
            if (leavingCards.Contains(hand[i]))
                continue;

            if (remaining == 0)
                return i;

            remaining--;
        }
        return -1;
    }

    private int CountLeavingInHand()
    {
        int count = 0;
        foreach (RectTransform card in hand)
        {
            if (leavingCards.Contains(card))
            {
                count++;
            }
        }
        return count;
    }

    private void Update()
    {
        float followRate = 1f - Mathf.Exp(-followSpeed * Time.deltaTime);
        for (int i = 0; i < hand.Count; i++)
        {
            RectTransform card = hand[i];
            // 사라지는 중인 카드는 이펙트가 그 자리에서 재생되도록 움직이지 않는다
            if (leavingCards.Contains(card))
                continue;

            CardPose target = targets[i];

            card.anchoredPosition = Vector2.Lerp(card.anchoredPosition, target.position, followRate);
            float angle = Mathf.LerpAngle(card.localEulerAngles.z, target.angle, followRate);
            card.localEulerAngles = new Vector3(0f, 0f, angle);
            card.localScale = Vector3.one * Mathf.Lerp(card.localScale.x, target.scale, followRate);
        }
    }

    private void OnValidate()
    {
        if (layout.selectedScale > layout.SafeSelectedScale)
        {
            Debug.LogWarning($"[HandView] 선택 배율 {layout.selectedScale:0.00} → {layout.SafeSelectedScale:0.00} 로 제한됩니다 (화면 중앙 침범 방지)");
        }
    }
}
