using Study.Utilities;
using Study3D.FpsGame.Deck;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Study3D.FpsGame.Player
{
    // PlayerController
    // : 1인칭 이동, 시점, 사격, 재장전 입력과 애니메이션 처리를 담당하는 클래스 입니다;.
    // - Unity의 CharacterController 컴포넌트를 이용
    // - 카메라를 기반으로 모든 처리들을 담당합니다.
    // - WASD => 이동
    // - 마우스 움직임 => 카메라 회전
    // - 마우스 왼쪽 클릭 => 사격
    // - LeftShift : 달리기
    // - Space : 점프
    // - R : 재장전
    // - 마우스 휠 : 카드 선택 이동, 숫자 1~6 : 손패의 해당 카드 선택
    
    public partial class PlayerController : MonoBehaviour
    {
        private static readonly int Fire = Animator.StringToHash("Fire");
        private static readonly int Reload = Animator.StringToHash("Reload");
        // Base Layer 이동 Blend Tree 파라미터 (-1 = 뒤로 걷기, 0 = 정지, 1 = 걷기, 2 = 달리기)
        private static readonly int Speed = Animator.StringToHash("Speed");

        [Header("이동")] 
        [SerializeField] private float walkSpeed = 4f;
        [SerializeField] private float runSpeed = 7f;
        [SerializeField] private float jumpHeight = 1.2f;
        [SerializeField] private float gravity = -9.81f;

        [Header("카메라")] 
        [SerializeField] private Transform cameraTransform;
        [SerializeField] private float mouseSensitivity = 2f; // 마우스 감도 입니다.
        [SerializeField] private float maxLookAngle = 80f;

        [Header("척추")]
        [SerializeField] private Transform[] spineBones;

        [Header("애니메이터")] 
        [SerializeField] private Animator animator;
        // 카메라 자식으로 둔 1인칭 팔/총(뷰모델)의 Animator. 몸과 같은 파라미터를 받습니다. (없으면 비워두세요)
        [SerializeField] private Animator viewModelAnimator;

        // 아래는 내부변수들
        private CharacterController controller;
        private Vector3 velocity;
        private float verticalLookAngle;
        private Quaternion initialCameraRotation;
        private float moveX, moveZ; // 키를 따라 -1 ~ 1 사이를 부드럽게 움직이는 로직에 사용되는 축 값
        private int relockFramesLeft; // 메뉴에서 돌아온 뒤 커서를 다시 잠글 남은 프레임 수

        // 마우스로 누적한 상하 시점 각도 (+ = 아래, - = 위). 카메라 스크립트가 읽어갑니다.
        public float VerticalLookAngle => verticalLookAngle;

        // canAct가 false면(강화 화면 등) 입력이 없는 것으로 처리합니다. 중력과 멈추는 애니메이션은 그대로 진행됩니다.
        private void UpdateMovement(bool canAct)
        {
            // 첫번째로는 이동 속도를 결정해줍니다. LeftShift가 true이면 runSpeed 입니다.
            bool isRunning = canAct && SimpleInput.GetKey(Key.LeftShift);
            float speed = isRunning ? runSpeed : walkSpeed;

            // 이동 연산에 사용할 방향의 축값을 가져옵니다. 가져온 데이터는 한축은
            // -1, 0, 1 의 3가지중 하나가 반환됩니다
            Vector2 inputRaw = canAct ? SimpleInput.GetMoveAxisRaw() : Vector2.zero;
            moveX = SmoothAxis(moveX, inputRaw.x);
            moveZ = SmoothAxis(moveZ, inputRaw.y);

            // 현재 플레이어의 방향에 맞춰서 moveVector를 계산합니다.
            Vector3 moveVector = (transform.right * moveX) + (transform.forward * moveZ);
            // moveVector의 길이만큼 현재 프레임의 이동량이 조정이 됩니다.
            controller.Move(moveVector * (speed * Time.deltaTime));
            UpdateMoveAnimation(isRunning);

            // CharacterController는 쉬운만큼 까다로워서
            // 여러군데에서 나눠서 호출할 경우 버그를 찾기가 어려울수도 있습니다. 
            // 특히 CharacterController의 Move()함수를 Update, FixedUpdate등을 이용해서
            // 서로 다른 갱신 타이밍에 호출하면 생각지도 못한 에러를 발생시킬수도 있습니다.
            // 보통 바닥 판정이 잘 안된다거나 하는 등의 이슈가 있습니다.
            
            // 아래부터는 중력과 점프 구현
            velocity.y += gravity * Time.deltaTime;
            controller.Move(velocity * Time.deltaTime);

            // controller의 isGrounded는 마지막 Move 적용 기준이라
            // 마지막 수직이동 바로 뒤에 읽어야 합니다. 위의
            // "서로 다른 갱신 타이밍에 호출하면 생각지도 못한 에러를 발생시킬수도 있습니다."
            // 요 내용입니다.
            bool isGrounded = controller.isGrounded;

            // 땅에 서있는 동안 낙하속도가 계속 쌓이지 않도록 작은 값으로 되돌립니다.
            // 아래 줄이 없으면 점프 하자마 바로 땅으로 떨어질 수 있습니다.
            // -2f 등을 이용해서 점프 느낌을 조절 합니다.
            // 아니면 따로 변수를 주세요
            if (isGrounded && velocity.y < 0f) velocity.y = -2f;

            if (canAct && isGrounded && SimpleInput.GetKeyDown(Key.Space))
            {
                // 점프의 최대 높이를 지정해주는게 개발에 쉬움.
                // 힘으로 점프를 설정하면 플레이어가 얼마 뛸 지를 모르니까
                // 등가속 운동 공식을 사용해서 바꿔준거임
                // 아래처럼 점프를 만들면 힘을 써서 최대 높이를 설정해주지 않아도
                // jumpHeight를 입력하는 것만으로 최대 높이의 점프를 설저애줄 수 있음
                // 위치 에너지 = 1/2 * m * v^2
                // m = 계산 안함 = 1
                // v^2 = 2 * 위치에너지
                // v = Sqrt(위치에너지 * 2)
                // 운동에너지 = 등가속운동 = velocity
                velocity.y = Mathf.Sqrt((jumpHeight * -2f * gravity));
            }
        }
        
        // 이동 입력의 크기를 Animator의 Speed 파라미터로 넘겨 하체 Blend Tree를 움직입니다.
        private void UpdateMoveAnimation(bool isRunning)
        {
            const float SPEED_DAMP_TIME = 0.1f;
            float moveAmount = Mathf.Clamp01(new Vector2(moveX, moveZ).magnitude);

            // 뒤로 갈 때는 걷기 클립을 거꾸로 재생(-1)하고, 뒤로 달리기는 따로 없으니 걷기로 처리합니다.
            float animSpeed;
            if (moveZ < 0f) animSpeed = -moveAmount;
            else animSpeed = moveAmount * (isRunning ? 2f : 1f);

            animator.SetFloat(Speed, animSpeed, SPEED_DAMP_TIME, Time.deltaTime);
            if (viewModelAnimator != null) viewModelAnimator.SetFloat(Speed, animSpeed, SPEED_DAMP_TIME, Time.deltaTime);
        }

        // 몸과 뷰모델 Animator에 같은 트리거를 겁니다. Any State 전이라 남은 트리거를 지우고 다시 겁니다.
        private void PlayTrigger(int trigger)
        {
            animator.ResetTrigger(trigger);
            animator.SetTrigger(trigger);
            if (viewModelAnimator == null) return;
            viewModelAnimator.ResetTrigger(trigger);
            viewModelAnimator.SetTrigger(trigger);
        }

        // 현재 값을
        private static float SmoothAxis(float current, float target)
        {
            // 아래값은 보간 속도(?)를 의미한다고 생각하시면 됩니다. 댐핑값
            const float AXIS_SPEED = 3f;
            // target이 0이면 정지를 의미하고
            // current * target 음수일 경우에는 반대 방향으로 이동하는 케이스
            // 둘다 current를 0으로 두고 계산합니다.
            // (왼쪽으로 이동하고 있는데 오른쪽으로 이동하라고 명령이 들어오면 0일 기준으로
            // 이동처리를 하라고 처리합니다.)
            if (target != 0f && current * target < 0f) current = 0f;
            // Toward : ~ 향해 이런뜻 
            // MoveTowards(current, target, maxDelta)
            // : current에서 target 목표 지점으로 maxDelta를 초과하지 않게 움직인 양을 반환합니다.
            // ex) current = -0.2f, target : -1, maxDelta = 0.1f
            // -0.3이 반환 됩니다.
            return Mathf.MoveTowards(current, target, AXIS_SPEED * Time.deltaTime);
        }
        
        private void UpdateCameraRotation()
        {
            const float MOUSE_AXIS_SCALE = 0.1f;
            // 현재 프레임의 마우스 이동량을 가져옵니다.
            // MOUSE_AXIS_SCALE을 곱해서 픽셀로 돌려받은 값을 줄여줍니다.
            Vector2 delta = SimpleInput.GetMouseDelta() * MOUSE_AXIS_SCALE;
            float mouseX = delta.x * mouseSensitivity;
            float mouseY = delta.y * mouseSensitivity;
            
            Quaternion yawRotation = Quaternion.Euler(0, mouseX, 0);
            transform.rotation *= yawRotation; // y축은 내가 돌아갑니다. 나는 월드 입니다 

            // Pitch값은 누적. verticalLookRotation => verticalLookAngle로 수정
            verticalLookAngle -= mouseY;
            verticalLookAngle = Mathf.Clamp(verticalLookAngle, -maxLookAngle, maxLookAngle);
            // 척추 굽힘은 Animator가 포즈를 쓴 뒤인 LateUpdate에서 적용합니다.
            // 카메라는 플레이어 루트의 자식이고, 상하 회전은 SpineOrbitCamera가 이 각도를 읽어서 처리합니다.
        }

        // Humanoid Animator가 매 프레임 척추 본을 덮어쓰기 때문에
        // 반드시 Animator 갱신 이후(LateUpdate)에 호출해야 합니다.
        private void ApplySpineBend()
        {
            if (spineBones == null || spineBones.Length == 0) return;

            // 시점 각도를 척추 본 개수만큼 나눠서 조금씩 꺾어주면 허리가 자연스럽게 휩니다.
            float anglePerBone = verticalLookAngle / spineBones.Length;
            // 본마다 로컬 축 방향이 제각각이라, 플레이어의 오른쪽 축(월드)을 기준으로 회전합니다.
            Quaternion bend = Quaternion.AngleAxis(anglePerBone, transform.right);
            foreach (Transform bone in spineBones)
            {
                if (bone == null) continue;
                bone.rotation = bend * bone.rotation;
            }
        }

        // 총발사와 재장전을 담당하는 함수 입니다.
        private void UpdatePlayerInput()
        {
            // 발사 하는 버튼
            if(SimpleInput.GetMouseButtonDown(SimpleInput.MouseButton.Left))
            {
                bool successFire = Player.LocalPlayer.Fire();
                
                // 발사 성공일 경우
                if (successFire)
                {
                    // 해당 animator의 발사동작은 Any State에 들어가므로,
                    // 남은 트리거를 지우고 다시 겁니다.
                    PlayTrigger(Fire);
                }
            }

            // 카드 선택: 휠을 올리면 왼쪽, 내리면 오른쪽 카드로 옮깁니다.
            float scroll = SimpleInput.GetMouseScrollDelta();
            if (scroll > 0f) Player.LocalPlayer.SelectNextCard(-1);
            else if (scroll < 0f) Player.LocalPlayer.SelectNextCard(1);

            // 숫자 1~6 = 손패의 1~6번째 카드를 바로 선택합니다. (Key.Digit1~Digit6은 연속된 값입니다)
            for (int i = 0; i < 6; i++)
            {
                if (SimpleInput.GetKeyDown(Key.Digit1 + i)) Player.LocalPlayer.SelectCard(i);
            }

            // 재장전
            if (SimpleInput.GetKeyDown(Key.R)) Player.LocalPlayer.RequestReload();
        }
        
        private void PlayReloadAnimation()
        {
            PlayTrigger(Reload);
        }
    }
}