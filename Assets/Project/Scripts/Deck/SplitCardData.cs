using Study3D.FpsGame.Data;
using Study3D.FpsGame.Weapon.Cards;
using UnityEngine;

namespace Study3D.FpsGame.Deck
{
    // 분열 강화 카드: 발동한 지점에서 발사체를 복제해 랜덤한 방향으로 퍼뜨립니다.
    // - 원본은 그대로 날아가고(또는 소멸하고), ExtraCount(스펙 표의 Count)만큼 자식이 추가로 생깁니다. 수는 Effect 배율을 받습니다.
    // - 자식은 이 분열 카드를 빼고 나머지 카드를 물려받으므로 같은 카드로 다시 분열하지 않습니다.
    // - 동시 발사체 상한에 닿으면 자식을 만들지 않고, 그만큼 원본의 화력(피해, 크기)을 키웁니다.
    // - 퍼지는 기준 방향: 발사/진행 중/적 충돌은 진행 방향, 벽 충돌은 반사 방향
    public class SplitCardData : CardData
    {
        public SplitCardData(CardDef def) : base(def)
        {
        }


        public override CardType Type => CardType.Enhance;

        // 추가로 생기는 발사체 수 (스펙 표의 Count. 원본 포함 Count + 1장)
        private int ExtraCount
        {
            get { return Spec.Count; }
        }

        protected override void OnShoot(CardContext context)
        {
            Split(context);
        }

        protected override void OnFlying(CardContext context)
        {
            Split(context);
        }

        protected override void OnHit(CardContext context)
        {
            Split(context);
        }

        protected override void OnDestroyed(CardContext context)
        {
            Split(context);
        }

        private void Split(CardContext context)
        {
            Quaternion baseRotation = Quaternion.LookRotation(GetBaseDirection(context));

            int count = CeilMultiply(ExtraCount, context.Multiplier.Effect);
            int absorbed = 0;
            for (int i = 0; i < count; i++)
            {
                float yaw = Random.Range(-Spec.AngleH, Spec.AngleH);
                float pitch = Random.Range(-Spec.AngleV, Spec.AngleV);
                Quaternion childRotation = baseRotation * Quaternion.Euler(pitch, yaw, 0f);
                if (context.Projectile.SpawnSplit(context, childRotation) == false)
                    absorbed++;
            }

            // 발사체 상한 때문에 만들지 못한 자식은 원본의 화력으로 바꿉니다.
            context.Projectile.AbsorbSplits(context, absorbed);
        }

        // 퍼지는 기준 방향: 벽에 부딪혔으면 반사 방향, 그 밖에는 진행 방향
        private Vector3 GetBaseDirection(CardContext context)
        {
            bool hitWorld = context.Target != null && context.IsEnemy == false;
            if (hitWorld)
                return Vector3.Reflect(context.Forward, context.Normal);

            return context.Forward;
        }
        
        public override string Description {
            get { return $"{TimingText(Timing)}, \n{ValueText(ExtraCount + 1)}개로 분열하여 날아갑니다."; }
        }
    }
}
