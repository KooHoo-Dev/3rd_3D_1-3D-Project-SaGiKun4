using System.Collections;
using Study3D.FpsGame.Drop;
using Study3D.FpsGame.Player;
using UnityEngine;

namespace Study3D.FpsGame.Interaction
{
    // ChestInteractable
    // : 보상 상자. F키를 누르면 잠깐 흔들린 뒤 뚜껑이 열리고, 열리는 순간 골드와 카드 보상을 꺼냅니다. 한 번만 열립니다.
    // - 보상은 몬스터와 같은 것을 씁니다. 골드는 CurrencyPickup.Scatter로 흩뿌리고, 카드는 CardDropTable.Drop으로 카드 보상(CardDrop)을 놓습니다.
    //   (카드 보상은 놓인 뒤 몬스터 보상처럼 F키로 따로 고릅니다)
    // - 흔들림: 상자(이 오브젝트)를 앞뒤 축(로컬 Z)으로 좌우로 기울이며, 갈수록 약해집니다.
    // - 열림: 뚜껑(lid)을 닫힌 회전에서 로컬 X축으로 openAngle만큼 돌립니다.
    // - 누르는 즉시 상호작용 범위(트리거)와 이 컴포넌트를 꺼서 프롬프트를 지웁니다.
    //   컴포넌트를 꺼도 이미 시작한 코루틴은 계속 돕니다.
    public class ChestInteractable : Interactable
    {
        [Header("연결")]
        [Tooltip("열릴 뚜껑 (ChestSmall1_Lid)")]
        [SerializeField] private Transform lid;
        [Tooltip("상호작용 범위 트리거. 열면 꺼서 다시 잡히지 않게 합니다")]
        [SerializeField] private Collider interactTrigger;
        [Tooltip("보상이 나올 위치 (상자 안). 비우면 이 오브젝트 위치")]
        [SerializeField] private Transform rewardPoint;

        [Header("흔들림")]
        [SerializeField] private float shakeDuration = 0.5f;
        [SerializeField] private float shakeAngle = 6f;     // 처음 기울기 (도)
        [SerializeField] private float shakeFrequency = 8f; // 초당 왕복 횟수

        [Header("열림")]
        [SerializeField] private float openAngle = -120f; // 뚜껑 로컬 X축 회전량 (도)
        [SerializeField] private float openDuration = 0.35f;

        [Header("보상")]
        [Tooltip("카드 보상의 선택지를 정하는 표 (비워두면 카드를 주지 않음)")]
        [SerializeField] private CardDropTable dropTable;
        [Tooltip("흩뿌릴 골드 프리팹 (비워두면 골드를 주지 않음)")]
        [SerializeField] private CurrencyPickup goldPrefab;
        [SerializeField] private int goldCount = 5;

        private bool isOpened;

        public override bool CanInteract(LocalPlayer player)
        {
            return isOpened == false;
        }

        public override void Interact(LocalPlayer player)
        {
            isOpened = true;
            StartCoroutine(OpenRoutine());

            // 코루틴을 먼저 시작한 뒤 끕니다. (꺼진 컴포넌트에서는 코루틴을 시작할 수 없음)
            if (interactTrigger != null)
                interactTrigger.enabled = false;
            enabled = false;
        }

        // 흔들림 → 보상 → 뚜껑 열림
        private IEnumerator OpenRoutine()
        {
            yield return ShakeRoutine();
            GiveRewards();
            yield return OpenLidRoutine();
        }

        private IEnumerator ShakeRoutine()
        {
            Quaternion startRotation = transform.localRotation;
            float time = 0f;
            while (time < shakeDuration)
            {
                time += Time.deltaTime;
                float fade = 1f - Mathf.Clamp01(time / shakeDuration); // 갈수록 약해짐
                float angle = Mathf.Sin(time * shakeFrequency * Mathf.PI * 2f) * shakeAngle * fade;
                transform.localRotation = startRotation * Quaternion.Euler(0f, 0f, angle);
                yield return null;
            }
            transform.localRotation = startRotation;
        }

        private IEnumerator OpenLidRoutine()
        {
            if (lid == null)
            {
                Debug.LogWarning("[ChestInteractable] 뚜껑(lid)이 연결되지 않았습니다.", this);
                yield break;
            }

            Quaternion closedRotation = lid.localRotation;
            Quaternion openedRotation = closedRotation * Quaternion.Euler(openAngle, 0f, 0f);
            float time = 0f;
            while (time < openDuration)
            {
                time += Time.deltaTime;
                float t = Mathf.Clamp01(time / openDuration);
                t = 1f - (1f - t) * (1f - t); // 처음엔 빠르고 끝에서 느려짐
                lid.localRotation = Quaternion.Slerp(closedRotation, openedRotation, t);
                yield return null;
            }
            lid.localRotation = openedRotation;
        }

        // 몬스터 보상과 같은 방식으로 골드를 흩뿌리고 카드 보상을 놓습니다.
        private void GiveRewards()
        {
            Vector3 point = transform.position;
            if (rewardPoint != null)
                point = rewardPoint.position;

            CurrencyPickup.Scatter(goldPrefab, goldCount, point, transform.position.y);

            if (dropTable != null)
                dropTable.Drop(point);
        }
    }
}
