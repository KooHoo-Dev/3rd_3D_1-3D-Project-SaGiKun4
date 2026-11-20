using Study3D.FpsGame.Player;
using UnityEngine;

namespace Study3D.FpsGame.Boss
{
    // BossRock
    // : 보스가 마법으로 던지는 돌 입니다. 날아가는 동안은 투사체, 떨어진 뒤에는 방해물이 됩니다. (돌 프리팹 루트에 붙임)
    // - 프리팹: 루트에 BoxCollider + 이 컴포넌트, 자식에 돌 모델(SM_Gen_Env_Rock_03, 스케일 4). 레이어는 Prop
    //   (플레이어 카드가 벽처럼 부딪히고 반사, 관통 카드가 반응함)
    //   판정은 루트의 BoxCollider 하나로 합니다. 모델 크기를 바꾸면 BoxCollider 크기도 같이 맞춰야 합니다.
    // - 비행: 출발점 → 착탄점을 직선으로 잇고, 그 위에 포물선 높이를 더합니다. 회전은 하지 않습니다.
    //   높이 = arcHeight × 4 × t × (1 - t)   (t = 0 출발, 0.5 가장 높음, 1 착탄)
    //   날아가는 동안은 Collider 를 꺼서 카드, 보스, 플레이어를 막지 않습니다.
    //   대신 매 프레임 돌의 상자 안에 플레이어가 겹치는지 확인해서, 한 번 던질 때 한 번만 피해를 줍니다. (맞아도 계속 날아감)
    // - 착탄: 바닥에 놓이고 Collider 를 켭니다. 착탄 피해는 없습니다.
    //   플레이어나 보스가 돌 자리에 끼어 있으면, 겹친 만큼 가장 가까운 옆면 바깥으로 밀어냅니다.
    //   (둘 다 세로로 선 원기둥으로 보고, 돌을 위에서 본 사각형과 비교합니다)
    // - 부술 수 없습니다. 마법이 Remove 로 지울 때까지 남습니다.
    [RequireComponent(typeof(BoxCollider))]
    public class BossRock : MonoBehaviour
    {
        private static readonly Collider[] OverlapBuffer = new Collider[8];

        private BoxCollider box;

        private bool isFlying;
        private Vector3 startPosition;
        private Vector3 landPosition;
        private float flightTime;
        private float arcHeight;
        private float elapsed;

        private int damage;
        private IDamageable owner;
        private bool hasHitPlayer;

        public bool IsLanded
        {
            get { return isFlying == false; }
        }

        private void Awake()
        {
            box = GetComponent<BoxCollider>();
        }

        // 던지기 전 보스 머리 위에 들고 있는 상태로 만듭니다. (BossRockSkill 이 생성 직후 부름)
        // Collider 를 꺼서 카드, 보스, 플레이어를 막지 않습니다.
        public void Hold()
        {
            isFlying = false;
            box.enabled = false;
        }

        // 들고 있던 돌을 던질 때 BossRockSkill 이 부릅니다.
        // landPosition: 착탄 지점의 바닥 위치. 돌의 바닥면이 여기에 닿도록 놓입니다.
        public void Launch(Vector3 start, Vector3 landPosition, float flightTime, float arcHeight, int damage, IDamageable owner)
        {
            startPosition = start;
            this.landPosition = landPosition + Vector3.up * GetHalfHeight();
            this.flightTime = Mathf.Max(0.01f, flightTime);
            this.arcHeight = arcHeight;
            this.damage = damage;
            this.owner = owner;

            elapsed = 0f;
            hasHitPlayer = false;
            isFlying = true;
            box.enabled = false;
            transform.position = startPosition;
        }

        private void Update()
        {
            if (isFlying == false || GameTime.IsPaused)
                return;

            elapsed += GameTime.DeltaTime;
            float t = Mathf.Clamp01(elapsed / flightTime);

            Vector3 position = Vector3.Lerp(startPosition, landPosition, t);
            position += Vector3.up * (arcHeight * 4f * t * (1f - t));
            transform.position = position;

            CheckHitPlayer();

            if (t >= 1f)
            {
                Land();
            }
        }

