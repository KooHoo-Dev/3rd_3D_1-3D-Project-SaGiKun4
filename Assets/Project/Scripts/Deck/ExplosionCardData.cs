using System.Collections.Generic;
using Study3D.FpsGame.Weapon.Cards;
using UnityEngine;

namespace Study3D.FpsGame.Deck
{
    // 폭발 강화 카드: 발동한 지점을 중심으로 반경 안의 적들에게 범위 피해를 줍니다.
    // - 어느 시점에 걸어도 같은 폭발을 일으킵니다. (충돌: 부딪힐 때마다, 소멸: 마지막에 한 번)
    // - 직접 맞은 적도 반경 안에 있으면 폭발 피해를 함께 받습니다.
    // - 반경과 피해는 Effect 배율을 받습니다.
    [CreateAssetMenu(menuName = "Card/Explosion Card", fileName = "ExplosionCard")]
    public class ExplosionCardData : CardData
    {
        [SerializeField] private float radius = 2.5f;
        [SerializeField] private int damage = 10;
        [Tooltip("발동 이펙트를 크기 1로 재생했을 때 기준 파티클이 가장 커진 원의 반경(m). 이 원이 폭발 반경과 같아지도록 이펙트 전체를 같은 비율로 키우거나 줄입니다")]
        [SerializeField] private float effectRadius = 2f; // FX_TouchParticle_13의 안쪽 shock: 시작 크기 4 × 수명 끝 크기 1 → 지름 4

        public override CardType Type => CardType.Enhance;

        protected override void OnShoot(CardContext context) => Explode(context);
        protected override void OnFlying(CardContext context) => Explode(context);
        protected override void OnHit(CardContext context) => Explode(context);
        protected override void OnDestroyed(CardContext context) => Explode(context);

        private void Explode(CardContext context)
        {
            float finalRadius = radius * context.Multiplier.Effect;
            int finalDamage = CeilMultiply(damage, context.Multiplier.Effect);
            Collider[] colliders = Physics.OverlapSphere(context.Point, finalRadius, CombatSystem.Instance.EnemyMask);

            // 한 적의 여러 Collider가 반경 안에 있어도 한 번만 피해를 줍니다.
            var damaged = new HashSet<IDamageable>();
            foreach (Collider collider in colliders)
            {
                IDamageable target = CombatSystem.Instance.GetMonsterOrNull(collider);
                if (target == null || damaged.Add(target) == false) continue;

                CombatSystem.Instance.SendDamage(Player.Player.LocalPlayer, collider, finalDamage, context.Point);
            }

            // 이펙트의 원이 실제 폭발 범위와 같은 크기로 보이도록 맞춥니다.
            float effectScale = finalRadius / effectRadius;
            PlayEffect(context, effectScale);
            GizmoDrawer.WireSphere(context.Point, finalRadius, Color.red, 2f); // 실제 피해 범위 확인용 (에디터 전용)
            Debug.Log($"[Explosion] {Timing} {context.Point} 반경 {finalRadius}m (이펙트 x{effectScale:0.##}), 적 {damaged.Count}명에게 -{finalDamage}");
        }
    }
}
