using Study3D.FpsGame.Weapon.Cards;
using UnityEngine;

namespace Study3D.FpsGame.Deck
{
    // 조향 카드의 공통 부모: 어디를 향해 갈지(목표 중심 이동)를 정합니다.
    // - 반응 단계 뒤에 실행되므로, 반사로 꺾인 방향도 조향이 다시 고칩니다. (CardRole)
    // - 방향을 바꿀 때는 Projectile.Steer를 씁니다. 충돌 중에 조향하면 반응 결과가 소멸이어도 계속 날아갑니다.
    public abstract class SteerCardData : CardData
    {
        // 한 번에 검사할 Collider 수. 넘치는 적은 후보에서 빠집니다.
        private static readonly Collider[] SearchBuffer = new Collider[32];

        public override CardType Type => CardType.Enhance;
        public override CardRole Role => CardRole.Steer;
        public override bool CanChangeTiming => false; // 조향 카드는 각자 정해진 시점(전이: 충돌, 유도: 진행 중)에서만 동작합니다.

        // origin에서 radius 안의 적 중, 벽에 가려지지 않은 가장 가까운 적의 Collider를 찾습니다. 없으면 null 입니다.
        // - exclude(직전에 맞힌 적 등)는 후보에서 뺍니다.
        // - 등록된 몬스터의 Collider만 후보가 됩니다. (죽은 적은 Collider가 꺼져 있어 잡히지 않습니다)
        protected static Collider FindNearestEnemy(Vector3 origin, float radius, IDamageable exclude)
        {
            CombatSystem combat = CombatSystem.Instance;
            int count = Physics.OverlapSphereNonAlloc(origin, radius, SearchBuffer, combat.EnemyMask);

            Collider nearest = null;
            float nearestSqr = float.MaxValue;
            for (int i = 0; i < count; i++)
            {
                Collider candidate = SearchBuffer[i];
                IDamageable monster = combat.GetMonsterOrNull(candidate);
                if (monster == null || monster == exclude) continue;

                Vector3 center = candidate.bounds.center;
                float sqr = (center - origin).sqrMagnitude;
                if (sqr >= nearestSqr) continue;
                // 벽 너머 적을 향하면 벽에 박히므로, 가까운 후보만 가려졌는지 확인합니다.
                if (Physics.Linecast(origin, center, combat.EnvironmentMask)) continue;

                nearest = candidate;
                nearestSqr = sqr;
            }

            return nearest;
        }
    }
}
