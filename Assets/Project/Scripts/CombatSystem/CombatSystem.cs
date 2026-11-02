using System;
using System.Collections.Generic;
using UnityEngine;

namespace Study3D.FpsGame
{
    public class CombatSystem : MonoBehaviour
    {
        [Header("적 판정")]
        [Tooltip("카드가 맞힐 적의 레이어 이름. Project Settings > Tags and Layers의 이름과 같아야 합니다")]
        [SerializeField] private string enemyLayerName = "Enemy";

        [Header("환경 판정")]
        [Tooltip("맵(벽, 바닥)과 프롭처럼 카드를 막거나 튕겨내는 레이어 이름들 (관통, 반사 판정에 사용)")]
        [SerializeField] private string[] environmentLayerNames = { "World", "Prop" };

        public static CombatSystem Instance { get; private set; }

        public int EnemyMask { get; private set; }
        public int EnvironmentMask { get; private set; }

        public event Action<CombatEvent> OnCombatEvent;
        public event Action<HealEvent> OnHealEvent;
        // 적이 죽으면 그 적을 죽인 공격의 번호를 알립니다. (킬 그래프가 공격마다 킬 수를 셀 때 사용)
        public event Action<int> OnEnemyKilled;

        private readonly Dictionary<Collider, IDamageable> monsterDic = new Dictionary<Collider, IDamageable>();
        private readonly Queue<InGameEvent> inGameEventQueue = new Queue<InGameEvent>();

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Debug.LogWarning($"{name}: CombatSystem이 이미 있습니다. 이 컴포넌트는 쓰지 않습니다");
                enabled = false;
                return;
            }

            Instance = this;
            EnemyMask = LayerMask.GetMask(enemyLayerName);
            if (EnemyMask == 0) Debug.LogWarning($"{enemyLayerName} 레이어가 없어 카드가 아무것도 맞히지 못합니다");
            EnvironmentMask = LayerMask.GetMask(environmentLayerNames);
            // GetMask는 없는 이름을 조용히 무시하므로, 오타나 미등록 레이어를 여기서 알려줍니다.
            foreach (string layerName in environmentLayerNames)
            {
                if (LayerMask.NameToLayer(layerName) < 0)
                    Debug.LogWarning($"{layerName} 레이어가 없어 카드가 해당 사물을 맞히지 못합니다");
            }
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        // 쌓인 전투 이벤트를 매 프레임 전부 처리합니다. (피해가 다음 프레임으로 밀리지 않도록)
        // - 처리 중에 새로 들어온 이벤트는 다음 프레임에 처리합니다.
        // - 피격 연출(피, 데미지 숫자)이 너무 많아지는 것은 여기서 막지 않고 EffectBudget이 막습니다.
        private void Update()
        {
            int count = inGameEventQueue.Count;
            for (int i = 0; i < count; i++)
            {
                var inGameEvent = inGameEventQueue.Dequeue();

                if (inGameEvent.Receiver is UnityEngine.Object receiverObject && 
                    receiverObject == null) continue;

                switch (inGameEvent.Type)
                {
                    case InGameEvent.EventType.Combat:
                        var combatEvent = inGameEvent as CombatEvent;
                        inGameEvent.Receiver.TakeDamage(combatEvent);
                        OnCombatEvent?.Invoke(combatEvent);
                        break;
                    case InGameEvent.EventType.Heal:
                        var healEvent = inGameEvent as HealEvent;
                        inGameEvent.Receiver.TakeHeal(healEvent);
                        OnHealEvent?.Invoke(healEvent);
                        break;
                }
            }
        }

        // 콜라이더의 레이어로 무엇과 부딪혔는지 분류합니다.
        public ContactKind GetContactKind(Collider collider)
        {
            int layerBit = 1 << collider.gameObject.layer;
            if ((EnemyMask & layerBit) != 0) return ContactKind.Enemy;
            if ((EnvironmentMask & layerBit) != 0) return ContactKind.World;
            return ContactKind.Ignore;
        }

        // 판정 방식(Ray, 충돌, 폭발 범위)과 상관없이 "누가, 무엇을, 어디서, 얼마만큼" 맞혔는지만 넘기면
        // CombatEvent를 만들어 큐에 넣습니다. 등록된 몬스터가 아니면 피해를 주지 않고 false를 반환합니다.
        // attackId : 이 피해를 준 공격의 번호 입니다. 이 피해로 적이 죽으면 이 번호의 킬이 됩니다.
        public bool SendDamage(IDamageable sender, Collider target, int damage, Vector3 hitPosition, int attackId)
        {
            IDamageable receiver = GetMonsterOrNull(target);
            if (receiver == null) return false;

            CombatEvent combatEvent = new CombatEvent();
            combatEvent.Sender = sender;
            combatEvent.Receiver = receiver;
            combatEvent.Damage = damage;
            combatEvent.HitPosition = hitPosition;
            combatEvent.Collider = target;
            combatEvent.AttackId = attackId;

            AddInGameEvent(combatEvent);
            return true;
        }

        // 적이 죽는 순간 적이 호출합니다.
        // attackId : 마지막 피해를 준 공격의 번호 입니다.
        public void ReportKill(int attackId)
        {
            if (OnEnemyKilled != null)
                OnEnemyKilled.Invoke(attackId);
        }

        public void RegisterMonster(IDamageable monster)
        {
            RegisterMonster(monster.MainCollider, monster);
        }

        public void RegisterMonster(Collider collider, IDamageable monster)
        {
            if (monsterDic.TryAdd(collider, monster) == false)
            {
                Debug.LogWarning($"{monster.GameObject.name}가 등록되어 있습니다." +
                                 $"{monsterDic[collider]}를 대체합니다");
                monsterDic[collider] = monster;
            }
        }

        public void UnregisterMonster(Collider collider)
        {
            monsterDic.Remove(collider);
        }

        public IDamageable GetMonsterOrNull(Collider collider)
        {
            if (monsterDic.TryGetValue(collider, out var monster)) return monster;
            return null;
        }

        public void AddInGameEvent(InGameEvent inGameEvent)
        {
            inGameEventQueue.Enqueue(inGameEvent);
        }
    }
}
