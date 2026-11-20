using System.Collections.Generic;
using Study3D.FpsGame.Deck;
using UnityEngine;

namespace Study3D.FpsGame.Weapon.Cards
{
    // Projectile
    // : Weapon이 ShotPoint에서 Instantiate 하는 투사체 입니다. 덱의 카드(CardData)들이 실려서 날아갑니다.
    // - 생성될 때 받은 회전값 기준으로 자신의 +z(forward) 방향으로 직진합니다.
    // - 발사 / 진행 중 / 충돌 / 소멸 시점마다, 그 시점에 걸린 카드들을 역할 순서(반응 → 조향 → 효과)로,
    //   같은 역할끼리는 실린 순서(스택 순서)대로 실행합니다.
    // - 카드는 상태를 갖지 않으므로, 카드마다 필요한 런타임 값(남은 반사 수 등)은 여기 counts에 보관합니다.
    // - 소멸은 Die() 한 곳에서만 결정합니다.
    // - 매 프레임 처리는 Update가 아니라 Tick이며, ProjectileManager가 모든 발사체를 모아서 호출합니다.
    // - 오브젝트 풀에서 재사용되므로, 발사할 때마다 ResetState()로 이전 발사의 상태를 지웁니다.
    //   (풀이 없으면 Instantiate / Destroy로 동작합니다)
    public class Projectile : MonoBehaviour, IObjectPoolItem
    {
        [SerializeField] private float speed = 30.0f;
        [SerializeField] private float lifeTime = 120.0f; // 최대 수명. 관통, 반사, 유도 조합으로 끝없이 날아다니는 발사체를 끝내는 안전장치 (사라질 때 소멸 시점 카드가 발동)
        [SerializeField] private float surfaceOffset = 0.01f; // 통과, 반사 후 같은 면에 바로 다시 맞지 않도록 띄우는 거리
        [SerializeField] private float maxPowerScale = 2.0f;  // 화력 전환으로 커질 수 있는 최대 크기 배율
        [Tooltip("카드 꼬리선 (FX_UI_BarLine 하위 Line의 TrailRenderer). 풀에서 다시 꺼낼 때 이전 궤적을 지웁니다")]
        [SerializeField] private TrailRenderer trail;

        private const float MaxPower = 1e8f;     // 화력 배율 상한 (float 넘침 방지)
        private const float MaxDamage = 2e9f;    // 피해 상한 (int 넘침 방지)

        // 화력 배율: 발사체 상한 때문에 만들지 못한 분열 자식들의 몫을 부모가 대신 가집니다. (자식 n장 → ×(1 + n))
        // 피해와 폭발 피해에 곱해지고, 크기는 세제곱근만큼 커집니다(maxPowerScale까지).
        public float Power { get; private set; } = 1f;
        public int Damage
        {
            get { return (int)Mathf.Min(baseDamage * Power, MaxDamage); }
        }
        public CardMultiplier Multiplier { get; private set; }

        private int baseDamage; // 화력 배율을 곱하기 전 피해

        // 이 발사체가 속한 공격의 번호 입니다.
        // - 발사할 때 Weapon이 정해줍니다.
        // - 분열된 자식은 부모의 번호를 그대로 받습니다.
        // - 피해(충돌, 폭발)를 줄 때 함께 보내서, 적이 죽으면 어느 공격의 킬인지 알 수 있습니다.
        public int AttackId { get; private set; }

        // 풀에서 꺼낸 발사체는 풀이 Key를 넣어줍니다. Instantiate로 만든 발사체는 null 입니다.
        public string Key { get; set; }
        public GameObject GameObject => gameObject;

        private Vector3 baseScale; // Size 배율을 곱하기 전 프리팹 크기. 재사용할 때 배율이 누적되지 않도록 기준으로 씁니다.
        private Vector3 meshExtents = Vector3.one * 0.5f; // 스케일 1일 때 메시의 절반 크기. 여기에 현재 스케일을 곱한 상자로 판정합니다.

        private readonly List<CardData> cards = new List<CardData>();
        private readonly List<int> counts = new List<int>(); // cards와 같은 순서. 카드 몫 런타임 값
        // 시점별로 발동할 카드의 위치(cards 인덱스)를 미리 나눠둡니다. (CardTiming 값으로 접근)
        // 각 시점 안에서는 역할 순서(반응 → 조향 → 효과)로, 같은 역할끼리는 쌓은 순서로 정렬돼 있습니다.
        private static readonly CardRole[] RoleOrder = { CardRole.Response, CardRole.Steer, CardRole.Effect };
        private readonly List<int>[] timingSlots = { new List<int>(), new List<int>(), new List<int>(), new List<int>() };

