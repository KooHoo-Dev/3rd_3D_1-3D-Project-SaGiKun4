using TMPro;
using UnityEngine;

namespace Study3D.FpsGame.UI
{
    // BossHpView
    // : 화면 위쪽의 보스 체력 바 입니다. (HUD 캔버스에 둠)
    // - 평소에는 숨겨 두고, 보스 전투가 시작되면(Boss.BattleStarted) 그 보스의 체력을 보여줍니다.
    //   보스는 맵 생성 때 만들어지는 보스방 안에 있어서 인스펙터로 연결할 수 없으므로, 시작 알림으로 받습니다.
    // - 바의 길이는 PlayerHpView 처럼 오른쪽 앵커를 체력 비율만큼 옮겨서 줄입니다.
    // - 체력이 바뀔 때(Boss.HpChanged)만 갱신하고, 보스가 죽으면 숨깁니다.
    public class BossHpView : MonoBehaviour
    {
        [Tooltip("켜고 끌 바 전체 (이 컴포넌트가 붙은 오브젝트와 달라야 합니다. 같으면 꺼진 동안 알림을 못 받음)")]
        [SerializeField] private GameObject root;
        [Tooltip("체력 바 (왼쪽 끝 기준으로 늘어나고 줄어듭니다)")]
        [SerializeField] private RectTransform hpFill;
        [Tooltip("보스 이름 (비워두면 표시 안 함)")]
        [SerializeField] private TMP_Text nameText;
        [SerializeField] private string bossName = "사악한 마법사";

        private Boss.Boss subscribedBoss;

        private void Awake()
        {
            root.SetActive(false);
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

            subscribedBoss = boss;
            subscribedBoss.HpChanged += ShowHp;
            subscribedBoss.Died += OnBossDied;

            if (nameText != null)
            {
                nameText.text = bossName;
            }
            ShowHp(boss.Health.HP, boss.Health.MaxHP);
            root.SetActive(true);
        }

        private void OnBossDied(Boss.Boss boss)
        {
            Unsubscribe();
            root.SetActive(false);
        }

        private void Unsubscribe()
        {
            if (subscribedBoss == null)
                return;

            subscribedBoss.HpChanged -= ShowHp;
            subscribedBoss.Died -= OnBossDied;
            subscribedBoss = null;
        }

        private void ShowHp(int hp, int maxHp)
        {
            float ratio = 0f;
            if (maxHp > 0)
            {
                ratio = Mathf.Clamp01((float)hp / maxHp);
            }

            hpFill.anchorMax = new Vector2(ratio, 1f);
        }
    }
}
