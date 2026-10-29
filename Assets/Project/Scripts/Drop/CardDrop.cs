using System.Collections.Generic;
using System.Text;
using Study.Utilities;
using Study3D.FpsGame.Deck;
using Study3D.FpsGame.Player;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Study3D.FpsGame.Drop
{
    // CardDrop
    // : 몬스터가 죽은 자리에 떨어지는 카드 보상 입니다. CardDropTable이 만들어서 Setup으로 표를 넣어줍니다.
    // - 플레이어가 트리거 안에서 F키를 누르면 선택지를 로그로 보여주고, 숫자 1/2/3키로 고른 카드를 덱에 넣은 뒤 사라집니다.
    // - 선택지는 처음 열 때 한 번만 만듭니다. 닫았다 다시 열어도 같은 선택지가 나옵니다.
    // - 고르는 동안은 메뉴 상태로 두어 이동, 발사, 손패 숫자키 입력을 막습니다. F 또는 Esc로 닫습니다.
    // - (임시) UI가 생기면 로그 대신 화면에 띄우고 클릭으로 고르게 바꿉니다.
    public class CardDrop : MonoBehaviour
    {
        private CardDropTable table;
        private List<CardData> options;
        private bool playerInRange;
        private bool isChoosing;

        public void Setup(CardDropTable dropTable)
        {
            table = dropTable;
        }

        private void OnTriggerEnter(Collider other)
        {
            if (IsLocalPlayer(other) == false) return;

            playerInRange = true;
            Debug.Log("[CardDrop] F키를 눌러 카드를 고르세요.");
        }

        private void OnTriggerExit(Collider other)
        {
            if (IsLocalPlayer(other) == false) return;

            playerInRange = false;
            Close();
        }

        private void Update()
        {
            LocalPlayer player = Player.Player.LocalPlayer;
            if (player == null) return;

            if (isChoosing)
            {
                if (player.IsDead || SimpleInput.GetKeyDown(Key.F) || SimpleInput.GetKeyDown(Key.Escape))
                {
                    Close();
                    return;
                }

                // Key.Digit1~Digit3은 연속된 값입니다.
                for (int i = 0; i < options.Count; i++)
                {
                    if (SimpleInput.GetKeyDown(Key.Digit1 + i))
                    {
                        Choose(player, i);
                        return;
                    }
                }
                return;
            }

            // 다른 메뉴(강화 화면, 다른 보상)가 열려 있으면 CanAct가 false라 열리지 않습니다.
            if (playerInRange && player.CanAct && SimpleInput.GetKeyDown(Key.F)) Open(player);
        }

        // 보상이 사라질 때 고르는 중이었다면 조작을 되돌려 줍니다.
        private void OnDisable()
        {
            Close();
        }

        private void Open(LocalPlayer player)
        {
            if (table == null)
            {
                Debug.LogWarning("[CardDrop] 드랍 표가 없어 선택지를 만들 수 없습니다. (CardDropTable.TryDrop으로 만들어야 합니다)");
                return;
            }

            options ??= table.CreateOptions(player.Deck);
            if (options.Count == 0)
            {
                Debug.Log("[CardDrop] 고를 수 있는 카드가 없습니다.");
                return;
            }

            isChoosing = true;
            player.SetInMenu(true);

            var log = new StringBuilder("[CardDrop] 카드를 고르세요. (숫자키 선택, F/Esc 닫기)");
            for (int i = 0; i < options.Count; i++)
                log.Append($"\n  {i + 1}번: {Describe(options[i])}");
            Debug.Log(log.ToString());
        }

        private void Close()
        {
            if (isChoosing == false) return;

            isChoosing = false;
            if (Player.Player.LocalPlayer != null) Player.Player.LocalPlayer.SetInMenu(false);
        }

        private void Choose(LocalPlayer player, int index)
        {
            CardData card = options[index];
            player.AcquireCard(card);
            Debug.Log($"[CardDrop] {Describe(card)} 획득");

            Close();
            Destroy(gameObject);
        }

        // 이름(종류, 차수). 조합 카드는 재료 카드도 순서대로 붙입니다.
        private static string Describe(CardData card)
        {
            string text = $"{card.CardName} ({card.Type}, Lv.{card.EnhanceLevel})";
            if (card is not CompositeCardData) return text;

            var parts = new List<CardData>();
            card.CollectCards(parts);
            return $"{text} = {string.Join(" + ", parts.ConvertAll(part => part.CardName))}";
        }

        private static bool IsLocalPlayer(Collider other)
        {
            LocalPlayer player = Player.Player.LocalPlayer;
            return player != null && other == player.MainCollider;
        }
    }
}
