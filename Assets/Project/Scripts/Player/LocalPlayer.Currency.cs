namespace Study3D.FpsGame.Player
{
    // LocalPlayer - 재화
    // : 몬스터가 흩뿌린 재화(CurrencyPickup)를 먹으면 오릅니다. 값이 바뀌면 OnCurrencyChanged로 알립니다. (UI는 이 이벤트를 구독)
    public partial class LocalPlayer
    {
        public int Currency { get; private set; }

        public void AddCurrency(int amount)
        {
            if (amount <= 0)
                return;

            Currency += amount;
            if (OnCurrencyChanged != null)
                OnCurrencyChanged.Invoke(amount, Currency);
        }
    }
}
