using System;
using Study3D.FpsGame.UI;
using UnityEngine;

namespace Study3D.FpsGame.Feedback
{
    // Hit이라고 하는 Effect의 모든 피드백들을 재생시키는 하나의 컨트롤러 입니다.
    // - 특정 코드에서 이것저것 참조하여 재생시키면 종속성이 늘어나서 새로 추가할때 힘듭니다
    // - 이런 느낌의 곁다리 코드들은 한곳에 집약적으로 모아두는게 좋습니다.
    
    public class HitEffectController : MonoBehaviour
    {
        [Header("Settings")] 
        [SerializeField] private string bloodPoolKey = "Blood";
        [SerializeField] private string damageTextPoolKey = "DamageText";
        [SerializeField] private float damageTextDuration = 0.5f;

        [Header("이펙트 예산 (피격, 카드 발동 이펙트 공통)")]
        [Tooltip("모든 이펙트를 합쳐 한 프레임에 새로 만들 수 있는 수. 넘으면 연출만 생략합니다 (피해는 그대로)")]
        [SerializeField] private int maxEffectSpawnsPerFrame = 20;
        [Tooltip("같은 종류의 이펙트가 동시에 살아 있을 수 있는 수")]
        [SerializeField] private int maxActiveEffectsPerKind = 40;

        private void Awake()
        {
            EffectBudget.MaxSpawnsPerFrame = maxEffectSpawnsPerFrame;
            EffectBudget.MaxActivePerKind = maxActiveEffectsPerKind;
        }

        private void Start()
        {
            CombatSystem.Instance.OnCombatEvent += OnHit;
        }

        private void OnDestroy()
        {
            if(CombatSystem.Instance != null) 
                CombatSystem.Instance.OnCombatEvent -= OnHit;
        }

        // 피와 데미지 숫자는 각각 이펙트 예산 안에서만 띄웁니다. (피해는 이미 들어간 뒤라 연출만 생략됩니다)
        private void OnHit(CombatEvent combatEvent)
        {
            if (EffectBudget.TryUse(bloodPoolKey))
            {
                var blood = ObjectPoolManager.Instance.GetObjectOrNull(bloodPoolKey);
                blood.GameObject.transform.position = combatEvent.HitPosition;
                blood.GameObject.SetActive(true);
            }

            if (EffectBudget.TryUse(damageTextPoolKey) == false)
                return;

            FloatingText dmgText =
                ObjectPoolManager.Instance.GetObjectOrNull(damageTextPoolKey) as FloatingText;

            dmgText.Set(
                combatEvent.HitPosition, 
                $"{combatEvent.Damage}",
                damageTextDuration,
                Color.red);

            dmgText.gameObject.SetActive(true);
        }
    }
}