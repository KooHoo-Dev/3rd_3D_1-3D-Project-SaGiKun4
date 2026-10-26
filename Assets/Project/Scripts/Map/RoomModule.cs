using System;
using Study3D.FpsGame.Player;
using UnityEngine;
using UnityEngine.Events;

namespace Study3D.FpsGame.Map
{
    // RoomLayout
    // : 방 하나의 배치용 정보 입니다. (모두 방 루트 로컬 기준)
    // - ModularMapGenerator 가 방을 실제로 만들기 전에 이 정보만으로 회전/위치/겹침을 계산합니다.
    public struct RoomLayout
    {
        public struct GateInfo
        {
            public Vector3 position;     // 문 오브젝트 피벗
            public Quaternion rotation;  // 문 오브젝트 회전
            public Vector3 center;       // 통로 중앙 (문 메시 중앙, 바닥 높이)
        }

        public Bounds bounds;            // 메시 기준 방 범위
        public GateInfo entry;
        public GateInfo exit;
    }

    // RoomModule
    // : 모듈식 맵의 방 하나 입니다. (방 루트에 붙임. 없으면 생성기가 배치할 때 붙여 줌)
    // - 입구/출구 = GateWay 아래에 있는 문 오브젝트. 비워두면 태그(StartGateWay / EndGateWay)로 찾습니다.
    // - 플레이 중: 플레이어가 방 안쪽 트리거에 들어오면 차단막을 올리고 전투 시작 → Clear() 로 연결된 통로를 다시 엶
    // - 연결되지 않은 통로(첫 방 입구, 마지막 방 출구)는 항상 막혀 있습니다.
    public class RoomModule : MonoBehaviour
    {
        public const string EntryTag = "StartGateWay";
        public const string ExitTag = "EndGateWay";

        public enum RoomState { Idle, Combat, Cleared }

        [Serializable]
        public class Gate
        {
            [Tooltip("통로 문 오브젝트. 비워두면 태그로 찾음")]
            public Transform door;
            [Tooltip("차단막. 비워두면 플레이 시 Cube 로 생성")]
            public RoomBarrier barrier;
            [Tooltip("다른 방과 이어져 있는지. 꺼져 있으면 항상 막힘 (생성기가 설정)")]
            public bool connected = true;
        }

        [Header("통로")]
        [SerializeField] private Gate entry = new Gate();
        [SerializeField] private Gate exit = new Gate();

        [Header("차단막 (Cube 자동 생성)")]
        [Tooltip("폭(문 벽 방향), 높이, 두께")]
        [SerializeField] private Vector3 barrierSize = new Vector3(4f, 4f, 0.5f);

        [Header("전투")]
        [Tooltip("방 범위에서 이만큼 안쪽에 들어와야 전투 시작 (통로에 서 있을 때 갇히지 않도록)")]
        [SerializeField] private float triggerInset = 3f;
        [SerializeField] private float triggerHeight = 3f;
        [Tooltip("켜면 전투 없이 시작 (시작 방 등)")]
        [SerializeField] private bool startCleared;
        [SerializeField] private UnityEvent onCombatStart = new UnityEvent();
        [SerializeField] private UnityEvent onCleared = new UnityEvent();

        public RoomState State { get; private set; }
        public Gate Entry => entry;
        public Gate Exit => exit;
        public UnityEvent OnCombatStart => onCombatStart;
        public UnityEvent OnCleared => onCleared;

        public bool StartCleared
        {
            get => startCleared;
            set => startCleared = value;
        }

        private void Start()
        {
            FindGates();
            RoomLayout layout = CalculateLayout(gameObject);
            CreateBarrier(entry, layout.entry);
            CreateBarrier(exit, layout.exit);
            CreateTrigger(layout);

            State = startCleared ? RoomState.Cleared : RoomState.Idle;
            SetBarriers(false, true);
        }

        // 비어 있는 문을 태그로 찾아 채움 (꺼진 오브젝트 포함)
        public void FindGates()
        {
            if (entry.door == null) entry.door = FindTagged(transform, EntryTag);
            if (exit.door == null) exit.door = FindTagged(transform, ExitTag);
        }

        private void OnTriggerEnter(Collider other)
        {
            if (State != RoomState.Idle) return;
            if (other.GetComponentInParent<LocalPlayer>() == null) return;

            State = RoomState.Combat;
            SetBarriers(true, false);
            onCombatStart.Invoke();
        }

        // 전투 종료. 전투 시스템(웨이브 등)이 방의 적을 다 처리했을 때 호출
        [ContextMenu("전투 종료 (테스트)")]
        public void Clear()
        {
            if (State != RoomState.Combat) return;

            State = RoomState.Cleared;
            SetBarriers(false, false);
            onCleared.Invoke();
        }

        // closeAll: true 면 모두 닫음, false 면 연결된 통로만 엶
        private void SetBarriers(bool closeAll, bool immediate)
        {
            foreach (var gate in new[] { entry, exit })
                if (gate.barrier != null) gate.barrier.SetClosed(closeAll || !gate.connected, immediate);
        }

