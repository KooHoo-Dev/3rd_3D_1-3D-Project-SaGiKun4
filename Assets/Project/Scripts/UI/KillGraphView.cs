using System;
using System.Collections.Generic;
using Study3D.FpsGame.Weapon.Cards;
using UnityEngine;

namespace Study3D.FpsGame.UI
{
    // 불꽃 단계 하나. 킬 수가 killCount 이상이면 막대가 이 색으로 일렁입니다.
    [Serializable]
    public class KillGraphBurnTier
    {
        public int killCount;
        public Color color;

        public KillGraphBurnTier(int killCount, Color color)
        {
            this.killCount = killCount;
            this.color = color;
        }
    }

    // 킬 그래프의 배치, 연출 값 입니다. KillGraphView 인스펙터에서 조절합니다.
    [Serializable]
    public class KillGraphSettings
    {
        [Header("배치")]
        public float rowHeight = 34f;
        public float rowSpacing = 6f;

        [Header("막대 길이")]
        public float minBarWidth = 12f;  // 0킬일 때 길이 (줄이 생긴 게 보이도록)
        public float maxBarWidth = 640f; // 최대 길이
        [Tooltip("이 킬 수에서 최대 길이가 됩니다. 그 아래에서는 킬 수가 많을수록 덜 자랍니다 (제곱근)")]
        public int killsForFullBar = 60;

        [Header("움직임")]
        public float widthSmoothSpeed = 12f; // 막대 길이가 따라가는 빠르기
        public float moveSmoothSpeed = 12f;  // 줄 순서가 바뀔 때 미끄러지는 빠르기

        [Header("확정 / 사라짐")]
        public float shakeDuration = 0.35f;
        public float shakeAmplitude = 8f;
        [Tooltip("확정된 뒤 사라지기 시작할 때까지 보여주는 시간 (떨림 포함)")]
        public float holdTime = 1.5f;
        public float fadeInTime = 0.15f;
        public float fadeOutTime = 0.3f;

        [Header("불꽃")]
        public Color baseColor = new Color(0.85f, 0.85f, 0.85f);
        [Tooltip("킬 수가 작은 단계부터 순서대로 넣습니다")]
        public List<KillGraphBurnTier> burnTiers = new List<KillGraphBurnTier>()
        {
            new KillGraphBurnTier(10, new Color(1f, 0.6f, 0.15f)),
            new KillGraphBurnTier(25, new Color(1f, 0.25f, 0.1f)),
            new KillGraphBurnTier(50, new Color(0.35f, 0.6f, 1f)),
        };
        public float flickerSpeed = 8f;    // 일렁이는 빠르기
        [Range(0f, 1f)]
        public float flickerAmount = 0.45f; // 일렁일 때 흰색으로 밝아지는 정도

        // killCount가 넘은 가장 높은 단계. 하나도 못 넘었으면 null
        public KillGraphBurnTier FindBurnTier(int killCount)
        {
            KillGraphBurnTier found = null;
            foreach (KillGraphBurnTier tier in burnTiers)
            {
                if (killCount >= tier.killCount)
                    found = tier;
            }
            return found;
        }
    }

    // KillGraphView
    // : 공격마다 킬 수를 막대로 보여주는 그래프 입니다. 화면 오른쪽 위에 둡니다.
    // - AttackTracker의 이벤트를 받아서 줄을 만들고, 킬 수를 바꾸고, 확정합니다.
    // - 맨 위 줄은 화면에 있는 줄 중 킬 수가 가장 많은 공격입니다. 나머지는 공격 순서대로(새 공격이 아래) 놓입니다.
    // - 줄이 모자라면 다음 순서로 하나를 비웁니다.
    //   1. 비어 있거나 사라지는 중인 줄
    //   2. 확정된 줄 중 가장 오래된 줄 (맨 위 줄도 포함)
    //   3. 진행 중인 줄 중 맨 위 줄을 뺀 가장 오래된 줄
    // - 밀려난 진행 중 공격도 킬은 계속 들어옵니다. 맨 위 줄보다 많아지면 다시 나타나서 맨 위에 놓입니다.
    public class KillGraphView : MonoBehaviour
    {
        [SerializeField] private List<KillGraphRow> rows = new List<KillGraphRow>();
        [SerializeField] private KillGraphSettings settings = new KillGraphSettings();

        private readonly List<KillGraphRow> orderedRows = new List<KillGraphRow>(); // 화면 위에서부터의 순서 (매 프레임 다시 채움)
        private KillGraphRow topRow; // 지금 맨 위 줄. 킬 수가 같으면 바꾸지 않아서 줄이 깜빡이며 바뀌지 않습니다.

        public KillGraphSettings Settings => settings;

#if UNITY_EDITOR
        // KillGraphBuilder가 조립 직후 호출합니다.
        public void EditorSetup(List<KillGraphRow> builtRows)
        {
            rows = builtRows;
        }
#endif

        private void Awake()
        {
            for (int i = 0; i < rows.Count; i++)
                rows[i].Init(settings, i);
        }

