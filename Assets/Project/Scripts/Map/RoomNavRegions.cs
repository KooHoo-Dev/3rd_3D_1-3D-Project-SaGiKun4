using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using Unity.AI.Navigation;
using UnityEngine;
using UnityEngine.AI;
#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
#endif
using Debug = UnityEngine.Debug;

namespace Study3D.FpsGame.Map
{
    // RoomNavRegions
    // : 방에 구워 둔 네비메시 삼각형들을 "장애물 없는 볼록 영역" 으로 합칩니다. (방 루트에 붙임)
    // - 0. 삼각형 꺼내기: 다른 방 네비메시를 잠깐 내리고 이 방 NavMeshSurface 네비메시만 올려서 꺼냄
    //      (안전장치로 방 범위 안에 있는 삼각형만 사용)
    // - 1. 용접: 같은 위치의 꼭짓점에 같은 번호를 붙임 (번호로 공유 변을 찾기 위해)
    //      + 높이 보정: 꼭짓점 높이를 바닥 콜라이더 표면으로 바꿈 (네비메시 높이는 근사라서)
    // - 2. 이웃 찾기: 두 조각에 똑같이 나오는 변 = 공유 변 = 합칠 후보
    // - 3. 합쳐 보기: 긴 공유 변부터 두 조각을 이어 붙임
    // - 4. 볼록 검사: 합친 다각형을 반시계로 돌며 모든 꺾임이 왼쪽(CCW)이어야 확정
    //      + 평면 검사: 모든 꼭짓점이 한 평면 위여야 확정 (평지와 경사로를 합치지 않음. 영역 = 평면 하나로 높이 계산)
    // - 5. 반복: 한 바퀴 동안 아무것도 안 합쳐질 때까지
    // - 연결점: 다른 두 영역의 변이 같은 직선 위에서 겹치고 높이도 같은 구간 = 맞닿은 경계 (위아래 층의 겹친 변은 제외)
    //   (번호가 아니라 위치로 비교해서, 한쪽에만 꼭짓점이 있는 T자 접합도 찾음)
    //   같은 두 영역 사이 조각은 하나로 합치고, 방향별(A→B, B→A)로 저장
    // - 남은 거리 표: 표[연결점 p][목표 영역 T] = p 를 넘은 뒤 T 에 들어가기까지 남은 거리
    //   목표 영역마다 다익스트라를 거꾸로(T 로 들어가는 연결점 = 0 에서 바깥으로) 한 번씩 돌려서 채움
    // - 결과 좌표는 방 루트 로컬 기준입니다. (에셋으로 저장해 회전/이동된 방에서도 그대로 쓰기 위해)
    // - 사용법: 프리팹 모드에서 NavMeshSurface 를 베이크 → 컴포넌트 메뉴(⋮)의 "영역 생성" 으로 확인 → "지도 저장"
    //   저장한 지도(RoomNavMap) 는 방이 켜질 때 불러옵니다.
    public class RoomNavRegions : MonoBehaviour
    {
        // 방향이 있는 연결점. from 영역에서 to 영역으로 a~b 선분을 지나 넘어갑니다.
        [Serializable]
        public struct Portal
        {
            public int from;
            public int to;
            public Vector3 a;   // 방 로컬 좌표
            public Vector3 b;
        }

        private const float WeldPrecision = 100f;   // 0.01 단위로 반올림해서 같으면 같은 꼭짓점
        private const float ConvexEpsilon = 1e-4f;  // 이보다 작은 음수 외적은 일직선으로 보고 허용 (계산 오차)
        private const float MinOverlap = 0.05f;     // 겹친 길이가 이보다 짧으면 모서리끼리 점으로 닿은 것으로 보고 무시
        private const float PlaneTolerance = 0.15f; // 합친 영역의 꼭짓점이 영역 평면에서 이 높이 안이어야 합침 (평지와 경사로를 합치지 않음)

        [Tooltip("저장된 지도. \"지도 저장\" 을 누르면 만들어지고 연결됩니다")]
        [SerializeField] private RoomNavMap navMap;

        [Header("생성")]
        [Tooltip("방 범위를 이만큼 넓혀서 삼각형을 고릅니다 (바닥 가장자리가 잘리지 않도록)")]
        [SerializeField] private float boundsMargin = 0.5f;
        [Tooltip("변의 양 끝이 다른 변의 직선에서 이 거리 안이면 같은 직선 위로 보고 연결점을 찾습니다 (m)")]
        [SerializeField] private float lineTolerance = 0.01f;
        [Tooltip("겹친 구간 양 끝에서 두 변의 높이 차가 이 값 안이어야 연결점입니다. 위아래 층의 겹친 변을 거름 (m)")]
        [SerializeField] private float portalHeightTolerance = 0.3f;
        [Tooltip("꼭짓점 높이를 맞출 바닥 콜라이더 레이어. 네비메시 높이는 근사라서 생성할 때 실제 바닥 높이로 바꿈")]
        [SerializeField] private LayerMask groundMask = ~0;
        [Tooltip("꼭짓점 위아래로 이 거리 안에서 바닥 콜라이더를 찾습니다 (m). 못 찾으면 네비메시 높이 그대로")]
        [SerializeField] private float groundSnapDistance = 0.5f;

        [Header("목표")]
        [Tooltip("플레이어가 지도 밖이어도 가장 가까운 영역 경계가 이 거리 안이면 그 경계 위의 점을 목표로 씁니다 (m)\n" +
                 "네비메시는 벽에서 베이크 반지름만큼 깎여 있으므로, 벽에 붙은 플레이어를 찾으려면 베이크 반지름보다 조금 크게")]
        [SerializeField] private float goalSnapDistance = 0.7f;

        // 방 로컬 좌표의 꼭짓점들과, 영역마다 반시계 순서의 꼭짓점 번호 목록
        private readonly List<Vector3> points = new List<Vector3>();
        private readonly List<List<int>> regions = new List<List<int>>();

        // 방향별 연결점과, 영역마다 "내 영역에서 나가는" 연결점 번호 목록 (regions 와 같은 순서)
        private readonly List<Portal> portals = new List<Portal>();
        private readonly List<List<int>> regionPortals = new List<List<int>>();