        // 직전에 피해를 준 적. 같은 적에 연달아 다시 닿으면(한 적의 여러 Collider 등) 피해 없이 통과합니다.
        // 다른 적이나 벽에 부딪히면 바뀌므로, 벽에 반사된 뒤나 다른 적을 거친 뒤에는 같은 적을 다시 맞힐 수 있습니다.
        private IDamageable lastHitTarget;

        public IDamageable LastHitTarget => lastHitTarget;

        // 조향 카드(유도 등)가 쫓고 있는 적의 Collider. 없으면 null 입니다.
        public Collider SteerTarget { get; set; }

        // 보스가 쏜 발사체 (LaunchHostile)
        // - 적 대신 플레이어(LocalBody 레이어)를 맞히고, 벽과 구조물에는 플레이어 카드와 똑같이 부딪힙니다.
        // - 적(보스 포함)은 맞히지 않고 지나갑니다. 플레이어 카드와도 서로 통과합니다.
        // - 킬 그래프에 잡히지 않도록 공격 번호(AttackId)는 0 이고, AttackTracker 에 세지 않습니다.
        private IDamageable hostileOwner;
        private float flightSpeed; // 이번 발사의 날아가는 속도 (플레이어 카드 = speed, 보스 카드 = 보스가 정한 값)

        public bool IsHostile
        {
            get { return hostileOwner != null; }
        }

        // 조준 보정: 총구에서 출발한 발사체를 조준점까지 보낸 뒤, 카메라 레이 방향으로 꺾어 시선과 같은 직선을 따라가게 합니다.
        // - 총구 → 조준점 방향을 그대로 유지하면, 관통한 뒤에 시선과 어긋난 대각선으로 날아가기 때문입니다.
        // - 조준점에 닿기 전에 카드가 방향을 바꾸면(조향, 반사) 그 방향을 따르도록 보정을 끕니다.
        private bool hasAim;
        private Vector3 aimPoint;     // 카메라 레이가 맞은 위치 (못 맞혔으면 사거리 끝)
        private Vector3 aimDirection; // 카메라 레이 방향

        private float age;
        private bool isDead;
        private bool keepFlying;      // 이번 충돌 뒤에도 계속 날아갈지 (반응 카드, 충돌 중 조향이 켭니다)
        private bool responseClaimed; // 이번 충돌의 반응이 이미 정해졌는지 (앞의 반응 카드가 정했으면 뒤 반응 카드는 넘어갑니다)

        // ProjectileManager 목록 안의 위치. 목록에 없으면 -1 (복제될 때도 -1에서 시작하도록 초기값을 둡니다)
        public int ManagerIndex { get; set; } = -1;

        // 지금 실행 중인 시점과 그 시점 안에서 몇 번째 카드인지. 분열된 자식이 이어서 실행할 위치를 정할 때 씁니다.
        private CardTiming runningTiming;
        private int runningIndex;

        // 풀이 미리 만들어둘 때도 한 번 호출됩니다. (프리팹 크기를 기억해두는 용도)
        private void Awake()
        {
            baseScale = transform.localScale;
            if (TryGetComponent(out MeshFilter meshFilter) && meshFilter.sharedMesh != null)
                meshExtents = meshFilter.sharedMesh.bounds.extents;
        }

        // 풀이 있으면 풀에서 꺼내고, 없으면 prefab을 Instantiate 합니다.
        // 풀에서 꺼낸 발사체는 꺼진 상태라서, 위치와 회전을 먼저 잡은 뒤 켭니다. (켜진 첫 프레임에 엉뚱한 곳에서 움직이지 않도록)
        public static Projectile Spawn(string poolKey, Projectile prefab, Vector3 position, Quaternion rotation)
        {
            ObjectPoolManager pool = ObjectPoolManager.Instance;
            if (pool != null && string.IsNullOrEmpty(poolKey) == false && pool.HasPool(poolKey) &&
                pool.GetObjectOrNull(poolKey) is Projectile pooled)
            {
                pooled.transform.SetPositionAndRotation(position, rotation);
                pooled.gameObject.SetActive(true);
                pooled.ClearTrail();
                return pooled;
            }

            return Instantiate(prefab, position, rotation);
        }

