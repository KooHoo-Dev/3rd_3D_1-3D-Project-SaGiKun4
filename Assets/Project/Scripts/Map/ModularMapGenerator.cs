using System.Collections.Generic;
using Study3D.FpsGame.Player;
using Study3D.FpsGame.Weapon.Cards;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Study3D.FpsGame.Map
{
    // ModularMapGenerator
    // : 크기별 방 목록에서 진행도에 맞는 방을 꺼내, 통로(Path)로 이어 붙여 큰 맵 하나를 만듭니다.
    // - 방 순서 (6개): [S, S, S/M, M, M/L, L]
    //   '/' 슬롯은 50:50 으로 둘 중 하나 → Small 2~3개, Medium 1~2개, Large 1~2개. 모든 크기가 최소 한 번씩 나옴
    //   (초반 = 적응·자원 수집, 중반 = 조합 테스트·재료 수집, 후반 = 완성된 조합 시험)
    // - 연결: (시작방 →) 방 → 통로 → 방 → 통로 → ... → 방 (통로 원본이 없으면 방끼리 바로 붙임)
    //   시작방이 있으면 생성기 위치에 먼저 놓고, 그 출구(EndGateWay)에 통로 없이 첫 방을 바로 붙임. 플레이 중이면 플레이어를 시작방으로 옮김
    // - 통로는 잇기만 하고 열고 닫지 않음. 막는 건 양쪽 방의 차단막이 맡음
    // - 다음 조각의 StartGateWay 문이 앞 조각의 EndGateWay 문과 위치/회전이 똑같아지도록 배치합니다.
    //   (두 문은 같은 에셋, 같은 회전이라 정확히 한 장으로 겹침 → 뒤 조각의 입구 문은 꺼서 이중으로 그리지 않음)
    // - 방끼리 겹쳐도 거르지 않고 그대로 생성합니다. 겹침은 방 제작 오류이므로 눈으로 보고 고쳐야 함
    // - 에디터: 컴포넌트 메뉴(⋮)의 "생성"/"지우기" 로 플레이 없이 미리 볼 수 있습니다.
    // - 플레이 중 생성하면 맵 전체 범위(+ 여유 거리)를 발사체 경계로 넘깁니다. 밖으로 나간 발사체는 조용히 회수됩니다.
    public class ModularMapGenerator : MonoBehaviour
    {
        private const string ContainerName = "GeneratedRooms";

        private enum RoomSize { Small, Medium, Large }

        // 슬롯 하나에 올 수 있는 크기 두 개
        private struct Slot
        {
            public RoomSize first;
            public RoomSize second;

            public Slot(RoomSize first, RoomSize second)
            {
                this.first = first;
                this.second = second;
            }
        }

        // 슬롯별 크기. 두 번째 값이 다르면 50:50 으로 둘 중 하나
        private static readonly Slot[] Slots =
        {
            new Slot(RoomSize.Small, RoomSize.Small),
            new Slot(RoomSize.Small, RoomSize.Small),
            new Slot(RoomSize.Small, RoomSize.Medium),
            new Slot(RoomSize.Medium, RoomSize.Medium),
            new Slot(RoomSize.Medium, RoomSize.Large),
            new Slot(RoomSize.Large, RoomSize.Large),
        };

        private struct Placement
        {
            public bool isStart;
            public bool isPath;
            public GameObject template;
            public Vector3 position;   // 생성기 로컬
            public Quaternion rotation;
            public Vector3 min;        // 범위 (생성기 로컬)
            public Vector3 max;
            public Vector3 exitPosition;   // 출구 문 (생성기 로컬)
            public Quaternion exitRotation;
        }

        [Header("시작방")]
        [Tooltip("맨 처음에 놓을 시작방 원본. 출구(EndGateWay)만 있으면 됨. 비워두면 첫 방부터 시작")]
        [SerializeField] private GameObject startRoom;
        [Tooltip("플레이 중 생성하면 플레이어를 시작방 가운데로 옮김")]
        [SerializeField] private bool movePlayerToStart = true;

        [Header("방 (크기별)")]
        [SerializeField] private List<GameObject> smallRooms = new List<GameObject>();
        [SerializeField] private List<GameObject> mediumRooms = new List<GameObject>();
        [SerializeField] private List<GameObject> largeRooms = new List<GameObject>();
        [Tooltip("방과 방 사이에 끼울 통로 원본. 여러 개면 랜덤, 비워두면 방끼리 바로 붙임")]
        [SerializeField] private List<GameObject> pathTemplates = new List<GameObject>();
        [Tooltip("0 이면 매번 랜덤")]
        [SerializeField] private int seed;

        [Header("프로토타입 (임시)")]
        [Tooltip("하나라도 들어 있으면 크기 규칙을 무시하고 이 목록의 방을 순서대로 모두 붙임")]
        [SerializeField] private List<GameObject> testRooms = new List<GameObject>();

        [Header("연결")]
        [Tooltip("뒤 조각의 입구 문을 꺼서 앞 조각 출구 문과 이중으로 그려지지 않게 함")]
        [SerializeField] private bool hideDuplicateGate = true;

        [Header("발사체 경계")]
        [Tooltip("맵 전체 범위에서 이만큼 더 나간 발사체는 조용히 회수됩니다 (플레이 중 생성할 때만 적용)")]
        [SerializeField] private float projectileBoundsMargin = 5f;

        [Header("실행")]
        [SerializeField] private bool generateOnStart = true;

        private readonly List<RoomModule> rooms = new List<RoomModule>();
        public IReadOnlyList<RoomModule> Rooms => rooms;

        private RoomLayout startLayout;
        private readonly Dictionary<GameObject, RoomLayout> layoutCache = new Dictionary<GameObject, RoomLayout>();
        private readonly List<Placement> placed = new List<Placement>();
        private int roomTotal;

        private void Start()
        {
            if (generateOnStart)
            {
                Generate();
            }
        }

        [ContextMenu("생성")]
        public void Generate()
        {
            Clear();

            int usedSeed;
            if (seed != 0)
            {
                usedSeed = seed;
            }
            else
            {
                usedSeed = System.Environment.TickCount;
            }
            System.Random random = new System.Random(usedSeed);

            List<GameObject> order;
            if (testRooms.Count > 0)
            {
                order = testRooms;
            }
            else
            {
                order = PickRooms(random);
            }
            if (order == null)
                return;

            layoutCache.Clear();
            placed.Clear();
            roomTotal = order.Count;

            PlanPlacements(order, random);

            GameObject startObject = Build(placed);
            if (startObject != null && movePlayerToStart && Application.isPlaying)
            {
                MovePlayerToStart(startObject.transform);
            }
            if (Application.isPlaying && placed.Count > 0)
            {
                ProjectileManager.SetWorldBounds(CalculateWorldBounds(placed));
            }
            Debug.Log($"[ModularMap] 방 {roomTotal}개, 조각 {placed.Count}개 생성 (seed {usedSeed})", this);
        }

        // (시작방 →) 방 → 통로 → 방 ... 순서로 배치를 계산해서 placed 에 쌓음
        private void PlanPlacements(List<GameObject> order, System.Random random)
        {
            // 시작방은 생성기 위치에 먼저 놓음
            if (startRoom != null)
            {
                startLayout = RoomModule.CalculateLayout(startRoom, false);
                Placement start = MakePlacement(false, startRoom, startLayout);
                start.isStart = true;
                placed.Add(start);
            }

            for (int i = 0; i < order.Count; i++)
            {
                // 앞 조각이 방이면 통로를 끼움. 시작방 바로 뒤에는 통로 없이 방을 붙임
                if (i > 0 && pathTemplates.Count > 0)
                {
                    GameObject path = pathTemplates[random.Next(pathTemplates.Count)];
                    placed.Add(MakePlacement(true, path, GetLayout(path)));
                }
                placed.Add(MakePlacement(false, order[i], GetLayout(order[i])));
            }
        }

        // 슬롯 순서대로 크기를 정하고, 그 크기 목록에서 방을 하나씩 랜덤으로 꺼냄
        private List<GameObject> PickRooms(System.Random random)
        {
            if (smallRooms.Count == 0 || mediumRooms.Count == 0 || largeRooms.Count == 0)
            {
                Debug.LogError("[ModularMap] Small / Medium / Large 방 목록에 각각 하나 이상 넣어 주세요.", this);
                return null;
            }

            List<GameObject> order = new List<GameObject>();
            foreach (Slot slot in Slots)
            {
                RoomSize size;
                if (random.Next(2) == 0)
                {
                    size = slot.first;
                }
                else
                {
                    size = slot.second;
                }

                List<GameObject> list = GetRoomList(size);
                order.Add(list[random.Next(list.Count)]);
            }
            return order;
        }

        private List<GameObject> GetRoomList(RoomSize size)
        {
            switch (size)
            {
                case RoomSize.Small:
                    return smallRooms;
                case RoomSize.Medium:
                    return mediumRooms;
                default:
                    return largeRooms;
            }
        }

        private RoomLayout GetLayout(GameObject template)
        {
            if (!layoutCache.TryGetValue(template, out RoomLayout layout))
            {
                layout = RoomModule.CalculateLayout(template);
                layoutCache.Add(template, layout);
            }
            return layout;
        }

        // 배치된 모든 조각의 범위(생성기 로컬)를 월드 좌표로 바꿔 하나로 합치고, 여유 거리만큼 넓힙니다.
        private Bounds CalculateWorldBounds(List<Placement> placements)
        {
            Bounds bounds = new Bounds();
            bool first = true;
            foreach (Placement placement in placements)
            {
                // 상자의 8개 꼭짓점 (생성기가 회전해 있어도 맞도록 꼭짓점마다 월드로 바꿉니다)
                for (int corner = 0; corner < 8; corner++)
                {
                    Vector3 local = BoxCorner(placement.min, placement.max, corner);
                    Vector3 world = transform.TransformPoint(local);

                    if (first)
                    {
                        bounds = new Bounds(world, Vector3.zero);
                    }
                    else
                    {
                        bounds.Encapsulate(world);
                    }
                    first = false;
                }
            }

            bounds.Expand(projectileBoundsMargin * 2f);
            return bounds;
        }

        // 상자의 꼭짓점 8개 중 index(0~7) 번째
        // - index 를 2진수로 보면 1의 자리 = x, 2의 자리 = y, 4의 자리 = z 가 max 쪽인지
        private static Vector3 BoxCorner(Vector3 min, Vector3 max, int index)
        {
            bool maxX = index % 2 == 1;
            bool maxY = (index / 2) % 2 == 1;
            bool maxZ = (index / 4) % 2 == 1;

            Vector3 corner = min;
            if (maxX)
            {
                corner.x = max.x;
            }
            if (maxY)
            {
                corner.y = max.y;
            }
            if (maxZ)
            {
                corner.z = max.z;
            }
            return corner;
        }

        [ContextMenu("지우기")]
        public void Clear()
        {
            rooms.Clear();
            Transform container = transform.Find(ContainerName);
            if (container == null)
                return;
#if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                Undo.DestroyObjectImmediate(container.gameObject);
                return;
            }
#endif
            Destroy(container.gameObject);
        }

        // 지금까지 놓인 마지막 조각의 출구에 이어 붙이는 배치 계산
        private Placement MakePlacement(bool isPath, GameObject template, RoomLayout layout)
        {
            Placement placement = new Placement { isPath = isPath, template = template };

            if (placed.Count == 0)
            {
                // 첫 조각은 생성기 위치에 그대로
                placement.rotation = Quaternion.identity;
                placement.position = Vector3.zero;
            }
            else
            {
                // 조각 회전 * 입구 문 회전 = 앞 조각 출구 문 회전 → 조각 회전 = 출구 문 회전 * 입구 문 회전의 역
                Placement prev = placed[placed.Count - 1];
                Quaternion rotation = prev.exitRotation * Quaternion.Inverse(layout.entry.rotation);
                // 문끼리 정확히 겹치도록 Y 회전만 남기고 90도 단위로 맞춤 (곱셈 오차가 쌓이지 않게)
                placement.rotation = Quaternion.Euler(0f, Mathf.Round(rotation.eulerAngles.y / 90f) * 90f, 0f);
                placement.position = prev.exitPosition - placement.rotation * layout.entry.position;
            }

            placement.exitPosition = placement.position + placement.rotation * layout.exit.position;
            placement.exitRotation = placement.rotation * layout.exit.rotation;

            // 회전한 범위의 꼭짓점 8개로 다시 감싼 상자 (발사체 경계, 기즈모용)
            Bounds layoutBounds = layout.bounds;
            placement.min = Vector3.positiveInfinity;
            placement.max = Vector3.negativeInfinity;
            for (int i = 0; i < 8; i++)
            {
                Vector3 corner = BoxCorner(layoutBounds.min, layoutBounds.max, i);
                Vector3 world = placement.position + placement.rotation * corner;
                placement.min = Vector3.Min(placement.min, world);
                placement.max = Vector3.Max(placement.max, world);
            }
            return placement;
        }

        // 시작방을 만들었으면 그 오브젝트를 돌려줌
        private GameObject Build(List<Placement> plan)
        {
            GameObject startObject = null;
            bool hasStart = plan.Count > 0 && plan[0].isStart;
            Transform container = new GameObject(ContainerName).transform;
            container.SetParent(transform, false);
#if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                Undo.RegisterCreatedObjectUndo(container.gameObject, "Generate Modular Map");
            }
#endif

            int roomIndex = 0;
            int pathIndex = 0;
            for (int i = 0; i < plan.Count; i++)
            {
                Placement placement = plan[i];
                GameObject go = Spawn(placement.template, container);
                if (placement.isStart)
                {
                    go.name = $"{placement.template.name}_Start";
                }
                else if (placement.isPath)
                {
                    go.name = $"{placement.template.name}_{pathIndex:D2}";
                    pathIndex++;
                }
                else
                {
                    go.name = $"{placement.template.name}_{roomIndex:D2}";
                }
                go.transform.localPosition = placement.position;
                go.transform.localRotation = placement.rotation;

                if (i > 0 && hideDuplicateGate)
                {
                    Transform entryDoor = RoomModule.FindTagged(go.transform, RoomModule.EntryTag);
                    if (entryDoor != null)
                    {
                        entryDoor.gameObject.SetActive(false);
                    }
                }

                if (placement.isStart)
                {
                    SetupStartRoom(go);
                    startObject = go;
                }
                else if (!placement.isPath)
                {
                    SetupRoom(go, roomIndex, hasStart);
                    roomIndex++;
                }

                // 씬 원본이 꺼져 있었으면 복제본도 꺼진 채로 나오므로 설정을 끝낸 뒤 켬
                go.SetActive(true);
            }

            // 씬에 있는 원본은 생성된 맵과 겹치지 않게 끔
            foreach (Placement placement in plan)
            {
                HideSceneTemplate(placement.template);
            }

            return startObject;
        }

        private void SetupStartRoom(GameObject go)
        {
            // 시작방에 RoomModule 이 있으면 출구만 다음 방과 이어짐 (EnterPoint 를 두지 않으면 전투 없음)
            RoomModule startModule = go.GetComponent<RoomModule>();
            if (startModule != null)
            {
                startModule.FindGates();
                startModule.Entry.connected = false;
                startModule.Exit.connected = roomTotal > 0;
            }
        }

        private void SetupRoom(GameObject go, int roomIndex, bool hasStart)
        {
            RoomModule module = go.GetComponent<RoomModule>();
            if (module == null)
            {
                module = go.AddComponent<RoomModule>();
            }
            module.FindGates();

            // 앞과 이어진 입구, 뒤와 이어진 출구만 열림. 나머지는 막힌 채로 둠
            module.Entry.connected = roomIndex > 0 || hasStart;
            module.Exit.connected = roomIndex < roomTotal - 1;
            rooms.Add(module);
        }

        // 플레이어를 시작방 가운데(바닥 높이)로 옮기고 출구 쪽을 보게 함
        // TODO: 시작방을 만들면 방 안에 플레이어 시작 위치를 따로 두고 그 위치를 사용
        private void MovePlayerToStart(Transform start)
        {
            LocalPlayer player = Player.Player.LocalPlayer;
            if (player == null)
            {
                Debug.LogWarning("[ModularMap] 옮길 플레이어가 없습니다.", this);
                return;
            }

            Vector3 center = startLayout.bounds.center;
            center.y = startLayout.exit.position.y;
            Vector3 position = start.TransformPoint(center);

            Vector3 toExit = start.TransformPoint(startLayout.exit.center) - position;
            toExit.y = 0f;

            Quaternion rotation;
            if (toExit.sqrMagnitude > 0.0001f)
            {
                rotation = Quaternion.LookRotation(toExit);
            }
            else
            {
                rotation = start.rotation;
            }

            player.Teleport(position, rotation);
        }

        private static void HideSceneTemplate(GameObject template)
        {
            if (!template.scene.IsValid() || !template.activeSelf)
                return;
#if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                Undo.RecordObject(template, "Hide Map Template");
            }
#endif
            template.SetActive(false);
        }

        private static GameObject Spawn(GameObject template, Transform parent)
        {
#if UNITY_EDITOR
            // 에디터 미리보기에서 프리팹 원본이면 연결을 유지한 채로 생성 (원본 수정이 바로 반영됨)
            if (!Application.isPlaying && PrefabUtility.IsPartOfPrefabAsset(template))
                return (GameObject)PrefabUtility.InstantiatePrefab(template, parent);
#endif
            return Instantiate(template, parent);
        }

        private void OnDrawGizmosSelected()
        {
            // 마지막 생성 결과의 범위(방 = 하늘색, 통로 = 주황)와 연결 지점
            Gizmos.matrix = transform.localToWorldMatrix;
            foreach (Placement placement in placed)
            {
                if (placement.isPath)
                {
                    Gizmos.color = new Color(1f, 0.5f, 0f);
                }
                else
                {
                    Gizmos.color = Color.cyan;
                }
                Gizmos.DrawWireCube((placement.min + placement.max) * 0.5f, placement.max - placement.min);
                Gizmos.color = Color.red;
                Gizmos.DrawSphere(placement.exitPosition, 0.5f);
            }
        }
    }
}
