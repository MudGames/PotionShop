using System.Collections.Generic;
using Puzzle.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 미션(주문 진행도) 표시를, PuzzlePanel 바로 오른쪽에 위치한 자체 패널로 분리한 것 -
// PuzzleHud의 상단 바는 짧은 라벨 몇 개를 놓을 공간밖에 없어서 전체 주문 정보를 담을 수
// 없었기 때문이다. PuzzlePanel 자체의 높이를 그대로 따라가며, PuzzlePanel이 왼쪽에 이미 남겨둔
// 것과 같은 크기의 빈 여백에서 그 오른쪽 가장자리에 딱 맞닿게 배치된다. 남은 이동 횟수는
// 2026-08-04부터 여기가 아니라 PuzzleHud의 점수 오른쪽에 표시된다.
//
// 각 주문 요구사항을 "아이콘 × 남은 개수" 한 줄로 표시한다(2026-08-04 이전에는 남은 개수만큼
// 아이콘을 grid로 늘어놓았으나, 개수가 많으면 알아보기 어렵다는 피드백으로 변경) - 튜토리얼
// 패널(TutorialPanel)의 물약 설명 줄과 같은 형식/아이콘 크기를 쓴다. 요구사항은 최대
// 3개(GridController.GenerateRandomRequirements의 상한)이므로 행도 고정 3개만 미리 만들어두고
// 필요한 만큼만 활성화한다.
//
// GridController를 직접 참조하지 않는다 - OrderProgressChannel을 구독해 스스로 갱신한다(CLAUDE.md
// 이벤트 채널 아키텍처 원칙 참고). 재료 아이콘(ingredientSprites)은 이벤트가 아니라 레벨 설정값이므로,
// Match3Controller가 SetupLevel마다 SetIngredientSprites로 직접 넘겨준다.
public sealed class PuzzleSidePanel
{
    private static readonly Color PanelBackgroundColor = new Color(0.0f, 0.0f, 0.0f, 0.55f);
    private static readonly Color MissingIconColor = new Color(1.0f, 1.0f, 1.0f, 0.4f);

    // GridController.GenerateRandomRequirements가 만드는 주문 요구사항 개수의 상한과 맞춘 값 -
    // 그 이상은 만들어지지 않으므로 고정 행 개수로 충분하다.
    private const int MaxRequirementRows = 3;

    private readonly RequirementRow[] _requirementRows = new RequirementRow[MaxRequirementRows];
    private Sprite[] _ingredientSprites = new Sprite[0];

    // parent: 퍼즐 Canvas(퍼즐 패널이 아님) - 이렇게 해야 이 패널의 위치가 PuzzlePanel 자체의
    // 로컬 스케일/장식과 무관해지며, PuzzleHud의 HudBar가 이미 따르고 있는 것과 같은 이유다.
    public PuzzleSidePanel(Transform parent, OrderProgressEventChannel orderProgressChannel)
    {
        RectTransform panelRect = CreatePanelRoot(parent);
        CreateFillBackground(panelRect);

        TextMeshProUGUI titleLabel = CreateLabel(panelRect, "MissionTitle", new Vector2(0.05f, 0.9f), new Vector2(0.95f, 0.98f), TextAlignmentOptions.Center, 30.0f);
        titleLabel.text = "미션";

        // 행 높이(0.14)와 아이콘 x범위(0.06-0.2)는 TutorialPanel.CreateBombRow와 동일한 값이다 -
        // 두 패널이 서로 대칭되는 같은 크기이므로, 같은 비율을 쓰면 아이콘의 실제 픽셀 크기도 같아진다.
        _requirementRows[0] = CreateRequirementRow(panelRect, 0.7f, 0.84f);
        _requirementRows[1] = CreateRequirementRow(panelRect, 0.5f, 0.64f);
        _requirementRows[2] = CreateRequirementRow(panelRect, 0.3f, 0.44f);

        orderProgressChannel.OnRaised += UpdateOrderProgress;
    }

    // 레벨마다 재료 아이콘이 달라지므로 SetupLevel 시점에 한 번 호출한다 - OrderProgressChannel이
    // 실어 나르는 값은 타입 인덱스/수집 개수뿐이라, 그걸 그림으로 바꾸려면 이 정보가 필요하다.
    public void SetIngredientSprites(Sprite[] ingredientSprites)
    {
        _ingredientSprites = ingredientSprites;
    }

