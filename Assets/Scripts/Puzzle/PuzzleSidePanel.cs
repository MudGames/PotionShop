using System.Collections.Generic;
using Puzzle.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 미션(주문 진행도)과 이동 제한 표시를, PuzzlePanel 바로 오른쪽에 위치한 자체 패널로 분리한 것 -
// PuzzleHud의 상단 바는 짧은 라벨 몇 개를 놓을 공간밖에 없어서 전체 주문/이동 정보를 담을 수
// 없었기 때문이다. PuzzlePanel 자체의 높이를 그대로 따라가며, PuzzlePanel이 왼쪽에 이미 남겨둔
// 것과 같은 크기의 빈 여백에서 그 오른쪽 가장자리에 딱 맞닿게 배치된다.
//
// GridController를 직접 참조하지 않는다 - MovesChangedChannel/OrderProgressChannel을 구독해
// 스스로 갱신한다(CLAUDE.md 이벤트 채널 아키텍처 원칙 참고). 재료 아이콘(ingredientSprites)은
// 이벤트가 아니라 레벨 설정값이므로, Match3Controller가 SetupLevel마다 SetIngredientSprites로
// 직접 넘겨준다.
public sealed class PuzzleSidePanel
{
    private static readonly Color PanelBackgroundColor = new Color(0.0f, 0.0f, 0.0f, 0.55f);
    private const float SwatchSize = 48.0f;

    private readonly RectTransform _orderPanel;
    private readonly TextMeshProUGUI _movesLabel;
    private readonly PoolManager<Image> _swatchPool;
    private Sprite[] _ingredientSprites = new Sprite[0];

    // parent: 퍼즐 Canvas(퍼즐 패널이 아님) - 이렇게 해야 이 패널의 위치가 PuzzlePanel 자체의
    // 로컬 스케일/장식과 무관해지며, PuzzleHud의 HudBar가 이미 따르고 있는 것과 같은 이유다.
    public PuzzleSidePanel(Transform parent, IntEventChannel movesChangedChannel, OrderProgressEventChannel orderProgressChannel)
    {
        RectTransform panelRect = CreatePanelRoot(parent);
        CreateFillBackground(panelRect);

        TextMeshProUGUI titleLabel = CreateLabel(panelRect, "MissionTitle", new Vector2(0.05f, 0.9f), new Vector2(0.95f, 0.98f), TextAlignmentOptions.Center, 30.0f);
        titleLabel.text = "미션";

        _orderPanel = CreateOrderPanel(panelRect);
        _movesLabel = CreateLabel(panelRect, "MovesLabel", new Vector2(0.05f, 0.02f), new Vector2(0.95f, 0.12f), TextAlignmentOptions.Center, 32.0f);
        _swatchPool = new PoolManager<Image>(() => CreateSwatch(_orderPanel, SwatchSize));

        movesChangedChannel.OnRaised += UpdateMoves;
        orderProgressChannel.OnRaised += UpdateOrderProgress;
    }

    // 레벨마다 재료 아이콘이 달라지므로 SetupLevel 시점에 한 번 호출한다 - OrderProgressChannel이
    // 실어 나르는 값은 타입 인덱스/수집 개수뿐이라, 그걸 그림으로 바꾸려면 이 정보가 필요하다.
    public void SetIngredientSprites(Sprite[] ingredientSprites)
    {
        _ingredientSprites = ingredientSprites;
    }

    public void SetMovesVisible(bool visible)
    {
        _movesLabel.gameObject.SetActive(visible);
    }

    // 각 요구사항의 남은 스와치를 왼쪽에서 오른쪽, 위에서 아래로 그리드 형태로 렌더링한다(한 행의
    // 너비가 다 차면 다음 줄로 넘어감) - 이 패널은 HUD 바처럼 넓고 낮은 게 아니라 좁고 길기 때문에,
    // 한 줄(PuzzleHud의 원래 레이아웃)로는 큰 주문을 담을 수 없다.
    private void UpdateOrderProgress(IReadOnlyList<OrderProgressEntry> progress)
    {
        Sprite[] ingredientSprites = _ingredientSprites;
        _swatchPool.ResetRent();

        const float swatchSpacing = 8.0f;
        const float groupSpacing = 20.0f;
        const float rowHeight = 56.0f;

        float panelWidth = _orderPanel.rect.width;
        float x = SwatchSize / 2.0f;
        float y = -SwatchSize / 2.0f;

        for (int i = 0; i < progress.Count; i++)
        {
            OrderProgressEntry entry = progress[i];
            int remaining = Mathf.Max(0, entry.Required - entry.Collected);
            if (remaining == 0)
            {
                continue;
            }

            Sprite sprite = entry.TypeIndex >= 0 && entry.TypeIndex < ingredientSprites.Length ? ingredientSprites[entry.TypeIndex] : null;

            for (int s = 0; s < remaining; s++)
            {
                if (x + SwatchSize / 2.0f > panelWidth)
                {
                    x = SwatchSize / 2.0f;
                    y -= rowHeight;
                }

                RentSwatch(sprite, new Vector2(x, y));
                x += SwatchSize + swatchSpacing;
            }

            x += groupSpacing - swatchSpacing;
        }

        for (int i = _swatchPool.RentedCount; i < _swatchPool.All.Count; i++)
        {
            _swatchPool.All[i].gameObject.SetActive(false);
        }
    }

    private void UpdateMoves(int movesRemaining)
    {
        _movesLabel.text = $"남은 이동: {movesRemaining}";
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

    private static RectTransform CreateOrderPanel(Transform parent)
    {
        GameObject panelObject = new GameObject("OrderPanel", typeof(RectTransform));
        panelObject.transform.SetParent(parent, false);

        RectTransform rect = panelObject.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.05f, 0.15f);
        rect.anchorMax = new Vector2(0.95f, 0.88f);
        rect.pivot = new Vector2(0.0f, 1.0f);
        rect.anchoredPosition = Vector2.zero;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        return rect;
    }

    // PoolManager<Image>에 위임 - 캐스케이드 스텝마다 호출되므로(OrderProgressChannel이 스텝마다
    // Raise됨), 매번 Destroy/Instantiate하는 대신 기존 스와치를 재사용하고 모자랄 때만 새로 만든다.
    private Image RentSwatch(Sprite sprite, Vector2 anchoredPosition)
    {
        Image image = _swatchPool.Rent();
        image.gameObject.SetActive(true);

        RectTransform rect = image.rectTransform;
        rect.anchoredPosition = anchoredPosition;

        if (sprite != null)
        {
            image.sprite = sprite;
            image.color = Color.white;
            image.preserveAspect = true;
        }
        else
        {
            // 이 재료에 대해 설정된 아이콘이 없다 - 완전히 사라지는 대신 스와치가 계속 보이도록
            // 밋밋한 중립색 사각형으로 대체한다.
            image.sprite = null;
            image.preserveAspect = false;
            image.color = new Color(1.0f, 1.0f, 1.0f, 0.4f);
        }

        return image;
    }

    private static Image CreateSwatch(Transform parent, float size)
    {
        GameObject swatchObject = new GameObject("Swatch", typeof(RectTransform), typeof(Image));
        swatchObject.transform.SetParent(parent, false);

        RectTransform rect = swatchObject.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.0f, 1.0f);
        rect.anchorMax = new Vector2(0.0f, 1.0f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = new Vector2(size, size);

        return swatchObject.GetComponent<Image>();
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
