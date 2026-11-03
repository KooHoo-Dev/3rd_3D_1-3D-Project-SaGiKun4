using Study3D.FpsGame.Player;
using TMPro;
using UnityEngine;

namespace Study3D.FpsGame.UI
{
    // PlayerHpView
    // : 화면 왼쪽 아래 캐릭터 초상화 옆에 초록색 체력 바와 "현재 / 최대" 숫자를 보여줍니다. (실드는 없음)
    // - 바의 길이는 이미지 채우기 대신 오른쪽 앵커를 체력 비율만큼 옮겨서 줄입니다. (스프라이트가 없어도 됨)
    // - 피격(OnTakenDamage)과 회복(OnHealed) 때만 갱신합니다.
    public class PlayerHpView : MonoBehaviour
    {
        [Tooltip("초록색 체력 바 (왼쪽 끝 기준으로 늘어나고 줄어듭니다)")]
        [SerializeField] private RectTransform hpFill;
        [SerializeField] private TMP_Text hpText;

        private LocalPlayer subscribedPlayer;

        private void Start()
        {
            subscribedPlayer = Player.Player.LocalPlayer;
            if (subscribedPlayer == null)
            {
                Debug.LogWarning("[PlayerHpView] LocalPlayer가 없어 체력을 표시하지 않습니다.", this);
                return;
            }

            subscribedPlayer.OnTakenDamage += OnTakenDamage;
            subscribedPlayer.OnHealed += OnHealed;
            ShowHp(subscribedPlayer.Health.HP, subscribedPlayer.Health.MaxHP);
        }

        private void OnDestroy()
        {
            if (subscribedPlayer != null)
            {
                subscribedPlayer.OnTakenDamage -= OnTakenDamage;
                subscribedPlayer.OnHealed -= OnHealed;
            }
        }

        private void OnTakenDamage(int damage, int hp, int maxHp)
        {
            ShowHp(hp, maxHp);
        }

        private void OnHealed(int healed, int hp, int maxHp)
        {
            ShowHp(hp, maxHp);
        }

        private void ShowHp(int hp, int maxHp)
        {
            float ratio = 0f;
            if (maxHp > 0)
            {
                ratio = Mathf.Clamp01((float)hp / maxHp);
            }

            hpFill.anchorMax = new Vector2(ratio, 1f);
            hpText.SetText("{0} / {1}", hp, maxHp);
        }

#if UNITY_EDITOR
        public void EditorSetup(RectTransform hpFill, TMP_Text hpText)
        {
            this.hpFill = hpFill;
            this.hpText = hpText;
        }
#endif
    }
}