        // 꼬리선은 월드 좌표로 지나온 점을 기억하므로, 풀에서 다시 꺼내면 이전 발사의 궤적이 새 위치까지 이어져 꺾여 보입니다.
        // 새 위치로 옮기고 켠 직후에 지나온 점을 모두 지웁니다.
        private void ClearTrail()
        {
            if (trail != null)
            {
                trail.Clear();
            }
        }

        // Spawn 직후 Weapon이 호출합니다. 실을 카드를 받고 발사 시점 카드를 실행합니다.
        // aimPoint, aimDirection : 조준점과 카메라 레이 방향. 조준점을 지나면 레이 방향으로 꺾습니다.
        public void Launch(IReadOnlyList<CardData> launchCards, int baseDamage, CardMultiplier multiplier, int attackId,
            Vector3 aimPoint, Vector3 aimDirection)
        {
            ResetState(multiplier);
            this.baseDamage = Mathf.CeilToInt(baseDamage * multiplier.Damage);
            AttackId = attackId;
            // 발사 시점 조향 카드가 보정을 끌 수 있도록 카드 실행보다 먼저 정합니다.
            SetAim(aimPoint, aimDirection);
            // 발사 시점 카드보다 먼저 세어 둡니다.
            AttackTracker.AddProjectile(AttackId);

            LoadCards(launchCards, multiplier);
            RunTiming(CardTiming.Shoot, FlightContext(), 0);
        }

        // 보스가 Spawn 직후 호출합니다. 플레이어를 맞히는 발사체로 발사합니다. (IsHostile)
        // - 피해는 배율 없이 damage 그대로, 크기는 프리팹 크기 × sizeScale, 속도는 flySpeed 입니다.
        // - 조준 보정 없이 생성할 때 받은 회전 방향으로 직진합니다.
        public void LaunchHostile(IReadOnlyList<CardData> launchCards, int damage, float flySpeed, float sizeScale, IDamageable owner)
        {
            CardMultiplier multiplier = CardMultiplier.One;
            multiplier.Size = sizeScale;
            ResetState(multiplier);
            hostileOwner = owner;
            flightSpeed = flySpeed;
            baseDamage = damage;
            AttackId = 0;

            LoadCards(launchCards, multiplier);
            RunTiming(CardTiming.Shoot, FlightContext(), 0);
        }

        private void LoadCards(IReadOnlyList<CardData> launchCards, CardMultiplier multiplier)
        {
            for (int i = 0; i < launchCards.Count; i++)
            {
                cards.Add(launchCards[i]);
                counts.Add(launchCards[i].CreateCount(multiplier));
            }

            BuildTimingSlots();
        }

        // 분열된 자식의 초기화: 부모의 카드와 런타임 값을 복사하되, 자신을 만든 분열 카드(splitContext.Slot)는 빼서 다시 분열하지 않게 합니다.
        // - 발사 시점 분열이면 부모가 실행하던 위치 뒤의 발사 시점 카드부터 이어서 실행합니다. (스택 순서 유지)
        // - 벽에서 분열했으면 회전한 자식 상자가 벽과 겹치지 않도록 면 바깥으로 밀어냅니다.
        private void LaunchSplit(Projectile parent, CardContext splitContext, int shootStartIndex)
        {
            // 풀이 없어 부모를 Instantiate로 복제한 경우, Awake가 이미 배율이 곱해진 크기를 기억하므로 부모의 기준 크기를 받아옵니다.
            baseScale = parent.baseScale;
            ResetState(parent.Multiplier);
            baseDamage = parent.baseDamage;
            // 분열을 몇 번 거쳐도 처음 발사한 공격으로 셉니다.
            AttackId = parent.AttackId;
            // 보스 카드에서 분열된 자식도 보스 카드입니다.
            hostileOwner = parent.hostileOwner;
            flightSpeed = parent.flightSpeed;
            // 부모가 아직 살아 있을 때 자식을 먼저 세므로, 분열 중에 살아 있는 수가 0이 되지 않습니다.
            if (IsHostile == false)
            {
                AttackTracker.AddProjectile(AttackId);
            }
            Power = parent.Power;
            ApplyPowerScale();
            if (splitContext.Target != null && splitContext.IsEnemy == false)
                PushOutOfSurface(splitContext.Point, splitContext.Normal);

            for (int i = 0; i < parent.cards.Count; i++)
            {
                if (i == splitContext.Slot) continue;
                cards.Add(parent.cards[i]);
                counts.Add(parent.counts[i]);
            }

            lastHitTarget = parent.lastHitTarget;
            SteerTarget = parent.SteerTarget; // 퍼져 나간 자식도 같은 적을 향해 모입니다.
            BuildTimingSlots();
            RunTiming(CardTiming.Shoot, FlightContext(), shootStartIndex);
        }

