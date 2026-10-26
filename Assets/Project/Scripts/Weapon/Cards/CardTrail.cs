using UnityEngine;

namespace Study3D.FpsGame.Weapon.Cards
{
    // CardTrail
    // : 날아가는 카드 뒤에, 카드가 지나온 궤적을 따라가는 꼬리선을 LineRenderer로 그립니다.
    // - 시작점은 카드 뒤쪽 끝(카드와 맞닿은 부분)이고, 시작점 너비는 카드 너비와 같으며 끝으로 갈수록 가늘어집니다.
    // - 길이는 카드 길이 × lengthRatio(기본 2배) 입니다. 카드 크기는 매 프레임 실제 크기로 계산하므로 Size 배율로 커지면 꼬리선도 커집니다.
    // - 직선이 아니라 지나온 점들을 이어 그리므로, 반사로 꺾이면 꼬리선도 함께 꺾입니다.
    // - 색은 카드 색의 색조를 유지한 파스텔 색(원본보다 어둡지 않음)이고, 시작점은 불투명하며 끝으로 갈수록 투명해집니다.
    // - 풀에서 다시 꺼내 켜질 때(OnEnable) 이전 궤적을 지웁니다. (Projectile.Spawn이 위치를 잡은 뒤에 켜므로 새 위치부터 시작)
    // - 꼬리선은 카메라를 바라보지 않고 카드 면과 같은 평면에 눕습니다. (카드처럼 옆에서 보면 얇게 보입니다)
    //   LineRenderer의 Alignment를 TransformZ로 두면 선이 그 Transform의 Z축을 향하므로,
    //   Z축이 카드의 위쪽(면의 법선)을 향하는 자식 오브젝트(Trail)에 LineRenderer를 붙입니다.
    // - 자식은 카드의 납작한 비균일 스케일을 물려받아 꼬리선 너비가 틀어지므로,
    //   자식의 localScale을 카드 스케일의 역수로 두어 월드 기준 스케일이 1이 되도록 상쇄합니다.
    public class CardTrail : MonoBehaviour
    {
        private const int MAX_POINTS = 64;

        [Tooltip("꼬리선 길이 = 카드 길이 × 이 값. 너무 짧아 안 보이거나 길어서 시야를 가리지 않는 범위로 제한합니다")]
        [SerializeField, Range(1.5f, 2.5f)] private float lengthRatio = 2.0f;
        [Tooltip("파스텔 채도 상한. 카드 색의 채도가 이보다 높으면 이 값으로 낮춥니다 (낮을수록 연한 색)")]
        [SerializeField, Range(0f, 1f)] private float pastelSaturation = 0.45f;
        [Tooltip("파스텔 밝기 하한. 카드 색의 밝기가 이보다 낮으면 이 값으로 올립니다 (원본보다 어두워지지는 않음)")]
        [SerializeField, Range(0f, 1f)] private float pastelBrightness = 0.95f;
        [SerializeField] private float minPointDistance = 0.1f;   // 이만큼 움직였을 때만 지나온 점을 추가합니다.
        [Tooltip("꼬리선 머티리얼 (정점 색을 쓰는 셰이더). 비우면 Sprites/Default로 만듭니다")]
        [SerializeField] private Material lineMaterial;

        private static Material defaultMaterial;

        private LineRenderer line;
        private Vector3 meshSize = Vector3.one; // 스케일 1일 때 카드 메시 크기
        // 지나온 점들 (카드 중심 위치). [0]이 가장 오래된 점, [count - 1]이 가장 최근 점 입니다.
        private readonly Vector3[] points = new Vector3[MAX_POINTS];
        // LineRenderer에 넘길 점들. [0]이 시작점(카드 뒤쪽 끝), 뒤로 갈수록 꼬리 끝 입니다.
        private readonly Vector3[] drawPoints = new Vector3[MAX_POINTS + 2];
        private int count;

        private void Awake()
        {
            if (TryGetComponent(out MeshFilter meshFilter) && meshFilter.sharedMesh != null)
                meshSize = meshFilter.sharedMesh.bounds.size;

            // 자식 Trail의 Z축 = 카드의 위쪽. (X축으로 -90도 돌리면 앞(+Z)이 위(+Y)를 향합니다)
            Transform trail = new GameObject("Trail").transform;
            trail.SetParent(transform, false);
            trail.localRotation = Quaternion.Euler(-90f, 0f, 0f);
            line = trail.gameObject.AddComponent<LineRenderer>();

            line.alignment = LineAlignment.TransformZ; // 카메라가 아니라 Trail의 Z축(카드 면의 법선)을 향합니다.
            line.useWorldSpace = true; // 점 위치는 카드의 크기, 회전과 상관없이 월드 좌표로 그립니다.
            line.positionCount = 0;
            line.widthCurve = AnimationCurve.Linear(0f, 1f, 1f, 0f); // 시작점 = 카드 너비, 끝 = 0
            line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            line.receiveShadows = false;

            if (lineMaterial == null)
            {
                // 풀의 카드마다 새로 만들지 않도록 한 번 만든 것을 같이 씁니다.
                if (defaultMaterial == null) defaultMaterial = new Material(Shader.Find("Sprites/Default"));
                lineMaterial = defaultMaterial;
            }
            line.sharedMaterial = lineMaterial;

            Color color = ToPastel(GetCardColor());
            line.startColor = new Color(color.r, color.g, color.b, 1f); // 시작점은 불투명
            line.endColor = new Color(color.r, color.g, color.b, 0f);   // 끝으로 갈수록 투명
        }

