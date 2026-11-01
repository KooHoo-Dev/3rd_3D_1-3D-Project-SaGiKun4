using UnityEngine;

namespace Study3D.FpsGame.Deck
{
    // 공격 카드: 자기 효과는 없고, Weapon에 쌓인 강화 카드를 모두 실어서 발사하게 합니다.
    [CreateAssetMenu(menuName = "Card/Attack Card", fileName = "AttackCard")]
    public class AttackCardData : CardData
    {
        public override CardType Type => CardType.Attack;
        // 시점에 발동하는 효과가 없으므로 시점 문구를 쓰지 않습니다.
        public override string Description => "쌓아 둔 강화 카드를 모두 실어 발사합니다.";
    }
}
