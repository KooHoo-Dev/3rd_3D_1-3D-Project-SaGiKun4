using Study3D.FpsGame.Player;
using Study3D.FpsGame.UI;

namespace Study3D.FpsGame.Interaction
{
    // BlacksmithInteractable
    // : 대장간. F키를 누르면 카드 강화 화면(UpgradeScreen)을 엽니다.
    // - 열 수 있는지(재장전 중 등)는 UpgradeScreen.Open이 판단합니다.
    public class BlacksmithInteractable : Interactable
    {
        public override bool CanInteract(LocalPlayer player) => UpgradeScreen.Instance != null;

        public override void Interact(LocalPlayer player)
        {
            UpgradeScreen.Instance.Open();
        }
    }
}
