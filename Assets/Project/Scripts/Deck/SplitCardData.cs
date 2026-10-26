using Study3D.FpsGame.Weapon.Cards;
using UnityEngine;

namespace Study3D.FpsGame.Deck
{
    // 분열 강화 카드: 발동한 지점에서 발사체를 복제해 랜덤한 방향으로 퍼뜨립니다.
    // - 원본은 그대로 날아가고(또는 소멸하고), extraCount만큼 자식이 추가로 생깁니다. 수는 Effect 배율을 받습니다.
    // - 자식은 이 분열 카드를 빼고 나머지 카드를 물려받으므로 같은 카드로 다시 분열하지 않습니다.
    // - 퍼지는 기준 방향: 발사/진행 중/적 충돌은 진행 방향, 벽 충돌은 반사 방향
    [CreateAssetMenu(menuName = "Card/Split Card", fileName = "SplitCard")]
    public class SplitCardData : CardData
    {
        [SerializeField] private int extraCount = 2;            // 추가로 생기는 발사체 수
        [SerializeField] private float horizontalAngle = 30.0f; // 기준 방향에서 좌우로 퍼지는 최대 각도
        [SerializeField] private float verticalAngle = 10.0f;   // 기준 방향에서 위아래로 퍼지는 최대 각도

        public override CardType Type => CardType.Enhance;

        protected override void OnShoot(CardContext context) => Split(context);
        protected override void OnFlying(CardContext context) => Split(context);
        protected override void OnHit(CardContext context) => Split(context);
        protected override void OnDestroyed(CardContext context) => Split(context);

        private void Split(CardContext context)
        {
            Vector3 baseDirection = context.Target != null && context.IsEnemy == false
                ? Vector3.Reflect(context.Forward, context.Normal)
                : context.Forward;
            Quaternion baseRotation = Quaternion.LookRotation(baseDirection);

            int count = CeilMultiply(extraCount, context.Multiplier.Effect);
            for (int i = 0; i < count; i++)
            {
                float yaw = Random.Range(-horizontalAngle, horizontalAngle);
                float pitch = Random.Range(-verticalAngle, verticalAngle);
                context.Projectile.SpawnSplit(context, baseRotation * Quaternion.Euler(pitch, yaw, 0f));
            }
        }
    }
}
