using System;
using System.Collections.Generic;
using Study.Utilities;
using Study3D.FpsGame.Player;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Study3D.FpsGame.Interaction
{
    // PlayerInteractor
    // : 플레이어 범위 안에 들어온 Interactable 후보 중 하나를 현재 타겟으로 고르고, F키로 실행합니다. (LocalPlayer와 같은 오브젝트에 붙임)
    // - 후보는 Interactable이 트리거로 넣고 뺍니다. 여기서는 물리 검사를 하지 않습니다.
    // - 카메라가 바라보는 방향과 가장 가까운 후보가 타겟입니다. (후보가 없으면 아무 계산도 하지 않음)
    // - 타겟이 바뀔 때만 OnTargetChanged를 알립니다. UI는 이걸 듣고 문구를 바꿉니다.
    // - 조작할 수 없는 동안(메뉴, 사망)은 F키를 받지 않습니다.
    public class PlayerInteractor : MonoBehaviour
    {
        [Tooltip("바라보는 방향을 잴 카메라")]
        [SerializeField] private Transform viewCamera;
        [SerializeField] private Key interactKey = Key.F;

        // 타겟이 바뀌었을 때 (없으면 null)
        public event Action<Interactable> OnTargetChanged;

        public Interactable Target { get; private set; }
        public Key InteractKey => interactKey;

        private readonly List<Interactable> candidates = new List<Interactable>();

        public void AddCandidate(Interactable interactable)
        {
            if (candidates.Contains(interactable) == false) candidates.Add(interactable);
        }

        public void RemoveCandidate(Interactable interactable)
        {
            candidates.Remove(interactable);
            if (Target == interactable) SetTarget(null);
        }

        private void Update()
        {
            if (candidates.Count == 0) return;

            SetTarget(PickTarget());

            LocalPlayer player = Player.Player.LocalPlayer;
            if (Target == null || player == null || player.CanAct == false) return;
            if (SimpleInput.GetKeyDown(interactKey) && Target.CanInteract(player)) Target.Interact(player);
        }

        // 카메라 정면과 후보 방향의 내적이 가장 큰(가장 정면에 있는) 후보
        private Interactable PickTarget()
        {
            Interactable best = null;
            float bestDot = float.MinValue;
            foreach (Interactable candidate in candidates)
            {
                Vector3 toTarget = candidate.PromptPosition - viewCamera.position;
                float dot = Vector3.Dot(viewCamera.forward, toTarget.normalized);
                if (dot > bestDot)
                {
                    bestDot = dot;
                    best = candidate;
                }
            }
            return best;
        }

        private void SetTarget(Interactable target)
        {
            if (Target == target) return;
            Target = target;
            OnTargetChanged?.Invoke(target);
        }
    }
}
