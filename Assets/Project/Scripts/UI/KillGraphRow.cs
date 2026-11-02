using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Study3D.FpsGame.UI
{
    // KillGraphRow
    // : 킬 그래프의 줄 하나 입니다. 공격 하나의 킬 수를 오른쪽 끝에 붙어 왼쪽으로 자라는 막대로 보여줍니다.
    // - KillGraphView가 줄을 미리 만들어 두고 켜고 끄며 다시 씁니다.
    // - 막대 길이, 떨림, 불꽃, 나타남/사라짐은 이 줄이 직접 처리합니다.
    // - 몇 번째 줄에 놓일지(위치)는 KillGraphView가 정해줍니다.
    public class KillGraphRow : MonoBehaviour
    {
        public enum RowState
        {
            Empty,     // 쓰지 않는 줄 (꺼져 있음)
            Active,    // 공격이 진행 중. 킬이 날 때마다 막대가 자랍니다.
            Confirmed, // 공격이 끝나 킬 수가 확정됨. 떨린 뒤 잠시 보여줍니다.
            Hiding,    // 사라지는 중. 다른 공격이 이 줄을 바로 가져갈 수 있습니다.
        }

        [SerializeField] private RectTransform rowRect;   // 줄 전체. 위아래 위치를 옮깁니다.
        [SerializeField] private RectTransform shakeRect; // 떨리는 부분 (막대 + 숫자)
        [SerializeField] private RectTransform barRect;   // 오른쪽 끝에 붙어서 왼쪽으로 늘어나는 막대
        [SerializeField] private Image barImage;
        [SerializeField] private TMP_Text killText;       // 막대 왼쪽 끝을 따라다니는 킬 수
        [SerializeField] private CanvasGroup canvasGroup; // 나타남/사라짐 투명도

        private KillGraphSettings settings;
        private float noiseSeed; // 줄마다 불꽃이 다르게 일렁이도록 하는 값

        private RowState state = RowState.Empty;
        private float stateTime; // 지금 상태가 된 뒤 지난 시간
        private int attackId;
        private int killCount;

        private float currentWidth;
        private float currentY;
        private float targetY;
        private bool needsSnap; // 처음 나타날 때는 미끄러지지 않고 바로 제자리에 놓습니다.

        public RowState State => state;
        public int AttackId => attackId;
        public int KillCount => killCount;

        // 화면에 자리를 차지하고 있는 줄인지 (진행 중이거나 확정되어 보여주는 중)
        public bool IsVisible
        {
            get { return state == RowState.Active || state == RowState.Confirmed; }
        }

#if UNITY_EDITOR
        // KillGraphBuilder가 조립 직후 호출합니다.
        public void EditorSetup(RectTransform row, RectTransform shake, RectTransform bar, Image image, TMP_Text text, CanvasGroup group)
        {
            rowRect = row;
            shakeRect = shake;
            barRect = bar;
            barImage = image;
            killText = text;
            canvasGroup = group;
        }
#endif

        // KillGraphView가 시작할 때 한 번 호출합니다.
        public void Init(KillGraphSettings graphSettings, int rowIndex)
        {
            settings = graphSettings;
            noiseSeed = rowIndex * 13.7f;
            Clear();
        }

        // 공격 하나를 이 줄에 보여주기 시작합니다.
        public void Show(int newAttackId, int newKillCount)
        {
            attackId = newAttackId;
            killCount = newKillCount;
            ChangeState(RowState.Active);

            currentWidth = settings.minBarWidth;
            shakeRect.anchoredPosition = Vector2.zero;
            canvasGroup.alpha = 0f;
            needsSnap = true;

            UpdateKillText();
            gameObject.SetActive(true);
        }

        public void SetKillCount(int newKillCount)
        {
            killCount = newKillCount;
            UpdateKillText();
        }

        // 공격이 끝났습니다. 떨림 → 잠시 보여줌 → 사라짐 순서로 진행합니다.
        public void Confirm()
        {
            if (state != RowState.Active)
                return;

            ChangeState(RowState.Confirmed);
        }

        // 몇 번째 줄에 놓일지 정합니다. 처음 나타난 줄은 바로 그 자리에 놓습니다.
        public void SetTargetY(float y)
        {
            targetY = y;
            if (needsSnap)
            {
                currentY = y;
                needsSnap = false;
            }
        }

        public void Clear()
        {
            ChangeState(RowState.Empty);
            attackId = 0;
            killCount = 0;
            gameObject.SetActive(false);
        }

        // 한 프레임 처리. KillGraphView가 호출합니다.
        public void Tick(float deltaTime)
        {
            if (state == RowState.Empty)
                return;

            stateTime += deltaTime;
            UpdateState();
            if (state == RowState.Empty)
                return;

            UpdateWidth(deltaTime);
            UpdatePosition(deltaTime);
            UpdateShake();
            UpdateBurn();
            UpdateAlpha();
        }

        private void ChangeState(RowState newState)
        {
            state = newState;
            stateTime = 0f;
        }

        // 시간이 다 되면 다음 상태로 넘어갑니다. (확정 → 사라지는 중 → 비어 있음)
        private void UpdateState()
        {
            if (state == RowState.Confirmed && stateTime >= settings.holdTime)
            {
                ChangeState(RowState.Hiding);
                return;
            }

            if (state == RowState.Hiding && stateTime >= settings.fadeOutTime)
                Clear();
        }

        // 막대 길이: 킬 수가 많을수록 덜 자라고(제곱근), killsForFullBar에서 최대 길이가 됩니다.
        private void UpdateWidth(float deltaTime)
        {
            float ratio = Mathf.Clamp01((float)killCount / settings.killsForFullBar);
            float targetWidth = Mathf.Lerp(settings.minBarWidth, settings.maxBarWidth, Mathf.Sqrt(ratio));

            currentWidth = Mathf.Lerp(currentWidth, targetWidth, Smooth(settings.widthSmoothSpeed, deltaTime));
            barRect.sizeDelta = new Vector2(currentWidth, barRect.sizeDelta.y);
        }

        private void UpdatePosition(float deltaTime)
        {
            currentY = Mathf.Lerp(currentY, targetY, Smooth(settings.moveSmoothSpeed, deltaTime));
            rowRect.anchoredPosition = new Vector2(0f, currentY);
        }

        // 확정된 직후 잠깐 떨립니다. 점점 약해집니다.
        private void UpdateShake()
        {
            if (state != RowState.Confirmed || stateTime >= settings.shakeDuration)
            {
                shakeRect.anchoredPosition = Vector2.zero;
                return;
            }

            float strength = 1f - stateTime / settings.shakeDuration;
            Vector2 offset = Random.insideUnitCircle * (settings.shakeAmplitude * strength);
            shakeRect.anchoredPosition = offset;
        }

        // 킬 수가 불꽃 단계를 넘으면 그 단계의 색으로 일렁입니다. 못 넘었으면 기본 색 입니다.
        private void UpdateBurn()
        {
            KillGraphBurnTier tier = settings.FindBurnTier(killCount);
            if (tier == null)
            {
                barImage.color = settings.baseColor;
                return;
            }

            float noise = Mathf.PerlinNoise(Time.time * settings.flickerSpeed, noiseSeed);
            barImage.color = Color.Lerp(tier.color, Color.white, noise * settings.flickerAmount);
        }

        // 나타날 때 서서히 보이고, 사라질 때 서서히 투명해집니다.
        private void UpdateAlpha()
        {
            if (state == RowState.Active)
            {
                canvasGroup.alpha = Mathf.Clamp01(stateTime / settings.fadeInTime);
                return;
            }

            if (state == RowState.Hiding)
            {
                canvasGroup.alpha = 1f - Mathf.Clamp01(stateTime / settings.fadeOutTime);
                return;
            }

            canvasGroup.alpha = 1f;
        }

        // 숫자를 문자열로 만들지 않고 넘겨서, 킬이 날 때마다 문자열이 생기지 않게 합니다.
        private void UpdateKillText()
        {
            killText.SetText("{0}", killCount);
        }

        // 프레임 속도와 상관없이 같은 빠르기로 따라가도록 하는 보간 비율
        private static float Smooth(float speed, float deltaTime)
        {
            return 1f - Mathf.Exp(-speed * deltaTime);
        }
    }
}
