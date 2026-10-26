using System.Collections.Generic;
using UnityEngine;

namespace Study3D.FpsGame
{
    // GizmoDrawer
    // : 어디서든 "이 위치에 이 반경을 몇 초 동안 보여줘"를 부를 수 있는 디버그용 기즈모 입니다. (폭발 범위 확인 등)
    // - ScriptableObject(카드)는 OnDrawGizmos를 받을 수 없어서, 처음 호출될 때 숨은 오브젝트를 하나 만들어 대신 그립니다.
    // - Scene 뷰에 그려지고, Game 뷰에서는 상단의 Gizmos 버튼을 켜야 보입니다.
    // - 에디터에서만 동작합니다. 빌드에서는 아무것도 하지 않습니다.
    public class GizmoDrawer : MonoBehaviour
    {
        private struct Sphere
        {
            public Vector3 Center;
            public float Radius;
            public Color Color;
            public float EndTime;
        }

        private static GizmoDrawer instance;
        private readonly List<Sphere> spheres = new List<Sphere>();

        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        public static void WireSphere(Vector3 center, float radius, Color color, float duration = 2f)
        {
            if (instance == null)
            {
                // 씬에 일반 오브젝트로 만들어서, 플레이를 끄거나 씬이 바뀌면 함께 사라지게 합니다.
                instance = new GameObject(nameof(GizmoDrawer)).AddComponent<GizmoDrawer>();
            }

            instance.spheres.Add(new Sphere { Center = center, Radius = radius, Color = color, EndTime = Time.time + duration });
        }

        private void OnDrawGizmos()
        {
            spheres.RemoveAll(sphere => sphere.EndTime < Time.time);
            foreach (Sphere sphere in spheres)
            {
                Gizmos.color = sphere.Color;
                Gizmos.DrawWireSphere(sphere.Center, sphere.Radius);
            }
        }
    }
}