        // 이전 발사에서 남은 상태를 모두 지웁니다. (풀에서 재사용될 때 상태가 섞이지 않도록)
        private void ResetState(CardMultiplier multiplier)
        {
            Multiplier = multiplier;
            Power = 1f;
            transform.localScale = baseScale * multiplier.Size;

            cards.Clear();
            counts.Clear();
            lastHitTarget = null;
            SteerTarget = null;
            hostileOwner = null;
            flightSpeed = speed;
            hasAim = false; // 분열된 자식은 일부러 퍼뜨린 방향을 따르므로 보정을 이어받지 않습니다.
            age = 0f;
            isDead = false;
            keepFlying = false;
            responseClaimed = false;
            ProjectileManager.Register(this);
        }

        private void BuildTimingSlots()
        {
            foreach (List<int> slots in timingSlots) slots.Clear();
            // 역할마다 한 바퀴씩 돌며 담으므로, 같은 역할 안의 쌓은 순서가 그대로 유지됩니다.
            foreach (CardRole role in RoleOrder)
            {
                for (int i = 0; i < cards.Count; i++)
                {
                    if (cards[i].Role == role)
                        timingSlots[(int)cards[i].Timing].Add(i);
                }
            }
        }

        // 한 프레임 처리. 발사체마다 Update를 두지 않고 ProjectileManager가 모아서 호출합니다.
        public void Tick(float deltaTime)
        {
            if (isDead) return;

            age += deltaTime;
            if (age >= lifeTime)
            {
                Die(FlightContext());
                return;
            }

            RunTiming(CardTiming.Flying, FlightContext(), 0);
            Move(deltaTime);

            if (isDead == false && ProjectileManager.IsOutsideWorld(transform.position))
                Recall();
        }

        // 회수: 맵 밖으로 나간 발사체를 소멸 시점 카드 없이 조용히 풀로 돌려보냅니다.
        // (맵 밖에서 폭발 이펙트를 남기거나 분열해서 새 발사체를 만들지 않도록)
        private void Recall()
        {
            isDead = true;
            ProjectileManager.Unregister(this);
            ReturnToPool();
        }

        // 풀로 돌아가거나 파괴될 때 매니저 목록에서 빠집니다. (Die를 거치지 않고 꺼지는 경우 대비)
        private void OnDisable()
        {
            ProjectileManager.Unregister(this);
        }

        // 이번 프레임에 이동할 거리만큼 발사체 모양의 상자를 앞으로 쓸어서(BoxCast), 맞는 게 있으면 닿는 위치까지만 이동하고 충돌 처리를 합니다.
        // (Rigidbody 없이 Transform으로 움직이므로 충돌 콜백 대신 직접 검사합니다. 빠른 발사체가 벽을 뚫는 것도 막아줍니다)
        // - 상자 크기 = 메시 크기 × 현재 스케일 이라서, Size 배율로 커진 만큼 판정도 커집니다.
        // - BoxCast는 출발할 때 이미 겹쳐 있는 Collider는 감지하지 않습니다. 관통은 이 성질을 이용합니다.
        // - 조준 보정 중이면 이번 이동이 조준점을 넘는지 보고, 넘으면 조준점까지 이동 → 레이 방향으로 꺾기 → 남은 거리 이동 순서로 나눠 갑니다.
        private void Move(float deltaTime)
        {
            float distance = flightSpeed * deltaTime;

            if (hasAim)
            {
                float aimRemain = GetAimRemain();
                if (aimRemain <= distance)
                {
                    if (MoveStep(aimRemain))
                        return;

                    TurnToAim();
                    distance -= aimRemain;
                }
            }

            MoveStep(distance);
        }