    // 아직 다 모으지 못한 요구사항만 위에서부터 순서대로 행을 채운다 - 다 모은 요구사항은 그
    // 시점부터 해당 행이 사라지고, 아래 행들이 위로 당겨지는 대신 남은 요구사항 개수만큼만
    // 행이 보인다(원래 grid 방식의 "다 모으면 사라짐" 동작과 동일).
    private void UpdateOrderProgress(IReadOnlyList<OrderProgressEntry> progress)
    {
        Sprite[] ingredientSprites = _ingredientSprites;
        int rowIndex = 0;

        for (int i = 0; i < progress.Count && rowIndex < _requirementRows.Length; i++)
        {
            OrderProgressEntry entry = progress[i];
            int remaining = Mathf.Max(0, entry.Required - entry.Collected);
            if (remaining == 0)
            {
                continue;
            }

            RequirementRow row = _requirementRows[rowIndex];
            row.Root.SetActive(true);

            Sprite sprite = entry.TypeIndex >= 0 && entry.TypeIndex < ingredientSprites.Length ? ingredientSprites[entry.TypeIndex] : null;
            if (sprite != null)
            {
                row.Icon.sprite = sprite;
                row.Icon.color = Color.white;
            }
            else
            {
                // 이 재료에 대해 설정된 아이콘이 없다 - 완전히 숨기는 대신 밋밋한 중립색 사각형으로 대체한다.
                row.Icon.sprite = null;
                row.Icon.color = MissingIconColor;
            }

            row.Label.text = $"× {remaining}";
            rowIndex++;
        }

        for (int i = rowIndex; i < _requirementRows.Length; i++)
        {
            _requirementRows[i].Root.SetActive(false);
        }
    }

    private static RectTransform CreatePanelRoot(Transform parent)
    {
        GameObject panelObject = new GameObject("MissionPanel", typeof(RectTransform));
        panelObject.transform.SetParent(parent, false);

        RectTransform rect = panelObject.GetComponent<RectTransform>();
        // PuzzlePanel 자체의 앵커(가로 0.317-0.683, 세로 0-0.65)를 보드 반대쪽의 같은 크기 빈
        // 여백에 대칭시켜, 그 오른쪽 가장자리에 딱 맞닿게 배치한다.
        rect.anchorMin = new Vector2(0.683f, 0.0f);
        rect.anchorMax = new Vector2(1.0f, 0.65f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta = Vector2.zero;
        return rect;
    }

    // targetRect를 채우는 어둡고 반투명한 패널 - PuzzleHud의 HudBar 배경과 동일하다.
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

    // 아이콘(왼쪽) + "×개수" 라벨(오른쪽) 한 줄. Root를 통째로 SetActive해서 요구사항이 다 채워지면
    // 행 전체를 숨긴다.
    private static RequirementRow CreateRequirementRow(Transform parent, float yMin, float yMax)
    {
        GameObject rowObject = new GameObject("Row", typeof(RectTransform));
        rowObject.transform.SetParent(parent, false);
        RectTransform rowRect = rowObject.GetComponent<RectTransform>();
        rowRect.anchorMin = new Vector2(0.0f, yMin);
        rowRect.anchorMax = new Vector2(1.0f, yMax);
        rowRect.offsetMin = Vector2.zero;
        rowRect.offsetMax = Vector2.zero;

        GameObject iconObject = new GameObject("Icon", typeof(RectTransform), typeof(Image));
        iconObject.transform.SetParent(rowRect, false);
        RectTransform iconRect = iconObject.GetComponent<RectTransform>();
        // TutorialPanel.CreateBombRow와 동일한 x범위(0.06-0.2)와 세로 패딩 비율(15%) - 두 패널의
        // 크기가 같으므로 같은 비율을 쓰면 아이콘의 실제 픽셀 크기도 같아진다.
        iconRect.anchorMin = new Vector2(0.06f, 0.15f);
        iconRect.anchorMax = new Vector2(0.2f, 0.85f);
        iconRect.offsetMin = Vector2.zero;
        iconRect.offsetMax = Vector2.zero;

        Image icon = iconObject.GetComponent<Image>();
        icon.preserveAspect = true;

        TextMeshProUGUI label = CreateLabel(rowRect, "Label", new Vector2(0.24f, 0.0f), new Vector2(0.95f, 1.0f), TextAlignmentOptions.MidlineLeft, 20.0f);

        return new RequirementRow(rowObject, icon, label);
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

    private sealed class RequirementRow
    {
        public readonly GameObject Root;
        public readonly Image Icon;
        public readonly TextMeshProUGUI Label;

        public RequirementRow(GameObject root, Image icon, TextMeshProUGUI label)
        {
            Root = root;
            Icon = icon;
            Label = label;
        }
    }
}
