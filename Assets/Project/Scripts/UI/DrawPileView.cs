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
    // : Tab을 누르고 있는 동안 화면 왼쪽 가운데에 덱에 남은(아직 드로우되지 않은) 카드 목록을 보여 줍니다. DrawPileUIBuilder가 조립하면서 참조를 넣어 줍니다. (이름으로 찾지 않음)
    // - 항상 켜져 있는 캔버스 루트에 붙어서 Tab 입력을 직접 보고, 목록 패널만 켜고 끕니다. (PlayerController는 이 UI를 몰라도 됨)
    // - 조작할 수 없는 동안(메뉴 화면, 사망 등)은 Tab을 눌러도 보이지 않습니다.
    // - 목록은 매 프레임 다시 만들지 않습니다. 패널이 켜질 때 한 번, 켜져 있는 동안 드로우(OnCardAdded)가 일어날 때만 다시 그립니다.
    //   (강화 화면에서 뽑을 카드가 바뀌는 건 메뉴가 열려 있을 때라 패널이 꺼져 있으므로, 다음에 켤 때 반영됩니다)
    // - 드로우는 랜덤이라 뽑을 카드의 순서에는 의미가 없으므로 이름순으로 보여 줍니다.
    // - 칸은 템플릿을 복제해 필요한 만큼만 늘리고, 늘린 칸은 재사용합니다. (남는 칸은 끔)
    // - 칸은 위에서부터 세로로 쌓고, 한 열이 rowsPerColumn칸을 넘으면 오른쪽 옆 열로 이어집니다. (GridLayoutGroup의 행 수를 장수에 맞춤)
    public class DrawPileView : MonoBehaviour
    {
        [Tooltip("Tab을 누르는 동안 켜지는 목록 패널")]
        [SerializeField] private GameObject panel;
        [Tooltip("\"남은 덱 N장\" 제목")]
        [SerializeField] private TMP_Text titleText;
        [Tooltip("복제한 칸이 들어가는 목록 (세로로 채우고 옆 열로 넘어감)")]
        [SerializeField] private GridLayoutGroup grid;
        [Tooltip("복제할 칸 (꺼 둠)")]
        [SerializeField] private DrawPileRow rowTemplate;
        [Tooltip("한 열에 놓는 최대 칸 수. 넘으면 오른쪽 옆 열로 이어집니다")]
        [SerializeField, Min(1)] private int rowsPerColumn = 15;

        private readonly List<DrawPileRow> rows = new List<DrawPileRow>();
        private readonly List<CardData> cards = new List<CardData>();
        private CardDeck deck;

#if UNITY_EDITOR
        // DrawPileUIBuilder가 조립 직후 호출합니다.
        public void EditorSetup(GameObject listPanel, TMP_Text title, GridLayoutGroup listGrid, DrawPileRow template, int maxRows)
        {
            panel = listPanel;
            titleText = title;
            grid = listGrid;
            rowTemplate = template;
            rowsPerColumn = maxRows;
        }
#endif

        // LocalPlayer.Awake에서 덱이 만들어진 뒤라야 하므로 Start에서 연결합니다.
        private void Start()
        {
            panel.SetActive(false);

            if (Player.Player.LocalPlayer == null)
            {
                Debug.LogWarning("[DrawPileView] LocalPlayer가 없어 남은 덱 목록을 연결하지 못했습니다.");
                return;
            }

            deck = Player.Player.LocalPlayer.Deck;
            deck.OnCardAdded += OnCardAdded;
        }

        private void OnDestroy()
        {
            if (deck == null)
                return;

            deck.OnCardAdded -= OnCardAdded;
        }

        private void Update()
        {
            if (deck == null)
                return;

            bool show = IsShowKeyHeld();
            if (show == panel.activeSelf)
                return;

            panel.SetActive(show);
            if (show)
            {
                Refresh();
            }
        }

        // Tab을 누르고 있고, 조작할 수 있는 상태인지 (메뉴 화면이나 사망 중이면 false)
        private bool IsShowKeyHeld()
        {
            if (SimpleInput.GetKey(Key.Tab) == false)
                return false;

            return Player.Player.LocalPlayer.CanAct;
        }

        // 드로우하면 뽑을 카드가 줄어듭니다. 보고 있는 동안에만 다시 그립니다.
        private void OnCardAdded(CardData card)
        {
            if (panel.activeSelf)
            {
                Refresh();
            }
        }

        // 뽑을 카드를 이름순으로 읽어 제목과 줄을 다시 그립니다.
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
    }
}
