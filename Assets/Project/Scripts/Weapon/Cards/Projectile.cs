using System.Collections.Generic;
using Study3D.FpsGame.Deck;
using UnityEngine;

namespace Study3D.FpsGame.Weapon.Cards
{
    // Projectile
    // : Weapon이 ShotPoint에서 Instantiate 하는 투사체 입니다. 덱의 카드(CardData)들이 실려서 날아갑니다.
    // - 생성될 때 받은 회전값 기준으로 자신의 +z(forward) 방향으로 직진합니다.
    // - 발사 / 진행 중 / 충돌 / 소멸 시점마다, 그 시점에 걸린 카드들을 실린 순서(스택 순서)대로 실행합니다.
    // - 카드는 상태를 갖지 않으므로, 카드마다 필요한 런타임 값(남은 관통 수 등)은 여기 counts에 보관합니다.
    // - 소멸은 Die() 한 곳에서만 결정합니다.
    // - 오브젝트 풀에서 재사용되므로, 발사할 때마다 ResetState()로 이전 발사의 상태를 지웁니다.
    //   (풀이 없으면 Instantiate / Destroy로 동작합니다)
    public class Projectile : MonoBehaviour, IObjectPoolItem
    {
        [SerializeField] private float speed = 30.0f;
        [SerializeField] private float lifeTime = 5.0f; // 아무것도 맞지 않았을 때 사라지는 시간 (사라질 때 소멸 시점 카드가 발동)
        [SerializeField] private float surfaceOffset = 0.01f; // 통과, 반사 후 같은 면에 바로 다시 맞지 않도록 띄우는 거리

        public int Damage { get; private set; }
        public CardMultiplier Multiplier { get; private set; }

        // 풀에서 꺼낸 발사체는 풀이 Key를 넣어줍니다. Instantiate로 만든 발사체는 null 입니다.
        public string Key { get; set; }
        public GameObject GameObject => gameObject;

        private Vector3 baseScale; // Size 배율을 곱하기 전 프리팹 크기. 재사용할 때 배율이 누적되지 않도록 기준으로 씁니다.
        private Vector3 meshExtents = Vector3.one * 0.5f; // 스케일 1일 때 메시의 절반 크기. 여기에 현재 스케일을 곱한 상자로 판정합니다.

        private readonly List<CardData> cards = new List<CardData>();
        private readonly List<int> counts = new List<int>(); // cards와 같은 순서. 카드 몫 런타임 값
        // 시점별로 발동할 카드의 위치(cards 인덱스)를 미리 나눠둡니다. (CardTiming 값으로 접근)
        private readonly List<int>[] timingSlots = { new List<int>(), new List<int>(), new List<int>(), new List<int>() };

        // 이 발사체가 이미 피해를 준 적들. 관통 중 한 적의 여러 Collider에 중복 피해를 주지 않기 위해 사용합니다.
        private readonly HashSet<IDamageable> hitTargets = new HashSet<IDamageable>();

        private float age;
        private bool isDead;
        private bool keepFlying;      // 이번 충돌 뒤에도 계속 날아갈지 (관통, 반사가 켭니다)
        private bool directionLocked; // 이번 충돌에서 진행 방향이 이미 정해졌는지 (앞 카드가 정했으면 뒤 카드는 바꾸지 않습니다)

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
                return pooled;
            }

