using Study3D.FpsGame.Player;
using UnityEngine;

namespace Study3D.FpsGame.Interaction
{
    // Interactable
    // : F키로 상호작용할 수 있는 오브젝트의 공통 부분 입니다. (대장간, 줍는 아이템 등은 이 클래스를 상속합니다)
    // - 보여줄 글자(이름, 행동 문구)와 실제 행동(Interact)을 한 곳에 둡니다. 플레이어/UI는 상대가 무엇인지 모릅니다.
    // - 같은 오브젝트에 트리거 콜라이더(상호작용 범위)를 두고, 플레이어가 들어오고 나갈 때 PlayerInteractor에 후보로 알립니다.
    //   여러 후보 중 무엇을 고를지는 PlayerInteractor가 정합니다.
    // - 프롬프트 UI는 promptAnchor 위치에 뜹니다. (비우면 이 오브젝트 위치)
    public abstract class Interactable : MonoBehaviour
    {
        [SerializeField] private string displayName;
        [SerializeField] private string actionText;
        [Tooltip("프롬프트 UI가 뜰 위치. 비우면 이 오브젝트 위치")]
        [SerializeField] private Transform promptAnchor;

        public virtual string DisplayName => displayName;
        public virtual string ActionText => actionText;

        public Vector3 PromptPosition
        {
            get
            {
                if (promptAnchor != null)
                    return promptAnchor.position;

                return transform.position;
            }
        }

        private PlayerInteractor interactor;

        // 상황에 따라 막을 때 override 합니다. (골드 부족 등)
        public virtual bool CanInteract(LocalPlayer player)
        {
            return true;
        }

        // F키를 눌렀을 때 실제 행동
        public abstract void Interact(LocalPlayer player);

        private void OnTriggerEnter(Collider other)
        {
            if (IsLocalPlayer(other) == false)
                return;

            // 플레이어 쪽 컴포넌트는 처음 들어올 때 한 번만 찾아 둡니다.
            if (interactor == null)
                interactor = Player.Player.LocalPlayer.GetComponent<PlayerInteractor>();
            if (interactor == null)
            {
                Debug.LogWarning("[Interactable] 플레이어에 PlayerInteractor가 없습니다.", this);
                return;
            }
            interactor.AddCandidate(this);
        }

        private void OnTriggerExit(Collider other)
        {
            if (IsLocalPlayer(other) == false)
                return;

            if (interactor != null)
                interactor.RemoveCandidate(this);
        }

        // 범위 안에서 꺼지거나 사라지면 OnTriggerExit가 오지 않으므로 직접 빠집니다.
        private void OnDisable()
        {
            if (interactor != null)
                interactor.RemoveCandidate(this);
        }

        private static bool IsLocalPlayer(Collider other)
        {
            LocalPlayer player = Player.Player.LocalPlayer;
            return player != null && other == player.MainCollider;
        }
    }
}