        // 남은 거리 표: portalDist[연결점 번호 * 영역 수 + 목표 영역] (갈 수 없으면 float.MaxValue)
        private float[] portalDist;

        // 영역마다 바닥 평면 y = a·x + b·z + c 의 (a, b, c). 꼭짓점에서 계산하므로 저장하지 않고 생성/불러오기 때마다 만듦
        private readonly List<Vector3> regionPlanes = new List<Vector3>();

        // 위 데이터가 이미 채워졌는지 (생성, 불러오기, 지우기 중 하나를 했으면 true. 아니면 처음 쓸 때 에셋에서 불러옴)
        [NonSerialized] private bool isReady;

        public RoomNavMap NavMap => navMap;
        public RoomCrowd Crowd { get; } = new RoomCrowd(); // 이 방에 있는 몬스터끼리 밀어내기
        public int TriangleCount { get; private set; }
        public int RegionCount => regions.Count;
        public int PortalCount => portals.Count / 2; // 방향 두 개 = 연결점 하나
        public int TableBytes => portalDist == null ? 0 : portalDist.Length * sizeof(float);

        private void Awake()
        {
            EnsureReady();
        }

        // 아직 아무 데이터도 없으면 저장된 지도에서 불러옵니다.
        private void EnsureReady()
        {
            if (isReady) return;
            isReady = true;
            if (navMap != null) Load(navMap);
        }

        [ContextMenu("영역 생성")]
        public void Build()
        {
            isReady = true;
            ResetGoal();
            var sw = Stopwatch.StartNew();

            List<List<int>> polys = CollectTriangles();
            TriangleCount = polys.Count;
            MergePolygons(polys);

            regions.Clear();
            foreach (List<int> poly in polys)
                if (poly != null) regions.Add(poly);

            BuildPlanes();
            BuildPortals();
            float regionMs = (float)sw.Elapsed.TotalMilliseconds;

            sw.Restart();
            BuildNextTable();
            float tableMs = (float)sw.Elapsed.TotalMilliseconds;

            Debug.Log($"[RoomNavRegions] {name}: 삼각형 {TriangleCount}개 → 영역 {regions.Count}개, " +
                      $"연결점 {PortalCount}개 ({regionMs:F1}ms), 남은 거리 표 {TableBytes}바이트 ({tableMs:F1}ms)");
        }

        // 지금 보이는 결과만 지웁니다. 저장된 지도 에셋은 그대로 둡니다.
        [ContextMenu("영역 지우기")]
        public void Clear()
        {
            isReady = true;
            ResetGoal();
            points.Clear();
            regions.Clear();
            portals.Clear();
            regionPortals.Clear();
            regionPlanes.Clear();
            portalDist = null;
            TriangleCount = 0;
        }

        #region 0~1. 삼각형 꺼내기, 용접

        private List<List<int>> CollectTriangles()
        {
            points.Clear();
            var polys = new List<List<int>>();

            if (TryCalculateOwnTriangulation(out NavMeshTriangulation tri) == false || tri.indices.Length == 0)
            {
                Debug.LogWarning($"[RoomNavRegions] {name}: 네비메시가 없습니다. NavMeshSurface 를 먼저 베이크하세요.");
                return polys;
            }

            Bounds bounds = CalculateRoomBounds();

            // 1. 용접: 위치(0.01 단위)가 같으면 같은 번호
            var weldMap = new Dictionary<Vector3Int, int>();
            int[] remap = new int[tri.vertices.Length];
            for (int i = 0; i < tri.vertices.Length; i++)
            {
                Vector3 local = transform.InverseTransformPoint(tri.vertices[i]);
                Vector3Int key = Vector3Int.RoundToInt(local * WeldPrecision);
                if (weldMap.TryGetValue(key, out int id) == false)
                {
                    id = points.Count;
                    points.Add(local);
                    weldMap.Add(key, id);
                }
                remap[i] = id;
            }

            SnapPointsToGround();

            // 0. 방 범위 안의 삼각형만, 위에서 봤을 때 반시계 순서로
            for (int i = 0; i < tri.indices.Length; i += 3)
            {
                int a = remap[tri.indices[i]];
                int b = remap[tri.indices[i + 1]];
                int c = remap[tri.indices[i + 2]];
                if (a == b || b == c || a == c) continue; // 용접으로 점이 겹쳐 납작해진 삼각형

                Vector3 centroid = transform.TransformPoint((points[a] + points[b] + points[c]) / 3f);
                if (bounds.Contains(centroid) == false) continue;

                float cross = Cross(points[b] - points[a], points[c] - points[a]);
                if (Mathf.Abs(cross) < ConvexEpsilon) continue; // 넓이가 거의 없는 삼각형
                polys.Add(cross > 0f ? new List<int> { a, b, c } : new List<int> { a, c, b });
            }

            return polys;
        }

        // 꼭짓점 높이를 바닥 콜라이더 표면으로 맞춥니다.
        // - 네비메시는 복셀로 구워서 높이가 들쭉날쭉하고 바닥보다 조금 떠 있음 → 그대로 평면을 만들면 경사로에서 발이 박히거나 뜸
        // - 꼭짓점 위에서 아래로 레이를 쏴서 처음 맞은 면(= 가장 위 바닥)의 높이를 씀. 위층 바닥은 거리 밖이라 안 맞음
        // - 프리팹 모드는 따로 물리 씬을 쓰므로 이 방이 속한 씬의 물리 씬으로 쏨
        private void SnapPointsToGround()
        {
            PhysicsScene physics = gameObject.scene.GetPhysicsScene();
            Physics.SyncTransforms();

            int missed = 0;
            for (int i = 0; i < points.Count; i++)
            {
                Vector3 world = transform.TransformPoint(points[i]);
                Vector3 origin = world + Vector3.up * groundSnapDistance;
                if (physics.Raycast(origin, Vector3.down, out RaycastHit hit, groundSnapDistance * 2f, groundMask, QueryTriggerInteraction.Ignore))
                    points[i] = transform.InverseTransformPoint(new Vector3(world.x, hit.point.y, world.z));
                else
                    missed++;
            }

            if (missed > 0)
                Debug.LogWarning($"[RoomNavRegions] {name}: 꼭짓점 {missed}/{points.Count}개는 바닥 콜라이더를 못 찾아 네비메시 높이를 그대로 씁니다.");
        }

