using System;
using UnityEngine;

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
            UpdateMovement();
            UpdateCameraRotation();
            UpdatePlayerInput();
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