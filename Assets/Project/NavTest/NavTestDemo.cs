using System.Collections.Generic;
using System.Diagnostics;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.InputSystem;
using Debug = UnityEngine.Debug;

namespace Study3D.FpsGame.NavTest
{
    // NavTestDemo
    // : 네비메시 베이크 결과를 "장애물 없는 볼록 영역" 단위로 묶어, 미리 계산한 표로 길을 찾는 테스트 입니다.
    // - 베이크: 자식 BoxCollider 들만 모아서 NavMeshBuilder 로 런타임 베이크 (씬의 다른 오브젝트는 포함하지 않음)
    // - 영역: 베이크된 삼각형을 볼록함이 유지되는 한 최대한 합칩니다. (NavGraph 참고)
    // - 표: 연결점별 "목표 영역까지 남은 거리" 를 베이크 시점에 다익스트라로 계산, 런타임엔 조회만 합니다.
    // - 시야 직진: 목표가 보이면(네비메시 시야 판정) 경로 무시하고 직진합니다.
    // - 인게임: 목표를 플레이어(Player.LocalPlayer)로, 몬스터를 씬의 Enemy 복제본으로 바꿀 수 있습니다.
    //           Enemy 는 자기 FSM 을 그대로 쓰고, 추격 이동만 이 길찾기로 처리합니다. Enemy 끼리는 서로 밀어냅니다.
    // - 단축키: F1 Funnel 다듬기, F2 시야 직진, F3 +10, F4 +100, F5 +1000, F6 +5000, F7 전체 삭제,
    //           F8 목표 이동(Goal 프리셋), F9 플레이어를 데모로 + 플레이어 추적, F10 생성할 몬스터 전환 (캡슐 / Enemy)
    public class NavTestDemo : MonoBehaviour
    {
        // 영역 병합 시 허용할 오목함 깊이 (반지름 대비). 반지름보다 작으면 영역 안 직선 이동이 실제 장애물에 닿지 않음
        private const float ConcaveToleranceRatio = 0.75f;

        [Header("베이크")]
        [SerializeField] private float agentRadius = 0.2f;
        [SerializeField] private float agentHeight = 1.8f;
        [SerializeField] private float maxSlope = 45f;
        [SerializeField] private float stepHeight = 0.3f;
        [Tooltip("윗면이 데모 바닥보다 이 높이 이상인 박스는 윗면을 걸을 수 없는 장애물로 베이크")]
        [SerializeField] private float notWalkableTopHeight = 1.5f;

        [Header("몬스터")]
        [SerializeField] private float moveSpeed = 3f;
        [Tooltip("표를 따라 연결점을 모아 Funnel 로 경로를 다듬기")]
        [SerializeField] private bool tableFunnel;
        [Tooltip("목표가 보이면(네비메시 시야 판정) 경로 무시하고 직진")]
        [SerializeField] private bool losChase = true;

        [Header("목표")]
        [Tooltip("플레이 중 씬 뷰에서 직접 옮겨도 됩니다")]
        [SerializeField] private Transform goal;
        [SerializeField] private Transform[] goalPresets;

        [Header("인게임")]
        [Tooltip("켜면 Goal 대신 플레이어 위치를 목표로 사용")]
        [SerializeField] private bool followPlayer;
        [Tooltip("F9 로 플레이어를 옮길 위치 (데모 기준)")]
        [SerializeField] private Vector3 playerSpawnOffset = new Vector3(0f, 0.1f, -12f);
        [Tooltip("켜면 캡슐 대신 Enemy 를 생성")]
        [SerializeField] private bool spawnEnemies;
        [Tooltip("복제할 Enemy. 비워두면 씬에서 찾아 씀")]
        [SerializeField] private Enemy.Enemy enemyTemplate;
        [Tooltip("Enemy 끼리 서로 밀어내기")]
        [SerializeField] private bool enemySeparation = true;
        [Tooltip("밀어낼 때 쓰는 Enemy 반지름. 0 이면 Enemy 콜라이더에서 계산")]
        [SerializeField] private float enemyRadius;

        [Header("기즈모")]
        [SerializeField] private bool drawRegions = true;
        [SerializeField] private bool drawPortals = true;
        [Tooltip("경로를 그릴 몬스터 수")]
        [SerializeField] private int drawPathCount = 20;

