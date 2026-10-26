using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

namespace Study3D.FpsGame.NavTest
{
    // NavPortal
    // : 두 영역이 맞닿은 경계 선분(연결점) 입니다.
    // - from 영역에서 to 영역으로 넘어갈 때 지나가는 선분입니다. (반대 방향은 별도 인스턴스)
    public class NavPortal
    {
        public int id;            // 양방향이 같은 id 를 공유
        public int dirId;         // 방향별 id (id * 2 또는 id * 2 + 1)
        public NavRegion from;
        public NavRegion to;
        public NavPortal reverse; // to → from 방향 인스턴스
        public Vector3 a;
        public Vector3 b;

        public Vector3 Mid => (a + b) * 0.5f;

        public Vector3 ClosestPoint(Vector3 p)
        {
            Vector3 ab = b - a;
            float t = Vector3.Dot(p - a, ab) / Mathf.Max(ab.sqrMagnitude, 1e-6f);
            return a + ab * Mathf.Clamp01(t);
        }
    }

    // NavRegion
    // : 장애물이 없는 (거의) 볼록 다각형 한 덩어리 입니다. (= 그래프의 노드)
    // - 볼록하기 때문에 영역 안의 두 점은 직선으로 이동할 수 있습니다.
    // - 장애물 모서리를 깎으며 생기는 작은 홈(몬스터 반지름보다 얕음)은 허용합니다.
    public class NavRegion
    {
        public int index;
        public Vector3[] verts;   // 위에서 봤을 때 반시계 방향
        public Vector3 center;
        public Vector3 normal;
        public float area;
        public readonly List<NavPortal> portals = new List<NavPortal>();

        public float HeightAt(float x, float z)
        {
            if (normal.y < 0.01f) return center.y;
            return center.y - (normal.x * (x - center.x) + normal.z * (z - center.z)) / normal.y;
        }

        // 약간 오목한 영역도 있으므로 교차 횟수 방식 + 경계에서 eps 이내도 포함
        public bool ContainsXZ(Vector3 p, float eps)
        {
            bool inside = false;
            for (int i = 0, j = verts.Length - 1; i < verts.Length; j = i++)
            {
                Vector3 a = verts[i];
                Vector3 b = verts[j];
                if ((a.z > p.z) != (b.z > p.z) && p.x < (b.x - a.x) * (p.z - a.z) / (b.z - a.z) + a.x)
                    inside = !inside;
            }
            if (inside) return true;

            for (int i = 0, j = verts.Length - 1; i < verts.Length; j = i++)
                if (DistanceToEdgeXZ(p, verts[j], verts[i]) <= eps) return true;
            return false;
        }

        private static float DistanceToEdgeXZ(Vector3 p, Vector3 a, Vector3 b)
        {
            float abx = b.x - a.x, abz = b.z - a.z;
            float t = ((p.x - a.x) * abx + (p.z - a.z) * abz) / Mathf.Max(abx * abx + abz * abz, 1e-6f);
            t = Mathf.Clamp01(t);
            float dx = p.x - (a.x + abx * t), dz = p.z - (a.z + abz * t);
            return Mathf.Sqrt(dx * dx + dz * dz);
        }
    }

    // NavGraph
    // : 베이크된 네비메시 삼각형을 볼록 영역으로 합치고, 영역 단위로 경로를 구합니다.
    // - 병합: Hertel-Mehlhorn (공유 변을 지워도 볼록하면 합침, 긴 변부터)
    // - 표: 연결점별 "목표 영역까지 남은 거리" 를 다익스트라로 미리 계산해 두고, 런타임엔 내 영역 연결점 중 고르기만 함 (BuildNextTable)
    // - 경로 다듬기: 표를 따라 지나갈 연결점 목록 → Funnel(String Pulling) 로 코너 목록 생성
    // - 시야 판정: 직선이 네비메시 위로만 지나가는지 영역을 따라가며 확인 (Raycast)
    public class NavGraph
    {
        private const float WeldPrecision = 100f;      // 0.01 단위로 정점 용접
        private const float PlaneTolerance = 0.25f;    // 같은 평면으로 볼 높이 오차 (경사로 밑동처럼 살짝 뜬 조각 흡수)
        private const float InsideEps = 0.02f;
        private const float MaxHeightDiff = 0.5f;      // 이 높이 차 안에 있어야 그 영역 위에 있다고 판단
        private const float PortalLineTolerance = 0.05f;
        private const float PortalHeightTolerance = 0.35f;   // 연결점 양쪽 변의 높이 차 허용 (계단 높이 정도)
        private const float RaycastPortalTolerance = 0.01f;  // 시야 판정에서 "연결점 위" 로 볼 거리
        private const float MinPortalLength = 0.05f;

