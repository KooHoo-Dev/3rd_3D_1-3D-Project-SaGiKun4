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
/// 메뉴 화면(강화, 이후 인벤토리/옵션)을 코드만으로 조립하는 에디터 툴.
/// 빈 GameObject에 붙인 뒤 컴포넌트 우클릭 메뉴에서 실행한다.
/// 화면을 절반으로 나눠 오른쪽엔 모든 모드가 같이 쓰는 카드 목록(CardListView), 왼쪽엔 모드별 패널을 둔다.
/// 지금 왼쪽 패널은 강화(UpgradePanel: 선택 칸 2개 + 강화 + 비우기)와 인벤토리. 오른쪽 상단엔 취소 버튼.
/// 가마솥 선택 화면(CauldronPanel: 왼쪽 강화 / 오른쪽 요리)은 화면 전체를 덮는다. 이미 만든 화면에는 '5. Add Cauldron Panel'로 붙인다.
/// 조립하면서 루트에 MenuView를 붙이고 목록/패널/버튼 참조를 넣어 준다. (런타임에 이름으로 찾지 않음)
/// 카드 보상 화면(CardDropScreen: 화면 3등분, 칸마다 가운데 카드 한 장)은 따로 '3. Build Card Drop UI'로 조립한다.
/// </summary>
public class UpgradeUIBuilder : MonoBehaviour
{
#if UNITY_EDITOR
    private const string RootName = "Menu UI";
    private const string DropRootName = "Card Drop UI";
    private const int DropOptionCount = 3; // CardDropTable의 optionCount와 맞춤
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
    [Tooltip("캔버스를 그릴 UI 카메라. 비우면 MainCamera 태그 카메라")]
    [SerializeField] private Camera uiCamera;

    [Header("강화 선택 칸 하이라이트 (비우면 붙이지 않음)")]
    [SerializeField] private GameObject highlightPrefab;
    [SerializeField, Min(0f)] private float highlightScale = 150f;

    [Header("오른쪽 카드 목록")]
    [Tooltip("기준 해상도에서 한 줄에 놓을 카드 수. 카드 크기는 이 값으로 계산하고, 플레이 중 열 수는 목록 폭에 맞춰 바뀐다 (CardListGrid)")]
    [SerializeField, Range(3, 8)] private int listColumns = 5;
    [Tooltip("미리 만들어 둘 목록 카드 수. 덱 카드 수보다 많아야 함 (남는 칸은 플레이 중 꺼짐)")]
    [SerializeField, Min(1)] private int listCardCount = 30;
    [SerializeField] private Vector2 listSpacing = new Vector2(20f, 28f);

    [Header("왼쪽 선택 칸")]
    [Tooltip("목록 카드 대비 선택 칸 카드 배율 (왼쪽 절반에 안 들어가면 자동으로 줄임)")]
    [SerializeField, Range(2f, 3f)] private float slotScale = 2.5f;
    [Tooltip("인벤토리 가로 목록의 카드 사이 간격")]
    [SerializeField] private float slotGap = 60f;
    [Tooltip("선택 칸: 앞 칸 카드의 강화 태그와 다음 칸 카드 사이 간격 (태그는 카드 오른쪽 바깥에 붙으므로 칸 사이는 태그 폭 + 이 값)")]
    [SerializeField] private float tagGap = 20f;

    [Header("카드 보상 화면")]
    [Tooltip("기준 카드(200x280) 대비 보상 카드 배율 (화면 1/3 폭에 안 들어가면 자동으로 줄임)")]
    [SerializeField, Range(1f, 3f)] private float dropCardScale = 1.8f;

    // 조립한 결과 (지우기에 사용)
    [SerializeField, HideInInspector] private GameObject builtRoot;
    [SerializeField, HideInInspector] private GameObject builtDropRoot;

    // 여백
    private const float AreaPadding = 40f;
    private const float CancelButtonSize = 56f;
    private const float SortBarHeight = 56f;     // 정렬 메뉴 줄 (취소 버튼과 같은 줄)
    private const float SortBarGap = 24f;        // 정렬 메뉴와 목록 사이
    private const float ListTopPadding = AreaPadding + SortBarHeight + SortBarGap;
    private const float ScrollbarWidth = 16f;
    private const float ScrollbarGap = 16f;
    private const float SortDropdownWidth = 220f;
    private const float DropdownItemHeight = 44f; // 정렬 드롭다운을 펼쳤을 때 항목 한 줄 높이
    private static readonly string[] SortOptions = { "획득순", "이름순" }; // CardListView.SortMode 순서

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
    private static readonly Color SortBarColor = new Color(1f, 1f, 1f, 0.06f);
    private static readonly Color DropdownColor = new Color(0.25f, 0.27f, 0.26f);
    private static readonly Color DropdownItemColor = new Color(0.20f, 0.22f, 0.21f);
    private static readonly Color DropdownItemHighlightColor = new Color(0.35f, 0.38f, 0.36f);
    private static readonly Color ClearColor = new Color(0.45f, 0.45f, 0.48f);
    private static readonly Color TagColor = new Color(0.85f, 0.65f, 0.25f);
    private static readonly Color TagTextColor = new Color(0.12f, 0.10f, 0.06f);
    private static readonly Color CauldronBackgroundColor = new Color(0.04f, 0.04f, 0.04f, 0.96f);
    private static readonly Color CookColor = new Color(0.80f, 0.45f, 0.20f);
    private static readonly Vector2 CauldronOptionSize = new Vector2(560f, 560f);

    // 강화 태그 (카드 기준 크기 200x280 안의 단위. 카드 스케일을 같이 받음)
    // 태그는 카드 영역을 덮지 않도록 카드 오른쪽 가장자리 바깥에 딱 붙인다
    private const float TagWidth = 56f;
    private const float TagHeight = 24f;
    private const float TagSpacing = 4f;
    private const float TagFontSize = 14f;
    private const float TagTop = 50f;      // 카드 위에서 첫 태그까지 (이름 배너 아래)