        // distance만큼 앞으로 이동합니다. 도중에 부딪혔으면 닿은 위치에서 멈추고 true를 반환합니다.
        private bool MoveStep(float distance)
        {
            // 보스 카드는 적 대신 플레이어를 맞힙니다.
            int targetMask = CombatSystem.Instance.EnemyMask;
            if (IsHostile)
            {
                targetMask = CombatSystem.Instance.PlayerMask;
            }
            int contactMask = targetMask | CombatSystem.Instance.EnvironmentMask;

            // 트리거는 무시합니다. 적의 공격 판정(칼, 돌진 판정)도 Enemy 레이어의 트리거라서, 맞으면 피해 없이 카드만 사라지기 때문입니다.
            if (Physics.BoxCast(transform.position, HalfExtents, transform.forward, out RaycastHit hit,
                    transform.rotation, distance, contactMask, QueryTriggerInteraction.Ignore))
            {
                // hit.point는 상자 모서리가 닿은 지점이라 중심이 아닙니다. 중심은 진행 방향으로 닿은 거리만큼만 옮깁니다.
                transform.position += transform.forward * hit.distance;
                OnContact(hit);
                return true;
            }

            // Space.Self 기준 Vector3.forward = 발사체 자신의 +z 방향 입니다.
            transform.Translate(Vector3.forward * distance, Space.Self);
            return false;
        }

        // 조준 보정을 켭니다.
        private void SetAim(Vector3 point, Vector3 direction)
        {
            if (direction.sqrMagnitude < 0.0001f)
            {
                hasAim = false;
                return;
            }

            hasAim = true;
            aimPoint = point;
            aimDirection = direction;
        }

        // 진행 방향 기준으로 조준점까지 남은 거리. 관통 등으로 이미 지나쳤으면 0 입니다.
        private float GetAimRemain()
        {
            float remain = Vector3.Dot(aimPoint - transform.position, transform.forward);
            if (remain < 0f)
                return 0f;

            return remain;
        }

        // 조준점에 닿았으면 카메라 레이 방향으로 꺾고 보정을 끝냅니다.
        // 조준점은 카메라 레이 위의 점이라서, 꺾인 뒤의 경로는 카메라 레이와 겹칩니다.
        private void TurnToAim()
        {
            hasAim = false;
            transform.rotation = Quaternion.LookRotation(aimDirection);
        }

        // 적중 → 충돌 시점 카드 실행 → (계속 날아가게 한 카드가 없으면) 소멸 순서로 처리합니다.
        // - 무엇과 부딪혔는지만 분류하고, 카드의 종류는 알지 못합니다.
        private void OnContact(RaycastHit hit)
        {
            ContactKind contactKind = GetContactKind(hit.collider);
            if (contactKind == ContactKind.Ignore) return;

            // 맞힐 대상(플레이어 카드 = 적, 보스 카드 = 플레이어)이면 "적" 으로 분류됩니다.
            bool isEnemy = contactKind == ContactKind.Enemy;
            if (isEnemy)
            {
                IDamageable target = GetTargetOrNull(hit.collider);
                // 직전에 피해를 준 적에 다시 닿은 경우: 피해, 카드 발동 없이 그대로 통과합니다.
                if (target != null && target == lastHitTarget)
                {
                    PassThrough();
                    return;
                }

                lastHitTarget = target;
                SendHitDamage(target, hit.collider, hit.point);
            }
            else
            {
                // 벽에 부딪히면 기록을 비웁니다. (반사 뒤에 같은 적을 다시 맞힐 수 있도록)
                // 벽은 기록하지 않습니다. 방 전체가 Collider 하나여도 연달아 반사될 수 있어야 하기 때문입니다.
                lastHitTarget = null;
            }

            CardContext context = new CardContext(this, hit.point, hit.normal, hit.collider, contactKind);
            keepFlying = false;
            responseClaimed = false;
            RunTiming(CardTiming.Hit, context, 0);

            if (keepFlying == false) Die(context);
        }

        // 무엇과 부딪혔는지 분류합니다. 보스 카드는 플레이어를 "적"(맞힐 대상)으로 분류합니다.
        private ContactKind GetContactKind(Collider collider)
        {
            if (IsHostile)
            {
                Player.LocalPlayer player = Player.Player.LocalPlayer;
                if (player != null && collider == player.MainCollider)
                {
                    return ContactKind.Enemy;
                }
            }

            return CombatSystem.Instance.GetContactKind(collider);
        }

