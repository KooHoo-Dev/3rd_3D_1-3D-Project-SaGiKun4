using UnityEngine;
#if UNITY_EDITOR
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.UI;
#endif

namespace Study3D.FpsGame.UI
{
    /// <summary>
    /// 화면 왼쪽의 HUD(왼쪽 위 원형 미니맵 + 재화 띠, 왼쪽 아래 초상화 + 체력 바)를 코드만으로 조립하는 에디터 툴.
    /// 빈 GameObject에 붙인 뒤 컴포넌트 우클릭 메뉴에서 실행한다.
    /// - 캔버스는 새로 만들지 않고, targetCanvas(씬의 HandCanvas) 아래에 "HUD UI" 루트를 만들어 붙인다.
    /// - 1번 메뉴는 화면만 만든다. 게임 로직(적 점 찍기, 재화/체력 갱신)은 3번 메뉴에서 따로 붙인다.
    /// - 이미지는 유니티 내장 UI 스프라이트만 쓴다. (UISprite = 둥근 사각형, Knob = 원)
    /// </summary>
    public class HudUIBuilder : MonoBehaviour
    {
#if UNITY_EDITOR
        private const string RootName = "HUD UI";

        // 유니티 내장 스프라이트 경로
        private const string RoundRectSpritePath = "UI/Skin/UISprite.psd";
        private const string CircleSpritePath = "UI/Skin/Knob.psd";

        // 화면 가장자리에서 띄울 거리
        private const float ScreenMargin = 30f;

        // 미니맵 크기
        private const float MinimapSize = 220f;
        private const float MinimapBorderWidth = 7f;

        // 초상화 크기
        private const float PortraitSize = 120f;
        private const float PortraitBorderWidth = 5f;
        private const float PortraitBarGap = 12f;

        [Header("폰트 (한글 폰트 교체용, 비우면 TMP 기본 폰트)")]
        [SerializeField] private TMP_FontAsset font;

        [Header("붙일 캔버스 (씬의 HandCanvas)")]
        [SerializeField] private Canvas targetCanvas;

        [Header("초상화 (PortraitCamera의 Output Texture와 같은 Render Texture)")]
        [SerializeField] private RenderTexture portraitTexture;

        // 조립한 결과 (중복 조립 막기, 지우기, 로직 연결에 사용). 이름으로 찾지 않고 이 참조로만 다룬다.
        [SerializeField, HideInInspector] private GameObject builtRoot;
        [SerializeField, HideInInspector] private RectTransform builtMinimapArea;
        [SerializeField, HideInInspector] private RectTransform builtEnemyDots;
        [SerializeField, HideInInspector] private RectTransform[] builtSampleDots;
        [SerializeField, HideInInspector] private TMP_Text builtCurrencyText;
        [SerializeField, HideInInspector] private RectTransform builtHpFill;
        [SerializeField, HideInInspector] private TMP_Text builtHpText;

        // 시안 색상
        private static readonly Color BorderColor = new Color(0.05f, 0.05f, 0.06f, 1f);
        private static readonly Color BandColor = new Color(0.05f, 0.05f, 0.06f, 0.9f);
        private static readonly Color MapBackColor = new Color(0.15f, 0.15f, 0.18f, 0.35f);
        private static readonly Color EnemyDotColor = new Color(0.95f, 0.15f, 0.15f, 1f);
        private static readonly Color PlayerMarkerColor = new Color(0.35f, 0.95f, 0.45f, 1f);
        private static readonly Color CurrencyIconColor = new Color(1f, 0.8f, 0.2f, 1f);
        private static readonly Color HpBackColor = new Color(0.05f, 0.05f, 0.06f, 0.85f);
        private static readonly Color HpFillColor = new Color(0.35f, 0.85f, 0.3f, 1f);

        private Sprite roundRectSprite;
        private Sprite circleSprite;

        [ContextMenu("1. Build UI")]
        private void BuildUI()
        {
            if (builtRoot != null)
            {
                Debug.LogWarning($"[HudUIBuilder] '{builtRoot.name}' 이(가) 이미 존재합니다. 먼저 '2. Clear UI'를 실행하세요.");
                return;
            }

            if (targetCanvas == null)
            {
                Debug.LogWarning("[HudUIBuilder] targetCanvas 칸이 비어 있습니다. 씬의 HandCanvas를 넣어 주세요.");
                return;
            }

            Undo.RecordObject(this, "Build UI");
            LoadBuiltinSprites();

            Transform root = CreateRoot(targetCanvas.transform);
            CreateMinimap(root);
            CreateStatus(root);
            builtRoot = root.gameObject;

            Undo.RegisterCreatedObjectUndo(root.gameObject, "Build UI");
            EditorUtility.SetDirty(this);
            EditorSceneManager.MarkSceneDirty(gameObject.scene);
            Debug.Log($"[HudUIBuilder] '{RootName}' 생성 완료");
        }