            return Instantiate(prefab, position, rotation);
        }

        // Spawn 직후 Weapon이 호출합니다. 실을 카드를 받고 발사 시점 카드를 실행합니다.
        public void Launch(IReadOnlyList<CardData> launchCards, int baseDamage, CardMultiplier multiplier)
        {
            ResetState(multiplier);
            Damage = Mathf.CeilToInt(baseDamage * multiplier.Damage);

            for (int i = 0; i < launchCards.Count; i++)
            {
                cards.Add(launchCards[i]);
                counts.Add(launchCards[i].CreateCount(multiplier));
            }

            BuildTimingSlots();
            RunTiming(CardTiming.Shoot, FlightContext(), 0);
        }

        // 분열된 자식의 초기화: 부모의 카드와 런타임 값을 복사하되, 자신을 만든 분열 카드(splitContext.Slot)는 빼서 다시 분열하지 않게 합니다.
        // - 발사 시점 분열이면 부모가 실행하던 위치 뒤의 발사 시점 카드부터 이어서 실행합니다. (스택 순서 유지)
        // - 벽에서 분열했으면 회전한 자식 상자가 벽과 겹치지 않도록 면 바깥으로 밀어냅니다.
        private void LaunchSplit(Projectile parent, CardContext splitContext, int shootStartIndex)
        {
            // 풀이 없어 부모를 Instantiate로 복제한 경우, Awake가 이미 배율이 곱해진 크기를 기억하므로 부모의 기준 크기를 받아옵니다.
            baseScale = parent.baseScale;
            ResetState(parent.Multiplier);
            Damage = parent.Damage;
            if (splitContext.Target != null && splitContext.IsEnemy == false)
                PushOutOfSurface(splitContext.Point, splitContext.Normal);

            for (int i = 0; i < parent.cards.Count; i++)
            {
                if (i == splitContext.Slot) continue;
                cards.Add(parent.cards[i]);
                counts.Add(parent.counts[i]);
            }

            hitTargets.UnionWith(parent.hitTargets);
            BuildTimingSlots();
            RunTiming(CardTiming.Shoot, FlightContext(), shootStartIndex);
        }

        // 이전 발사에서 남은 상태를 모두 지웁니다. (풀에서 재사용될 때 상태가 섞이지 않도록)
        private void ResetState(CardMultiplier multiplier)
        {
            Multiplier = multiplier;
            transform.localScale = baseScale * multiplier.Size;

            cards.Clear();
            counts.Clear();
            hitTargets.Clear();
            age = 0f;
            isDead = false;
            keepFlying = false;
            directionLocked = false;
        }

        private void BuildTimingSlots()
        {
            foreach (List<int> slots in timingSlots) slots.Clear();
            for (int i = 0; i < cards.Count; i++)
                timingSlots[(int)cards[i].Timing].Add(i);
        }

        private void Update()
        {
            if (isDead) return;

            age += Time.deltaTime;
            if (age >= lifeTime)
            {
                Die(FlightContext());
                return;
            }

            RunTiming(CardTiming.Flying, FlightContext(), 0);
            Move();
        }

        // 이번 프레임에 이동할 거리만큼 발사체 모양의 상자를 앞으로 쓸어서(BoxCast), 맞는 게 있으면 닿는 위치까지만 이동하고 충돌 처리를 합니다.
        // (Rigidbody 없이 Transform으로 움직이므로 충돌 콜백 대신 직접 검사합니다. 빠른 발사체가 벽을 뚫는 것도 막아줍니다)
        // - 상자 크기 = 메시 크기 × 현재 스케일 이라서, Size 배율로 커진 만큼 판정도 커집니다.
        // - BoxCast는 출발할 때 이미 겹쳐 있는 Collider는 감지하지 않습니다. 관통은 이 성질을 이용합니다.
        private void Move()
        {
            float distance = speed * Time.deltaTime;
            int contactMask = CombatSystem.Instance.EnemyMask | CombatSystem.Instance.EnvironmentMask;

            if (Physics.BoxCast(transform.position, HalfExtents, transform.forward, out RaycastHit hit,
                    transform.rotation, distance, contactMask))
            {
                // hit.point는 상자 모서리가 닿은 지점이라 중심이 아닙니다. 중심은 진행 방향으로 닿은 거리만큼만 옮깁니다.
                transform.position += transform.forward * hit.distance;
                OnContact(hit);
                return;
            }

            // Space.Self 기준 Vector3.forward = 발사체 자신의 +z 방향 입니다.
            transform.Translate(Vector3.forward * distance, Space.Self);
        }

        // 적중 → 충돌 시점 카드 실행 → (계속 날아가게 한 카드가 없으면) 소멸 순서로 처리합니다.
        // - 무엇과 부딪혔는지만 분류하고, 카드의 종류는 알지 못합니다.
        private void OnContact(RaycastHit hit)
        {
            ContactKind contactKind = CombatSystem.Instance.GetContactKind(hit.collider);
            if (contactKind == ContactKind.Ignore) return;

            bool isEnemy = contactKind == ContactKind.Enemy;
            if (isEnemy)
            {
                IDamageable target = CombatSystem.Instance.GetMonsterOrNull(hit.collider);
                // 이미 피해를 준 적의 다른 Collider에 닿은 경우: 피해, 카드 발동 없이 그대로 통과합니다.
                if (target != null && hitTargets.Add(target) == false)
                {
                    PassThrough();
                    return;
                }

                CombatSystem.Instance.SendDamage(Player.Player.LocalPlayer, hit.collider, Damage, hit.point);
            }

            Debug.Log($"[Projectile] 충돌: {hit.collider.name} ({contactKind})");

            CardContext context = new CardContext(this, hit.point, hit.normal, hit.collider, isEnemy);
            keepFlying = false;
            directionLocked = false;
            RunTiming(CardTiming.Hit, context, 0);

            if (keepFlying == false) Die(context);
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
            new CardContext(this, transform.position, transform.forward, null, false);

        public int GetCount(int slot) => counts[slot];
        public void SetCount(int slot, int value) => counts[slot] = value;

        // 이번 충돌에서 진행 방향을 정할 권리를 가져갑니다. 앞 카드가 이미 정했으면 false를 반환합니다.
        // 가져가면 이번 충돌 뒤에도 소멸하지 않고 계속 날아갑니다.
        public bool TryLockDirection()
        {
            if (directionLocked) return false;
            directionLocked = true;
            keepFlying = true;
            return true;
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
        public void SpawnSplit(CardContext context, Quaternion rotation)
        {
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
        }
    }
}