        // 이 방의 NavMeshSurface 네비메시만 올라와 있는 상태에서 삼각형을 꺼냅니다.
        // - CalculateTriangulation 은 지금 올라와 있는 모든 네비메시를 한 덩어리로 주기 때문에,
        //   켜져 있는 다른 NavMeshSurface 의 네비메시를 잠깐 내리고, 이 방 것만 직접 올린 뒤 꺼내고, 원래대로 돌려놓습니다.
        // - 프리팹 모드의 NavMeshSurface 는 스스로 네비메시를 올리지 않아서, 직접 올려야 프리팹 모드에서도 읽을 수 있습니다.
        private bool TryCalculateOwnTriangulation(out NavMeshTriangulation tri)
        {
            tri = default;
            NavMeshSurface surface = GetComponentInChildren<NavMeshSurface>(true);
            if (surface == null || surface.navMeshData == null) return false;

            var others = new List<NavMeshSurface>(NavMeshSurface.activeSurfaces);
            foreach (NavMeshSurface other in others) other.RemoveData();

            NavMeshDataInstance own = NavMesh.AddNavMeshData(surface.navMeshData, surface.transform.position, surface.transform.rotation);
            try
            {
                tri = NavMesh.CalculateTriangulation();
            }
            finally
            {
                own.Remove();
                foreach (NavMeshSurface other in others) other.AddData();
            }
            return true;
        }

        // 방의 렌더러들을 모두 감싸는 월드 범위 (방은 90도 단위로만 회전하므로 월드 축 상자로 충분)
        private Bounds CalculateRoomBounds()
        {
            Renderer[] renderers = GetComponentsInChildren<Renderer>();
            Bounds bounds = new Bounds(transform.position, Vector3.zero);
            for (int i = 0; i < renderers.Length; i++)
            {
                if (i == 0) bounds = renderers[i].bounds;
                else bounds.Encapsulate(renderers[i].bounds);
            }
            bounds.Expand(boundsMargin * 2f);
            return bounds;
        }

        #endregion

        #region 2~5. 합치기

        private void MergePolygons(List<List<int>> polys)
        {
            var edgeOwner = new Dictionary<long, int>(); // 변 → 그 변을 처음 가진 조각 번호
            var candidates = new List<(int pa, int pb, int u, int v, float len)>();
            var touched = new HashSet<int>();            // 이번 바퀴에 이미 합쳐진 조각

            // 5. 한 바퀴 동안 하나도 안 합쳐질 때까지 반복
            bool mergedAny = true;
            while (mergedAny)
            {
                mergedAny = false;
                edgeOwner.Clear();
                candidates.Clear();
                touched.Clear();

                // 2. 같은 변이 두 번째로 나오면 앞의 조각과 공유하는 변
                for (int p = 0; p < polys.Count; p++)
                {
                    List<int> poly = polys[p];
                    if (poly == null) continue;

                    for (int i = 0; i < poly.Count; i++)
                    {
                        int u = poly[i];
                        int v = poly[(i + 1) % poly.Count];
                        long key = EdgeKey(u, v);
                        if (edgeOwner.TryGetValue(key, out int other))
                            candidates.Add((other, p, u, v, (points[u] - points[v]).sqrMagnitude));
                        else
                            edgeOwner[key] = p;
                    }
                }

                // 3. 긴 변부터 (긴 변을 먼저 지워야 큰 영역이 생김)
                candidates.Sort((x, y) => y.len.CompareTo(x.len));

                foreach (var c in candidates)
                {
                    // 이번 바퀴에 이미 모양이 바뀐 조각은 변 정보가 낡았으므로 다음 바퀴에
                    if (touched.Contains(c.pa) || touched.Contains(c.pb)) continue;

                    List<int> merged = TryMerge(polys[c.pa], polys[c.pb], c.u, c.v);
                    if (merged == null) continue;

                    polys[c.pa] = merged;
                    polys[c.pb] = null;
                    touched.Add(c.pa);
                    touched.Add(c.pb);
                    mergedAny = true;
                }
            }
        }

        // 공유 변 (u, v) 를 지우고 두 조각을 이어 붙입니다. 볼록하지 않으면 null.
        private List<int> TryMerge(List<int> a, List<int> b, int u, int v)
        {
            // 둘 다 반시계라서 공유 변이 a 에서는 x→y, b 에서는 y→x 순서로 나옴
            int ia = FindEdge(a, u, v);
            if (ia < 0) return null;
            int x = a[ia];
            int y = a[(ia + 1) % a.Count];

            int jb = FindEdge(b, y, x);
            if (jb < 0 || b[jb] != y) return null;

            // a 를 y 부터 한 바퀴 돌아 x 까지, 이어서 b 를 x 다음부터 y 앞까지
            var merged = new List<int>(a.Count + b.Count - 2);
            for (int k = 0; k < a.Count; k++) merged.Add(a[(ia + 1 + k) % a.Count]);
            for (int k = 2; k < b.Count; k++) merged.Add(b[(jb + k) % b.Count]);

            // 두 조각이 변을 두 개 이상 공유하면 같은 꼭짓점이 두 번 나옴 → 꼬인 모양이라 합치지 않음
            if (new HashSet<int>(merged).Count != merged.Count) return null;

            return IsConvex(merged) && IsFlat(merged) ? merged : null;
        }

        // 평면 검사: 모든 꼭짓점이 다각형 평면에서 PlaneTolerance 높이 안 (평지와 경사로, 기울기가 다른 경사로끼리는 합치지 않음)
        // - 영역 하나를 평면 하나로 보고 높이를 계산하므로, 평면에서 벗어난 영역이 생기면 안 됨
        private bool IsFlat(List<int> poly)
        {
            Vector3 plane = CalculatePlane(poly);
            foreach (int idx in poly)
            {
                Vector3 p = points[idx];
                if (Mathf.Abs(p.y - PlaneHeight(plane, p)) > PlaneTolerance) return false;
            }
            return true;
        }

