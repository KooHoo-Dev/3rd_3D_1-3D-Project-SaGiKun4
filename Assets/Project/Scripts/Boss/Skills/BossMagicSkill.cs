using System;
using System.Collections.Generic;
using Study3D.FpsGame.Deck;
using Study3D.FpsGame.Player;
using UnityEngine;

namespace Study3D.FpsGame.Boss
{
    // BossMagicSkill
    // : 마법 (필살기). 돌을 4번 이상 던졌으면 대기하며 플레이어의 카드를 훔쳐 돌마다 4방향으로 쏩니다. (우선순위 5)
    // - 쓸 수 있는 조건: 쿨타임(30초)이 끝났고, 돌던지기 횟수(BossRockSkill.ThrowCount)가 requiredThrowCount 이상
    // - 1. 대기 (channelTime = 30초)
    //      게이지(UI)와 발밑 바닥 파티클(channelCircleFx)을 켜고 아무것도 하지 않습니다. 받는 피해는 1/3 로 줄어듭니다. (Boss.DamageTakenRate)
    //      대기가 어떻게 끝나든(성공, 파훼, 경직) 게이지와 바닥 파티클은 꺼집니다.
    //      게이지와 카드 이름은 이 스킬이 알림(GaugeVisibleChanged, GaugeChanged, CopiedCardChanged)만 보내고, 화면 표시는 HUD 의 BossMagicView 가 합니다.
    //      대기 중 받은 피해를 줄이기 전 값으로 기록합니다. (Boss.DamageReceived) 게이지 = 기록 / breakDamage
    // - 2-a. 파훼: 기록이 breakDamage(1000) 이상이 되면
    //      돌을 모두 지우고 던진 횟수를 0 으로 만든 뒤, 보스를 기절시킵니다. (Boss.Stun → 기절 5초)
    // - 2-b. 성공: 대기 시간이 다 지나면 보스 몸에서 터지는 파티클(castFlashFx)을 한 번 재생하고
    //      플레이어 손패에서 강화 차수(EnhanceLevel) maxEnhanceLevel(3) 이하인 카드 중 하나를 무작위로 복사합니다.
    //      (손패에는 그대로 남음. 해당 카드가 없으면 기본 카드)
    //      복사한 카드 이름을 UI 에 표시하고, 돌을 모두 지운 뒤 각 돌 자리에서 +X, -X, +Z, -Z 4방향으로 그 카드를 쏩니다.
    //      (아직 날아가는 중인 돌은 지우기만 하고 쏘지 않습니다)
    //      쏜 뒤 던진 횟수를 0 으로 만들고, showTime 동안 카드 이름을 보여준 다음 끝납니다.
    //      복사한 카드는 이 한 번만 씁니다. (원거리 공격에는 쓰지 않음)
    // - 경직(HP 70/30/10%)으로 끊기면 쿨타임 없이 끝나고, 던진 횟수는 그대로라 다시 처음부터 대기합니다.
    public class BossMagicSkill : BossSkill
    {
        private static readonly Vector3[] FireDirections = { Vector3.right, Vector3.left, Vector3.forward, Vector3.back };

        [Header("조건")]
        [Tooltip("돌을 이만큼 던졌으면 마법을 씁니다")]
        [SerializeField] private int requiredThrowCount = 4;

        [Header("대기")]
        [Tooltip("대기 시간 (초). 이 동안 파훼하지 못하면 카드를 훔쳐 씁니다")]
        [SerializeField] private float channelTime = 30f;
        [Tooltip("대기 중 받는 피해 배율")]
        [SerializeField] private float damageTakenRate = 1f / 3f;
        [Tooltip("대기 중 이만큼(줄이기 전 피해 기준) 맞으면 파훼되어 기절합니다")]
        [SerializeField] private int breakDamage = 1000;
        [Tooltip("대기 중 재생할 애니메이터 상태 이름")]
        [SerializeField] private string channelAnimState = "SenseSomethingRPT";

