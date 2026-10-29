using UnityEngine;

namespace Study3D.FpsGame.Deck
{
    // 시점 변경 강화 전용 카드: 강화 재료로 쓰면 다른 카드의 발동 시점을 이 카드의 Timing으로 바꿉니다.
    // - 이 카드의 Timing은 "자기가 발동할 시점"이 아니라 "바꿔 줄 시점" 입니다. (발사체에 실리지 않으므로 직접 발동하지 않음)
    // - 드로우되지 않고 강화 전용 보관함에만 들어갑니다. (CardType.Modifier)
    // - 시점을 바꿀 수 없는 카드(CanChangeTiming == false)에는 쓸 수 없습니다. 조합 카드는 맨 첫 재료 카드만 바뀝니다.
    [CreateAssetMenu(menuName = "Card/Timing Card", fileName = "TimingCard")]
    public class TimingCardData : CardData
    {
        public override CardType Type => CardType.Modifier;
        public override bool CanChangeTiming => false;
    }
}
