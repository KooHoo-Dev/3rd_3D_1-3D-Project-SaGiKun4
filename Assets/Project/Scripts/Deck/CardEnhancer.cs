using System.Collections.Generic;
using Study3D.FpsGame.Weapon.Cards;
using UnityEngine;

namespace Study3D.FpsGame.Deck
{
    // CardEnhancer
    // : 카드 강화 규칙 입니다. 덱의 카드 2장을 한 장으로 합칩니다. (첫 번째 카드 → 두 번째 카드 순서로 발동)
    // - 어디서 강화하는지(UI, 강화 오브젝트 등)는 모르고, 덱 전체 번호 2개만 받습니다. (CardDeck의 손패 → 뽑을 카드 → 버린 카드 순)
    // - 합쳐진 카드는 첫 번째 카드 자리에 들어가고, 두 번째 카드는 덱에서 완전히 사라집니다.
    // - 강화된 카드를 다시 재료로 쓸 수 있습니다. (CompositeCardData 안쪽 재료까지 순서대로 펼쳐집니다)
    // - 강화 카드끼리만 합칠 수 있습니다. 공격 카드는 재료로 쓸 수 없습니다.
    // - 한쪽이 강화 전용 카드면 합치지 않고, 다른 쪽 카드의 값을 바꾼 카드로 교체합니다. (칸 순서 상관없음)
    //   시점 변경: 맨 첫 재료 카드의 시점 / 대상 변경: 첫 반응 카드의 반응 대상 / 스펙 강화: 맨 첫 재료 카드의 스펙 레벨
    //   강화 전용 카드는 쓰고 나면 사라집니다.
    // - 강화 1회에 ENHANCE_COST만큼 재화가 듭니다. 재화를 내는 것은 강화하는 쪽(UpgradePanel)이 맡습니다. (여기는 덱만 다룸)
    public static class CardEnhancer
    {
        public const int ENHANCE_COST = 200;          // 강화 1회 비용 (재화)
        private const string ENHANCED_MARK = "+"; // 강화된 카드 표시 (프로토타입: 이름 뒤에 붙임)

        // 강화하지 못했으면 이유를 로그로 남기고 false를 반환합니다.
        public static bool TryEnhance(CardDeck deck, int firstIndex, int secondIndex)
        {
            if (firstIndex == secondIndex)
            {
                Debug.Log("[Enhance] 같은 카드끼리는 강화할 수 없습니다.");
                return false;
            }

            CardData first = deck.GetCard(firstIndex);
            CardData second = deck.GetCard(secondIndex);
            if (CanUseAsMaterials(first, second) == false)
                return false;

            TimingCardData firstTiming = first as TimingCardData;
            if (firstTiming != null)
                return TryChangeTiming(deck, secondIndex, firstIndex, firstTiming);

            TimingCardData secondTiming = second as TimingCardData;
            if (secondTiming != null)
                return TryChangeTiming(deck, firstIndex, secondIndex, secondTiming);

            TargetCardData firstTarget = first as TargetCardData;
            if (firstTarget != null)
                return TryChangeTarget(deck, secondIndex, firstIndex, firstTarget);

            TargetCardData secondTarget = second as TargetCardData;
            if (secondTarget != null)
                return TryChangeTarget(deck, firstIndex, secondIndex, secondTarget);

            SpecCardData firstSpec = first as SpecCardData;
            if (firstSpec != null)
                return TrySpecLevelUp(deck, secondIndex, firstIndex, firstSpec);

            SpecCardData secondSpec = second as SpecCardData;
            if (secondSpec != null)
                return TrySpecLevelUp(deck, firstIndex, secondIndex, secondSpec);

            Combine(deck, firstIndex, secondIndex, first, second);
            return true;
        }

        // 두 카드를 강화 재료로 함께 쓸 수 있는지. 쓸 수 없으면 이유를 로그로 남깁니다.
        private static bool CanUseAsMaterials(CardData first, CardData second)
        {
            if (first == null || second == null)
            {
                Debug.LogWarning("[Enhance] 고른 카드를 덱에서 찾지 못했습니다.");
                return false;
            }
            if (first.Type == CardType.Attack || second.Type == CardType.Attack)
            {
                Debug.Log("[Enhance] 공격 카드는 강화에 쓸 수 없습니다.");
                return false;
            }
            if (first.Type == CardType.Modifier && second.Type == CardType.Modifier)
            {
                Debug.Log("[Enhance] 강화 전용 카드끼리는 강화할 수 없습니다.");
                return false;
            }
            return true;
        }