        // 4. 볼록 검사: 반시계로 돌면서 연속한 세 점의 꺾임이 모두 왼쪽(외적 >= 0)
        // - 0 (일직선) 은 허용합니다. 합치다 보면 변 위에 꼭짓점이 남는 경우가 있어서입니다.
        private bool IsConvex(List<int> poly)
        {
            int n = poly.Count;
            for (int i = 0; i < n; i++)
            {
                Vector3 prev = points[poly[(i + n - 1) % n]];
                Vector3 cur = points[poly[i]];
                Vector3 next = points[poly[(i + 1) % n]];
                if (Cross(cur - prev, next - cur) < -ConvexEpsilon) return false;
            }
            return true;
        }

        private static int FindEdge(List<int> poly, int u, int v)
        {
            for (int i = 0; i < poly.Count; i++)
            {
                int p = poly[i];
                int q = poly[(i + 1) % poly.Count];
                if ((p == u && q == v) || (p == v && q == u)) return i;
            }
            return -1;
        }

        // 방향과 상관없이 같은 변이면 같은 키
        private static long EdgeKey(int u, int v)
        {
            int min = Mathf.Min(u, v);
            int max = Mathf.Max(u, v);
            return ((long)min << 32) | (uint)max;
        }

        // 위에서 본(XZ) 2D 외적. 양수면 v 가 u 의 왼쪽 (반시계로 꺾임)
        private static float Cross(Vector3 u, Vector3 v) => u.x * v.z - u.z * v.x;

        #endregion

        #region 영역 평면 (높이 계산)

        private void BuildPlanes()
        {
            regionPlanes.Clear();
            foreach (List<int> poly in regions) regionPlanes.Add(CalculatePlane(poly));
        }

        // 다각형의 평면 y = a·x + b·z + c 를 (a, b, c) 로 구합니다.
        // - 법선: 뉴웰 방식 (꼭짓점을 한 바퀴 돌며 이웃한 두 점으로 더함. 꼭짓점이 여러 개여도 안정적)
        // - 평면 위 한 점: 꼭짓점 평균
        // - 법선 n 인 평면 n·(p - p0) = 0 을 y 에 대해 풀면 y = p0.y - (n.x(x - p0.x) + n.z(z - p0.z)) / n.y
        private Vector3 CalculatePlane(List<int> poly)
        {
            Vector3 normal = Vector3.zero;
            Vector3 center = Vector3.zero;
            for (int i = 0; i < poly.Count; i++)
            {
                Vector3 cur = points[poly[i]];
                Vector3 next = points[poly[(i + 1) % poly.Count]];
                normal.x += (cur.y - next.y) * (cur.z + next.z);
                normal.y += (cur.z - next.z) * (cur.x + next.x);
                normal.z += (cur.x - next.x) * (cur.y + next.y);
                center += cur;
            }
            center /= poly.Count;

            // 거의 세로인 면(걸을 수 없음)은 평평한 바닥으로 취급
            if (Mathf.Abs(normal.y) < 1e-6f) return new Vector3(0f, 0f, center.y);

            float a = -normal.x / normal.y;
            float b = -normal.z / normal.y;
            return new Vector3(a, b, center.y - a * center.x - b * center.z);
        }

        private static float PlaneHeight(Vector3 plane, Vector3 p) => plane.x * p.x + plane.y * p.z + plane.z;

        // 영역 바닥에서 p 와 같은 XZ 위치의 높이 (방 로컬)
        public float HeightAt(int region, Vector3 localPos)
        {
            EnsureReady();
            return PlaneHeight(regionPlanes[region], localPos);
        }

        #endregion

        #region 연결점

        private void BuildPortals()
        {
            portals.Clear();
            regionPortals.Clear();
            for (int r = 0; r < regions.Count; r++) regionPortals.Add(new List<int>());

            // 1. 모든 변을 "어느 영역의 변인지 + 양 끝 좌표" 로 모으기
            var edges = new List<(int region, Vector3 a, Vector3 b)>();
            for (int r = 0; r < regions.Count; r++)
            {
                List<int> poly = regions[r];
                for (int i = 0; i < poly.Count; i++)
                    edges.Add((r, points[poly[i]], points[poly[(i + 1) % poly.Count]]));
            }

            // 2. 다른 영역의 변 두 개씩 비교해서, 같은 직선 위에서 겹치는 구간을 그 두 영역의 조각으로 모으기
            var pairPieces = new Dictionary<long, List<Vector3>>();       // 영역 쌍 → 겹친 구간들의 양 끝 좌표
            var pairOrder = new List<long>();                             // 쌍을 찾은 순서 (결과 순서를 매번 같게)
            for (int i = 0; i < edges.Count; i++)
            {
                for (int j = i + 1; j < edges.Count; j++)
                {
                    if (edges[i].region == edges[j].region) continue;
                    if (TryGetOverlap(edges[i].a, edges[i].b, edges[j].a, edges[j].b, out Vector3 from, out Vector3 to) == false) continue;

                    long pair = EdgeKey(edges[i].region, edges[j].region);
                    if (pairPieces.TryGetValue(pair, out List<Vector3> pieces) == false)
                    {
                        pieces = new List<Vector3>();
                        pairPieces.Add(pair, pieces);
                        pairOrder.Add(pair);
                    }
                    pieces.Add(from);
                    pieces.Add(to);
                }
            }

            // 3~4. 같은 쌍의 조각들을 선분 하나로 합치고, 두 방향으로 저장
            foreach (long pair in pairOrder)
            {
                int r0 = (int)(pair >> 32);
                int r1 = (int)(pair & 0xffffffff);
                GetOuterEnds(pairPieces[pair], out Vector3 a, out Vector3 b);

                regionPortals[r0].Add(portals.Count);
                portals.Add(new Portal { from = r0, to = r1, a = a, b = b });
                regionPortals[r1].Add(portals.Count);
                portals.Add(new Portal { from = r1, to = r0, a = a, b = b });
            }
        }

