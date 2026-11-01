using UnityEngine;
using UnityEngine.UI;

namespace Study3D.FpsGame.UI
{
    // CardListGrid
    // : 메뉴 화면 카드 목록(CardListView)의 열 수를 목록 폭에 맞춥니다. GridLayoutGroup과 같은 오브젝트(목록 Content)에 붙입니다.
    // - 카드 크기(cellSize)는 그대로 두고 한 줄에 들어가는 장수만 바꿉니다. (카드 비율·글자 크기 유지)
    // - 폭이 바뀔 때만 다시 계산하므로 매 프레임 비용은 없습니다.
    [RequireComponent(typeof(GridLayoutGroup))]
    public class CardListGrid : MonoBehaviour
    {
        [SerializeField, Min(1)] private int minColumns = 1;

        private GridLayoutGroup grid;
        private RectTransform rect;

        private void Awake()
        {
            grid = GetComponent<GridLayoutGroup>();
            rect = (RectTransform)transform;
        }

        private void OnEnable()
        {
            UpdateColumns();
        }

        private void OnRectTransformDimensionsChange()
        {
            UpdateColumns();
        }

        private void UpdateColumns()
        {
            if (grid == null)
                return;

            float width = rect.rect.width - grid.padding.horizontal;
            float step = grid.cellSize.x + grid.spacing.x;
            // 마지막 카드 뒤에는 간격이 없으므로 폭에 간격 하나를 더해서 나눕니다
            int fitColumns = Mathf.FloorToInt((width + grid.spacing.x + 0.01f) / step);
            int columns = Mathf.Max(minColumns, fitColumns);

            // 같은 값을 넣어도 레이아웃이 다시 계산되므로 바뀔 때만 넣습니다
            if (grid.constraintCount != columns)
            {
                grid.constraintCount = columns;
            }
        }
    }
}
