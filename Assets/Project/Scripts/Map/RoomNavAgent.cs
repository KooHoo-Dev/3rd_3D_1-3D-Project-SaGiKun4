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
    // - 높이는 걸음마다 서 있는 영역의 바닥 평면에 맞춥니다. (경사로, 계단, 위층으로 이어진 길을 따라감. 몬스터 피벗 = 발밑 전제)
    // - 밀어내기: 방의 RoomCrowd 에 등록하고, 추격 이동 중에만 겹친 이웃에게서 멀어지는 쪽으로 더 밀림
    //   (공격, 대기 중인 몬스터는 밀리지 않고 이웃으로만 쓰임. 밀린 자리가 지도 밖이면 밀림 없이 이동)
    //   반경 = Enemy 의 MainCollider 반경
    [RequireComponent(typeof(Enemy.Enemy))]
    public class RoomNavAgent : MonoBehaviour, IEnemyMover
    {
        private const float RoomSearchInterval = 0.5f; // 지도 밖일 때 서 있는 방을 다시 찾는 간격
        private const float LosInterval = 0.25f;       // 목표가 보이는지 확인하는 간격
        private const float PortalInset = 0.1f;        // 연결점 양 끝(장애물 모서리)에서 띄울 거리
        private const float HeightTolerance = 1f;      // 영역 높이와 이만큼 차이 나도 그 영역 위로 봄 (몬스터 발 기준)
        private const float DefaultRadius = 0.5f;      // MainCollider 가 없을 때 반경

        [Tooltip("밀려나는 최대 속도 = 이동 속도 × 이 값 (겹친 정도에 비례). 0 이면 밀지 않음")]
        [SerializeField, Range(0f, 2f)] private float separationWeight = 1f;

        public float Radius { get; private set; }

        public bool IsSolid // 죽는 중이면 콜라이더가 꺼짐
        {
            get { return enemy.MainCollider != null && enemy.MainCollider.enabled; }
        }

        internal int CrowdIndex { get; set; } = -1;

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
            if (enemy.MainCollider != null)
            {
                Radius = ColliderRadius(enemy.MainCollider);
            }
            else
            {
                Radius = DefaultRadius;
            }
            losTimer = Random.Range(0f, LosInterval); // 몬스터마다 시야 확인 타이밍을 흩어 둠
        }

        // 위에서 본 콜라이더 반경 (스케일 포함)
        private static float ColliderRadius(Collider target)
        {
            Vector3 scale = target.transform.lossyScale;
            float scaleXZ = Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.z));

            CapsuleCollider capsule = target as CapsuleCollider;
            if (capsule != null)
                return capsule.radius * scaleXZ;

            SphereCollider sphere = target as SphereCollider;
            if (sphere != null)
                return sphere.radius * Mathf.Max(scaleXZ, Mathf.Abs(scale.y));

            BoxCollider box = target as BoxCollider;
            if (box != null)
                return Mathf.Max(box.size.x * Mathf.Abs(scale.x), box.size.z * Mathf.Abs(scale.z)) * 0.5f;

            CharacterController controller = target as CharacterController;
            if (controller != null)
                return controller.radius * scaleXZ;

            return Mathf.Max(target.bounds.extents.x, target.bounds.extents.z);
        }

        // 풀에서 다시 꺼내지면 새 위치에서 바로 방을 찾음
        private void OnEnable()
        {
            roomTimer = 0f;
            losVisible = false;
        }

        private void OnDisable()
        {
            LeaveRoom();
        }

        private void Update()
        {
            if (room != null && room.isActiveAndEnabled)
                return;

            roomTimer -= GameTime.DeltaTime;
            if (roomTimer > 0f)
                return;

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
                if (found < 0)
                    continue;

                if (room != candidate)
                {
                    if (room != null)
                    {
                        room.Crowd.Remove(this);
                    }
                    candidate.Crowd.Add(this);
                }
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
            if (room != null)
            {
                room.Crowd.Remove(this);
            }
            room = null;
            region = -1;
            if (enemy != null)
            {
                enemy.SetMover(null);
            }
        }

        // IEnemyMover: Enemy 의 추격 상태에서 매 프레임 호출됩니다. 길찾기 방향(월드, 수평)을 돌려줍니다. (밀린 방향을 보면 몸이 흔들려서)
        public Vector3 Move(float deltaTime, float speed)
        {
            if (room == null || room.TryGetGoal(out int goal, out Vector3 goalPoint) == false)
                return Vector3.zero;

            Vector3 pos = room.transform.InverseTransformPoint(transform.position);
            Vector3 target = ChooseTarget(deltaTime, pos, goal, goalPoint, out bool towardPortal);

            // 목표 지점 쪽으로 이번 프레임 거리만큼 (XZ 로 움직이고, 높이는 도착한 영역의 바닥에 맞춤)
            Vector3 to = target - pos;
            to.y = 0f;
            float dist = to.magnitude;
            if (dist < 0.0001f)
                return Vector3.zero;

            float step = speed * deltaTime;
            bool arrived = dist <= step;
            Vector3 dir = to / dist;
            Vector3 next;
            if (arrived)
            {
                next = new Vector3(target.x, pos.y, target.z);
            }
            else
            {
                next = pos + dir * step;
            }

            if (towardPortal && arrived)
            {
                // 연결점 위의 점 = 두 영역의 경계. 도착하면 다음 영역에 들어간 것으로 처리 → 다음 프레임에 다음 연결점 선택
                enteredPortal = portal;
                region = room.GetPortal(portal).to;
                portal = -1;
            }
            else
            {
                // 이웃과 겹쳐 있으면 멀어지는 쪽으로 더 밀림. 밀린 자리가 지도 밖이면 밀림 없이
                // (연결점에 도착하는 걸음은 영역 전환이 정확해야 해서 밀지 않음)
                if (TryStepOnMap(deltaTime, speed, ref next) == false)
                    return Vector3.zero;
            }

            // 경사로, 계단을 따라 오르내리도록 발을 영역 바닥 높이에 붙임
            next.y = room.HeightAt(region, next);

            transform.position = room.transform.TransformPoint(next);
            return room.transform.TransformDirection(dir);
        }

        // 밀림을 더한 걸음(next)이 지도 위면 서 있는 영역을 갱신하고 true. 지도 밖이면 길찾기로 전환하고 false
        private bool TryStepOnMap(float deltaTime, float speed, ref Vector3 next)
        {
            int found = -1;
            Vector3 push = GetPush(deltaTime, speed);
            if (push != Vector3.zero)
            {
                found = room.FindRegion(next + push, region, HeightTolerance);
                if (found >= 0)
                {
                    next += push;
                }
            }

            // 지도 밖으로 나가는 걸음은 하지 않음 (시야 확인 사이에 목표가 장애물 뒤로 간 경우 등) → 바로 길찾기로 전환
            if (found < 0)
            {
                found = room.FindRegion(next, region, HeightTolerance);
            }
            if (found < 0)
            {
                losVisible = false;
                losTimer = LosInterval;
                ResetPath();
                return false;
            }

            region = found;
            return true;
        }

        // 이웃과 겹친 만큼 멀어지는 이번 프레임 이동량 (방 로컬). 이동 속도 × separationWeight 를 넘지 않음
        private Vector3 GetPush(float deltaTime, float speed)
        {
            if (separationWeight <= 0f)
                return Vector3.zero;

            Vector3 separation = room.Crowd.GetSeparation(this);
            if (separation.sqrMagnitude < 1e-6f)
                return Vector3.zero;

            separation = Vector3.ClampMagnitude(separation, 1f) * (speed * separationWeight * deltaTime);
            return room.transform.InverseTransformDirection(separation);
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
                if (losVisible && visible == false)
                {
                    ResetPath(); // 보이다가 가려지면 지금 위치 기준으로 다시 고름
                }
                losVisible = visible;
            }
            if (losVisible)
                return goalPoint;

            // 3. 표: 내 영역이나 목표 영역이 바뀌었을 때만 다시 고름
            if (portal < 0 || region != lookupRegion || goal != lookupGoal)
            {
                // 방금 연결점을 넘어 이 영역에 들어왔다면(목표 그대로), 그 연결점보다 남은 거리가 작은 출구만 후보
                float maxRemain = float.MaxValue;
                bool justEntered = enteredPortal >= 0 && room.GetPortal(enteredPortal).to == region && goal == lookupGoal;
                if (justEntered)
                {
                    maxRemain = room.RemainDistance(enteredPortal, goal);
                }

                lookupRegion = region;
                lookupGoal = goal;
                portal = room.ChooseNextPortal(region, goal, pos, maxRemain);
            }
            if (portal < 0)
                return pos; // 갈 길이 없음

            towardPortal = true;
            return CrossingPoint(room.GetPortal(portal), pos);
        }

        // 연결점 선분 위에서 나와 가장 가까운 점 (양 끝 = 장애물 모서리는 살짝 피함)
        private static Vector3 CrossingPoint(RoomNavRegions.Portal portalLine, Vector3 pos)
        {
            Vector3 ab = portalLine.b - portalLine.a;
            float length = ab.magnitude;
            float inset = Mathf.Min(PortalInset, length * 0.25f) / Mathf.Max(length, 1e-4f);
            float t = Vector3.Dot(pos - portalLine.a, ab) / Mathf.Max(length * length, 1e-6f);
            return portalLine.a + ab * Mathf.Clamp(t, inset, 1f - inset);
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
