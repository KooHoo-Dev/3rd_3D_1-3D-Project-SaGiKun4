using System.Collections.Generic;
using Study3D.FpsGame.Player;
using UnityEngine;

namespace Study3D.FpsGame.Boss
{
    // BossRockSkill
    // : 돌던지기. 마법으로 돌을 포물선으로 던져 착탄 지점에 방해물을 만듭니다. (우선순위 4)
    // - 쓸 수 있는 조건: 쿨타임(10초)이 끝났고 플레이어가 있을 때
    // - 1. 모으기: 머리 위(throwPoint)에 돌을 만들어 growTime 동안 스케일 0 에서 프리팹 스케일까지 키우고,
    //      다 커진 뒤 holdTime(1초) 동안 들고 있습니다. 이 동안 플레이어 쪽으로 몸을 돌립니다.
    //      들고 있는 돌은 Collider 가 꺼져 있어 아무것도 막지 않고, 던진 돌 목록에도 들어가지 않습니다.
    // - 2. 던지기: 던지는 모션을 시작하는 순간의 플레이어 발밑(바닥)을 착탄 지점으로 고정합니다. 이후 플레이어를 따라가지 않습니다.
    //      재생 진행도가 releaseTime 이 되면 들고 있던 돌을 던집니다. (BossRock)
    // - 던지기 전에 경직 등으로 끊기면 들고 있던 돌은 지웁니다.
    //   던진 순간 쿨타임이 시작되고 던진 횟수가 1 늘어납니다. (남은 모션이 경직으로 끊겨도 다시 던지지 않음)
    // - 던진 돌 목록과 던진 횟수를 가지고 있습니다. 마법 스킬이 횟수를 보고 발동하고, 돌을 모두 지웁니다.
    public class BossRockSkill : BossSkill
    {
        [Header("모션")]
        [Tooltip("던질 때 재생할 애니메이터 상태 이름")]
        [SerializeField] private string throwAnimState = "Attack02";
        [Tooltip("재생 진행도(0~1)가 이 값이 되면 돌을 던집니다")]
        [Range(0f, 1f)]
        [SerializeField] private float releaseTime = 0.4f;

        [Header("모으기")]
        [Tooltip("머리 위에서 돌이 0 에서 원래 크기까지 커지는 시간 (초)")]
        [SerializeField] private float growTime = 1f;
        [Tooltip("다 커진 뒤 던지기 전까지 들고 있는 시간 (초)")]
        [SerializeField] private float holdTime = 1f;

        [Header("돌")]
        [Tooltip("돌 프리팹 (BoxCollider + BossRock + 돌 모델, 레이어 Prop). 다 커진 크기는 프리팹 스케일")]
        [SerializeField] private BossRock rockPrefab;
        [Tooltip("날아가는 시간 (초)")]
        [SerializeField] private float flightTime = 1.2f;
        [Tooltip("포물선의 가장 높은 지점이 직선보다 얼마나 높은지")]
        [SerializeField] private float arcHeight = 5f;
        [Tooltip("날아가는 돌에 맞았을 때 피해")]
        [SerializeField] private int damage = 20;

        [Header("Ref")]
        [Tooltip("돌이 출발하는 위치 (머리 위 등)")]
        [SerializeField] private Transform throwPoint;

        private readonly List<BossRock> rocks = new List<BossRock>();

        private int throwAnimHash;
        private BossRock heldRock; // 머리 위에 들고 있는 돌 (던지면 null)
        private float chargeTimer;
        private bool isThrowing; // 모으기가 끝나고 던지는 모션 중인지
        private bool hasThrown;
        private Vector3 landPosition;
        private Boss owner;

        // 마지막으로 마법을 쓴 뒤(또는 전투 시작 뒤) 던진 횟수
        public int ThrowCount { get; private set; }

        // 지금 맵에 있는 돌 (날아가는 중인 돌 포함)
        public IReadOnlyList<BossRock> Rocks
        {
            get { return rocks; }
        }

        protected override float DefaultCoolTime
        {
            get { return 10f; }
        }

