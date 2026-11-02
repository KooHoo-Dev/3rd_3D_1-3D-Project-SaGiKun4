using UnityEngine;
#if UNITY_EDITOR
using System.Collections.Generic;
using Study3D.FpsGame.UI;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.UI;
#endif

/// <summary>
/// 화면 오른쪽 위의 "킬 그래프" UI를 코드만으로 조립하는 에디터 툴.
/// 빈 GameObject에 붙인 뒤 컴포넌트 우클릭 메뉴에서 실행한다.
/// 조립하면서 KillGraphView와 줄(KillGraphRow)들을 붙이고 참조를 넣어 준다. (런타임에 이름으로 찾지 않음)
/// → 플레이하면 KillGraphView가 AttackTracker의 이벤트를 받아 바로 동작한다.
/// </summary>
public class KillGraphBuilder : MonoBehaviour
{
#if UNITY_EDITOR
    private const string RootName = "KillGraphCanvas";
    private const int RowCount = 5;

    [Header("폰트 (비우면 TMP 기본 폰트)")]
    [SerializeField] private TMP_FontAsset font;

    [Header("배치")]
    [SerializeField] private Vector2 referenceResolution = new Vector2(1920f, 1080f);
    [SerializeField] private Vector2 screenMargin = new Vector2(30f, 30f); // 화면 오른쪽 위 모서리에서 떨어진 거리
    [SerializeField] private float killTextWidth = 90f; // 막대 왼쪽에 붙는 숫자 칸 폭
    [SerializeField] private float killTextSize = 26f;

    // 조립한 결과 (지우기에 사용)
    [SerializeField, HideInInspector] private GameObject builtRoot;

    [ContextMenu("1. Build Kill Graph")]
    private void BuildKillGraph()
    {
        if (builtRoot != null)
        {
            Debug.LogWarning($"[KillGraphBuilder] '{builtRoot.name}' 이(가) 이미 존재합니다. 먼저 '2. Clear Kill Graph'를 실행하세요.");
            return;
        }

        Undo.RecordObject(this, "Build Kill Graph");
        Transform canvas = CreateCanvas();
        CreateGraph(canvas);
        builtRoot = canvas.gameObject;

        Undo.RegisterCreatedObjectUndo(canvas.gameObject, "Build Kill Graph");
        EditorUtility.SetDirty(this);
        EditorSceneManager.MarkSceneDirty(gameObject.scene);
        Debug.Log($"[KillGraphBuilder] '{RootName}' 생성 완료 (KillGraphView, 줄 {RowCount}개 연결)");
    }

    [ContextMenu("2. Clear Kill Graph")]
    private void ClearKillGraph()
    {
        if (builtRoot == null)
        {
            Debug.Log("[KillGraphBuilder] 이 KillGraphBuilder로 만든 UI가 없습니다.");
            return;
        }

        Undo.RecordObject(this, "Clear Kill Graph");
        Undo.DestroyObjectImmediate(builtRoot);
        builtRoot = null;
        EditorUtility.SetDirty(this);
        EditorSceneManager.MarkSceneDirty(gameObject.scene);
        Debug.Log($"[KillGraphBuilder] '{RootName}' 삭제 완료");
    }

    #region Canvas
    // 손패 UI(UIBuilder)와 같은 방식의 캔버스. 그래프만 따로 두어서 막대 길이가 바뀔 때 다른 UI를 다시 그리지 않게 한다.
    private Transform CreateCanvas()
    {
        GameObject go = new GameObject(RootName, typeof(RectTransform));
        go.layer = LayerMask.NameToLayer("UI");

        Canvas canvas = go.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceCamera;
        canvas.worldCamera = Camera.main;
        canvas.planeDistance = 1f;
        if (canvas.worldCamera == null)
            Debug.LogWarning("[KillGraphBuilder] MainCamera 태그가 붙은 카메라가 없습니다. Render Camera를 직접 지정하세요.");

        CanvasScaler scaler = go.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = referenceResolution;
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;

        // 클릭을 받지 않는 UI라서 GraphicRaycaster는 붙이지 않는다.
        return go.transform;
    }
    #endregion

    #region Graph
    private void CreateGraph(Transform canvas)
    {
        // 그래프 영역: 화면 오른쪽 위. 줄들은 이 영역의 오른쪽 위 모서리 기준으로 아래로 쌓인다.
        RectTransform graph = NewUI("KillGraph", canvas).GetComponent<RectTransform>();
        KillGraphView view = graph.gameObject.AddComponent<KillGraphView>();
        KillGraphSettings settings = view.Settings;

        float width = settings.maxBarWidth + killTextWidth;
        float height = RowCount * (settings.rowHeight + settings.rowSpacing);
        Place(graph, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-screenMargin.x, -screenMargin.y), new Vector2(width, height));

        List<KillGraphRow> rows = new List<KillGraphRow>();
        for (int i = 0; i < RowCount; i++)
            rows.Add(CreateRow(i, graph, width, settings));

        view.EditorSetup(rows);
        EditorUtility.SetDirty(view);
    }

    // 줄 하나 조립: 줄(위치) → 떨림 → 막대(오른쪽 끝 고정) → 숫자(막대 왼쪽 끝)
    private KillGraphRow CreateRow(int index, Transform graph, float width, KillGraphSettings settings)
    {
        RectTransform row = NewUI($"Row_{index:00}", graph).GetComponent<RectTransform>();
        Place(row, new Vector2(1f, 1f), new Vector2(1f, 1f), Vector2.zero, new Vector2(width, settings.rowHeight));
        CanvasGroup group = row.gameObject.AddComponent<CanvasGroup>();
        group.interactable = false;
        group.blocksRaycasts = false;

        RectTransform shake = NewUI("Shake", row).GetComponent<RectTransform>();
        Stretch(shake, 0f);

        // 막대: 앵커를 오른쪽 세로 한 줄에 두고 피벗도 오른쪽이라, 폭을 늘리면 왼쪽으로만 자란다.
        GameObject barObject = NewUI("Bar", shake);
        Image barImage = barObject.AddComponent<Image>();
        barImage.raycastTarget = false;
        barImage.color = settings.baseColor;
        RectTransform bar = barObject.GetComponent<RectTransform>();
        bar.anchorMin = new Vector2(1f, 0f);
        bar.anchorMax = new Vector2(1f, 1f);
        bar.pivot = new Vector2(1f, 0.5f);
        bar.anchoredPosition = Vector2.zero;
        bar.sizeDelta = new Vector2(settings.minBarWidth, 0f);

        // 숫자: 막대의 자식으로 막대 왼쪽 끝 바깥에 붙인다. 막대가 자라면 같이 왼쪽으로 밀려난다.
        TMP_Text killText = NewText("KillCount", bar, "0", killTextSize, TextAlignmentOptions.MidlineRight);
        Place(killText.rectTransform, new Vector2(0f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-8f, 0f), new Vector2(killTextWidth, settings.rowHeight));
        killText.fontStyle = FontStyles.Bold;

        KillGraphRow graphRow = row.gameObject.AddComponent<KillGraphRow>();
        graphRow.EditorSetup(row, shake, bar, barImage, killText, group);
        EditorUtility.SetDirty(graphRow);

        // 쓰기 전까지는 꺼 둔다. (KillGraphView가 켜고 끈다)
        row.gameObject.SetActive(false);
        return graphRow;
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
        tmp.textWrappingMode = TextWrappingModes.NoWrap;
        return tmp;
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
