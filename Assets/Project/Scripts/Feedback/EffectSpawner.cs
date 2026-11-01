using System.Collections.Generic;
using UnityEngine;

namespace Study3D.FpsGame
{
    // EffectSpawner
    // : 이펙트 프리팹만 넘기면 위치, 회전, 크기를 잡아 재생해주는 재생기 입니다. (카드 발동 이펙트 등)
    // - 처음 쓰는 프리팹이면 ObjectPoolManager에 그 프리팹의 풀을 자동으로 만듭니다. Inspector 목록에 따로 넣지 않아도 됩니다.
    // - 에셋 스토어 파티클처럼 IObjectPoolItem이 없는 프리팹은, ParticleEffect를 붙인 원본(template)을 한 번 만들어 그걸로 풀을 만듭니다.
    //   (원본 프리팹 에셋은 수정하지 않습니다)
    // - ObjectPoolManager가 없으면 Instantiate 후 재생 시간이 지나면 Destroy 합니다.
    public static class EffectSpawner
    {
        private const byte POOL_EXPAND_SIZE = 8;

        private static readonly Dictionary<GameObject, string> poolKeys = new Dictionary<GameObject, string>();

        // 이펙트 예산(EffectBudget)을 넘으면 재생하지 않습니다. (연출만 생략, 카드 효과는 이미 처리된 뒤)
        public static void Play(GameObject prefab, Vector3 position, Quaternion rotation, float scale = 1f)
        {
            if (prefab == null) return;
            if (EffectBudget.TryUse(GetBudgetKey(prefab)) == false)
                return;

            GameObject effect = GetPooledOrNull(prefab);
            if (effect == null)
            {
                effect = Object.Instantiate(prefab, position, rotation);
                effect.transform.localScale = prefab.transform.localScale * scale;
                Object.Destroy(effect, GetDuration(effect));
                return;
            }

            // 풀에서 꺼낸 이펙트는 꺼진 상태라서, 자리를 먼저 잡고 켭니다. (켜질 때 playOnAwake로 재생됩니다)
            effect.transform.SetPositionAndRotation(position, rotation);
            effect.transform.localScale = prefab.transform.localScale * scale;
            effect.SetActive(true);
        }

        private static GameObject GetPooledOrNull(GameObject prefab)
        {
            ObjectPoolManager pool = ObjectPoolManager.Instance;
            if (pool == null) return null;

            string key = GetPoolKey(prefab);
            if (pool.HasPool(key) == false)
                pool.CreatePool(new ObjectPoolManager.ObjectPoolData(key, CreateTemplate(prefab, pool.transform), POOL_EXPAND_SIZE));

            return pool.GetObjectOrNull(key)?.GameObject;
        }

        // 프리팹마다 하나씩 정해지는 풀 이름
        private static string GetPoolKey(GameObject prefab)
        {
            if (poolKeys.TryGetValue(prefab, out string key))
                return key;

            key = $"Effect_{prefab.name}_{prefab.GetInstanceID()}";
            poolKeys.Add(prefab, key);
            return key;
        }

        // 이펙트 예산이 동시 개수를 셀 풀 이름. 풀 매니저가 없으면(Instantiate로 만듦) null
        private static string GetBudgetKey(GameObject prefab)
        {
            if (ObjectPoolManager.Instance == null)
                return null;

            return GetPoolKey(prefab);
        }

        // 풀에 넣을 수 있는 원본을 만듭니다. 이미 IObjectPoolItem이 있으면 프리팹을 그대로 씁니다.
        // 꺼진 부모 아래에 복제해서, 원본 자체는 재생되지도 보이지도 않게 합니다.
        private static GameObject CreateTemplate(GameObject prefab, Transform poolRoot)
        {
            if (prefab.GetComponent<IObjectPoolItem>() != null) return prefab;

            Transform templateRoot = poolRoot.Find("EffectTemplates");
            if (templateRoot == null)
            {
                templateRoot = new GameObject("EffectTemplates").transform;
                templateRoot.SetParent(poolRoot, false);
                templateRoot.gameObject.SetActive(false);
            }

            GameObject template = Object.Instantiate(prefab, templateRoot);
            template.name = prefab.name;
            template.AddComponent<ParticleEffect>();
            return template;
        }

        // 풀이 없을 때 Destroy까지 기다릴 시간: 가장 늦게 끝나는 파티클 기준
        private static float GetDuration(GameObject effect)
        {
            float duration = 0f;
            foreach (ParticleSystem particle in effect.GetComponentsInChildren<ParticleSystem>())
            {
                ParticleSystem.MainModule main = particle.main;
                duration = Mathf.Max(duration, main.duration + main.startLifetime.constantMax);
            }
            return duration;
        }
    }
}
