using System;
using System.Collections.Generic;
using UnityEngine;

namespace Study3D.FpsGame.Map
{
    // RoomSpawner
    // : 방의 전투가 시작되면 오브젝트 풀에서 적을 꺼내 스폰 위치에 놓고, 다 죽으면 방을 클리어합니다. (RoomModule 과 같은 오브젝트에 붙임)
    // - 방은 "어떤 적 프리팹을 몇 마리" 목록을 가짐. 풀 키는 프리팹 이름이고, 풀이 없으면 처음 소환할 때 만듦
    // - 스폰 위치마다 StartPoint(맵 밖)와 EndPoint(맵 안쪽)를 인스펙터에서 짝지어 넣음 (이름으로 찾지 않음)
    // - 소환 순서: 스폰 위치를 목록 순서대로 돌며 한 마리씩 놓고, 목록의 마릿수가 모두 0이 될 때까지 반복
    //   예: 적 A 3마리, B 2마리, 스폰 위치 4개 → A→1, A→2, A→3, B→4, B→1
    // - 적은 StartPoint 에서 등장 상태로 EndPoint 까지 직진한 뒤 길찾기로 추격함
    //   EndPoint 는 지도 영역(바닥) 위에, StartPoint 에서 EndPoint 까지는 벽에 막히지 않게 둬야 함
    // - 스폰 위치는 방의 자식이라 생성기가 방을 어디에 어떤 방향으로 놓든 그대로 따라감
    // - 꺼낸 적은 위치/회전을 먼저 잡은 뒤 켬 (켜지는 순간 예전 위치에서 방을 찾지 않도록)
    [RequireComponent(typeof(RoomModule))]
    public class RoomSpawner : MonoBehaviour
    {
        [Serializable]
        public class EnemyEntry
        {
            public Enemy.Enemy prefab;
            [Min(0)] public int count = 1;
        }

        [Serializable]
        public class SpawnPoint
        {
            [Tooltip("맵 밖 등장 위치")]
            public Transform start;
            [Tooltip("걸어 들어올 맵 안쪽 도착 지점")]
            public Transform end;
        }

        [Tooltip("이 방에서 소환할 적과 마릿수")]
        [SerializeField] private EnemyEntry[] enemies = new EnemyEntry[0];
        [Tooltip("StartPoint / EndPoint 짝. 이 순서대로 돌며 소환")]
        [SerializeField] private SpawnPoint[] spawnPoints = new SpawnPoint[0];

        private RoomModule room;
        private readonly List<Enemy.Enemy> alive = new List<Enemy.Enemy>();

        private void Awake()
        {
            room = GetComponent<RoomModule>();
            room.OnCombatStart.AddListener(Spawn);
        }

        private void OnDestroy()
        {
            if (room != null) room.OnCombatStart.RemoveListener(Spawn);
            foreach (Enemy.Enemy enemy in alive)
                if (enemy != null) enemy.Despawned -= OnEnemyDespawned;
        }

        private void Spawn()
        {
            ObjectPoolManager pool = ObjectPoolManager.Instance;
            if (pool == null || spawnPoints.Length == 0)
            {
                Debug.LogWarning($"[RoomSpawner] {name}: ObjectPoolManager 나 스폰 위치가 없습니다.", this);
                room.Clear();
                return;
            }

            int pointIndex = 0;
            foreach (EnemyEntry entry in enemies)
            {
                if (entry.prefab == null || entry.count <= 0) continue;

                string key = entry.prefab.name;
                if (pool.HasPool(key) == false)
                    pool.CreatePool(new ObjectPoolManager.ObjectPoolData(key, entry.prefab.gameObject, (byte)Mathf.Clamp(entry.count, 1, 255)));

                for (int i = 0; i < entry.count; i++)
                {
                    SpawnPoint spawnPoint = spawnPoints[pointIndex];
                    pointIndex = (pointIndex + 1) % spawnPoints.Length;
                    SpawnOne(pool, key, spawnPoint);
                }
            }

            // 꺼낸 적이 없으면 바로 클리어 (막힌 채로 갇히지 않도록)
            if (alive.Count == 0) room.Clear();
        }

        private void SpawnOne(ObjectPoolManager pool, string key, SpawnPoint spawnPoint)
        {
            if (spawnPoint.start == null || spawnPoint.end == null)
            {
                Debug.LogWarning($"[RoomSpawner] {name}: StartPoint / EndPoint 가 비어 있는 스폰 위치가 있습니다.", this);
                return;
            }

            if (pool.GetObjectOrNull(key) is not Enemy.Enemy enemy) return;

            // EndPoint 쪽을 바라보며 등장
            Vector3 start = spawnPoint.start.position;
            Vector3 end = spawnPoint.end.position;
            Vector3 toEnd = end - start;
            toEnd.y = 0f;
            Quaternion rotation = toEnd.sqrMagnitude > 0.0001f ? Quaternion.LookRotation(toEnd) : spawnPoint.start.rotation;

            enemy.transform.SetPositionAndRotation(start, rotation);
            enemy.SetEntrance(end);
            enemy.Despawned += OnEnemyDespawned;
            alive.Add(enemy);
            enemy.gameObject.SetActive(true);
        }

        private void OnEnemyDespawned(Enemy.Enemy enemy)
        {
            enemy.Despawned -= OnEnemyDespawned;
            alive.Remove(enemy);
            if (alive.Count == 0) room.Clear();
        }
    }
}
