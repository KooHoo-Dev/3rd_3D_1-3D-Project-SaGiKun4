using System;
using System.Collections.Generic;
using Study3D.FpsGame.Deck;
using UnityEngine;
using UnityEngine.UI;

namespace Study3D.FpsGame.UI
{
    // CardDropScreen
    // : 카드 보상(CardDrop)의 선택지를 화면에 띄웁니다. 화면을 가로로 3등분해 칸마다 가운데에 카드를 한 장씩 그립니다. 씬에 하나만 둡니다.
    // - UpgradeUIBuilder의 '3. Build Card Drop UI'가 조립하면서 참조를 넣어 줍니다. (이름으로 찾지 않음)
    // - 보상 쪽은 Instance.Open(선택지, 골랐을 때 할 일)만 부르면 됩니다. 카드를 누르면 그 번호를 돌려줍니다.
    // - 플레이어 조작 막기(메뉴 상태)와 숫자키/F/Esc 입력은 보상 쪽(CardDrop)이 맡고, 여기는 그리기와 클릭, 커서만 맡습니다.
    // - 닫는 것은 content를 끄는 것이라, 이 컴포넌트는 항상 켜져 있는 캔버스 루트에 붙습니다.
    public class CardDropScreen : MonoBehaviour
    {
        // 선택지 칸 하나
        [Serializable]
        public class OptionView
        {
            public GameObject root;
            public Button button;
            public CardView card;
        }

        [SerializeField] private GameObject content;
        [SerializeField] private List<OptionView> options = new List<OptionView>();

        public static CardDropScreen Instance { get; private set; }

        public bool IsOpen { get; private set; }

        private Action<int> onChosen;
        private readonly List<CardData> parts = new List<CardData>();

#if UNITY_EDITOR
        // UpgradeUIBuilder가 조립 직후 호출합니다.
        public void EditorSetup(GameObject screenContent, List<OptionView> optionViews)
        {
            content = screenContent;
            options = optionViews;
        }
#endif

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Debug.LogWarning($"{name}: CardDropScreen이 이미 있습니다. 이 컴포넌트는 쓰지 않습니다");
                enabled = false;
                return;
            }
            Instance = this;

            for (int i = 0; i < options.Count; i++)
            {
                int index = i;
                options[i].button.onClick.AddListener(() => OnOptionClicked(index));
            }
            // 화면은 열 때까지 꺼 둡니다. (씬에 켜진 채로 저장되어 있어도 가려지지 않게)
            content.SetActive(false);
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
        }

        // 선택지 칸을 눌렀을 때 그 번호를 알립니다.
        private void OnOptionClicked(int index)
        {
            if (onChosen != null)
            {
                onChosen.Invoke(index);
            }
        }

        // cards를 칸마다 한 장씩 그립니다. 칸보다 카드가 적으면 남는 칸은 끕니다. 카드를 누르면 chosen(번호)을 부릅니다.
        public void Open(IReadOnlyList<CardData> cards, Action<int> chosen)
        {
            if (cards.Count > options.Count)
            {
                Debug.LogWarning($"[CardDropScreen] 선택지 칸이 부족합니다. ({cards.Count}장 중 {options.Count}장만 표시)");
            }

            onChosen = chosen;
            for (int i = 0; i < options.Count; i++)
            {
                bool active = i < cards.Count;
                options[i].root.SetActive(active);

                if (active)
                {
                    Show(options[i].card, cards[i]);
                }
            }

            IsOpen = true;
            content.SetActive(true);
            Cursor.visible = true;
            Cursor.lockState = CursorLockMode.None;
        }

        public void Close()
        {
            if (IsOpen == false)
                return;

            IsOpen = false;
            onChosen = null;
            content.SetActive(false);
            Cursor.visible = false;
            Cursor.lockState = CursorLockMode.Locked;
        }

        // 이름/종류는 CardView가 그리고, 설명 자리에 차수와 (조합 카드면) 재료 카드를 적습니다. 같은 이름의 강화 카드를 구분하기 위함입니다.
        private void Show(CardView view, CardData card)
        {
            view.Show(card);
            if (view.descText == null)
                return;

            string desc = $"Lv.{card.EnhanceLevel}";
            if (card is CompositeCardData)
            {
                parts.Clear();
                card.CollectCards(parts);
                desc += "\n" + JoinPartNames();
            }
            view.descText.text = desc;
        }

        // 재료 카드 이름을 " + "로 이어 붙입니다.
        private string JoinPartNames()
        {
            string names = "";
            for (int i = 0; i < parts.Count; i++)
            {
                if (i > 0)
                {
                    names += " + ";
                }
                names += parts[i].CardName;
            }
            return names;
        }
    }
}