        // 이름으로 찾지 않고, 조립할 때 기억해 둔 루트(builtRoot)만 지운다.
        [ContextMenu("2. Clear UI")]
        private void ClearUI()
        {
            if (builtRoot == null)
            {
                Debug.Log("[HudUIBuilder] 이 HudUIBuilder로 만든 UI가 없습니다. (예전에 만든 UI는 직접 지워주세요)");
                return;
            }

            Undo.RecordObject(this, "Clear UI");
            Undo.DestroyObjectImmediate(builtRoot);
            builtRoot = null;
            EditorUtility.SetDirty(this);
            EditorSceneManager.MarkSceneDirty(gameObject.scene);
            Debug.Log($"[HudUIBuilder] '{RootName}' 삭제 완료");
        }

        private void LoadBuiltinSprites()
        {
            roundRectSprite = AssetDatabase.GetBuiltinExtraResource<Sprite>(RoundRectSpritePath);
            circleSprite = AssetDatabase.GetBuiltinExtraResource<Sprite>(CircleSpritePath);
        }

        // 1번 메뉴로 만든 화면에 게임 로직을 붙이고 참조를 넣는다.
        // - 미니맵(MinimapView), 재화(CurrencyView), 체력(PlayerHpView)
        [ContextMenu("3. Connect Logic")]
        private void ConnectLogic()
        {
            if (CanConnect() == false)
                return;

            Undo.RecordObject(this, "Connect Logic");
            ConnectMinimap();
            ConnectCurrency();
            ConnectHp();

            EditorUtility.SetDirty(this);
            EditorSceneManager.MarkSceneDirty(gameObject.scene);
            Debug.Log("[HudUIBuilder] 로직 연결 완료");
        }

        #region 로직 연결
        private bool CanConnect()
        {
            if (builtRoot == null)
            {
                Debug.LogWarning("[HudUIBuilder] 조립된 HUD가 없습니다. 먼저 '1. Build UI'를 실행하세요.");
                return false;
            }
            if (builtEnemyDots == null || builtSampleDots == null || builtSampleDots.Length == 0)
            {
                Debug.LogWarning("[HudUIBuilder] 예전 버전으로 만든 HUD라 연결할 부품 정보가 없습니다. '2. Clear UI' 후 '1. Build UI'를 다시 실행하세요.");
                return false;
            }
            return true;
        }

        // 첫 시안 점을 복사 원본으로 쓰고, 나머지 시안 점은 지운다. (원본은 게임 시작 시 MinimapView가 꺼 둠)
        private void ConnectMinimap()
        {
            MinimapView view = AddComponentOnce<MinimapView>(builtMinimapArea.gameObject);
            if (view == null)
                return;

            for (int i = 1; i < builtSampleDots.Length; i++)
            {
                if (builtSampleDots[i] != null)
                {
                    Undo.DestroyObjectImmediate(builtSampleDots[i].gameObject);
                }
            }

            RectTransform template = builtSampleDots[0];
            template.anchoredPosition = Vector2.zero;
            builtSampleDots = new RectTransform[] { template };

            // 점이 원 테두리에 붙어도 반쯤 잘리지 않도록 점 반지름만큼 안쪽까지만 쓴다
            float mapRadius = (MinimapSize - MinimapBorderWidth * 2f) * 0.5f - template.sizeDelta.x * 0.5f;
            view.EditorSetup(builtEnemyDots, template, mapRadius);
            EditorUtility.SetDirty(view);
        }

        private void ConnectCurrency()
        {
            CurrencyView view = AddComponentOnce<CurrencyView>(builtCurrencyText.transform.parent.gameObject);
            if (view == null)
                return;

            view.EditorSetup(builtCurrencyText);
            EditorUtility.SetDirty(view);
        }

        private void ConnectHp()
        {
            PlayerHpView view = AddComponentOnce<PlayerHpView>(builtHpFill.parent.gameObject);
            if (view == null)
                return;

            view.EditorSetup(builtHpFill, builtHpText);
            EditorUtility.SetDirty(view);
        }

        // 이미 붙어 있으면 겹쳐 붙이지 않고 null을 돌려준다
        private T AddComponentOnce<T>(GameObject target) where T : Component
        {
            if (target.GetComponent<T>() != null)
            {
                Debug.LogWarning($"[HudUIBuilder] {target.name}에 {typeof(T).Name}이(가) 이미 있어 건너뜁니다.");
                return null;
            }
            return Undo.AddComponent<T>(target);
        }
        #endregion

