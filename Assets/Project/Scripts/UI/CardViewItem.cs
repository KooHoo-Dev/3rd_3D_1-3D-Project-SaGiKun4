using UnityEngine;

namespace Study3D.FpsGame.UI
{
    // CardViewItem
    // : 런타임에 복제해서 쓰는 카드 한 장(템플릿)에 붙여 CardView 참조를 들고 있습니다.
    // - 복제(Instantiate)하면 참조도 복제본 안의 오브젝트로 바뀌어 있으므로, 이름으로 찾지 않고 바로 씁니다.
    public class CardViewItem : MonoBehaviour
    {
        [SerializeField] private CardView view;

        public CardView View => view;

#if UNITY_EDITOR
        // UpgradeUIBuilder가 조립 직후 호출합니다.
        public void EditorSetup(CardView cardView)
        {
            view = cardView;
        }
#endif
    }
}