        public readonly List<NavRegion> regions = new List<NavRegion>();
        public int TriangleCount { get; private set; }
        public int PortalCount { get; private set; }

        // 연결점별 남은 거리 표: portalDist[연결점 dirId * 영역수 + 목표 영역] = 그 연결점을 넘은 뒤 목표 영역 경계까지 거리
        private float[] portalDist;
        public int TableBytes => portalDist == null ? 0 : portalDist.Length * sizeof(float);

        // 경로 생성 임시 버퍼 (매번 할당하지 않도록 재사용)
        private readonly List<NavPortal> portalPath = new List<NavPortal>();
        private readonly List<Vector3> funnelLeft = new List<Vector3>();
        private readonly List<Vector3> funnelRight = new List<Vector3>();

        // 위에서 본(XZ) 2D 외적. 양수면 v 가 u 의 왼쪽
        public static float Cross(Vector3 u, Vector3 v) => u.x * v.z - u.z * v.x;

        // concaveTolerance: 병합 시 허용할 오목한 홈의 최대 깊이
        public void Build(NavMeshTriangulation tri, Bounds bounds, float concaveTolerance)
        {
            regions.Clear();
            portalDist = null;

            // 1. 정점 용접 (같은 위치의 정점을 하나의 인덱스로)
            var weldMap = new Dictionary<Vector3Int, int>();
            var points = new List<Vector3>();
            int[] remap = new int[tri.vertices.Length];
            for (int i = 0; i < tri.vertices.Length; i++)
            {
                Vector3 v = tri.vertices[i];
                Vector3Int key = Vector3Int.RoundToInt(v * WeldPrecision);
                if (!weldMap.TryGetValue(key, out int id))
                {
                    id = points.Count;
                    points.Add(v);
                    weldMap.Add(key, id);
                }
                remap[i] = id;
            }

            // 2. 삼각형을 반시계 방향 다각형으로 (데모 범위 밖 삼각형은 제외)
            var polys = new List<List<int>>();
            for (int i = 0; i < tri.indices.Length; i += 3)
            {
                int a = remap[tri.indices[i]];
                int b = remap[tri.indices[i + 1]];
                int c = remap[tri.indices[i + 2]];
                if (a == b || b == c || a == c) continue;

                Vector3 centroid = (points[a] + points[b] + points[c]) / 3f;
                if (!bounds.Contains(centroid)) continue;

                float area2 = Cross(points[b] - points[a], points[c] - points[a]);
                if (Mathf.Abs(area2) < 1e-5f) continue;
                polys.Add(area2 > 0f ? new List<int> { a, b, c } : new List<int> { a, c, b });
            }
            TriangleCount = polys.Count;

            // 3. 볼록함이 (거의) 유지되는 한 합치기
            MergePolygons(polys, points, concaveTolerance);

            // 4. 영역 생성
            foreach (var poly in polys)
            {
                if (poly == null) continue;

                var region = new NavRegion { index = regions.Count, verts = new Vector3[poly.Count] };
                Vector3 sum = Vector3.zero;
                for (int i = 0; i < poly.Count; i++)
                {
                    region.verts[i] = points[poly[i]];
                    sum += region.verts[i];
                }
                region.center = sum / poly.Count;
                region.normal = PlaneNormal(poly, points);

                float area2 = 0f;
                for (int i = 0; i < poly.Count; i++)
                    area2 += Cross(region.verts[i] - region.center, region.verts[(i + 1) % poly.Count] - region.center);
                region.area = area2 * 0.5f;

                regions.Add(region);
            }

            // 5. 연결점(포털) 생성
            BuildPortals();
        }

