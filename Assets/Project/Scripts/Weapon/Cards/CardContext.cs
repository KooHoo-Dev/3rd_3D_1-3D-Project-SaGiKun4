using UnityEngine;

namespace Study3D.FpsGame.Weapon.Cards
{
    // 카드가 발동할 때 넘겨받는 "누가, 어디서, 무엇에" 정보 입니다.
    // - 카드(SO)는 상태를 갖지 않으므로, 이 카드 몫의 런타임 값(남은 관통 수 등)은 Count로 발사체에 읽고 씁니다.
    public struct CardContext
    {
        public Projectile Projectile;
        public int Slot;          // 발사체에 실린 카드 목록 중 이 카드의 위치 (Count를 찾는 데 사용)
        public Vector3 Point;     // 발동 위치 (충돌이면 맞은 지점)
        public Vector3 Center;    // 발동 순간 발사체 중심 위치 (상자 판정이라 맞은 지점과 다를 수 있음)
        public Vector3 Normal;    // 맞은 면의 법선 (부딪힌 게 아니면 진행 방향)
        public Vector3 Forward;   // 발동 순간 발사체의 진행 방향
        public Collider Target;   // 맞은 대상 (부딪힌 게 아니면 null)
        public bool IsEnemy;      // 맞은 대상이 적인지

        public CardContext(Projectile projectile, Vector3 point, Vector3 normal, Collider target, bool isEnemy)
        {
            Projectile = projectile;
            Slot = -1;
            Point = point;
            Center = projectile.transform.position;
            Normal = normal;
            Forward = projectile.transform.forward;
            Target = target;
            IsEnemy = isEnemy;
        }

        public CardMultiplier Multiplier => Projectile.Multiplier;

        public int Count
        {
            get => Projectile.GetCount(Slot);
            set => Projectile.SetCount(Slot, value);
        }
    }
}
