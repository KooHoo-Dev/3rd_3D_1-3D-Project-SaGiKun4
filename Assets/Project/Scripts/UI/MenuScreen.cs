using Study3D.FpsGame.Player;
using UnityEngine;

namespace Study3D.FpsGame.UI
{
    // MenuScreen
    // : 메뉴 화면(강화, 인벤토리, 이후 옵션)을 열고 닫습니다. 씬에 하나만 둡니다.
    // - 화면을 여는 쪽(대장간, Tab키, 테스트용 B키 등)은 Instance.Open(모드)만 부르면 됩니다. 누가 열었는지는 모릅니다.
    // - 오른쪽 카드 목록은 모든 모드가 같이 쓰고, 왼쪽 패널만 모드에 맞게 바뀝니다. (MenuView)
    // - 열려 있는 동안 LocalPlayer를 메뉴 상태로 두어 조작을 막습니다. (CanAct)
    // - 닫는 것은 화면 루트를 끄는 것(SetActive(false)) 이라, 이 컴포넌트는 화면 루트가 아닌 항상 켜져 있는 오브젝트에 붙입니다.
    public class MenuScreen : MonoBehaviour
    {
        [SerializeField] private MenuView menuView;

        public static MenuScreen Instance { get; private set; }

        public bool IsOpen { get; private set; }
        public MenuMode CurrentMode { get; private set; }

        private LocalPlayer player;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Debug.LogWarning($"{name}: MenuScreen이 이미 있습니다. 이 컴포넌트는 쓰지 않습니다");
                enabled = false;
                return;
            }
            Instance = this;

            if (menuView == null)
            {
                Debug.LogWarning("[MenuScreen] 메뉴 화면(MenuView)이 연결되어 있지 않습니다.");
                return;
            }
            menuView.CancelButton.onClick.AddListener(Close);
            // 강화에 성공하면 화면을 닫습니다.
            menuView.UpgradePanel.OnEnhanced += Close;
            // 화면은 열 때까지 꺼 둡니다. (씬에 켜진 채로 저장되어 있어도 가려지지 않게)
            menuView.gameObject.SetActive(false);
        }

        // LocalPlayer.Awake 뒤라야 하므로 Start에서 연결합니다.
        private void Start()
        {
            player = Player.Player.LocalPlayer;
            // 화면을 연 채로 죽으면 닫습니다. (죽은 뒤에 강화가 확정되지 않게)
            if (player != null)
            {
                player.OnDied += Close;
            }
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }

            // 열린 채로 씬이 바뀌면 정지 요청이 남지 않게 풀어 줍니다.
            if (IsOpen)
            {
                IsOpen = false;
                GameTime.Resume();
            }

            if (player != null)
            {
                player.OnDied -= Close;
            }

            if (menuView == null)
                return;

            menuView.CancelButton.onClick.RemoveListener(Close);
            menuView.UpgradePanel.OnEnhanced -= Close;
        }

        // 같은 모드가 열려 있으면 닫고, 아니면 그 모드로 엽니다.
        public void Toggle(MenuMode mode)
        {
            if (IsOpen && CurrentMode == mode)
            {
                Close();
            }
            else
            {
                Open(mode);
            }
        }

        public void Open(MenuMode mode)
        {
            if (menuView == null || player == null)
                return;

            // 이미 열려 있으면 왼쪽 패널만 바꿉니다.
            if (IsOpen)
            {
                CurrentMode = mode;
                menuView.ShowPanel(mode);
                return;
            }
            // 죽었거나 다른 메뉴 중이면 열지 않습니다.
            if (player.CanAct == false)
                return;

            // 재장전이 끝나는 순간 손패를 다시 채우므로, 화면 목록 번호와 덱이 어긋나지 않게 재장전 중에는 열지 않습니다.
            if (player.IsReloading)
            {
                Debug.Log("[Menu] 재장전 중에는 메뉴 화면을 열 수 없습니다.");
                return;
            }

            IsOpen = true;
            CurrentMode = mode;
            player.SetInMenu(true);
            GameTime.Pause(); // 화면이 막힌 동안 공격당하지 않도록 게임을 멈춥니다.
            menuView.ShowPanel(mode);
            // 켜질 때 카드 목록이 덱을 다시 읽어 채웁니다.
            menuView.gameObject.SetActive(true);

            Cursor.visible = true;
            Cursor.lockState = CursorLockMode.None;
        }

        public void Close()
        {
            if (IsOpen == false)
                return;

            IsOpen = false;
            menuView.gameObject.SetActive(false);
            player.SetInMenu(false);
            GameTime.Resume();

            Cursor.visible = false;
            Cursor.lockState = CursorLockMode.Locked;
        }
    }
}