        // 맞힌 대상. 플레이어 카드는 등록된 적, 보스 카드는 플레이어 입니다.
        public IDamageable GetTargetOrNull(Collider collider)
        {
            if (IsHostile)
            {
                return Player.Player.LocalPlayer;
            }

            return CombatSystem.Instance.GetMonsterOrNull(collider);
        }

        // 맞힌 대상에게 발사체 피해를 줍니다.
        // - 플레이어 카드: 적에게 (CombatSystem.SendDamage 가 등록된 적인지 확인)
        // - 보스 카드: 플레이어에게 (적의 칼 판정 EnemyWeapon 과 같은 방식)
        private void SendHitDamage(IDamageable target, Collider collider, Vector3 point)
        {
            if (IsHostile == false)
            {
                CombatSystem.Instance.SendDamage(Player.Player.LocalPlayer, collider, Damage, point, AttackId);
                return;
            }

            if (target == null)
                return;

            CombatEvent combatEvent = new CombatEvent();
            combatEvent.Sender = hostileOwner;
            combatEvent.Receiver = target;
            combatEvent.Damage = Damage;
            combatEvent.HitPosition = point;
            combatEvent.Collider = collider;
            CombatSystem.Instance.AddInGameEvent(combatEvent);
        }

        // 해당 시점에 걸린 카드들을 실린 순서대로 실행합니다.
        private void RunTiming(CardTiming timing, CardContext context, int startIndex)
        {
            List<int> slots = timingSlots[(int)timing];
            runningTiming = timing;
            for (runningIndex = startIndex; runningIndex < slots.Count; runningIndex++)
            {
                context.Slot = slots[runningIndex];
                cards[context.Slot].Activate(context);
            }
        }

        // 소멸: 소멸 시점 카드를 한 번만 실행하고 사라집니다.
        // - 반드시 카드 실행 → 반환 순서여야 합니다. 먼저 반환하면 소멸 시점 분열이 풀에서 자기 자신을 다시 꺼낼 수 있습니다.
        private void Die(CardContext context)
        {
            if (isDead) return;
            isDead = true;

            RunTiming(CardTiming.Destroy, context, 0);
            ProjectileManager.Unregister(this);
            ReturnToPool();
        }

        // 풀에서 꺼낸 발사체는 풀로 돌려보내고, Instantiate로 만든 발사체는 Destroy 합니다.
        public void ReturnToPool()
        {
            if (Key != null && ObjectPoolManager.Instance != null) ObjectPoolManager.Instance.ReturnToPool(this);
            else Destroy(gameObject);
        }

        // 부딪힌 게 아닐 때(발사, 진행 중, 수명 만료)의 정보 입니다.
        private CardContext FlightContext() =>
            new CardContext(this, transform.position, transform.forward, null, ContactKind.Ignore);

        public int GetCount(int slot) => counts[slot];
        public void SetCount(int slot, int value) => counts[slot] = value;

        // 이번 충돌의 반응(관통, 반사 등)을 정할 권리를 가져갑니다. 앞의 반응 카드가 이미 정했으면 false를 반환합니다.
        // 가져가면 이번 충돌 뒤에도 소멸하지 않고 계속 날아갑니다.
        public bool TryClaimResponse()
        {
            if (responseClaimed)
                return false;

            responseClaimed = true;
            keepFlying = true;
            return true;
        }

        // 조향: 진행 방향을 direction으로 바꿉니다. 조향 카드(전이, 유도 등)가 호출합니다.
        // - 충돌 중에 조향하면 반응 결과가 소멸이어도 계속 날아갑니다. (조향 카드 혼자서도 동작하도록)
        // - 벽에 부딪힌 충돌이면 회전한 상자가 벽과 다시 겹치지 않도록 면 바깥으로 밀어냅니다.
        //   (겹친 채로 출발하면 BoxCast가 그 벽을 감지하지 못해 뚫고 나가기 때문입니다)
        public void Steer(CardContext context, Vector3 direction)
        {
            if (direction.sqrMagnitude < 0.0001f)
                return;

            transform.rotation = Quaternion.LookRotation(direction);
            hasAim = false; // 카드가 정한 방향을 따릅니다.
            if (context.Target == null)
                return; // 비행 중 조향

            keepFlying = true;
            if (context.IsEnemy == false)
                PushOutOfSurface(context.Point, context.Normal);
        }