        private void OnEnable()
        {
            AttackTracker.OnAttackStarted += HandleAttackStarted;
            AttackTracker.OnKillCountChanged += HandleKillCountChanged;
            AttackTracker.OnAttackConfirmed += HandleAttackConfirmed;
        }

        // 이벤트가 static이라서, 꺼질 때 꼭 빼야 씬이 바뀐 뒤 사라진 그래프가 호출되지 않습니다.
        private void OnDisable()
        {
            AttackTracker.OnAttackStarted -= HandleAttackStarted;
            AttackTracker.OnKillCountChanged -= HandleKillCountChanged;
            AttackTracker.OnAttackConfirmed -= HandleAttackConfirmed;
        }

        private void Update()
        {
            float deltaTime = Time.deltaTime;
            foreach (KillGraphRow row in rows)
                row.Tick(deltaTime);

            UpdateLayout();
        }

        #region AttackTracker 이벤트

        private void HandleAttackStarted(int attackId)
        {
            ShowAttack(attackId, 0);
        }

        private void HandleKillCountChanged(int attackId, int killCount)
        {
            KillGraphRow row = FindRow(attackId);
            if (row != null)
            {
                row.SetKillCount(killCount);
                return;
            }

            // 화면에서 밀려난 진행 중 공격 입니다. 맨 위 줄보다 많아졌을 때만 다시 보여줍니다.
            KillGraphRow top = FindTopRow();
            if (top != null && killCount <= top.KillCount)
                return;

            ShowAttack(attackId, killCount);
        }

        // 밀려나서 화면에 없는 공격이면 아무것도 하지 않습니다.
        private void HandleAttackConfirmed(int attackId, int killCount)
        {
            KillGraphRow row = FindRow(attackId);
            if (row == null)
                return;

            row.SetKillCount(killCount);
            row.Confirm();
        }

        #endregion

        #region 줄 고르기

        private void ShowAttack(int attackId, int killCount)
        {
            KillGraphRow row = TakeRow();
            if (row == null)
                return;

            row.Show(attackId, killCount);
        }

        // 새 공격을 보여줄 줄을 하나 고릅니다. (고르는 순서는 클래스 설명 참고)
        private KillGraphRow TakeRow()
        {
            KillGraphRow freeRow = FindFreeRow();
            if (freeRow != null)
                return freeRow;

            KillGraphRow oldestConfirmed = FindOldestRow(KillGraphRow.RowState.Confirmed, null);
            if (oldestConfirmed != null)
                return oldestConfirmed;

            return FindOldestRow(KillGraphRow.RowState.Active, FindTopRow());
        }

        // 비어 있거나 사라지는 중인 줄
        private KillGraphRow FindFreeRow()
        {
            foreach (KillGraphRow row in rows)
            {
                if (row.IsVisible == false)
                    return row;
            }
            return null;
        }

        // state 상태인 줄 중 가장 오래된(공격 번호가 가장 작은) 줄. except 줄은 고르지 않습니다.
        private KillGraphRow FindOldestRow(KillGraphRow.RowState state, KillGraphRow except)
        {
            KillGraphRow oldest = null;
            foreach (KillGraphRow row in rows)
            {
                if (row.State != state || row == except)
                    continue;

                if (oldest == null || row.AttackId < oldest.AttackId)
                    oldest = row;
            }
            return oldest;
        }

        private KillGraphRow FindRow(int attackId)
        {
            foreach (KillGraphRow row in rows)
            {
                if (row.IsVisible && row.AttackId == attackId)
                    return row;
            }
            return null;
        }

        // 화면에 있는 줄 중 킬 수가 가장 많은 줄. 지금 맨 위 줄과 같으면 맨 위 줄을 그대로 둡니다.
        private KillGraphRow FindTopRow()
        {
            KillGraphRow best = null;
            if (topRow != null && topRow.IsVisible)
                best = topRow;

            foreach (KillGraphRow row in rows)
            {
                if (row.IsVisible == false)
                    continue;

                if (best == null || row.KillCount > best.KillCount)
                    best = row;
            }

            topRow = best;
            return best;
        }

        #endregion

        #region 배치

        // 맨 위 줄을 먼저 놓고, 나머지는 공격 순서대로 아래에 놓습니다.
        private void UpdateLayout()
        {
            orderedRows.Clear();
            KillGraphRow top = FindTopRow();
            if (top != null)
                orderedRows.Add(top);

            foreach (KillGraphRow row in rows)
            {
                if (row.IsVisible && row != top)
                    InsertByAttackOrder(row);
            }

            float step = settings.rowHeight + settings.rowSpacing;
            for (int i = 0; i < orderedRows.Count; i++)
                orderedRows[i].SetTargetY(-i * step);
        }

        // 맨 위 줄(0번) 아래에서, 공격 번호가 작은(오래된) 줄이 위로 오도록 끼워 넣습니다.
        // (보이는 줄이 있으면 맨 위 줄도 반드시 있으므로 0번은 항상 맨 위 줄 입니다)
        private void InsertByAttackOrder(KillGraphRow row)
        {
            int index = orderedRows.Count;
            while (index > 1 && orderedRows[index - 1].AttackId > row.AttackId)
                index--;

            orderedRows.Insert(index, row);
        }

        #endregion
    }
}
