using Study3D.FpsGame.Player;
using TMPro;
using UnityEngine;

namespace Study3D.FpsGame.UI
{
    // CurrencyView
    // : 미니맵 테두리에서 이어진 검은 띠에 지금 가진 재화를 숫자로 보여줍니다.
    // - 재화가 바뀔 때(OnCurrencyChanged)만 글자를 바꿉니다. (TMP 글자를 매 프레임 바꾸면 메시를 다시 만듦)
    // - 구독은 Start에서 합니다. LocalPlayer는 Awake에서 등록되므로 Start 시점에는 항상 있습니다.
    public class CurrencyView : MonoBehaviour
    {
        [SerializeField] private TMP_Text currencyText;

        private LocalPlayer subscribedPlayer;

        private void Start()
        {
            subscribedPlayer = Player.Player.LocalPlayer;
            if (subscribedPlayer == null)
            {
                Debug.LogWarning("[CurrencyView] LocalPlayer가 없어 재화를 표시하지 않습니다.", this);
                return;
            }

            subscribedPlayer.OnCurrencyChanged += OnCurrencyChanged;
            ShowCurrency(subscribedPlayer.Currency);
        }

        private void OnDestroy()
        {
            if (subscribedPlayer != null)
            {
                subscribedPlayer.OnCurrencyChanged -= OnCurrencyChanged;
            }
        }

        private void OnCurrencyChanged(int amount, int currency)
        {
            ShowCurrency(currency);
        }

        private void ShowCurrency(int currency)
        {
            currencyText.text = currency.ToString();
        }

#if UNITY_EDITOR
        public void EditorSetup(TMP_Text currencyText)
        {
            this.currencyText = currencyText;
        }
#endif
    }
}
