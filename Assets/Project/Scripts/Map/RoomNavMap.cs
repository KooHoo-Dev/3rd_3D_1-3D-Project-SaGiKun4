using UnityEngine;

namespace Study3D.FpsGame.Map
{
    // RoomNavMap
    // : RoomNavRegions 가 만든 방 하나의 지도(영역, 연결점, 남은 거리 표)를 저장하는 에셋 입니다.
    // - RoomNavRegions 의 "지도 저장" 으로 만들어지고, 같은 프리팹으로 만든 방들이 함께 씁니다.
    // - 좌표는 모두 방 루트 로컬 기준이라 방이 어디에 어떤 방향으로 놓이든 그대로 씁니다.
    // - Unity 는 "목록의 목록" 을 저장하지 못해서, 영역별 목록은 한 줄로 펴서 두 배열로 저장합니다.
    //   예: 영역 A = [0, 7, 6], B = [1, 8] → regionVerts = [0, 7, 6, 1, 8], regionStarts = [0, 3]
    //   (영역 r 의 목록 = regionVerts[regionStarts[r]] 부터 다음 영역 시작 전까지)
    public class RoomNavMap : ScriptableObject
    {
        public int triangleCount;

        public Vector3[] points;

        public int[] regionStarts;
        public int[] regionVerts;

        public RoomNavRegions.Portal[] portals;

        // 영역마다 "내 영역에서 나가는" 연결점 번호 목록 (펴는 방식은 regionVerts 와 같음)
        public int[] portalStarts;
        public int[] regionPortalList;

        // 남은 거리 표: portalDist[연결점 번호 * 영역 수 + 목표 영역] (갈 수 없으면 float.MaxValue)
        public float[] portalDist;

        public int RegionCount
        {
            get
            {
                if (regionStarts == null)
                    return 0;

                return regionStarts.Length;
            }
        }
    }
}
