// ─────────────────────────────────────────────
// AnimStateEventSender — 애니메이터 상태의 진행률에 맞춰 신호를 보내는 StateMachineBehaviour
// 학습 포인트
//  - 상용 FBX의 애니메이션 이벤트를 고치지 않고도, 상태 진행률(normalizedTime)로 '재장전 끝' 같은 시점을 알릴 수 있습니다
//  - 상태에 들어올 때 IsComplete를 지워, 같은 신호가 한 번 재생에 한 번만 나가게 합니다
//  - sendOnExit를 켠 신호는 상태가 그 시점 전에 끊겨도 빠져나갈 때 보내, '재장전 끝'처럼 꼭 와야 하는 신호가 사라지지 않게 합니다
// 조작
//  - 없음
// ─────────────────────────────────────────────
using System;
using UnityEngine;

namespace Study3D.FpsGame
{
    public class AnimStateEventSender : StateMachineBehaviour
    {
        [Serializable]
        public class AnimStateEvent
        {
            [SerializeField] private string name;
            [SerializeField] [Range(0f, 1f)] private float time;
            [SerializeField] private string parameter;
            [Tooltip("시점에 닿기 전에 상태를 빠져나가도(전이로 끊겨도) 빠져나갈 때 보냅니다")]
            [SerializeField] private bool sendOnExit;

            public string Name => name;
            public float NormalizedTime => time;
            public string Parameter => parameter;
            public bool SendOnExit => sendOnExit;
            public bool IsComplete { get; set; }
        }

        [Header("상태 진행률(0~1)에 맞춰 보낼 신호")]
        [SerializeField] private AnimStateEvent[] events;

        private AnimStateEventListener eventListener;

        public override void OnStateEnter(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
        {
            // 상태에 들어올 때마다 지난 재생에서 보낸 기록을 지웁니다
            for (int i = 0; i < events.Length; i++)
            {
                events[i].IsComplete = false;
            }

            if (eventListener == null) eventListener = animator.GetComponent<AnimStateEventListener>();
        }

        public override void OnStateUpdate(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
        {
            for (int i = 0; i < events.Length; i++)
            {
                var eventData = events[i];
                if (eventData.IsComplete) continue;
                if (eventData.NormalizedTime > stateInfo.normalizedTime) continue; // 아직 보낼 시점이 아님

                eventData.IsComplete = true;
                eventListener.OccursAnimStateEvent(eventData.Name);
            }
        }

        // 정상 종료든 전이로 끊긴 것이든 상태를 빠져나갈 때마다 호출됩니다.
        // 이미 보낸 신호(IsComplete)는 건너뛰므로, 끊겨서 못 보낸 신호 중 sendOnExit를 켠 것만 보냅니다.
        public override void OnStateExit(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
        {
            for (int i = 0; i < events.Length; i++)
            {
                var eventData = events[i];
                if (eventData.IsComplete || eventData.SendOnExit == false) continue;

                eventData.IsComplete = true;
                eventListener.OccursAnimStateEvent(eventData.Name);
            }
        }
    }
}