    [ContextMenu("1. Build UI")]
    private void BuildUI()
    {
        if (builtRoot != null)
        {
            Debug.LogWarning($"[UpgradeUIBuilder] '{builtRoot.name}' 이(가) 이미 존재합니다. 먼저 '2. Clear UI'를 실행하세요.");
            return;
        }

        Undo.RecordObject(this, "Build Menu UI");
        Transform canvas = CreateCanvas(RootName);
        CreateBackground(canvas);

        Vector2 listCardSize = CalcListCardSize();
        Vector2 slotCardSize = CalcSlotCardSize(listCardSize);

        // 오른쪽 공통 목록을 먼저 만들어야 왼쪽 패널이 참조할 수 있다
        CardListView cardList = CreateListArea(canvas, listCardSize);
        UpgradePanel upgradePanel = CreateUpgradePanel(canvas, cardList, slotCardSize);
        InventoryPanel inventoryPanel = CreateInventoryPanel(canvas, cardList, slotCardSize);
        CreateDivider(canvas);
        // 가마솥 패널은 화면 전체를 덮으므로 목록, 구분선보다 뒤(위)에, 취소 버튼보다는 앞(아래)에 둔다
        CauldronPanel cauldronPanel = CreateCauldronPanel(canvas);
        Button cancel = CreateCancelButton(canvas);

        MenuView view = canvas.gameObject.AddComponent<MenuView>();
        view.EditorSetup(cardList, upgradePanel, inventoryPanel, cauldronPanel, cancel);
        EditorUtility.SetDirty(view);

        AttachSlotHighlight(canvas, upgradePanel);

        builtRoot = canvas.gameObject;
        Undo.RegisterCreatedObjectUndo(builtRoot, "Build Menu UI");
        EditorUtility.SetDirty(this);
        EditorSceneManager.MarkSceneDirty(gameObject.scene);
        Debug.Log($"[UpgradeUIBuilder] '{RootName}' 생성 완료 (목록 카드 {listCardSize.x:0}x{listCardSize.y:0}, 선택 칸 {slotCardSize.x:0}x{slotCardSize.y:0}, 맞는 파티클 크기 {slotCardSize.y / SheetSilhouetteFill:0})");
    }

    // 하이라이트 프리팹이 있을 때만 선택 칸 파티클을 붙인다
    private void AttachSlotHighlight(Transform canvas, UpgradePanel upgradePanel)
    {
        if (highlightPrefab == null)
            return;

        UpgradeSlotHighlight highlight = canvas.gameObject.AddComponent<UpgradeSlotHighlight>();
        highlight.EditorSetup(upgradePanel, highlightPrefab, highlightScale);
        EditorUtility.SetDirty(highlight);
    }

    [ContextMenu("2. Clear UI")]
    private void ClearUI()
    {
        if (builtRoot == null)
        {
            Debug.Log("[UpgradeUIBuilder] 이 UpgradeUIBuilder로 만든 UI가 없습니다. (예전에 만든 UI는 직접 지워주세요)");
            return;
        }

        Undo.RecordObject(this, "Clear Menu UI");
        string removed = builtRoot.name;
        Undo.DestroyObjectImmediate(builtRoot);
        builtRoot = null;
        EditorUtility.SetDirty(this);
        EditorSceneManager.MarkSceneDirty(gameObject.scene);
        Debug.Log($"[UpgradeUIBuilder] '{removed}' 삭제 완료");
    }

    [ContextMenu("3. Build Card Drop UI")]
    private void BuildCardDropUI()
    {
        if (builtDropRoot != null)
        {
            Debug.LogWarning($"[UpgradeUIBuilder] '{builtDropRoot.name}' 이(가) 이미 존재합니다. 먼저 '4. Clear Card Drop UI'를 실행하세요.");
            return;
        }

        Undo.RecordObject(this, "Build Card Drop UI");
        // 캔버스 루트는 항상 켜 두고(CardDropScreen이 붙음), 열고 닫을 때는 Content만 켜고 끈다
        Transform canvas = CreateCanvas(DropRootName);
        Transform content = NewUI("Content", canvas).transform;
        Stretch((RectTransform)content, 0f);
        CreateBackground(content);

        CreateDropTitle(content);

        Vector2 cardSize = CalcDropCardSize();
        List<CardDropScreen.OptionView> options = new List<CardDropScreen.OptionView>();
        for (int i = 0; i < DropOptionCount; i++)
        {
            options.Add(CreateDropOption(i, content, cardSize));
        }

        CardDropScreen screen = canvas.gameObject.AddComponent<CardDropScreen>();
        screen.EditorSetup(content.gameObject, options);
        EditorUtility.SetDirty(screen);

        builtDropRoot = canvas.gameObject;
        Undo.RegisterCreatedObjectUndo(builtDropRoot, "Build Card Drop UI");
        EditorUtility.SetDirty(this);
        EditorSceneManager.MarkSceneDirty(gameObject.scene);
        Debug.Log($"[UpgradeUIBuilder] '{DropRootName}' 생성 완료 (보상 카드 {cardSize.x:0}x{cardSize.y:0})");
    }

    [ContextMenu("4. Clear Card Drop UI")]
    private void ClearCardDropUI()
    {
        if (builtDropRoot == null)
        {
            Debug.Log("[UpgradeUIBuilder] 이 UpgradeUIBuilder로 만든 카드 보상 UI가 없습니다.");
            return;
        }

        Undo.RecordObject(this, "Clear Card Drop UI");
        string removed = builtDropRoot.name;
        Undo.DestroyObjectImmediate(builtDropRoot);
        builtDropRoot = null;
        EditorUtility.SetDirty(this);
        EditorSceneManager.MarkSceneDirty(gameObject.scene);
        Debug.Log($"[UpgradeUIBuilder] '{removed}' 삭제 완료");
    }

