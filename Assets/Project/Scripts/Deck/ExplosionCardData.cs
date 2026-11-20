using System.Collections.Generic;
using Study3D.FpsGame.Data;
using Study3D.FpsGame.Weapon.Cards;
using UnityEngine;

namespace Study3D.FpsGame.Deck
{
    // 폭발 강화 카드: 발동한 지점을 중심으로 반경 안의 적들에게 범위 피해를 줍니다.
    // - 어느 시점에 걸어도 같은 폭발을 일으킵니다. (충돌: 부딪힐 때마다, 소멸: 마지막에 한 번)
    // - 직접 맞은 적도 반경 안에 있으면 폭발 피해를 함께 받습니다.
    // - 피해는 따로 갖지 않고 발사체 피해(카드 피해)에 비율을 곱해 정합니다. (Damage 배율, 화력 배율을 자동으로 따라감)
    // - 반경은 Effect 배율을 받습니다.
    public class ExplosionCardData : CardData
    {
        public ExplosionCardData(CardDef def) : base(def)
        {
        }

        public override CardType Type => CardType.Enhance;

        // 폭발 반경 (스펙 표의 Radius)
        private float Radius
        {
            get { return Spec.Radius; }
        }

        // 발사체 피해에 곱하는 비율 (스펙 표의 Ratio. 0.8 = 발사체 피해의 80%)
        private float DamageRatio
        {
            get { return Spec.Ratio; }
        }

        protected override void OnShoot(CardContext context)
        {
            Explode(context);
        }

        protected override void OnFlying(CardContext context)
        {
            Explode(context);
        }

        protected override void OnHit(CardContext context)
        {
            Explode(context);
        }

        protected override void OnDestroyed(CardContext context)
        {
            Explode(context);
        }

        private void Explode(CardContext context)
        {
            float finalRadius = Radius * context.Multiplier.Effect;
            // 발사체 피해에 이미 Damage 배율과 화력 배율(분열 상한으로 넘겨받은 몫)이 들어 있습니다.
            int finalDamage = CeilMultiply(context.Projectile.Damage, DamageRatio);

            // 보스 카드의 폭발은 적 대신 플레이어에게 피해를 줍니다.
            if (context.Projectile.IsHostile)
            {
                ExplodeOnPlayer(context, finalRadius, finalDamage);
            }
            else
            {
                ExplodeOnEnemies(context, finalRadius, finalDamage);
            }

            PlayExplosion(context, finalRadius);
        }

        private void ExplodeOnEnemies(CardContext context, float finalRadius, int finalDamage)
        {
            Collider[] colliders = Physics.OverlapSphere(context.Point, finalRadius, CombatSystem.Instance.EnemyMask);

            // 한 적의 여러 Collider가 반경 안에 있어도 한 번만 피해를 줍니다.
            HashSet<IDamageable> damaged = new HashSet<IDamageable>();
            foreach (Collider collider in colliders)
            {
                IDamageable target = CombatSystem.Instance.GetMonsterOrNull(collider);
                if (target == null)
                    continue;
                if (damaged.Add(target) == false)
                    continue;

                // 폭발한 발사체의 공격 번호를 함께 보냅니다. (폭발로 죽은 적도 그 공격의 킬로 셉니다)
                int attackId = context.Projectile.AttackId;
                CombatSystem.Instance.SendDamage(Player.Player.LocalPlayer, collider, finalDamage, context.Point, attackId);
            }
        }

        // 보스 카드의 폭발: 플레이어 몸이 반경 안에 들어오면 피해를 줍니다.
        private void ExplodeOnPlayer(CardContext context, float finalRadius, int finalDamage)
        {
            Player.LocalPlayer player = Player.Player.LocalPlayer;
            if (player == null || player.IsDead)
                return;

            Vector3 closest = player.MainCollider.ClosestPoint(context.Point);
            if ((closest - context.Point).sqrMagnitude > finalRadius * finalRadius)
                return;

            CombatEvent combatEvent = new CombatEvent();
            combatEvent.Sender = null;
            combatEvent.Receiver = player;
            combatEvent.Damage = finalDamage;
            combatEvent.HitPosition = closest;
            combatEvent.Collider = player.MainCollider;
            CombatSystem.Instance.AddInGameEvent(combatEvent);
        }

        // 폭발 이펙트와 소리
        private void PlayExplosion(CardContext context, float finalRadius)
        {
            // 이펙트의 원이 실제 폭발 범위와 같은 크기로 보이도록 맞춥니다.
            // 카드 표의 EffectRadius: 이펙트를 크기 1로 재생했을 때 원의 반경 (비어 있으면 1로 봄)
            float effectRadius = Def.EffectRadius > 0f ? Def.EffectRadius : 1f;
            float effectScale = finalRadius / effectRadius;
            PlayEffect(context, effectScale);
            PlayExplosionSound(context.Point);
        }

        // 폭발 지점에서 3D로 재생합니다. (멀수록 작게, 너무 멀면 생략)
        // 같은 프레임에 여러 번 터져도 AudioManager가 한 번만 재생합니다.
        private void PlayExplosionSound(Vector3 point)
        {
            if (AudioManager.Instance == null)
                return;

            AudioManager.Instance.PlaySfxAt(AudioManager.Sfx.Explosion, point);
        }

        public override string Description {
            get { return $"{TimingText(Timing)}, \n{ValueText(Radius)}m 반경에 {ValueText(DamageRatio)}배의 피해를 줍니다."; }
        }
    }
}
