using System;
using System.Collections.Generic;
using System.Globalization;
using Study3D.FpsGame.Deck;
using Study3D.FpsGame.Weapon.Cards;
using UnityEngine;

namespace Study3D.FpsGame.Data
{
    // CardTable
    // : 카드 데이터의 로드 / 해석 / 보관 / 조회를 맡습니다.
    // - cards.tsv : 카드 한 장당 한 줄 (동작 종류, 이름, 기본 시점, 막힌 시점, 반응 대상, 발동 이펙트)
    // - card_specs.tsv : 카드 ID + 레벨당 한 줄 (스펙 수치). 카드의 줄 수 = 최대 스펙 레벨
    public class CardTable
    {
        private const string CardTableName = "Table/Cards/cards.tsv";
        private const string SpecTableName = "Table/Cards/card_specs.tsv";

        private readonly Dictionary<int, CardDef> defs = new Dictionary<int, CardDef>();
        // 카드 ID → 레벨 순서대로 정렬한 스펙 (0번 = 1레벨)
        private readonly Dictionary<int, List<CardSpecDef>> specs = new Dictionary<int, List<CardSpecDef>>();

        public int Count => defs.Count;

        // 없는 ID면 null
        public CardDef Get(int id)
        {
            CardDef def;
            if (defs.TryGetValue(id, out def))
                return def;

            return null;
        }

        // 표에 있는 모든 카드 정의
        public IEnumerable<CardDef> GetAll()
        {
            return defs.Values;
        }

        // 카드의 level 단계 스펙. 스펙이 없는 카드면 null, 표를 넘는 레벨이면 마지막 단계
        public CardSpecDef GetSpec(int id, int level)
        {
            List<CardSpecDef> levels;
            if (specs.TryGetValue(id, out levels) == false)
                return null;

            int index = Mathf.Clamp(level - 1, 0, levels.Count - 1);
            return levels[index];
        }

        // 최대 스펙 레벨 (스펙 줄이 없는 카드는 1 = 스펙 강화 불가)
        public int GetMaxLevel(int id)
        {
            List<CardSpecDef> levels;
            if (specs.TryGetValue(id, out levels) == false)
                return 1;

            return levels.Count;
        }

        public void Load()
        {
            LoadCards();
            LoadSpecs();
        }

        #region 카드
        private void LoadCards()
        {
            defs.Clear();
            foreach (string[] cells in TsvReader.ReadRows(CardTableName))
            {
                CardDTO dto = ReadCardDTO(cells);
                if (defs.ContainsKey(dto.ID))
                {
                    Debug.LogWarning($"[CardTable] 카드 ID {dto.ID}가 두 번 있습니다. 뒤의 줄은 무시합니다.");
                    continue;
                }
                defs.Add(dto.ID, ToCardDef(dto));
            }
        }

        // ID	Kind	Name	Timing	BlockedTimings	Target	EffectKey	EffectRadius
        private CardDTO ReadCardDTO(string[] cells)
        {
            CardDTO dto = new CardDTO();
            dto.ID = ParseInt(TsvReader.GetCell(cells, 0));
            dto.Kind = TsvReader.GetCell(cells, 1);
            dto.Name = TsvReader.GetCell(cells, 2);
            dto.Timing = TsvReader.GetCell(cells, 3);
            dto.BlockedTimings = TsvReader.GetCell(cells, 4);
            dto.Target = TsvReader.GetCell(cells, 5);
            dto.EffectKey = TsvReader.GetCell(cells, 6);
            dto.EffectRadius = ParseFloat(TsvReader.GetCell(cells, 7));
            return dto;
        }

        private CardDef ToCardDef(CardDTO dto)
        {
            CardDef def = new CardDef();
            def.ID = dto.ID;
            def.Kind = (CardKind)Enum.Parse(typeof(CardKind), dto.Kind);
            def.Name = dto.Name;
            def.Timing = ParseTiming(dto.Timing);
            foreach (string timing in SplitList(dto.BlockedTimings))
            {
                def.BlockedTimings.Add(ParseTiming(timing));
            }
            def.Target = ParseTarget(dto.Target);
            def.EffectPrefab = LoadEffect(dto.ID, dto.EffectKey);
            def.EffectRadius = dto.EffectRadius;
            return def;
        }

        // 이펙트 키가 있으면 Resources에서 프리팹을 찾습니다. 키가 없으면 null (이펙트 없음)
        private static GameObject LoadEffect(int cardId, string effectKey)
        {
            if (effectKey.Length == 0)
                return null;

            GameObject prefab = Resources.Load<GameObject>(effectKey);
            if (prefab == null)
            {
                Debug.LogWarning($"[CardTable] 카드 {cardId}의 이펙트 Resources/{effectKey} 를 찾지 못했습니다.");
            }
            return prefab;
        }
        #endregion

