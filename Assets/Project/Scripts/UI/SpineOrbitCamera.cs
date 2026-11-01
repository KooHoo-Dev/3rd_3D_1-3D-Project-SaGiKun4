using Study3D.FpsGame.Player;
using UnityEngine;

namespace Study3D.FpsGame.UI
{
    // SpineOrbitCamera
    // : 캐릭터 애니메이션과 상관없이, spine_01을 중심으로 원을 그리며 위아래로 도는 1인칭 카메라 입니다.
    // - 카메라는 플레이어 루트 바로 아래에 두고(head의 자식이 아님), 좌우 회전은 부모(플레이어)를 그대로 따라갑니다.
    // - 상하 회전은 PlayerController의 마우스 각도만 사용하므로, 애니메이션의 흔들림이나 기울어진 자세가 카메라에 들어오지 않습니다.
    //
    // 원리
    // : 척추를 굽히면 spine_03(그리고 그 위의 머리)은 spine_01을 중심으로 원을 그리며 움직입니다.
    //   카메라도 같은 원의 바깥쪽 한 점이라고 보고, 시작 위치를 spine_01 중심으로 시점 각도만큼 돌립니다.
    //   중심점과 시작 위치는 애니메이션이 적용되기 전(Awake)에 한 번만 저장해서, 이후 흔들림의 영향을 받지 않습니다.
    //
    // 벽 충돌
    // : 원 위의 위치는 벽을 고려하지 않으므로, 벽에 붙어 숙이면 카메라가 벽 안으로 들어갑니다.
    //   중심점(몸 안쪽이라 벽 밖)에서 카메라 위치까지 구를 쏴서, 환경에 막히면 막힌 지점 바로 앞에 둡니다.
    public class SpineOrbitCamera : MonoBehaviour
    {
        [Tooltip("마우스 상하 각도를 가져올 플레이어")]
        [SerializeField] private PlayerController player;
        [Tooltip("원의 중심이 될 척추 본 (spine_01)")]
        [SerializeField] private Transform spinePivot;
        [Tooltip("벽 충돌 검사에 쓰는 구의 반지름 (m). 카메라 Near Clip Plane 보다 커야 화면에 벽 속이 보이지 않습니다")]
        [SerializeField] private float collisionRadius = 0.15f;

        // 아래는 모두 플레이어 루트 기준 값입니다.
        private Vector3 pivotLocalPosition;
        private Vector3 initialLocalPosition;
        // 시작 시 카메라가 바라보던 방향의 상하(pitch)/좌우(yaw) 각도. 기울기(roll, Z축)는 저장하지 않습니다.
        private float initialPitch;
        private float initialYaw;

        private void Awake()
        {
            if (player == null || spinePivot == null)
            {
                Debug.LogWarning("[SpineOrbitCamera] player 또는 spinePivot이 비어 있어 카메라 회전을 끕니다.", this);
                enabled = false;
                return;
            }

            // Awake는 Animator가 첫 포즈를 쓰기 전이므로, 에디터에서 배치한 자세가 그대로 저장됩니다.
            Transform root = player.transform;
            pivotLocalPosition = root.InverseTransformPoint(spinePivot.position);
            initialLocalPosition = root.InverseTransformPoint(transform.position);

            // 시작 회전에서 "바라보는 방향"만 뽑아 pitch/yaw로 나눕니다. (오일러 값을 그대로 읽으면 Z 기울기가 섞여 들어옵니다)
            Vector3 localForward = Quaternion.Inverse(root.rotation) * transform.forward;
            initialPitch = -Mathf.Asin(Mathf.Clamp(localForward.y, -1f, 1f)) * Mathf.Rad2Deg;
            initialYaw = Mathf.Atan2(localForward.x, localForward.z) * Mathf.Rad2Deg;
        }

        private void LateUpdate()
        {
            Transform root = player.transform;

            // 플레이어 기준 오른쪽 축(X)으로 시점 각도만큼 돌리는 회전 (+ 각도면 앞으로 숙임)
            Quaternion pitch = Quaternion.AngleAxis(player.VerticalLookAngle, Vector3.right);

            // spine_01 → 카메라 방향의 벡터를 돌려서, 원 위의 새 위치를 구합니다.
            Vector3 orbitPosition = pivotLocalPosition + pitch * (initialLocalPosition - pivotLocalPosition);

            // 회전은 pitch와 yaw만으로 다시 만들어서 Z축 기울기(roll)가 생기지 않게 합니다.
            Quaternion lookRotation = Quaternion.Euler(initialPitch + player.VerticalLookAngle, initialYaw, 0f);

            Vector3 pivotWorld = root.TransformPoint(pivotLocalPosition);
            Vector3 orbitWorld = root.TransformPoint(orbitPosition);
            Vector3 cameraPosition = BlockByEnvironment(pivotWorld, orbitWorld);
            transform.SetPositionAndRotation(cameraPosition, root.rotation * lookRotation);
        }

        // 중심점(월드) → 원하는 카메라 위치(월드) 사이에 환경이 있으면, 막힌 지점 바로 앞을 돌려줍니다.
        private Vector3 BlockByEnvironment(Vector3 pivot, Vector3 desired)
        {
            if (CombatSystem.Instance == null)
                return desired;

            Vector3 toCamera = desired - pivot;
            float distance = toCamera.magnitude;
            if (distance < 0.0001f)
                return desired;

            Vector3 dir = toCamera / distance;
            int environmentMask = CombatSystem.Instance.EnvironmentMask;
            bool blocked = Physics.SphereCast(pivot, collisionRadius, dir, out RaycastHit hit, distance,
                environmentMask, QueryTriggerInteraction.Ignore);
            if (blocked == false)
                return desired;

            // hit.distance = 구의 중심이 이동한 거리라서, 그 위치에 카메라를 두면 벽과 반지름만큼 떨어짐
            return pivot + dir * hit.distance;
        }
    }
}