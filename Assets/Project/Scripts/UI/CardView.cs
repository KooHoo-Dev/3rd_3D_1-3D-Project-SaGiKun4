using System;
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

        public void Show(CardData data)
        {
            if (titleText != null)
            {
                titleText.text = data.CardName;
                titleText.color = Color.white;
            }
            if (typeText != null)
            {
                typeText.text = data.Type.ToString();
            }
            if (descText != null)
            {
                descText.text = data.Description;
            }
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
