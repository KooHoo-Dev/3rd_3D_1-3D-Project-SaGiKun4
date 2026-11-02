using Study3D.FpsGame.Data;

namespace Study3D.FpsGame.Deck
{
    // 스펙 강화 전용 카드: 강화 재료로 쓰면 다른 카드의 스펙 레벨을 1 올립니다. (분열 2장 → 3장, 폭발 5m 0.8배 → 6m 1배 등)
    // - 레벨별 수치는 스펙 표(card_specs.tsv)에 있고, 카드의 줄 수가 최대 레벨입니다. 최대 레벨이면 쓸 수 없습니다.
    // - 조합 카드는 맨 첫 재료 카드만 오릅니다.
    // - 드로우되지 않고 강화 전용 보관함에만 들어갑니다. (CardType.Modifier) 발사체에 실리지 않으므로 직접 발동하지 않습니다.
    public class SpecCardData : CardData
    {
        public SpecCardData(CardDef def) : base(def)
        {
        }

        public override CardType Type => CardType.Modifier;
        public override bool CanChangeTiming => false;

        public override string Description
        {
            get { return $"다른 카드의 스펙 레벨을 {ValueText(1)} 올립니다."; }
        }
    }
}