        // 모든 목표 영역에 대해 "각 연결점(방향 있음)을 넘은 뒤 목표 영역까지 남은 거리" 를 미리 계산합니다. (Build 이후 한 번)
        // - A→B 와 B→A 를 다른 노드로 보고, 목표 영역마다 Dijkstra 를 거꾸로(목표에서 바깥으로) 한 번씩 돌립니다.
        // - 런타임에는 내 영역의 연결점 몇 개 중 (내 위치 → 연결점 + 남은 거리) 가 가장 작은 것을 고르기만 함 (탐색 없음)
        // - 정적인 맵 전제. 메모리 = 연결점 수 * 2 * 영역 수 * 4 바이트, 계산은 베이크 시점에만.
        public void BuildNextTable()
        {
            int n = regions.Count;
            int dirCount = PortalCount * 2;
            portalDist = new float[dirCount * n];

            var dirPortals = new NavPortal[dirCount];
            foreach (var region in regions)
                foreach (var p in region.portals)
                    dirPortals[p.dirId] = p;

            var dist = new float[dirCount];
            var done = new bool[dirCount];
            foreach (var target in regions)
            {
                for (int i = 0; i < dirCount; i++)
                {
                    dist[i] = float.MaxValue;
                    done[i] = false;
                }
                // 목표 영역으로 들어가는 연결점은 남은 거리 0 (넘으면 그 뒤는 직진)
                foreach (var p in target.portals) dist[p.reverse.dirId] = 0f;

                // Dijkstra (연결점 수가 적어서 선형 탐색으로 충분, 베이크 시점에만 실행)
                for (int iter = 0; iter < dirCount; iter++)
                {
                    int u = -1;
                    for (int i = 0; i < dirCount; i++)
                        if (!done[i] && dist[i] < float.MaxValue && (u < 0 || dist[i] < dist[u])) u = i;
                    if (u < 0) break;
                    done[u] = true;

                    // u(A→B) 바로 앞에 넘을 수 있는 연결점 = A 로 들어오는 연결점 w(X→A), 단 u 의 반대 방향(B→A)은 제외
                    NavPortal up = dirPortals[u];
                    if (up.from == target) continue;   // 목표 영역 안을 지나가는 경로는 의미 없음
                    foreach (var exit in up.from.portals)
                    {
                        NavPortal w = exit.reverse;
                        if (w.id == up.id) continue;
                        float nd = dist[u] + Vector3.Distance(w.Mid, up.Mid);
                        if (nd < dist[w.dirId]) dist[w.dirId] = nd;
                    }
                }

                for (int i = 0; i < dirCount; i++) portalDist[i * n + target.index] = dist[i];
            }
        }

        public float RemainDistance(NavPortal p, NavRegion to)
        {
            return portalDist[p.dirId * regions.Count + to.index];
        }

        // 내 영역에서 목표 영역으로 가기 위해 지나갈 연결점 선택
        // - 후보는 내 영역의 연결점들뿐 (보통 3~8개), 비용 = 내 위치 → 연결점 위 가장 가까운 점 + 표의 남은 거리
        // - maxRemain: 직전에 넘은 연결점의 남은 거리. 이보다 남은 거리가 작은 연결점만 후보로 삼아서
        //   남은 거리가 매번 줄어들게 함 → 되돌아가거나 빙빙 도는 것이 불가능
        public NavPortal ChooseNextPortal(NavRegion from, NavRegion to, Vector3 pos, float maxRemain = float.MaxValue)
        {
            if (portalDist == null || from == null || to == null || from == to) return null;

            NavPortal best = null;
            float bestCost = float.MaxValue;
            foreach (var p in from.portals)
            {
                float remain = RemainDistance(p, to);
                if (remain >= maxRemain || remain == float.MaxValue) continue;

                float cost = Vector3.Distance(pos, p.ClosestPoint(pos)) + remain;
                if (cost < bestCost)
                {
                    bestCost = cost;
                    best = p;
                }
            }

            // 조건을 만족하는 출구가 없으면(직전 선택이 최적이 아니었던 경우) 조건 없이 다시 선택
            if (best == null && maxRemain < float.MaxValue) return ChooseNextPortal(from, to, pos);
            return best;
        }

