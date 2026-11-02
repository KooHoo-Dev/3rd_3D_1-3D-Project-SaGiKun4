using System;
using Study.Utilities;
using Study3D.FpsGame.Deck;
using Study3D.FpsGame.Weapon;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Study3D.FpsGame.Player
{
    // LocalPlayer
    // : 유저가 직접 제어하는 게임 속 내의 플레이어 객체
    //  HP, 무기 제어, 아이템 줍기, 재장전, 무기 교체, 다양한 알림등
    //  게임 이벤트(게임의 고유한 사건)와 연결되어있는 주요 클래스 입니다.
    // (고유한 : 내가 만든 게임이면 다른 게임이랑 처리되는 내용이 다르겠죠?
    //  로아의 전투 처리와 리니의 전투 처리가 다른것처럼. 그 고유한임)
    
    public partial class LocalPlayer : Player, IDamageable
    {
        [Header("Stat")] 
        [SerializeField] private int maxHp = 100;
        
        // Weapon을 연결하기 위한 참조들을 설정해 줍니다.
        [SerializeField] private Weapon.Weapon weapon;
        
        [Header("애니메이션 콜백")]
        [SerializeField] private string reloadCompleteEvent = "ReloadComplete";

        [Header("Ref")]
        [SerializeField] private CharacterController characterController;
        [SerializeField] private AnimStateEventListener animStateEventListener;

        // (제 방식) MainCollider를 따로 지정하는것을 선호합니다. 
        public Collider MainCollider => characterController;
        // 이 부분은 인터페이스 보시면 됩니다.
        public GameObject GameObject => gameObject;
        public Health Health { get; private set; } // 단순 데미지 차감용
        public bool IsDead { get; private set; }
        public bool IsReloading { get; private set; } = false;

        // 메뉴 화면(강화 화면 등)이 열려 있는 동안 true. 화면 쪽이 열고 닫을 때 SetInMenu로 알려 줍니다.
        // 플레이어는 어떤 화면인지 모릅니다. (메뉴는 한 번에 하나만 열리므로 서로 덮어쓰지 않습니다)
        public bool IsInMenu { get; private set; }

        // 플레이어가 조작(이동/시점/발사/카드 입력)할 수 있는지. 입력을 받는 쪽(PlayerController)은 이 값만 봅니다.
        // - 상태(IsDead, IsInMenu 등)는 각자 관리하고, 여기서는 조건만 모아 계산합니다. (직접 켜고 끄는 플래그로 두면 서로 덮어씀)
        // - 조작을 막는 상태가 새로 생기면 여기에 조건만 추가하면 됩니다.
        public bool CanAct
        {
            get { return IsDead == false && IsInMenu == false; }
        }

        public void SetInMenu(bool inMenu)
        {
            IsInMenu = inMenu;
        }

        #region 유니티 함수들

        private void Awake()
        {
            Player.LocalPlayer = this;
            Health = new Health(maxHp);
            CreateDeck();
        }

        private void Start()
        {
            // 게임에 들어서면 덱에서 손패 최대 장수만큼 뽑습니다.
            Deck.FillHand();
        }

        private void OnDestroy()
        {
            if (Player.LocalPlayer == this) Player.LocalPlayer = null;
        }

        private void OnEnable()
        {
            animStateEventListener.OnOccursAnimStateEvent += OnAnimEvent;
        }

        private void OnDisable()
        {
            animStateEventListener.OnOccursAnimStateEvent -= OnAnimEvent;
        }

        #endregion

        #region 애니메이션 이벤트 처리
        private void OnAnimEvent(string eventName)
        {
            // 재장전이 완료된 시점에 호출되는 애니메이션 상태 이벤트 입니다.
            if (eventName == reloadCompleteEvent)
            {
                if (IsReloading == false) return;
                IsReloading = false;
                // 애니메이션이 끝난 시점에 손패를 모두 버리고 다시 채웁니다.
                RefillHand();
                // 리로드 완료 시점의 델리게이트를 깨워줍니다.
                // (알림해놓은 구독자들에게 완료 됐음을 전달합니다.)
                OnEndedReload?.Invoke();
            }
        }
        #endregion

        #region 총기 명령

        // 선택된 카드를 Weapon으로 보내 발사합니다. 선택된 카드가 없으면(손패가 비면) 발사되지 않습니다.
        public bool Fire()
        {
            // 재장전 중에는 쏠 수 없습니다. (발사 트리거가 Reload 애니메이션을 끊지 않게도 해줍니다)
            if (IsReloading) return false;

            CardData card = Deck.SelectedCard;
            if (weapon.Fire(card) == false) return false;

            // 실제로 발사됐을 때만 사용한 카드를 버린 카드로 보냅니다.
            Deck.DiscardSelected();
            // 손패를 모두 쓰면 자동으로 재장전합니다. (애니메이션이 끝나면 손패가 다시 채워집니다)
            // 발사 모션 중이면 애니메이터의 Shooting → Reload 전이(Exit Time)가 발사 모션 뒤로 미뤄줍니다.
            if (Deck.Hand.Count == 0) RequestReload();
            return true;
        }

        // 재장전 = 손패를 모두 버리고 다시 채웁니다.
        // 여기서는 재장전 상태로 들어가 애니메이션만 시작하고,
        // 실제 손패 교체는 Reload 상태가 보내는 ReloadComplete 신호(OnAnimEvent)에서 처리합니다.
        public void RequestReload()
        {
            // 이미 재장전 중이면 중복 요청을 무시합니다.
            if (IsDead || IsReloading) return;

            IsReloading = true;
            OnRequestedReload?.Invoke();
        }

        #endregion
        
        #region 게임 이벤트 처리

        public void TakeDamage(CombatEvent combatEvent)
        {
            if (IsDead) return;

            Health.TakeDamage(combatEvent.Damage);
            Debug.Log($"[LocalPlayer] 피격 -{combatEvent.Damage} (HP {Health.HP}/{Health.MaxHP})");
            OnTakenDamage?.Invoke(combatEvent.Damage, Health.HP, Health.MaxHP);

            if (Health.IsDead)
            {
                IsDead = true;
                OnDied?.Invoke();
            }
        }

        public void TakeHeal(HealEvent healEvent)
        {

        }

        #endregion

        #region 이동

        // 플레이어를 순간이동 시킵니다. (맵 생성 후 시작방으로 옮길 때 등)
        // CharacterController 가 켜져 있으면 위치를 직접 바꿔도 되돌려지므로 잠깐 끄고 옮깁니다.
        public void Teleport(Vector3 position, Quaternion rotation)
        {
            bool wasEnabled = characterController.enabled;
            characterController.enabled = false;
            transform.SetPositionAndRotation(position, rotation);
            characterController.enabled = wasEnabled;
        }

        #endregion
    }
}