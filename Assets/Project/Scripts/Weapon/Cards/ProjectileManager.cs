using System.Collections.Generic;
using UnityEngine;

namespace Study3D.FpsGame.Weapon.Cards
{
    // ProjectileManager
    // : 살아 있는 발사체들의 한 프레임 처리(Projectile.Tick)를 한곳에서 호출합니다.
    // - 발사체마다 Update를 두면 엔진이 발사체 수만큼 스크립트를 따로 호출해서, 발사체가 많을 때 호출 비용이 커집니다.
    // - 씬에 따로 둘 필요 없이, 처음 발사체가 등록될 때 스스로 만들어집니다. (씬이 바뀌면 씬과 함께 사라짐)
    // - 발사체는 발사할 때 등록되고, 소멸하거나 꺼질 때 빠집니다.
    // - 이번 프레임에 새로 생긴 발사체(분열 등)는 다음 프레임부터 처리합니다.
    // - 동시에 존재할 수 있는 발사체 수에 상한(Capacity)이 있습니다. 가득 차면 분열은 새 발사체 대신 부모의 화력을 키웁니다.
    //   (플레이어가 직접 쏘는 발사체는 상한과 상관없이 나갑니다)
    public class ProjectileManager : MonoBehaviour
    {
        private static ProjectileManager instance;

        private readonly List<Projectile> projectiles = new List<Projectile>();

        // 동시 발사체 상한. 매니저는 씬에 따로 두지 않으므로 Weapon이 인스펙터 값으로 정해줍니다.
        public static int Capacity { get; set; } = 500;

        public static bool IsFull
        {
            get { return instance != null && instance.projectiles.Count >= Capacity; }
        }

        // 맵 경계. 맵 생성기가 맵을 만든 뒤 정해줍니다. 이 밖으로 나간 발사체는 조용히 회수됩니다. (경계가 없으면 검사하지 않음)
        private static Bounds worldBounds;
        private static bool hasWorldBounds;

        public static void SetWorldBounds(Bounds bounds)
        {
            worldBounds = bounds;
            hasWorldBounds = true;
        }

        public static void ClearWorldBounds()
        {
            hasWorldBounds = false;
        }

        public static bool IsOutsideWorld(Vector3 position)
        {
            return hasWorldBounds && worldBounds.Contains(position) == false;
        }

        public static void Register(Projectile projectile)
        {
            if (projectile.ManagerIndex >= 0)
                return;

            if (instance == null)
                instance = new GameObject(nameof(ProjectileManager)).AddComponent<ProjectileManager>();

            projectile.ManagerIndex = instance.projectiles.Count;
            instance.projectiles.Add(projectile);
        }

        // 빠진 자리에 맨 뒤 발사체를 옮겨서, 앞쪽 발사체들을 당기지 않고 바로 뺍니다.
        public static void Unregister(Projectile projectile)
        {
            int index = projectile.ManagerIndex;
            if (index < 0 || instance == null)
                return;

            List<Projectile> list = instance.projectiles;
            int lastIndex = list.Count - 1;
            Projectile last = list[lastIndex];
            list[index] = last;
            last.ManagerIndex = index;
            list.RemoveAt(lastIndex);
            projectile.ManagerIndex = -1;

            // 실제로 빠진 경우에만 여기까지 옵니다. (이미 빠진 발사체는 위에서 돌아감)
            // 그래서 소멸, 맵 밖 회수, 강제로 꺼짐 중 어느 경로든 공격의 살아 있는 수가 한 번만 줄어듭니다.
            AttackTracker.RemoveProjectile(projectile.AttackId);
        }

        // 씬이 바뀌어 매니저가 사라질 때, 남은 발사체(풀에 살아남는 경우 등)가 다음에 다시 등록될 수 있게 위치를 지웁니다.
        private void OnDestroy()
        {
            foreach (Projectile projectile in projectiles)
            {
                if (projectile != null)
                    projectile.ManagerIndex = -1;
            }
            projectiles.Clear();
            hasWorldBounds = false; // 다음 씬의 맵 생성기가 새로 정해줍니다.
            if (instance == this)
                instance = null;
        }

        // 뒤에서부터 처리합니다. 소멸한 발사체 자리에는 맨 뒤 발사체가 옮겨 오는데,
        // 그건 이미 처리했거나 이번 프레임에 새로 생긴 발사체라서 이번 프레임에 다시 처리하지 않습니다.
        private void Update()
        {
            float deltaTime = Time.deltaTime;
            for (int i = projectiles.Count - 1; i >= 0; i--)
            {
                // 한 발사체가 처리 중에 여러 발사체를 빼면 목록이 i보다 짧아질 수 있습니다.
                if (i < projectiles.Count)
                    projectiles[i].Tick(deltaTime);
            }
        }
    }
}