        // 네비메시 시야 판정: start → end 직선이 네비메시 위로만 지나가는지
        // - 현재 영역에서 선분이 빠져나가는 경계를 찾고, 그 경계가 연결점이면 옆 영역으로 넘어가 반복
        // - 연결점이 아닌 경계(벽, 낭떠러지)로 빠져나가면 막힘
        // - 네비메시가 몬스터 반지름만큼 깎여 있으므로, 통과하면 몸이 벽에 닿지 않고 직진 가능
        public bool Raycast(Vector3 start, NavRegion startRegion, Vector3 end, NavRegion endRegion)
        {
            if (startRegion == null || endRegion == null) return false;

            Vector3 d = end - start;
            NavRegion r = startRegion;
            float t = 0f;
            for (int step = 0; step <= regions.Count; step++)
            {
                if (r == endRegion) return true;   // 목표 영역에 들어오면 그 안은 직진 가능

                // 이 영역에서 선분이 빠져나가는 지점 = 지금보다 뒤쪽(t 가 큰) 경계 교차점 중 가장 먼 것
                float exitT = -1f;
                for (int i = 0, j = r.verts.Length - 1; i < r.verts.Length; j = i++)
                {
                    Vector3 a = r.verts[j];
                    Vector3 e = r.verts[i] - a;
                    float denom = Cross(d, e);
                    if (Mathf.Abs(denom) < 1e-8f) continue;   // 평행

                    float tt = Cross(a - start, e) / denom;
                    float u = Cross(a - start, d) / denom;
                    if (u < -1e-3f || u > 1f + 1e-3f) continue;
                    if (tt <= t + 1e-4f || tt > 1f) continue;
                    if (tt > exitT) exitT = tt;
                }

                // 빠져나가는 지점이 없음 = 끝점이 이 영역 안이거나, 출발점이 경계 바로 바깥(허용 오차)이라 교차를 못 찾은 경우
                // → 끝점이 정말 이 영역 위에 있을 때만 보임
                if (exitT < 0f) return IsOn(r, end, MaxHeightDiff);

                // 빠져나가는 지점이 연결점 위인지
                Vector3 exit = start + d * exitT;
                NavPortal through = null;
                foreach (var p in r.portals)
                {
                    if (IsOnPortal(exit, p))
                    {
                        through = p;
                        break;
                    }
                }
                if (through == null) return false;   // 벽이나 낭떠러지

                r = through.to;
                t = exitT;
            }
            return false;
        }

        // 점이 연결점 선분 "안쪽" 위에 있는지 (양 끝 = 장애물 모서리를 스치며 잘라 지나가는 경우는 제외)
        private static bool IsOnPortal(Vector3 p, NavPortal portal)
        {
            float abx = portal.b.x - portal.a.x, abz = portal.b.z - portal.a.z;
            float lenSq = abx * abx + abz * abz;
            if (lenSq < 1e-6f) return false;

            float k = ((p.x - portal.a.x) * abx + (p.z - portal.a.z) * abz) / lenSq;
            if (k < 0f || k > 1f) return false;

            float dx = p.x - (portal.a.x + abx * k), dz = p.z - (portal.a.z + abz * k);
            return dx * dx + dz * dz <= RaycastPortalTolerance * RaycastPortalTolerance;
        }

        // 표를 따라가며 지나갈 연결점을 모은 뒤 Funnel 로 코너 목록 생성 (탐색 없음)
        public bool BuildPathFromTable(Vector3 start, NavRegion startRegion, Vector3 goal, NavRegion goalRegion, List<Vector3> corners)
        {
            corners.Clear();
            if (startRegion == null || goalRegion == null) return false;

            portalPath.Clear();
            NavRegion r = startRegion;
            Vector3 pos = start;
            float maxRemain = float.MaxValue;
            while (r != goalRegion)
            {
                NavPortal p = ChooseNextPortal(r, goalRegion, pos, maxRemain);
                if (p == null || portalPath.Count > regions.Count) return false;
                portalPath.Add(p);
                pos = p.ClosestPoint(pos);
                maxRemain = RemainDistance(p, goalRegion);
                r = p.to;
            }

            if (portalPath.Count == 0) corners.Add(goal);
            else Funnel(start, goal, portalPath, corners);
            return true;
        }

