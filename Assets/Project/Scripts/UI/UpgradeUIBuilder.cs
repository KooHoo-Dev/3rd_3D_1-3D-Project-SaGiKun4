using UnityEngine;
#if UNITY_EDITOR
using System.Collections.Generic;
using Study3D.FpsGame.UI;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.EventSystems;
using UnityEngine.UI;
#endif

/// <summary>
/// 카드 강화 화면을 코드만으로 조립하는 에디터 툴.
/// 빈 GameObject에 붙인 뒤 컴포넌트 우클릭 메뉴에서 실행한다.
/// 화면을 절반으로 나눠 왼쪽엔 선택 칸 2개 + 확인 버튼, 오른쪽엔 카드 목록(스크롤), 오른쪽 상단엔 취소 버튼을 둔다.
/// 조립하면서 루트에 UpgradeView를 붙이고 칸/목록/버튼 참조를 넣어 준다. (런타임에 이름으로 찾지 않음)
/// </summary>
public class UpgradeUIBuilder : MonoBehaviour
{
#if UNITY_EDITOR
    private const string RootName = "Upgrade UI";
    private const int SlotCount = 2;
    // 카드 내부 배치를 잡은 기준 크기 (UIBuilder의 카드와 같음). 실제 크기는 이 크기에서 스케일로 맞춘다
    private static readonly Vector2 DesignCardSize = new Vector2(200f, 280f);
    // 선택 칸 파티클(Card_Sheet_01B, 4x4 시트) 한 칸 안의 사각형 실루엣 비율 (512x512 칸 중 241x377, 가운데 정렬)
    // 선택 칸 카드는 이 비율로 세로를 늘린다. 파티클 크기 = 칸 높이 / SheetSilhouetteFill
    private const float SheetSilhouetteAspect = 241f / 377f;
    private const float SheetSilhouetteFill = 377f / 512f;

    [Header("폰트 (한글 폰트 교체용, 비우면 TMP 기본 폰트)")]
    [SerializeField] private TMP_FontAsset font;

    [SerializeField] private Vector2 referenceResolution = new Vector2(1920f, 1080f);

    [Header("오른쪽 카드 목록")]
    [Tooltip("한 줄에 놓을 카드 수. 카드 크기는 오른쪽 절반 폭에 맞춰 계산한다")]
    [SerializeField, Range(3, 8)] private int listColumns = 5;
    [Tooltip("미리 만들어 둘 목록 카드 수. 덱 카드 수보다 많아야 함 (남는 칸은 플레이 중 꺼짐)")]
    [SerializeField, Min(1)] private int listCardCount = 30;
    [SerializeField] private Vector2 listSpacing = new Vector2(20f, 28f);

    [Header("왼쪽 선택 칸")]
    [Tooltip("목록 카드 대비 선택 칸 카드 배율 (왼쪽 절반에 안 들어가면 자동으로 줄임)")]
    [SerializeField, Range(2f, 3f)] private float slotScale = 2.5f;
    [SerializeField] private float slotGap = 60f;

    // 조립한 결과 (지우기에 사용)
    [SerializeField, HideInInspector] private GameObject builtRoot;

    // 여백
    private const float AreaPadding = 40f;
    private const float ListTopPadding = 110f;   // 취소 버튼 자리
    private const float ScrollbarWidth = 16f;
    private const float ScrollbarGap = 16f;

    // 시안 색상
    private static readonly Color DimColor = new Color(0f, 0f, 0f, 0.75f);
    private static readonly Color DividerColor = new Color(1f, 1f, 1f, 0.15f);
    private static readonly Color EmptyBoxColor = new Color(0.45f, 0.45f, 0.45f, 0.6f);
    private static readonly Color EmptyTextColor = new Color(0.85f, 0.85f, 0.85f);
    private static readonly Color ConfirmColor = new Color(0.20f, 0.55f, 0.85f);
    private static readonly Color CancelColor = new Color(0.55f, 0.55f, 0.55f);
    private static readonly Color ScrollTrackColor = new Color(1f, 1f, 1f, 0.1f);
    private static readonly Color ScrollHandleColor = new Color(0.85f, 0.65f, 0.25f);
    private static readonly Color FrameColor = new Color(0.30f, 0.55f, 0.35f);
    private static readonly Color BodyColor = new Color(0.16f, 0.20f, 0.18f);
    private static readonly Color BannerColor = new Color(0.62f, 0.64f, 0.62f);
    private static readonly Color AttackArtColor = new Color(0.72f, 0.16f, 0.14f);
    private static readonly Color SkillArtColor = new Color(0.20f, 0.42f, 0.30f);
    private static readonly Color TypeLabelColor = new Color(0.55f, 0.55f, 0.52f);
    private static readonly Color UpgradedTextColor = new Color(0.45f, 0.95f, 0.35f);