        // 2. 변 1(a1~b1) 을 자로 놓고, 변 2(a2~b2) 가 같은 직선 위에서 겹치는 구간을 구합니다. (위에서 본 XZ 기준)
        private bool TryGetOverlap(Vector3 a1, Vector3 b1, Vector3 a2, Vector3 b2, out Vector3 from, out Vector3 to)
        {
            from = to = Vector3.zero;

            Vector3 dir = b1 - a1;
            float length = Mathf.Sqrt(DotXZ(dir, dir));
            if (length < MinOverlap) return false;

            // 같은 직선 위인지: 변 2의 양 끝이 변 1의 직선에서 떨어진 거리 (외적 / 길이)
            if (Mathf.Abs(Cross(dir, a2 - a1)) / length > lineTolerance) return false;
            if (Mathf.Abs(Cross(dir, b2 - a1)) / length > lineTolerance) return false;

            // 자의 눈금: 변 1의 시작 = 0, 끝 = length. 변 2의 양 끝이 몇 눈금에 오는지 (내적으로 투영)
            float ta = DotXZ(a2 - a1, dir) / length;
            float tb = DotXZ(b2 - a1, dir) / length;
            float lo = Mathf.Max(0f, Mathf.Min(ta, tb));
            float hi = Mathf.Min(length, Mathf.Max(ta, tb));
            if (hi - lo < MinOverlap) return false;

            // 겹친 구간을 변 1 위의 점으로 (높이도 변 1을 따라 보간)
            from = Vector3.Lerp(a1, b1, lo / length);
            to = Vector3.Lerp(a1, b1, hi / length);

            // 높이가 같은지: 겹친 구간 양 끝에서 변 1 높이와 변 2 높이 비교 (위에서 보면 겹쳐도 위아래 층이면 연결점 아님)
            // 양 끝이 모두 가까우면 두 변이 직선이라 그 사이도 모두 가까움 (기울어진 변이 엇갈리는 경우도 거름)
            if (Mathf.Abs(from.y - HeightOnEdge(a2, b2, from)) > portalHeightTolerance) return false;
            if (Mathf.Abs(to.y - HeightOnEdge(a2, b2, to)) > portalHeightTolerance) return false;
            return true;
        }

        // 변(a~b) 위에서 점 p 와 같은 XZ 위치의 높이 (p 를 변에 투영한 비율로 보간)
        private static float HeightOnEdge(Vector3 a, Vector3 b, Vector3 p)
        {
            Vector3 ab = b - a;
            float lengthSq = DotXZ(ab, ab);
            if (lengthSq < 1e-8f) return a.y;
            float t = Mathf.Clamp01(DotXZ(p - a, ab) / lengthSq);
            return Mathf.Lerp(a.y, b.y, t);
        }

        // 3. 같은 두 영역 사이 조각들의 가장 바깥쪽 두 점
        // - 볼록한 두 영역이 맞닿은 경계는 항상 한 직선 위의 이어진 구간이라, 조각들의 끝점 중 서로 가장 먼 두 점이 양 끝입니다.
        private static void GetOuterEnds(List<Vector3> pieceEnds, out Vector3 a, out Vector3 b)
        {
            Vector3 origin = pieceEnds[0];
            Vector3 dir = pieceEnds[1] - origin;

            // 첫 조각의 방향으로 투영했을 때 가장 앞과 가장 뒤에 있는 점
            float min = 0f, max = 0f;
            a = b = origin;
            foreach (Vector3 p in pieceEnds)
            {
                float t = DotXZ(p - origin, dir);
                if (t < min)
                {
                    min = t;
                    a = p;
                }
                if (t > max)
                {
                    max = t;
                    b = p;
                }
            }
        }

        // 위에서 본(XZ) 내적
        private static float DotXZ(Vector3 u, Vector3 v) => u.x * v.x + u.z * v.z;

        #endregion

        #region 남은 거리 표

        // 목표 영역마다 다익스트라를 거꾸로 한 번씩 돌려서 표를 채웁니다.
        // - 노드 = 방향별 연결점, 비용 = 연결점 중간점 사이 거리 (어느 쪽으로 갈지 고르는 용도의 근사값)
        // - 목표로 들어가는 연결점(X→T) 을 0 으로 놓고, 확정된 연결점 u(X→Y) 바로 앞에 넘을 수 있는
        //   연결점 p(W→X) 의 값을 "u 의 값 + p~u 중간점 거리" 로 갱신하며 바깥으로 퍼져 나갑니다.
        private void BuildNextTable()
        {
            int regionCount = regions.Count;
            int portalCount = portals.Count;
            portalDist = new float[portalCount * regionCount];

            var dist = new float[portalCount];
            var done = new bool[portalCount];
            for (int target = 0; target < regionCount; target++)
            {
                for (int i = 0; i < portalCount; i++)
                {
                    dist[i] = float.MaxValue;
                    done[i] = false;
                }

                // 목표로 들어가는 연결점 = 목표에서 나가는 연결점의 반대 방향. 넘으면 바로 목표 안이라 0
                foreach (int exit in regionPortals[target]) dist[Reverse(exit)] = 0f;

                // 연결점 수가 수백 개 수준이라 우선순위 큐 없이 가장 작은 값을 직접 찾습니다
                for (int iter = 0; iter < portalCount; iter++)
                {
                    int u = -1;
                    for (int i = 0; i < portalCount; i++)
                        if (done[i] == false && dist[i] < float.MaxValue && (u < 0 || dist[i] < dist[u])) u = i;
                    if (u < 0) break; // 남은 연결점은 목표로 갈 길이 없음
                    done[u] = true;

                    // u 가 목표에서 출발하는 연결점이면, 그 앞은 목표 안이라 더 퍼질 필요가 없음
                    int x = portals[u].from;
                    if (x == target) continue;

                    // x 로 들어오는 연결점 p = x 에서 나가는 연결점 e 의 반대 방향
                    foreach (int e in regionPortals[x])
                    {
                        if (e == u) continue; // p 가 u 의 반대(Y→X) 면 넘자마자 되돌아가는 경로라 제외
                        int p = Reverse(e);
                        float nd = dist[u] + Vector3.Distance(Mid(portals[p]), Mid(portals[u]));
                        if (nd < dist[p]) dist[p] = nd;
                    }
                }

                for (int i = 0; i < portalCount; i++) portalDist[i * regionCount + target] = dist[i];
            }
        }

        public float RemainDistance(int portal, int target) => portalDist[portal * regions.Count + target];

