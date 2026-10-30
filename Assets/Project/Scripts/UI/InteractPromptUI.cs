using Study3D.FpsGame.Interaction;
using TMPro;
using UnityEngine;

namespace Study3D.FpsGame.UI
{
    // InteractPromptUI
    // : 현재 상호작용 타겟 위에 "[F] 행동" 프롬프트를 띄웁니다. 화면에 하나만 두고 타겟을 따라다닙니다.
    // - 문구는 타겟이 바뀔 때만 갱신하고, 위치만 매 프레임 옮깁니다. (TMP 텍스트를 매 프레임 바꾸면 메시를 다시 만듦)
    // - 위치: 타겟의 PromptPosition(월드) → 화면 좌표(viewCamera) → 캔버스 좌표. 카메라 뒤에 있으면 숨깁니다.
    //   promptRoot는 화면 전체를 덮는 부모의 가운데에 앵커를 둡니다. (화면 가운데 기준 좌표를 그대로 넣음)
    // - 카메라가 LateUpdate에서 움직이므로, 그보다 늦게 실행되도록 실행 순서를 뒤로 둡니다.
    // - 조작할 수 없는 동안(강화 화면 등)은 숨깁니다.
    [DefaultExecutionOrder(100)]
    public class InteractPromptUI : MonoBehaviour
    {
        [SerializeField] private PlayerInteractor interactor;
        [Tooltip("월드를 그리는 카메라 (월드 → 화면 변환용). 캔버스의 UI 카메라와 다를 수 있습니다")]
        [SerializeField] private Camera viewCamera;
        [SerializeField] private Canvas canvas;
        [Tooltip("켜고 끌 프롬프트 루트 (이 컴포넌트가 붙은 오브젝트가 아닌 자식)")]
        [SerializeField] private RectTransform promptRoot;
        [SerializeField] private TMP_Text keyText;
        [SerializeField] private TMP_Text actionText;
        [SerializeField] private TMP_Text nameText;

        private Interactable target;

        private void Awake()
        {
            promptRoot.gameObject.SetActive(false);
        }

        private void OnEnable()
        {
            interactor.OnTargetChanged += SetTarget;
        }

        private void OnDisable()
        {
            interactor.OnTargetChanged -= SetTarget;
        }

        private void SetTarget(Interactable newTarget)
        {
            target = newTarget;
            if (target == null) return;

            keyText.SetText(interactor.InteractKey.ToString());
            actionText.SetText(target.ActionText);
            nameText.SetText(target.DisplayName);
        }

        private void LateUpdate()
        {
            bool visible = target != null && Player.Player.LocalPlayer != null && Player.Player.LocalPlayer.CanAct;
            Vector3 screenPoint = Vector3.zero;
            if (visible)
            {
                screenPoint = viewCamera.WorldToScreenPoint(target.PromptPosition);
                // z가 0보다 작으면 카메라 뒤에 있다는 뜻이라 그리지 않습니다.
                visible = screenPoint.z > 0f;
            }

            if (promptRoot.gameObject.activeSelf != visible) promptRoot.gameObject.SetActive(visible);
            if (visible == false) return;

            // 화면 좌표 → 캔버스 좌표를 캔버스의 월드 위치 없이 계산합니다. (화면 가운데 = 0, 캔버스 단위 = 픽셀 / scaleFactor)
            // ScreenPointToLocalPointInRectangle은 캔버스 평면의 월드 위치를 쓰는데, Screen Space - Camera 캔버스는
            // 렌더링 직전에야 카메라를 따라 옮겨집니다. 이 시점엔 아직 지난 프레임 위치라, 카메라가 움직인 만큼 어긋나 떨립니다.
            Vector2 halfScreen = new Vector2(Screen.width, Screen.height) * 0.5f;
            promptRoot.anchoredPosition = ((Vector2)screenPoint - halfScreen) / canvas.scaleFactor;
        }
    }
}
