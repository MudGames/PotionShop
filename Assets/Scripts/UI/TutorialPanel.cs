using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 매치3 기본 규칙 + 특수 타일(물약) 3종 설명을, 미션 패널(PuzzleSidePanel)이 보드 오른쪽에 있는 것과
// 대칭되는 자리(보드 왼쪽의 같은 크기 빈 여백)에 배치한 것 - 원래는 화면 전체를 가리는 Dim 모달 +
// "시작하기" 버튼으로 닫는 구조였으나, 퍼즐 패널을 가리지 않고 게임 내내 참고할 수 있는 상시
// 사이드 패널이 낫다는 피드백으로 PuzzleSidePanel과 같은 스타일로 전환함(2026-08-04). 모달이
// 아니므로 열고 닫는 상태 자체가 없다.
public sealed class TutorialPanel
{
    private static readonly Color PanelBackgroundColor = new Color(0.0f, 0.0f, 0.0f, 0.55f);

    public TutorialPanel(Transform parent, Sprite rowBombSprite, Sprite columnBombSprite, Sprite radiusBombSprite)
    {
        RectTransform panelRect = CreatePanelRoot(parent);
        CreateFillBackground(panelRect);

        CreateLabel(panelRect, "TitleLabel", new Vector2(0.05f, 0.9f), new Vector2(0.95f, 0.98f), TextAlignmentOptions.Center, 34.0f).text = "플레이 방법";

        CreateLabel(panelRect, "BasicsLabel", new Vector2(0.05f, 0.78f), new Vector2(0.95f, 0.89f), TextAlignmentOptions.Center, 22.0f).text =
            "인접한 재료를 교환해서 같은 재료 3개 이상을 나란히 맞추면 사라져요.";

        CreateLabel(panelRect, "SpecialIntroLabel", new Vector2(0.05f, 0.6f), new Vector2(0.95f, 0.77f), TextAlignmentOptions.Center, 20.0f).text =
            "재료 4개 이상을 매치하거나 매치가 십자로 겹치면 물약이 생겨요.\n인접 타일과 교환하거나 직접 탭하면 터뜨릴 수 있어요.";

        CreateBombRow(panelRect, 0.44f, 0.58f, rowBombSprite, "빨간 물약 — 가로줄 전체를 없애요");
        CreateBombRow(panelRect, 0.26f, 0.4f, columnBombSprite, "초록 물약 — 세로줄 전체를 없애요");
        CreateBombRow(panelRect, 0.08f, 0.22f, radiusBombSprite, "파란 물약 — 주변 3x3 칸을 없애요");
    }

    // PuzzlePanel 자체의 앵커(가로 0.317-0.683, 세로 0-0.65)를 보드 반대쪽(왼쪽)의 같은 크기 빈
    // 여백에 대칭시켜, 그 왼쪽 가장자리에 딱 맞닿게 배치한다 - PuzzleSidePanel.CreatePanelRoot 참고.
    private static RectTransform CreatePanelRoot(Transform parent)
    {
        GameObject panelObject = new GameObject("TutorialPanel", typeof(RectTransform));
        panelObject.transform.SetParent(parent, false);

        RectTransform rect = panelObject.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.0f, 0.0f);
        rect.anchorMax = new Vector2(0.317f, 0.65f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta = Vector2.zero;
        return rect;
    }

    // targetRect를 채우는 어둡고 반투명한 패널 - PuzzleSidePanel의 배경과 동일하다.
    private static void CreateFillBackground(RectTransform targetRect)
    {
        GameObject backgroundObject = new GameObject("Background", typeof(RectTransform), typeof(Image));
        backgroundObject.transform.SetParent(targetRect, false);
        backgroundObject.transform.SetAsFirstSibling();

        RectTransform rect = backgroundObject.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        backgroundObject.GetComponent<Image>().color = PanelBackgroundColor;
    }

    private static void CreateBombRow(Transform parent, float yMin, float yMax, Sprite badgeSprite, string text)
    {
        float yPadding = (yMax - yMin) * 0.15f;

        GameObject iconObject = new GameObject("BombIcon", typeof(RectTransform), typeof(Image));
        iconObject.transform.SetParent(parent, false);
        RectTransform iconRect = iconObject.GetComponent<RectTransform>();
        iconRect.anchorMin = new Vector2(0.06f, yMin + yPadding);
        iconRect.anchorMax = new Vector2(0.2f, yMax - yPadding);
        iconRect.offsetMin = Vector2.zero;
        iconRect.offsetMax = Vector2.zero;

        Image icon = iconObject.GetComponent<Image>();
        icon.sprite = badgeSprite;
        icon.preserveAspect = true;

        TextMeshProUGUI label = CreateLabel(parent, "BombLabel", new Vector2(0.24f, yMin), new Vector2(0.95f, yMax), TextAlignmentOptions.MidlineLeft, 18.0f);
        label.text = text;
    }

    private static TextMeshProUGUI CreateLabel(Transform parent, string name, Vector2 anchorMin, Vector2 anchorMax, TextAlignmentOptions alignment, float fontSize)
    {
        GameObject labelObject = new GameObject(name, typeof(RectTransform));
        labelObject.transform.SetParent(parent, false);

        RectTransform rect = labelObject.GetComponent<RectTransform>();
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        TextMeshProUGUI label = labelObject.AddComponent<TextMeshProUGUI>();
        label.alignment = alignment;
        label.fontSize = fontSize;
        label.color = Color.white;
        label.outlineWidth = 0.2f;
        label.outlineColor = Color.black;
        return label;
    }
}
