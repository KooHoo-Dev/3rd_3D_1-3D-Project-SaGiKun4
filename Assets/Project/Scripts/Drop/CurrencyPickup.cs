using Study3D.FpsGame.Player;
using UnityEngine;

namespace Study3D.FpsGame.Drop
{
    // CurrencyPickup
    // : 몬스터가 죽을 때 흩뿌려지는 골드 하나 입니다. 몬스터는 프리팹과 개수만 정하고 Scatter로 흩뿌립니다.
    // - 개당 값, 흩뿌림, 흡수 세부값은 이 프리팹에 한 번만 둡니다. (모든 몬스터가 같은 값을 씀)
    // - 흩뿌림: 착지점을 먼저 정하고, 몬스터 중앙에서 그 자리에 정확히 닿도록 포물선으로 날아갑니다.
    //   1. 벽 검사: 몸 중앙에서 고른 방향으로 수평 레이를 쏴서, 막히면 벽 앞(wallPadding)까지 당깁니다.
    //   2. 땅 찾기: 그 위치의 몸 중앙 높이에서 아래로 레이를 쏴서 실제 땅 높이를 얻습니다. (경사로, 턱, 프롭 위)
    //      몸 중앙에서 쏘므로 천장 위를 찾지 않습니다. 못 찾으면(맵 밖 등) 죽은 발밑 높이를 씁니다.
    //   (레이는 CombatSystem의 환경 레이어만 봅니다. CombatSystem이 없으면 검사 없이 발밑 높이를 씁니다)
    //   비행 중에는 충돌 검사를 하지 않습니다.
    // - 대기: 착지 후 pickupDelay가 지나면 플레이어가 흡수 반경 안에 들어오길 기다립니다. (공중에서는 흡수되지 않음)
    // - 흡수: 점점 빨라지며 플레이어 몸 중앙으로 날아가고, 닿으면 골드를 더하고 풀로 돌아갑니다.
    // - Update 대신 CurrencyPickupManager가 Tick을 호출합니다.
    // - 풀(키: 프리팹 이름)로 관리합니다. 처음 흩뿌릴 때 풀이 없으면 만듭니다.
    // - 프리팹에는 Collider를 두지 않습니다. (플레이어 이동이나 카드를 막지 않도록)
    public class CurrencyPickup : MonoBehaviour, IObjectPoolItem
    {
        private enum State
        {
            Flying,
            Resting,
            Attracting,
        }

        [Header("값")]
        [SerializeField] private int minValue = 100; // 하나를 먹을 때 오르는 골드 (개마다 랜덤)
        [SerializeField] private int maxValue = 200;
        [SerializeField] private byte poolExpandSize = 20;

        [Header("흩뿌림")]
        [SerializeField] private float minDistance = 1f;   // 몸 중앙에서 착지점까지 수평 거리
        [SerializeField] private float maxDistance = 2.5f;
        [SerializeField] private float minFlightTime = 0.45f; // 착지까지 걸리는 시간 (높이 차와 상관없이 이 시간에 닿음)
        [SerializeField] private float maxFlightTime = 0.65f;
        [SerializeField] private float gravity = 20f;
        [SerializeField] private float restHeight = 0.15f;    // 땅에서 띄우는 높이 (스피어 반지름)
        [SerializeField] private float wallPadding = 0.3f;    // 벽에 막히면 벽에서 이만큼 앞에 떨어집니다
        [SerializeField] private float groundProbeDepth = 5f; // 땅을 찾을 때 몸 중앙에서 아래로 내려볼 거리

        [Header("흡수")]
        [SerializeField] private float pickupDelay = 0.3f;        // 착지 후 이 시간이 지나야 흡수됩니다
        [SerializeField] private float attractRadius = 3f;        // 플레이어 몸 중앙이 이 거리 안에 오면 빨려 들어가기 시작
        [SerializeField] private float attractStartSpeed = 4f;
        [SerializeField] private float attractAcceleration = 30f; // 빨려 들어가는 동안 초당 늘어나는 속도
        [SerializeField] private float collectDistance = 0.3f;    // 플레이어 몸 중앙과 이만큼 가까워지면 획득

        // 풀에서 꺼낸 골드는 풀이 Key를 넣어줍니다. Instantiate로 만든 골드는 null 입니다.
        public string Key { get; set; }
        public GameObject GameObject => gameObject;

        // CurrencyPickupManager 목록 안의 위치. 목록에 없으면 -1
        public int ManagerIndex { get; set; } = -1;

        private int value;
        private State state;
        private float time; // 지금 상태에 들어온 뒤 지난 시간

        private Vector3 start;
        private Vector3 landing;
        private Vector3 launchVelocity;
        private float flightTime;
        private float attractSpeed;

        // center(몬스터 몸 중앙)에서 prefab을 count개 흩뿌립니다. floorY는 땅을 못 찾았을 때 쓸 높이(죽은 발밑) 입니다.
        public static void Scatter(CurrencyPickup prefab, int count, Vector3 center, float floorY)
        {
            if (prefab == null || count <= 0)
                return;

            string poolKey = PreparePool(prefab);
            int mask = CombatSystem.Instance != null ? CombatSystem.Instance.EnvironmentMask : 0;
            for (int i = 0; i < count; i++)
            {
                Vector3 landing = prefab.FindLanding(center, floorY, mask);
                CurrencyPickup pickup = Spawn(poolKey, prefab, center);
                pickup.Launch(landing);
            }
        }

