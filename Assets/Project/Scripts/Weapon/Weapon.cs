using System;
using System.Collections.Generic;
using Study3D.FpsGame.Deck;
using Study3D.FpsGame.Weapon.Cards;
using UnityEngine;

namespace Study3D.FpsGame.Weapon
{
    public partial class Weapon : MonoBehaviour
    {
        private Camera mainCam;

        [Header("카드 발사")]
        [SerializeField] private Transform shotPoint;       // 카드가 생성되는 위치 (ViewModel 총의 Gun/ShotPoint)
        [SerializeField] private Projectile cardPrefab;    // 발사할 카드 발사체 프리팹
        [SerializeField] private CardMultiplier multiplier = CardMultiplier.One; // 발사체에 실을 공통 배율 (버프로 변경)

        [Header("발사체 풀")]
        [SerializeField] private string projectilePoolKey = "Projectile"; // 같은 프리팹을 쓰는 무기끼리는 같은 키로 풀을 공유합니다
        [SerializeField] private byte projectilePoolExpandSize = 32;      // 풀이 비었을 때 한 번에 늘리는 수
        [Tooltip("동시에 존재할 수 있는 발사체 수. 넘으면 분열은 새 발사체 대신 부모의 피해와 크기를 키웁니다")]
        [SerializeField] private int maxProjectiles = 500;

        // 마지막으로 쓴 공격 번호 입니다.
        // - 발사할 때마다 1씩 늘려서 발사체에 붙입니다.
        // - 0은 "공격 번호 없음"이라서 1부터 씁니다.
        // - static이라서 무기가 여러 개여도 번호가 겹치지 않습니다.
        private static int lastAttackId;

        public float CurrentFireRate { get; protected set; }

        public CardMultiplier Multiplier
        {
            get => multiplier;
            set => multiplier = value;
        }

        // 강화 카드가 쌓이는 곳. 공격 카드를 쓰면 한꺼번에 적용되고 비워집니다.
        public EnhanceStack EnhanceStack { get; } = new EnhanceStack();

        private void Start()
        {
            mainCam = Camera.main;
            ProjectileManager.Capacity = maxProjectiles;
            RegisterProjectilePool();
        }

        // 발사체 풀을 등록합니다. (ObjectPoolManager의 Inspector 목록에 따로 넣지 않아도 됩니다)
        // ObjectPoolManager가 씬에 없으면 발사체는 Instantiate / Destroy로 동작합니다.
        private void RegisterProjectilePool()
        {
            ObjectPoolManager pool = ObjectPoolManager.Instance;
            if (pool == null)
            {
                Debug.LogWarning($"{name}: ObjectPoolManager가 없어 발사체를 풀링하지 않습니다");
                return;
            }

            if (pool.HasPool(projectilePoolKey)) return;
            pool.CreatePool(new ObjectPoolManager.ObjectPoolData(projectilePoolKey, cardPrefab.gameObject, projectilePoolExpandSize));
        }
        
        // 선택된 카드를 발사합니다. 발사했으면 true, 쿨타임이거나 카드가 없으면 false를 반환합니다.
        // card : 플레이어가 선택해서 보낸 카드 = 이번에 발사할 카드 입니다.
        public bool Fire(CardData card)
        {
            // 발사 가능한지 체크해줍니다.
            // 함수를 호출하는쪽에서 False를 받으면 발사 불가능에 대한 연출(피드백, 사운드) 처리를 합니다.
            if (IsAbleFire(card) == false) return false;
            // 발사 가능 상태입니다. => 사격처리를 하면 됩니다.
            // 사격은 화면의 중앙(Crosshair 위치)을 기준으로
            // 카메라 -> 월드 -> Ray까지 이어지는 좌표 변환을 활용하여
            // Ray를 생성해서 처리합니다.
            
            // 우리가 이런 처리를 뷰포트 좌표를 활용한 변환이라고 합니다.
            // 화면 좌표계의 비율을 왼쪽 아래가 (0, 0). 오른쪽 위가 (1, 1)인 비율 좌표 입니다.
            // (0.5, 0.5)로(화면 중앙 비율) 연산하면 언제나 한가운데에서 출발하는 Ray를 생성할 수 있습니다.
            Ray ray = mainCam.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0));
            FireRay(ray, card);
            
