namespace Study3D.FpsGame.Player
{
    // LocalPlayer - 재화
    // : 몬스터가 흩뿌린 재화(CurrencyPickup)를 먹으면 오르고, 강화 등에 쓰면 줄어듭니다. 값이 바뀌면 OnCurrencyChanged로 알립니다. (UI는 이 이벤트를 구독)
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

        // 재화가 amount 이상 있는지
        public bool HasCurrency(int amount)
        {
            return Currency >= amount;
        }

        // amount만큼 씁니다. 모자라면 쓰지 않고 false를 반환합니다.
        public bool TrySpendCurrency(int amount)
        {
            if (amount <= 0)
                return true;
            if (HasCurrency(amount) == false)
                return false;

            Currency -= amount;
            if (OnCurrencyChanged != null)
                OnCurrencyChanged.Invoke(-amount, Currency);
            return true;
        }
    }
}
