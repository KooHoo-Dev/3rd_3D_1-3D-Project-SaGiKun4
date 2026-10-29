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
    public class ProjectileManager : MonoBehaviour
    {
        private static ProjectileManager instance;

        private readonly List<Projectile> projectiles = new List<Projectile>();

        public static void Register(Projectile projectile)
        {
            if (projectile.ManagerIndex >= 0) return;

            if (instance == null) instance = new GameObject(nameof(ProjectileManager)).AddComponent<ProjectileManager>();
            projectile.ManagerIndex = instance.projectiles.Count;
            instance.projectiles.Add(projectile);
        }

        // 빠진 자리에 맨 뒤 발사체를 옮겨서, 앞쪽 발사체들을 당기지 않고 바로 뺍니다.
        public static void Unregister(Projectile projectile)
        {
            int index = projectile.ManagerIndex;
            if (index < 0 || instance == null) return;

            List<Projectile> list = instance.projectiles;
            int lastIndex = list.Count - 1;
            Projectile last = list[lastIndex];
            list[index] = last;
            last.ManagerIndex = index;
            list.RemoveAt(lastIndex);
            projectile.ManagerIndex = -1;
        }

        // 씬이 바뀌어 매니저가 사라질 때, 남은 발사체(풀에 살아남는 경우 등)가 다음에 다시 등록될 수 있게 위치를 지웁니다.
        private void OnDestroy()
        {
            foreach (Projectile projectile in projectiles)
            {
                if (projectile != null) projectile.ManagerIndex = -1;
            }
            projectiles.Clear();
            if (instance == this) instance = null;
        }

        // 뒤에서부터 처리합니다. 소멸한 발사체 자리에는 맨 뒤 발사체가 옮겨 오는데,
        // 그건 이미 처리했거나 이번 프레임에 새로 생긴 발사체라서 이번 프레임에 다시 처리하지 않습니다.
        private void Update()
        {
            float deltaTime = Time.deltaTime;
            for (int i = projectiles.Count - 1; i >= 0; i--)
            {
                if (i < projectiles.Count) projectiles[i].Tick(deltaTime);
            }
        }
    }
}