        // maxHeightDiff: 영역 높이와 이만큼 차이 나도 그 영역 위로 봄 (기본은 몬스터 발 기준, 플레이어처럼 점프하는 대상은 크게)
        public NavRegion FindRegion(Vector3 p, NavRegion hint = null, float maxHeightDiff = MaxHeightDiff)
        {
            if (hint != null)
            {
                if (IsOn(hint, p, maxHeightDiff)) return hint;
                foreach (var portal in hint.portals)
                    if (IsOn(portal.to, p, maxHeightDiff)) return portal.to;
            }

            NavRegion best = null;
            float bestDy = float.MaxValue;
            foreach (var region in regions)
            {
                if (!region.ContainsXZ(p, InsideEps)) continue;
                float dy = Mathf.Abs(region.HeightAt(p.x, p.z) - p.y);
                if (dy < maxHeightDiff && dy < bestDy)
                {
                    best = region;
                    bestDy = dy;
                }
            }
            return best;
        }

        // Simple Stupid Funnel Algorithm
        // : 지나갈 연결점들을 왼쪽/오른쪽 끝점으로 나눈 뒤, 깔때기를 좁혀가며 꺾이는 코너만 남깁니다.
        private void Funnel(Vector3 start, Vector3 goal, List<NavPortal> path, List<Vector3> corners)
        {
            funnelLeft.Clear();
            funnelRight.Clear();
            funnelLeft.Add(start);
            funnelRight.Add(start);
            foreach (var p in path)
            {
                Vector3 dir = p.Mid - p.from.center;
                if (Cross(dir, p.a - p.Mid) > 0f)
                {
                    funnelLeft.Add(p.a);
                    funnelRight.Add(p.b);
                }
                else
                {
                    funnelLeft.Add(p.b);
                    funnelRight.Add(p.a);
                }
            }
            funnelLeft.Add(goal);
            funnelRight.Add(goal);

            Vector3 apex = start;
            Vector3 left = funnelLeft[0];
            Vector3 right = funnelRight[0];
            int apexIdx = 0, leftIdx = 0, rightIdx = 0;

            for (int i = 1; i < funnelLeft.Count; i++)
            {
                Vector3 l = funnelLeft[i];
                Vector3 r = funnelRight[i];

                // 오른쪽 경계 좁히기
                if (Side(apex, right, r) >= 0f)
                {
                    if (SameXZ(apex, right) || Side(apex, left, r) < 0f)
                    {
                        right = r;
                        rightIdx = i;
                    }
                    else
                    {
                        // 오른쪽이 왼쪽을 넘어감 → 왼쪽 끝이 코너
                        AddCorner(corners, left);
                        apex = left;
                        apexIdx = leftIdx;
                        left = right = apex;
                        leftIdx = rightIdx = apexIdx;
                        i = apexIdx;
                        continue;
                    }
                }

                // 왼쪽 경계 좁히기
                if (Side(apex, left, l) <= 0f)
                {
                    if (SameXZ(apex, left) || Side(apex, right, l) > 0f)
                    {
                        left = l;
                        leftIdx = i;
                    }
                    else
                    {
                        AddCorner(corners, right);
                        apex = right;
                        apexIdx = rightIdx;
                        left = right = apex;
                        leftIdx = rightIdx = apexIdx;
                        i = apexIdx;
                        continue;
                    }
                }
            }
            if (corners.Count > 0 && SameXZ(corners[corners.Count - 1], goal)) corners.RemoveAt(corners.Count - 1);
            corners.Add(goal);
        }

        private static void AddCorner(List<Vector3> corners, Vector3 p)
        {
            if (corners.Count > 0 && SameXZ(corners[corners.Count - 1], p)) return;
            corners.Add(p);
        }

        private bool IsOn(NavRegion region, Vector3 p, float maxHeightDiff)
        {
            return region.ContainsXZ(p, InsideEps) && Mathf.Abs(region.HeightAt(p.x, p.z) - p.y) < maxHeightDiff;
        }

        private static float Side(Vector3 apex, Vector3 p, Vector3 q) => Cross(p - apex, q - apex);

