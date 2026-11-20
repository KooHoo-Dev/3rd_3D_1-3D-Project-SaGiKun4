using Study3D.FpsGame.Player;
using UnityEngine;
using UnityEngine.UI;

namespace Study3D.FpsGame.Feedback
{
    // ScreenEdgeEffect
    // : 화면 가장자리 연출을 담당합니다. (ScreenEdgeEffectUI 셰이더를 쓰는 화면 전체 Image 하나로 그립니다)
    // - 달리기 : 앞으로 달리는 동안 가장자리에 가시 모양 속도선이 서서히 나타나고, 멈추면 사라집니다.
    // - 피격 : 맞는 순간 가장자리가 빨갛게 번쩍였다가 서서히 사라집니다. 체력이 낮으면 옅은 빨강이 계속 남습니다.
    // - 두 효과가 모두 0이면 Image를 꺼서 화면 전체를 그리는 비용을 없앱니다.
    // - 게임이 멈추면(GameTime) 효과도 그 자리에서 멈춥니다.
    public class ScreenEdgeEffect : MonoBehaviour
    {
        private static readonly int SpeedAmountId = Shader.PropertyToID("_SpeedAmount");
        private static readonly int DamageAmountId = Shader.PropertyToID("_DamageAmount");
        private static readonly int EffectTimeId = Shader.PropertyToID("_EffectTime");

        [Header("Ref")]
        [Tooltip("화면 전체를 덮는 Image (머티리얼: M_ScreenEdgeEffect, Raycast Target 끄기)")]
        [SerializeField] private Image effectImage;
        [SerializeField] private PlayerController playerController;

        [Header("속도선 (달리기)")]
        [Tooltip("1초에 나타나는 양 (클수록 빨리 나타남)")]
        [SerializeField] private float speedFadeInSpeed = 4f;
        [Tooltip("1초에 사라지는 양")]
        [SerializeField] private float speedFadeOutSpeed = 6f;

        [Header("피격 (빨간 가장자리)")]
        [Tooltip("맞았을 때 최소 번쩍임 세기")]
        [SerializeField] private float minHitFlash = 0.5f;
        [Tooltip("최대 체력 대비 받은 피해 비율에 곱해서 번쩍임에 더할 양 (최대 체력의 25%를 맞으면 0.25 x 이 값)")]
        [SerializeField] private float hitFlashPerDamageRatio = 2f;
        [Tooltip("1초에 사라지는 양")]
        [SerializeField] private float hitFadeOutSpeed = 1.5f;
        [Tooltip("체력 비율이 이 값 이하면 옅은 빨강이 계속 남습니다")]
        [SerializeField] private float lowHpRatio = 0.3f;
        [Tooltip("체력이 낮을 때 계속 남는 빨강의 최대 세기 (체력이 0에 가까울수록 이 값에 가까워짐)")]
        [SerializeField] private float lowHpMaxAmount = 0.35f;

        private Material runtimeMaterial;
        private LocalPlayer subscribedPlayer;
        private float speedAmount;
        private float hitFlashAmount;
        private float lowHpAmount;
        private float effectTime;

        private void Awake()
        {
            // 머티리얼 에셋 자체를 바꾸지 않도록 복사본을 만들어 씁니다.
            runtimeMaterial = new Material(effectImage.material);
            effectImage.material = runtimeMaterial;
            effectImage.raycastTarget = false;
            effectImage.enabled = false;
        }

        private void Start()
        {
            subscribedPlayer = Player.Player.LocalPlayer;
            if (subscribedPlayer == null)
            {
                Debug.LogWarning("[ScreenEdgeEffect] LocalPlayer가 없어 피격 연출을 하지 않습니다.", this);
                return;
            }

            subscribedPlayer.OnTakenDamage += OnTakenDamage;
            subscribedPlayer.OnHealed += OnHealed;
        }

        private void OnDestroy()
        {
            if (subscribedPlayer != null)
            {
                subscribedPlayer.OnTakenDamage -= OnTakenDamage;
                subscribedPlayer.OnHealed -= OnHealed;
            }

            if (runtimeMaterial != null)
            {
                Destroy(runtimeMaterial);
            }
        }

