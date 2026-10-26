using System;

namespace Study3D.FpsGame.Weapon.Cards
{
    // CardMultiplier
    // : 발사체 한 발에 실리는 공통 배율 입니다. 버프처럼 런타임에 바뀌는 값이라 카드(SO)가 아니라 발사체가 들고 다닙니다.
    // - 카드는 자기 수치에 이 배율을 곱해서 씁니다. 정수 값(분열 수, 관통 횟수 등)은 올림 처리합니다.
    [Serializable]
    public struct CardMultiplier
    {
        public float Size;   // 발사체 크기
        public float Effect; // 부가 효과 (폭발 반경/피해, 분열 수, 관통/반사 횟수 등)
        public float Damage; // 발사체가 직접 주는 피해

        public static CardMultiplier One => new CardMultiplier { Size = 1f, Effect = 1f, Damage = 1f };

        public override string ToString() => $"Size x{Size}, Effect x{Effect}, Damage x{Damage}";
    }
}
