using System.Collections.Generic;
using Study3D.FpsGame.Enemy;
using UnityEngine;

namespace Study3D.FpsGame.NavTest
{
    // NavTestAgent
    // : 데모용 몬스터의 길찾기/이동 입니다. (다음 연결점 표 방식)
    // - 캡슐: 각자 Update 를 돌지 않고 NavTestDemo 가 한 번에 Tick 을 호출합니다.
    // - Enemy: Enemy 의 추격 상태가 IEnemyMover.Move 로 호출합니다. (속도는 Enemy 값, 회전도 Enemy 가 처리)
    // - 이동 순서
    //   1. 목표가 보이면(네비메시 시야 판정) 경로 무시하고 직진
    //   2. 목표와 같은 영역이면 직진
    //   3. 아니면 표로 다음 연결점을 골라 그쪽으로 (Funnel 다듬기를 켜면 표를 따라 코너 목록을 만들어 따라감)
    public class NavTestAgent : MonoBehaviour, IEnemyMover
    {
        private const float PortalInset = 0.1f;     // 연결점 양 끝(장애물 모서리)에서 띄울 거리
        private const float LosInterval = 0.25f;    // 목표가 보이는지 확인하는 간격

        private static readonly Color PathColor = new Color(1f, 0.75f, 0.2f);      // 표를 따라 이동 중
        private static readonly Color DirectColor = new Color(0.2f, 0.8f, 1f);     // 목표가 보여서 직진 중

        private NavTestDemo demo;
        private Renderer bodyRenderer;   // 캡슐일 때만 (상태별 색 표시)
        private NavRegion region;
        private float speed;
        private bool rotateSelf = true;

        // Enemy 가 움직이는 몬스터인지 (데모가 직접 Tick 하지 않음)
        public bool IsEnemyDriven { get; private set; }

        // 따라갈 점 목록 (다음 연결점 위의 점, Funnel 코너들, 혹은 목표)
        private readonly List<Vector3> corners = new List<Vector3>();
        private int cornerIndex;

        // 표 조회 (마지막으로 조회한 시점의 내 영역 / 목표 영역 / 고른 연결점)
        private NavRegion lookupRegion;
        private NavRegion lookupGoalRegion;
        private NavPortal lookupPortal;

        // Funnel 다듬기: 코너 목록을 만든 시점의 목표 영역
        private NavRegion pathGoalRegion;

        // 시야 직진
        private float losTimer;
        private bool losVisible;

        public void Init(NavTestDemo owner, Renderer body)
        {
            demo = owner;
            bodyRenderer = body;
            speed = demo.MoveSpeed;
            region = demo.Graph.FindRegion(transform.position);
            ResetState();
        }

        // Enemy 에 붙일 때: 추격 이동만 맡고, 속도와 회전은 Enemy 를 따름
        public void InitAsEnemy(NavTestDemo owner, Enemy.Enemy enemy)
        {
            demo = owner;
            IsEnemyDriven = true;
            rotateSelf = false;
            region = demo.Graph.FindRegion(transform.position);
            ResetState();
            enemy.SetMover(this);
        }

        // IEnemyMover: Enemy 의 추격 상태에서 매 프레임 호출됨
        public Vector3 Move(float deltaTime, float moveSpeed)
        {
            speed = moveSpeed;
            Vector3 before = transform.position;
            Tick(deltaTime);
            Vector3 moved = transform.position - before;
            moved.y = 0f;
            return moved;
        }

        // 다른 몬스터에게 밀려날 때 (네비메시 밖으로는 밀리지 않음)
        public void Push(Vector3 offset)
        {
            Vector3 next = transform.position + offset;
            NavRegion found = demo.Graph.FindRegion(next, region);
            if (found == null) return;

            region = found;
            next.y = region.HeightAt(next.x, next.z);
            transform.position = next;
        }

        public void ResetState()
        {
            ResetPath();
            losVisible = false;
            losTimer = Random.Range(0f, LosInterval);   // 몬스터마다 시야 확인 타이밍 분산
            UpdateColor();
        }

        public void Tick(float dt)
        {
            if (region == null || demo.GoalRegion == null) return;

            if (demo.LosChase && TickLineOfSight(dt)) return;

            if (demo.TableFunnel) TickTableFunnel(dt);
            else TickTable(dt);
        }

        // 시야 직진: 일정 간격으로 네비메시 시야 판정, 보이는 동안은 목표로 직진. 직진 중이면 true
        private bool TickLineOfSight(float dt)
        {
            losTimer -= dt;
            if (losTimer <= 0f)
            {
                losTimer = LosInterval;
                bool visible = region == demo.GoalRegion || demo.CheckLineOfSight(transform.position, region);

                // 보이다가 가려지면 길찾기를 처음부터 (표 조회를 지금 위치 기준으로 다시)
                if (losVisible && !visible) ResetPath();
                SetVisible(visible);
            }
            if (!losVisible) return false;

            corners.Clear();
            corners.Add(demo.GoalPosition);
            cornerIndex = 0;

            // 확인 사이에 목표가 장애물 뒤로 가서 직진하면 네비메시 밖으로 나가게 되면,
            // 이번 확인 간격 동안은 길찾기로 이동 (매 프레임 다시 확인하며 제자리에 멈추지 않도록)
            Vector3 before = transform.position;
            MoveTowards(demo.GoalPosition, dt, false);
            if (transform.position == before && !IsAtGoal())
            {
                SetVisible(false);
                losTimer = LosInterval;
                ResetPath();
                return false;
            }
            return true;
        }

