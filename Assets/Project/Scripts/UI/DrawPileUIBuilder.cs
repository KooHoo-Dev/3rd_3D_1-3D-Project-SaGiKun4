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
    [SerializeField] private float padding = 12f;      // 목록 배경 안쪽 여백
    [SerializeField] private float titleSize = 26f;
    [SerializeField] private Vector2 cellSize = new Vector2(200f, 36f);
    [SerializeField] private Vector2 cellSpacing = new Vector2(6f, 4f); // (열 사이, 칸 사이)
    [SerializeField] private float cellTextSize = 20f;
    [SerializeField, Min(1)] private int rowsPerColumn = 15; // 한 열의 최대 칸 수. 넘으면 오른쪽 옆 열로 이어짐

    [Header("색상")]
    [SerializeField] private Color panelColor = new Color(0f, 0f, 0f, 0.6f);         // 목록 배경
    [SerializeField] private Color cellColor = new Color(0.25f, 0.30f, 0.38f, 0.9f); // 칸 하나

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
        CreatePanel(canvas);
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

    #region Panel
    // 목록 배경(화면 왼쪽 끝 가운데에 딱 붙음) → 제목(남은 장수) → 칸 목록(칸 템플릿)
    // 배경은 피벗이 왼쪽 가운데라, 칸이 늘어나면 위아래로 똑같이 커지고 열이 늘어나면 오른쪽으로 넓어진다.
    private void CreatePanel(Transform canvas)
    {
        RectTransform panel = NewUI("DrawPilePanel", canvas).GetComponent<RectTransform>();
        Place(panel, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), Vector2.zero, Vector2.zero);
        Image background = panel.gameObject.AddComponent<Image>();
        background.color = panelColor;
        background.raycastTarget = false;
        AddPanelLayout(panel.gameObject);

        TMP_Text title = NewText("Title", panel, "남은 덱 0장", titleSize);
        title.fontStyle = FontStyles.Bold;

        GridLayoutGroup grid = CreateGrid(panel);
        DrawPileRow rowTemplate = CreateCellTemplate(grid.transform);

        DrawPileView view = canvas.gameObject.AddComponent<DrawPileView>();
        view.EditorSetup(panel.gameObject, title, grid, rowTemplate, rowsPerColumn);
        EditorUtility.SetDirty(view);

        // 플레이 전에는 꺼 둔다. (DrawPileView가 Tab 입력에 맞춰 켜고 끈다)
        panel.gameObject.SetActive(false);
    }

    // 제목과 칸 목록을 위에서부터 쌓고, 배경 크기를 내용에 맞추는 레이아웃
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

        ContentSizeFitter fitter = target.AddComponent<ContentSizeFitter>();
        fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
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
