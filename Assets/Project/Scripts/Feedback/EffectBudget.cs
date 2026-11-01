using UnityEngine;

namespace Study3D.FpsGame
{
    // EffectBudget
    // : 이펙트(연출)를 한꺼번에 너무 많이 만들지 않도록 막는 예산 입니다. 피해 같은 게임 결과와는 상관없습니다.
    // - 프레임당 생성 수: 모든 이펙트를 합쳐 한 프레임에 MaxSpawnsPerFrame개까지 새로 만듭니다.
    // - 동시 개수: 같은 풀(종류)에서 꺼낸 이펙트가 MaxActivePerKind개 이상 살아 있으면 더 만들지 않습니다.
    // - 예산을 넘은 요청은 이펙트만 생략합니다. 호출한 쪽은 피해 처리를 그대로 합니다.
    // - 값은 HitEffectController 인스펙터에서 정합니다. (없으면 기본값)
    public static class EffectBudget
    {
        public static int MaxSpawnsPerFrame { get; set; } = 20;
        public static int MaxActivePerKind { get; set; } = 40;

        private static int countedFrame = -1;
        private static int spawnedThisFrame;

        // 이펙트 하나를 만들어도 되면 예산을 쓰고 true를 반환합니다.
        // poolKey가 없으면(풀 없이 Instantiate 하는 경우) 동시 개수는 세지 않고 프레임당 생성 수만 봅니다.
        public static bool TryUse(string poolKey)
        {
            ResetCountIfNewFrame();
            if (spawnedThisFrame >= MaxSpawnsPerFrame)
                return false;

            if (IsKindFull(poolKey))
                return false;

            spawnedThisFrame++;
            return true;
        }

        // 프레임이 바뀌었으면 이번 프레임 생성 수를 0부터 다시 셉니다.
        private static void ResetCountIfNewFrame()
        {
            if (countedFrame == Time.frameCount)
                return;

            countedFrame = Time.frameCount;
            spawnedThisFrame = 0;
        }

        // poolKey 풀에서 꺼낸 이펙트가 동시 개수 상한만큼 살아 있는지
        private static bool IsKindFull(string poolKey)
        {
            ObjectPoolManager pool = ObjectPoolManager.Instance;
            if (pool == null || string.IsNullOrEmpty(poolKey))
                return false;

            return pool.GetActiveCount(poolKey) >= MaxActivePerKind;
        }
    }
}
