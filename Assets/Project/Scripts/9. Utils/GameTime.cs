using System;
using UnityEngine;

namespace Study3D.FpsGame
{
    // GameTime
    // : 게임 진행(적, 플레이어 이동, 발사체 등)이 쓰는 시간 입니다. 유니티 시간(Time)과 따로 멈출 수 있습니다.
    // - 화면을 덮는 UI(메뉴, 카드 선택)가 열리면 Pause, 닫히면 Resume 합니다.
    // - 정지 요청은 개수로 셉니다. 카드 선택 화면 위에서 메뉴를 열고 닫아도, 카드 선택 화면이 닫혀야 다시 움직입니다.
    // - UI 연출은 정지 중에도 움직여야 하므로 유니티 시간(Time.deltaTime, Time.unscaledDeltaTime)을 그대로 씁니다.
    // - 애니메이터, 파티클은 이 시간을 모르므로, 각자 PausedChanged 를 받거나 IsPaused 를 보고 직접 멈춥니다.
    public static class GameTime
    {
        private static int pauseCount;
        private static float pauseStartTime; // 정지가 시작된 유니티 시각
        private static float pausedTotal;    // 지금까지 멈춰 있던 시간의 합

        // 정지 상태가 바뀔 때만 알립니다. (true = 멈춤)
        public static event Action<bool> PausedChanged;

        public static bool IsPaused
        {
            get { return pauseCount > 0; }
        }

        // 이번 프레임에 흐른 게임 시간. 멈춰 있으면 0 입니다.
        public static float DeltaTime
        {
            get
            {
                if (IsPaused)
                    return 0f;

                return UnityEngine.Time.deltaTime;
            }
        }

        // 게임이 시작된 뒤 흐른 게임 시각. 멈춰 있던 시간은 빠집니다. (쿨타임처럼 "언제 다시 쓸 수 있는지" 비교할 때 사용)
        public static float Time
        {
            get
            {
                if (IsPaused)
                    return pauseStartTime - pausedTotal;

                return UnityEngine.Time.time - pausedTotal;
            }
        }

        public static void Pause()
        {
            pauseCount++;
            if (pauseCount != 1)
                return;

            pauseStartTime = UnityEngine.Time.time;
            NotifyPausedChanged();
        }

        public static void Resume()
        {
            if (pauseCount <= 0)
            {
                Debug.LogWarning("[GameTime] Pause 없이 Resume 이 불렸습니다.");
                return;
            }

            pauseCount--;
            if (pauseCount != 0)
                return;

            pausedTotal += UnityEngine.Time.time - pauseStartTime;
            NotifyPausedChanged();
        }

        private static void NotifyPausedChanged()
        {
            if (PausedChanged != null)
                PausedChanged.Invoke(IsPaused);
        }

        // 플레이 모드를 시작할 때마다 처음 상태로 되돌립니다. (도메인 리로드를 꺼 두면 static 값이 남기 때문)
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOnPlay()
        {
            pauseCount = 0;
            pauseStartTime = 0f;
            pausedTotal = 0f;
            PausedChanged = null;
        }
    }
}
