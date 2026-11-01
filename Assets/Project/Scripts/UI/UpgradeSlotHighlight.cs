using System.Collections.Generic;
using UnityEngine;

namespace Study3D.FpsGame.UI
{
    // UpgradeSlotHighlight
    // : 강화 화면에서 지금 선택된 칸에 파티클을 띄워 어느 칸인지 보여 줍니다. UpgradePanel과 같은 화면(Menu UI)에 붙입니다.
    // - 파티클은 하나만 만들어 두고, 선택된 칸이 바뀌면 그 칸 아래로 옮겨 처음부터 다시 재생합니다.
    // - 칸 하위에 있으므로 화면을 끄면 같이 꺼지고, 다시 켜면 첫 번째 칸에서 시작합니다. (UpgradePanel이 켜질 때 칸을 비우고 0번 칸을 선택)
    // - 파티클 정렬 순서는 강화 화면 캔버스 바로 위(+1, +2...)로 다시 씁니다. 파티클끼리의 원래 앞뒤 관계는 유지합니다.
    // - 강화 화면 캔버스는 Screen Space - Camera(UI 카메라)여야 파티클이 UI와 함께 정렬됩니다. (Overlay에서는 파티클이 UI 뒤로 가려짐)
    public class UpgradeSlotHighlight : MonoBehaviour
    {
        [SerializeField] private UpgradePanel upgradePanel;
        [Tooltip("선택된 칸에 띄울 파티클 프리팹 (FX_CardRimStar_B 등)")]
        [SerializeField] private GameObject highlightPrefab;
        [Tooltip("칸 크기에 맞추는 배율 (선택 칸 카드는 손패 카드보다 큼)")]
        [SerializeField, Min(0f)] private float scale = 1f;

        private Transform instance;
        private ParticleSystem system;

#if UNITY_EDITOR
        // UpgradeUIBuilder가 조립 직후 호출합니다.
        public void EditorSetup(UpgradePanel panel, GameObject prefab, float particleScale)
        {
            upgradePanel = panel;
            highlightPrefab = prefab;
            scale = particleScale;
        }
#endif

        private void Awake()
        {
            if (upgradePanel == null || highlightPrefab == null)
            {
                Debug.LogWarning("[UpgradeSlotHighlight] UpgradePanel이나 파티클 프리팹이 연결되어 있지 않습니다.");
                return;
            }

            // 씬 루트에 잠깐이라도 보이지 않게 처음부터 첫 번째 칸 아래에 만듭니다.
            instance = Instantiate(highlightPrefab, upgradePanel.GetSlotRect(0), false).transform;
            // UI 카메라는 UI 레이어만 그리므로 파티클도 강화 화면과 같은 레이어로 맞춥니다. (프리팹은 Default 레이어)
            foreach (Transform child in instance.GetComponentsInChildren<Transform>(true))
            {
                child.gameObject.layer = upgradePanel.gameObject.layer;
            }
            system = instance.GetComponentInChildren<ParticleSystem>(true);
            ApplySorting(instance.GetComponentsInChildren<ParticleSystemRenderer>(true));

            upgradePanel.OnSelectedSlotChanged += MoveTo;
        }

        // 켜지는 순서에 따라 UpgradePanel의 첫 선택 알림을 놓칠 수 있으므로, 켜질 때 현재 선택된 칸으로 한 번 맞춥니다.
        private void OnEnable()
        {
            if (instance != null)
            {
                MoveTo(upgradePanel.SelectedSlotIndex);
            }
        }

        private void OnDestroy()
        {
            if (upgradePanel != null)
            {
                upgradePanel.OnSelectedSlotChanged -= MoveTo;
            }
        }

        private void MoveTo(int slot)
        {
            if (slot < 0 || slot >= upgradePanel.SlotCount)
                return;

            // 프리팹 루트에 들어 있는 위치값은 쓰지 않고 칸 중앙에 둡니다.
            instance.SetParent(upgradePanel.GetSlotRect(slot), false);
            instance.localPosition = Vector3.zero;
            instance.localScale = Vector3.one * scale;

            if (system == null)
                return;

            system.Clear(true);
            system.Play(true);
        }

        // 파티클들을 원래 정렬 순서대로 줄 세운 뒤, 강화 화면 캔버스 순서 +1부터 차례로 다시 매깁니다. (같은 순서끼리는 같은 값)
        private void ApplySorting(ParticleSystemRenderer[] renderers)
        {
            Canvas canvas = upgradePanel.GetComponentInParent<Canvas>(true).rootCanvas;

            List<int> orders = new List<int>();
            foreach (ParticleSystemRenderer particleRenderer in renderers)
            {
                if (!orders.Contains(particleRenderer.sortingOrder))
                {
                    orders.Add(particleRenderer.sortingOrder);
                }
            }
            orders.Sort();

            foreach (ParticleSystemRenderer particleRenderer in renderers)
            {
                int rank = orders.IndexOf(particleRenderer.sortingOrder);
                particleRenderer.sortingLayerID = canvas.sortingLayerID;
                particleRenderer.sortingOrder = canvas.sortingOrder + 1 + rank;
            }
        }
    }
}