        // 두 카드를 조합 카드 한 장으로 합쳐 첫 번째 카드 자리에 넣고, 두 번째 카드는 덱에서 없앱니다.
        private static void Combine(CardDeck deck, int firstIndex, int secondIndex, CardData first, CardData second)
        {
            CompositeCardData combined = CompositeCardData.Create(first.CardName + ENHANCED_MARK, first, second);
            // 바꾸기를 먼저 해야 second를 뺄 때 당겨지는 번호를 신경 쓰지 않아도 됩니다.
            deck.Replace(firstIndex, combined);
            deck.Remove(secondIndex);
            Debug.Log($"[Enhance] {first.CardName}(Lv.{first.EnhanceLevel}) + {second.CardName}(Lv.{second.EnhanceLevel}) → {combined.CardName}(Lv.{combined.EnhanceLevel})");
        }

        // 시점 변경: targetIndex 카드의 시점을 바꾼 카드로 교체하고, 시점 변경 카드는 덱에서 없앱니다.
        // 조합 카드는 맨 첫 재료 카드의 시점만 바뀝니다.
        private static bool TryChangeTiming(CardDeck deck, int targetIndex, int modifierIndex, TimingCardData modifier)
        {
            CardData target = deck.GetCard(targetIndex);
            CardData head = GetFirstCard(target);
            if (CanChangeTimingTo(target, head, modifier.Timing) == false)
                return false;

            CardData changed = target.WithTiming(modifier.Timing, target.CardName + ENHANCED_MARK);
            // 바꾸기를 먼저 해야 시점 변경 카드를 뺄 때 당겨지는 번호를 신경 쓰지 않아도 됩니다.
            deck.Replace(targetIndex, changed);
            deck.Remove(modifierIndex);
            Debug.Log($"[Enhance] {target.CardName}({head.Timing}, Lv.{target.EnhanceLevel}) + {modifier.CardName} → {changed.CardName}({modifier.Timing}, Lv.{changed.EnhanceLevel})");
            return true;
        }

        // target(첫 카드는 head)의 시점을 newTiming으로 바꿀 수 있는지. 바꿀 수 없으면 이유를 로그로 남깁니다.
        private static bool CanChangeTimingTo(CardData target, CardData head, CardTiming newTiming)
        {
            if (head == null || head.CanChangeTiming == false)
            {
                Debug.Log($"[Enhance] {target.CardName}은(는) 시점을 바꿀 수 없는 카드입니다.");
                return false;
            }
            if (head.IsTimingBlocked(newTiming))
            {
                Debug.Log($"[Enhance] {target.CardName}은(는) {newTiming} 시점으로 바꿀 수 없습니다.");
                return false;
            }
            if (head.Timing == newTiming)
            {
                Debug.Log($"[Enhance] {target.CardName}은(는) 이미 {newTiming} 시점입니다.");
                return false;
            }
            return true;
        }

        // 대상 변경: targetIndex 카드의 반응 대상을 바꾼 카드로 교체하고, 대상 변경 카드는 덱에서 없앱니다.
        // 조합 카드는 발동 순서상 첫 반응 카드의 대상만 바뀝니다.
        private static bool TryChangeTarget(CardDeck deck, int targetIndex, int modifierIndex, TargetCardData modifier)
        {
            CardData target = deck.GetCard(targetIndex);
            ResponseCardData response = GetFirstResponseCard(target);
            if (CanChangeTargetTo(target, response, modifier.NewTarget) == false)
                return false;

            CardData changed = target.WithTarget(modifier.NewTarget, target.CardName + ENHANCED_MARK);
            // 바꾸기를 먼저 해야 대상 변경 카드를 뺄 때 당겨지는 번호를 신경 쓰지 않아도 됩니다.
            deck.Replace(targetIndex, changed);
            deck.Remove(modifierIndex);
            Debug.Log($"[Enhance] {target.CardName}({response.Target}, Lv.{target.EnhanceLevel}) + {modifier.CardName} → {changed.CardName}({response.GetAppliedTarget(modifier.NewTarget)}, Lv.{changed.EnhanceLevel})");
            return true;
        }

        // target(안의 첫 반응 카드는 response)의 대상을 newTarget으로 바꿀 수 있는지. 바꿀 수 없으면 이유를 로그로 남깁니다.
        private static bool CanChangeTargetTo(CardData target, ResponseCardData response, ContactTarget newTarget)
        {
            if (response == null)
            {
                Debug.Log($"[Enhance] {target.CardName}에는 대상을 바꿀 수 있는 카드(반응 카드)가 없습니다.");
                return false;
            }

            ContactTarget applied = response.GetAppliedTarget(newTarget);
            if (applied == 0)
            {
                Debug.Log($"[Enhance] {target.CardName}은(는) {newTarget}을(를) 대상으로 할 수 없습니다.");
                return false;
            }
            if (applied == response.Target)
            {
                Debug.Log($"[Enhance] {target.CardName}은(는) 이미 {applied}이(가) 대상입니다.");
                return false;
            }
            return true;
        }