        // 표 이동 (Funnel 없음)
        // 1. 목표와 같은 영역 → 직진
        // 2. 아니면 표로 다음 연결점을 고르고, 그 선분 위 나와 가장 가까운 점으로 이동 (도착 = 다음 영역 진입)
        // 3. 내 영역이나 목표 영역이 바뀔 때만 다시 고름
        private void TickTable(float dt)
        {
            NavRegion goalRegion = demo.GoalRegion;
            if (region == goalRegion)
            {
                lookupRegion = null;
                corners.Clear();
                corners.Add(demo.GoalPosition);
                cornerIndex = 0;
            }
            else if (region != lookupRegion || goalRegion != lookupGoalRegion || cornerIndex >= corners.Count)
            {
                // 방금 연결점을 넘어 이 영역에 들어왔다면(목표 영역은 그대로), 그 연결점보다 남은 거리가 작은 출구만 후보
                // → 남은 거리가 매번 줄어들어 되돌아가거나 빙빙 돌 수 없음
                float maxRemain = lookupPortal != null && lookupPortal.to == region && goalRegion == lookupGoalRegion
                    ? demo.Graph.RemainDistance(lookupPortal, goalRegion)
                    : float.MaxValue;

                lookupRegion = region;
                lookupGoalRegion = goalRegion;
                corners.Clear();
                cornerIndex = 0;

                lookupPortal = demo.LookupNextPortal(region, goalRegion, transform.position, maxRemain);
                if (lookupPortal != null) corners.Add(CrossingPoint(lookupPortal));
            }

            if (cornerIndex < corners.Count && MoveTowards(corners[cornerIndex], dt, true))
            {
                cornerIndex++;
                // 연결점 위의 점 = 두 영역의 경계. 도착하면 다음 영역에 들어간 것으로 처리 → 다음 프레임에 다음 연결점 선택
                if (region != demo.GoalRegion && lookupPortal != null && region == lookupPortal.from) region = lookupPortal.to;
            }
        }

        // 표 이동 (Funnel 다듬기)
        // : 표를 따라 지나갈 연결점을 모아 Funnel 로 코너를 만듦. 목표 영역이 바뀔 때만 다시 만듦 (탐색 없음)
        private void TickTableFunnel(float dt)
        {
            if (pathGoalRegion != demo.GoalRegion || corners.Count == 0)
            {
                demo.RequestTablePath(transform.position, region, corners);
                cornerIndex = 0;
                pathGoalRegion = demo.GoalRegion;
            }
            else
            {
                corners[corners.Count - 1] = demo.GoalPosition;
            }

            if (cornerIndex < corners.Count && MoveTowards(corners[cornerIndex], dt, true))
                cornerIndex++;
        }

        // 연결점 선분 위에서 나와 가장 가까운 점 (양 끝 = 장애물 모서리는 살짝 피함)
        private Vector3 CrossingPoint(NavPortal portal)
        {
            Vector3 ab = portal.b - portal.a;
            float len = ab.magnitude;
            float inset = Mathf.Min(PortalInset, len * 0.25f) / Mathf.Max(len, 1e-4f);
            float t = Vector3.Dot(transform.position - portal.a, ab) / Mathf.Max(len * len, 1e-6f);
            return portal.a + ab * Mathf.Clamp(t, inset, 1f - inset);
        }

        private void ResetPath()
        {
            corners.Clear();
            cornerIndex = 0;
            pathGoalRegion = null;
            lookupRegion = null;
            lookupGoalRegion = null;
            lookupPortal = null;
        }

        private bool IsAtGoal()
        {
            Vector3 d = demo.GoalPosition - transform.position;
            return d.x * d.x + d.z * d.z < 0.0001f;
        }

        private void SetVisible(bool visible)
        {
            if (losVisible == visible) return;
            losVisible = visible;
            UpdateColor();
        }

        private void UpdateColor()
        {
            if (bodyRenderer != null) bodyRenderer.sharedMaterial = demo.GetColorMaterial(losVisible ? DirectColor : PathColor);
        }

        // 목표 지점에 도착하면 true. allowOffMesh 가 false 면 네비메시 밖으로는 이동하지 않음
        private bool MoveTowards(Vector3 target, float dt, bool allowOffMesh)
        {
            Vector3 pos = transform.position;
            Vector3 to = target - pos;
            to.y = 0f;
            float dist = to.magnitude;
            float step = speed * dt;

            bool arrived = dist <= step;
            Vector3 next = arrived ? new Vector3(target.x, pos.y, target.z) : pos + to / dist * step;

            NavRegion found = demo.Graph.FindRegion(next, region);
            if (found == null && !allowOffMesh) return false;
            if (found != null) region = found;

            next.y = region.HeightAt(next.x, next.z);
            transform.position = next;
            if (rotateSelf && dist > 0.001f) transform.rotation = Quaternion.LookRotation(to);
            return arrived;
        }

        public void DrawGizmoPath()
        {
            if (cornerIndex >= corners.Count) return;

            Gizmos.color = losVisible ? DirectColor : PathColor;
            Vector3 lift = Vector3.up * 0.1f;
            Vector3 prev = transform.position + lift;
            for (int i = cornerIndex; i < corners.Count; i++)
            {
                Vector3 c = corners[i] + lift;
                Gizmos.DrawLine(prev, c);
                Gizmos.DrawWireSphere(c, 0.08f);
                prev = c;
            }
        }
    }
}