        private void Update()
        {
            float deltaTime = GameTime.DeltaTime;
            effectTime += deltaTime;

            UpdateSpeedAmount(deltaTime);
            UpdateHitFlash(deltaTime);
            ApplyToMaterial();
        }

        // 앞으로 달리는 중이면 1을 향해, 아니면 0을 향해 움직입니다.
        private void UpdateSpeedAmount(float deltaTime)
        {
            bool isRunning = false;
            if (playerController != null)
            {
                isRunning = playerController.IsRunningForward;
            }

            if (isRunning)
            {
                speedAmount = Mathf.MoveTowards(speedAmount, 1f, speedFadeInSpeed * deltaTime);
            }
            else
            {
                speedAmount = Mathf.MoveTowards(speedAmount, 0f, speedFadeOutSpeed * deltaTime);
            }
        }

        private void UpdateHitFlash(float deltaTime)
        {
            hitFlashAmount = Mathf.MoveTowards(hitFlashAmount, 0f, hitFadeOutSpeed * deltaTime);
        }

        private void ApplyToMaterial()
        {
            // 번쩍임과 낮은 체력 중 더 큰 쪽을 씁니다. (번쩍임이 사라져도 낮은 체력 빨강은 남음)
            float damageAmount = Mathf.Max(hitFlashAmount, lowHpAmount);

            bool visible = speedAmount > 0.001f || damageAmount > 0.001f;
            if (effectImage.enabled != visible)
            {
                effectImage.enabled = visible;
            }

            if (visible == false)
            {
                return;
            }

            runtimeMaterial.SetFloat(SpeedAmountId, speedAmount);
            runtimeMaterial.SetFloat(DamageAmountId, damageAmount);
            runtimeMaterial.SetFloat(EffectTimeId, effectTime);
        }

        private void OnTakenDamage(int damage, int hp, int maxHp)
        {
            float damageRatio = 0f;
            if (maxHp > 0)
            {
                damageRatio = (float)damage / maxHp;
            }

            float flash = Mathf.Clamp01(minHitFlash + damageRatio * hitFlashPerDamageRatio);
            hitFlashAmount = Mathf.Max(hitFlashAmount, flash);
            UpdateLowHpAmount(hp, maxHp);
        }

        private void OnHealed(int healed, int hp, int maxHp)
        {
            UpdateLowHpAmount(hp, maxHp);
        }

        // 체력이 lowHpRatio 이하로 떨어지면, 적게 남을수록 진한 빨강이 계속 남게 합니다.
        private void UpdateLowHpAmount(int hp, int maxHp)
        {
            if (maxHp <= 0 || lowHpRatio <= 0f)
            {
                lowHpAmount = 0f;
                return;
            }

            float hpRatio = (float)hp / maxHp;
            if (hpRatio > lowHpRatio)
            {
                lowHpAmount = 0f;
                return;
            }

            float danger = 1f - (hpRatio / lowHpRatio);
            lowHpAmount = Mathf.Lerp(lowHpMaxAmount * 0.4f, lowHpMaxAmount, danger);
        }

#if UNITY_EDITOR
        // 컴포넌트 우클릭 메뉴: 연결한 Image를 화면 전체로 늘리고, 클릭을 막지 않게 합니다.
        [ContextMenu("Image를 화면 전체로 맞추기")]
        private void FitImageToScreen()
        {
            if (effectImage == null)
            {
                Debug.LogWarning("[ScreenEdgeEffect] Effect Image를 먼저 연결하세요.", this);
                return;
            }

            RectTransform rect = effectImage.rectTransform;
            UnityEditor.Undo.RecordObject(rect, "Fit Screen Edge Effect");
            UnityEditor.Undo.RecordObject(effectImage, "Fit Screen Edge Effect");
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            effectImage.raycastTarget = false;
            effectImage.sprite = null;
            effectImage.color = Color.white;
            // 다른 UI보다 위에 그려지지 않게 맨 앞(뒤쪽에 그려짐)으로 보냅니다. HUD가 효과 위에 보입니다.
            rect.SetAsFirstSibling();
        }
#endif
    }
}