        private static bool SameXZ(Vector3 a, Vector3 b)
        {
            float dx = a.x - b.x;
            float dz = a.z - b.z;
            return dx * dx + dz * dz < 1e-6f;
        }

        private static void MergePolygons(List<List<int>> polys, List<Vector3> points, float concaveTolerance)
        {
            var edgeOwner = new Dictionary<long, int>();
            var candidates = new List<(int pa, int pb, int u, int v, float len)>();
            var touched = new HashSet<int>();

            bool mergedAny = true;
            while (mergedAny)
            {
                mergedAny = false;
                edgeOwner.Clear();
                candidates.Clear();
                touched.Clear();

                // 두 다각형이 공유하는 변 수집
                for (int p = 0; p < polys.Count; p++)
                {
                    var poly = polys[p];
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

                // 긴 변부터 지워야 영역이 덜 잘게 쪼개짐
                candidates.Sort((x, y) => y.len.CompareTo(x.len));

                foreach (var c in candidates)
                {
                    if (touched.Contains(c.pa) || touched.Contains(c.pb)) continue;

                    var merged = TryMerge(polys[c.pa], polys[c.pb], c.u, c.v, points, concaveTolerance);
                    if (merged == null) continue;

                    polys[c.pa] = merged;
                    polys[c.pb] = null;
                    touched.Add(c.pa);
                    touched.Add(c.pb);
                    mergedAny = true;
                }
            }
        }

        private static List<int> TryMerge(List<int> a, List<int> b, int u, int v, List<Vector3> points, float concaveTolerance)
        {
            int n = a.Count, m = b.Count;

            // a 에서는 x→y, b 에서는 y→x 순서로 공유 변이 나타남 (둘 다 반시계)
            int ia = -1;
            for (int i = 0; i < n; i++)
            {
                int p = a[i], q = a[(i + 1) % n];
                if ((p == u && q == v) || (p == v && q == u))
                {
                    ia = i;
                    break;
                }
            }
            if (ia < 0) return null;
            int x = a[ia], y = a[(ia + 1) % n];

            int jb = -1;
            for (int j = 0; j < m; j++)
            {
                if (b[j] == y && b[(j + 1) % m] == x)
                {
                    jb = j;
                    break;
                }
            }
            if (jb < 0) return null;

            var merged = new List<int>(n + m - 2);
            for (int k = 0; k < n; k++) merged.Add(a[(ia + 1 + k) % n]);   // y ... x
            for (int k = 2; k < m; k++) merged.Add(b[(jb + k) % m]);       // x 다음 ... y 이전

            if (new HashSet<int>(merged).Count != merged.Count) return null;

            // 같은 평면(바닥끼리, 경사로끼리)만 합침
            // : 합친 다각형의 평면(면적 가중이라 가는 조각에 흔들리지 않음)에서 모든 정점이 허용치 이내
            Vector3 normal = PlaneNormal(merged, points);
            Vector3 center = Vector3.zero;
            foreach (int idx in merged) center += points[idx];
            center /= merged.Count;
            foreach (int idx in merged)
                if (Mathf.Abs(Vector3.Dot(points[idx] - center, normal)) > PlaneTolerance) return null;

            // 볼록 검사: 모든 정점이 볼록 껍질 경계에서 허용치 이내여야 함 (작은 홈만 허용, 장애물을 감싸는 모양은 거부)
            if (MaxHullDeviation(merged, points) > concaveTolerance) return null;
            return IsSimple(merged, points) ? merged : null;
        }

        private void BuildPortals()
        {
            // 변끼리 기하학적으로 비교 (정점이 정확히 공유되지 않은 T자 접합도 처리)
            var edges = new List<(NavRegion region, Vector3 a, Vector3 b)>();
            foreach (var region in regions)
                for (int i = 0; i < region.verts.Length; i++)
                    edges.Add((region, region.verts[i], region.verts[(i + 1) % region.verts.Length]));

            // 위에서 본(XZ) 일직선 여부로 비교하고, 높이는 계단 정도 차이까지 허용
            // (같은 위치의 정점이 영역마다 높이가 조금 다르게 들어오는 경우가 있음. 예: 경사로 밑동)
            var overlaps = new Dictionary<long, (NavRegion r0, NavRegion r1, Vector3 a, Vector3 b)>();
            for (int i = 0; i < edges.Count; i++)
            {
                var ei = edges[i];
                Vector3 dir = ei.b - ei.a;
                dir.y = 0f;
                float len = dir.magnitude;
                if (len < 1e-4f) continue;
                dir /= len;

                for (int j = i + 1; j < edges.Count; j++)
                {
                    var ej = edges[j];
                    if (ej.region == ei.region) continue;
                    if (Mathf.Abs(Cross(dir, ej.a - ei.a)) > PortalLineTolerance) continue;
                    if (Mathf.Abs(Cross(dir, ej.b - ei.a)) > PortalLineTolerance) continue;

                    float t0 = DotXZ(ej.a - ei.a, dir);
                    float t1 = DotXZ(ej.b - ei.a, dir);
                    float lo = Mathf.Max(0f, Mathf.Min(t0, t1));
                    float hi = Mathf.Min(len, Mathf.Max(t0, t1));
                    if (hi - lo < MinPortalLength || Mathf.Abs(t1 - t0) < 1e-4f) continue;

                    // 겹치는 구간 양 끝에서 두 변의 높이 비교 (위층 가장자리와 아래 바닥처럼 높이가 다르면 연결 아님)
                    Vector3 pa = Vector3.Lerp(ei.a, ei.b, lo / len);
                    Vector3 pb = Vector3.Lerp(ei.a, ei.b, hi / len);
                    float ja = Mathf.Lerp(ej.a.y, ej.b.y, (lo - t0) / (t1 - t0));
                    float jb = Mathf.Lerp(ej.a.y, ej.b.y, (hi - t0) / (t1 - t0));
                    if (Mathf.Abs(pa.y - ja) > PortalHeightTolerance || Mathf.Abs(pb.y - jb) > PortalHeightTolerance) continue;

                    long key = EdgeKey(ei.region.index, ej.region.index);
                    if (overlaps.TryGetValue(key, out var prev))
                    {
                        // 같은 두 영역 사이의 일직선 조각들은 하나의 선분으로 합침
                        Vector3 d = (prev.b - prev.a).normalized;
                        float s0 = 0f, s1 = Vector3.Dot(prev.b - prev.a, d);
                        float s2 = Vector3.Dot(pa - prev.a, d), s3 = Vector3.Dot(pb - prev.a, d);
                        float min = Mathf.Min(Mathf.Min(s0, s1), Mathf.Min(s2, s3));
                        float max = Mathf.Max(Mathf.Max(s0, s1), Mathf.Max(s2, s3));
                        overlaps[key] = (prev.r0, prev.r1, prev.a + d * min, prev.a + d * max);
                    }
                    else
                    {
                        overlaps[key] = (ei.region, ej.region, pa, pb);
                    }
                }
            }

            PortalCount = 0;
            foreach (var o in overlaps.Values)
            {
                var forward = new NavPortal { id = PortalCount, dirId = PortalCount * 2, from = o.r0, to = o.r1, a = o.a, b = o.b };
                var backward = new NavPortal { id = PortalCount, dirId = PortalCount * 2 + 1, from = o.r1, to = o.r0, a = o.a, b = o.b };
                forward.reverse = backward;
                backward.reverse = forward;
                o.r0.portals.Add(forward);
                o.r1.portals.Add(backward);
                PortalCount++;
            }
        }

        private static float DotXZ(Vector3 a, Vector3 b) => a.x * b.x + a.z * b.z;

        private static readonly List<int> hullBuffer = new List<int>();
        private static readonly List<int> sortBuffer = new List<int>();

        // 볼록 껍질(XZ)에서 움푹 들어간 홈의 최대 깊이
        // - 껍질 꼭짓점 사이의 다각형 구간(홈)마다, 그 홈을 덮는 껍질 변으로부터 가장 깊은 정점까지의 거리
        // - (각 정점과 "가장 가까운" 껍질 변의 거리로 재면, 홈 바닥이 다른 변에 가까울 때 큰 홈을 놓침)
        private static float MaxHullDeviation(List<int> poly, List<Vector3> points)
        {
            int n = poly.Count;

            // Monotone chain 으로 볼록 껍질 계산 (다각형 안에서의 위치로 저장)
            sortBuffer.Clear();
            for (int i = 0; i < n; i++) sortBuffer.Add(i);
            sortBuffer.Sort((i, j) =>
            {
                Vector3 p = points[poly[i]], q = points[poly[j]];
                return p.x != q.x ? p.x.CompareTo(q.x) : p.z.CompareTo(q.z);
            });

            hullBuffer.Clear();
            for (int pass = 0; pass < 2; pass++)
            {
                int start = hullBuffer.Count;
                for (int s = 0; s < n; s++)
                {
                    int i = pass == 0 ? sortBuffer[s] : sortBuffer[n - 1 - s];
                    Vector3 p = points[poly[i]];
                    while (hullBuffer.Count >= start + 2)
                    {
                        Vector3 h1 = points[poly[hullBuffer[hullBuffer.Count - 1]]];
                        Vector3 h0 = points[poly[hullBuffer[hullBuffer.Count - 2]]];
                        if (Cross(h1 - h0, p - h0) > 0f) break;
                        hullBuffer.RemoveAt(hullBuffer.Count - 1);
                    }
                    hullBuffer.Add(i);
                }
                hullBuffer.RemoveAt(hullBuffer.Count - 1);
            }

            // 다각형 순서(반시계)로 걸으며, 껍질 꼭짓점 사이에 낀 정점들의 깊이 측정
            var onHull = new bool[n];
            foreach (int i in hullBuffer) onHull[i] = true;
            int first = -1;
            for (int i = 0; i < n; i++)
            {
                if (onHull[i])
                {
                    first = i;
                    break;
                }
            }
            if (first < 0) return float.MaxValue;

            float max = 0f;
            int lid = first;
            for (int step = 1; step <= n; step++)
            {
                int k = (first + step) % n;
                if (!onHull[k]) continue;

                // lid ~ k 사이의 정점들 = 홈
                Vector3 a = points[poly[lid]];
                Vector3 edge = points[poly[k]] - a;
                float len = Mathf.Sqrt(edge.x * edge.x + edge.z * edge.z);
                for (int m = (lid + 1) % n; m != k; m = (m + 1) % n)
                {
                    float depth = len > 1e-6f ? Mathf.Abs(Cross(edge, points[poly[m]] - a)) / len : (points[poly[m]] - a).magnitude;
                    max = Mathf.Max(max, depth);
                }
                lid = k;
            }
            return max;
        }

        // 오목함을 허용하므로 변끼리 교차(꼬임)하지 않는지 확인
        private static bool IsSimple(List<int> poly, List<Vector3> points)
        {
            int n = poly.Count;
            for (int i = 0; i < n; i++)
            {
                Vector3 a0 = points[poly[i]];
                Vector3 a1 = points[poly[(i + 1) % n]];
                for (int j = i + 2; j < n; j++)
                {
                    if (i == 0 && j == n - 1) continue;   // 이웃한 변
                    Vector3 b0 = points[poly[j]];
                    Vector3 b1 = points[poly[(j + 1) % n]];
                    float d1 = Cross(a1 - a0, b0 - a0), d2 = Cross(a1 - a0, b1 - a0);
                    float d3 = Cross(b1 - b0, a0 - b0), d4 = Cross(b1 - b0, a1 - b0);
                    if (d1 * d2 < 0f && d3 * d4 < 0f) return false;
                }
            }
            return true;
        }

        private static Vector3 PlaneNormal(List<int> poly, List<Vector3> points)
        {
            // Newell 방식
            Vector3 n = Vector3.zero;
            for (int i = 0; i < poly.Count; i++)
            {
                Vector3 cur = points[poly[i]];
                Vector3 next = points[poly[(i + 1) % poly.Count]];
                n.x += (cur.y - next.y) * (cur.z + next.z);
                n.y += (cur.z - next.z) * (cur.x + next.x);
                n.z += (cur.x - next.x) * (cur.y + next.y);
            }
            n.Normalize();
            return n.y < 0f ? -n : n;
        }

        private static long EdgeKey(int u, int v)
        {
            int min = Mathf.Min(u, v);
            int max = Mathf.Max(u, v);
            return ((long)min << 32) | (uint)max;
        }
    }
}
