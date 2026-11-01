using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace Study3D.FpsGame.UI
{
    // CardAppearEffect
    // : 다 타서 없어진 카드가 복구되듯 나타나는 등장 연출입니다. 카드 UI 루트(Card_XX)에 붙입니다.
    // - 연출 중에는 카드 루트 Image에 Dissolve 머티리얼과 Mask를 걸어, 하위 UI(본문·글자 등)까지 같은 모양으로 잘라냅니다.
    // - 카드 맨 위에 경계 오버레이(AppearEdge)를 두고, 타는 경계만 그립니다.
    // - 드러난 정도는 두 Image의 color.a로 넘깁니다. (Project/UI/CardDissolve 셰이더가 알파를 Dissolve 양으로 씁니다)
    // - 다 드러나면 카드를 원래 상태(원래 머티리얼, Mask 끔)로 돌리고 플래시 파티클을 재생합니다.
    // - 사라질 때는 같은 노이즈로 거꾸로 타들어가며 없어집니다. (PlayVanish, 다 탄 뒤 카드를 끄는 건 호출한 쪽이 합니다)
    // - 연출 중에 카드가 꺼지면 바로 원래 상태로 돌립니다.
    [RequireComponent(typeof(Image))]
    public class CardAppearEffect : MonoBehaviour
    {
        private const string EdgeName = "AppearEdge";

        [Header("머티리얼 (Project/UI/CardDissolve, 비우면 연출 없이 바로 보임)")]
        [Tooltip("카드 루트 Image에 거는 머티리얼 (Edge Only = 0)")]
        [SerializeField] private Material cardMaterial;
        [Tooltip("경계 오버레이에 거는 머티리얼 (Edge Only = 1, 비우면 경계 없이 잘리기만 함)")]
        [SerializeField] private Material edgeMaterial;

        [Header("연출")]
        [Tooltip("다 탄 상태에서 다 드러날 때까지 걸리는 시간")]
        [SerializeField, Min(0.01f)] private float revealTime = 0.85f;
        [Tooltip("시간(0~1)에 따른 드러난 정도 (0 = 다 탐, 1 = 다 드러남)")]
        [SerializeField] private AnimationCurve revealCurve = AnimationCurve.Linear(0f, 0f, 1f, 1f);
        [Tooltip("다 드러난 순간 재생할 플래시 파티클 (Card_Appear, 비우면 생략)")]
        [SerializeField] private ParticleSystem flash;
        [Tooltip("사라질 때 다 드러난 상태에서 다 탈 때까지 걸리는 시간 (드러남 곡선을 거꾸로 씁니다)")]
        [SerializeField, Min(0.01f)] private float vanishTime = 0.6f;

        private Image image;
        private Mask mask;
        private Image edge;
        private Coroutine routine;
        // 연출 전 카드 루트 Image 상태 (끝나면 되돌림)
        private Material baseMaterial;
        private Color baseColor;

        public ParticleSystem Flash => flash;
        // 노이즈로 타서 사라지는 연출을 쓸 수 있는지 (머티리얼이 없으면 호출한 쪽이 다른 방법으로 사라지게 합니다)
        public bool CanVanish => cardMaterial != null;
        // 사라짐 연출이 진행 중인지. 끝나면 카드는 다 탄(안 보이는) 상태로 남습니다.
        public bool IsVanishing { get; private set; }

        // UIBuilder가 조립할 때 값을 채웁니다.
        public void Setup(Material card, Material edgeOverlay, ParticleSystem flashEffect)
        {
            cardMaterial = card;
            edgeMaterial = edgeOverlay;
            flash = flashEffect;
        }

        // 카드를 켠 뒤 호출합니다. 재생 중이면 처음부터 다시 재생합니다.
        public void Play()
        {
            if (isActiveAndEnabled == false || cardMaterial == null)
                return;

            StartRoutine(AppearRoutine());
        }

        // 카드가 켜진 상태에서 호출합니다. 등장 연출 중이었으면 그 자리에서 사라짐으로 바꿉니다.
        // IsVanishing이 false가 되면 다 탄 것이니, 그때 카드를 끄면 원래 상태로 돌아갑니다.
        public void PlayVanish()
        {
            if (isActiveAndEnabled == false || cardMaterial == null)
                return;

            StartRoutine(VanishRoutine());
        }

        private void StartRoutine(IEnumerator next)
        {
            EnsureParts();
            if (routine != null)
            {
                StopCoroutine(routine);
                EndDissolve();
            }
            IsVanishing = false;
            routine = StartCoroutine(next);
        }

        private void OnDisable()
        {
            IsVanishing = false;
            // 꺼지면 코루틴은 알아서 멈추므로 상태만 되돌립니다.
            if (routine == null)
                return;

            routine = null;
            EndDissolve();
        }

        private IEnumerator AppearRoutine()
        {
            BeginDissolve();
            for (float t = 0f; t < revealTime; t += Time.deltaTime)
            {
                SetReveal(revealCurve.Evaluate(t / revealTime));
                yield return null;
            }

            EndDissolve();
            routine = null;

            if (flash != null)
            {
                flash.gameObject.SetActive(true);
                flash.Play(true);
            }
        }

        // 다 드러난 상태에서 시작해, 드러남 곡선을 거꾸로 따라 다 탄 상태까지 갑니다.
        // 다 타도 원래 상태로 돌리지 않습니다. (돌리면 카드가 한 번 번쩍 보이므로, 카드를 끌 때 OnDisable이 돌립니다)
        private IEnumerator VanishRoutine()
        {
            IsVanishing = true;
            BeginDissolve(revealCurve.Evaluate(1f));
            for (float t = 0f; t < vanishTime; t += Time.deltaTime)
            {
                SetReveal(revealCurve.Evaluate(1f - t / vanishTime));
                yield return null;
            }

            SetReveal(revealCurve.Evaluate(0f));
            IsVanishing = false;
        }

        // 첫 프레임부터 startReveal 상태로 시작합니다. (등장은 다 탄 상태 → 카드가 한 번 번쩍 보이지 않게)
        private void BeginDissolve()
        {
            BeginDissolve(revealCurve.Evaluate(0f));
        }

        private void BeginDissolve(float startReveal)
        {
            baseMaterial = image.material;
            baseColor = image.color;
            image.material = cardMaterial;
            mask.enabled = true;

            if (edge != null)
            {
                // 카드 하위 UI보다 위에 그려지도록 맨 뒤로 보냅니다.
                edge.transform.SetAsLastSibling();
                edge.gameObject.SetActive(true);
            }
            SetReveal(startReveal);
        }

        private void EndDissolve()
        {
            mask.enabled = false;
            image.material = baseMaterial;
            image.color = baseColor;
            if (edge != null)
            {
                edge.gameObject.SetActive(false);
            }
        }

        private void SetReveal(float reveal)
        {
            reveal = Mathf.Clamp01(reveal);
            image.color = new Color(baseColor.r, baseColor.g, baseColor.b, reveal);
            if (edge != null)
            {
                edge.color = new Color(1f, 1f, 1f, reveal);
            }
        }

        // Mask와 경계 오버레이는 처음 재생할 때 만듭니다. (평소에는 꺼 두므로 카드 모습은 그대로)
        private void EnsureParts()
        {
            if (image != null)
                return;

            image = GetComponent<Image>();

            if (TryGetComponent(out mask) == false)
            {
                mask = gameObject.AddComponent<Mask>();
            }
            mask.showMaskGraphic = true;
            mask.enabled = false;

            if (edgeMaterial == null)
                return;

            edge = CreateEdgeOverlay();
        }

        // 카드를 꽉 채우는 경계 오버레이 Image (꺼 둔 채로 만듭니다)
        private Image CreateEdgeOverlay()
        {
            GameObject go = new GameObject(EdgeName, typeof(RectTransform));
            go.layer = gameObject.layer;
            go.transform.SetParent(transform, false);

            RectTransform rect = (RectTransform)go.transform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            Image overlay = go.AddComponent<Image>();
            overlay.material = edgeMaterial;
            overlay.raycastTarget = false;
            go.SetActive(false);
            return overlay;
        }
    }
}