    // 이미 조립된 메뉴 화면에 가마솥 선택 패널만 붙인다. (화면을 다시 조립하지 않아도 되게)
    [ContextMenu("5. Add Cauldron Panel")]
    private void AddCauldronPanel()
    {
        // 이 빌더로 만든 화면이 없으면(예전에 만든 화면 등) 씬의 MenuView에 붙인다
        MenuView view = null;
        if (builtRoot != null)
        {
            view = builtRoot.GetComponent<MenuView>();
        }
        if (view == null)
        {
            view = FindAnyObjectByType<MenuView>(FindObjectsInactive.Include);
        }
        if (view == null)
        {
            Debug.LogWarning("[UpgradeUIBuilder] 씬에 메뉴 화면(MenuView)이 없습니다. 먼저 '1. Build UI'를 실행하세요.");
            return;
        }
        if (view.CauldronPanel != null)
        {
            Debug.LogWarning("[UpgradeUIBuilder] 가마솥 패널이 이미 있습니다.");
            return;
        }

        CauldronPanel cauldronPanel = CreateCauldronPanel(view.transform);
        // 취소 버튼보다 아래에 그려지도록 취소 버튼 자리로 옮긴다 (취소 버튼은 한 칸 뒤로 밀림)
        if (view.CancelButton != null)
        {
            cauldronPanel.transform.SetSiblingIndex(view.CancelButton.transform.GetSiblingIndex());
        }
        Undo.RegisterCreatedObjectUndo(cauldronPanel.gameObject, "Add Cauldron Panel");

        Undo.RecordObject(view, "Add Cauldron Panel");
        view.EditorSetCauldronPanel(cauldronPanel);
        EditorUtility.SetDirty(view);
        EditorSceneManager.MarkSceneDirty(view.gameObject.scene);
        Debug.Log("[UpgradeUIBuilder] 가마솥 패널 추가 완료");
    }

    #region Size
    // 오른쪽 절반 폭에 listColumns 장이 딱 들어가는 카드 크기 (비율은 기준 카드와 같음)
    private Vector2 CalcListCardSize()
    {
        float gridWidth = referenceResolution.x * 0.5f - AreaPadding * 2f - ScrollbarWidth - ScrollbarGap;
        float cardWidth = (gridWidth - listSpacing.x * (listColumns - 1)) / listColumns;
        return new Vector2(cardWidth, cardWidth * DesignCardSize.y / DesignCardSize.x);
    }

    // 폭은 목록 카드의 slotScale 배, 높이는 파티클 실루엣 비율에 맞춘다.
    // 왼쪽 절반(두 장 + 카드마다 오른쪽 태그 폭 + 간격)이나 화면 높이(확인 버튼 자리 제외)를 넘으면 줄인다
    private Vector2 CalcSlotCardSize(Vector2 listCardSize)
    {
        float slotHeightPerWidth = 1f / SheetSilhouetteAspect;
        // 태그 폭은 카드 폭에 비례 (카드 내부 기준 폭 DesignCardSize.x 당 TagWidth)
        float widthWithTag = 1f + TagWidth / DesignCardSize.x;
        float maxByWidth = (referenceResolution.x * 0.5f - AreaPadding * 2f - tagGap) / (listCardSize.x * SlotCount * widthWithTag);
        float maxByHeight = (referenceResolution.y - 320f) / (listCardSize.x * slotHeightPerWidth);
        float scale = Mathf.Min(slotScale, maxByWidth, maxByHeight);
        if (scale < slotScale)
            Debug.LogWarning($"[UpgradeUIBuilder] 선택 칸이 왼쪽 절반에 들어가지 않아 배율을 {slotScale:0.##} → {scale:0.##}로 줄였습니다.");

        float width = listCardSize.x * scale;
        return new Vector2(width, width * slotHeightPerWidth);
    }

    // 기준 카드의 dropCardScale 배. 1/3 폭(여백 포함)이나 화면 높이(제목, 번호 자리 제외)를 넘으면 줄인다
    private Vector2 CalcDropCardSize()
    {
        float maxByWidth = (referenceResolution.x / DropOptionCount - AreaPadding * 2f) / DesignCardSize.x;
        float maxByHeight = (referenceResolution.y - 320f) / DesignCardSize.y;
        float scale = Mathf.Min(dropCardScale, maxByWidth, maxByHeight);
        if (scale < dropCardScale)
            Debug.LogWarning($"[UpgradeUIBuilder] 보상 카드가 화면 1/3에 들어가지 않아 배율을 {dropCardScale:0.##} → {scale:0.##}로 줄였습니다.");
        return DesignCardSize * scale;
    }

    // 기준 카드 폭은 그대로 두고, 높이만 size와 같은 비율로 늘린 카드 내부 크기 (선택 칸처럼 세로로 긴 칸)
    private Vector2 CalcTallDesignSize(Vector2 size)
    {
        return new Vector2(DesignCardSize.x, DesignCardSize.x * size.y / size.x);
    }
    #endregion

    #region Canvas
    private Transform CreateCanvas(string rootName)
    {
        GameObject go = new GameObject(rootName, typeof(RectTransform));
        go.layer = LayerMask.NameToLayer("UI");

        Canvas canvas = go.AddComponent<Canvas>();
        // 선택 칸 파티클이 보이도록 카메라 모드 (Overlay에서는 파티클이 그려지지 않음)
        canvas.renderMode = RenderMode.ScreenSpaceCamera;
        canvas.worldCamera = uiCamera != null ? uiCamera : Camera.main;
        canvas.planeDistance = 1f;
        if (canvas.worldCamera == null)
            Debug.LogWarning("[UpgradeUIBuilder] UI 카메라도, MainCamera 태그가 붙은 카메라도 없습니다. Render Camera를 직접 지정하세요.");

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

    // 화면의 가로 minX ~ maxX 구간을 위아래 꽉 채우는 영역 (왼쪽 절반 = 0 ~ 0.5, 오른쪽 절반 = 0.5 ~ 1)
    private RectTransform CreateHalfArea(string name, Transform canvas, float minX, float maxX)
    {
        RectTransform area = NewUI(name, canvas).GetComponent<RectTransform>();
        area.anchorMin = new Vector2(minX, 0f);
        area.anchorMax = new Vector2(maxX, 1f);
        area.pivot = new Vector2(0.5f, 0.5f);
        area.anchoredPosition = Vector2.zero;
        area.sizeDelta = Vector2.zero;
        return area;
    }
    #endregion

    #region Left (강화 패널: 선택 칸 + 강화 버튼 + 비우기 버튼)
    private UpgradePanel CreateUpgradePanel(Transform canvas, CardListView cardList, Vector2 slotCardSize)
    {
        // 왼쪽 절반
        RectTransform area = CreateHalfArea("UpgradePanel", canvas, 0f, 0.5f);

        List<UpgradePanel.SlotView> slots = CreateSlotRow(area, slotCardSize);
        Button confirm = CreateConfirmButton(area);
        Button clear = CreateClearButton(area);

        UpgradePanel panel = area.gameObject.AddComponent<UpgradePanel>();
        panel.EditorSetup(cardList, slots, confirm, clear);
        EditorUtility.SetDirty(panel);
        return panel;
    }

    private List<UpgradePanel.SlotView> CreateSlotRow(RectTransform area, Vector2 slotCardSize)
    {
        // 칸 두 개를 가로로 나란히 (확인 버튼 자리만큼 위로 올림)
        // 칸 사이는 앞 칸 태그 폭 + tagGap. 마지막 칸의 태그까지 합쳐 가운데에 오도록 태그 폭 절반만큼 왼쪽으로 민다.
        float tagWidth = CalcSlotTagWidth(slotCardSize);
        float spacing = tagWidth + tagGap;
        RectTransform row = NewUI("SlotRow", area).GetComponent<RectTransform>();
        Place(row, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-tagWidth * 0.5f, 60f),
            new Vector2(slotCardSize.x * SlotCount + spacing * (SlotCount - 1), slotCardSize.y));
        AddCenteredHorizontalLayout(row.gameObject, spacing);

        List<UpgradePanel.SlotView> slots = new List<UpgradePanel.SlotView>();
        for (int i = 0; i < SlotCount; i++)
        {
            slots.Add(CreateSlot(i, row, slotCardSize));
        }
        return slots;
    }