        private void OnEnable()
        {
            count = 0;
            AddPoint(transform.position);
            if (line != null) line.positionCount = 0;
        }

        // 카드가 이번 프레임 이동을 마친 뒤에 그립니다.
        private void LateUpdate()
        {
            Vector3 head = transform.position;
            if ((head - points[count - 1]).sqrMagnitude >= minPointDistance * minPointDistance) AddPoint(head);

            // 지금 크기 기준의 카드 너비와 길이 (Size 배율 반영)
            Vector3 scale = transform.lossyScale;

            // 카드 스케일 상쇄: Trail은 X축으로 -90도 돌아 있어서 Trail의 (X, Y, Z)축이 카드의 (X, Z, Y)축과 겹칩니다.
            // 각 축을 카드 스케일의 역수로 두면 Trail의 월드 스케일이 1이 되어, 꼬리선 너비가 카드 스케일에 휘둘리지 않습니다.
            // (Size 배율로 카드 스케일이 바뀔 수 있어서 매 프레임 맞춥니다)
            line.transform.localScale = new Vector3(1f / scale.x, 1f / scale.z, 1f / scale.y);
            float cardWidth = meshSize.x * scale.x;
            float cardLength = meshSize.z * scale.z;

            line.widthMultiplier = cardWidth;
            Draw(head, cardLength * 0.5f, cardLength * lengthRatio);
        }

        private void AddPoint(Vector3 point)
        {
            // 가득 차면 가장 오래된 점을 버립니다. (꼬리선보다 한참 뒤의 점이라 그려지지 않는 점)
            if (count == MAX_POINTS)
            {
                System.Array.Copy(points, 1, points, 0, MAX_POINTS - 1);
                count--;
            }
            points[count++] = point;
        }

        // 카드 중심에서 지나온 궤적을 거꾸로 따라가며, startOffset(카드 반 길이)만큼 건너뛴 곳부터 length만큼 그립니다.
        // - 궤적의 점은 카드 중심 위치라서, 몸통 길이만큼 건너뛰지 않으면 꼬리선이 카드 중심에서 나오는 것처럼 보입니다.
        private void Draw(Vector3 head, float startOffset, float length)
        {
            int drawCount = 0;
            float skip = startOffset;
            float remaining = length;
            Vector3 previous = head;

            for (int i = count - 1; i >= 0 && remaining > 0f; i--)
            {
                Vector3 next = points[i];
                float distance = Vector3.Distance(previous, next);
                if (distance <= 0f) continue;

                Vector3 direction = (next - previous) / distance;
                float travelled = 0f;

                // 아직 카드 몸통 구간이면 건너뜁니다. 이 구간 안에서 몸통이 끝나면 그 지점이 시작점입니다.
                if (skip > 0f)
                {
                    if (distance <= skip)
                    {
                        skip -= distance;
                        previous = next;
                        continue;
                    }

                    travelled = skip;
                    skip = 0f;
                }

                // 시작점은 궤적 위의 점이 아니라 카드 뒤쪽 끝 모서리의 정확한 위치로 둡니다.
                // (반사 직후처럼 궤적과 카드 방향이 어긋나도 꼬리선이 항상 카드 끝에 붙어 있도록)
                if (drawCount == 0) drawPoints[drawCount++] = head - transform.forward * startOffset;

                float available = distance - travelled;
                if (available >= remaining)
                {
                    drawPoints[drawCount++] = previous + direction * (travelled + remaining);
                    remaining = 0f;
                    break;
                }

                drawPoints[drawCount++] = next;
                remaining -= available;
                previous = next;
            }

            // 아직 카드 길이만큼도 날아가지 않았으면 그릴 것이 없습니다.
            line.positionCount = drawCount >= 2 ? drawCount : 0;
            if (drawCount >= 2) line.SetPositions(drawPoints);
        }

        // 1차: 카드 색에서 색조(Hue)를 뽑습니다. 2차: 파스텔에 맞게 채도는 상한까지 낮추고, 밝기는 하한까지 올립니다.
        // - 채도를 낮추고 밝기를 올리기만 하므로 원본보다 어두워지지 않습니다.
        // - 원본이 이미 충분히 연하고 밝으면(채도 ≤ 상한, 밝기 ≥ 하한) 원본 색 그대로 입니다.
        private Color ToPastel(Color source)
        {
            Color.RGBToHSV(source, out float hue, out float saturation, out float brightness);
            saturation = Mathf.Min(saturation, pastelSaturation);
            brightness = Mathf.Max(brightness, pastelBrightness);
            return Color.HSVToRGB(hue, saturation, brightness);
        }

        // 카드 머티리얼의 색 (URP Lit은 _BaseColor, 그 밖에는 _Color)
        private Color GetCardColor()
        {
            if (TryGetComponent(out MeshRenderer meshRenderer) == false || meshRenderer.sharedMaterial == null) return Color.white;

            Material material = meshRenderer.sharedMaterial;
            if (material.HasProperty("_BaseColor")) return material.GetColor("_BaseColor");
            if (material.HasProperty("_Color")) return material.GetColor("_Color");
            return Color.white;
        }
    }
}
