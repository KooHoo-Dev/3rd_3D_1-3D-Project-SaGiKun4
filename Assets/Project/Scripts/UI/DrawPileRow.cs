using TMPro;
using UnityEngine;

namespace Study3D.FpsGame.UI
{
    // DrawPileRow
    // : 남은 덱 목록의 칸 하나(템플릿)에 붙여 글자 참조를 들고 있습니다. DrawPileUIBuilder가 조립하면서 참조를 넣어 줍니다.
    // - 복제(Instantiate)하면 참조도 복제본 안의 오브젝트로 바뀌어 있으므로, 이름으로 찾지 않고 바로 씁니다.
    public class DrawPileRow : MonoBehaviour
    {
        [SerializeField] private TMP_Text text;

        public TMP_Text Text => text;

#if UNITY_EDITOR
        // DrawPileUIBuilder가 조립 직후 호출합니다.
        public void EditorSetup(TMP_Text rowText)
        {
            text = rowText;
        }
#endif
    }
}
