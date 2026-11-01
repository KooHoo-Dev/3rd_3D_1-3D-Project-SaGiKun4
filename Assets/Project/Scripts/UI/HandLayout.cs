using System;
using UnityEngine;

/// <summary>손패 배치에 쓰이는 값. UIBuilder(에디터 조립)와 HandView(런타임 연출)가 같은 값을 쓴다.</summary>
[Serializable]
public class HandLayoutSettings
{
    [Tooltip("CanvasScaler 기준 해상도")]
    public Vector2 referenceResolution = new Vector2(1920f, 1080f);

    [Header("카드 크기")]
    public Vector2 cardSize = new Vector2(200f, 280f);
    [Tooltip("카드 하단이 화면 아래로 잘려 들어가는 정도 (시안처럼 설명 부분이 살짝 가려짐)")]
    public float cardBottomHang = 60f;

    [Header("손패 영역")]
    [Tooltip("좌우 각각 남겨둘 여백 비율 (0.25 ~ 0.30)")]
    [Range(0.25f, 0.30f)] public float sideMarginRatio = 0.27f;
    [Tooltip("카드가 적을 때 카드 간격 (카드 폭 대비 비율)")]
    [Range(0.5f, 1.2f)] public float preferredSpacingRatio = 0.95f;
    [Tooltip("가장자리 카드의 최대 기울기 (도)")]
    [Range(0f, 15f)] public float maxEdgeAngle = 8f;
    [Tooltip("가장자리 카드가 아래로 처지는 정도 (부채꼴)")]
    [Range(0f, 60f)] public float arcDepth = 20f;

    [Header("선택 카드 연출")]
    public float selectedRaise = 30f;
    [Range(1.1f, 2f)] public float selectedScale = 1.2f;
    [Tooltip("카드 윗변이 넘으면 안 되는 높이 (화면 높이 대비 비율). 화면 중앙(0.5)을 가리지 않도록 보장")]
    [Range(0.3f, 0.5f)] public float centerSafeRatio = 0.4f;

    /// <summary>좌우 여백을 뺀 손패 최대 폭</summary>
    public float MaxHandWidth
    {
        get { return referenceResolution.x * (1f - sideMarginRatio * 2f); }
    }

    /// <summary>선택 카드가 화면 중앙 안전선을 넘지 않는 최대 배율</summary>
    public float SafeSelectedScale
    {
        get
        {
            float safeTopY = referenceResolution.y * centerSafeRatio;
            float selectedBottomY = -cardBottomHang + selectedRaise;
            return (safeTopY - selectedBottomY) / cardSize.y;
        }
    }

    /// <summary>실제로 적용될 선택 배율 (안전선 기준으로 제한)</summary>
    public float EffectiveSelectedScale
    {
        get { return Mathf.Min(selectedScale, SafeSelectedScale); }
    }
}

/// <summary>카드 한 장의 목표 자세 (HandArea 기준 anchoredPosition, z 회전, 균일 배율)</summary>
public struct CardPose
{
    public Vector2 position;
    public float angle;
    public float scale;
}

/// <summary>인덱스 기반 손패 배치 계산. 카드 피벗·앵커는 하단 중앙을 전제로 한다.</summary>
public static class HandLayout
{
    /// <param name="index">손패 안에서의 순서 (0 = 왼쪽 끝)</param>
    /// <param name="count">현재 손패 장수</param>
    /// <param name="selectedIndex">선택된 카드 인덱스 (-1 = 선택 없음)</param>
    public static CardPose Calculate(int index, int count, int selectedIndex, HandLayoutSettings s)
    {
        float half = (count - 1) * 0.5f;
        float t = index - half;                       // 중앙 기준 칸 위치

        float n = 0f;                                 // -1 ~ 1 정규화
        if (count > 1)
        {
            n = t / half;
        }

        // 카드 수가 적으면 덜 기울이고, 많을수록 최대 각도까지
        float edgeAngle = s.maxEdgeAngle * Mathf.Clamp01((count - 1) / 4f);

        float spacing = CalculateSpacing(count, edgeAngle, s);

        float baseY = -s.cardBottomHang;
        CardPose pose;
        pose.position = new Vector2(t * spacing, baseY - n * n * s.arcDepth);
        pose.angle = -n * edgeAngle;
        pose.scale = 1f;

        if (index == selectedIndex)
        {
            pose.position.y = baseY + s.selectedRaise;
            pose.angle = 0f;
            pose.scale = s.EffectiveSelectedScale;
        }

        return pose;
    }

    // 간격: 기본 간격을 쓰되, 기울어진 가장자리 카드까지 최대 폭 안에 들어오도록 축소
    private static float CalculateSpacing(int count, float edgeAngle, HandLayoutSettings s)
    {
        if (count <= 1)
            return 0f;

        float edgeOverhang = s.cardSize.y * Mathf.Sin(edgeAngle * Mathf.Deg2Rad);
        float usable = Mathf.Max(0f, s.MaxHandWidth - s.cardSize.x - edgeOverhang * 2f);
        float preferredSpacing = s.cardSize.x * s.preferredSpacingRatio;
        return Mathf.Min(preferredSpacing, usable / (count - 1));
    }

    /// <summary>계산된 자세를 즉시 적용한다. (애니메이션 없이)</summary>
    public static void Apply(RectTransform card, CardPose pose)
    {
        card.anchoredPosition = pose.position;
        card.localEulerAngles = new Vector3(0f, 0f, pose.angle);
        card.localScale = Vector3.one * pose.scale;
    }
}
