using UnityEngine;

namespace Study3D.FpsGame.Data
{
    // DataManager
    // : 데이터 표들이 들어가는 단일 창구입니다. 종류별 테이블을 "보유"하고 로딩을 맡습니다. (실제 로드/해석/보관/조회는 각 테이블)
    // - 새 데이터 종류를 추가하려면 Table 클래스를 만들고 여기에 프로퍼티와 Load 한 줄을 추가합니다.
    // - 씬에 두지 않는 일반 클래스입니다. 처음 Instance에 접근할 때 로드합니다.
    //   (덱은 LocalPlayer.Awake에서 만들어지므로, 컴포넌트 실행 순서와 상관없이 그 전에 표가 준비되도록)
    public class DataManager
    {
        private static DataManager instance;

        public static DataManager Instance
        {
            get
            {
                if (instance == null)
                {
                    instance = new DataManager();
                    instance.Load();
                }
                return instance;
            }
        }

        // 표를 다시 읽을 때마다 1씩 오릅니다. 표 값을 캐시해 둔 쪽은 이 값이 바뀌면 다시 읽습니다.
        public static int LoadVersion { get; private set; }

        public CardTable Cards { get; } = new CardTable();

        private void Load()
        {
            Cards.Load();
            LoadVersion++;
            Debug.Log($"[DataManager] 카드 {Cards.Count}장을 불러왔습니다.");
        }

        // 플레이를 시작할 때마다 표를 새로 읽도록 비웁니다. (도메인 리로드를 꺼 둔 경우에도 표 수정이 반영되게)
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOnPlay()
        {
            instance = null;
        }
    }
}
