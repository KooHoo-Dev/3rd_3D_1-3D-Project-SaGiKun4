using UnityEngine;
#if UNITY_EDITOR
using Study3D.FpsGame.UI;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.UI;
#endif

/// <summary>
/// 화면 왼쪽 가운데의 "남은 덱 목록" UI를 코드만으로 조립하는 에디터 툴.
/// 빈 GameObject에 붙인 뒤 컴포넌트 우클릭 메뉴에서 실행한다.
/// 조립하면서 캔버스 루트에 DrawPileView를 붙이고 패널/제목/목록/줄 템플릿 참조를 넣어 준다. (런타임에 이름으로 찾지 않음)
/// → 플레이하면 Tab을 누르고 있는 동안 LocalPlayer 덱의 뽑을 카드가 보인다.
/// </summary>
public class DrawPileUIBuilder : MonoBehaviour
{
#if UNITY_EDITOR
    private const string RootName = "DrawPileCanvas";

    [Header("폰트 (비우면 TMP 기본 폰트)")]
    [SerializeField] private TMP_FontAsset font;

    [Header("배치")]
    [SerializeField] private Vector2 referenceResolution = new Vector2(1920f, 1080f);
    [Tooltip("화면 세로 가운데에서 탭 위쪽 끝(= 목록 위쪽 끝)까지의 높이. 탭은 항상 이 높이에 있고 목록은 여기서 아래로 늘어남")]
    [SerializeField] private float topOffset = 320f;
    [SerializeField] private float padding = 12f;      // 목록 배경 안쪽 여백
    [SerializeField] private float titleSize = 26f;
    [SerializeField] private Vector2 cellSize = new Vector2(200f, 36f);
    [SerializeField] private Vector2 cellSpacing = new Vector2(6f, 4f); // (열 사이, 칸 사이)
    [SerializeField] private float cellTextSize = 20f;
    [SerializeField, Min(1)] private int rowsPerColumn = 15; // 한 열의 최대 칸 수. 넘으면 오른쪽 옆 열로 이어짐

    [Header("색상")]
    [SerializeField] private Color panelColor = new Color(0f, 0f, 0f, 0.6f);         // 목록 배경
    [SerializeField] private Color cellColor = new Color(0.25f, 0.30f, 0.38f, 0.9f); // 칸 하나

    [Header("인덱스 탭 (평소에 화면 왼쪽 끝, Tab을 누르면 목록 오른쪽 끝에 붙음)")]
    [Tooltip("탭 이미지. 비우면 Tab Color 단색 네모")]
    [SerializeField] private Sprite indexSprite;
    [SerializeField] private Vector2 tabSize = new Vector2(72f, 88f);
    [SerializeField] private Color tabColor = new Color(0.25f, 0.30f, 0.38f, 0.9f);
    [SerializeField] private float countTextSize = 36f;  // 이미지 위의 남은 장수
    [SerializeField] private float tabLabelSize = 14f;   // 아래쪽 작은 "Tab" 글자

    // 조립한 결과 (지우기에 사용)
    [SerializeField, HideInInspector] private GameObject builtRoot;

    [ContextMenu("1. Build Draw Pile UI")]
    private void BuildDrawPileUI()
    {
        if (builtRoot != null)
        {
            Debug.LogWarning($"[DrawPileUIBuilder] '{builtRoot.name}' 이(가) 이미 존재합니다. 먼저 '2. Clear Draw Pile UI'를 실행하세요.");
            return;
        }

        Undo.RecordObject(this, "Build Draw Pile UI");
        Transform canvas = CreateCanvas();
        CreateDrawPile(canvas);
        builtRoot = canvas.gameObject;

        Undo.RegisterCreatedObjectUndo(canvas.gameObject, "Build Draw Pile UI");
        EditorUtility.SetDirty(this);
        EditorSceneManager.MarkSceneDirty(gameObject.scene);
        Debug.Log($"[DrawPileUIBuilder] '{RootName}' 생성 완료 (DrawPileView 연결)");
    }

    [ContextMenu("2. Clear Draw Pile UI")]
    private void ClearDrawPileUI()
    {
        if (builtRoot == null)
        {
            Debug.Log("[DrawPileUIBuilder] 이 DrawPileUIBuilder로 만든 UI가 없습니다.");
            return;
        }

        Undo.RecordObject(this, "Clear Draw Pile UI");
        Undo.DestroyObjectImmediate(builtRoot);
        builtRoot = null;
        EditorUtility.SetDirty(this);
        EditorSceneManager.MarkSceneDirty(gameObject.scene);
        Debug.Log($"[DrawPileUIBuilder] '{RootName}' 삭제 완료");
    }