    // 확인(강화 확정) 버튼: 왼쪽 절반 하단 중앙
    private Button CreateConfirmButton(RectTransform area)
    {
        Button confirm = NewButton("ConfirmButton", area, "강화", out TMP_Text confirmLabel);
        confirm.GetComponent<Image>().color = ConfirmColor;
        Place((RectTransform)confirm.transform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 80f), new Vector2(220f, 80f));
        return confirm;
    }

    // 비우기(선택 칸 전부 빼기) 버튼: 왼쪽 절반 오른쪽 아래, 확인 버튼과 같은 높이
    private Button CreateClearButton(RectTransform area)
    {
        Button clear = NewButton("ClearButton", area, "비우기", out TMP_Text clearLabel);
        clear.GetComponent<Image>().color = ClearColor;
        clearLabel.fontSize = 26f;
        Place((RectTransform)clear.transform, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-AreaPadding, 80f), new Vector2(130f, 80f));
        return clear;
    }

    private UpgradePanel.SlotView CreateSlot(int index, Transform parent, Vector2 size)
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

        // 선택된 카드 (처음엔 꺼 둠, 글자/그림은 UpgradePanel이 목록 카드에서 복사)
        CardView card = CreateCard("Card", slot.transform, size, false, false, CalcTallDesignSize(size));
        AddCardTags(card);
        card.card.SetActive(false);

        return new UpgradePanel.SlotView { button = button, emptyBox = empty, card = card };
    }

    // 선택 칸 카드 옆에 붙는 태그의 실제 폭 (카드와 같은 스케일을 받음)
    private float CalcSlotTagWidth(Vector2 slotCardSize)
    {
        return TagWidth * slotCardSize.x / DesignCardSize.x;
    }

    // 자식을 spacing 간격으로 가로로 나란히, 가운데 정렬 (자식 크기는 건드리지 않음)
    private void AddCenteredHorizontalLayout(GameObject go, float spacing)
    {
        HorizontalLayoutGroup layout = go.AddComponent<HorizontalLayoutGroup>();
        layout.spacing = spacing;
        layout.childAlignment = TextAnchor.MiddleCenter;
        layout.childControlWidth = false;
        layout.childControlHeight = false;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = false;
    }
    #endregion

    #region Left (인벤토리 패널: 고른 카드 + 강화 재료 가로 목록)
    // 카드 칸은 강화 선택 칸과 같은 크기. 강화 재료 수에 제한이 없으므로 가로 스크롤 목록에 템플릿을 복제해 넣는다
    private InventoryPanel CreateInventoryPanel(Transform canvas, CardListView cardList, Vector2 cardSize)
    {
        // 왼쪽 절반
        RectTransform area = CreateHalfArea("InventoryPanel", canvas, 0f, 0.5f);

        // 고르기 전 안내
        TMP_Text hint = NewText("EmptyHint", area, "카드를 선택하세요", 32f, TextAlignmentOptions.Center);
        hint.color = EmptyTextColor;
        Stretch(hint.rectTransform, AreaPadding);

        // 가로 목록: 카드 높이 + 스크롤바 자리, 왼쪽 절반 가운데
        float scrollHeight = cardSize.y + ScrollbarGap + ScrollbarWidth;
        ScrollRect scroll = CreateInventoryScroll(area, scrollHeight);
        TMP_Text title = CreateInventoryTitle(area, scrollHeight);
        CardViewItem template = CreateInventoryCardTemplate(area, cardSize);

        InventoryPanel panel = area.gameObject.AddComponent<InventoryPanel>();
        panel.EditorSetup(cardList, scroll, scroll.content, template, title, hint.gameObject);
        EditorUtility.SetDirty(panel);
        return panel;
    }

    private ScrollRect CreateInventoryScroll(RectTransform area, float scrollHeight)
    {
        RectTransform scrollRect = NewUI("CardScroll", area).GetComponent<RectTransform>();
        Place(scrollRect, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -30f), new Vector2(0f, scrollHeight));
        scrollRect.anchorMin = new Vector2(0f, 0.5f);
        scrollRect.anchorMax = new Vector2(1f, 0.5f);
        scrollRect.sizeDelta = new Vector2(-AreaPadding * 2f, scrollHeight);
        ScrollRect scroll = scrollRect.gameObject.AddComponent<ScrollRect>();
        scroll.horizontal = true;
        scroll.vertical = false;
        scroll.movementType = ScrollRect.MovementType.Clamped;
        scroll.scrollSensitivity = 40f;

        // 뷰포트: 스크롤바 자리를 뺀 위쪽. 카드 사이 빈 곳도 드래그되도록 투명 이미지
        RectTransform viewport = CreateScrollViewport(scrollRect);
        viewport.offsetMin = new Vector2(0f, ScrollbarWidth + ScrollbarGap);

        RectTransform content = CreateInventoryContent(viewport);

        scroll.viewport = viewport;
        scroll.content = content;
        scroll.horizontalScrollbar = CreateScrollbar(scrollRect, true);
        scroll.horizontalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHide;
        return scroll;
    }

    // 내용: 카드를 가로로 나열하고 폭은 카드 수에 맞춰 늘어남. 피벗이 가운데라 카드가 적으면 가운데에 모임
    private RectTransform CreateInventoryContent(RectTransform viewport)
    {
        RectTransform content = NewUI("Content", viewport).GetComponent<RectTransform>();
        content.anchorMin = new Vector2(0.5f, 0f);
        content.anchorMax = new Vector2(0.5f, 1f);
        content.pivot = new Vector2(0.5f, 0.5f);
        content.anchoredPosition = Vector2.zero;
        content.sizeDelta = Vector2.zero;
        AddCenteredHorizontalLayout(content.gameObject, slotGap);
        ContentSizeFitter fitter = content.gameObject.AddComponent<ContentSizeFitter>();
        fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
        fitter.verticalFit = ContentSizeFitter.FitMode.Unconstrained;
        return content;
    }

    // 고른 카드 이름: 목록 바로 위
    private TMP_Text CreateInventoryTitle(RectTransform area, float scrollHeight)
    {
        TMP_Text title = NewText("TitleText", area, "", 32f, TextAlignmentOptions.Center);
        title.color = Color.white;
        Place(title.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0f), new Vector2(0f, -30f + scrollHeight * 0.5f + 16f), new Vector2(0f, 50f));
        title.rectTransform.anchorMin = new Vector2(0f, 0.5f);
        title.rectTransform.anchorMax = new Vector2(1f, 0.5f);
        title.rectTransform.sizeDelta = new Vector2(-AreaPadding * 2f, 50f);
        return title;
    }

    // 복제할 카드 칸 (패널 아래에 꺼 둠, 레이아웃에 끼지 않게 목록 밖에 둔다)
    private CardViewItem CreateInventoryCardTemplate(RectTransform area, Vector2 cardSize)
    {
        GameObject cell = NewUI("CardTemplate", area);
        ((RectTransform)cell.transform).sizeDelta = cardSize;
        CardView card = CreateCard("Card", cell.transform, cardSize, false, false, CalcTallDesignSize(cardSize));
        CardViewItem template = cell.AddComponent<CardViewItem>();
        template.EditorSetup(card);
        cell.SetActive(false);
        return template;
    }
    #endregion

    #region Right (공통 카드 목록)
    private CardListView CreateListArea(Transform canvas, Vector2 cardSize)
    {
        // 오른쪽 절반
        RectTransform area = CreateHalfArea("CardListArea", canvas, 0.5f, 1f);

        TMP_Dropdown sort = CreateSortBar(area);
        ScrollRect scroll = CreateListScroll(area, cardSize);
        List<CardListView.ItemView> items = CreateListItems(scroll.content, cardSize);

        CardListView list = area.gameObject.AddComponent<CardListView>();
        list.EditorSetup(items, sort);
        EditorUtility.SetDirty(list);
        return list;
    }

    private ScrollRect CreateListScroll(RectTransform area, Vector2 cardSize)
    {
        // 스크롤 영역 (위쪽은 정렬 메뉴 자리만큼 비움)
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
        RectTransform viewport = CreateScrollViewport(scrollRect);
        viewport.offsetMax = new Vector2(-(ScrollbarWidth + ScrollbarGap), 0f);

        RectTransform content = CreateListContent(viewport, cardSize);

        scroll.viewport = viewport;
        scroll.content = content;
        scroll.verticalScrollbar = CreateScrollbar(scrollRect);
        scroll.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.Permanent;
        return scroll;
    }

    // 내용: 위에서부터 카드를 격자로 채우고, 높이는 카드 수에 맞춰 늘어남
    private RectTransform CreateListContent(RectTransform viewport, Vector2 cardSize)
    {
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
        // 화면 폭이 바뀌면 카드 크기는 그대로 두고 열 수만 맞춘다
        content.gameObject.AddComponent<CardListGrid>();
        return content;
    }

    private List<CardListView.ItemView> CreateListItems(RectTransform content, Vector2 cardSize)
    {
        List<CardListView.ItemView> items = new List<CardListView.ItemView>();
        for (int i = 0; i < listCardCount; i++)
        {
            // 목록 칸 = 버튼. 누를 때 카드 테두리가 어두워짐
            GameObject cell = NewPanel($"ListCard_{i:00}", content, cardSize);
            Image hit = cell.GetComponent<Image>();
            hit.color = Color.clear;
            hit.raycastTarget = true;
            Button button = cell.AddComponent<Button>();

            bool isAttack = Random.value < 0.5f;
            bool upgraded = Random.value < 0.25f;
            CardView card = CreateCard("Card", cell.transform, cardSize, isAttack, upgraded, DesignCardSize);
            button.targetGraphic = card.card.GetComponent<Image>();

            items.Add(new CardListView.ItemView { button = button, card = card });
        }
        return items;
    }

    // 스크롤 영역을 꽉 채우는 뷰포트 (빈 곳도 드래그되도록 투명 이미지 + 넘치는 부분 자르기). 스크롤바 자리는 부르는 쪽에서 뺀다
    private RectTransform CreateScrollViewport(RectTransform scrollRect)
    {
        GameObject viewportGo = NewPanel("Viewport", scrollRect, Vector2.zero);
        Image viewportImage = viewportGo.GetComponent<Image>();
        viewportImage.color = Color.clear;
        viewportImage.raycastTarget = true;
        viewportGo.AddComponent<RectMask2D>();
        RectTransform viewport = viewportGo.GetComponent<RectTransform>();
        Stretch(viewport, 0f);
        return viewport;
    }

    // 세로: 부모 오른쪽 끝, 가로: 부모 아래쪽 끝
    private Scrollbar CreateScrollbar(Transform parent, bool horizontal = false)
    {
        GameObject track = NewPanel("Scrollbar", parent, Vector2.zero);
        Image trackImage = track.GetComponent<Image>();
        trackImage.color = ScrollTrackColor;
        trackImage.raycastTarget = true;
        PlaceScrollbarTrack(track.GetComponent<RectTransform>(), horizontal);

        RectTransform slidingArea = NewUI("SlidingArea", track.transform).GetComponent<RectTransform>();
        Stretch(slidingArea, 0f);

        GameObject handle = NewPanel("Handle", slidingArea, Vector2.zero);
        Image handleImage = handle.GetComponent<Image>();
        handleImage.color = ScrollHandleColor;
        handleImage.raycastTarget = true;
        Stretch(handle.GetComponent<RectTransform>(), 0f);

        Scrollbar scrollbar = track.AddComponent<Scrollbar>();
        scrollbar.direction = horizontal ? Scrollbar.Direction.LeftToRight : Scrollbar.Direction.BottomToTop;
        scrollbar.handleRect = handle.GetComponent<RectTransform>();
        scrollbar.targetGraphic = handleImage;
        return scrollbar;
    }

    private void PlaceScrollbarTrack(RectTransform rect, bool horizontal)
    {
        if (horizontal)
        {
            rect.anchorMin = new Vector2(0f, 0f);
            rect.anchorMax = new Vector2(1f, 0f);
            rect.pivot = new Vector2(0.5f, 0f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = new Vector2(0f, ScrollbarWidth);
        }
        else
        {
            rect.anchorMin = new Vector2(1f, 0f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(1f, 0.5f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = new Vector2(ScrollbarWidth, 0f);
        }
    }

    // 목록 위쪽 정렬 메뉴 줄: 왼쪽은 목록 폭에 맞추고, 오른쪽은 취소 버튼 앞에서 끝난다. 정렬 드롭다운은 줄 오른쪽 끝
    private TMP_Dropdown CreateSortBar(Transform area)
    {
        GameObject bar = NewPanel("SortBar", area, Vector2.zero);
        bar.GetComponent<Image>().color = SortBarColor;
        RectTransform rect = bar.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(1f, 1f);
        rect.pivot = new Vector2(0.5f, 1f);
        rect.offsetMin = new Vector2(AreaPadding, -AreaPadding - SortBarHeight);
        rect.offsetMax = new Vector2(-(AreaPadding + CancelButtonSize + ScrollbarGap), -AreaPadding);

        TMP_Text label = NewText("SortLabel", bar.transform, "정렬", 24f, TextAlignmentOptions.MidlineRight);
        label.color = EmptyTextColor;
        Place(label.rectTransform, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-(SortDropdownWidth + 16f), 0f), new Vector2(80f, SortBarHeight));

        return CreateDropdown(bar.transform);
    }

    // TMP_Dropdown 조립: 펼쳤을 때 보이는 Template(꺼 둠) 안에 항목 하나(Item)를 두면 옵션 수만큼 복제된다
    private TMP_Dropdown CreateDropdown(Transform parent)
    {
        float height = SortBarHeight - 12f;

        GameObject root = NewPanel("SortDropdown", parent, Vector2.zero);
        Image rootImage = root.GetComponent<Image>();
        rootImage.color = DropdownColor;
        rootImage.raycastTarget = true;
        Place(root.GetComponent<RectTransform>(), new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-6f, 0f), new Vector2(SortDropdownWidth, height));

        TMP_Text caption = NewText("Label", root.transform, SortOptions[0], 24f, TextAlignmentOptions.MidlineLeft);
        Stretch(caption.rectTransform, 0f);
        caption.rectTransform.offsetMin = new Vector2(16f, 0f);
        caption.rectTransform.offsetMax = new Vector2(-40f, 0f);

        TMP_Text arrow = NewText("Arrow", root.transform, "▼", 18f, TextAlignmentOptions.Center);
        Place(arrow.rectTransform, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-8f, 0f), new Vector2(28f, height));

        RectTransform templateRect = CreateDropdownTemplate(root.transform, out TMP_Text itemLabel);

        TMP_Dropdown dropdown = root.AddComponent<TMP_Dropdown>();
        dropdown.targetGraphic = rootImage;
        dropdown.template = templateRect;
        dropdown.captionText = caption;
        dropdown.itemText = itemLabel;
        dropdown.ClearOptions();
        dropdown.AddOptions(new List<string>(SortOptions));
        dropdown.SetValueWithoutNotify(0);
        return dropdown;
    }

    // 펼친 목록: 드롭다운 바로 아래, 옵션 수만큼의 높이
    private RectTransform CreateDropdownTemplate(Transform root, out TMP_Text itemLabel)
    {
        GameObject template = NewPanel("Template", root, Vector2.zero);
        template.GetComponent<Image>().color = DropdownItemColor;
        RectTransform templateRect = template.GetComponent<RectTransform>();
        templateRect.anchorMin = new Vector2(0f, 0f);
        templateRect.anchorMax = new Vector2(1f, 0f);
        templateRect.pivot = new Vector2(0.5f, 1f);
        templateRect.anchoredPosition = new Vector2(0f, -2f);
        templateRect.sizeDelta = new Vector2(0f, DropdownItemHeight * SortOptions.Length);

        RectTransform content = NewUI("Content", template.transform).GetComponent<RectTransform>();
        content.anchorMin = new Vector2(0f, 1f);
        content.anchorMax = new Vector2(1f, 1f);
        content.pivot = new Vector2(0.5f, 1f);
        content.anchoredPosition = Vector2.zero;
        content.sizeDelta = new Vector2(0f, DropdownItemHeight);

        itemLabel = CreateDropdownItem(content);

        template.SetActive(false);
        return templateRect;
    }

    // 옵션 수만큼 복제될 항목 하나 (배경 + 글자 + Toggle)
    private TMP_Text CreateDropdownItem(RectTransform content)
    {
        GameObject item = NewUI("Item", content);
        RectTransform itemRect = item.GetComponent<RectTransform>();
        itemRect.anchorMin = new Vector2(0f, 0.5f);
        itemRect.anchorMax = new Vector2(1f, 0.5f);
        itemRect.pivot = new Vector2(0.5f, 0.5f);
        itemRect.sizeDelta = new Vector2(0f, DropdownItemHeight);

        GameObject itemBg = NewPanel("Item Background", item.transform, Vector2.zero);
        Image itemBgImage = itemBg.GetComponent<Image>();
        itemBgImage.color = Color.white;
        itemBgImage.raycastTarget = true;
        Stretch(itemBg.GetComponent<RectTransform>(), 0f);

        TMP_Text itemLabel = NewText("Item Label", item.transform, SortOptions[0], 24f, TextAlignmentOptions.MidlineLeft);
        Stretch(itemLabel.rectTransform, 0f);
        itemLabel.rectTransform.offsetMin = new Vector2(16f, 0f);

        Toggle toggle = item.AddComponent<Toggle>();
        toggle.targetGraphic = itemBgImage;
        toggle.isOn = true;
        ColorBlock colors = toggle.colors;
        colors.normalColor = DropdownItemColor;
        colors.highlightedColor = DropdownItemHighlightColor;
        colors.pressedColor = DropdownItemHighlightColor;
        colors.selectedColor = DropdownItemColor;
        toggle.colors = colors;

        return itemLabel;
    }
    #endregion

    #region Card Drop (화면 3등분, 칸마다 가운데 카드 + 아래 번호)
    private void CreateDropTitle(Transform content)
    {
        TMP_Text title = NewText("TitleText", content, "카드를 고르세요  (클릭 또는 숫자키, F/Esc 닫기)", 40f, TextAlignmentOptions.Center);
        Place(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -AreaPadding), new Vector2(referenceResolution.x - AreaPadding * 2f, 60f));
    }

    private CardDropScreen.OptionView CreateDropOption(int index, Transform parent, Vector2 cardSize)
    {
        // 화면 가로 1/3 칸 (선택지가 모자라면 칸째로 꺼짐)
        RectTransform column = NewUI($"Option_{index:00}", parent).GetComponent<RectTransform>();
        column.anchorMin = new Vector2((float)index / DropOptionCount, 0f);
        column.anchorMax = new Vector2((float)(index + 1) / DropOptionCount, 1f);
        column.pivot = new Vector2(0.5f, 0.5f);
        column.anchoredPosition = Vector2.zero;
        column.sizeDelta = Vector2.zero;

        CardView card = CreateCard("Card", column, cardSize, false, false, DesignCardSize);
        AddCardTags(card);
        Button button = AddDropCardButton(card);

        // 카드 아래 숫자키 번호
        TMP_Text number = NewText("NumberText", column, $"{index + 1}", 44f, TextAlignmentOptions.Center);
        Place(number.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 1f), new Vector2(0f, -cardSize.y * 0.5f - 24f), new Vector2(120f, 60f));

        return new CardDropScreen.OptionView { root = column.gameObject, button = button, card = card };
    }

    // 카드 = 버튼 (테두리 이미지가 클릭을 받고, 평소엔 살짝 어둡다가 마우스를 올리면 원래 색)
    private Button AddDropCardButton(CardView card)
    {
        Image frame = card.card.GetComponent<Image>();
        frame.raycastTarget = true;
        Button button = card.card.AddComponent<Button>();
        button.targetGraphic = frame;
        ColorBlock colors = button.colors;
        colors.normalColor = new Color(0.7f, 0.7f, 0.7f);
        colors.highlightedColor = Color.white;
        colors.selectedColor = new Color(0.7f, 0.7f, 0.7f);
        colors.pressedColor = new Color(0.5f, 0.5f, 0.5f);
        button.colors = colors;
        return button;
    }
    #endregion

    #region Cauldron (가마솥 선택 화면: 화면 2등분, 왼쪽 강화 / 오른쪽 요리)
    private CauldronPanel CreateCauldronPanel(Transform canvas)
    {
        RectTransform area = NewUI("CauldronPanel", canvas).GetComponent<RectTransform>();
        Stretch(area, 0f);

        // 뒤쪽 카드 목록을 가리고 클릭도 막는 배경
        GameObject bg = NewPanel("Background", area, Vector2.zero);
        Image bgImage = bg.GetComponent<Image>();
        bgImage.color = CauldronBackgroundColor;
        bgImage.raycastTarget = true;
        Stretch(bg.GetComponent<RectTransform>(), 0f);

        TMP_Text title = NewText("TitleText", area, "가마솥", 48f, TextAlignmentOptions.Center);
        Place(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -AreaPadding * 2f), new Vector2(referenceResolution.x - AreaPadding * 2f, 70f));

        TMP_Text upgradeDesc;
        Button upgrade = CreateCauldronOption("UpgradeOption", area, 0f, 0.5f, "카드를 강화한다", "강화창을 엽니다", ConfirmColor, out upgradeDesc);
        TMP_Text cookDesc;
        Button cook = CreateCauldronOption("CookOption", area, 0.5f, 1f, "요리를 준비한다", "최대 체력의 30% 회복\n200 골드", CookColor, out cookDesc);

        CauldronPanel panel = area.gameObject.AddComponent<CauldronPanel>();
        panel.EditorSetup(upgrade, cook, cookDesc);
        EditorUtility.SetDirty(panel);
        return panel;
    }

    // 화면 가로 minX ~ maxX 칸 가운데에 큰 버튼 하나 (위쪽 제목 + 아래쪽 설명)
    private Button CreateCauldronOption(string name, RectTransform area, float minX, float maxX, string titleText, string descText, Color color, out TMP_Text desc)
    {
        RectTransform column = CreateHalfArea(name, area, minX, maxX);

        Button button = NewButton("Button", column, titleText, out TMP_Text label);
        button.GetComponent<Image>().color = color;
        ColorBlock colors = button.colors;
        colors.disabledColor = new Color(0.35f, 0.35f, 0.35f, 0.6f);
        button.colors = colors;
        Place((RectTransform)button.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -30f), CauldronOptionSize);

        // 제목은 버튼 위쪽 절반, 설명은 아래쪽 절반
        label.fontSize = 44f;
        label.rectTransform.anchorMin = new Vector2(0f, 0.5f);
        label.rectTransform.offsetMin = new Vector2(AreaPadding, 0f);
        label.rectTransform.offsetMax = new Vector2(-AreaPadding, -AreaPadding);

        desc = NewText("DescText", button.transform, descText, 30f, TextAlignmentOptions.Top);
        desc.color = EmptyTextColor;
        Stretch(desc.rectTransform, 0f);
        desc.rectTransform.anchorMax = new Vector2(1f, 0.5f);
        desc.rectTransform.offsetMin = new Vector2(AreaPadding, AreaPadding);
        desc.rectTransform.offsetMax = new Vector2(-AreaPadding, -16f);
        return button;
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
        Place(go.GetComponent<RectTransform>(), new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-AreaPadding, -AreaPadding), new Vector2(CancelButtonSize, CancelButtonSize));

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
    private CardView CreateCard(string name, Transform parent, Vector2 size, bool isAttack, bool upgraded, Vector2 designSize)
    {
        GameObject card = NewPanel(name, parent, designSize);
        card.GetComponent<Image>().color = FrameColor;
        Place(card.GetComponent<RectTransform>(), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, designSize);
        card.transform.localScale = Vector3.one * (size.x / designSize.x);

        GameObject body = NewPanel("CardBody", card.transform, Vector2.zero);
        body.GetComponent<Image>().color = BodyColor;
        Stretch(body.GetComponent<RectTransform>(), 6f);

        float width = designSize.x;
        string title = GetCardTitle(isAttack, upgraded);
        string desc = GetCardDescription(isAttack, upgraded);

        // 카드 이름 배너
        GameObject banner = NewPanel("TitleBanner", card.transform, Vector2.zero);
        banner.GetComponent<Image>().color = BannerColor;
        Place(banner.GetComponent<RectTransform>(), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -12f), new Vector2(width - 24f, 34f));
        TMP_Text titleText = NewText("TitleText", banner.transform, title, 22f, TextAlignmentOptions.Center);
        if (upgraded)
        {
            titleText.color = UpgradedTextColor;
        }
        Stretch(titleText.rectTransform, 0f);

        // 카드 일러스트 자리 (스프라이트 대체 색상 블록)
        GameObject art = NewPanel("ArtImage", card.transform, Vector2.zero);
        Image artImage = art.GetComponent<Image>();
        artImage.color = isAttack ? AttackArtColor : SkillArtColor;
        Place(art.GetComponent<RectTransform>(), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -50f), new Vector2(width - 24f, 120f));

        // 카드 타입 라벨
        GameObject typeLabel = NewPanel("TypeLabel", card.transform, Vector2.zero);
        typeLabel.GetComponent<Image>().color = TypeLabelColor;
        Place(typeLabel.GetComponent<RectTransform>(), new Vector2(0.5f, 1f), new Vector2(0.5f, 0.5f), new Vector2(0f, -172f), new Vector2(64f, 22f));
        TMP_Text typeText = NewText("TypeText", typeLabel.transform, isAttack ? "공격" : "스킬", 14f, TextAlignmentOptions.Center);
        Stretch(typeText.rectTransform, 0f);

        // 설명
        TMP_Text descText = NewText("DescText", card.transform, desc, 18f, TextAlignmentOptions.Top);
        Place(descText.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -190f), new Vector2(width - 30f, 70f));

        return new CardView
        {
            card = card,
            artImage = artImage,
            titleText = titleText,
            typeText = typeText,
            descText = descText,
        };
    }

    /// <summary>
    /// 강화 태그 목록 (카드 오른쪽 가장자리 바깥에 딱 붙고, 위에서 아래로 쌓임). 강화 칸과 보상 카드에만 붙인다.
    /// 오른쪽 목록 카드와 인벤토리 카드는 태그 없이 이름에 +만 붙인다.
    /// </summary>
    private void AddCardTags(CardView card)
    {
        RectTransform list = NewUI("TagList", card.card.transform).GetComponent<RectTransform>();
        list.anchorMin = new Vector2(1f, 1f);
        list.anchorMax = new Vector2(1f, 1f);
        list.pivot = new Vector2(0f, 1f);
        list.anchoredPosition = new Vector2(0f, -TagTop);
        list.sizeDelta = new Vector2(TagWidth, 0f);

        VerticalLayoutGroup layout = list.gameObject.AddComponent<VerticalLayoutGroup>();
        layout.spacing = TagSpacing;
        layout.childAlignment = TextAnchor.UpperLeft;
        layout.childControlWidth = false;
        layout.childControlHeight = false;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = false;

        CardTag template = CreateTagTemplate(list);
        CardTagList tagList = list.gameObject.AddComponent<CardTagList>();
        tagList.EditorSetup(template);
        EditorUtility.SetDirty(tagList);

        // 강화된 카드를 그릴 때만 CardTagList가 켠다
        list.gameObject.SetActive(false);
        card.tags = tagList;
    }

    // 복제해서 쓸 태그 한 개 (꺼 둠). 머리 태그("강화")와 이름 태그가 같은 모양을 쓴다.
    private CardTag CreateTagTemplate(RectTransform list)
    {
        GameObject go = NewPanel("Tag", list, new Vector2(TagWidth, TagHeight));
        go.GetComponent<Image>().color = TagColor;

        TMP_Text label = NewText("Label", go.transform, "강화", TagFontSize, TextAlignmentOptions.Center);
        label.color = TagTextColor;
        // 이름이 길면 글자를 줄여서 태그 안에 넣는다
        label.enableAutoSizing = true;
        label.fontSizeMin = TagFontSize * 0.5f;
        label.fontSizeMax = TagFontSize;
        Stretch(label.rectTransform, 2f);

        CardTag tag = go.AddComponent<CardTag>();
        tag.EditorSetup(label);
        EditorUtility.SetDirty(tag);

        go.SetActive(false);
        return tag;
    }

    // 시안용 카드 이름 (강화되면 뒤에 +)
    private string GetCardTitle(bool isAttack, bool upgraded)
    {
        string title = isAttack ? "타격" : "수비";
        if (upgraded)
        {
            title += "+";
        }
        return title;
    }

    // 시안용 카드 설명
    private string GetCardDescription(bool isAttack, bool upgraded)
    {
        if (isAttack)
        {
            int damage = upgraded ? 9 : 6;
            return $"피해를 {damage} 줍니다.";
        }

        int block = upgraded ? 8 : 5;
        return $"방어도를 {block} 얻습니다.";
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
        if (font != null)
        {
            tmp.font = font;
        }
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
        rect.anchorMin = anchor;
        rect.anchorMax = anchor;
        rect.pivot = pivot;
        rect.anchoredPosition = pos;
        rect.sizeDelta = size;
    }
    #endregion
#endif
}
