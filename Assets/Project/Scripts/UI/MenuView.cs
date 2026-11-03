using UnityEngine;
using UnityEngine.UI;

namespace Study3D.FpsGame.UI
{
    // 메뉴 화면 종류 (왼쪽 패널). 옵션 패널은 이후에 추가합니다.
    public enum MenuMode
    {
        Upgrade,
        Inventory,
        Cauldron, // 가마솥 선택 화면 (강화 / 요리). 화면 전체를 덮습니다.
    }

    // MenuView
    // : 메뉴 화면 캔버스 루트에 붙어 오른쪽 공통 카드 목록, 왼쪽 패널들, 취소 버튼을 묶습니다. UpgradeUIBuilder가 조립하면서 참조를 넣어 줍니다. (이름으로 찾지 않음)
    // - 왼쪽 패널은 한 번에 하나만 켭니다. 꺼진 패널은 목록 클릭을 받지 않습니다.
    // - 열고 닫기, 플레이어 조작 막기는 MenuScreen이 맡습니다.
    public class MenuView : MonoBehaviour
    {
        [SerializeField] private CardListView cardList;
        [SerializeField] private UpgradePanel upgradePanel;
        [SerializeField] private InventoryPanel inventoryPanel;
        [SerializeField] private CauldronPanel cauldronPanel;
        [SerializeField] private Button cancelButton;

        public CardListView CardList => cardList;
        public UpgradePanel UpgradePanel => upgradePanel;
        public InventoryPanel InventoryPanel => inventoryPanel;
        public CauldronPanel CauldronPanel => cauldronPanel;
        public Button CancelButton => cancelButton;

#if UNITY_EDITOR
        // UpgradeUIBuilder가 조립 직후 호출합니다.
        public void EditorSetup(CardListView list, UpgradePanel upgrade, InventoryPanel inventory, CauldronPanel cauldron, Button cancel)
        {
            cardList = list;
            upgradePanel = upgrade;
            inventoryPanel = inventory;
            cauldronPanel = cauldron;
            cancelButton = cancel;
        }

        // 이미 조립된 화면에 가마솥 패널만 붙일 때 UpgradeUIBuilder가 호출합니다.
        public void EditorSetCauldronPanel(CauldronPanel cauldron)
        {
            cauldronPanel = cauldron;
        }
#endif

        // mode의 패널만 켭니다. (화면 루트를 켜기 전에 불러야 꺼질 패널의 OnEnable이 돌지 않습니다)
        public void ShowPanel(MenuMode mode)
        {
            upgradePanel.gameObject.SetActive(mode == MenuMode.Upgrade);
            inventoryPanel.gameObject.SetActive(mode == MenuMode.Inventory);
            if (cauldronPanel != null)
            {
                cauldronPanel.gameObject.SetActive(mode == MenuMode.Cauldron);
            }
        }
    }
}