        [Header("카드 복사")]
        [Tooltip("강화 차수가 이 값 이하인 카드만 복사합니다")]
        [SerializeField] private int maxEnhanceLevel = 3;
        [Tooltip("돌 자리에서 카드를 쏘는 높이 (바닥에서)")]
        [SerializeField] private float fireHeight = 1f;
        [Tooltip("쏜 뒤 카드 이름을 보여주는 시간 (초)")]
        [SerializeField] private float showTime = 1.5f;

        [Header("Ref")]
        [SerializeField] private BossRockSkill rockSkill;
        [SerializeField] private BossCardLauncher launcher;

        [Header("연출")]
        [Tooltip("대기 중 보스 발밑 바닥에 까는 파티클 (보스 자식, 바닥에 눕혀 둠)")]
        [SerializeField] private ParticleSystem channelCircleFx;
        [Tooltip("대기가 성공으로 끝날 때 보스 몸에서 한 번 터뜨리는 파티클 (보스 자식)")]
        [SerializeField] private ParticleSystem castFlashFx;

        private readonly List<CardData> launchCards = new List<CardData>();
        private readonly List<Vector3> firePositions = new List<Vector3>();

        private int channelAnimHash;
        private Boss owner;

        private bool isChanneling;
        private float timer;
        private int recordedDamage;

        // 대기 게이지를 켜거나 끌 때 알립니다. (true = 켬) 화면 표시는 UI(BossMagicView)가 합니다.
        public event Action<bool> GaugeVisibleChanged;

        // 대기 게이지 비율(0~1, 기록한 피해 / 파훼 피해)이 바뀔 때 알립니다.
        public event Action<float> GaugeChanged;

        // 복사한 카드 이름을 보여줄 때 이름을, 숨길 때 null 을 알립니다.
        public event Action<string> CopiedCardChanged;

        protected override float DefaultCoolTime
        {
            get { return 30f; }
        }

        private void Awake()
        {
            channelAnimHash = Animator.StringToHash(channelAnimState);
            owner = GetComponent<Boss>();
        }

        protected override void OnEnable()
        {
            base.OnEnable();
            SetGaugeVisible(false);
            SetCardText(null);
        }

        private void OnDisable()
        {
            StopChannel();
        }

        protected override bool CheckCondition(Boss boss)
        {
            if (rockSkill == null || launcher == null)
                return false;

            return rockSkill.ThrowCount >= requiredThrowCount;
        }

        protected override void OnBegin(Boss boss)
        {
            isChanneling = true;
            timer = channelTime;
            recordedDamage = 0;

            boss.DamageTakenRate = damageTakenRate;
            boss.DamageReceived += OnDamageReceived;

            SetGaugeVisible(true);
            UpdateGauge();
            SetCardText(null);
            PlayFx(channelCircleFx);
            boss.PlayAnim(channelAnimHash);
        }

        protected override void OnExecute(Boss boss, float deltaTime)
        {
            if (isChanneling)
            {
                TickChannel(boss, deltaTime);
                return;
            }

            // 성공 뒤: 카드 이름을 잠깐 보여주고 끝냅니다.
            boss.KeepAnim(Boss.IdleAnim);
            timer -= deltaTime;
            if (timer > 0f)
                return;

            SetCardText(null);
            FinishSkill();
        }

        // 경직, 사망 등으로 끊기면 대기를 풀고 표시를 끕니다.
        protected override void OnCancel(Boss boss)
        {
            StopChannel();
            SetCardText(null);
        }

        private void TickChannel(Boss boss, float deltaTime)
        {
            boss.KeepAnim(channelAnimHash);

            if (recordedDamage >= breakDamage)
            {
                Break(boss);
                return;
            }

            timer -= deltaTime;
            if (timer > 0f)
                return;

            CastStolenCard(boss);
        }

        private void OnDamageReceived(int rawDamage)
        {
            recordedDamage += rawDamage;
            UpdateGauge();
        }

        // 대기를 풉니다. (피해 배율 복구, 기록 중지, 게이지와 바닥 파티클 끔)
        private void StopChannel()
        {
            if (isChanneling == false)
                return;

            isChanneling = false;
            if (owner != null)
            {
                owner.DamageTakenRate = 1f;
                owner.DamageReceived -= OnDamageReceived;
            }
            SetGaugeVisible(false);
            StopFx(channelCircleFx);
        }