        // 닿은 위치에서 살짝 안쪽으로 들어가 둡니다.
        // BoxCast는 출발할 때 겹쳐 있는 Collider를 감지하지 않으므로, 다음 프레임부터 같은 Collider를 지나쳐 갑니다.
        public void PassThrough()
        {
            transform.position += transform.forward * surfaceOffset;
        }

        // direction 방향으로 진행 방향을 바꾸고, 맞은 면에서 떨어뜨립니다.
        public void Redirect(Vector3 point, Vector3 normal, Vector3 direction)
        {
            transform.rotation = Quaternion.LookRotation(direction);
            hasAim = false; // 카드가 정한 방향을 따릅니다.
            PushOutOfSurface(point, normal);
        }

        // 회전한 상자가 맞은 면(point, normal)과 겹치지 않도록 면 바깥으로 밀어냅니다.
        // 겹친 채로 출발하면 BoxCast가 그 면을 감지하지 못해 벽을 뚫고 나가기 때문입니다.
        private void PushOutOfSurface(Vector3 point, Vector3 normal)
        {
            Vector3 halfExtents = HalfExtents;
            // 상자가 normal 방향으로 차지하는 반 두께
            float extent = Mathf.Abs(Vector3.Dot(transform.right, normal)) * halfExtents.x
                           + Mathf.Abs(Vector3.Dot(transform.up, normal)) * halfExtents.y
                           + Mathf.Abs(Vector3.Dot(transform.forward, normal)) * halfExtents.z;
            float needed = extent + surfaceOffset;
            float current = Vector3.Dot(transform.position - point, normal); // 중심이 면에서 떨어진 거리
            if (current < needed) transform.position += normal * (needed - current);
        }

        // 판정 상자의 절반 크기 (부모 스케일까지 반영한 월드 크기)
        private Vector3 HalfExtents => Vector3.Scale(meshExtents, transform.lossyScale);

        // 분열: 이 발사체를 복제해서 rotation 방향으로 날립니다. 발동 중인 분열 카드(context.Slot)는 자식에게 넘기지 않습니다.
        // 발사체 상한에 닿아 만들지 못했으면 false를 반환합니다. (호출한 쪽이 AbsorbSplits로 화력을 넘겨받습니다)
        public bool SpawnSplit(CardContext context, Quaternion rotation)
        {
            if (ProjectileManager.IsFull)
                return false;

            // 부모 상자의 중심에서 생성합니다. 적이면 안쪽으로 살짝 들여보내고, 벽이면 LaunchSplit에서 면 바깥으로 밀어냅니다.
            Vector3 position = context.Center;
            if (context.Target != null && context.IsEnemy)
                position += context.Forward * surfaceOffset;

            // 발사 시점 분열이면 자식은 이 분열 카드 뒤의 발사 시점 카드부터 이어서 실행합니다.
            // (분열 카드가 빠지므로 자식의 목록에서는 같은 인덱스가 다음 카드 입니다)
            // 충돌, 소멸 시점 분열이면 발사는 이미 끝났으므로 발사 시점 카드는 실행하지 않습니다.
            int shootStartIndex = runningTiming == CardTiming.Shoot ? runningIndex : int.MaxValue;

            // 같은 풀에서 꺼냅니다. 풀이 없으면 자기 자신을 복제합니다.
            Projectile child = Spawn(Key, this, position, rotation);
            child.LaunchSplit(this, context, shootStartIndex);
            return true;
        }

        // 화력 전환: 만들지 못한 분열 자식 count장의 몫을 이 발사체가 대신 가집니다. (화력 ×(1 + count))
        // 벽에서 커졌으면 커진 상자가 벽과 겹치지 않도록 다시 면 바깥으로 밀어냅니다.
        public void AbsorbSplits(CardContext context, int count)
        {
            if (count <= 0)
                return;

            Power = Mathf.Min(Power * (1 + count), MaxPower);
            ApplyPowerScale();
            if (context.Target != null && context.IsEnemy == false)
                PushOutOfSurface(context.Point, context.Normal);
        }

        // 화력 배율의 세제곱근만큼 키웁니다. (부피가 화력에 비례하도록, maxPowerScale까지)
        private void ApplyPowerScale()
        {
            float powerScale = Mathf.Min(Mathf.Pow(Power, 1f / 3f), maxPowerScale);
            transform.localScale = baseScale * (Multiplier.Size * powerScale);
        }
    }
}
