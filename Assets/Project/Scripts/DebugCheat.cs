using Study.Utilities;
using Study3D.FpsGame.Data;
using Study3D.FpsGame.Deck;
using Study3D.FpsGame.Player;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Study3D.FpsGame
{
    // DebugCheat (디버그 전용, 언제든 이 파일과 씬의 컴포넌트를 지우면 됩니다)
    // - P : 카드 목록 창을 열고 닫습니다. 버튼을 누르면 그 카드를 바로 획득합니다.
    // - O : 재화를 99999 추가합니다.
    // 씬의 아무 오브젝트에 붙이면 됩니다.
    public class DebugCheat : MonoBehaviour
    {
        private const int CheatCurrency = 99999;

        private bool isOpen;
        private Vector2 scroll;

        private void Update()
        {
            LocalPlayer player = Player.Player.LocalPlayer;
            if (player == null)
                return;

            if (SimpleInput.GetKeyDown(Key.O))
            {
                player.AddCurrency(CheatCurrency);
            }

            if (SimpleInput.GetKeyDown(Key.P))
            {
                SetOpen(player, isOpen == false);
            }
        }

        private void SetOpen(LocalPlayer player, bool open)
        {
            isOpen = open;
            player.SetInMenu(open);
            Cursor.visible = open;
            if (open)
            {
                Cursor.lockState = CursorLockMode.None;
            }
            else
            {
                Cursor.lockState = CursorLockMode.Locked;
            }
        }

        private void OnGUI()
        {
            if (isOpen == false)
                return;

            GUILayout.BeginArea(new Rect(20, 20, 300, Screen.height - 40), GUI.skin.box);
            GUILayout.Label("[Debug] 카드 획득 (P로 닫기)");
            scroll = GUILayout.BeginScrollView(scroll);
            foreach (CardDef def in DataManager.Instance.Cards.GetAll())
            {
                if (GUILayout.Button(def.ID + "  " + def.Name))
                {
                    AcquireCard(def);
                }
            }
            GUILayout.EndScrollView();
            GUILayout.EndArea();
        }

        private void AcquireCard(CardDef def)
        {
            LocalPlayer player = Player.Player.LocalPlayer;
            if (player == null)
                return;

            player.AcquireCard(CardFactory.Create(def));
            Debug.Log($"[Debug] 카드 {def.ID} {def.Name} 획득");
        }
    }
}