        // 내 영역(region) 에서 목표 영역으로 가려면 넘어야 할 연결점 번호. 내 영역의 연결점 중
        // "내 위치(방 로컬) → 연결점 위 가장 가까운 점 거리 + 표의 남은 거리" 가 가장 작은 것 (갈 수 없으면 -1)
        // - maxRemain: 방금 넘어온 연결점의 남은 거리. 이보다 남은 거리가 작은 출구만 후보로 삼아서
        //   남은 거리가 넘을 때마다 줄어들게 함 → 되돌아가거나 빙빙 돌 수 없음 (만족하는 출구가 없으면 조건 없이 다시 고름)
        public int ChooseNextPortal(int region, int target, Vector3 localPos, float maxRemain = float.MaxValue)
        {
            if (portalDist == null || region < 0 || target < 0 || region == target) return -1;

            int best = -1;
            float bestCost = float.MaxValue;
            foreach (int p in regionPortals[region])
            {
                float remain = RemainDistance(p, target);
                if (remain == float.MaxValue || remain >= maxRemain) continue;

                float cost = Vector3.Distance(localPos, ClosestPoint(portals[p], localPos)) + remain;
                if (cost < bestCost)
                {
                    bestCost = cost;
                    best = p;
                }
            }

            if (best < 0 && maxRemain < float.MaxValue) return ChooseNextPortal(region, target, localPos);
            return best;
        }

        public Portal GetPortal(int portal) => portals[portal];

        // 방향 두 개를 연달아 저장했으므로 (A→B = 짝수, B→A = 그다음 홀수) 마지막 비트만 바꾸면 반대 방향
        private static int Reverse(int portal) => portal ^ 1;

        private static Vector3 Mid(Portal p) => (p.a + p.b) * 0.5f;

        private static Vector3 ClosestPoint(Portal p, Vector3 pos)
        {
            Vector3 ab = p.b - p.a;
            float t = Vector3.Dot(pos - p.a, ab) / Mathf.Max(ab.sqrMagnitude, 1e-6f);
            return p.a + ab * Mathf.Clamp01(t);
        }

        #endregion

        #region 실행 중 길찾기 (몬스터가 쓰는 기능, 모두 방 로컬 좌표)

        private const float InsideEpsilon = 0.02f;       // 영역 경계에서 이 거리(2cm)까지는 안쪽으로 봄 (경계 위의 점이 양쪽 모두에 속하도록)
        private const float PortalEndInset = 0.05f;       // 시야가 연결점 양 끝(장애물 모서리)을 스치며 지나가면 막힌 것으로 봄
        private const float PlayerHeightTolerance = 2f;   // 플레이어는 점프하므로 영역 높이와 2m 차이까지 그 영역 위로 봄

        // 켜져 있는 방들. 몬스터가 자기가 서 있는 방을 찾을 때 씁니다.
        public static readonly List<RoomNavRegions> Active = new List<RoomNavRegions>();

        // 플레이어가 마지막으로 지도 위에 있던 영역과 지점 (몬스터들이 함께 쓰는 목표)
        private int goalRegion = -1;
        private Vector3 goalPoint;
        private int goalFrame = -1;

        // 플레이어 아래에 영역이 없어서(사물 위 등) 마지막 목표를 그대로 쓰는 중인지
        public bool IsGoalOffMap { get; private set; }

        private void OnEnable()
        {
            if (Active.Contains(this) == false) Active.Add(this);
        }

        private void OnDisable()
        {
            Active.Remove(this);
        }

        // 점(방 로컬)이 서 있는 영역 번호 (없으면 -1)
        // - hint 영역과 그 이웃부터 확인하고, 없으면 전체를 확인합니다. (몬스터는 보통 같은 영역이나 바로 옆 영역에 있음)
        //   hint 와 이웃은 지금 층에서 이어진 영역이라 처음 맞는 곳을 그대로 씀
        // - 전체를 확인할 때는 위아래로 여러 층이 겹칠 수 있으므로, 맞는 영역 중 발밑 아래에서 가장 가까운 층을 고름
        //   (점프 중인 플레이어가 위층 영역으로 잘못 잡히지 않도록. 발밑 아래 층이 없으면 위쪽에서 가장 가까운 층)
        public int FindRegion(Vector3 localPos, int hint, float heightTolerance)
        {
            EnsureReady();
            if (hint >= 0 && hint < regions.Count)
            {
                if (IsOnRegion(hint, localPos, heightTolerance)) return hint;
                foreach (int p in regionPortals[hint])
                    if (IsOnRegion(portals[p].to, localPos, heightTolerance)) return portals[p].to;
            }

            int best = -1;
            float bestScore = float.MaxValue;
            for (int r = 0; r < regions.Count; r++)
            {
                if (IsInsideXZ(r, localPos) == false) continue;

                float above = localPos.y - HeightAt(r, localPos); // 바닥보다 얼마나 위에 있는지 (음수면 바닥 아래)
                if (Mathf.Abs(above) > heightTolerance) continue;

                // 발밑 아래 층(above >= 0) 은 가까운 순서, 위쪽 층은 그보다 항상 뒤
                float score = above >= -InsideEpsilon ? above : heightTolerance - above;
                if (score >= bestScore) continue;
                bestScore = score;
                best = r;
            }
            return best;
        }

        // 점(방 로컬)이 어느 영역에도 없을 때, maxDistance 안에서 가장 가까운 영역 경계 위의 점으로 옮깁니다. (없으면 -1, 점은 그대로)
        // - 벽에 붙은 플레이어처럼 네비메시가 깎인 띠(베이크 반지름) 안에 있는 점을 지도 위로 되돌리는 용도
        // - hint 영역과 그 이웃부터 확인하고, 거기서 못 찾으면 전체를 확인합니다. (벽을 따라 움직이면 보통 hint 근처에서 찾음)
        // - 옮긴 점의 높이는 그대로 두고 XZ 만 바꿉니다. (경계 위의 점은 InsideEpsilon 덕분에 그 영역 안으로 판정됨)
        private int SnapToRegion(ref Vector3 localPos, int hint, float heightTolerance, float maxDistance)
        {
            int best = -1;
            float bestSq = maxDistance * maxDistance;
            Vector3 bestPoint = localPos;

            if (hint >= 0 && hint < regions.Count)
            {
                ConsiderSnap(hint, localPos, heightTolerance, ref best, ref bestSq, ref bestPoint);
                foreach (int p in regionPortals[hint])
                    ConsiderSnap(portals[p].to, localPos, heightTolerance, ref best, ref bestSq, ref bestPoint);
            }

            if (best < 0)
                for (int r = 0; r < regions.Count; r++)
                    ConsiderSnap(r, localPos, heightTolerance, ref best, ref bestSq, ref bestPoint);

            if (best >= 0) localPos = new Vector3(bestPoint.x, localPos.y, bestPoint.z);
            return best;
        }

