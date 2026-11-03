using System;
using Study3D.FpsGame.Player;
using UnityEngine;

namespace Study3D.FpsGame.Enemy
{
    // EnemyWeapon
    // : 적의 칼(Sword)에 붙이는 공격 판정 입니다.
    // - BoxCollider를 켜고 끄는 건 Attack01 애니메이션이 합니다. 이 컴포넌트는 닿았을 때 피해만 줍니다.
    //   (돌진 스킬의 몸 앞쪽 판정처럼 스킬 코드가 켜고 끄는 경우도 있음)
    // - 애니메이션이 휘두를 때마다 콜라이더를 껐다 켜므로, Enter만 받으면 한 번 휘두를 때 한 번만 맞힙니다.
    // - 트리거 이벤트를 받으려면 한쪽에 Rigidbody가 있어야 해서, 움직이지 않는(Kinematic) Rigidbody를 둡니다.
    [RequireComponent(typeof(BoxCollider), typeof(Rigidbody))]
    public class EnemyWeapon : MonoBehaviour
    {
        [SerializeField] private int damage = 10;

        // 플레이어에게 피해를 넣은 순간 알립니다. (돌진 스킬이 부딪히면 멈출 때 사용)
        public event Action<EnemyWeapon> HitPlayer;

        private IDamageable owner;

        private void Awake()
        {
            GetComponent<BoxCollider>().isTrigger = true;

            var body = GetComponent<Rigidbody>();
            body.isKinematic = true;
            body.useGravity = false;

            // 피해를 준 쪽(Sender)으로 쓸 주인은 부모에서 찾습니다.
            owner = GetComponentInParent<IDamageable>();
        }

        private void OnTriggerEnter(Collider other)
        {
            LocalPlayer player = Player.Player.LocalPlayer;
            if (player == null || player.IsDead || other != player.MainCollider) return;

            CombatEvent combatEvent = new CombatEvent();
            combatEvent.Sender = owner;
            combatEvent.Receiver = player;
            combatEvent.Damage = damage;
            combatEvent.HitPosition = other.ClosestPoint(transform.position);
            combatEvent.Collider = other;

            CombatSystem.Instance.AddInGameEvent(combatEvent);

            if (HitPlayer != null)
                HitPlayer.Invoke(this);
        }
    }
}