    [ContextMenu("1. Build UI")]
    private void BuildUI()
    {
        if (builtRoot != null)
        {
            Debug.LogWarning($"[UpgradeUIBuilder] '{builtRoot.name}' 이(가) 이미 존재합니다. 먼저 '2. Clear UI'를 실행하세요.");
            return;
        }

        Undo.RecordObject(this, "Build Upgrade UI");
        Transform canvas = CreateCanvas();
        CreateBackground(canvas);

        Vector2 listCardSize = CalcListCardSize();
        Vector2 slotCardSize = CalcSlotCardSize(listCardSize);

        List<UpgradeView.SlotView> slots = CreateSlotArea(canvas, slotCardSize, out Button confirm);
        CreateDivider(canvas);
        List<UpgradeView.ListItemView> items = CreateListArea(canvas, listCardSize);
        Button cancel = CreateCancelButton(canvas);

        UpgradeView view = canvas.gameObject.AddComponent<UpgradeView>();
        view.EditorSetup(slots, items, confirm, cancel);
        EditorUtility.SetDirty(view);

        builtRoot = canvas.gameObject;
        Undo.RegisterCreatedObjectUndo(builtRoot, "Build Upgrade UI");
        EditorUtility.SetDirty(this);
        EditorSceneManager.MarkSceneDirty(gameObject.scene);
        Debug.Log($"[UpgradeUIBuilder] '{RootName}' 생성 완료 (목록 카드 {listCardSize.x:0}x{listCardSize.y:0}, 선택 칸 {slotCardSize.x:0}x{slotCardSize.y:0}, 맞는 파티클 크기 {slotCardSize.y / SheetSilhouetteFill:0})");
    }

    [ContextMenu("2. Clear UI")]
    private void ClearUI()
    {
        if (builtRoot == null)
        {
            Debug.Log("[UpgradeUIBuilder] 이 UpgradeUIBuilder로 만든 UI가 없습니다. (예전에 만든 UI는 직접 지워주세요)");
            return;
        }

        Undo.RecordObject(this, "Clear Upgrade UI");
        string removed = builtRoot.name;
        Undo.DestroyObjectImmediate(builtRoot);
        builtRoot = null;
        EditorUtility.SetDirty(this);
        EditorSceneManager.MarkSceneDirty(gameObject.scene);
        Debug.Log($"[UpgradeUIBuilder] '{removed}' 삭제 완료");
    }

    #region Size
    // 오른쪽 절반 폭에 listColumns 장이 딱 들어가는 카드 크기 (비율은 기준 카드와 같음)
    private Vector2 CalcListCardSize()
    {
        float gridWidth = referenceResolution.x * 0.5f - AreaPadding * 2f - ScrollbarWidth - ScrollbarGap;
        float w = (gridWidth - listSpacing.x * (listColumns - 1)) / listColumns;
        return new Vector2(w, w * DesignCardSize.y / DesignCardSize.x);
    }

    // 폭은 목록 카드의 slotScale 배, 높이는 파티클 실루엣 비율에 맞춘다.
    // 왼쪽 절반(두 장 + 간격)이나 화면 높이(확인 버튼 자리 제외)를 넘으면 줄인다
    private Vector2 CalcSlotCardSize(Vector2 listCardSize)
    {
        float slotHeightPerWidth = 1f / SheetSilhouetteAspect;
        float maxByWidth = (referenceResolution.x * 0.5f - AreaPadding * 2f - slotGap) / (listCardSize.x * SlotCount);
        float maxByHeight = (referenceResolution.y - 320f) / (listCardSize.x * slotHeightPerWidth);
        float scale = Mathf.Min(slotScale, maxByWidth, maxByHeight);
        if (scale < slotScale)
            Debug.LogWarning($"[UpgradeUIBuilder] 선택 칸이 왼쪽 절반에 들어가지 않아 배율을 {slotScale:0.##} → {scale:0.##}로 줄였습니다.");

        float width = listCardSize.x * scale;
        return new Vector2(width, width * slotHeightPerWidth);
    }
    #endregion

