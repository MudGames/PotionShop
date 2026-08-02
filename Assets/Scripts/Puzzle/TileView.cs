using System;
using System.Collections;
using Puzzle.Core;
using UnityEngine;
using UnityEngine.UI;

// 절차적으로 생성된 타일 GameObject 하나(RectTransform + Image + Button)를 감싼다. 재사용/갱신이
// 가능해서, TileViewPool이 레벨을 (재)구성할 때마다 Destroy/Instantiate를 반복하는 대신 인스턴스를
// 재활용할 수 있다.
public sealed class TileView
{
    private static int _instanceCounter;

    // 더 이상 재료별 색상 팔레트는 없다 - 채워진 타일은 모두 같은 중립적인 "슬롯" 배경을 보여주고,
    // 재료 스프라이트만으로 타일의 정체성을 나타낸다.
    private static readonly Color TileSlotColor = new Color(0.16f, 0.14f, 0.18f, 1.0f);

    // 특수 타일 배지(RefreshSpecialEdges 참고, Match3Controller.lineBombSprite/colorBombSprite/
    // radiusBombSprite) - 재료 아이콘을 완전히 덮어 대체한다. 물약 스프라이트는 정사각형 캔버스
    // 중앙에 그려져 있으므로, 일반 재료 아이콘과 동일한 크기/위치로 표시한다 - SetSize 참고.

    private readonly Image _image;
    private readonly Image _iconImage;
    private readonly Image _specialBadge;
    private readonly Sprite _lineBombSprite;
    private readonly Sprite _colorBombSprite;
    private readonly Sprite _radiusBombSprite;
    private readonly Image _selectionOverlay;
    private readonly float _selectionScale;
    private readonly Vector2 _selectionOffset;
    private readonly Button _button;
    private float _size;

    public RectTransform RectTransform { get; }
    public GameObject GameObject => RectTransform.gameObject;
    public GridCell Cell { get; private set; }

    public event Action<TileView> Clicked;

    public TileView(Transform parent, Sprite selectionSprite, float selectionScale, Vector2 selectionOffset, Sprite lineBombSprite, Sprite colorBombSprite, Sprite radiusBombSprite)
    {
        _selectionScale = selectionScale;
        _selectionOffset = selectionOffset;
        _lineBombSprite = lineBombSprite;
        _colorBombSprite = colorBombSprite;
        _radiusBombSprite = radiusBombSprite;

        GameObject tileObject = new GameObject($"Tile_{_instanceCounter++}", typeof(RectTransform), typeof(Image), typeof(Button));
        tileObject.transform.SetParent(parent, false);

        RectTransform = tileObject.GetComponent<RectTransform>();
        RectTransform.anchorMin = new Vector2(0.5f, 0.5f);
        RectTransform.anchorMax = new Vector2(0.5f, 0.5f);
        RectTransform.pivot = new Vector2(0.5f, 0.5f);

        _image = tileObject.GetComponent<Image>();

        _button = tileObject.GetComponent<Button>();
        _button.onClick.AddListener(() => Clicked?.Invoke(this));

        // 아이콘보다 먼저 생성되어 아이콘 뒤에 렌더링된다 - 선택 링은 투명 원반이 아니라 불투명한
        // 원반이라서, 그렇지 않으면 감싸려는 아이콘 자체를 가려버릴 것이다. 아이콘보다 크기 때문에
        // (SetSize 참고), 황금색 테두리는 여전히 가장자리 밖으로 삐져나와 보인다.
        _selectionOverlay = CreateSelectionOverlay(RectTransform, selectionSprite);
        _iconImage = CreateIconImage(RectTransform);

        // 선택 링과 달리, 특수 타일 배지는 아이콘 앞에 렌더링되어 완전히 덮어버린다 - 특수 타일은
        // 타일의 재료 모습과 얌전히 공존하는 게 아니라, 물약(라인/컬러/레이디우스 폭탄)으로 시각적
        // 대체하려는 의도다(RefreshSpecialEdges 참고). 스프라이트는 종류마다 달라지므로 생성 시점엔
        // 비워두고 RefreshSpecialEdges에서 매번 지정한다.
        _specialBadge = CreateSpecialBadge(RectTransform);
    }

    // 아래 깔린 색상 사각형은 여전히 한눈에 종류를 구분할 수 있도록 타입의 색을 담고 있으며,
    // 이 스프라이트는 그 위에 실제 재료 그림을 렌더링한다(LevelData.ingredients의 IngredientData.sprite 참고).
    // 어떤 타입에 스프라이트가 설정되지 않은 레벨은 그냥 이걸 숨긴 채로 두어, 밋밋한 색상
    // 사각형 모습으로 돌아간다.
    private static Image CreateIconImage(Transform parent)
    {
        GameObject iconObject = new GameObject("Icon", typeof(RectTransform), typeof(Image));
        iconObject.transform.SetParent(parent, false);

        RectTransform rect = iconObject.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);

        Image image = iconObject.GetComponent<Image>();
        image.raycastTarget = false;
        image.preserveAspect = true;

