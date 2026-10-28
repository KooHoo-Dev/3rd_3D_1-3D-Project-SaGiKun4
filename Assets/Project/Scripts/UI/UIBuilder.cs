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
/// 화면 중앙 하단의 "손패(카드 나열)" UI를 코드만으로 조립하는 에디터 툴.
/// 빈 GameObject에 붙인 뒤 컴포넌트 우클릭 메뉴에서 실행한다.
/// 조립하면서 HandArea에 HandView와 HandUIBinder를 붙이고 슬롯/카드/파티클/글자 참조를 넣어 준다. (런타임에 이름으로 찾지 않음)
/// → 플레이하면 HandUIBinder가 LocalPlayer의 손패와 바로 연동된다.
/// </summary>
public class UIBuilder : MonoBehaviour
{
#if UNITY_EDITOR
    private const string RootName = "HandCanvas";
    private const int MaxCards = 6;

    [Header("폰트 (한글 폰트 교체용, 비우면 TMP 기본 폰트)")]
    [SerializeField] private TMP_FontAsset font;

    [Header("처음 켜 둘 카드 수 (0이면 1~6 랜덤, 슬롯은 항상 6개 생성)")]
    [SerializeField, Range(0, MaxCards)] private int cardCount = 0;

    [Header("카드 사라짐 파티클 (슬롯마다 카드 UI와 형제로 하나씩 넣음, 비우면 생략)")]
    [SerializeField] private GameObject vanishEffectPrefab;

    [Header("카드 등장 연출 (카드 UI에 CardAppearEffect를 붙임, 카드 머티리얼을 비우면 생략)")]
    [Tooltip("M_CardDissolve (Project/UI/CardDissolve, Edge Only = 0)")]
    [SerializeField] private Material appearCardMaterial;
    [Tooltip("M_CardDissolveEdge (Project/UI/CardDissolve, Edge Only = 1)")]
    [SerializeField] private Material appearEdgeMaterial;
    [Tooltip("다 드러난 순간 재생할 플래시 (Card_Appear). 슬롯마다 카드 UI와 형제로 하나씩 넣음")]
    [SerializeField] private GameObject appearFlashPrefab;

    [Tooltip("조립 시 배치 값. 조립할 때 HandView에도 같은 값을 복사해 넣는다")]
    [SerializeField] private HandLayoutSettings layout = new HandLayoutSettings();

    // 조립한 결과 (지우기, 등장 연출 추가에 사용)
    [SerializeField, HideInInspector] private GameObject builtRoot;
    [SerializeField, HideInInspector] private HandUIBinder builtBinder;

