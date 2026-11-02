using System;
using System.Collections.Generic;
using UnityEngine;

namespace Study3D.FpsGame.Weapon.Cards
{
    // AttackTracker
    // : 공격(발사 한 번)마다 킬 수를 세고, 공격이 끝났는지 판정합니다. (킬 그래프가 이 값을 받아서 그립니다)
    // - 공격이 끝났다 = 그 공격의 발사체(분열된 자식 포함)가 모두 사라졌다
    // - 발사체가 생길 때 살아 있는 수를 +1, 매니저에서 빠질 때 -1 합니다.
    // - 0이 돼도 바로 확정하지 않고, 다음 프레임 LateUpdate에서 확정합니다.
    //   (마지막 발사체가 소멸하며 터뜨린 폭발의 피해가 아직 CombatSystem 큐에 남아 있기 때문입니다)
    // - 확정되면 알린 뒤 기록을 지웁니다. 확정 뒤에는 킬이 더 들어오지 않으므로 화면 쪽은 받은 값을 그대로 쓰면 됩니다.
    // - 씬에 따로 둘 필요 없이, 처음 공격이 시작될 때 스스로 만들어집니다. (씬이 바뀌면 씬과 함께 사라짐)
    public class AttackTracker : MonoBehaviour
    {
        // 공격 하나의 기록 입니다.
        private class AttackRecord
        {
            public int KillCount;
            public int AliveCount;      // 지금 날고 있는 이 공격의 발사체 수
            public int ZeroFrame = -1;  // 살아 있는 수가 0이 된 프레임. 아직 날고 있으면 -1
        }

        private static AttackTracker instance;

        // 공격이 시작될 때 (공격 번호)
        public static event Action<int> OnAttackStarted;
        // 공격의 킬 수가 바뀔 때 (공격 번호, 지금까지의 킬 수)
        public static event Action<int, int> OnKillCountChanged;
        // 공격이 끝나서 킬 수가 확정될 때 (공격 번호, 최종 킬 수)
        public static event Action<int, int> OnAttackConfirmed;

        private readonly Dictionary<int, AttackRecord> records = new Dictionary<int, AttackRecord>();
        private readonly List<int> waitingIds = new List<int>(); // 살아 있는 수가 0이 되어 확정을 기다리는 공격 번호

        // 발사체가 생길 때 Projectile이 호출합니다. (발사, 분열)
        // 처음 보는 공격 번호면 새 기록을 만듭니다.
        public static void AddProjectile(int attackId)
        {
            if (instance == null)
                instance = new GameObject(nameof(AttackTracker)).AddComponent<AttackTracker>();

            AttackRecord record;
            if (instance.records.TryGetValue(attackId, out record) == false)
            {
                record = new AttackRecord();
                instance.records.Add(attackId, record);
                if (OnAttackStarted != null)
                    OnAttackStarted.Invoke(attackId);
            }

            record.AliveCount++;
        }

        // 발사체가 매니저에서 실제로 빠질 때 ProjectileManager가 호출합니다.
        // (소멸, 맵 밖 회수, 강제로 꺼짐 모두 여기를 한 번만 지나갑니다)
        public static void RemoveProjectile(int attackId)
        {
            if (instance == null)
                return;

            AttackRecord record;
            if (instance.records.TryGetValue(attackId, out record) == false)
                return;

            record.AliveCount--;
            if (record.AliveCount > 0)
                return;

            // 이 공격의 발사체가 모두 사라졌습니다. 이번 프레임 번호를 적어두고 다음 프레임에 확정합니다.
            record.ZeroFrame = Time.frameCount;
            instance.waitingIds.Add(attackId);
        }

        private void Awake()
        {
            if (CombatSystem.Instance != null)
                CombatSystem.Instance.OnEnemyKilled += OnEnemyKilled;
        }

        private void OnDestroy()
        {
            if (CombatSystem.Instance != null)
                CombatSystem.Instance.OnEnemyKilled -= OnEnemyKilled;

            records.Clear();
            waitingIds.Clear();
            if (instance == this)
                instance = null;
        }

        // 적이 죽으면 그 공격의 킬 수를 1 올립니다.
        // 기록이 없는 공격 번호(적의 무기처럼 0이거나, 이미 확정된 공격)는 무시합니다.
        private void OnEnemyKilled(int attackId)
        {
            AttackRecord record;
            if (records.TryGetValue(attackId, out record) == false)
                return;

            record.KillCount++;
            if (OnKillCountChanged != null)
                OnKillCountChanged.Invoke(attackId, record.KillCount);
        }

        // 모든 Update가 끝난 뒤 실행되므로, 이 시점에는 CombatSystem 큐가 처리된 상태 입니다.
        // 0이 된 프레임보다 뒤 프레임이면 남은 피해까지 모두 처리된 것이므로 확정합니다.
        private void LateUpdate()
        {
            int currentFrame = Time.frameCount;

            // 뒤에서부터 확인합니다. 확정한 번호를 목록에서 빼도 아직 확인하지 않은 앞쪽 번호가 밀리지 않습니다.
            for (int i = waitingIds.Count - 1; i >= 0; i--)
            {
                int attackId = waitingIds[i];
                AttackRecord record = records[attackId];
                if (currentFrame <= record.ZeroFrame)
                    continue;

                waitingIds.RemoveAt(i);
                Confirm(attackId, record);
            }
        }

        // 공격을 확정합니다. 최종 킬 수를 알리고 기록을 지웁니다.
        private void Confirm(int attackId, AttackRecord record)
        {
            records.Remove(attackId);
            if (OnAttackConfirmed != null)
                OnAttackConfirmed.Invoke(attackId, record.KillCount);
        }
    }
}