        #region 스펙
        private void LoadSpecs()
        {
            specs.Clear();
            foreach (string[] cells in TsvReader.ReadRows(SpecTableName))
            {
                CardSpecDTO dto = ReadSpecDTO(cells);
                List<CardSpecDef> levels;
                if (specs.TryGetValue(dto.CardID, out levels) == false)
                {
                    levels = new List<CardSpecDef>();
                    specs.Add(dto.CardID, levels);
                }
                levels.Add(ToSpecDef(dto));
            }

            foreach (KeyValuePair<int, List<CardSpecDef>> pair in specs)
            {
                SortAndCheckLevels(pair.Key, pair.Value);
            }
        }

        // CardID	Level	Count	Radius	Ratio	TurnSpeed	AngleH	AngleV	Interval	LoseRange
        private CardSpecDTO ReadSpecDTO(string[] cells)
        {
            CardSpecDTO dto = new CardSpecDTO();
            dto.CardID = ParseInt(TsvReader.GetCell(cells, 0));
            dto.Level = ParseInt(TsvReader.GetCell(cells, 1));
            dto.Count = ParseInt(TsvReader.GetCell(cells, 2));
            dto.Radius = ParseFloat(TsvReader.GetCell(cells, 3));
            dto.Ratio = ParseFloat(TsvReader.GetCell(cells, 4));
            dto.TurnSpeed = ParseFloat(TsvReader.GetCell(cells, 5));
            dto.AngleH = ParseFloat(TsvReader.GetCell(cells, 6));
            dto.AngleV = ParseFloat(TsvReader.GetCell(cells, 7));
            dto.Interval = ParseInt(TsvReader.GetCell(cells, 8));
            dto.LoseRange = ParseFloat(TsvReader.GetCell(cells, 9));
            return dto;
        }

        private CardSpecDef ToSpecDef(CardSpecDTO dto)
        {
            CardSpecDef def = new CardSpecDef();
            def.Level = dto.Level;
            def.Count = dto.Count;
            def.Radius = dto.Radius;
            def.Ratio = dto.Ratio;
            def.TurnSpeed = dto.TurnSpeed;
            def.AngleH = dto.AngleH;
            def.AngleV = dto.AngleV;
            def.Interval = dto.Interval;
            def.LoseRange = dto.LoseRange;
            return def;
        }

        // 레벨 순으로 정렬하고, 1부터 빠짐없이 이어지는지 확인합니다. (0번 = 1레벨로 꺼내 쓰므로)
        private void SortAndCheckLevels(int cardId, List<CardSpecDef> levels)
        {
            levels.Sort(CompareLevel);
            for (int i = 0; i < levels.Count; i++)
            {
                if (levels[i].Level != i + 1)
                {
                    Debug.LogWarning($"[CardTable] 카드 {cardId}의 스펙 레벨이 1부터 이어지지 않습니다. ({i + 1}레벨 자리에 {levels[i].Level}레벨)");
                    return;
                }
            }
        }

        private static int CompareLevel(CardSpecDef a, CardSpecDef b)
        {
            return a.Level.CompareTo(b.Level);
        }
        #endregion

        #region 칸 해석
        private static CardTiming ParseTiming(string text)
        {
            return (CardTiming)Enum.Parse(typeof(CardTiming), text.Trim());
        }

        // 비어 있으면 0 (대상 없음). 여러 개는 쉼표로 구분 (Enemy,Prop)
        private static ContactTarget ParseTarget(string text)
        {
            ContactTarget target = 0;
            foreach (string name in SplitList(text))
            {
                target |= (ContactTarget)Enum.Parse(typeof(ContactTarget), name);
            }
            return target;
        }

        // 쉼표로 나눈 이름들 (빈 칸은 빼고)
        private static List<string> SplitList(string text)
        {
            List<string> names = new List<string>();
            if (string.IsNullOrEmpty(text))
                return names;

            foreach (string name in text.Split(','))
            {
                string trimmed = name.Trim();
                if (trimmed.Length > 0)
                {
                    names.Add(trimmed);
                }
            }
            return names;
        }

        // 빈 칸은 0으로 봅니다.
        private static int ParseInt(string text)
        {
            if (text.Length == 0)
                return 0;

            return int.Parse(text, CultureInfo.InvariantCulture);
        }

        private static float ParseFloat(string text)
        {
            if (text.Length == 0)
                return 0f;

            return float.Parse(text, CultureInfo.InvariantCulture);
        }
        #endregion
    }
}