        // 영역 경계 위의 가장 가까운 점이 지금까지의 후보보다 가깝고 높이도 맞으면 후보로 바꿈
        private void ConsiderSnap(int region, Vector3 p, float heightTolerance, ref int best, ref float bestSq, ref Vector3 bestPoint)
        {
            float sq = ClosestOnBoundaryXZ(region, p, out Vector3 closest);
            if (sq >= bestSq) return;
            if (Mathf.Abs(p.y - HeightAt(region, closest)) > heightTolerance) return;

            best = region;
            bestSq = sq;
            bestPoint = closest;
        }

        // 위에서 봤을 때(XZ) 영역 경계의 변들 중 p 에 가장 가까운 점과 그 거리의 제곱
        private float ClosestOnBoundaryXZ(int region, Vector3 p, out Vector3 closest)
        {
            List<int> poly = regions[region];
            float bestSq = float.MaxValue;
            closest = p;
            for (int i = 0; i < poly.Count; i++)
            {
                Vector3 a = points[poly[i]];
                Vector3 ab = points[poly[(i + 1) % poly.Count]] - a;
                float lengthSq = DotXZ(ab, ab);
                float t = lengthSq < 1e-8f ? 0f : Mathf.Clamp01(DotXZ(p - a, ab) / lengthSq);
                Vector3 c = a + ab * t;

                float dx = p.x - c.x;
                float dz = p.z - c.z;
                float sq = dx * dx + dz * dz;
                if (sq < bestSq)
                {
                    bestSq = sq;
                    closest = c;
                }
            }
            return bestSq;
        }

        // 위에서 봤을 때 볼록 영역 안이고, 그 위치의 바닥 높이와 heightTolerance 안인지
        private bool IsOnRegion(int region, Vector3 p, float heightTolerance)
        {
            return IsInsideXZ(region, p) && Mathf.Abs(p.y - HeightAt(region, p)) <= heightTolerance;
        }

        // 위에서 봤을 때 볼록 영역 안인지 (반시계로 돌며 모든 변의 왼쪽에 있으면 안쪽 = CCW 판정)
        private bool IsInsideXZ(int region, Vector3 p)
        {
            List<int> poly = regions[region];
            for (int i = 0; i < poly.Count; i++)
            {
                Vector3 a = points[poly[i]];
                Vector3 b = points[poly[(i + 1) % poly.Count]];
                Vector3 ab = b - a;
                float length = Mathf.Sqrt(DotXZ(ab, ab));
                if (length < 1e-6f) continue;
                if (Cross(ab, p - a) / length < -InsideEpsilon) return false; // 이 변의 오른쪽(바깥)
            }
            return true;
        }

        // 지도 위 시야 판정: start → end 직선이 장애물(지도 밖)을 지나지 않고 갈 수 있는지
        // - 지금 영역에서 직선이 빠져나가는 지점을 찾고, 그 지점이 연결점 위면 옆 영역으로 넘어가 반복
        // - 연결점이 아닌 경계(벽, 장애물)로 빠져나가면 막힘
        // - 연결점은 높이가 같은 곳에만 있으므로, 층이 바뀌려면 경사로 연결점을 지나야 이어짐
        // - 네비메시가 몬스터 반지름만큼 깎여 있으므로, 통과하면 몸이 장애물에 닿지 않고 직진할 수 있음
        public bool HasLineOfSight(Vector3 start, int startRegion, Vector3 end, int endRegion)
        {
            if (startRegion < 0 || endRegion < 0) return false;

            int region = startRegion;
            float tNow = 0f;
            for (int guard = 0; guard <= regions.Count; guard++)
            {
                if (region == endRegion) return true; // 목표 영역 안은 볼록이라 직진 가능

                // 직선 S(t) = start + (end - start) * t 가 이 영역을 빠져나가는 t (가장 먼저 바깥으로 나가는 변)
                List<int> poly = regions[region];
                float tExit = float.MaxValue;
                for (int i = 0; i < poly.Count; i++)
                {
                    Vector3 a = points[poly[i]];
                    Vector3 ab = points[poly[(i + 1) % poly.Count]] - a;
                    float f0 = Cross(ab, start - a); // 변의 왼쪽(안쪽)이면 양수
                    float f1 = Cross(ab, end - a);
                    if (f1 >= 0f) continue;          // 끝점이 이 변 안쪽이면 이 변으로는 나가지 않음

                    float t = f0 / (f0 - f1);         // 직선이 이 변의 연장선과 만나는 지점
                    if (t < tExit) tExit = t;
                }

                // 끝점이 위에서 봤을 때 이 영역 안 = 목표 영역 경계 근처의 오차일 때만 보임
                // 위아래 다른 층(2층 플레이어 바로 아래의 1층 영역 등)이면 안 보임 → 표로 경사로를 찾아감
                // (플레이어 높이가 아니라 두 영역의 바닥 높이를 비교해서, 점프 중이어도 같은 층이면 보임)
                if (tExit == float.MaxValue)
                    return Mathf.Abs(HeightAt(region, end) - HeightAt(endRegion, end)) <= portalHeightTolerance;
                tExit = Mathf.Max(tExit, tNow);

                // 빠져나가는 지점이 연결점 위인지 (양 끝 모서리를 스치는 경우는 막힌 것으로 봄)
                Vector3 exitPoint = Vector3.Lerp(start, end, tExit);
                int next = -1;
                foreach (int p in regionPortals[region])
                {
                    if (IsOnPortal(portals[p], exitPoint) == false) continue;
                    next = portals[p].to;
                    break;
                }
                if (next < 0) return false; // 벽이나 장애물로 나감

                region = next;
                tNow = tExit;
            }
            return false;
        }

