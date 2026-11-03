using System;

namespace Study3D.FpsGame.Player
{
    public partial class LocalPlayer
    {
        public event Action OnRequestedReload;
        public event Action OnEndedReload;
        public event Action<int, int, int> OnTakenDamage; // (데미지, 현재 HP, 최대 HP)
        public event Action<int, int, int> OnHealed; // (실제로 오른 양, 현재 HP, 최대 HP)
        public event Action OnDied;
        public event Action<int, int> OnCurrencyChanged; // (바뀐 양: 얻으면 +, 쓰면 -, 현재 재화)
    }
}