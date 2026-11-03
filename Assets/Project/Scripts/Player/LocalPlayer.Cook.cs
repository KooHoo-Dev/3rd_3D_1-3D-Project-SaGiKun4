namespace Study3D.FpsGame.Player
{
    // LocalPlayer - 요리
    // : 가마솥에서 요리(회복)한 전체 횟수 입니다. 가마솥마다 따로 세지 않고, 요리 비용(CauldronPanel)이 이 횟수로 늘어납니다.
    // - 재화(Currency)처럼 판 전체에 걸친 값이라 플레이어가 가집니다. (씬이 바뀔 때 넘겨야 하면 재화와 같이 넘김)
    public partial class LocalPlayer
    {
        public int CookCount { get; private set; }

        public void AddCookCount()
        {
            CookCount++;
        }
    }
}
