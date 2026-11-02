using UnityEngine;

namespace Study3D.FpsGame.Deck
{
    // 대상 변경 강화 전용 카드: 강화 재료로 쓰면 다른 카드의 반응 대상(적, 구조체)을 이 카드의 NewTarget으로 바꿉니다.
    // - 반응 카드(관통, 반사 등)에만 쓸 수 있습니다. 조합 카드는 발동 순서상 첫 반응 카드만 바뀝니다.
    // - 카드가 허용하지 않는 대상은 빠집니다. (관통은 맵을 뚫을 수 없으므로 "구조체"를 받으면 Prop만 관통)
    // - 드로우되지 않고 강화 전용 보관함에만 들어갑니다. (CardType.Modifier) 발사체에 실리지 않으므로 직접 발동하지 않습니다.
    [CreateAssetMenu(menuName = "Card/Target Card", fileName = "TargetCard")]
    public class TargetCardData : CardData
    {
        [Tooltip("바꿔 줄 반응 대상 (적 / 구조체(맵 + 구조물) / 둘 다)")]
        [SerializeField] private ContactTarget newTarget = ContactTarget.Enemy;

        public override CardType Type => CardType.Modifier;
        public override bool CanChangeTiming => false;
        public ContactTarget NewTarget => newTarget;

        // 자기가 발동하지 않으므로 바꿔 줄 대상을 설명합니다.
        public override string Description
        {
            get { return $"다른 카드가 {TargetText(newTarget)}에 반응하도록 바꿉니다."; }
        }

        // 바꿔 줄 대상을 파란색으로 감쌉니다.
        private static string TargetText(ContactTarget target)
        {
            bool enemy = (target & ContactTarget.Enemy) != 0;
            bool world = (target & (ContactTarget.World | ContactTarget.Prop)) != 0;
            if (enemy && world)
                return ValueText("적과 구조체") + " 모두";
            if (enemy)
                return ValueText("적");
            return ValueText("구조체");
        }
    }
}
