using System;
using System.Collections.Generic;
using Study3D.FpsGame.Deck;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Study3D.FpsGame.UI
{
    // CardView
    // : 메뉴 화면에 그리는 카드 한 장의 글자/그림 참조입니다. (목록 카드, 강화 칸 카드 등에서 같이 씀)
    [Serializable]
    public class CardView
    {
        public GameObject card;
        public Image artImage;
        public TMP_Text titleText;
        public TMP_Text typeText;
        public TMP_Text descText;
        [Tooltip("강화 태그 목록. 비우면 태그 없이 카드 이름(강화되면 뒤에 +)만 씁니다 (오른쪽 목록 카드 등)")]
        public CardTagList tags;

        // 카드를 펼쳐 담아 두는 버퍼 (Show 안에서만 씀)
        private static readonly List<CardData> CollectBuffer = new List<CardData>();

        public void Show(CardData data)
        {
            CardData face = GetFaceCard(data);
            if (titleText != null)
            {
                titleText.text = face.CardName;
                titleText.color = Color.white;
            }
            if (typeText != null)
            {
                typeText.text = data.Type.ToString();
            }
            if (descText != null)
            {
                descText.text = face.Description;
            }
            if (tags != null)
            {
                tags.Show(CollectBuffer);
            }
        }

        // 카드 면에 그릴 카드. 태그가 있으면 펼친 첫 카드(본체)이고, 나머지 강화 카드는 태그로 따로 보여 줍니다.
        // 태그가 없으면 카드 그대로 그립니다. (조합 카드 이름 = 본체 이름 + "+")
        private CardData GetFaceCard(CardData data)
        {
            CollectBuffer.Clear();
            data.CollectCards(CollectBuffer);

            if (tags == null || CollectBuffer.Count == 0)
                return data;

            return CollectBuffer[0];
        }

        // 다른 카드 뷰에 보이는 그대로 옮겨 그립니다.
        public void CopyFrom(CardView from)
        {
            if (from.titleText != null && titleText != null)
            {
                titleText.text = from.titleText.text;
                titleText.color = from.titleText.color;
            }
            if (from.typeText != null && typeText != null)
            {
                typeText.text = from.typeText.text;
            }
            if (from.descText != null && descText != null)
            {
                descText.text = from.descText.text;
            }
            if (from.artImage != null && artImage != null)
            {
                artImage.color = from.artImage.color;
            }
        }
    }
}