        private void Awake()
        {
            throwAnimHash = Animator.StringToHash(throwAnimState);
            owner = GetComponent<Boss>();
        }

        protected override bool CheckCondition(Boss boss)
        {
            return rockPrefab != null && boss.HasTarget;
        }

        protected override void OnBegin(Boss boss)
        {
            chargeTimer = 0f;
            isThrowing = false;
            hasThrown = false;

            heldRock = Instantiate(rockPrefab, GetThrowPosition(boss), Quaternion.identity);
            heldRock.Hold();
            heldRock.transform.localScale = Vector3.zero;
            boss.PlayAnim(Boss.IdleAnim);
        }

        protected override void OnExecute(Boss boss, float deltaTime)
        {
            if (isThrowing == false)
            {
                Charge(boss, deltaTime);
                return;
            }

            if (hasThrown == false)
            {
                boss.LookAtDirection(landPosition - boss.transform.position, deltaTime);
                heldRock.transform.position = GetThrowPosition(boss);

                if (boss.GetAnimProgress(throwAnimHash) >= releaseTime)
                {
                    ThrowRock(boss);
                    hasThrown = true;
                    StartCoolTime();
                }
            }

            if (boss.IsAnimEnded(throwAnimHash) == false)
                return;

            FinishSkill();
        }

        // 던지기 전에 끊기면 들고 있던 돌을 지웁니다.
        protected override void OnCancel(Boss boss)
        {
            if (heldRock != null)
            {
                heldRock.Remove();
                heldRock = null;
            }
        }

        // 모으기: 머리 위에서 돌을 키우고 잠깐 들고 있다가 던지는 모션으로 넘어갑니다.
        private void Charge(Boss boss, float deltaTime)
        {
            boss.KeepAnim(Boss.IdleAnim);
            boss.LookAtPlayer(deltaTime);

            chargeTimer += deltaTime;
            float growRatio = Mathf.Clamp01(chargeTimer / Mathf.Max(0.01f, growTime));
            heldRock.transform.position = GetThrowPosition(boss);
            heldRock.transform.localScale = rockPrefab.transform.localScale * growRatio;

            if (chargeTimer < growTime + holdTime)
                return;

            isThrowing = true;
            landPosition = FindPlayerGround();
            boss.PlayAnim(throwAnimHash);
        }

        // 마법이 돌을 모두 지울 때 부릅니다. (날아가는 중인 돌도 지움)
        public void ClearRocks()
        {
            for (int i = 0; i < rocks.Count; i++)
            {
                if (rocks[i] != null)
                {
                    rocks[i].Remove();
                }
            }
            rocks.Clear();
        }

        // 마법을 쓰거나 파훼됐을 때 부릅니다.
        public void ResetThrowCount()
        {
            ThrowCount = 0;
        }

        private void ThrowRock(Boss boss)
        {
            BossRock rock = heldRock;
            heldRock = null;

            rock.transform.localScale = rockPrefab.transform.localScale;
            rock.Launch(GetThrowPosition(boss), landPosition, flightTime, arcHeight, damage, owner);
            rocks.Add(rock);
            ThrowCount++;
        }

        // 돌을 들고 있다가 던지는 위치 (머리 위)
        private Vector3 GetThrowPosition(Boss boss)
        {
            if (throwPoint != null)
            {
                return throwPoint.position;
            }
            return boss.transform.position + Vector3.up * 3f;
        }

        // 지금 플레이어가 서 있는 바닥 위치. 플레이어 아래로 레이를 쏴서 찾고, 못 찾으면 플레이어 위치 그대로
        private Vector3 FindPlayerGround()
        {
            LocalPlayer player = Player.Player.LocalPlayer;
            Vector3 position = player.transform.position;

            RaycastHit hit;
            Vector3 origin = position + Vector3.up;
            if (Physics.Raycast(origin, Vector3.down, out hit, 20f, CombatSystem.Instance.WorldMask, QueryTriggerInteraction.Ignore))
            {
                return hit.point;
            }
            return position;
        }
    }
}
