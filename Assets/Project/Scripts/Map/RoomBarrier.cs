using UnityEngine;

namespace Study3D.FpsGame.Map
{
    // RoomBarrier
    // : 방 통로를 막는 차단막 입니다. 닫히면 바닥 위로 올라오고, 열리면 바닥 아래로 내려갑니다.
    // - 지금은 RoomModule 이 Cube 를 만들어 붙이지만, 나중에 실제 모델에 직접 붙여도 됩니다.
    //   (직접 붙일 때는 "닫힌 위치" 에 배치해 두면 그 위치를 기준으로 travel 만큼 내려갑니다)
    // - 완전히 열린 동안은 렌더러와 콜라이더를 꺼서 그리기/충돌 비용을 없앱니다.
    public class RoomBarrier : MonoBehaviour
    {
        [Tooltip("열릴 때 내려가는 거리. 0 이면 렌더러 높이로 계산")]
        [SerializeField] private float travel;
        [Tooltip("열고 닫는 데 걸리는 시간 (초)")]
        [SerializeField] private float moveTime = 0.4f;

        private Vector3 closedPosition;
        private bool initialized;
        private bool closed;
        private float progress;   // 0 = 완전히 열림, 1 = 완전히 닫힘
        private Renderer[] renderers;
        private Collider[] colliders;

        public bool IsClosed => closed;

        private void Awake()
        {
            if (!initialized)
            {
                Setup(transform.localPosition, travel);
            }
        }

        // closedLocalPosition: 닫혔을 때 위치 (부모 기준), travelDistance: 열릴 때 내려갈 거리
        public void Setup(Vector3 closedLocalPosition, float travelDistance)
        {
            renderers = GetComponentsInChildren<Renderer>();
            colliders = GetComponentsInChildren<Collider>();
            closedPosition = closedLocalPosition;

            if (travelDistance <= 0f)
            {
                travelDistance = 3f;
                if (renderers.Length > 0)
                {
                    Bounds bounds = renderers[0].bounds;
                    foreach (Renderer r in renderers)
                    {
                        bounds.Encapsulate(r.bounds);
                    }
                    travelDistance = bounds.size.y;
                }
            }
            // 바닥과 겹쳐 깜빡이지 않도록 살짝 더 내림
            travel = travelDistance + 0.05f;
            initialized = true;
            SetClosed(closed, true);
        }

        public void Close()
        {
            SetClosed(true, false);
        }

        public void Open()
        {
            SetClosed(false, false);
        }

        public void SetClosed(bool value, bool immediate)
        {
            closed = value;
            if (immediate)
            {
                progress = closed ? 1f : 0f;
            }
            Apply();
            enabled = !immediate;
        }

        private void Update()
        {
            float target = closed ? 1f : 0f;
            progress = Mathf.MoveTowards(progress, target, Time.deltaTime / Mathf.Max(moveTime, 0.01f));
            Apply();
            if (Mathf.Approximately(progress, target))
            {
                enabled = false;
            }
        }

        private void Apply()
        {
            transform.localPosition = closedPosition + Vector3.down * (travel * (1f - progress));

            // 움직이는 중에는 보이고 막혀 있어야 함. 완전히 열렸을 때만 끔
            bool visible = progress > 0f;
            foreach (Renderer r in renderers)
            {
                r.enabled = visible;
            }
            foreach (Collider c in colliders)
            {
                c.enabled = visible;
            }
        }
    }
}
