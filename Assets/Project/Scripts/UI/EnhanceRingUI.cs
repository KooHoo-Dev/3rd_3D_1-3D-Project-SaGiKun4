using Study3D.FpsGame.Weapon;
using UnityEngine;

namespace Study3D.FpsGame.UI
{
    // EnhanceRingUI
    // : 크로스헤어 링을 3칸으로 나눠, 무기에 쌓인 강화 수만큼 칸을 칠합니다.
    // - 칸은 RingSegment 3개입니다. 칸 하나는 120°에서 틈(gapAngle)을 뺀 만큼 그립니다.
    //   칸 i는 맨 위에서 시계 방향으로 120° * i 부터 시작합니다. (Awake에서 자동으로 맞춤)
    // - 거리·틈은 여기서 한 번에 정합니다. 에디터에서 값을 바꾸면 바로 반영됩니다. (OnValidate)
    //   두께는 각 RingSegment가 직접 가집니다. (여기서 덮어쓰지 않음)
    // - 강화 수가 바뀔 때(EnhanceStack.OnChanged)만 색을 다시 칠합니다.
    // - 가득 차면 모든 칸을 최대색으로 칠해 더 쌓을 수 없다는 것을 알립니다.
    public class EnhanceRingUI : MonoBehaviour
    {
        private const float SEGMENT_ANGLE = 360f / EnhanceStack.MAX_COUNT; // 칸 하나가 차지하는 각도

        [SerializeField] private Weapon.Weapon weapon;
        [Tooltip("링 칸. 맨 위에서 시계 방향 순서로 넣습니다 (EnhanceStack.MAX_COUNT개)")]
        [SerializeField] private RingSegment[] segments;
        [Tooltip("칸 사이 틈 (도)")]
        [SerializeField] private float gapAngle = 12f;
        [Tooltip("크로스헤어 가운데에서 링까지의 거리 (캔버스 단위, 부모 스케일을 그대로 받음)")]
        [SerializeField] private float radius = 10f;

        [Header("색")]
        [SerializeField] private Color emptyColor = new Color(1f, 1f, 1f, 0.3f);
        [SerializeField] private Color filledColor = new Color(1f, 0.8f, 0.2f, 1f);
        [SerializeField] private Color fullColor = new Color(1f, 0.3f, 0.2f, 1f);

        private void Awake()
        {
            SetupSegments();
        }

        private void OnValidate()
        {
            if (segments == null)
                return;

            SetupSegments();
        }

        private void OnEnable()
        {
            weapon.EnhanceStack.OnChanged += Refresh;
            Refresh();
        }

        private void OnDisable()
        {
            weapon.EnhanceStack.OnChanged -= Refresh;
        }

        // 칸마다 시작 각도, 길이, 거리를 맞춥니다. 틈의 절반만큼 밀어서 틈이 칸 양쪽에 고르게 생기게 합니다.
        private void SetupSegments()
        {
            float arcAngle = SEGMENT_ANGLE - gapAngle;
            for (int i = 0; i < segments.Length; i++)
            {
                if (segments[i] == null)
                    continue;

                float startAngle = SEGMENT_ANGLE * i + gapAngle * 0.5f;
                segments[i].SetShape(startAngle, arcAngle, radius);
            }
        }

        private void Refresh()
        {
            int count = weapon.EnhanceStack.Count;
            Color onColor = filledColor;
            if (count >= EnhanceStack.MAX_COUNT)
            {
                onColor = fullColor;
            }

            for (int i = 0; i < segments.Length; i++)
            {
                if (i < count)
                {
                    segments[i].color = onColor;
                }
                else
                {
                    segments[i].color = emptyColor;
                }
            }
        }
    }
}
