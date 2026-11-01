using System;
using Study.Utilities;
using Study3D.FpsGame.UI;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Study3D.FpsGame.Player
{
    public partial class PlayerController
    {
        private void Awake()
        {
            controller = GetComponent<CharacterController>();
        }

        private void Start()
        {
            LockCursor();
            initialCameraRotation = cameraTransform.localRotation;

            // Player.LocalPlayer는 LocalPlayer.Awake에서 채워지는데, 같은 오브젝트의 컴포넌트끼리는
            // Awake/OnEnable 순서가 보장되지 않아 OnEnable에서는 아직 null일 수 있습니다.
            // 모든 Awake가 끝난 뒤인 Start에서 구독합니다.
            Player.LocalPlayer.OnRequestedReload += PlayReloadAnimation;
        }

        private void OnDestroy()
        {
            // 종료나 씬 전환 때 LocalPlayer가 먼저 파괴됐으면 이미 null이라 해제할 대상이 없습니다.
            if (Player.LocalPlayer != null) 
                Player.LocalPlayer.OnRequestedReload -= PlayReloadAnimation;
        }
        
        private void Update()
        {
            // 화면이 열려 있어도 닫을 수 있어야 하므로 CanAct보다 먼저 받습니다. (죽었으면 열리지 않음)
            UpdateMenuInput();

            // 조작할 수 없는 동안(강화 화면, 사망 등)은 이동/점프/시점 회전/발사/카드 입력을 모두 막습니다.
            bool canAct = Player.LocalPlayer.CanAct;

            UpdateMovement(canAct);
            if (canAct == false)
                return;

            UpdateCameraRotation();
            UpdatePlayerInput();
        }

        // 메뉴 화면 열기/닫기 키 입력
        private void UpdateMenuInput()
        {
            if (MenuScreen.Instance == null)
                return;

            // (테스트용) 강화 화면 열기/닫기. 대장간은 MenuScreen.Instance.Open(MenuMode.Upgrade)를 부릅니다.
            if (SimpleInput.GetKeyDown(Key.B))
                MenuScreen.Instance.Toggle(MenuMode.Upgrade);

            // 인벤토리 열기/닫기. (강화 화면이 열려 있으면 인벤토리로 바뀝니다)
            if (SimpleInput.GetKeyDown(Key.Tab))
                MenuScreen.Instance.Toggle(MenuMode.Inventory);
        }

        // Animator가 포즈를 적용한 다음에 실행되므로, 여기서 바꾼 본 회전은 덮어써지지 않습니다.
        private void LateUpdate()
        {
            ApplySpineBend();
        }

        private void LockCursor()
        {
            Cursor.visible = false;
            Cursor.lockState = CursorLockMode.Locked;
        }
    }
}