        private static bool IsOnPortal(Portal portal, Vector3 point)
        {
            Vector3 ab = portal.b - portal.a;
            float length = Mathf.Sqrt(DotXZ(ab, ab));
            if (length < 1e-6f) return false;

            if (Mathf.Abs(Cross(ab, point - portal.a)) / length > InsideEpsilon) return false;
            float along = DotXZ(point - portal.a, ab) / length;
            return along >= PortalEndInset && along <= length - PortalEndInset;
        }

        // 몬스터들이 함께 쓰는 목표 (플레이어가 서 있는 영역과 지점). 한 프레임에 한 번만 갱신합니다.
        // - 플레이어 아래에 영역이 없으면 goalSnapDistance 안의 가장 가까운 영역 경계 위의 점을 목표로 씀
        //   (벽에 붙으면 네비메시가 깎인 띠 안에 서게 되므로. 매 프레임 지금 위치에서 다시 구해서 벽을 따라 움직여도 따라감)
        // - 그보다 멀면(큰 사물 위, 방 밖) 갱신하지 않아서 마지막으로 지도 위에 있던 곳이 목표로 남음
        // - 목표가 아직 한 번도 정해지지 않았으면 false
        public bool TryGetGoal(out int region, out Vector3 localPoint)
        {
            EnsureReady();
            if (goalFrame != Time.frameCount)
            {
                goalFrame = Time.frameCount;
                Player.LocalPlayer player = Player.Player.LocalPlayer;
                if (player != null && regions.Count > 0)
                {
                    Vector3 local = transform.InverseTransformPoint(player.transform.position);
                    int found = FindRegion(local, goalRegion, PlayerHeightTolerance);
                    if (found < 0) found = SnapToRegion(ref local, goalRegion, PlayerHeightTolerance, goalSnapDistance);
                    IsGoalOffMap = found < 0;
                    if (found >= 0)
                    {
                        goalRegion = found;
                        goalPoint = local;
                    }
                }
            }

            region = goalRegion;
            localPoint = goalPoint;
            return goalRegion >= 0;
        }

        // 지도가 새로 만들어지거나 불러와지면 이전 목표는 번호가 맞지 않으므로 버림
        private void ResetGoal()
        {
            goalRegion = -1;
            goalFrame = -1;
            IsGoalOffMap = false;
        }

        #endregion

        #region 저장, 불러오기

#if UNITY_EDITOR
        // 지금 만든 결과를 지도 에셋에 저장합니다.
        // - 연결된 지도가 있으면 그 에셋을 덮어씀 (같은 프리팹의 방이면 어디서 눌러도 결과가 같음)
        // - 없으면 프리팹 모드에서만 새로 만듦: 프리팹 옆 "<프리팹 이름>/NavMap-<프리팹 이름>.asset" (네비메시 베이크 파일과 같은 폴더)
        [ContextMenu("지도 저장")]
        public void Save()
        {
            if (regions.Count == 0)
            {
                Debug.LogWarning($"[RoomNavRegions] {name}: 저장할 영역이 없습니다. \"영역 생성\" 을 먼저 누르세요.");
                return;
            }

            RoomNavMap map = navMap;
            if (map == null)
            {
                string path = GetNewMapPath();
                if (path == null)
                {
                    Debug.LogWarning($"[RoomNavRegions] {name}: 처음 저장은 방 프리팹을 프리팹 모드로 연 상태에서 해주세요.");
                    return;
                }

                map = ScriptableObject.CreateInstance<RoomNavMap>();
                AssetDatabase.CreateAsset(map, path);

                Undo.RecordObject(this, "Assign Room Nav Map");
                navMap = map;
                EditorUtility.SetDirty(this);
            }

            WriteTo(map);
            EditorUtility.SetDirty(map);
            AssetDatabase.SaveAssetIfDirty(map);
            Debug.Log($"[RoomNavRegions] {name}: 지도 저장 → {AssetDatabase.GetAssetPath(map)}", map);
        }

        private string GetNewMapPath()
        {
            PrefabStage stage = PrefabStageUtility.GetPrefabStage(gameObject);
            if (stage == null) return null;

            string prefabPath = stage.assetPath;
            string dir = Path.GetDirectoryName(prefabPath).Replace('\\', '/');
            string prefabName = Path.GetFileNameWithoutExtension(prefabPath);
            string folder = dir + "/" + prefabName;
            if (AssetDatabase.IsValidFolder(folder) == false) AssetDatabase.CreateFolder(dir, prefabName);
            return AssetDatabase.GenerateUniqueAssetPath(folder + "/NavMap-" + prefabName + ".asset");
        }
#endif

        // 목록의 목록을 한 줄로 펴서 저장 (RoomNavMap 설명 참고)
        private void WriteTo(RoomNavMap map)
        {
            map.triangleCount = TriangleCount;
            map.points = points.ToArray();
            Flatten(regions, out map.regionStarts, out map.regionVerts);
            map.portals = portals.ToArray();
            Flatten(regionPortals, out map.portalStarts, out map.regionPortalList);
            map.portalDist = (float[])portalDist.Clone();
        }

        // 한 줄로 편 배열을 다시 영역별 목록으로 나눠서 불러옴
        private void Load(RoomNavMap map)
        {
            Clear();
            if (map.points == null || map.regionStarts == null) return;

            TriangleCount = map.triangleCount;
            points.AddRange(map.points);
            Unflatten(map.regionStarts, map.regionVerts, regions);
            portals.AddRange(map.portals);
            Unflatten(map.portalStarts, map.regionPortalList, regionPortals);
            portalDist = (float[])map.portalDist.Clone();
            BuildPlanes();
        }

        private static void Flatten(List<List<int>> lists, out int[] starts, out int[] values)
        {
            var flat = new List<int>();
            starts = new int[lists.Count];
            for (int i = 0; i < lists.Count; i++)
            {
                starts[i] = flat.Count;
                flat.AddRange(lists[i]);
            }
            values = flat.ToArray();
        }

        private static void Unflatten(int[] starts, int[] values, List<List<int>> lists)
        {
            lists.Clear();
            for (int i = 0; i < starts.Length; i++)
            {
                int end = i + 1 < starts.Length ? starts[i + 1] : values.Length;
                var list = new List<int>(end - starts[i]);
                for (int k = starts[i]; k < end; k++) list.Add(values[k]);
                lists.Add(list);
            }
        }

        #endregion
    }
}