    #region Canvas
    // 손패 UI(UIBuilder)와 같은 방식의 캔버스. 목록만 따로 두어서 목록이 바뀔 때 다른 UI를 다시 그리지 않게 한다.
    private Transform CreateCanvas()
    {
        GameObject go = new GameObject(RootName, typeof(RectTransform));
        go.layer = LayerMask.NameToLayer("UI");

        Canvas canvas = go.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceCamera;
        canvas.worldCamera = Camera.main;
        canvas.planeDistance = 1f;
        if (canvas.worldCamera == null)
            Debug.LogWarning("[DrawPileUIBuilder] MainCamera 태그가 붙은 카메라가 없습니다. Render Camera를 직접 지정하세요.");

        CanvasScaler scaler = go.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = referenceResolution;
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;

        // 클릭을 받지 않는 UI라서 GraphicRaycaster는 붙이지 않는다.
        return go.transform;
    }
    #endregion

    #region Root
    // 루트(화면 왼쪽 끝에 딱 붙음, 가로 레이아웃) → [목록 패널, 인덱스 탭]
    // 목록이 꺼져 있으면 레이아웃에서 빠지므로 탭이 화면 왼쪽 끝으로 오고, 켜지면 탭이 목록 오른쪽 위 모서리로 밀려난다.
    // 루트는 피벗이 왼쪽 위라 위쪽 끝 높이(topOffset)가 고정된다. 그래서 탭은 열고 닫아도 같은 높이에 있고,
    // 목록은 탭 높이에서 아래로 늘어나서 탭을 잡고 옆에서 끌어낸 것처럼 보인다. (열이 늘어나면 오른쪽으로 넓어짐)
    private void CreateDrawPile(Transform canvas)
    {
        RectTransform root = NewUI("DrawPileRoot", canvas).GetComponent<RectTransform>();
        Place(root, new Vector2(0f, 0.5f), new Vector2(0f, 1f), new Vector2(0f, topOffset), Vector2.zero);
        AddRootLayout(root.gameObject);

        RectTransform panel = CreatePanel(root, out TMP_Text title, out GridLayoutGroup grid, out DrawPileRow rowTemplate);
        RectTransform beat = CreateIndexTab(root, out TMP_Text countText);

        DrawPileView view = canvas.gameObject.AddComponent<DrawPileView>();
        view.EditorSetup(root, panel, title, grid, rowTemplate, rowsPerColumn, beat, countText);
        EditorUtility.SetDirty(view);

        // 플레이 전에는 꺼 둔다. (DrawPileView가 Tab 입력에 맞춰 켜고 끈다)
        panel.gameObject.SetActive(false);
    }

    // 목록과 탭을 왼쪽부터 가로로 붙이고(위쪽 끝 맞춤), 루트 크기를 내용에 맞추는 레이아웃
    private void AddRootLayout(GameObject target)
    {
        HorizontalLayoutGroup layout = target.AddComponent<HorizontalLayoutGroup>();
        layout.spacing = 0f;
        layout.childAlignment = TextAnchor.UpperLeft;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = false;

        ContentSizeFitter fitter = target.AddComponent<ContentSizeFitter>();
        fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
    }
    #endregion

    #region Panel
    // 목록 배경 → 제목(남은 장수) → 칸 목록(칸 템플릿)
    // 크기는 루트의 가로 레이아웃이 내용(선호 크기)에 맞춰 준다. (ContentSizeFitter를 겹쳐 붙이지 않음)
    private RectTransform CreatePanel(Transform root, out TMP_Text title, out GridLayoutGroup grid, out DrawPileRow rowTemplate)
    {
        RectTransform panel = NewUI("DrawPilePanel", root).GetComponent<RectTransform>();
        Image background = panel.gameObject.AddComponent<Image>();
        background.color = panelColor;
        background.raycastTarget = false;
        AddPanelLayout(panel.gameObject);

        title = NewText("Title", panel, "남은 덱 0장", titleSize);
        title.fontStyle = FontStyles.Bold;

        grid = CreateGrid(panel);
        rowTemplate = CreateCellTemplate(grid.transform);
        return panel;
    }

    // 제목과 칸 목록을 위에서부터 쌓는 레이아웃
    private void AddPanelLayout(GameObject target)
    {
        int layoutPadding = Mathf.RoundToInt(padding);
        VerticalLayoutGroup layout = target.AddComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(layoutPadding, layoutPadding, layoutPadding, layoutPadding);
        layout.spacing = cellSpacing.y * 2f;
        layout.childAlignment = TextAnchor.UpperLeft;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = false;
    }

