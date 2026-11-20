using UnityEngine;

namespace Study3D.FpsGame.Map
{
    // BossRoom
    // : 보스방의 전투 진행 입니다. (보스방 루트에 RoomModule 과 함께 붙임. RoomSpawner 대신 사용)
    // - 보스는 방에 미리 있지 않고, 일반 몬스터처럼 플레이어가 들어올 때 소환합니다.
    // - 플레이어가 입구 안쪽(EnterPoint)에 들어오면 RoomModule 이 차단막을 올리고 전투 시작을 알림
    //   → 스폰 위치에 보스 프리팹을 만들고 바닥에서 올라오게 합니다. (Boss.Appear, 다 올라오면 전투 시작)
    // - 보스는 한 마리뿐이라 오브젝트 풀을 쓰지 않고 Instantiate 합니다.
    // - 보스의 달리기 스킬이 쓸 방 중앙은 소환할 때 넣어 줍니다. (프리팹은 방 안의 오브젝트를 참조할 수 없음)
    // - 보스가 죽으면 방을 클리어합니다. (차단막이 열림. 보스방은 마지막 방이라 출구는 계속 막혀 있음)
    [RequireComponent(typeof(RoomModule))]
    public class BossRoom : MonoBehaviour
    {
        [Tooltip("소환할 보스 프리팹")]
        [SerializeField] private Boss.Boss bossPrefab;
        [Tooltip("보스가 올라와 설 위치와 바라볼 방향 (보스방 프리팹 안의 빈 오브젝트)")]
        [SerializeField] private Transform spawnPoint;
        [Tooltip("방 중앙 위치 (보스 달리기 스킬이 이 방향으로 달림)")]
        [SerializeField] private Transform roomCenter;

        private RoomModule room;
        private Boss.Boss boss;

        private void Awake()
        {
            room = GetComponent<RoomModule>();
            room.OnCombatStart.AddListener(SpawnBoss);
        }

        private void OnDestroy()
        {
            if (room != null)
            {
                room.OnCombatStart.RemoveListener(SpawnBoss);
            }
            if (boss != null)
            {
                boss.Died -= OnBossDied;
            }
        }

        private void SpawnBoss()
        {
            if (bossPrefab == null || spawnPoint == null)
            {
                Debug.LogWarning($"[BossRoom] {name}: 보스 프리팹이나 스폰 위치가 없습니다.", this);
                room.Clear();
                return;
            }

            boss = Instantiate(bossPrefab, spawnPoint.position, spawnPoint.rotation, transform);

            Boss.BossRunSkill runSkill = boss.GetComponent<Boss.BossRunSkill>();
            if (runSkill != null)
            {
                runSkill.SetRoomCenter(roomCenter);
            }

            boss.Died += OnBossDied;
            boss.Appear();
        }

        private void OnBossDied(Boss.Boss deadBoss)
        {
            deadBoss.Died -= OnBossDied;
            room.Clear();
        }
    }
}