    #region Canvas
    private Transform CreateCanvas()
    {
        GameObject go = new GameObject(RootName, typeof(RectTransform));
        go.layer = LayerMask.NameToLayer("UI");

        Canvas canvas = go.AddComponent<Canvas>();
        // 선택 칸 파티클이 보이도록 카메라 모드 (Overlay에서는 파티클이 그려지지 않음)
        canvas.renderMode = RenderMode.ScreenSpaceCamera;
        canvas.worldCamera = Camera.main;
        canvas.planeDistance = 1f;
        if (canvas.worldCamera == null)
            Debug.LogWarning("[UpgradeUIBuilder] MainCamera 태그가 붙은 카메라가 없습니다. Render Camera를 직접 지정하세요.");

        CanvasScaler scaler = go.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = referenceResolution;
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;

        go.AddComponent<GraphicRaycaster>();

        if (FindAnyObjectByType<EventSystem>() == null)
            Debug.LogWarning("[UpgradeUIBuilder] 씬에 EventSystem이 없습니다. 버튼 입력을 받으려면 추가하세요.");
        return go.transform;
    }

    // 화면 전체를 덮는 반투명 검정 배경 (뒤쪽 입력도 막음)
    private void CreateBackground(Transform canvas)
    {
        GameObject bg = NewPanel("DimBackground", canvas, Vector2.zero);
        Image image = bg.GetComponent<Image>();
        image.color = DimColor;
        image.raycastTarget = true;
        Stretch(bg.GetComponent<RectTransform>(), 0f);
    }

    // 화면 가운데 세로 구분선
    private void CreateDivider(Transform canvas)
    {
        GameObject divider = NewPanel("Divider", canvas, Vector2.zero);
        divider.GetComponent<Image>().color = DividerColor;
        RectTransform rect = divider.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0f);
        rect.anchorMax = new Vector2(0.5f, 1f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta = new Vector2(3f, -AreaPadding * 2f);
    }
    #endregion

    #region Left (선택 칸 + 확인 버튼)
    private List<UpgradeView.SlotView> CreateSlotArea(Transform canvas, Vector2 slotCardSize, out Button confirm)
    {
        // 왼쪽 절반
        RectTransform area = NewUI("SlotArea", canvas).GetComponent<RectTransform>();
        area.anchorMin = new Vector2(0f, 0f);
        area.anchorMax = new Vector2(0.5f, 1f);
        area.pivot = new Vector2(0.5f, 0.5f);
        area.anchoredPosition = Vector2.zero;
        area.sizeDelta = Vector2.zero;

        // 칸 두 개를 가로로 나란히 (확인 버튼 자리만큼 위로 올림)
        RectTransform row = NewUI("SlotRow", area).GetComponent<RectTransform>();
        Place(row, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 60f),
            new Vector2(slotCardSize.x * SlotCount + slotGap * (SlotCount - 1), slotCardSize.y));
        HorizontalLayoutGroup layout = row.gameObject.AddComponent<HorizontalLayoutGroup>();
        layout.spacing = slotGap;
        layout.childAlignment = TextAnchor.MiddleCenter;
        layout.childControlWidth = layout.childControlHeight = false;
        layout.childForceExpandWidth = layout.childForceExpandHeight = false;

        var slots = new List<UpgradeView.SlotView>();
        for (int i = 0; i < SlotCount; i++)
            slots.Add(CreateSlot(i, row, slotCardSize));

