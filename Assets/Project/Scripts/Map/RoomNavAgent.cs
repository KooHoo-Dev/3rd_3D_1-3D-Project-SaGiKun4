using Study3D.FpsGame.Enemy;
using UnityEngine;

namespace Study3D.FpsGame.Map
{
    // RoomNavAgent
    // : 적의 추격 이동을 방 지도(RoomNavRegions)로 처리합니다. (Enemy 와 같은 오브젝트에 붙임)
    // - 서 있는 방을 찾으면 Enemy.SetMover(this) 로 추격 이동을 맡고, 지도 밖이면 SetMover(null) 로 기존 직진으로 돌려놓습니다.
    // - 상태(대기/추격/공격/피격/사망)와 회전은 Enemy 가 그대로 처리하고, 여기서는 추격 중 "어디로 한 걸음 갈지" 만 정합니다.
    // - 이동 순서 (프로토타입 NavTestAgent 와 같음)
    //   1. 목표와 같은 영역이면 직진 (볼록 영역이라 막힘 없음)
    //   2. 0.25초마다 지도 위 시야 판정. 목표가 보이면 직진
    //   3. 아니면 표로 다음 연결점을 골라, 그 선분 위 나와 가장 가까운 점으로 이동. 도착하면 다음 영역에 들어간 것으로 처리
    // - 목표 = 플레이어가 마지막으로 지도 위에 있던 지점 (사물 위로 올라가면 올라가기 직전 지점)
    // - 좌표는 모두 방 로컬로 바꿔서 계산합니다. (지도가 방 로컬 기준이라서)
    [RequireComponent(typeof(Enemy.Enemy))]
    public class RoomNavAgent : MonoBehaviour, IEnemyMover
    {
        private const float RoomSearchInterval = 0.5f; // 지도 밖일 때 서 있는 방을 다시 찾는 간격
        private const float LosInterval = 0.25f;       // 목표가 보이는지 확인하는 간격
        private const float PortalInset = 0.1f;        // 연결점 양 끝(장애물 모서리)에서 띄울 거리
        private const float HeightTolerance = 1f;      // 영역 높이와 이만큼 차이 나도 그 영역 위로 봄 (몬스터 발 기준)

        private Enemy.Enemy enemy;
        private RoomNavRegions room;
        private int region = -1;
        private float roomTimer;

        // 표 이동: 지금 향하는 연결점, 그 연결점을 고른 영역과 목표, 마지막으로 넘어온 연결점
        private int portal = -1;
        private int lookupRegion = -1;
        private int lookupGoal = -1;
        private int enteredPortal = -1;

        // 시야 직진
        private float losTimer;
        private bool losVisible;

        private void Awake()
        {
            enemy = GetComponent<Enemy.Enemy>();
            losTimer = Random.Range(0f, LosInterval); // 몬스터마다 시야 확인 타이밍을 흩어 둠
        }

        private void OnDisable()
        {
            LeaveRoom();
        }

        private void Update()
        {
            if (room != null && room.isActiveAndEnabled) return;

            roomTimer -= Time.deltaTime;
            if (roomTimer > 0f) return;
            roomTimer = RoomSearchInterval;
            FindRoom();
        }

        // 켜져 있는 방들 중 내가 서 있는 영역이 있는 방을 찾습니다.
        private void FindRoom()
        {
            foreach (RoomNavRegions candidate in RoomNavRegions.Active)
            {
                Vector3 local = candidate.transform.InverseTransformPoint(transform.position);
                int found = candidate.FindRegion(local, -1, HeightTolerance);
                if (found < 0) continue;

                room = candidate;
                region = found;
                ResetPath();
                enemy.SetMover(this);
                return;
            }
            LeaveRoom();
        }

        private void LeaveRoom()
        {
            room = null;
            region = -1;
            if (enemy != null) enemy.SetMover(null);
        }