        private void CreateBarrier(Gate gate, RoomLayout.GateInfo info)
        {
            if (gate.barrier != null || gate.door == null) return;

            var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube.name = "Barrier_" + gate.door.tag;
            // 문 오브젝트는 스케일이 있어서 방 루트 바로 아래에 두고, 회전만 문과 맞춤 (폭 = 문 벽 방향)
            cube.transform.SetParent(transform, false);
            cube.transform.localRotation = info.rotation;
            cube.transform.localScale = barrierSize;

            gate.barrier = cube.AddComponent<RoomBarrier>();
            gate.barrier.Setup(info.center + Vector3.up * (barrierSize.y * 0.5f), barrierSize.y);
        }

        private void CreateTrigger(RoomLayout layout)
        {
            var box = gameObject.AddComponent<BoxCollider>();
            box.isTrigger = true;
            Bounds b = layout.bounds;
            float floorY = layout.entry.position.y;
            box.center = new Vector3(b.center.x, floorY + triggerHeight * 0.5f, b.center.z);
            box.size = new Vector3(
                Mathf.Max(b.size.x - triggerInset * 2f, 1f),
                triggerHeight,
                Mathf.Max(b.size.z - triggerInset * 2f, 1f));
        }

        // 방 원본(프리팹 에셋 포함)에서 배치용 정보를 계산합니다.
        // - Renderer.bounds 는 에셋 상태에서 쓸 수 없으므로 MeshFilter 의 메시 범위를 루트 로컬로 옮겨서 합칩니다.
        public static RoomLayout CalculateLayout(GameObject root)
        {
            var module = root.GetComponent<RoomModule>();
            var layout = new RoomLayout { bounds = MeshBounds(root.transform, root.transform, true) };
            layout.entry = CalculateGate(root.transform, module != null ? module.entry.door : null, EntryTag);
            layout.exit = CalculateGate(root.transform, module != null ? module.exit.door : null, ExitTag);
            return layout;
        }

        private static RoomLayout.GateInfo CalculateGate(Transform root, Transform door, string tag)
        {
            // 프리팹 에셋을 건드리지 않도록 여기서는 찾기만 함 (저장은 FindGates 에서)
            if (door == null) door = FindTagged(root, tag);
            if (door == null) throw new InvalidOperationException($"{root.name}: 태그 '{tag}' 인 통로 문을 찾을 수 없습니다.");

            var info = new RoomLayout.GateInfo
            {
                position = root.InverseTransformPoint(door.position),
                rotation = Quaternion.Inverse(root.rotation) * door.rotation,
            };

            // 문 피벗은 벽 끝에 있으므로 통로 중앙은 문 메시 범위의 중앙
            Bounds doorBounds = MeshBounds(root, door, false);
            info.center = doorBounds.size == Vector3.zero ? info.position : doorBounds.center;
            info.center.y = info.position.y;
            return info;
        }

        // target 아래 메시들의 범위를 root 로컬 기준으로 계산
        private static Bounds MeshBounds(Transform root, Transform target, bool skipBarriers)
        {
            Matrix4x4 toRoot = root.worldToLocalMatrix;
            Bounds bounds = default;
            bool hasBounds = false;
            foreach (var mf in target.GetComponentsInChildren<MeshFilter>(true))
            {
                if (mf.sharedMesh == null) continue;
                if (skipBarriers && mf.GetComponent<RoomBarrier>() != null) continue;

                Bounds mb = mf.sharedMesh.bounds;
                Matrix4x4 m = toRoot * mf.transform.localToWorldMatrix;
                for (int i = 0; i < 8; i++)
                {
                    Vector3 corner = mb.center + Vector3.Scale(mb.extents, new Vector3(
                        (i & 1) == 0 ? -1f : 1f, (i & 2) == 0 ? -1f : 1f, (i & 4) == 0 ? -1f : 1f));
                    Vector3 p = m.MultiplyPoint3x4(corner);
                    if (hasBounds) bounds.Encapsulate(p);
                    else
                    {
                        bounds = new Bounds(p, Vector3.zero);
                        hasBounds = true;
                    }
                }
            }
            return bounds;
        }

        public static Transform FindTagged(Transform parent, string tag)
        {
            foreach (Transform child in parent)
            {
                if (child.CompareTag(tag)) return child;
                Transform found = FindTagged(child, tag);
                if (found != null) return found;
            }
            return null;
        }

        private void OnDrawGizmosSelected()
        {
            RoomLayout layout;
            try
            {
                layout = CalculateLayout(gameObject);
            }
            catch (InvalidOperationException)
            {
                return;
            }

            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireCube(layout.bounds.center, layout.bounds.size);

            DrawGate(layout.entry, Color.green);
            DrawGate(layout.exit, Color.red);
        }

        // 차단막이 생길 자리
        private void DrawGate(RoomLayout.GateInfo info, Color color)
        {
            Gizmos.color = color;
            Matrix4x4 prev = Gizmos.matrix;
            Gizmos.matrix = prev * Matrix4x4.TRS(info.center + Vector3.up * (barrierSize.y * 0.5f), info.rotation, Vector3.one);
            Gizmos.DrawWireCube(Vector3.zero, barrierSize);
            Gizmos.matrix = prev;
        }
    }
}
