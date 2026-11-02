using Study3D.FpsGame.Data;

namespace Study3D.FpsGame.Deck
{
    // 시점 변경 강화 전용 카드: 강화 재료로 쓰면 다른 카드의 발동 시점을 이 카드의 Timing으로 바꿉니다.
    // - 이 카드의 Timing은 "자기가 발동할 시점"이 아니라 "바꿔 줄 시점" 입니다. (발사체에 실리지 않으므로 직접 발동하지 않음)
    // - 드로우되지 않고 강화 전용 보관함에만 들어갑니다. (CardType.Modifier)
    // - 바꿀 카드가 이 시점을 막아 두었으면(카드 표의 BlockedTimings) 쓸 수 없습니다. 조합 카드는 맨 첫 재료 카드만 바뀝니다.
    public class TimingCardData : CardData
    {
        public TimingCardData(CardDef def) : base(def)
        {
        }

        public override CardType Type => CardType.Modifier;
        public override bool CanChangeTiming => false;
        // 자기가 발동하지 않으므로 바꿔 줄 시점을 설명합니다.
        public override string Description
        {
            get { return $"다른 카드가 {TimingText(Timing)} 타이밍에 발동하도록 바꿉니다."; }
        }
    }
}
