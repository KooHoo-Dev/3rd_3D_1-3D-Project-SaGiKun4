using System.Collections.Generic;
using Study3D.FpsGame.Deck;
using Study3D.FpsGame.Weapon.Cards;
using UnityEngine;

namespace Study3D.FpsGame.Data
{
    // cards.tsv 한 줄을 글자 그대로 담는 그릇 (파일 형식 = 칸 이름과 같음)
    // ID	Kind	Name	Timing	BlockedTimings	Target	EffectKey	EffectRadius
    public class CardDTO
    {
        public int ID;
        public string Kind;           // 카드 동작 종류 (Split, Explosion ...). CardFactory가 이 값으로 카드 객체를 만듭니다
        public string Name;
        public string Timing;         // 시점 이름 (Shoot, Flying, Hit, Destroy)
        public string BlockedTimings; // 시점 변경으로 바꿀 수 없는 시점들, 쉼표로 구분 (비면 없음)
        public string Target;         // 반응 대상, 쉼표로 구분 (Enemy, World, Prop). 반응 카드와 대상 변경 카드만 씀
        public string EffectKey;      // 발동 이펙트 프리팹의 Resources 경로 (비면 이펙트 없음)
        public float EffectRadius;    // 이펙트를 크기 1로 재생했을 때 원의 반경(m). 범위에 맞춰 키울 때 씀 (폭발)
    }

    // 게임에서 쓰는 카드 한 장의 정의 (DTO의 글자를 실제 값으로 바꾸고, 프리팹을 미리 찾아 둔 것)
    public class CardDef
    {
        public int ID;
        public CardKind Kind;
        public string Name;
        public CardTiming Timing;
        public List<CardTiming> BlockedTimings = new List<CardTiming>();
        public ContactTarget Target;
        public GameObject EffectPrefab; // 없으면 null
        public float EffectRadius;
    }

    // card_specs.tsv 한 줄: 카드 하나의 스펙 레벨 한 단계
    // CardID	Level	Count	Radius	Ratio	TurnSpeed	AngleH	AngleV	Interval	LoseRange
    // - 칸은 모든 카드가 같이 쓰고, 카드마다 필요한 칸만 읽습니다. (분열: Count, AngleH, AngleV / 폭발: Radius, Ratio ...)
    public class CardSpecDTO
    {
        public int CardID;
        public int Level;
        public int Count;
        public float Radius;
        public float Ratio;
        public float TurnSpeed;
        public float AngleH;
        public float AngleV;
        public int Interval;
        public float LoseRange;
    }

    // 게임에서 쓰는 스펙 레벨 한 단계의 수치
    public class CardSpecDef
    {
        public int Level;
        public int Count;       // 횟수, 장수 (분열 추가 장수, 관통/반사/전이 횟수)
        public float Radius;    // 반경 (폭발, 전이, 유도)
        public float Ratio;     // 비율 (폭발 피해 비율)
        public float TurnSpeed; // 초당 회전 각도 (유도)
        public float AngleH;    // 좌우로 퍼지는 최대 각도 (분열)
        public float AngleV;    // 위아래로 퍼지는 최대 각도 (분열)
        public int Interval;    // 프레임 간격 (유도: 쫓을 적이 없을 때 다시 찾기까지)
        public float LoseRange; // 반경의 배수 (유도: 쫓던 적이 이만큼 멀어지면 놓침)
    }
}
