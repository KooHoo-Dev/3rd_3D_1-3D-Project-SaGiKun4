using TMPro;
using UnityEngine;

namespace Study3D.FpsGame.UI
{
    // CardTag
    // : 카드 옆에 붙는 강화 태그 한 개입니다. 복제해서 쓰는 태그 템플릿에 붙여 글자 참조를 들고 있습니다.
    // - 복제하면 참조도 복제본 안의 글자로 바뀌어 있으므로, 이름으로 찾지 않고 바로 씁니다.
    public class CardTag : MonoBehaviour
    {
        [SerializeField] private TMP_Text label;

#if UNITY_EDITOR
        // UpgradeUIBuilder가 조립 직후 호출합니다.
        public void EditorSetup(TMP_Text text)
        {
            label = text;
        }
#endif

        public void SetText(string text)
        {
            label.text = text;
        }
    }
}
