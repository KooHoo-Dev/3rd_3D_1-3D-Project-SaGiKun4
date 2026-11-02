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
        // 이 피해를 준 공격의 번호 입니다. (적의 무기처럼 공격에 속하지 않는 피해는 0)
        // - 피해는 큐에 들어갔다가 나중에 처리됩니다.
        // - 그 사이에 발사체가 풀로 돌아가 다른 공격에 다시 쓰일 수 있어서, 번호를 여기에 적어 둡니다.
        public int AttackId { get; set; }
        public override EventType Type => EventType.Combat;
    }

    public class HealEvent : InGameEvent
    {
        public int Heal { get; set; }
        public Vector3 Position { get; set; }
        public override EventType Type => EventType.Heal;
    }
}