        private readonly NavGraph graph = new NavGraph();
        private readonly List<NavTestAgent> agents = new List<NavTestAgent>();
        private readonly List<Mesh> regionMeshes = new List<Mesh>();
        private readonly Dictionary<Color, Material> colorMaterials = new Dictionary<Color, Material>();
        private Material agentBaseMaterial;
        private NavMeshDataInstance navMeshInstance;
        private Transform agentRoot;
        private NavRegion goalRegion;
        private Vector3 goalPoint;
        private bool goalOffMesh;
        private int presetIndex;

        // Enemy 끼리 밀어내기용 공간 격자 (칸마다 연결 리스트, 매 프레임 재사용)
        private readonly List<NavTestAgent> enemyAgents = new List<NavTestAgent>();
        private readonly Dictionary<long, int> cellHead = new Dictionary<long, int>();
        private int[] cellNext = new int[0];
        private Vector3[] pushes = new Vector3[0];

        // 통계
        private float bakeMs;
        private float tableMs;
        private int lookupCount;
        private int lookupPerSec;
        private int losCount;
        private int losVisibleCount;
        private long losTicks;
        private int losPerSec;
        private float losAvgUs;
        private float losVisibleRate;
        private int pathCount;
        private long pathTicks;
        private int pathPerSec;
        private float pathAvgMs;
        private long tickTicks;
        private int tickFrames;
        private float tickAvgMs;
        private float statTimer;

        public NavGraph Graph => graph;
        public float MoveSpeed => moveSpeed;
        public bool TableFunnel => tableFunnel;
        public bool LosChase => losChase;
        public NavRegion GoalRegion => goalRegion;
        public Vector3 GoalPosition => goalPoint;

        private Transform PlayerTransform => Player.Player.LocalPlayer != null ? Player.Player.LocalPlayer.transform : null;

        private void Start()
        {
            agentRoot = new GameObject("Agents").transform;
            agentRoot.SetParent(transform, false);

            // Enemy 원본: 씬에 있는 것 중 하나 (원본은 건드리지 않고 그대로 둠)
            if (enemyTemplate == null) enemyTemplate = FindAnyObjectByType<Enemy.Enemy>();
            if (enemyTemplate != null && enemyRadius <= 0f) enemyRadius = CalcEnemyRadius(enemyTemplate);

            Bake();
        }

