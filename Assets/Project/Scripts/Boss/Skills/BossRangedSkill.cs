using System.Collections.Generic;
using Study3D.FpsGame.Deck;
using Study3D.FpsGame.Player;
using UnityEngine;

namespace Study3D.FpsGame.Boss
{
    // BossRangedSkill
    // : 원거리 공격. 플레이어 방향으로 카드 한 장을 쏩니다. (우선순위 2)
    // - 쓸 수 있는 조건: 쿨타임(5초)이 끝났고, 플레이어가 원거리 사거리(Boss.RangedRange) 안에 있을 때
    // - Attack02 를 재생하며 플레이어를 바라보다가, 재생 진행도가 발사 시점(fireTime)이 되면 한 번 쏩니다.
    //   쏘는 순간의 플레이어 몸 중앙을 겨누고, 그 뒤로는 따라가지 않습니다.
    // - 카드는 기본 공격 카드와 같습니다. (실린 강화 카드 없음) 피해, 속도, 크기는 BossCardLauncher 가 정합니다.
    // - 애니메이션이 끝나면 스킬이 끝나고 쿨타임이 시작됩니다.
    public class BossRangedSkill : BossSkill
    {
        private static readonly int CastAnim = Animator.StringToHash("Attack02");

        // 기본 공격 카드 = 실을 카드 없음
        private static readonly List<CardData> NoCards = new List<CardData>();

        [Header("발사")]
        [Tooltip("Attack02 재생 진행도(0~1)가 이 값이 되면 카드를 쏩니다")]
        [Range(0f, 1f)]
        [SerializeField] private float fireTime = 0.4f;

        [Header("Ref")]
        [Tooltip("카드가 나가는 위치 (지팡이 끝 등)")]
        [SerializeField] private Transform shotPoint;
        [SerializeField] private BossCardLauncher launcher;

        private bool hasFired;

        protected override float DefaultCoolTime
        {
            get { return 5f; }
        }

        protected override bool CheckCondition(Boss boss)
        {
            if (boss.HasTarget == false)
                return false;

            return boss.DistanceToPlayer <= boss.RangedRange;
        }

        protected override void OnBegin(Boss boss)
        {
            hasFired = false;
            boss.PlayAnim(CastAnim);
        }

        protected override void OnExecute(Boss boss, float deltaTime)
        {
            if (hasFired == false)
            {
                boss.LookAtPlayer(deltaTime);

                float progress = boss.GetAnimProgress(CastAnim);
                if (progress >= fireTime)
                {
                    FireAtPlayer();
                    hasFired = true;
                    // 이미 쐈으므로 남은 모션이 끊겨도 다시 쏘지 않습니다.
                    StartCoolTime();
                }
            }

            if (boss.IsAnimEnded(CastAnim) == false)
                return;

            FinishSkill();
        }

        private void FireAtPlayer()
        {
            LocalPlayer player = Player.Player.LocalPlayer;
            if (player == null)
                return;

            Vector3 target = player.MainCollider.bounds.center;
            launcher.Fire(shotPoint.position, target - shotPoint.position, NoCards);
        }
    }
}