        // IEnemyMover: Enemy 의 추격 상태에서 매 프레임 호출됩니다. 이동한 수평 방향(월드)을 돌려줍니다.
        public Vector3 Move(float deltaTime, float speed)
        {
            if (room == null || room.TryGetGoal(out int goal, out Vector3 goalPoint) == false) return Vector3.zero;

            Vector3 pos = room.transform.InverseTransformPoint(transform.position);
            Vector3 target = ChooseTarget(deltaTime, pos, goal, goalPoint, out bool towardPortal);

            // 목표 지점 쪽으로 이번 프레임 거리만큼 (높이는 그대로: 평평한 방 전제)
            Vector3 to = target - pos;
            to.y = 0f;
            float dist = to.magnitude;
            if (dist < 0.0001f) return Vector3.zero;

            float step = speed * deltaTime;
            bool arrived = dist <= step;
            Vector3 next = arrived ? new Vector3(target.x, pos.y, target.z) : pos + to / dist * step;

            if (towardPortal && arrived)
            {
                // 연결점 위의 점 = 두 영역의 경계. 도착하면 다음 영역에 들어간 것으로 처리 → 다음 프레임에 다음 연결점 선택
                enteredPortal = portal;
                region = room.GetPortal(portal).to;
                portal = -1;
            }
            else
            {
                // 지도 밖으로 나가는 걸음은 하지 않음 (시야 확인 사이에 목표가 장애물 뒤로 간 경우 등) → 바로 길찾기로 전환
                int found = room.FindRegion(next, region, HeightTolerance);
                if (found < 0)
                {
                    losVisible = false;
                    losTimer = LosInterval;
                    ResetPath();
                    return Vector3.zero;
                }
                region = found;
            }

            Vector3 before = transform.position;
            transform.position = room.transform.TransformPoint(next);
            Vector3 moved = transform.position - before;
            moved.y = 0f;
            return moved;
        }

        // 이번 프레임에 향할 지점 (방 로컬). 연결점으로 가는 중이면 towardPortal = true
        private Vector3 ChooseTarget(float deltaTime, Vector3 pos, int goal, Vector3 goalPoint, out bool towardPortal)
        {
            towardPortal = false;

            // 1. 목표와 같은 영역
            if (region == goal)
            {
                ResetPath();
                return goalPoint;
            }

            // 2. 시야 직진
            losTimer -= deltaTime;
            if (losTimer <= 0f)
            {
                losTimer = LosInterval;
                bool visible = room.HasLineOfSight(pos, region, goalPoint, goal);
                if (losVisible && visible == false) ResetPath(); // 보이다가 가려지면 지금 위치 기준으로 다시 고름
                losVisible = visible;
            }
            if (losVisible) return goalPoint;

            // 3. 표: 내 영역이나 목표 영역이 바뀌었을 때만 다시 고름
            if (portal < 0 || region != lookupRegion || goal != lookupGoal)
            {
                // 방금 연결점을 넘어 이 영역에 들어왔다면(목표 그대로), 그 연결점보다 남은 거리가 작은 출구만 후보
                float maxRemain = enteredPortal >= 0 && room.GetPortal(enteredPortal).to == region && goal == lookupGoal
                    ? room.RemainDistance(enteredPortal, goal)
                    : float.MaxValue;

                lookupRegion = region;
                lookupGoal = goal;
                portal = room.ChooseNextPortal(region, goal, pos, maxRemain);
            }
            if (portal < 0) return pos; // 갈 길이 없음

            towardPortal = true;
            return CrossingPoint(room.GetPortal(portal), pos);
        }

        // 연결점 선분 위에서 나와 가장 가까운 점 (양 끝 = 장애물 모서리는 살짝 피함)
        private static Vector3 CrossingPoint(RoomNavRegions.Portal p, Vector3 pos)
        {
            Vector3 ab = p.b - p.a;
            float length = ab.magnitude;
            float inset = Mathf.Min(PortalInset, length * 0.25f) / Mathf.Max(length, 1e-4f);
            float t = Vector3.Dot(pos - p.a, ab) / Mathf.Max(length * length, 1e-6f);
            return p.a + ab * Mathf.Clamp(t, inset, 1f - inset);
        }

        private void ResetPath()
        {
            portal = -1;
            lookupRegion = -1;
            lookupGoal = -1;
            enteredPortal = -1;
        }
    }
}
