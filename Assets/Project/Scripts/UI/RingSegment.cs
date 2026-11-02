using UnityEngine;
using UnityEngine.UI;

namespace Study3D.FpsGame.UI
{
    // RingSegment
    // : 링(도넛)의 한 조각(호)을 스프라이트 없이 직접 그리는 UI 입니다. 크로스헤어와의 거리와 두께를 따로 정할 수 있습니다.
    // - 거리: radius (RectTransform 가운데 ~ 띠 가운데) / 두께: thickness. 둘 다 캔버스 단위이고 부모 스케일을 그대로 받습니다.
    //   띠는 radius를 기준으로 안쪽·바깥쪽에 두께의 절반씩 그리므로, 두께를 바꿔도 거리는 그대로입니다.
    // - 각도는 맨 위가 0°이고 시계 방향으로 늘어납니다.
    // - 그리는 방법: 호를 stepAngle 간격으로 나누고, 나눈 각도마다 안쪽 점·바깥쪽 점을 하나씩 만듭니다.
    //   이웃한 두 각도의 점 4개로 사각형(삼각형 2개)을 만들어 이어 붙이면 휘어진 띠가 됩니다.
    // - 모양이 바뀔 때(SetShape, 크기·색 변경)만 다시 그립니다.
    [RequireComponent(typeof(CanvasRenderer))]
    public class RingSegment : MaskableGraphic
    {
        [Tooltip("시작 각도 (맨 위 0°, 시계 방향)")]
        [SerializeField] private float startAngle = 0f;
        [Tooltip("호의 길이 (도)")]
        [SerializeField] private float arcAngle = 108f;
        [Tooltip("가운데에서 띠 가운데까지의 거리 (캔버스 단위)")]
        [SerializeField] private float radius = 10f;
        [Tooltip("링 두께 (캔버스 단위)")]
        [SerializeField] private float thickness = 1f;
        [Tooltip("호를 나누는 간격 (도). 작을수록 매끄럽고 정점이 늘어납니다")]
        [SerializeField] private float stepAngle = 6f;

        // 두께는 바꾸지 않습니다. (세그먼트마다 인스펙터에서 정함)
        public void SetShape(float newStartAngle, float newArcAngle, float newRadius)
        {
            startAngle = newStartAngle;
            arcAngle = newArcAngle;
            radius = newRadius;
            SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();

            Vector2 center = rectTransform.rect.center;
            float halfThickness = thickness * 0.5f;
            float innerRadius = Mathf.Max(0f, radius - halfThickness);
            float outerRadius = radius + halfThickness;

            int stepCount = Mathf.Max(1, Mathf.CeilToInt(arcAngle / stepAngle));
            for (int i = 0; i <= stepCount; i++)
            {
                float angle = startAngle + arcAngle * i / stepCount;
                AddEdgeVertices(vh, center, angle, innerRadius, outerRadius);

                if (i > 0)
                {
                    AddQuad(vh, i);
                }
            }
        }

        // angle 방향으로 안쪽 점(번호 i * 2)과 바깥쪽 점(번호 i * 2 + 1)을 넣습니다.
        private void AddEdgeVertices(VertexHelper vh, Vector2 center, float angle, float innerRadius, float outerRadius)
        {
            // 맨 위 0°, 시계 방향이라 x는 sin, y는 cos 입니다.
            float radian = angle * Mathf.Deg2Rad;
            Vector2 direction = new Vector2(Mathf.Sin(radian), Mathf.Cos(radian));

            vh.AddVert(center + direction * innerRadius, color, Vector2.zero);
            vh.AddVert(center + direction * outerRadius, color, Vector2.zero);
        }

        // 이전 각도(step - 1)와 이번 각도(step)의 점 4개로 사각형을 만듭니다.
        private void AddQuad(VertexHelper vh, int step)
        {
            int prevInner = (step - 1) * 2;
            int prevOuter = prevInner + 1;
            int inner = step * 2;
            int outer = inner + 1;

            vh.AddTriangle(prevInner, prevOuter, outer);
            vh.AddTriangle(prevInner, outer, inner);
        }
    }
}
