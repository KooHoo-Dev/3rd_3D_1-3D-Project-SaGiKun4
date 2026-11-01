using System;
using Study3D.FpsGame.Player;
using UnityEngine;

namespace Study3D.FpsGame.Map
{
    // RoomEnterTrigger
    // : 방 입구 안쪽의 진입 판정 입니다. (EnterPoint 오브젝트에 BoxCollider 와 함께 붙임)
    // - 플레이어가 콜라이더에 들어오면 Entered 를 알림. 방(RoomModule)이 인스펙터로 이 컴포넌트를 받아 구독함
    // - 판정 면 전체를 덮도록 BoxCollider 를 통로 폭만큼 넓고 얇게 배치
    [RequireComponent(typeof(BoxCollider))]
    public class RoomEnterTrigger : MonoBehaviour
    {
        public event Action Entered;

        private void Awake()
        {
            GetComponent<BoxCollider>().isTrigger = true;
        }

        private void OnTriggerEnter(Collider other)
        {
            LocalPlayer player = Player.Player.LocalPlayer;
            if (player == null || other != player.MainCollider)
                return;

            if (Entered != null)
            {
                Entered.Invoke();
            }
        }
    }
}
