using Study3D.FpsGame.Boss;
using TMPro;
using UnityEngine;

namespace Study3D.FpsGame.UI
{
    // BossMagicView
    // : 보스 마법 스킬의 대기 게이지와 복사한 카드 이름을 화면에 보여줍니다. (HUD 캔버스에 둠)
    // - 보스는 정보만 알리고(BossMagicSkill 의 알림), 출력은 이 UI 가 합니다.
    //   화면 UI 라서 어느 방향에서 봐도 똑같이 보입니다.
    // - 보스는 맵 생성 뒤에 소환되어 인스펙터로 연결할 수 없으므로, 보스 전투 시작 알림(Boss.BattleStarted)으로 보스를 받아
    //   그 보스의 마법 스킬을 구독합니다. 보스가 죽으면 구독을 풀고 숨깁니다.
    // - 게이지 길이는 BossHpView 처럼 오른쪽 앵커를 비율만큼 옮겨서 정합니다.
    public class BossMagicView : MonoBehaviour
    {
        [Tooltip("대기 중에만 켜지는 게이지 전체 (이 컴포넌트가 붙은 오브젝트와 달라야 합니다)")]
        [SerializeField] private GameObject gaugeRoot;
        [Tooltip("게이지 채움 (왼쪽 끝 기준으로 늘어남)")]
        [SerializeField] private RectTransform gaugeFill;
        [Tooltip("복사한 카드 이름 (보여줄 때만 켜짐. 이 컴포넌트가 붙은 오브젝트와 달라야 합니다)")]
        [SerializeField] private TMP_Text copiedCardText;

        private Boss.Boss subscribedBoss;
        private BossMagicSkill subscribedSkill;

        private void Awake()
        {
            ShowGauge(false);
            ShowCopiedCard(null);
            Boss.Boss.BattleStarted += OnBattleStarted;
        }

        private void OnDestroy()
        {
            Boss.Boss.BattleStarted -= OnBattleStarted;
            Unsubscribe();
        }

        private void OnBattleStarted(Boss.Boss boss)
        {
            Unsubscribe();

            BossMagicSkill skill = boss.GetComponent<BossMagicSkill>();
            if (skill == null)
                return;

            subscribedBoss = boss;
            subscribedSkill = skill;
            subscribedBoss.Died += OnBossDied;
            subscribedSkill.GaugeVisibleChanged += ShowGauge;
            subscribedSkill.GaugeChanged += SetGauge;
            subscribedSkill.CopiedCardChanged += ShowCopiedCard;
        }

        private void OnBossDied(Boss.Boss boss)
        {
            Unsubscribe();
            ShowGauge(false);
            ShowCopiedCard(null);
        }

        private void Unsubscribe()
        {
            if (subscribedBoss != null)
            {
                subscribedBoss.Died -= OnBossDied;
                subscribedBoss = null;
            }
            if (subscribedSkill != null)
            {
                subscribedSkill.GaugeVisibleChanged -= ShowGauge;
                subscribedSkill.GaugeChanged -= SetGauge;
                subscribedSkill.CopiedCardChanged -= ShowCopiedCard;
                subscribedSkill = null;
            }
        }

        private void ShowGauge(bool isVisible)
        {
            if (gaugeRoot != null)
            {
                gaugeRoot.SetActive(isVisible);
            }
        }

        private void SetGauge(float ratio)
        {
            if (gaugeFill != null)
            {
                gaugeFill.anchorMax = new Vector2(Mathf.Clamp01(ratio), 1f);
            }
        }

        // null 이면 숨깁니다.
        private void ShowCopiedCard(string cardName)
        {
            if (copiedCardText == null)
                return;

            if (cardName == null)
            {
                copiedCardText.gameObject.SetActive(false);
                return;
            }

            copiedCardText.text = cardName;
            copiedCardText.gameObject.SetActive(true);
        }
    }
}