        #region 파훼

        // 파훼: 돌을 지우고 횟수를 0 으로 만든 뒤 기절합니다. 기절 상태로 넘어가면서 이 스킬은 취소됩니다.
        private void Break(Boss boss)
        {
            StopChannel();
            rockSkill.ClearRocks();
            rockSkill.ResetThrowCount();
            StartCoolTime();
            boss.Stun();
        }

        #endregion

        #region 성공

        // 성공: 카드를 복사해 표시하고, 돌을 지운 뒤 돌 자리마다 4방향으로 쏩니다.
        private void CastStolenCard(Boss boss)
        {
            StopChannel();
            PlayFx(castFlashFx);

            CardData stolen = PickStolenCard();
            launchCards.Clear();
            if (stolen != null)
            {
                stolen.CollectCards(launchCards);
                SetCardText(stolen.CardName);
            }
            else
            {
                SetCardText("기본 카드");
            }

            // 돌 자리를 먼저 기억하고 지웁니다. (쏜 카드가 자기 돌에 바로 부딪히지 않도록)
            // 아직 날아가는 돌은 발사 위치에서 뺍니다. (지우기만 함)
            firePositions.Clear();
            IReadOnlyList<BossRock> rocks = rockSkill.Rocks;
            for (int i = 0; i < rocks.Count; i++)
            {
                if (rocks[i] != null && rocks[i].IsLanded)
                {
                    firePositions.Add(GetFirePosition(rocks[i]));
                }
            }
            rockSkill.ClearRocks();
            rockSkill.ResetThrowCount();

            for (int i = 0; i < firePositions.Count; i++)
            {
                for (int d = 0; d < FireDirections.Length; d++)
                {
                    launcher.Fire(firePositions[i], FireDirections[d], launchCards);
                }
            }

            StartCoolTime();
            timer = showTime;
            boss.PlayAnim(Boss.IdleAnim);
        }

        // 돌 바닥에서 fireHeight 만큼 위
        private Vector3 GetFirePosition(BossRock rock)
        {
            Collider rockCollider = rock.GetComponent<Collider>();
            Vector3 position = rock.transform.position;
            position.y = rockCollider.bounds.min.y + fireHeight;
            return position;
        }

        // 손패에서 강화 차수가 maxEnhanceLevel 이하인 카드 중 하나를 무작위로 고릅니다. 없으면 null (= 기본 카드)
        private CardData PickStolenCard()
        {
            LocalPlayer player = Player.Player.LocalPlayer;
            if (player == null || player.Deck == null)
                return null;

            List<CardData> candidates = new List<CardData>();
            IReadOnlyList<CardData> hand = player.Deck.Hand;
            for (int i = 0; i < hand.Count; i++)
            {
                if (hand[i].EnhanceLevel <= maxEnhanceLevel)
                {
                    candidates.Add(hand[i]);
                }
            }

            if (candidates.Count == 0)
                return null;

            return candidates[UnityEngine.Random.Range(0, candidates.Count)];
        }

        #endregion

        #region 표시 (알림만 보내고, 화면 출력은 UI 가 합니다)

        private void UpdateGauge()
        {
            if (GaugeChanged == null)
                return;

            float ratio = Mathf.Clamp01((float)recordedDamage / breakDamage);
            GaugeChanged.Invoke(ratio);
        }

        private void SetGaugeVisible(bool isVisible)
        {
            if (GaugeVisibleChanged != null)
            {
                GaugeVisibleChanged.Invoke(isVisible);
            }
        }

        // 남아 있던 입자를 지우고 처음부터 재생합니다.
        private static void PlayFx(ParticleSystem fx)
        {
            if (fx == null)
                return;

            fx.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            fx.Play(true);
        }

        private static void StopFx(ParticleSystem fx)
        {
            if (fx == null)
                return;

            fx.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }

        // null 이면 숨깁니다.
        private void SetCardText(string cardName)
        {
            if (CopiedCardChanged != null)
            {
                CopiedCardChanged.Invoke(cardName);
            }
        }

        #endregion
    }
}