            return true;
        }

        private void Update()
        {
            // 다음 발사 가능 시간을 체크하는 변수는 계속 더해 줍니다.
            CurrentFireRate += GameTime.DeltaTime;
        }

        // 발사 가능한 상태인지 체크합니다.
        // - 쿨타임이 지났고 선택된 카드가 있어야 합니다. 손패가 비면 선택된 카드도 없으므로 발사할 수 없습니다.
        private bool IsAbleFire(CardData card)
        {
            if (card == null) return false; // 발사할 카드가 없으면 발사 불가능입니다.

            // 내가 대기한 시간이 설정된 FireRate(왜 Rate지? Term, Duration)보다 적다면 발사 불가능 상태입니다.
            if (CurrentFireRate < FireRate) return false;
            // 발사시간은 통과. 대기한 시간을 0.0f로 바꿔줍니다.
            CurrentFireRate = 0.0f;
            return true;
        }
        
        // Ray로 맞은 위치를 확인하고, ShotPoint에서 카드를 발사하는 함수
        // - 데미지는 여기서 바로 주지 않습니다. (날아간 Card가 맞힌 시점에 처리합니다)
        private void FireRay(Ray ray, CardData card)
        {
            Quaternion cardRotation;
            // 적뿐 아니라 벽, 바닥(환경)도 맞혀야 조준한 곳으로 카드를 보낼 수 있습니다.
            int hitMask = CombatSystem.Instance.EnemyMask | CombatSystem.Instance.EnvironmentMask;
            // 적의 공격 판정(트리거)은 조준 대상이 아니므로 무시합니다. (발사체 충돌 검사와 같은 기준)
            if (Physics.Raycast(ray, out RaycastHit hit, Range, hitMask, QueryTriggerInteraction.Ignore))
            {
                // ShotPoint -> 맞은 위치 방향 벡터를 카드의 +z로 삼아, 플레이어가 바라본 곳으로 날아가게 합니다.
                // (LookRotation은 월드 up 기준이라 z 회전(롤)이 0으로 만들어집니다)
                Vector3 direction = hit.point - shotPoint.position;
                cardRotation = Quaternion.LookRotation(direction);
            }
            else
            {
                // 맞은 위치가 없으면 ShotPoint의 회전에서 z 회전만 제거해서 총구 정면(+z)으로 날아갑니다.
                Vector3 euler = shotPoint.eulerAngles;
                cardRotation = Quaternion.Euler(euler.x, euler.y, 0f);
            }

            // 카메라와 총구 사이에 적이나 벽이 있으면(적에 바짝 붙어 총구가 적 몸 안에 들어간 경우 등) 카메라 위치에서 발사합니다.
            // - 발사체 판정은 출발할 때 이미 겹쳐 있는 Collider를 감지하지 못해서, 총구에서 쏘면 그 적을 맞히지 못합니다.
            // - 또 조준점이 총구보다 뒤에 있게 되어, 총구 → 조준점 방향이 거꾸로(플레이어 쪽으로) 튕겨 나옵니다.
            Vector3 spawnPosition = shotPoint.position;
            if (Physics.Linecast(ray.origin, shotPoint.position, hitMask))
            {
                spawnPosition = ray.origin;
                cardRotation = Quaternion.LookRotation(ray.direction);
            }

            Projectile projectile = Projectile.Spawn(projectilePoolKey, cardPrefab, spawnPosition, cardRotation);

            // 이번 발사에 새 공격 번호를 붙입니다.
            lastAttackId++;
            int attackId = lastAttackId;
            projectile.Launch(BuildLaunchCards(card), Damage, multiplier, attackId);
        }

        // 받은 카드의 종류에 따라 이번 발사에 실을 카드 목록을 만듭니다. 발사체는 이 순서대로 카드를 실행합니다.
        private List<CardData> BuildLaunchCards(CardData card)
        {
            List<CardData> launchCards = new List<CardData>();
            if (card.Type == CardType.Attack)
            {
                // 공격 카드: 쌓아둔 강화를 모두 싣고 비웁니다. (강화는 일회성)
                EnhanceStack.CollectCards(launchCards);
                EnhanceStack.Clear();
            }
            else
            {
                // 강화 카드: 스택에 쌓습니다. 가득 찼으면 쌓지 않고 무시합니다.
                // 쌓였는지와 상관없이 이번 발사에는 이 카드 자신이 실립니다.
                EnhanceStack.Add(card);
            }

            card.CollectCards(launchCards);
            return launchCards;
        }
    }
}