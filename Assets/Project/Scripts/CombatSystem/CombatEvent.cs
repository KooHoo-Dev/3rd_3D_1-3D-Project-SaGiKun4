using UnityEngine;

namespace Study3D.FpsGame
{
    public abstract class InGameEvent
    {
        public enum EventType
        {
            Unknown,
            Combat,
            Heal,
        }

        public IDamageable Sender { get; set; }
        public IDamageable Receiver { get; set; }
        public abstract EventType Type { get; }
    }

    // 발사체가 "무엇과" 부딪혔는지의 분류 입니다. (CombatSystem.GetContactKind로 판단)
    // - 부딪힌 뒤 "어떻게" 반응할지(튕김, 관통 등)는 여기서 정하지 않고 발사체에 실린 카드가 처리합니다.
    public enum ContactKind
    {
        Ignore, // 처리 대상이 아님
        Enemy,  // 적: 피해를 주는 대상
        World,  // 벽, 바닥 등 환경
    }

    public class CombatEvent : InGameEvent
    {
        public int Damage { get; set; }
        public Vector3 HitPosition { get; set; }
        public Collider Collider { get; set; }
        public override EventType Type => EventType.Combat;
    }

    public class HealEvent : InGameEvent
    {
        public int Heal { get; set; }
        public Vector3 Position { get; set; }
        public override EventType Type => EventType.Heal;
    }
}
