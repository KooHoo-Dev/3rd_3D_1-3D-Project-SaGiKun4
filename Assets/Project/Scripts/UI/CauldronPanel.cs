using System;
using Study3D.FpsGame.Player;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Study3D.FpsGame.UI
{
    // CauldronPanel
    // : 가마솥과 상호작용하면 처음 뜨는 선택 화면입니다. 왼쪽은 "카드를 강화한다", 오른쪽은 "요리를 준비한다". UpgradeUIBuilder가 조립하면서 참조를 넣어 줍니다. (이름으로 찾지 않음)
    // - 화면 전체를 덮어서 뒤쪽 카드 목록은 가려지고 클릭도 막힙니다. (취소 버튼은 이 패널보다 위에 있어서 그대로 눌림)
    // - 왼쪽: OnUpgradeChosen을 알립니다. 강화창으로 바꾸는 것은 MenuScreen이 맡습니다.
    // - 오른쪽: 골드를 내고 최대 HP의 healRatio만큼 회복한 뒤 OnCooked를 알립니다. (화면을 닫는 것은 MenuScreen)
    //   비용은 가마솥과 상관없이 이번 판에 요리한 전체 횟수로 늘어납니다. (baseCost + costStep × LocalPlayer.CookCount)
    //   골드가 모자라거나 HP가 가득 차 있으면 버튼을 비활성화합니다.
    public class CauldronPanel : MonoBehaviour
    {
        [SerializeField] private Button upgradeButton;
        [SerializeField] private Button cookButton;
        [Tooltip("요리 버튼 아래 설명 (회복량, 비용, 못 쓰는 이유)")]
        [SerializeField] private TMP_Text cookDescText;

        [Header("요리")]
        [SerializeField, Range(0f, 1f)] private float healRatio = 0.3f; // 최대 HP 대비 회복량
        [SerializeField, Min(0)] private int baseCost = 200;            // 첫 요리 비용 (골드)
        [SerializeField, Min(0)] private int costStep = 100;            // 요리할 때마다 늘어나는 비용

        // 왼쪽(강화)을 골랐을 때
        public event Action OnUpgradeChosen;
        // 요리(회복)를 마쳤을 때
        public event Action OnCooked;


#if UNITY_EDITOR
        // UpgradeUIBuilder가 조립 직후 호출합니다.
        public void EditorSetup(Button upgrade, Button cook, TMP_Text cookDesc)
        {
            upgradeButton = upgrade;
            cookButton = cook;
            cookDescText = cookDesc;
        }
#endif

        private void Awake()
        {
            upgradeButton.onClick.AddListener(ChooseUpgrade);
            cookButton.onClick.AddListener(Cook);
        }

        private void OnEnable()
        {
            LocalPlayer player = Player.Player.LocalPlayer;
            if (player != null)
            {
                player.OnCurrencyChanged += OnCurrencyChanged;
            }
            UpdateCookButton();
        }

        private void OnDisable()
        {
            LocalPlayer player = Player.Player.LocalPlayer;
            if (player != null)
            {
                player.OnCurrencyChanged -= OnCurrencyChanged;
            }
        }

        private void ChooseUpgrade()
        {
            if (OnUpgradeChosen != null)
            {
                OnUpgradeChosen.Invoke();
            }
        }

        // 골드를 내고 회복합니다. 못 하는 상태면 그대로 둡니다.
        private void Cook()
        {
            LocalPlayer player = Player.Player.LocalPlayer;
            if (CanCook(player) == false)
                return;

            int cost = CalcCookCost(player);
            if (player.TrySpendCurrency(cost) == false)
                return;

            player.Heal(CalcHealAmount(player));
            player.AddCookCount();
            Debug.Log($"[Cauldron] 요리 {player.CookCount}회째 (-{cost} 골드, 다음 비용 {CalcCookCost(player)})");

            if (OnCooked != null)
            {
                OnCooked.Invoke();
            }
        }

        // 최대 HP × healRatio (내림, 최소 1)
        private int CalcHealAmount(LocalPlayer player)
        {
            int amount = Mathf.FloorToInt(player.Health.MaxHP * healRatio);
            return Mathf.Max(1, amount);
        }

        // baseCost + costStep × 지금까지 요리한 전체 횟수
        private int CalcCookCost(LocalPlayer player)
        {
            return baseCost + costStep * player.CookCount;
        }

        private bool CanCook(LocalPlayer player)
        {
            if (player == null || player.IsDead)
                return false;
            if (player.Health.HP >= player.Health.MaxHP)
                return false;
            return player.HasCurrency(CalcCookCost(player));
        }

        // 버튼 활성 여부와 설명 글자를 지금 상태에 맞춥니다.
        private void UpdateCookButton()
        {
            LocalPlayer player = Player.Player.LocalPlayer;
            cookButton.interactable = CanCook(player);

            if (cookDescText == null || player == null)
                return;

            int cost = CalcCookCost(player);
            int percent = Mathf.RoundToInt(healRatio * 100f);
            string desc = $"최대 체력의 {percent}% 회복\n{cost} 골드";
            if (player.Health.HP >= player.Health.MaxHP)
            {
                desc += "\n(체력이 가득 찼습니다)";
            }
            else if (player.HasCurrency(cost) == false)
            {
                desc += "\n(골드가 부족합니다)";
            }
            cookDescText.SetText(desc);
        }

        // 화면이 열려 있는 동안 골드가 바뀌면 버튼 상태를 다시 정합니다.
        private void OnCurrencyChanged(int amount, int currency)
        {
            UpdateCookButton();
        }
    }
}