        // 스펙 강화: targetIndex 카드의 스펙 레벨을 1 올린 카드로 교체하고, 스펙 강화 카드는 덱에서 없앱니다.
        // 조합 카드는 맨 첫 재료 카드만 오릅니다. 최대 레벨(스펙 표의 줄 수)이면 거부합니다.
        private static bool TrySpecLevelUp(CardDeck deck, int targetIndex, int modifierIndex, SpecCardData modifier)
        {
            CardData target = deck.GetCard(targetIndex);
            CardData head = GetFirstCard(target);
            if (head == null || head.CanSpecLevelUp() == false)
            {
                Debug.Log($"[Enhance] {target.CardName}은(는) 더 이상 스펙을 올릴 수 없습니다. (최대 레벨)");
                return false;
            }

            CardData changed = target.WithSpecLevelUp(target.CardName + ENHANCED_MARK);
            // 바꾸기를 먼저 해야 스펙 강화 카드를 뺄 때 당겨지는 번호를 신경 쓰지 않아도 됩니다.
            deck.Replace(targetIndex, changed);
            deck.Remove(modifierIndex);
            Debug.Log($"[Enhance] {target.CardName}(스펙 Lv.{head.SpecLevel}) + {modifier.CardName} → {changed.CardName}(스펙 Lv.{head.SpecLevel + 1})");
            return true;
        }

        // 한쪽이 강화 전용 카드(시점 변경, 대상 변경, 스펙 강화)이고, 다른 쪽에 쓸 수 없는 카드인지. (칸 순서 상관없음)
        // 강화 화면이 고를 수 없는 카드를 흐리게 표시할 때 씁니다. 실제 강화 판단은 TryEnhance가 합니다.
        public static bool IsModifierBlocked(CardData a, CardData b)
        {
            if (a == null || b == null)
                return false;

            TimingCardData aTiming = a as TimingCardData;
            if (aTiming != null)
                return IsTimingBlockedFor(b, aTiming.Timing);

            TimingCardData bTiming = b as TimingCardData;
            if (bTiming != null)
                return IsTimingBlockedFor(a, bTiming.Timing);

            TargetCardData aTarget = a as TargetCardData;
            if (aTarget != null)
                return IsTargetBlockedFor(b, aTarget.NewTarget);

            TargetCardData bTarget = b as TargetCardData;
            if (bTarget != null)
                return IsTargetBlockedFor(a, bTarget.NewTarget);

            if (a is SpecCardData)
                return IsSpecBlockedFor(b);
            if (b is SpecCardData)
                return IsSpecBlockedFor(a);

            return false;
        }

        // 맨 첫 카드가 더 이상 스펙을 올릴 수 없으면 true (최대 레벨이거나 스펙 표가 없는 카드)
        private static bool IsSpecBlockedFor(CardData target)
        {
            CardData head = GetFirstCard(target);
            return head == null || head.CanSpecLevelUp() == false;
        }

        // 반응 카드가 없거나, 바꿀 대상을 하나도 받을 수 없으면 true
        private static bool IsTargetBlockedFor(CardData target, ContactTarget newTarget)
        {
            ResponseCardData response = GetFirstResponseCard(target);
            if (response == null)
                return true;

            return response.GetAppliedTarget(newTarget) == 0;
        }

        // 발동 순서상 첫 반응 카드 (조합 카드면 펼친 뒤 찾음). 없으면 null
        private static ResponseCardData GetFirstResponseCard(CardData card)
        {
            List<CardData> cards = new List<CardData>();
            card.CollectCards(cards);
            foreach (CardData part in cards)
            {
                ResponseCardData response = part as ResponseCardData;
                if (response != null)
                    return response;
            }
            return null;
        }

        private static bool IsTimingBlockedFor(CardData target, CardTiming timing)
        {
            CardData head = GetFirstCard(target);
            return head == null || head.CanChangeTiming == false || head.IsTimingBlocked(timing);
        }

        // 발사체에 실릴 때 맨 처음 오는 카드 (조합 카드면 펼친 뒤의 첫 재료)
        private static CardData GetFirstCard(CardData card)
        {
            List<CardData> cards = new List<CardData>();
            card.CollectCards(cards);
            if (cards.Count == 0)
                return null;

            return cards[0];
        }
    }
}