        private static float CalcEnemyRadius(Enemy.Enemy enemy)
        {
            Vector3 scale = enemy.transform.lossyScale;
            float s = Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.z));
            if (enemy.MainCollider is CapsuleCollider capsule) return capsule.radius * s;
            if (enemy.MainCollider != null) return Mathf.Max(enemy.MainCollider.bounds.extents.x, enemy.MainCollider.bounds.extents.z);
            return 0.5f;
        }

        private void OnDisable()
        {
            if (navMeshInstance.valid) navMeshInstance.Remove();
        }

        public void Bake()
        {
            if (navMeshInstance.valid) navMeshInstance.Remove();

            // 자식 BoxCollider 만 베이크 소스로 사용
            var sources = new List<NavMeshBuildSource>();
            Bounds bounds = default;
            bool hasBounds = false;
            foreach (var col in GetComponentsInChildren<BoxCollider>())
            {
                if (!col.enabled) continue;
                sources.Add(new NavMeshBuildSource
                {
                    shape = NavMeshBuildSourceShape.Box,
                    size = col.size,
                    transform = col.transform.localToWorldMatrix * Matrix4x4.Translate(col.center),
                    // 0 = Walkable, 1 = Not Walkable (기둥/벽 윗면에 고립된 영역이 생기지 않도록)
                    area = col.bounds.max.y - transform.position.y >= notWalkableTopHeight ? 1 : 0,
                });
                if (hasBounds) bounds.Encapsulate(col.bounds);
                else
                {
                    bounds = col.bounds;
                    hasBounds = true;
                }
            }
            if (!hasBounds)
            {
                Debug.LogWarning("[NavTest] 베이크할 BoxCollider 가 없습니다.");
                return;
            }
            bounds.Expand(new Vector3(1f, agentHeight * 2f, 1f));

            NavMeshBuildSettings settings = NavMesh.GetSettingsByIndex(0);
            settings.agentRadius = agentRadius;
            settings.agentHeight = agentHeight;
            settings.agentSlope = maxSlope;
            settings.agentClimb = stepHeight;
            settings.overrideVoxelSize = true;
            settings.voxelSize = agentRadius / 3f;
            settings.overrideTileSize = true;
            settings.tileSize = 1024;   // 데모 범위가 타일 하나에 들어가도록

            var sw = Stopwatch.StartNew();
            // 기준점을 범위의 모서리로 잡아야 데모 전체가 타일 하나에 들어감 (중앙으로 잡으면 중앙에서 타일이 4개로 갈림)
            Vector3 origin = bounds.min;
            var localBounds = new Bounds(bounds.center - origin, bounds.size);
            NavMeshData data = NavMeshBuilder.BuildNavMeshData(settings, sources, localBounds, origin, Quaternion.identity);
            navMeshInstance = NavMesh.AddNavMeshData(data, origin, Quaternion.identity);

            NavMeshTriangulation tri = NavMesh.CalculateTriangulation();
            graph.Build(tri, bounds, agentRadius * ConcaveToleranceRatio);
            bakeMs = (float)sw.Elapsed.TotalMilliseconds;

            sw.Restart();
            graph.BuildNextTable();
            tableMs = (float)sw.Elapsed.TotalMilliseconds;
            DumpTriangulation(tri, bounds);

            RebuildRegionMeshes();
            goalRegion = null;
            foreach (var agent in agents) Destroy(agent.gameObject);
            agents.Clear();

            Debug.Log($"[NavTest] 베이크 완료: 삼각형 {graph.TriangleCount}개 → 영역 {graph.regions.Count}개 ({bakeMs:F1}ms), " +
                      $"다음 연결점 표 {graph.TableBytes}바이트 ({tableMs:F2}ms)");
        }

        private void Update()
        {
            HandleKeys();
            UpdateGoal();
            if (graph.regions.Count == 0 || goalRegion == null) return;

            // 죽어서 사라진 Enemy 정리
            agents.RemoveAll(a => a == null);

            // 캡슐만 여기서 움직임 (Enemy 는 자기 FSM 의 추격 상태에서 Move 를 호출)
            long t0 = Stopwatch.GetTimestamp();
            float dt = Time.deltaTime;
            for (int i = 0; i < agents.Count; i++)
                if (!agents[i].IsEnemyDriven) agents[i].Tick(dt);
            tickTicks += Stopwatch.GetTimestamp() - t0;
            tickFrames++;

            statTimer += Time.unscaledDeltaTime;
            if (statTimer >= 1f)
            {
                pathPerSec = Mathf.RoundToInt(pathCount / statTimer);
                pathAvgMs = pathCount > 0 ? TicksToMs(pathTicks) / pathCount : 0f;
                tickAvgMs = tickFrames > 0 ? TicksToMs(tickTicks) / tickFrames : 0f;
                lookupPerSec = Mathf.RoundToInt(lookupCount / statTimer);
                losPerSec = Mathf.RoundToInt(losCount / statTimer);
                losAvgUs = losCount > 0 ? TicksToMs(losTicks) * 1000f / losCount : 0f;
                losVisibleRate = losCount > 0 ? (float)losVisibleCount / losCount : 0f;
                pathCount = 0;
                pathTicks = 0;
                lookupCount = 0;
                losCount = 0;
                losVisibleCount = 0;
                losTicks = 0;
                tickTicks = 0;
                tickFrames = 0;
                statTimer = 0f;
            }
        }

        // 목표 = 플레이어 또는 Goal. 그 아래 영역을 찾아 영역 높이로 맞춘 점을 목표 지점으로 사용
        private void UpdateGoal()
        {
            Transform source = followPlayer && PlayerTransform != null ? PlayerTransform : goal;
            if (source == null || graph.regions.Count == 0) return;

            Vector3 p = source.position;
            NavRegion found = graph.FindRegion(p, goalRegion, followPlayer ? 2f : 0.5f);   // 플레이어는 점프하므로 높이 여유를 크게
            goalOffMesh = found == null;
            if (found == null) return;

            goalRegion = found;
            goalPoint = new Vector3(p.x, found.HeightAt(p.x, p.z), p.z);
        }

        // Enemy 끼리 겹치면 서로 반씩 밀어냄 (Enemy 의 Update 이동이 끝난 뒤)
        private void LateUpdate()
        {
            if (!enemySeparation || enemyRadius <= 0f) return;

            enemyAgents.Clear();
            foreach (var a in agents)
                if (a != null && a.IsEnemyDriven) enemyAgents.Add(a);
            int n = enemyAgents.Count;
            if (n < 2) return;

            if (cellNext.Length < n)
            {
                cellNext = new int[n * 2];
                pushes = new Vector3[n * 2];
            }

            // 1. 격자에 넣기 (칸 크기 = 지름, 주변 3x3 칸만 보면 됨)
            float cellSize = enemyRadius * 2f;
            cellHead.Clear();
            for (int i = 0; i < n; i++)
            {
                long key = CellKey(enemyAgents[i].transform.position, cellSize);
                cellNext[i] = cellHead.TryGetValue(key, out int head) ? head : -1;
                cellHead[key] = i;
                pushes[i] = Vector3.zero;
            }

            // 2. 겹친 쌍 찾아서 밀어낼 양 누적
            float minDist = enemyRadius * 2f;
            for (int i = 0; i < n; i++)
            {
                Vector3 pi = enemyAgents[i].transform.position;
                int cx = Mathf.FloorToInt(pi.x / cellSize);
                int cz = Mathf.FloorToInt(pi.z / cellSize);
                for (int dz = -1; dz <= 1; dz++)
                {
                    for (int dx = -1; dx <= 1; dx++)
                    {
                        if (!cellHead.TryGetValue(PackCell(cx + dx, cz + dz), out int j)) continue;
                        for (; j >= 0; j = cellNext[j])
                        {
                            if (j <= i) continue;
                            Vector3 d = enemyAgents[j].transform.position - pi;
                            d.y = 0f;
                            float dist = d.magnitude;
                            if (dist >= minDist) continue;

                            // 완전히 같은 위치면 임의 방향으로
                            Vector3 dir = dist > 1e-4f ? d / dist : new Vector3(Mathf.Cos(i), 0f, Mathf.Sin(i));
                            Vector3 push = dir * ((minDist - dist) * 0.5f);
                            pushes[i] -= push;
                            pushes[j] += push;
                        }
                    }
                }
            }

            // 3. 적용 (네비메시 밖으로는 밀리지 않음)
            for (int i = 0; i < n; i++)
                if (pushes[i] != Vector3.zero) enemyAgents[i].Push(pushes[i]);
        }

        private static long CellKey(Vector3 p, float cellSize)
        {
            return PackCell(Mathf.FloorToInt(p.x / cellSize), Mathf.FloorToInt(p.z / cellSize));
        }

        private static long PackCell(int x, int z) => ((long)x << 32) | (uint)z;

        // Funnel 다듬기: 표를 따라 연결점을 모아 코너 목록 생성
        public bool RequestTablePath(Vector3 start, NavRegion startRegion, List<Vector3> corners)
        {
            long t0 = Stopwatch.GetTimestamp();
            bool ok = graph.BuildPathFromTable(start, startRegion, goalPoint, goalRegion, corners);
            pathTicks += Stopwatch.GetTimestamp() - t0;
            pathCount++;
            return ok;
        }

        public NavPortal LookupNextPortal(NavRegion from, NavRegion to, Vector3 pos, float maxRemain)
        {
            lookupCount++;
            return graph.ChooseNextPortal(from, to, pos, maxRemain);
        }

        // 내 위치에서 목표가 보이는지 (네비메시 시야 판정)
        public bool CheckLineOfSight(Vector3 pos, NavRegion region)
        {
            long t0 = Stopwatch.GetTimestamp();
            bool visible = graph.Raycast(pos, region, goalPoint, goalRegion);
            losTicks += Stopwatch.GetTimestamp() - t0;
            losCount++;
            if (visible) losVisibleCount++;
            return visible;
        }

        // 같은 색 몬스터끼리 머티리얼 하나를 공유 (수천 마리일 때 몬스터마다 머티리얼 사본이 생기지 않도록)
        public Material GetColorMaterial(Color color)
        {
            if (!colorMaterials.TryGetValue(color, out Material mat))
            {
                mat = new Material(agentBaseMaterial) { color = color };
                colorMaterials.Add(color, mat);
            }
            return mat;
        }

        public void ToggleTableFunnel()
        {
            tableFunnel = !tableFunnel;
            foreach (var agent in agents) agent.ResetState();
        }

        public void ToggleLosChase()
        {
            losChase = !losChase;
            foreach (var agent in agents) agent.ResetState();
        }

        public void SpawnRandom(int count)
        {
            if (graph.regions.Count == 0) return;

            float totalArea = 0f;
            foreach (var r in graph.regions) totalArea += r.area;

            for (int i = 0; i < count; i++)
            {
                // 넓은 영역일수록 많이 뽑히도록
                float pick = Random.value * totalArea;
                NavRegion region = graph.regions[graph.regions.Count - 1];
                foreach (var r in graph.regions)
                {
                    pick -= r.area;
                    if (pick <= 0f)
                    {
                        region = r;
                        break;
                    }
                }
                SpawnAgent(RandomPointIn(region));
            }
        }

        public void ClearAgents()
        {
            foreach (var agent in agents) Destroy(agent.gameObject);
            agents.Clear();
        }

        // Goal 을 다음 프리셋 위치로 옮기고, 목표를 Goal 로 (플레이어 추적 해제)
        public void NextGoal()
        {
            if (goalPresets == null || goalPresets.Length == 0) return;
            presetIndex = (presetIndex + 1) % goalPresets.Length;
            goal.position = goalPresets[presetIndex].position;
            followPlayer = false;
        }

        private void SpawnAgent(Vector3 pos)
        {
            if (spawnEnemies && enemyTemplate != null)
            {
                SpawnEnemy(pos);
                return;
            }

            var go = new GameObject($"Agent_{agents.Count}");
            go.transform.SetParent(agentRoot, false);
            go.transform.position = pos;

            // 캡슐 기본 크기: 높이 2, 반지름 0.5 → 높이 1, 반지름 agentRadius 로 축소. 콜라이더는 필요 없으므로 제거
            var body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            Destroy(body.GetComponent<Collider>());
            if (agentBaseMaterial == null) agentBaseMaterial = body.GetComponent<Renderer>().sharedMaterial;
            body.transform.SetParent(go.transform, false);
            body.transform.localPosition = new Vector3(0f, 0.5f, 0f);
            body.transform.localScale = new Vector3(agentRadius * 2f, 0.5f, agentRadius * 2f);

            var agent = go.AddComponent<NavTestAgent>();
            agent.Init(this, body.GetComponent<Renderer>());
            agents.Add(agent);
        }

        // 씬의 Enemy 를 복제해서 길찾기를 붙임. FSM(대기/추격/공격/피격/사망)은 Enemy 것을 그대로 사용
        private void SpawnEnemy(Vector3 pos)
        {
            Enemy.Enemy enemy = Instantiate(enemyTemplate, pos, Quaternion.Euler(0f, Random.Range(0f, 360f), 0f), agentRoot);
            enemy.name = $"Enemy_{agents.Count}";

            var agent = enemy.gameObject.AddComponent<NavTestAgent>();
            agent.InitAsEnemy(this, enemy);
            agents.Add(agent);
        }

        // 플레이어를 데모 맵으로 옮기고 목표를 플레이어로 바꿈
        public void MovePlayerToDemo()
        {
            Transform player = PlayerTransform;
            if (player == null)
            {
                Debug.LogWarning("[NavTest] 플레이어(Player.LocalPlayer)를 찾지 못했습니다.");
                return;
            }

            // CharacterController 가 켜져 있으면 위치를 직접 바꿔도 되돌아가므로 잠깐 끔
            var controller = player.GetComponent<CharacterController>();
            if (controller != null) controller.enabled = false;
            player.position = transform.position + playerSpawnOffset;
            if (controller != null) controller.enabled = true;

            followPlayer = true;
        }

        public void ToggleSpawnType()
        {
            spawnEnemies = !spawnEnemies;
            if (spawnEnemies && enemyTemplate == null) Debug.LogWarning("[NavTest] 씬에서 Enemy 를 찾지 못해 캡슐로 생성합니다.");
        }

        private static Vector3 RandomPointIn(NavRegion region)
        {
            // 볼록 다각형을 부채꼴 삼각형으로 나눠 면적 비율로 선택
            Vector3[] v = region.verts;
            float total = 0f;
            for (int i = 1; i < v.Length - 1; i++) total += Mathf.Abs(NavGraph.Cross(v[i] - v[0], v[i + 1] - v[0]));

            float pick = Random.value * total;
            int tri = v.Length - 2;
            for (int i = 1; i < v.Length - 1; i++)
            {
                pick -= Mathf.Abs(NavGraph.Cross(v[i] - v[0], v[i + 1] - v[0]));
                if (pick <= 0f)
                {
                    tri = i;
                    break;
                }
            }

            float r1 = Random.value, r2 = Random.value;
            if (r1 + r2 > 1f)
            {
                r1 = 1f - r1;
                r2 = 1f - r2;
            }
            Vector3 p = v[0] + (v[tri] - v[0]) * r1 + (v[tri + 1] - v[0]) * r2;
            p.y = region.HeightAt(p.x, p.z);
            return p;
        }

        private void HandleKeys()
        {
            var kb = Keyboard.current;
            if (kb == null) return;
            if (kb.f1Key.wasPressedThisFrame) ToggleTableFunnel();
            if (kb.f2Key.wasPressedThisFrame) ToggleLosChase();
            if (kb.f3Key.wasPressedThisFrame) SpawnRandom(10);
            if (kb.f4Key.wasPressedThisFrame) SpawnRandom(100);
            if (kb.f5Key.wasPressedThisFrame) SpawnRandom(1000);
            if (kb.f6Key.wasPressedThisFrame) SpawnRandom(5000);
            if (kb.f7Key.wasPressedThisFrame) ClearAgents();
            if (kb.f8Key.wasPressedThisFrame) NextGoal();
            if (kb.f9Key.wasPressedThisFrame) MovePlayerToDemo();
            if (kb.f10Key.wasPressedThisFrame) ToggleSpawnType();
        }

        private void OnGUI()
        {
            GUILayout.BeginArea(new Rect(10f, 10f, 440f, 470f), GUI.skin.box);
            GUILayout.Label("<b>NavTest</b>  다음 연결점 표 길찾기");
            GUILayout.Label($"영역 {graph.regions.Count}개 (삼각형 {graph.TriangleCount}개 병합) / 베이크 {bakeMs:F1}ms");
            GUILayout.Label($"표 {graph.TableBytes}바이트 / 생성 {tableMs:F2}ms");
            GUILayout.Label($"몬스터 {agents.Count}마리 / 캡슐 갱신 {tickAvgMs:F3}ms/프레임");
            GUILayout.Label($"표 조회 {lookupPerSec}회/초 / Funnel 경로 {pathPerSec}회/초, 1회 {pathAvgMs * 1000f:F2}us");
            GUILayout.Label($"시야 판정 {losPerSec}회/초, 1회 {losAvgUs:F2}us, 보임 {losVisibleRate * 100f:F0}%");
            GUILayout.Label($"Funnel 다듬기: {OnOff(tableFunnel)} (F1)   시야 직진: {OnOff(losChase)} (F2)");
            GUILayout.Label($"목표: {(followPlayer ? "<color=cyan>플레이어</color>" : "Goal 오브젝트")} (F8 Goal 프리셋 / F9 플레이어)");
            GUILayout.Label($"생성: {(spawnEnemies ? "<color=cyan>Enemy</color>" : "캡슐")} (F10) / Enemy 반지름 {enemyRadius:F2}, 밀어내기 {OnOff(enemySeparation)}");
            if (goalOffMesh) GUILayout.Label("<color=red>목표가 네비메시 밖에 있습니다</color>");

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Funnel (F1)")) ToggleTableFunnel();
            if (GUILayout.Button("시야 직진 (F2)")) ToggleLosChase();
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("+10 (F3)")) SpawnRandom(10);
            if (GUILayout.Button("+100 (F4)")) SpawnRandom(100);
            if (GUILayout.Button("+1000 (F5)")) SpawnRandom(1000);
            if (GUILayout.Button("+5000 (F6)")) SpawnRandom(5000);
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("전체 삭제 (F7)")) ClearAgents();
            if (GUILayout.Button("Goal 프리셋 (F8)")) NextGoal();
            if (GUILayout.Button("다시 베이크")) Bake();
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("플레이어를 데모로 (F9)")) MovePlayerToDemo();
            if (GUILayout.Button("캡슐/Enemy (F10)")) ToggleSpawnType();
            GUILayout.EndHorizontal();
            GUILayout.Label("캡슐 색: <color=orange>주황</color> = 표 따라 이동, <color=cyan>하늘</color> = 목표가 보여서 직진");
            GUILayout.EndArea();
        }

        private static string OnOff(bool on) => on ? "<color=lime>켜짐</color>" : "<color=orange>꺼짐</color>";

        private void RebuildRegionMeshes()
        {
            foreach (var mesh in regionMeshes) Destroy(mesh);
            regionMeshes.Clear();

            foreach (var region in graph.regions)
            {
                var verts = new Vector3[region.verts.Length];
                for (int i = 0; i < verts.Length; i++) verts[i] = region.verts[i] + Vector3.up * 0.03f;

                // 반시계(위에서 봄) 정점을 위쪽에서 보이도록 시계 방향 인덱스로
                var tris = new int[(verts.Length - 2) * 3];
                for (int i = 1; i < verts.Length - 1; i++)
                {
                    tris[(i - 1) * 3] = 0;
                    tris[(i - 1) * 3 + 1] = i + 1;
                    tris[(i - 1) * 3 + 2] = i;
                }

                var mesh = new Mesh { name = $"Region_{region.index}", vertices = verts, triangles = tris };
                mesh.RecalculateNormals();
                regionMeshes.Add(mesh);
            }
        }

        private void OnDrawGizmos()
        {
            if (graph.regions.Count == 0) return;

            if (drawRegions)
            {
                for (int r = 0; r < graph.regions.Count; r++)
                {
                    NavRegion region = graph.regions[r];
                    Color c = Color.HSVToRGB(r * 0.618f % 1f, 0.6f, 0.95f);
                    if (r < regionMeshes.Count)
                    {
                        Gizmos.color = new Color(c.r, c.g, c.b, 0.35f);
                        Gizmos.DrawMesh(regionMeshes[r]);
                    }
                    Gizmos.color = c;
                    Vector3 lift = Vector3.up * 0.05f;
                    for (int i = 0; i < region.verts.Length; i++)
                        Gizmos.DrawLine(region.verts[i] + lift, region.verts[(i + 1) % region.verts.Length] + lift);
                }
            }

            if (drawPortals)
            {
                Gizmos.color = Color.yellow;
                Vector3 lift = Vector3.up * 0.08f;
                foreach (var region in graph.regions)
                {
                    foreach (var portal in region.portals)
                    {
                        if (portal.from.index > portal.to.index) continue;   // 양방향 중복 제거
                        Gizmos.DrawLine(portal.a + lift, portal.b + lift);
                        Gizmos.DrawSphere(portal.Mid + lift, 0.06f);
                    }
                }
            }

            if (goalRegion != null)
            {
                Gizmos.color = Color.magenta;
                Gizmos.DrawWireSphere(GoalPosition + Vector3.up * 0.3f, 0.4f);
            }

            int pathCountToDraw = Mathf.Min(drawPathCount, agents.Count);
            for (int i = 0; i < pathCountToDraw; i++)
                if (agents[i] != null) agents[i].DrawGizmoPath();
        }

        // 분석용: 베이크 결과를 프로젝트 Temp/NavTestDump.txt 로 저장 (데모 원점 기준 좌표)
        private void DumpTriangulation(NavMeshTriangulation tri, Bounds bounds)
        {
            var sb = new System.Text.StringBuilder();
            Vector3 origin = transform.position;
            sb.AppendLine($"bounds {bounds.center.x - origin.x} {bounds.center.y - origin.y} {bounds.center.z - origin.z} {bounds.size.x} {bounds.size.y} {bounds.size.z}");
            sb.AppendLine($"v {tri.vertices.Length}");
            foreach (var v in tri.vertices) sb.AppendLine($"{v.x - origin.x:R} {v.y - origin.y:R} {v.z - origin.z:R}");
            sb.AppendLine($"i {tri.indices.Length}");
            for (int i = 0; i < tri.indices.Length; i += 3) sb.AppendLine($"{tri.indices[i]} {tri.indices[i + 1]} {tri.indices[i + 2]}");
            System.IO.File.WriteAllText(System.IO.Path.Combine(Application.dataPath, "../Temp/NavTestDump.txt"), sb.ToString());
        }

        private static float TicksToMs(long ticks) => ticks * 1000f / Stopwatch.Frequency;
    }
}