        // 풀이 없으면 만들고 키를 돌려줍니다. 풀 매니저가 없으면 null (Instantiate로 만듭니다)
        private static string PreparePool(CurrencyPickup prefab)
        {
            ObjectPoolManager pool = ObjectPoolManager.Instance;
            if (pool == null)
                return null;

            string key = prefab.name;
            if (pool.HasPool(key) == false)
                pool.CreatePool(new ObjectPoolManager.ObjectPoolData(key, prefab.gameObject, prefab.poolExpandSize));
            return key;
        }

        // 풀이 있으면 풀에서 꺼내고, 없으면 prefab을 Instantiate 합니다.
        // 풀에서 꺼낸 골드는 꺼진 상태라서, 위치를 먼저 잡은 뒤 켭니다.
        private static CurrencyPickup Spawn(string poolKey, CurrencyPickup prefab, Vector3 position)
        {
            ObjectPoolManager pool = ObjectPoolManager.Instance;
            if (pool == null || string.IsNullOrEmpty(poolKey) || pool.HasPool(poolKey) == false)
                return Instantiate(prefab, position, Quaternion.identity);

            CurrencyPickup pooled = pool.GetObjectOrNull(poolKey) as CurrencyPickup;
            if (pooled == null)
                return Instantiate(prefab, position, Quaternion.identity);

            pooled.transform.position = position;
            pooled.gameObject.SetActive(true);
            return pooled;
        }

        // 랜덤 방향·거리로 고른 자리를 벽 검사, 땅 찾기로 다듬은 착지점 (골드 중심 위치)
        private Vector3 FindLanding(Vector3 center, float floorY, int mask)
        {
            float angle = Random.Range(0f, Mathf.PI * 2f);
            Vector3 direction = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
            float distance = Random.Range(minDistance, maxDistance);

            RaycastHit hit;
            if (mask != 0 && Physics.Raycast(center, direction, out hit, distance + wallPadding, mask, QueryTriggerInteraction.Ignore))
                distance = Mathf.Max(0f, hit.distance - wallPadding);

            Vector3 above = center + direction * distance;
            float groundY = floorY;
            if (mask != 0 && Physics.Raycast(above, Vector3.down, out hit, groundProbeDepth, mask, QueryTriggerInteraction.Ignore))
                groundY = hit.point.y;

            return new Vector3(above.x, groundY + restHeight, above.z);
        }

        // 지금 위치에서 landingPoint까지 포물선으로 날아가게 하고, 개당 값을 정합니다.
        private void Launch(Vector3 landingPoint)
        {
            value = Random.Range(minValue, Mathf.Max(minValue, maxValue) + 1);
            start = transform.position;
            landing = landingPoint;
            flightTime = Mathf.Max(0.01f, Random.Range(minFlightTime, maxFlightTime));

            // flightTime초 뒤에 정확히 landing에 닿는 처음 속도: 수평은 거리 ÷ 시간, 수직은 그동안 중력으로 줄어들 몫(½gT)을 더해줍니다.
            launchVelocity = (landing - start) / flightTime + Vector3.up * (0.5f * gravity * flightTime);

            state = State.Flying;
            time = 0f;
            CurrencyPickupManager.Register(this);
        }

        public void Tick(float deltaTime)
        {
            time += deltaTime;
            switch (state)
            {
                case State.Flying:
                    TickFlying();
                    break;
                case State.Resting:
                    TickResting();
                    break;
                case State.Attracting:
                    TickAttracting(deltaTime);
                    break;
            }
        }

        // 속도를 매 프레임 누적하지 않고 지난 시간으로 바로 위치를 구해서, 프레임이 흔들려도 착지점에서 어긋나지 않습니다.
        private void TickFlying()
        {
            if (time >= flightTime)
            {
                transform.position = landing;
                state = State.Resting;
                time = 0f;
                return;
            }

            transform.position = start + launchVelocity * time + Vector3.down * (0.5f * gravity * time * time);
        }

        private void TickResting()
        {
            if (time < pickupDelay)
                return;

            LocalPlayer player = Player.Player.LocalPlayer;
            if (player == null || player.IsDead)
                return;

            if ((PlayerCenter(player) - transform.position).sqrMagnitude > attractRadius * attractRadius)
                return;

            state = State.Attracting;
            attractSpeed = attractStartSpeed;
        }

        // 플레이어가 사라지거나 죽으면 그 자리에서 다시 기다립니다.
        private void TickAttracting(float deltaTime)
        {
            LocalPlayer player = Player.Player.LocalPlayer;
            if (player == null || player.IsDead)
            {
                state = State.Resting;
                return;
            }

            attractSpeed += attractAcceleration * deltaTime;
            float step = attractSpeed * deltaTime;
            Vector3 toPlayer = PlayerCenter(player) - transform.position;
            float distance = toPlayer.magnitude;

            // 이번 프레임에 닿을 수 있으면 바로 획득 (빨라진 뒤 플레이어를 지나쳐 버리지 않도록)
            if (distance <= collectDistance + step)
            {
                Collect(player);
                return;
            }

            transform.position += toPlayer * (step / distance);
        }

        private void Collect(LocalPlayer player)
        {
            player.AddCurrency(value);
            CurrencyPickupManager.Unregister(this);
            ReturnToPool();
        }

        private static Vector3 PlayerCenter(LocalPlayer player)
        {
            return player.MainCollider.bounds.center;
        }

        // 풀로 돌아가거나 파괴될 때 매니저 목록에서 빠집니다.
        private void OnDisable()
        {
            CurrencyPickupManager.Unregister(this);
        }

        // 풀에서 꺼낸 골드는 풀로 돌려보내고, Instantiate로 만든 골드는 Destroy 합니다.
        public void ReturnToPool()
        {
            if (Key != null && ObjectPoolManager.Instance != null)
            {
                ObjectPoolManager.Instance.ReturnToPool(this);
            }
            else
            {
                Destroy(gameObject);
            }
        }
    }
}