    // 시안 색상
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
            Debug.LogWarning($"[UIBuilder] '{builtRoot.name}' 이(가) 이미 존재합니다. 먼저 '2. Clear UI'를 실행하세요.");
            return;
        }

        Undo.RecordObject(this, "Build UI");
        Transform canvas = CreateCanvas();
        CreateHand(canvas);
        builtRoot = canvas.gameObject;

        Undo.RegisterCreatedObjectUndo(canvas.gameObject, "Build UI");
        EditorUtility.SetDirty(this);
        EditorSceneManager.MarkSceneDirty(gameObject.scene);
        Debug.Log($"[UIBuilder] '{RootName}' 생성 완료 (HandView, HandUIBinder 연결)");
    }

    [ContextMenu("2. Clear UI")]
    private void ClearUI()
    {
        if (builtRoot == null)
        {
            Debug.Log("[UIBuilder] 이 UIBuilder로 만든 UI가 없습니다. (예전에 만든 UI는 직접 지워주세요)");
            return;
        }

        Undo.RecordObject(this, "Clear UI");
        Undo.DestroyObjectImmediate(builtRoot);
        builtRoot = null;
        builtBinder = null;
        EditorUtility.SetDirty(this);
        EditorSceneManager.MarkSceneDirty(gameObject.scene);
        Debug.Log($"[UIBuilder] '{RootName}' 삭제 완료");
    }

    /// <summary>이미 조립된 손패에 등장 연출만 추가한다. (다시 조립하지 않고 기존 카드를 유지)</summary>
    [ContextMenu("3. Add Appear Effect")]
    private void AddAppearEffect()
    {
        if (builtBinder == null)
        {
            Debug.LogWarning("[UIBuilder] 조립된 손패가 없습니다. 먼저 '1. Build UI'를 실행하세요.");
            return;
        }
        if (appearCardMaterial == null)
        {
            Debug.LogWarning("[UIBuilder] 등장 연출 카드 머티리얼이 비어 있습니다.");
            return;
        }

        Undo.RecordObject(builtBinder, "Add Appear Effect");
        int added = 0;
        foreach (HandUIBinder.SlotView view in builtBinder.SlotViews)
        {
            if (view.card == null || view.appear != null) continue;
            view.appear = CreateAppearEffect((RectTransform)view.card.transform, view.slot, true);
            added++;
        }

        EditorUtility.SetDirty(builtBinder);
        EditorSceneManager.MarkSceneDirty(gameObject.scene);
        Debug.Log($"[UIBuilder] 등장 연출 {added}개 추가 완료");
    }

    #region Canvas
    private Transform CreateCanvas()
    {
        GameObject go = new GameObject(RootName, typeof(RectTransform));
        go.layer = LayerMask.NameToLayer("UI");

        Canvas canvas = go.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceCamera;
        canvas.worldCamera = Camera.main;
        canvas.planeDistance = 1f;
        if (canvas.worldCamera == null)
            Debug.LogWarning("[UIBuilder] MainCamera 태그가 붙은 카메라가 없습니다. Render Camera를 직접 지정하세요.");

        CanvasScaler scaler = go.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = layout.referenceResolution;
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;

        go.AddComponent<GraphicRaycaster>();
        return go.transform;
    }
    #endregion

    #region Hand (카드 나열)
    private void CreateHand(Transform canvas)
    {
        // 슬롯은 항상 최대 장수만큼 미리 만들어 두고(풀링), 앞에서부터 count 개만 켠다
        int count = cardCount > 0 ? cardCount : Random.Range(1, MaxCards + 1);

        // 손패 영역: 화면 하단 중앙, 폭은 좌우 여백을 뺀 최대 폭
        RectTransform hand = NewUI("HandArea", canvas).GetComponent<RectTransform>();
        Place(hand, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), Vector2.zero, new Vector2(layout.MaxHandWidth, layout.cardSize.y));

        var slots = new List<RectTransform>();
        var views = new List<HandUIBinder.SlotView>();

        for (int i = 0; i < MaxCards; i++)
        {
            // 슬롯 = 카드 UI + 사라짐 파티클. 배치는 슬롯이 받는다 (피벗은 카드와 같은 하단 중앙)
            RectTransform slot = NewUI($"Slot_{i:00}", hand).GetComponent<RectTransform>();
            Place(slot, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), Vector2.zero, layout.cardSize);

            bool isAttack = Random.value < 0.5f;
            bool upgraded = Random.value < 0.25f;
            RectTransform card = CreateCard($"Card_{i:00}", slot, isAttack, upgraded, out TMP_Text titleText, out TMP_Text typeText);

            slots.Add(slot);
            views.Add(new HandUIBinder.SlotView
            {
                slot = slot,
                card = card.gameObject,
                vanishEffect = CreateVanishEffect(slot),
                appear = CreateAppearEffect(card, slot, false),
                titleText = titleText,
                typeText = typeText,
            });

            if (i < count)
            {
                // 초기 배치는 선택 없음(-1). 게임 중 선택 연출은 HandView.SetSelected 가 담당
                HandLayout.Apply(slot, HandLayout.Calculate(i, count, -1, layout));
            }
            else
            {
                // 대기 중인 슬롯: 기본 자세로 두고 꺼 둔다 (드로우 시 데이터 적용 후 켜고 HandView.Refresh)
                slot.anchoredPosition = new Vector2(0f, -layout.cardBottomHang);
                slot.gameObject.SetActive(false);
            }
        }

        // 손패 연출(HandView)과 플레이어 덱 연동(HandUIBinder)을 붙이고 참조를 넣음
        HandView handView = hand.gameObject.AddComponent<HandView>();
        handView.EditorSetup(slots, layout);
        builtBinder = hand.gameObject.AddComponent<HandUIBinder>();
        builtBinder.EditorSetup(views);
        EditorUtility.SetDirty(handView);
        EditorUtility.SetDirty(builtBinder);
    }

    /// <summary>카드 UI의 형제로 사라짐 파티클을 넣는다. 카드 중앙에 두고 꺼 둔다 (재생은 HandUIBinder)</summary>
    private ParticleSystem CreateVanishEffect(Transform slot)
    {
        if (vanishEffectPrefab == null) return null;

        GameObject effect = (GameObject)PrefabUtility.InstantiatePrefab(vanishEffectPrefab, slot);
        effect.transform.localPosition = new Vector3(0f, layout.cardSize.y * 0.5f, 0f);
        effect.transform.localRotation = Quaternion.identity;
        effect.SetActive(false);

        ParticleSystem system = effect.GetComponentInChildren<ParticleSystem>(true);
        if (system == null) Debug.LogWarning($"[UIBuilder] 사라짐 파티클 프리팹 '{vanishEffectPrefab.name}'에 ParticleSystem이 없습니다.");
        return system;
    }

    /// <summary>
    /// 카드 UI에 등장 연출을 붙이고, 카드의 형제로 플래시 파티클을 넣는다. (재생은 HandUIBinder → CardAppearEffect)
    /// 플래시는 Play On Awake가 꺼져 있어 켜 둔 채로 둔다.
    /// </summary>
    private CardAppearEffect CreateAppearEffect(RectTransform card, Transform slot, bool undo)
    {
        if (appearCardMaterial == null) return null;

        ParticleSystem flash = null;
        if (appearFlashPrefab != null)
        {
            GameObject effect = (GameObject)PrefabUtility.InstantiatePrefab(appearFlashPrefab, slot);
            effect.transform.localPosition = new Vector3(0f, card.sizeDelta.y * 0.5f, 0f);
            effect.transform.localRotation = Quaternion.identity;
            flash = effect.GetComponent<ParticleSystem>();
            if (undo) Undo.RegisterCreatedObjectUndo(effect, "Add Appear Effect");
        }

        CardAppearEffect appear = undo
            ? Undo.AddComponent<CardAppearEffect>(card.gameObject)
            : card.gameObject.AddComponent<CardAppearEffect>();
        appear.Setup(appearCardMaterial, appearEdgeMaterial, flash);
        EditorUtility.SetDirty(appear);
        return appear;
    }

    /// <summary>카드 한 장 조립. 피벗은 하단 중앙 (부채꼴 회전·확대 기준)</summary>
    private RectTransform CreateCard(string name, Transform parent, bool isAttack, bool upgraded, out TMP_Text titleText, out TMP_Text typeText)
    {
        GameObject card = NewPanel(name, parent, layout.cardSize);
        card.GetComponent<Image>().color = FrameColor;
        RectTransform rect = card.GetComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0f);
        rect.pivot = new Vector2(0.5f, 0f);

        GameObject body = NewPanel("CardBody", card.transform, Vector2.zero);
        body.GetComponent<Image>().color = BodyColor;
        Stretch(body.GetComponent<RectTransform>(), 6f);

        float w = layout.cardSize.x;
        string title = (isAttack ? "타격" : "수비") + (upgraded ? "+" : "");
        string desc = isAttack ? $"피해를 {(upgraded ? 9 : 6)} 줍니다." : $"방어도를 {(upgraded ? 8 : 5)} 얻습니다.";

        // 카드 이름 배너
        GameObject banner = NewPanel("TitleBanner", card.transform, Vector2.zero);
        banner.GetComponent<Image>().color = BannerColor;
        Place(banner.GetComponent<RectTransform>(), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -12f), new Vector2(w - 24f, 34f));
        titleText = NewText("TitleText", banner.transform, title, 22f, TextAlignmentOptions.Center);
        if (upgraded) titleText.color = UpgradedTextColor;
        Stretch(titleText.rectTransform, 0f);

        // 카드 일러스트 자리 (스프라이트 대체 색상 블록)
        GameObject art = NewPanel(isAttack ? "ArtImage_Attack" : "ArtImage_Skill", card.transform, Vector2.zero);
        art.GetComponent<Image>().color = isAttack ? AttackArtColor : SkillArtColor;
        Place(art.GetComponent<RectTransform>(), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -50f), new Vector2(w - 24f, 120f));

        // 카드 타입 라벨
        GameObject typeLabel = NewPanel("TypeLabel", card.transform, Vector2.zero);
        typeLabel.GetComponent<Image>().color = TypeLabelColor;
        Place(typeLabel.GetComponent<RectTransform>(), new Vector2(0.5f, 1f), new Vector2(0.5f, 0.5f), new Vector2(0f, -172f), new Vector2(64f, 22f));
        typeText = NewText("TypeText", typeLabel.transform, isAttack ? "공격" : "스킬", 14f, TextAlignmentOptions.Center);
        Stretch(typeText.rectTransform, 0f);

        // 설명 (시안처럼 화면 하단에 일부 잘려도 무방)
        TMP_Text descText = NewText("DescText", card.transform, desc, 18f, TextAlignmentOptions.Top);
        Place(descText.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -190f), new Vector2(w - 30f, 70f));

        return rect;
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

    // 현재 화면에서는 사용하지 않음 (지정된 도우미 형태 유지)
    private Button NewButton(string name, Transform parent, string label, out TMP_Text labelText)
    {
        GameObject go = NewPanel(name, parent, new Vector2(160f, 50f));
        Image image = go.GetComponent<Image>();
        image.raycastTarget = true;
        Button button = go.AddComponent<Button>();
        button.targetGraphic = image;

        labelText = NewText("Label", go.transform, label, 24f, TextAlignmentOptions.Center);
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
