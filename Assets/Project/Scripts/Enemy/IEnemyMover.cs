using UnityEngine;

namespace Study3D.FpsGame.Enemy
{
    // IEnemyMover
    // : 적의 추격 이동을 외부 길찾기에 맡길 때 구현합니다. (예: NavTest)
    // - Enemy.SetMover 로 연결하면 추격 상태에서 플레이어로 직진하는 대신 이 이동을 사용합니다.
    // - 연결하지 않으면 기존처럼 플레이어로 직진합니다.
    public interface IEnemyMover
    {
        // speed 로 deltaTime 만큼 이동하고, 몸이 바라볼 수평 방향을 돌려줍니다. (이동하지 않았으면 Vector3.zero)
        Vector3 Move(float deltaTime, float speed);
    }
}