        // 확인(강화 확정) 버튼: 왼쪽 절반 하단 중앙
        confirm = NewButton("ConfirmButton", area, "강화", out TMP_Text _);
        confirm.GetComponent<Image>().color = ConfirmColor;
        Place((RectTransform)confirm.transform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 80f), new Vector2(220f, 80f));

        return slots;
    }

    private UpgradeView.SlotView CreateSlot(int index, Transform parent, Vector2 size)
    {
        // 칸 = 투명 버튼 (누르면 이 칸 선택)
        GameObject slot = NewPanel($"Slot_{index:00}", parent, size);
        Image hit = slot.GetComponent<Image>();
        hit.color = Color.clear;
        hit.raycastTarget = true;
        Button button = slot.AddComponent<Button>();
        button.targetGraphic = hit;
        button.transition = Selectable.Transition.None;

        // 빈 칸 표시 (회색 박스)
        GameObject empty = NewPanel("EmptyBox", slot.transform, Vector2.zero);
        empty.GetComponent<Image>().color = EmptyBoxColor;
        Stretch(empty.GetComponent<RectTransform>(), 0f);
        TMP_Text emptyText = NewText("EmptyText", empty.transform, $"{index + 1}\n카드 선택", 32f, TextAlignmentOptions.Center);
        emptyText.color = EmptyTextColor;
        Stretch(emptyText.rectTransform, 0f);

        // 선택된 카드 (처음엔 꺼 둠, 글자/그림은 UpgradeView가 목록 카드에서 복사)
        UpgradeView.CardView card = CreateCard("Card", slot.transform, size, false, false, new Vector2(DesignCardSize.x, DesignCardSize.x * size.y / size.x));
        card.card.SetActive(false);

        return new UpgradeView.SlotView { button = button, emptyBox = empty, card = card };
    }
    #endregion

    #region Right (카드 목록)
    private List<UpgradeView.ListItemView> CreateListArea(Transform canvas, Vector2 cardSize)
    {
        // 오른쪽 절반
        RectTransform area = NewUI("CardListArea", canvas).GetComponent<RectTransform>();
        area.anchorMin = new Vector2(0.5f, 0f);
        area.anchorMax = new Vector2(1f, 1f);
        area.pivot = new Vector2(0.5f, 0.5f);
        area.anchoredPosition = Vector2.zero;
        area.sizeDelta = Vector2.zero;

        // 스크롤 영역 (위쪽은 취소 버튼 자리만큼 비움)
        RectTransform scrollRect = NewUI("CardListScroll", area).GetComponent<RectTransform>();
        Stretch(scrollRect, 0f);
        scrollRect.offsetMin = new Vector2(AreaPadding, AreaPadding);
        scrollRect.offsetMax = new Vector2(-AreaPadding, -ListTopPadding);
        ScrollRect scroll = scrollRect.gameObject.AddComponent<ScrollRect>();
        scroll.horizontal = false;
        scroll.vertical = true;
        scroll.movementType = ScrollRect.MovementType.Clamped;
        scroll.scrollSensitivity = 40f;

        // 뷰포트: 스크롤바 자리를 뺀 나머지. 카드 사이 빈 곳도 드래그되도록 투명 이미지
        GameObject viewportGo = NewPanel("Viewport", scrollRect, Vector2.zero);
        Image viewportImage = viewportGo.GetComponent<Image>();
        viewportImage.color = Color.clear;
        viewportImage.raycastTarget = true;
        viewportGo.AddComponent<RectMask2D>();
        RectTransform viewport = viewportGo.GetComponent<RectTransform>();
        Stretch(viewport, 0f);
        viewport.offsetMax = new Vector2(-(ScrollbarWidth + ScrollbarGap), 0f);

        // 내용: 위에서부터 카드를 격자로 채우고, 높이는 카드 수에 맞춰 늘어남
        RectTransform content = NewUI("Content", viewport).GetComponent<RectTransform>();
        content.anchorMin = new Vector2(0f, 1f);
        content.anchorMax = new Vector2(1f, 1f);
        content.pivot = new Vector2(0.5f, 1f);
        content.anchoredPosition = Vector2.zero;
        content.sizeDelta = Vector2.zero;
        GridLayoutGroup grid = content.gameObject.AddComponent<GridLayoutGroup>();
        grid.cellSize = cardSize;
        grid.spacing = listSpacing;
        grid.startCorner = GridLayoutGroup.Corner.UpperLeft;
        grid.startAxis = GridLayoutGroup.Axis.Horizontal;
        grid.childAlignment = TextAnchor.UpperCenter;
        grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        grid.constraintCount = listColumns;
        ContentSizeFitter fitter = content.gameObject.AddComponent<ContentSizeFitter>();
        fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        scroll.viewport = viewport;
        scroll.content = content;
        scroll.verticalScrollbar = CreateScrollbar(scrollRect);
        scroll.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.Permanent;

        var items = new List<UpgradeView.ListItemView>();
        for (int i = 0; i < listCardCount; i++)
        {
            // 목록 칸 = 버튼. 누를 때 카드 테두리가 어두워짐
            GameObject cell = NewPanel($"ListCard_{i:00}", content, cardSize);
            Image hit = cell.GetComponent<Image>();
            hit.color = Color.clear;
            hit.raycastTarget = true;
            Button button = cell.AddComponent<Button>();

            UpgradeView.CardView card = CreateCard("Card", cell.transform, cardSize, Random.value < 0.5f, Random.value < 0.25f, DesignCardSize);
            button.targetGraphic = card.card.GetComponent<Image>();

            items.Add(new UpgradeView.ListItemView { button = button, card = card });
        }
        return items;
    }

    private Scrollbar CreateScrollbar(Transform parent)
    {
        GameObject track = NewPanel("Scrollbar", parent, Vector2.zero);
        Image trackImage = track.GetComponent<Image>();
        trackImage.color = ScrollTrackColor;
        trackImage.raycastTarget = true;
        RectTransform rect = track.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(1f, 0f);
        rect.anchorMax = new Vector2(1f, 1f);
        rect.pivot = new Vector2(1f, 0.5f);
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta = new Vector2(ScrollbarWidth, 0f);

        RectTransform slidingArea = NewUI("SlidingArea", track.transform).GetComponent<RectTransform>();
        Stretch(slidingArea, 0f);

        GameObject handle = NewPanel("Handle", slidingArea, Vector2.zero);
        Image handleImage = handle.GetComponent<Image>();
        handleImage.color = ScrollHandleColor;
        handleImage.raycastTarget = true;
        Stretch(handle.GetComponent<RectTransform>(), 0f);

        Scrollbar scrollbar = track.AddComponent<Scrollbar>();
        scrollbar.direction = Scrollbar.Direction.BottomToTop;
        scrollbar.handleRect = handle.GetComponent<RectTransform>();
        scrollbar.targetGraphic = handleImage;
        return scrollbar;
    }
    #endregion

    #region Cancel (오른쪽 상단)
    // 이미지는 나중에 넣을 자리라 글자 없이 색상 박스만 둔다
    private Button CreateCancelButton(Transform canvas)
    {
        GameObject go = NewPanel("CancelButton_Image", canvas, Vector2.zero);
        Image image = go.GetComponent<Image>();
        image.color = CancelColor;
        image.raycastTarget = true;
        Place(go.GetComponent<RectTransform>(), new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-AreaPadding, -AreaPadding), new Vector2(56f, 56f));

        Button button = go.AddComponent<Button>();
        button.targetGraphic = image;
        return button;
    }
    #endregion

    #region Card
    /// <summary>
    /// 카드 한 장 조립 (UIBuilder의 카드와 같은 구성). 내부는 designSize로 만들고, size에 맞게 스케일한다. 피벗은 가운데.
    /// 내부 요소는 위쪽 기준이라 designSize의 높이만 늘리면 아래쪽 여백이 늘어난다. (선택 칸)
    /// </summary>
    private UpgradeView.CardView CreateCard(string name, Transform parent, Vector2 size, bool isAttack, bool upgraded, Vector2 designSize)
    {
        GameObject card = NewPanel(name, parent, designSize);
        card.GetComponent<Image>().color = FrameColor;
        Place(card.GetComponent<RectTransform>(), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, designSize);
        card.transform.localScale = Vector3.one * (size.x / designSize.x);

        GameObject body = NewPanel("CardBody", card.transform, Vector2.zero);
        body.GetComponent<Image>().color = BodyColor;
        Stretch(body.GetComponent<RectTransform>(), 6f);

        float w = designSize.x;
        string title = (isAttack ? "타격" : "수비") + (upgraded ? "+" : "");
        string desc = isAttack ? $"피해를 {(upgraded ? 9 : 6)} 줍니다." : $"방어도를 {(upgraded ? 8 : 5)} 얻습니다.";

        // 카드 이름 배너
        GameObject banner = NewPanel("TitleBanner", card.transform, Vector2.zero);
        banner.GetComponent<Image>().color = BannerColor;
        Place(banner.GetComponent<RectTransform>(), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -12f), new Vector2(w - 24f, 34f));
        TMP_Text titleText = NewText("TitleText", banner.transform, title, 22f, TextAlignmentOptions.Center);
        if (upgraded) titleText.color = UpgradedTextColor;
        Stretch(titleText.rectTransform, 0f);

        // 카드 일러스트 자리 (스프라이트 대체 색상 블록)
        GameObject art = NewPanel("ArtImage", card.transform, Vector2.zero);
        Image artImage = art.GetComponent<Image>();
        artImage.color = isAttack ? AttackArtColor : SkillArtColor;
        Place(art.GetComponent<RectTransform>(), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -50f), new Vector2(w - 24f, 120f));

        // 카드 타입 라벨
        GameObject typeLabel = NewPanel("TypeLabel", card.transform, Vector2.zero);
        typeLabel.GetComponent<Image>().color = TypeLabelColor;
        Place(typeLabel.GetComponent<RectTransform>(), new Vector2(0.5f, 1f), new Vector2(0.5f, 0.5f), new Vector2(0f, -172f), new Vector2(64f, 22f));
        TMP_Text typeText = NewText("TypeText", typeLabel.transform, isAttack ? "공격" : "스킬", 14f, TextAlignmentOptions.Center);
        Stretch(typeText.rectTransform, 0f);

        // 설명
        TMP_Text descText = NewText("DescText", card.transform, desc, 18f, TextAlignmentOptions.Top);
        Place(descText.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -190f), new Vector2(w - 30f, 70f));

        return new UpgradeView.CardView
        {
            card = card,
            artImage = artImage,
            titleText = titleText,
            typeText = typeText,
            descText = descText,
        };
    }
    #endregion

    #region Helpers
    private GameObject NewUI(string name, Transform parent)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.layer = LayerMask.NameToLayer("UI");
        go.transform.SetParent(parent, false);
        return go;
    }

    private GameObject NewPanel(string name, Transform parent, Vector2 size)
    {
        GameObject go = NewUI(name, parent);
        go.AddComponent<Image>().raycastTarget = false;
        go.GetComponent<RectTransform>().sizeDelta = size;
        return go;
    }

    private TMP_Text NewText(string name, Transform parent, string text, float size, TextAlignmentOptions align)
    {
        GameObject go = NewUI(name, parent);
        TextMeshProUGUI tmp = go.AddComponent<TextMeshProUGUI>();
        if (font != null) tmp.font = font;
        tmp.text = text;
        tmp.fontSize = size;
        tmp.alignment = align;
        tmp.color = Color.white;
        tmp.raycastTarget = false;
        return tmp;
    }

    private Button NewButton(string name, Transform parent, string label, out TMP_Text labelText)
    {
        GameObject go = NewPanel(name, parent, new Vector2(160f, 50f));
        Image image = go.GetComponent<Image>();
        image.raycastTarget = true;
        Button button = go.AddComponent<Button>();
        button.targetGraphic = image;

        labelText = NewText("Label", go.transform, label, 32f, TextAlignmentOptions.Center);
        Stretch(labelText.rectTransform, 0f);
        return button;
    }

    private void Stretch(RectTransform rect, float padding)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.offsetMin = new Vector2(padding, padding);
        rect.offsetMax = new Vector2(-padding, -padding);
    }

    private void Place(RectTransform rect, Vector2 anchor, Vector2 pivot, Vector2 pos, Vector2 size)
    {
        rect.anchorMin = rect.anchorMax = anchor;
        rect.pivot = pivot;
        rect.anchoredPosition = pos;
        rect.sizeDelta = size;
    }
    #endregion
#endif
}
