using System.Collections.Generic;
using Study3D.FpsGame.Player;
using UnityEngine;

namespace Study3D.FpsGame.UI
{
    // MinimapView
    // : 화면 왼쪽 위 원형 지도에 "내 위치 기준으로 적이 어디 있는지"를 빨간 점으로 찍습니다.
    // - 맵 그림은 쓰지 않습니다. 원 가운데 = 나, 원 위쪽 = 내가 바라보는 방향 입니다. (내가 돌면 점들이 반대로 돕니다)
    // - 적 목록은 CombatSystem에 등록된 몬스터를 그대로 씁니다. (죽은 적은 등록이 풀려 있으므로 자동으로 빠짐)
    // - 점은 필요한 만큼만 만들고, 남는 점은 꺼 두었다가 다시 씁니다.
    //
    // 계산 순서 (적 한 명마다)
    // 1. 적 위치 - 내 위치 = 나에게서 적까지의 화살표 (높이는 버리고 바닥 평면만 봄)
    // 2. 그 화살표를 내 오른쪽 방향, 내 앞 방향으로 얼마나 가는지로 나눔 → (x = 오른쪽으로 몇 m, y = 앞으로 몇 m)
    // 3. m 단위를 지도 픽셀 단위로 바꿈 (지도 반지름 / 보여줄 거리)
    // 4. 보여줄 거리보다 멀면 숨기거나, 원 테두리에 붙여서 방향만 알려줌
    public class MinimapView : MonoBehaviour
    {
        [Tooltip("점들을 넣을 부모 (원 가운데에 앵커가 있어야 합니다)")]
        [SerializeField] private RectTransform dotParent;
        [Tooltip("적 점으로 복사해서 쓸 원본 (꺼 둔 상태)")]
        [SerializeField] private RectTransform dotTemplate;
        [Tooltip("지도 원의 반지름 (UI 픽셀)")]
        [SerializeField] private float mapRadius = 94f;

        [Header("범위")]
        [Tooltip("지도 가장자리까지의 실제 거리 (m)")]
        [SerializeField] private float worldRange = 30f;
        [Tooltip("범위 밖의 적을 원 테두리에 붙여서 보여줄지 (끄면 숨김)")]
        [SerializeField] private bool showOutOfRangeOnEdge = true;

        private readonly List<RectTransform> dots = new List<RectTransform>();

        private void Awake()
        {
            dotTemplate.gameObject.SetActive(false);
        }

        private void LateUpdate()
        {
            int shownCount = 0;
            LocalPlayer player = Player.Player.LocalPlayer;
            if (player != null && CombatSystem.Instance != null)
            {
                shownCount = DrawEnemies(player.transform);
            }
            HideDotsFrom(shownCount);
        }

        // 적마다 점 하나씩 찍고, 찍은 점의 수를 돌려줍니다.
        private int DrawEnemies(Transform player)
        {
            Vector3 right = FlattenDirection(player.right);
            Vector3 forward = FlattenDirection(player.forward);
            float pixelPerMeter = mapRadius / worldRange;

            int count = 0;
            foreach (IDamageable monster in CombatSystem.Instance.Monsters)
            {
                Vector3 toEnemy = monster.GameObject.transform.position - player.position;
                toEnemy.y = 0f;

                // 오른쪽으로 몇 m, 앞으로 몇 m
                Vector2 local = new Vector2(Vector3.Dot(toEnemy, right), Vector3.Dot(toEnemy, forward));
                if (local.magnitude > worldRange)
                {
                    if (showOutOfRangeOnEdge == false)
                        continue;

                    local = local.normalized * worldRange;
                }

                RectTransform dot = GetDot(count);
                dot.anchoredPosition = local * pixelPerMeter;
                count++;
            }
            return count;
        }

        // 높이를 버린 바닥 방향 (길이 1)
        private static Vector3 FlattenDirection(Vector3 direction)
        {
            direction.y = 0f;
            return direction.normalized;
        }

        // index번째 점을 켜서 돌려줍니다. 모자라면 원본을 복사해 새로 만듭니다.
        private RectTransform GetDot(int index)
        {
            if (index >= dots.Count)
            {
                RectTransform newDot = Instantiate(dotTemplate, dotParent);
                dots.Add(newDot);
            }

            RectTransform dot = dots[index];
            if (dot.gameObject.activeSelf == false)
            {
                dot.gameObject.SetActive(true);
            }
            return dot;
        }

        // 이번 프레임에 쓰지 않은 점을 끕니다.
        private void HideDotsFrom(int startIndex)
        {
            for (int i = startIndex; i < dots.Count; i++)
            {
                if (dots[i].gameObject.activeSelf)
                {
                    dots[i].gameObject.SetActive(false);
                }
            }
        }

#if UNITY_EDITOR
        public void EditorSetup(RectTransform dotParent, RectTransform dotTemplate, float mapRadius)
        {
            this.dotParent = dotParent;
            this.dotTemplate = dotTemplate;
            this.mapRadius = mapRadius;
        }
#endif
    }
}