        // 마법이 돌을 지울 때 부릅니다.
        public void Remove()
        {
            Destroy(gameObject);
        }

        #region 비행 중 피격

        // 돌의 상자 안에 플레이어 Collider 가 겹쳐 있으면 피해를 줍니다. (한 번 던질 때 한 번만)
        private void CheckHitPlayer()
        {
            if (hasHitPlayer)
                return;

            LocalPlayer player = Player.Player.LocalPlayer;
            if (player == null || player.IsDead)
                return;

            Vector3 center = transform.TransformPoint(box.center);
            int count = Physics.OverlapBoxNonAlloc(center, GetHalfExtents(), OverlapBuffer, transform.rotation,
                CombatSystem.Instance.PlayerMask, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
            {
                if (OverlapBuffer[i] == player.MainCollider)
                {
                    hasHitPlayer = true;
                    SendDamage(player, center);
                    return;
                }
            }
        }

        private void SendDamage(LocalPlayer player, Vector3 center)
        {
            CombatEvent combatEvent = new CombatEvent();
            combatEvent.Sender = owner;
            combatEvent.Receiver = player;
            combatEvent.Damage = damage;
            combatEvent.HitPosition = player.MainCollider.ClosestPoint(center);
            combatEvent.Collider = player.MainCollider;
            CombatSystem.Instance.AddInGameEvent(combatEvent);
        }

        #endregion

        #region 착탄

        private void Land()
        {
            isFlying = false;
            transform.position = landPosition;
            box.enabled = true;

            PushOutPlayer();
            PushOutBoss();
        }

        private void PushOutPlayer()
        {
            LocalPlayer player = Player.Player.LocalPlayer;
            if (player == null)
                return;

            float radius = player.MainCollider.bounds.extents.x;
            Vector3 push = GetPushOut(player.transform.position, radius);
            if (push.sqrMagnitude > 0f)
            {
                player.Teleport(player.transform.position + push, player.transform.rotation);
            }
        }

        private void PushOutBoss()
        {
            Boss boss = owner as Boss;
            if (boss == null)
                return;

            Vector3 push = GetPushOut(boss.transform.position, boss.BodyRadius);
            if (push.sqrMagnitude > 0f)
            {
                boss.transform.position += push;
            }
        }

        // 위에서 볼 때 반경 radius 의 원(position 중심)이 돌의 사각형과 겹치면, 빠져나갈 이동량을 돌려줍니다. 안 겹치면 0
        // - 돌 기준 좌표로 바꾼 뒤, x 와 z 중 덜 파고든 쪽으로 밀어냅니다. (가장 가까운 옆면 방향)
        private Vector3 GetPushOut(Vector3 position, float radius)
        {
            Vector3 center = transform.TransformPoint(box.center);
            Vector3 halfExtents = GetHalfExtents();
            Vector3 local = Quaternion.Inverse(transform.rotation) * (position - center);

            float overlapX = halfExtents.x + radius - Mathf.Abs(local.x);
            float overlapZ = halfExtents.z + radius - Mathf.Abs(local.z);
            if (overlapX <= 0f || overlapZ <= 0f)
                return Vector3.zero;

            Vector3 localPush;
            if (overlapX < overlapZ)
            {
                localPush = new Vector3(GetSide(local.x) * overlapX, 0f, 0f);
            }
            else
            {
                localPush = new Vector3(0f, 0f, GetSide(local.z) * overlapZ);
            }

            return transform.rotation * localPush;
        }

        // 중심에서 어느 쪽에 있는지 (정확히 가운데면 + 쪽)
        private static float GetSide(float value)
        {
            if (value < 0f)
            {
                return -1f;
            }
            return 1f;
        }

        #endregion

        // 판정 상자의 절반 크기 (스케일까지 반영한 월드 크기)
        private Vector3 GetHalfExtents()
        {
            return Vector3.Scale(box.size, transform.lossyScale) * 0.5f;
        }

        // 피벗에서 돌 바닥면까지의 높이 (큐브처럼 피벗이 가운데면 높이의 절반)
        private float GetHalfHeight()
        {
            float boxBottom = (box.center.y - box.size.y * 0.5f) * transform.lossyScale.y;
            return -boxBottom;
        }
    }
}