    // 칸 목록: 위에서부터 세로로 채우고, 행 수(constraintCount)를 넘으면 오른쪽 옆 열로 넘어간다.
    // 행 수는 DrawPileView가 장수에 맞춰 바꾼다. (장수가 적으면 그만큼만 높아짐)
    private GridLayoutGroup CreateGrid(Transform panel)
    {
        GameObject go = NewUI("Cells", panel);
        GridLayoutGroup grid = go.AddComponent<GridLayoutGroup>();
        grid.cellSize = cellSize;
        grid.spacing = cellSpacing;
        grid.startCorner = GridLayoutGroup.Corner.UpperLeft;
        grid.startAxis = GridLayoutGroup.Axis.Vertical;
        grid.childAlignment = TextAnchor.UpperLeft;
        grid.constraint = GridLayoutGroup.Constraint.FixedRowCount;
        grid.constraintCount = rowsPerColumn;
        return grid;
    }

    // 칸 템플릿 = 칸 배경 + 카드 이름. 꺼 두면 레이아웃에서 빠진다. (DrawPileView가 복제해서 씀)
    private DrawPileRow CreateCellTemplate(Transform grid)
    {
        GameObject cell = NewUI("CellTemplate", grid);
        Image cellImage = cell.AddComponent<Image>();
        cellImage.color = cellColor;
        cellImage.raycastTarget = false;

        TMP_Text nameText = NewText("Name", cell.transform, "카드 이름", cellTextSize);
        Stretch(nameText.rectTransform, 8f);
        nameText.overflowMode = TextOverflowModes.Ellipsis;

        DrawPileRow row = cell.AddComponent<DrawPileRow>();
        row.EditorSetup(nameText);
        cell.SetActive(false);
        return row;
    }
    #endregion

    #region Index Tab
    // 인덱스 탭(크기는 레이아웃이 tabSize로 맞춤) → 비트(크기 연출용) → 이미지 + 남은 장수 + "Tab"
    // 레이아웃은 탭 위치/크기만 정하고 scale은 건드리지 않으므로, 비트는 안쪽 자식의 scale만 바꾼다.
    // 비트의 피벗은 왼쪽 가운데라, 뛸 때 붙어 있는 쪽(왼쪽)은 그대로 두고 오른쪽으로 커진다.
    private RectTransform CreateIndexTab(Transform root, out TMP_Text countText)
    {
        GameObject tab = NewUI("IndexTab", root);
        LayoutElement element = tab.AddComponent<LayoutElement>();
        element.minWidth = tabSize.x;
        element.preferredWidth = tabSize.x;
        element.minHeight = tabSize.y;
        element.preferredHeight = tabSize.y;

        RectTransform beat = NewUI("Beat", tab.transform).GetComponent<RectTransform>();
        Stretch(beat, 0f);
        beat.pivot = new Vector2(0f, 0.5f);

        GameObject imageObject = NewUI("Image", beat);
        Image image = imageObject.AddComponent<Image>();
        image.sprite = indexSprite;
        image.color = TabImageColor();
        image.raycastTarget = false;
        Stretch(image.rectTransform, 0f);

        countText = NewText("Count", beat, "0", countTextSize);
        Stretch(countText.rectTransform, 0f);
        countText.alignment = TextAlignmentOptions.Center;
        countText.fontStyle = FontStyles.Bold;

        TMP_Text label = NewText("TabLabel", beat, "Tab", tabLabelSize);
        Place(label.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 4f), new Vector2(tabSize.x, tabLabelSize + 4f));
        label.alignment = TextAlignmentOptions.Bottom;
        label.color = new Color(1f, 1f, 1f, 0.7f);

        return beat;
    }

    // 이미지를 넣었으면 원래 색 그대로(흰색), 없으면 단색 네모
    private Color TabImageColor()
    {
        if (indexSprite != null)
            return Color.white;

        return tabColor;
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

    private TMP_Text NewText(string name, Transform parent, string text, float size)
    {
        GameObject go = NewUI(name, parent);
        TextMeshProUGUI tmp = go.AddComponent<TextMeshProUGUI>();
        if (font != null)
        {
            tmp.font = font;
        }
        tmp.text = text;
        tmp.fontSize = size;
        tmp.alignment = TextAlignmentOptions.MidlineLeft;
        tmp.color = Color.white;
        tmp.raycastTarget = false;
        tmp.textWrappingMode = TextWrappingModes.NoWrap;
        return tmp;
    }

    private void Stretch(RectTransform rect, float horizontalPadding)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.offsetMin = new Vector2(horizontalPadding, 0f);
        rect.offsetMax = new Vector2(-horizontalPadding, 0f);
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