        iconObject.SetActive(false);
        return image;
    }

    // 특수 타일 배지 - 물약 아이콘(Match3Controller.lineBombSprite/colorBombSprite/radiusBombSprite
    // 참고)으로 재료 아이콘을 완전히 덮어 대체한다. 스프라이트는 종류마다 달라지므로 생성 시점엔
    // 비워두고 RefreshSpecialEdges에서 매번 지정한다.
    private static Image CreateSpecialBadge(Transform parent)
    {
        GameObject badgeObject = new GameObject("SpecialBadge", typeof(RectTransform), typeof(Image));
        badgeObject.transform.SetParent(parent, false);

        RectTransform rect = badgeObject.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);

        Image image = badgeObject.GetComponent<Image>();
        image.raycastTarget = false;
        image.preserveAspect = true;

        badgeObject.SetActive(false);
        return image;
    }

    // 첫 탭 선택 링 - 타일 자체보다 약간 큰 UI 스프라이트(장식적인 원형 테두리)로, 이 타일이
    // 플레이어의 대기 중인 첫 번째 선택일 때만 표시된다(TileController.OnTileClicked ->
    // BoardView.SetHighlight 참고).
    private static Image CreateSelectionOverlay(Transform parent, Sprite selectionSprite)
    {
        GameObject overlayObject = new GameObject("SelectionOverlay", typeof(RectTransform), typeof(Image));
        overlayObject.transform.SetParent(parent, false);

        RectTransform rect = overlayObject.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);

        Image image = overlayObject.GetComponent<Image>();
        image.sprite = selectionSprite;
        image.raycastTarget = false;
        image.preserveAspect = true;

        overlayObject.SetActive(false);
        return image;
    }

    public void SetCell(GridCell cell)
    {
        Cell = cell;
    }

    public void SetSize(float size)
    {
        _size = size;
        RectTransform.sizeDelta = new Vector2(size, size);
        _selectionOverlay.rectTransform.sizeDelta = new Vector2(size * _selectionScale, size * _selectionScale);
        _selectionOverlay.rectTransform.anchoredPosition = _selectionOffset;

        float specialBadgeSize = size * 0.8f;
        _specialBadge.rectTransform.sizeDelta = new Vector2(specialBadgeSize, specialBadgeSize);
        _specialBadge.rectTransform.anchoredPosition = Vector2.zero;
    }

    public void SetPosition(Vector2 anchoredPosition)
    {
        RectTransform.anchoredPosition = anchoredPosition;
    }

    public void SetHighlight(bool isHighlighted)
    {
        _selectionOverlay.gameObject.SetActive(isHighlighted);
    }

    public void Refresh(TileState state, Sprite[] sprites)
    {
        if (!state.IsFilled)
        {
            _image.color = new Color(0f, 0f, 0f, 0f);
            _iconImage.gameObject.SetActive(false);
            _specialBadge.gameObject.SetActive(false);
            return;
        }

        _image.color = TileSlotColor;
        RefreshIcon(state.TypeIndex, sprites);
        RefreshSpecialEdges(state.Special);
    }

    private void RefreshIcon(int typeIndex, Sprite[] sprites)
    {
        Sprite sprite = sprites != null && typeIndex >= 0 && typeIndex < sprites.Length ? sprites[typeIndex] : null;
        if (sprite == null)
        {
            _iconImage.gameObject.SetActive(false);
            return;
        }

        _iconImage.gameObject.SetActive(true);
        _iconImage.color = Color.white;
        _iconImage.rectTransform.sizeDelta = new Vector2(_size * 0.8f, _size * 0.8f);
        _iconImage.sprite = sprite;
    }

    // 특수 타일은 재료 아이콘을 완전히 덮는 물약 배지로 표시한다: 라인 폭탄(행/열 공용)은 빨간
    // 물약, 컬러 폭탄은 초록 물약, 레이디우스 폭탄은 파란 물약 - 세 종류가 서로 다른 색이라 헷갈릴
    // 일이 없다. LineRow/LineColumn은 배지를 공유하므로 활성화 전에는 행/열 중 어느 쪽인지 배지만
    // 보고는 알 수 없다(의도된 단순화 - Docs/feature-spec/12-special-tiles.md 참고).
    private void RefreshSpecialEdges(SpecialKind special)
    {
        Sprite badgeSprite = special switch
        {
            SpecialKind.LineRow => _lineBombSprite,
            SpecialKind.LineColumn => _lineBombSprite,
            SpecialKind.ColorBomb => _colorBombSprite,
            SpecialKind.RadiusBomb => _radiusBombSprite,
            _ => null
        };

        if (badgeSprite == null)
        {
            _specialBadge.gameObject.SetActive(false);
            return;
        }

        _specialBadge.sprite = badgeSprite;
        _specialBadge.gameObject.SetActive(true);
        _iconImage.gameObject.SetActive(false); // 폭탄 배지는 아이콘을 그저 강조하는 게 아니라 완전히 대체한다
    }

    public void SetActive(bool active)
    {
        GameObject.SetActive(active);
    }

    // duration 동안 targetPosition을 향해 이징 이동하며(ease-out quadratic), 끝나면 정확히 그
    // 위치로 스냅한다. duration이 0 이하면 즉시 스냅한다.
    public IEnumerator AnimateMoveTo(Vector2 targetPosition, float duration)
    {
        Vector2 start = RectTransform.anchoredPosition;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float eased = 1f - (1f - t) * (1f - t);
            RectTransform.anchoredPosition = Vector2.LerpUnclamped(start, targetPosition, eased);
            yield return null;
        }

        RectTransform.anchoredPosition = targetPosition;
    }

    // 타일을 페이드아웃시킨 다음 빈 상태의 비주얼로 지운다. duration이 0 이하면 즉시 스냅한다.
    public IEnumerator AnimateFadeOut(float duration)
    {
        Color start = _image.color;
        Color iconStart = _iconImage.color;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            _image.color = new Color(start.r, start.g, start.b, Mathf.Lerp(start.a, 0f, t));
            _iconImage.color = new Color(iconStart.r, iconStart.g, iconStart.b, Mathf.Lerp(iconStart.a, 0f, t));
            yield return null;
        }

        _image.color = new Color(0f, 0f, 0f, 0f);
        _iconImage.gameObject.SetActive(false);
        _specialBadge.gameObject.SetActive(false);
    }
}
