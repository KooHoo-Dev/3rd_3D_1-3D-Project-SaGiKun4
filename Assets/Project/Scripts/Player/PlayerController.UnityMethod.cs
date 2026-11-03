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
            {
                // 메뉴가 닫히면 그 다음 두 프레임 동안 커서를 다시 잠급니다.
                relockFramesLeft = 2;
                return;
            }

            // 커서가 풀려 있던 프레임의 클릭은 잠그는 데만 쓰고 발사하지 않습니다.
            if (UpdateCursorLock())
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
            // Tab은 남은 덱 목록(DrawPileView)이 누르고 있는 동안 보여 주는 데 씁니다.
            if (SimpleInput.GetKeyDown(Key.I))
                MenuScreen.Instance.Toggle(MenuMode.Inventory);
        }

        // Animator가 포즈를 적용한 다음에 실행되므로, 여기서 바꾼 본 회전은 덮어써지지 않습니다.
        private void LateUpdate()
        {
            ApplySpineBend();
        }

        // 에디터는 Esc를 누른 프레임이 끝날 때 커서 잠금을 강제로 풉니다.
        // 그래서 Esc로 메뉴를 닫으면 Close에서 잠근 것이 바로 풀리므로, 메뉴에서 돌아온 직후 프레임에 한 번 더 잠급니다.
        // 그래도 풀려 있으면(게임 중 Esc, 다른 창 클릭 등) 화면을 클릭할 때 다시 잠급니다.
        // 클릭으로 잠갔으면 true를 돌려줍니다.
        private bool UpdateCursorLock()
        {
            if (relockFramesLeft > 0)
            {
                relockFramesLeft--;
                LockCursor();
                return false;
            }

            if (Cursor.lockState == CursorLockMode.Locked)
                return false;

            if (SimpleInput.GetMouseButtonDown(SimpleInput.MouseButton.Left))
            {
                LockCursor();
                return true;
            }
            return false;
        }

        private void LockCursor()
        {
            Cursor.visible = false;
            Cursor.lockState = CursorLockMode.Locked;
        }
    }
}