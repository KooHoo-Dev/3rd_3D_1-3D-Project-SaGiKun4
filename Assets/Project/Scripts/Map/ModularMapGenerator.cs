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
    // : 방 원본들을 통로(Path)로 이어 붙여 큰 맵 하나를 랜덤하게 만듭니다.
    // - 순서: (시작방 →) 방 → 통로 → 방 → 통로 → ... → 방 (통로 원본이 없으면 방끼리 바로 붙임)
    //   시작방이 있으면 생성기 위치에 먼저 놓고, 그 출구(EndGateWay)에 통로 없이 첫 방을 바로 붙임. 플레이 중이면 플레이어를 시작방으로 옮김
    // - 통로는 잇기만 하고 열고 닫지 않음. 막는 건 양쪽 방의 차단막이 맡음
    // - 다음 조각의 StartGateWay 문이 앞 조각의 EndGateWay 문과 위치/회전이 똑같아지도록 배치합니다.
    //   (두 문은 같은 에셋, 같은 회전이라 정확히 한 장으로 겹침 → 뒤 조각의 입구 문은 꺼서 이중으로 그리지 않음)
    // - 겹치면 다른 후보를 고르고, 막히면 한 칸 되돌아가서 다시 고릅니다. (백트래킹)
    //   통로가 층을 오르내릴 수 있으므로 겹침은 높이까지 포함한 3D 범위로 판정합니다.
    // - 계산은 RoomLayout(숫자)만으로 끝내고, 확정된 배치만 실제로 생성합니다. (오브젝트 수가 많은 조각을 시험 삼아 만들지 않음)
    // - 에디터: 컴포넌트 메뉴(⋮)의 "생성"/"지우기" 로 플레이 없이 미리 볼 수 있습니다.
    // - 플레이 중 생성하면 맵 전체 범위(+ 여유 거리)를 발사체 경계로 넘깁니다. 밖으로 나간 발사체는 조용히 회수됩니다.
    public class ModularMapGenerator : MonoBehaviour
    {
        private const string ContainerName = "GeneratedRooms";

        private struct Placement
        {
            public bool isStart;
            public bool isPath;
            public int template;       // isPath 에 따라 pathTemplates / roomTemplates 의 번호
            public Vector3 position;   // 생성기 로컬
            public Quaternion rotation;
            public Vector3 min, max;   // 범위 (생성기 로컬)
            public Vector3 exitPosition;   // 출구 문 (생성기 로컬)
            public Quaternion exitRotation;
        }

        [Header("시작방")]
        [Tooltip("맨 처음에 놓을 시작방 원본. 출구(EndGateWay)만 있으면 됨. 비워두면 첫 방부터 시작")]
        [SerializeField] private GameObject startRoom;
        [Tooltip("플레이 중 생성하면 플레이어를 시작방 가운데로 옮김")]
        [SerializeField] private bool movePlayerToStart = true;

        [Header("방")]
        [Tooltip("방 원본 (프리팹 권장). 씬에 있는 방을 넣으면 생성할 때 원본은 꺼둡니다")]
        [SerializeField] private List<GameObject> roomTemplates = new List<GameObject>();
        [SerializeField] private int roomCount = 5;
        [Tooltip("방과 방 사이에 끼울 통로 원본. 여러 개면 랜덤, 비워두면 방끼리 바로 붙임")]
        [SerializeField] private List<GameObject> pathTemplates = new List<GameObject>();
        [Tooltip("0 이면 매번 랜덤")]
        [SerializeField] private int seed;

        [Header("연결")]
        [Tooltip("뒤 조각의 입구 문을 꺼서 앞 조각 출구 문과 이중으로 그려지지 않게 함")]
        [SerializeField] private bool hideDuplicateGate = true;
        [Tooltip("겹침 판정에서 봐주는 깊이. 범위가 이만큼은 겹쳐도 허용")]
        [SerializeField] private float overlapTolerance = 0.5f;
        [Tooltip("백트래킹 시도 상한. 넘으면 그때까지 가장 길게 이은 배치를 사용")]
        [SerializeField] private int maxSteps = 5000;

        [Header("발사체 경계")]
        [Tooltip("맵 전체 범위에서 이만큼 더 나간 발사체는 조용히 회수됩니다 (플레이 중 생성할 때만 적용)")]
        [SerializeField] private float projectileBoundsMargin = 5f;

        [Header("실행")]
        [SerializeField] private bool generateOnStart = true;

        private readonly List<RoomModule> rooms = new List<RoomModule>();
        public IReadOnlyList<RoomModule> Rooms => rooms;

        private RoomLayout startLayout;
        private RoomLayout[] roomLayouts;
        private RoomLayout[] pathLayouts;
        private System.Random random;
        private int steps;
        private readonly List<Placement> placed = new List<Placement>();
        private readonly List<Placement> best = new List<Placement>();
        private int bestRooms;

        private void Start()
        {
            if (generateOnStart) Generate();
        }

        [ContextMenu("생성")]
        public void Generate()
        {
            Clear();
            roomTemplates.RemoveAll(t => t == null);
            pathTemplates.RemoveAll(t => t == null);
            if (roomTemplates.Count == 0 || roomCount <= 0)
            {
                Debug.LogWarning("[ModularMap] 방 원본이 없습니다.", this);
                return;
            }

            roomLayouts = CalculateLayouts(roomTemplates);
            pathLayouts = CalculateLayouts(pathTemplates);

            int usedSeed = seed != 0 ? seed : System.Environment.TickCount;
            random = new System.Random(usedSeed);
            steps = 0;
            placed.Clear();
            best.Clear();
            bestRooms = 0;

            // 시작방은 생성기 위치에 먼저 놓고 고정 (뒤 조각이 모두 막혀도 시작방은 남음)
            if (startRoom != null)
            {
                startLayout = RoomModule.CalculateLayout(startRoom, false);
                Placement start = MakePlacement(false, -1, startLayout);
                start.isStart = true;
                placed.Add(start);
                best.Add(start);
            }

            if (!TryPlace(0))
                Debug.LogWarning($"[ModularMap] {roomCount}개를 겹치지 않게 잇지 못해서 {bestRooms}개만 배치합니다. (시도 {steps})", this);

            GameObject startObject = Build(best);
            if (startObject != null && movePlayerToStart && Application.isPlaying) MovePlayerToStart(startObject.transform);
            if (Application.isPlaying && best.Count > 0) ProjectileManager.SetWorldBounds(CalculateWorldBounds(best));
            Debug.Log($"[ModularMap] 방 {bestRooms}개, 조각 {best.Count}개 생성 (seed {usedSeed}, 시도 {steps})", this);
        }

        private static RoomLayout[] CalculateLayouts(List<GameObject> templates)
        {
            var layouts = new RoomLayout[templates.Count];
            for (int i = 0; i < templates.Count; i++) layouts[i] = RoomModule.CalculateLayout(templates[i]);
            return layouts;
        }

        // 배치된 모든 조각의 범위(생성기 로컬)를 월드 좌표로 바꿔 하나로 합치고, 여유 거리만큼 넓힙니다.
        private Bounds CalculateWorldBounds(List<Placement> placements)
        {
            var bounds = new Bounds();
            bool first = true;
            foreach (Placement placement in placements)
            {
                // 상자의 8개 꼭짓점 (생성기가 회전해 있어도 맞도록 꼭짓점마다 월드로 바꿉니다)
                for (int corner = 0; corner < 8; corner++)
                {
                    Vector3 local = new Vector3(
                        (corner & 1) == 0 ? placement.min.x : placement.max.x,
                        (corner & 2) == 0 ? placement.min.y : placement.max.y,
                        (corner & 4) == 0 ? placement.min.z : placement.max.z);
                    Vector3 world = transform.TransformPoint(local);

                    if (first) bounds = new Bounds(world, Vector3.zero);
                    else bounds.Encapsulate(world);
                    first = false;
                }
            }

            bounds.Expand(projectileBoundsMargin * 2f);
            return bounds;
        }

        [ContextMenu("지우기")]
        public void Clear()
        {
            rooms.Clear();
            Transform container = transform.Find(ContainerName);
            if (container == null) return;
#if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                Undo.DestroyObjectImmediate(container.gameObject);
                return;
            }
#endif
            Destroy(container.gameObject);
        }

        // 깊이(= 방 번호) 우선으로 한 칸씩 이어 붙이기
        // - 첫 방은 (시작방이 있으면 그 출구에) 바로 붙이고, 그 뒤로는 (통로, 방) 조합을 후보로 섞어서 통로 → 방 순서로 붙임
        private bool TryPlace(int depth)
        {
            if (depth == roomCount) return true;

            // 앞에 놓인 조각이 방이면 통로를 끼움. 시작방 바로 뒤에는 통로 없이 방을 붙임
            bool usePath = placed.Count > 0 && !placed[placed.Count - 1].isStart && pathLayouts.Length > 0;
            var candidates = new List<(int path, int room)>();
            for (int r = 0; r < roomLayouts.Length; r++)
            {
                if (!usePath) candidates.Add((-1, r));
                else for (int p = 0; p < pathLayouts.Length; p++) candidates.Add((p, r));
            }
            Shuffle(candidates);

            foreach (var (path, room) in candidates)
            {
                if (++steps > maxSteps) return false;

                int added = 0;
                if (path >= 0)
                {
                    // 통로는 앞 방과 붙어 있으므로 앞 방은 검사에서 뺌
                    Placement pathPlacement = MakePlacement(true, path, pathLayouts[path]);
                    if (Overlaps(pathPlacement, 1)) continue;
                    placed.Add(pathPlacement);
                    added++;
                }

                // 방은 바로 앞 조각(통로 또는 방)과 붙어 있으므로 그 조각만 뺌
                Placement roomPlacement = MakePlacement(false, room, roomLayouts[room]);
                if (Overlaps(roomPlacement, 1))
                {
                    placed.RemoveRange(placed.Count - added, added);
                    continue;
                }
                placed.Add(roomPlacement);
                added++;

                if (depth + 1 > bestRooms)
                {
                    bestRooms = depth + 1;
                    best.Clear();
                    best.AddRange(placed);
                }
                if (TryPlace(depth + 1)) return true;
                placed.RemoveRange(placed.Count - added, added);
            }
            return false;
        }

        // 지금까지 놓인 마지막 조각의 출구에 이어 붙이는 배치 계산
        private Placement MakePlacement(bool isPath, int template, RoomLayout layout)
        {
            var p = new Placement { isPath = isPath, template = template };

            if (placed.Count == 0)
            {
                // 첫 방은 생성기 위치에 그대로
                p.rotation = Quaternion.identity;
                p.position = Vector3.zero;
            }
            else
            {
                // 조각 회전 * 입구 문 회전 = 앞 조각 출구 문 회전 → 조각 회전 = 출구 문 회전 * 입구 문 회전의 역
                Placement prev = placed[placed.Count - 1];
                Quaternion rotation = prev.exitRotation * Quaternion.Inverse(layout.entry.rotation);
                // 문끼리 정확히 겹치도록 Y 회전만 남기고 90도 단위로 맞춤 (곱셈 오차가 쌓이지 않게)
                p.rotation = Quaternion.Euler(0f, Mathf.Round(rotation.eulerAngles.y / 90f) * 90f, 0f);
                p.position = prev.exitPosition - p.rotation * layout.entry.position;
            }

            p.exitPosition = p.position + p.rotation * layout.exit.position;
            p.exitRotation = p.rotation * layout.exit.rotation;

            // 회전한 범위의 꼭짓점 8개로 다시 감싼 상자
            Bounds b = layout.bounds;
            p.min = Vector3.positiveInfinity;
            p.max = Vector3.negativeInfinity;
            for (int i = 0; i < 8; i++)
            {
                Vector3 corner = new Vector3(
                    (i & 1) == 0 ? b.min.x : b.max.x,
                    (i & 2) == 0 ? b.min.y : b.max.y,
                    (i & 4) == 0 ? b.min.z : b.max.z);
                Vector3 w = p.position + p.rotation * corner;
                p.min = Vector3.Min(p.min, w);
                p.max = Vector3.Max(p.max, w);
            }
            return p;
        }

        // ignoreLast: 끝에서부터 검사하지 않을 조각 수 (문 한 장을 사이에 두고 붙어 있는 조각)
        private bool Overlaps(Placement p, int ignoreLast)
        {
            float t = overlapTolerance;
            for (int i = 0; i < placed.Count - ignoreLast; i++)
            {
                Placement other = placed[i];
                if (p.min.x < other.max.x - t && p.max.x > other.min.x + t &&
                    p.min.y < other.max.y - t && p.max.y > other.min.y + t &&
                    p.min.z < other.max.z - t && p.max.z > other.min.z + t)
                    return true;
            }
            return false;
        }

        // 시작방을 만들었으면 그 오브젝트를 돌려줌
        private GameObject Build(List<Placement> plan)
        {
            GameObject startObject = null;
            bool hasStart = plan.Count > 0 && plan[0].isStart;
            var container = new GameObject(ContainerName).transform;
            container.SetParent(transform, false);
#if UNITY_EDITOR
            if (!Application.isPlaying) Undo.RegisterCreatedObjectUndo(container.gameObject, "Generate Modular Map");
#endif

            int roomIndex = 0, pathIndex = 0;
            for (int i = 0; i < plan.Count; i++)
            {
                Placement p = plan[i];
                GameObject template = p.isStart ? startRoom : p.isPath ? pathTemplates[p.template] : roomTemplates[p.template];
                GameObject go = Spawn(template, container);
                go.name = p.isStart ? $"{template.name}_Start" : p.isPath ? $"{template.name}_{pathIndex++:D2}" : $"{template.name}_{roomIndex:D2}";
                go.transform.localPosition = p.position;
                go.transform.localRotation = p.rotation;

                if (i > 0 && hideDuplicateGate)
                {
                    Transform entryDoor = RoomModule.FindTagged(go.transform, RoomModule.EntryTag);
                    if (entryDoor != null) entryDoor.gameObject.SetActive(false);
                }

                if (p.isStart)
                {
                    // 시작방에 RoomModule 이 있으면 출구만 다음 방과 이어짐 (EnterPoint 를 두지 않으면 전투 없음)
                    var startModule = go.GetComponent<RoomModule>();
                    if (startModule != null)
                    {
                        startModule.FindGates();
                        startModule.Entry.connected = false;
                        startModule.Exit.connected = bestRooms > 0;
                    }
                    startObject = go;
                }
                else if (!p.isPath)
                {
                    var module = go.GetComponent<RoomModule>();
                    if (module == null) module = go.AddComponent<RoomModule>();
                    module.FindGates();

                    // 앞과 이어진 입구, 뒤와 이어진 출구만 열림. 나머지는 막힌 채로 둠
                    module.Entry.connected = roomIndex > 0 || hasStart;
                    module.Exit.connected = roomIndex < bestRooms - 1;                    rooms.Add(module);
                    roomIndex++;
                }

                // 씬 원본이 꺼져 있었으면 복제본도 꺼진 채로 나오므로 설정을 끝낸 뒤 켬
                go.SetActive(true);
            }

            // 씬에 있는 원본은 생성된 맵과 겹치지 않게 끔
            if (startRoom != null) HideSceneTemplate(startRoom);
            foreach (var template in roomTemplates)
                HideSceneTemplate(template);
            foreach (var template in pathTemplates)
                HideSceneTemplate(template);

            return startObject;
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
            Quaternion rotation = toExit.sqrMagnitude > 0.0001f ? Quaternion.LookRotation(toExit) : start.rotation;

            player.Teleport(position, rotation);
        }

        private static void HideSceneTemplate(GameObject template)
        {
            if (!template.scene.IsValid() || !template.activeSelf) return;
#if UNITY_EDITOR
            if (!Application.isPlaying) Undo.RecordObject(template, "Hide Map Template");
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

        private void Shuffle<T>(List<T> list)
        {
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = random.Next(i + 1);
                (list[i], list[j]) = (list[j], list[i]);
            }
        }

        private void OnDrawGizmosSelected()
        {
            // 마지막 생성 결과의 범위(방 = 하늘색, 통로 = 주황)와 연결 지점
            Gizmos.matrix = transform.localToWorldMatrix;
            foreach (var p in best)
            {
                Gizmos.color = p.isPath ? new Color(1f, 0.5f, 0f) : Color.cyan;
                Gizmos.DrawWireCube((p.min + p.max) * 0.5f, p.max - p.min);
                Gizmos.color = Color.red;
                Gizmos.DrawSphere(p.exitPosition, 0.5f);
            }
        }
    }
}