        #region Root
        // 캔버스를 새로 만들지 않고, 이미 있는 캔버스(HandCanvas) 아래에 화면을 꽉 채우는 루트 하나만 만든다.
        // - 캔버스의 설정(렌더 모드, 기준 해상도)은 그대로 쓴다.
        // - 맨 앞 자식으로 두어 손패 카드가 HUD 위에 그려지게 한다.
        private Transform CreateRoot(Transform canvas)
        {
            RectTransform root = NewUI(RootName, canvas).GetComponent<RectTransform>();
            Stretch(root, 0f);
            root.SetAsFirstSibling();
            return root;
        }
        #endregion

        #region Minimap (왼쪽 위: 원형 지도 + 재화 띠)
        // 그리는 순서(뒤 → 앞): 재화 띠 → 검은 원 테두리 → 반투명 지도 원
        // 재화 띠는 원 가운데에서 시작해 오른쪽으로 뻗고, 왼쪽 절반은 원에 가려져 "테두리가 연장된" 모양이 된다.
        private void CreateMinimap(Transform canvas)
        {
            RectTransform area = NewUI("MinimapArea", canvas).GetComponent<RectTransform>();
            Place(area, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(ScreenMargin, -ScreenMargin), new Vector2(MinimapSize, MinimapSize));
            builtMinimapArea = area;

            CreateCurrencyBand(area);

            Image border = NewPanel("MinimapBorder", area, new Vector2(MinimapSize, MinimapSize)).GetComponent<Image>();
            SetSprite(border, circleSprite, BorderColor);
            Place(border.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(MinimapSize, MinimapSize));

            CreateMapCircle(area);
        }

        // 재화 띠: 아이콘(금색 원) + 숫자
        private void CreateCurrencyBand(RectTransform area)
        {
            const float BandWidth = 270f;
            const float BandHeight = 40f;
            const float BandTop = 18f;

            Image band = NewPanel("CurrencyBand", area, new Vector2(BandWidth, BandHeight)).GetComponent<Image>();
            SetSprite(band, roundRectSprite, BandColor);
            band.type = Image.Type.Sliced;
            Place(band.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(MinimapSize * 0.5f, -BandTop), new Vector2(BandWidth, BandHeight));

            // 원 밖으로 나온 부분부터 내용을 둔다 (띠 왼쪽 끝 = 원 가운데)
            float contentStart = MinimapSize * 0.5f + 16f;

            Image icon = NewPanel("CurrencyIcon", band.transform, new Vector2(24f, 24f)).GetComponent<Image>();
            SetSprite(icon, circleSprite, CurrencyIconColor);
            Place(icon.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(contentStart, 0f), new Vector2(24f, 24f));

            TMP_Text text = NewText("CurrencyText", band.transform, "0", 26f, TextAlignmentOptions.MidlineLeft);
            Place(text.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(contentStart + 32f, 0f), new Vector2(BandWidth - contentStart - 40f, BandHeight));
            builtCurrencyText = text;
        }

        // 지도 원: 반투명 원 + Mask (안의 점이 원 밖으로 나가면 잘림), 가운데 플레이어 표시, 적 점 시안
        private void CreateMapCircle(RectTransform area)
        {
            float mapSize = MinimapSize - MinimapBorderWidth * 2f;

            Image map = NewPanel("MinimapMask", area, new Vector2(mapSize, mapSize)).GetComponent<Image>();
            SetSprite(map, circleSprite, MapBackColor);
            Place(map.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(mapSize, mapSize));
            Mask mask = map.gameObject.AddComponent<Mask>();
            mask.showMaskGraphic = true;

            CreateEnemyDots(map.transform);
            CreatePlayerMarker(map.transform);
        }

        // 적 점들을 담을 부모 (원 가운데 = 0,0). 시안 확인용 점 3개를 넣어 둔다.
        private void CreateEnemyDots(Transform map)
        {
            RectTransform dots = NewUI("EnemyDots", map).GetComponent<RectTransform>();
            Place(dots, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);

            Vector2[] samplePositions = new Vector2[]
            {
                new Vector2(40f, 55f),
                new Vector2(-62f, 20f),
                new Vector2(25f, -70f),
            };

            builtEnemyDots = dots;
            builtSampleDots = new RectTransform[samplePositions.Length];
            for (int i = 0; i < samplePositions.Length; i++)
            {
                Image dot = NewPanel($"EnemyDot_{i:00}", dots, new Vector2(12f, 12f)).GetComponent<Image>();
                SetSprite(dot, circleSprite, EnemyDotColor);
                Place(dot.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), samplePositions[i], new Vector2(12f, 12f));
                builtSampleDots[i] = dot.rectTransform;
            }
        }

