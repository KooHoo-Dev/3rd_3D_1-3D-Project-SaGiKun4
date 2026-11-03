using Study3D.FpsGame.Player;
using Study3D.FpsGame.UI;

namespace Study3D.FpsGame.Interaction
{
    // CauldronInteractable
    // : 가마솥. F키를 누르면 선택 화면(MenuScreen의 가마솥 모드: 카드 강화 / 요리)을 엽니다.
    // - 열 수 있는지(재장전 중 등)는 MenuScreen.Open이 판단합니다.
    public class CauldronInteractable : Interactable
    {
        public override bool CanInteract(LocalPlayer player)
        {
            return MenuScreen.Instance != null;
        }

        public override void Interact(LocalPlayer player)
        {
            MenuScreen.Instance.Open(MenuMode.Cauldron);
        }
    }
}
