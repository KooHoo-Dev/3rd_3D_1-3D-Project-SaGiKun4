using Study3D.FpsGame.Deck;
using Study3D.FpsGame.Player;
using UnityEngine;

namespace Study3D.FpsGame.UI
{
    // UpgradeScreen
    // : 카드 강화 화면을 열고 닫고, 확인 버튼에서 강화를 확정합니다. 씬에 하나만 둡니다.
    // - 화면을 여는 쪽(강화 오브젝트, 테스트용 B키 등)은 Instance.Open()만 부르면 됩니다. 누가 열었는지는 모릅니다.
    // - 카드를 고르는 것은 UpgradeView가 하고, 강화 규칙은 CardEnhancer가 맡습니다.
    // - 열려 있는 동안 LocalPlayer를 메뉴 상태로 두어 조작을 막습니다. (CanAct)
    // - 닫는 것은 화면 루트를 끄는 것(SetActive(false)) 이라, 이 컴포넌트는 화면 루트가 아닌 항상 켜져 있는 오브젝트에 붙입니다.
    public class UpgradeScreen : MonoBehaviour
    {
        [SerializeField] private UpgradeView upgradeView;

        public static UpgradeScreen Instance { get; private set; }

        public bool IsOpen { get; private set; }

        private LocalPlayer player;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Debug.LogWarning($"{name}: UpgradeScreen이 이미 있습니다. 이 컴포넌트는 쓰지 않습니다");
                enabled = false;
                return;
            }
            Instance = this;

            if (upgradeView == null)
            {
                Debug.LogWarning("[UpgradeScreen] 강화 화면(UpgradeView)이 연결되어 있지 않습니다.");
                return;
            }
            upgradeView.ConfirmButton.onClick.AddListener(Confirm);
            upgradeView.CancelButton.onClick.AddListener(Close);
            // 화면은 열 때까지 꺼 둡니다. (씬에 켜진 채로 저장되어 있어도 가려지지 않게)
            upgradeView.gameObject.SetActive(false);
        }

        // LocalPlayer.Awake 뒤라야 하므로 Start에서 연결합니다.
        private void Start()
        {
            player = Player.Player.LocalPlayer;
            // 화면을 연 채로 죽으면 닫습니다. (죽은 뒤에 강화가 확정되지 않게)
            if (player != null) player.OnDied += Close;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
            if (player != null) player.OnDied -= Close;
            if (upgradeView == null) return;
            upgradeView.ConfirmButton.onClick.RemoveListener(Confirm);
            upgradeView.CancelButton.onClick.RemoveListener(Close);
        }

        public void Toggle()
        {
            if (IsOpen) Close();
            else Open();
        }

        public void Open()
        {
            if (IsOpen || upgradeView == null || player == null) return;
            // 죽었거나 다른 메뉴 중이면 열지 않습니다.
            if (player.CanAct == false) return;
            // 재장전이 끝나는 순간 손패를 다시 채우므로, 화면 목록 번호와 덱이 어긋나지 않게 재장전 중에는 열지 않습니다.
            if (player.IsReloading)
            {
                Debug.Log("[Enhance] 재장전 중에는 강화 화면을 열 수 없습니다.");
                return;
            }

            IsOpen = true;
            player.SetInMenu(true);
            // 켜질 때 UpgradeView가 덱을 다시 읽어 목록을 채웁니다.
            upgradeView.gameObject.SetActive(true);

            Cursor.visible = true;
            Cursor.lockState = CursorLockMode.None;
        }

        public void Close()
        {
            if (IsOpen == false) return;

            IsOpen = false;
            upgradeView.gameObject.SetActive(false);
            player.SetInMenu(false);

            Cursor.visible = false;
            Cursor.lockState = CursorLockMode.Locked;
        }

        // 확인 버튼: 두 칸이 모두 찼고 강화에 성공하면 화면을 닫습니다. 아니면 화면을 연 채로 둡니다.
        private void Confirm()
        {
            int firstIndex = upgradeView.GetSlotItemIndex(0);
            int secondIndex = upgradeView.GetSlotItemIndex(1);
            if (firstIndex < 0 || secondIndex < 0)
            {
                Debug.Log("[Enhance] 카드 두 장을 모두 골라야 강화할 수 있습니다.");
                return;
            }

            if (CardEnhancer.TryEnhance(player.Deck, firstIndex, secondIndex)) Close();
        }
    }
}
