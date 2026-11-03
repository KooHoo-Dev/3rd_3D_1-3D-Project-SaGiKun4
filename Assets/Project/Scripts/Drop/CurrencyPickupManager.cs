using System.Collections.Generic;
using UnityEngine;

namespace Study3D.FpsGame.Drop
{
    // CurrencyPickupManager
    // : 살아 있는 재화들의 한 프레임 처리(CurrencyPickup.Tick)를 한곳에서 호출합니다. (ProjectileManager와 같은 방식)
    // - 재화마다 Update를 두면 엔진이 재화 수만큼 스크립트를 따로 호출해서, 재화가 많을 때 호출 비용이 커집니다.
    // - 씬에 따로 둘 필요 없이, 처음 재화가 등록될 때 스스로 만들어집니다. (씬이 바뀌면 씬과 함께 사라짐)
    // - 재화는 흩뿌려질 때 등록되고, 획득되거나 꺼질 때 빠집니다.
    public class CurrencyPickupManager : MonoBehaviour
    {
        private static CurrencyPickupManager instance;

        private readonly List<CurrencyPickup> pickups = new List<CurrencyPickup>();

        public static void Register(CurrencyPickup pickup)
        {
            if (pickup.ManagerIndex >= 0)
                return;

            if (instance == null)
                instance = new GameObject(nameof(CurrencyPickupManager)).AddComponent<CurrencyPickupManager>();

            pickup.ManagerIndex = instance.pickups.Count;
            instance.pickups.Add(pickup);
        }

        // 빠진 자리에 맨 뒤 재화를 옮겨서, 앞쪽 재화들을 당기지 않고 바로 뺍니다.
        public static void Unregister(CurrencyPickup pickup)
        {
            int index = pickup.ManagerIndex;
            if (index < 0 || instance == null)
                return;

            List<CurrencyPickup> list = instance.pickups;
            int lastIndex = list.Count - 1;
            CurrencyPickup last = list[lastIndex];
            list[index] = last;
            last.ManagerIndex = index;
            list.RemoveAt(lastIndex);
            pickup.ManagerIndex = -1;
        }

        // 씬이 바뀌어 매니저가 사라질 때, 남은 재화가 다음에 다시 등록될 수 있게 위치를 지웁니다.
        private void OnDestroy()
        {
            foreach (CurrencyPickup pickup in pickups)
            {
                if (pickup != null)
                    pickup.ManagerIndex = -1;
            }
            pickups.Clear();
            if (instance == this)
                instance = null;
        }

        // 뒤에서부터 처리합니다. 획득된 재화 자리에는 이미 처리한 맨 뒤 재화가 옮겨 오므로 이번 프레임에 다시 처리하지 않습니다.
        private void Update()
        {
            float deltaTime = GameTime.DeltaTime;
            for (int i = pickups.Count - 1; i >= 0; i--)
            {
                if (i < pickups.Count)
                    pickups[i].Tick(deltaTime);
            }
        }
    }
}
