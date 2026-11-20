using System.Collections.Generic;
using Study3D.FpsGame.Deck;
using Study3D.FpsGame.Weapon.Cards;
using UnityEngine;

namespace Study3D.FpsGame.Boss
{
    // BossCardLauncher
    // : 보스가 카드를 쏘는 발사기 입니다. (보스 루트에 붙임)
    // - 원거리 공격과 마법(복사한 카드 발사)이 함께 씁니다. 발사체, 피해, 속도, 크기는 여기서 한 번에 정합니다.
    // - 플레이어와 같은 카드 발사체 프리팹을 쓰지만, 보스 카드(Projectile.LaunchHostile)로 쏴서 플레이어를 맞힙니다.
    // - 발사체 풀은 처음 쓸 때 없으면 만듭니다. (Weapon 과 같은 방식)
    public class BossCardLauncher : MonoBehaviour
    {
        [Header("발사체")]
        [Tooltip("발사할 카드 발사체 프리팹 (플레이어 카드와 같은 프리팹)")]
        [SerializeField] private Projectile cardPrefab;
        [Tooltip("플레이어 무기와 같은 프리팹이면 같은 키를 써서 풀을 함께 써도 됩니다")]
        [SerializeField] private string projectilePoolKey = "Projectile";
        [SerializeField] private byte projectilePoolExpandSize = 32;

        [Header("카드 수치")]
        [SerializeField] private int damage = 20;
        [SerializeField] private float speed = 3f;
        [Tooltip("프리팹 크기에 곱하는 배율")]
        [SerializeField] private float sizeScale = 3f;

        private Boss owner;

        private void Awake()
        {
            owner = GetComponent<Boss>();
        }

        private void Start()
        {
            RegisterProjectilePool();
        }

        private void RegisterProjectilePool()
        {
            ObjectPoolManager pool = ObjectPoolManager.Instance;
            if (pool == null)
                return;

            if (pool.HasPool(projectilePoolKey))
                return;

            pool.CreatePool(new ObjectPoolManager.ObjectPoolData(projectilePoolKey, cardPrefab.gameObject, projectilePoolExpandSize));
        }

        // position 에서 direction 방향으로 카드를 쏩니다. cards 는 발사체에 실을 카드 목록입니다. (기본 공격 카드면 빈 목록)
        public void Fire(Vector3 position, Vector3 direction, IReadOnlyList<CardData> cards)
        {
            if (direction.sqrMagnitude < 0.0001f)
                return;

            Quaternion rotation = Quaternion.LookRotation(direction);
            Projectile projectile = Projectile.Spawn(projectilePoolKey, cardPrefab, position, rotation);
            projectile.LaunchHostile(cards, damage, speed, sizeScale, owner);
        }
    }
}
