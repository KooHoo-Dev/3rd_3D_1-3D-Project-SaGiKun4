using System.Collections.Generic;
using Study.Utilities;
using Study3D.FpsGame.Deck;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Study3D.FpsGame.UI
{
    // DrawPileView
    // : 화면 왼쪽 가운데의 남은 덱(아직 드로우되지 않은 카드) 표시입니다. DrawPileUIBuilder가 조립하면서 참조를 넣어 줍니다. (이름으로 찾지 않음)
    // - 평소에는 공책 인덱스처럼 화면 왼쪽 끝에 붙은 네모(인덱스 탭)에 남은 장수만 보여 줍니다.
    // - Tab을 누르고 있는 동안은 목록 패널을 켭니다. 탭은 가로 레이아웃 때문에 목록 오른쪽 위 모서리로 밀려나 같이 보입니다. (탭 높이는 열고 닫아도 같음)
    // - 열고 닫을 때는 목록과 탭을 담은 루트 전체를 옆으로 밀어서, 탭을 잡고 목록을 화면 왼쪽 밖에서 끌어내는 것처럼 보이게 합니다.
    //   (패널 위치는 레이아웃이 정하므로 패널이 아니라 루트를 옮김. 위치만 바뀌므로 레이아웃은 다시 계산하지 않음)
    // - 항상 켜져 있는 캔버스 루트에 붙어서 Tab 입력을 직접 봅니다. (PlayerController는 이 UI를 몰라도 됨)
    // - 조작할 수 없는 동안(메뉴 화면, 사망 등)은 Tab을 눌러도 목록이 보이지 않습니다.
    // - 남은 장수는 매 프레임 정수 하나만 비교하고, 바뀌었을 때만 숫자/목록을 다시 그리고 탭에 비트 연출을 줍니다.
    //   (드로우, 버린 카드 되돌리기, 강화 재료로 빠지기 등 덱 이벤트가 없는 변경까지 모두 잡힘)
    //   목록이 꺼져 있을 때 장수 변경 없이 카드만 바뀐 경우(강화로 교체)는 다음에 켤 때 반영됩니다.
    // - 드로우는 랜덤이라 뽑을 카드의 순서에는 의미가 없으므로 이름순으로 보여 줍니다.
    // - 칸은 템플릿을 복제해 필요한 만큼만 늘리고, 늘린 칸은 재사용합니다. (남는 칸은 끔)
    // - 칸은 위에서부터 세로로 쌓고, 한 열이 rowsPerColumn칸을 넘으면 오른쪽 옆 열로 이어집니다. (GridLayoutGroup의 행 수를 장수에 맞춤)
    public class DrawPileView : MonoBehaviour
    {
        [Header("목록")]
        [Tooltip("목록 패널과 인덱스 탭을 담은 루트 (슬라이드로 옆으로 옮김)")]
        [SerializeField] private RectTransform root;
        [Tooltip("Tab을 누르는 동안 켜지는 목록 패널")]
        [SerializeField] private RectTransform panel;
        [Tooltip("\"남은 덱 N장\" 제목")]
        [SerializeField] private TMP_Text titleText;
        [Tooltip("복제한 칸이 들어가는 목록 (세로로 채우고 옆 열로 넘어감)")]
        [SerializeField] private GridLayoutGroup grid;
        [Tooltip("복제할 칸 (꺼 둠)")]
        [SerializeField] private DrawPileRow rowTemplate;
        [Tooltip("한 열에 놓는 최대 칸 수. 넘으면 오른쪽 옆 열로 이어집니다")]
        [SerializeField, Min(1)] private int rowsPerColumn = 15;

        [Header("슬라이드")]
        [Tooltip("다 닫힘 ↔ 다 열림에 걸리는 시간 (초)")]
        [SerializeField, Min(0.01f)] private float slideDuration = 0.15f;
        [Tooltip("열린 정도(가로: 0 ~ 1)에 따른 위치(세로: 0 = 숨김, 1 = 다 보임). 기본은 빨리 나오다가 끝에서 부드럽게 멈춤")]
        [SerializeField] private AnimationCurve slideCurve = new AnimationCurve(
            new Keyframe(0f, 0f, 0f, 2f),
            new Keyframe(1f, 1f, 0f, 0f));

        [Header("인덱스 탭")]
        [Tooltip("비트 연출로 크기가 바뀌는 탭 내용 (이미지, 장수, Tab 글자)")]
        [SerializeField] private RectTransform indexBeat;
        [Tooltip("탭 이미지 위에 겹치는 남은 장수")]
        [SerializeField] private TMP_Text countText;
        [Tooltip("장수가 바뀔 때의 크기 변화 (가로: 초, 세로: 크기 배율). 마지막 키의 시간이 연출 길이입니다")]
        [SerializeField] private AnimationCurve beatCurve = new AnimationCurve(
            new Keyframe(0f, 1f),
            new Keyframe(0.08f, 1.25f),
            new Keyframe(0.18f, 1f),
            new Keyframe(0.26f, 1.12f),
            new Keyframe(0.45f, 1f));

        private readonly List<DrawPileRow> rows = new List<DrawPileRow>();
        private readonly List<CardData> cards = new List<CardData>();
        private CardDeck deck;

        private float slide; // 열린 정도 (0 = 다 닫힘, 1 = 다 열림)
        private int shownCount = -1; // 마지막으로 그린 남은 장수
        private float beatTime = -1f; // 비트 연출 경과 시간 (연출 중이 아니면 -1)
        private float beatDuration;

#if UNITY_EDITOR
        // DrawPileUIBuilder가 조립 직후 호출합니다.
        public void EditorSetup(RectTransform slideRoot, RectTransform listPanel, TMP_Text title, GridLayoutGroup listGrid, DrawPileRow template, int maxRows, RectTransform beat, TMP_Text count)
        {
            root = slideRoot;
            panel = listPanel;
            titleText = title;
            grid = listGrid;
            rowTemplate = template;
            rowsPerColumn = maxRows;
            indexBeat = beat;
            countText = count;
        }
#endif

        // LocalPlayer.Awake에서 덱이 만들어진 뒤라야 하므로 Start에서 연결합니다.
        private void Start()
        {
            panel.gameObject.SetActive(false);
            SetRootX(0f);

            if (Player.Player.LocalPlayer == null)
            {
                Debug.LogWarning("[DrawPileView] LocalPlayer가 없어 남은 덱 표시를 연결하지 못했습니다.");
                return;
            }

            deck = Player.Player.LocalPlayer.Deck;
        }

        private void Update()
        {
            if (deck == null)
                return;

            UpdatePanel();
            UpdateCount();
            UpdateBeat();
        }

        #region 목록

        // Tab을 누르는 동안은 열리는 쪽(1)으로, 떼면 닫히는 쪽(0)으로 열린 정도를 옮기고 그만큼 루트를 밀어 냅니다.
        // 도중에 Tab을 떼거나 다시 누르면 지금 위치에서 방향만 바뀝니다.
        // 다 열렸거나 다 닫힌 채로 멈춰 있으면 아무것도 바꾸지 않습니다.
        private void UpdatePanel()
        {
            float target = 0f;
            if (IsShowKeyHeld())
            {
                target = 1f;
            }
            if (slide == target)
                return;

            if (slide == 0f)
            {
                BeginOpen();
            }

            slide = Mathf.MoveTowards(slide, target, Time.unscaledDeltaTime / slideDuration);
            if (slide == 0f)
            {
                EndClose();
                return;
            }

            float shown = slideCurve.Evaluate(slide);
            SetRootX(-panel.rect.width * (1f - shown));
        }

        // 열기 시작: 패널을 켜고 내용을 그린 뒤, 폭을 알기 위해 레이아웃을 한 번 바로 계산하고 다 숨긴 위치에 둡니다.
        // 탭은 계속 화면 왼쪽 끝에 있으므로 켜는 순간 튀지 않습니다.
        private void BeginOpen()
        {
            panel.gameObject.SetActive(true);
            Refresh();
            LayoutRebuilder.ForceRebuildLayoutImmediate(root);
            SetRootX(-panel.rect.width);
        }

        // 다 닫힘: 패널을 끄고 루트를 제자리로 돌립니다.
        // 패널이 꺼지면 레이아웃이 탭을 화면 왼쪽 끝에 두므로, 같은 프레임에 돌리면 화면상으로는 변화가 없습니다.
        private void EndClose()
        {
            panel.gameObject.SetActive(false);
            SetRootX(0f);
        }

        private void SetRootX(float x)
        {
            Vector2 position = root.anchoredPosition;
            position.x = x;
            root.anchoredPosition = position;
        }

        // Tab을 누르고 있고, 조작할 수 있는 상태인지 (메뉴 화면이나 사망 중이면 false)
        private bool IsShowKeyHeld()
        {
            if (SimpleInput.GetKey(Key.Tab) == false)
                return false;

            return Player.Player.LocalPlayer.CanAct;
        }

        // 뽑을 카드를 이름순으로 읽어 제목과 칸을 다시 그립니다.
        private void Refresh()
        {
            cards.Clear();
            cards.AddRange(deck.DrawPile);
            cards.Sort(CompareByName);

            titleText.text = $"남은 덱 {cards.Count}장";
            ApplyRowCount();
            ShowRows();
        }

        // 한 열의 칸 수를 장수에 맞춥니다. (rowsPerColumn장까지는 한 열, 넘으면 옆 열로)
        // 행 수를 고정해 두면 장수가 적을 때도 패널이 rowsPerColumn칸 높이가 되므로, 장수가 적으면 그만큼만 씁니다.
        private void ApplyRowCount()
        {
            grid.constraintCount = Mathf.Clamp(cards.Count, 1, rowsPerColumn);
        }

        // 한글 음절은 유니코드 순서가 가나다순입니다.
        private int CompareByName(CardData a, CardData b)
        {
            return string.CompareOrdinal(a.CardName, b.CardName);
        }

        // cards를 칸마다 한 장씩 적습니다. 칸이 모자라면 템플릿을 복제해 늘리고, 남는 칸은 끕니다.
        private void ShowRows()
        {
            while (rows.Count < cards.Count)
            {
                rows.Add(Instantiate(rowTemplate, grid.transform, false));
            }

            for (int i = 0; i < rows.Count; i++)
            {
                bool active = i < cards.Count;
                rows[i].gameObject.SetActive(active);

                if (active)
                {
                    rows[i].Text.text = cards[i].CardName;
                }
            }
        }

        #endregion

        #region 인덱스 탭

        // 남은 장수가 바뀌었을 때만 숫자를 바꾸고, 보고 있는 목록을 다시 그리고, 비트 연출을 시작합니다.
        // 처음 한 번은 숫자만 맞추고 비트는 주지 않습니다.
        private void UpdateCount()
        {
            int count = deck.DrawPile.Count;
            if (count == shownCount)
                return;

            bool firstShow = shownCount < 0;
            shownCount = count;
            countText.text = count.ToString();

            if (panel.gameObject.activeSelf)
            {
                Refresh();
            }
            if (firstShow == false)
            {
                StartBeat();
            }
        }

        // 연출 중에 다시 바뀌면 처음부터 다시 뜁니다.
        private void StartBeat()
        {
            if (beatCurve.length == 0)
                return;

            beatDuration = beatCurve[beatCurve.length - 1].time;
            beatTime = 0f;
        }

        // 커브대로 탭 내용의 크기를 바꿉니다. 게임 속도와 상관없이 같은 빠르기로 뜁니다.
        // 끝나면 원래 크기로 돌려 놓고 더 계산하지 않습니다.
        private void UpdateBeat()
        {
            if (beatTime < 0f)
                return;

            beatTime += Time.unscaledDeltaTime;
            if (beatTime >= beatDuration)
            {
                beatTime = -1f;
                indexBeat.localScale = Vector3.one;
                return;
            }

            float scale = beatCurve.Evaluate(beatTime);
            indexBeat.localScale = new Vector3(scale, scale, 1f);
        }

        #endregion
    }
}