        // 가운데 = 나. 초록 원 + 위쪽(바라보는 방향)을 가리키는 짧은 막대
        private void CreatePlayerMarker(Transform map)
        {
            Image marker = NewPanel("PlayerMarker", map, new Vector2(14f, 14f)).GetComponent<Image>();
            SetSprite(marker, circleSprite, PlayerMarkerColor);
            Place(marker.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(14f, 14f));

            Image heading = NewPanel("PlayerHeading", marker.transform, new Vector2(4f, 10f)).GetComponent<Image>();
            heading.color = PlayerMarkerColor;
            Place(heading.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 0f), Vector2.zero, new Vector2(4f, 10f));
        }
        #endregion

        #region Status (왼쪽 아래: 초상화 + 체력 바)
        private void CreateStatus(Transform canvas)
        {
            CreatePortrait(canvas);
            CreateHpBar(canvas);
        }

        // 초상화: 검은 원 테두리 + 원형 Mask + RawImage(PortraitCamera가 그린 Render Texture)
        // 카메라가 Render Texture에 그리기만 하면 화면이 나오므로 따로 붙일 로직은 없다.
        private void CreatePortrait(Transform canvas)
        {
            if (portraitTexture == null)
            {
                Debug.LogWarning("[HudUIBuilder] portraitTexture 칸이 비어 있어 빈 초상화 칸만 만듭니다. Render Texture를 넣은 뒤 다시 조립하세요.");
            }

            Image border = NewPanel("PortraitBorder", canvas, new Vector2(PortraitSize, PortraitSize)).GetComponent<Image>();
            SetSprite(border, circleSprite, BorderColor);
            Place(border.rectTransform, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(ScreenMargin, ScreenMargin), new Vector2(PortraitSize, PortraitSize));

            // 테두리 안쪽 원: 반투명 바탕 + Mask (네모난 Render Texture를 원 모양으로 자른다)
            Image mask = NewPanel("PortraitMask", border.transform, Vector2.zero).GetComponent<Image>();
            SetSprite(mask, circleSprite, MapBackColor);
            Stretch(mask.rectTransform, PortraitBorderWidth);
            Mask maskComponent = mask.gameObject.AddComponent<Mask>();
            maskComponent.showMaskGraphic = true;

            GameObject imageObject = NewUI("PortraitImage", mask.transform);
            RawImage image = imageObject.AddComponent<RawImage>();
            image.texture = portraitTexture;
            image.raycastTarget = false;
            Stretch(image.rectTransform, 0f);
        }

        // 체력 바: 검은 바탕 + 초록 채움 + "현재 / 최대" 숫자. 실드는 없음.
        // 초상화 오른쪽에 두고, 바닥선을 초상화 아래쪽에 맞춘다.
        private void CreateHpBar(Transform canvas)
        {
            const float BarWidth = 270f;
            const float BarHeight = 28f;

            float barX = ScreenMargin + PortraitSize + PortraitBarGap;

            Image back = NewPanel("HpBar", canvas, new Vector2(BarWidth, BarHeight)).GetComponent<Image>();
            SetSprite(back, roundRectSprite, HpBackColor);
            back.type = Image.Type.Sliced;
            Place(back.rectTransform, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(barX, ScreenMargin), new Vector2(BarWidth, BarHeight));

            Image fill = NewPanel("HpFill", back.transform, Vector2.zero).GetComponent<Image>();
            SetSprite(fill, roundRectSprite, HpFillColor);
            fill.type = Image.Type.Sliced;
            Stretch(fill.rectTransform, 3f);
            builtHpFill = fill.rectTransform;

            TMP_Text text = NewText("HpText", back.transform, "100 / 100", 18f, TextAlignmentOptions.Center);
            Stretch(text.rectTransform, 0f);
            builtHpText = text;
        }
        #endregion

        #region 도우미
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

        // 현재 화면에서는 사용하지 않음 (지정된 도우미 형태 유지)
        private Button NewButton(string name, Transform parent, string label, out TMP_Text labelText)
        {
            GameObject go = NewPanel(name, parent, new Vector2(160f, 50f));
            Image image = go.GetComponent<Image>();
            SetSprite(image, roundRectSprite, BandColor);
            image.type = Image.Type.Sliced;
            image.raycastTarget = true;
            Button button = go.AddComponent<Button>();

            labelText = NewText("Label", go.transform, label, 24f, TextAlignmentOptions.Center);
            Stretch(labelText.rectTransform, 0f);
            return button;
        }

        // 부모를 꽉 채우고 사방을 padding 만큼 띄운다
        private void Stretch(RectTransform rect, float padding)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = new Vector2(-padding * 2f, -padding * 2f);
        }

        private void Place(RectTransform rect, Vector2 anchor, Vector2 pivot, Vector2 pos, Vector2 size)
        {
            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.pivot = pivot;
            rect.anchoredPosition = pos;
            rect.sizeDelta = size;
        }

        private void SetSprite(Image image, Sprite sprite, Color color)
        {
            image.sprite = sprite;
            image.color = color;
        }
        #endregion
#endif
    }
}
