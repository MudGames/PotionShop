using System;
using System.Collections;
using Puzzle.Core;
using UnityEngine;
using UnityEngine.UI;

// 절차적으로 생성된 타일 GameObject 하나(RectTransform + TileDragInput)를 감싼다. 재사용/갱신이
// 가능해서, TileViewPool이 레벨을 (재)구성할 때마다 Destroy/Instantiate를 반복하는 대신 인스턴스를
// 재활용할 수 있다. 이 GameObject 자체는 스왑/캐스케이드마다 이리저리 움직이는 "말"(재료 아이콘 +
// 스페셜 배지)만 담당한다 - 칸의 "배경(슬롯)"은 절대 움직이지 않는 별도의 고정 레이어로
// BoardView가 그린다(2026-08-04, BoardView.CreateBackgroundGrid 참고 - 배경이 말과 같은
// RectTransform에 있었을 때는 드래그/캐스케이드 애니메이션 중 배경까지 함께 움직여 보였음).
// 루트에 여전히 Image가 하나 있지만 완전히 투명하며, 순전히 EventSystem 레이캐스트 대상 역할만
// 한다(Graphic 컴포넌트가 있어야 IPointerClickHandler 등이 호출된다).
public sealed class TileView
{
    private static int _instanceCounter;

    // 드래그로 인접 칸 스왑을 인정하는 최소 이동 거리 - 셀 크기(_size)에 대한 비율. 너무 작으면
    // 살짝 스친 것도 스왑으로 오인하고, 너무 크면 스왑 의도가 분명한데도 무시된다.
    private const float DragThresholdRatio = 0.3f;

    // 특수 타일 배지(RefreshSpecialEdges 참고, Match3Controller.rowBombSprite/columnBombSprite/
    // radiusBombSprite) - 재료 아이콘을 완전히 덮어 대체한다. 물약 스프라이트는 정사각형 캔버스
    // 중앙에 그려져 있으므로, 일반 재료 아이콘과 동일한 크기/위치로 표시한다 - SetSize 참고.

    private readonly Image _iconImage;
    private readonly Image _specialBadge;
    private readonly Image _specialGlow;
    private readonly Sprite _rowBombSprite;
    private readonly Sprite _columnBombSprite;
    private readonly Sprite _radiusBombSprite;
    private float _size;

    public RectTransform RectTransform { get; }
    public GameObject GameObject => RectTransform.gameObject;
    public GridCell Cell { get; private set; }

    // 캐스케이드 애니메이션 재생 중처럼 입력이 막혀 있어야 할 때 드래그 미리보기 자체를 시작하지
    // 않게 한다(BoardView.InputEnabled 참고) - 이게 없으면 애니메이션 도중 드래그를 시작한 타일이
    // 미리보기 위치로 밀린 채, 정작 TileController가 요청을 조용히 무시해버려 원위치로 돌아오지
    // 못하고 어긋난 채로 남을 수 있다.
    public bool DragEnabled { get; set; } = true;

    public event Action<TileView> Clicked;

    // 이 타일에서 시작된 드래그 제스처 하나가 끝났을 때, 우세한 축/방향으로 판정된 인접 칸 오프셋
    // (dRow, dCol) — 항상 (0,±1) 또는 (±1,0) 중 하나다(대각선 없음). BoardView가 이 오프셋으로
    // 실제 목적지 GridCell을 계산한다(이 타일은 자신의 셀 크기만 알 뿐, 보드 경계는 모른다).
    public event Action<TileView, int, int> Dragged;

    // 드래그 도중 매 프레임 발생 - 우세한 축/방향(dRow, dCol)과 그 방향으로 얼마나 진행됐는지를
    // 셀 크기에 대한 비율(0~1)로 알려준다. BoardView가 이걸로 실제 인접 칸을 반대 방향으로 같은
    // 만큼 밀어, 두 타일이 서로 자리를 바꾸는 도중처럼 보이는 미리보기 연출을 한다.
    public event Action<TileView, int, int, float> DragPreview;

    // 임계값 미만으로 드래그가 끝나 스왑이 성립되지 않았을 때 발생 - BoardView가 미리 밀어뒀던
    // 이웃 타일을 제자리로 되돌릴 수 있게 한다. 이 타일 자신의 위치는 이미 되돌린 뒤 발생시킨다.
    public event Action<TileView> DragCanceled;

    private Vector2 _dragRestPosition;

    public TileView(Transform parent, Sprite rowBombSprite, Sprite columnBombSprite, Sprite radiusBombSprite, Sprite specialGlowSprite)
    {
        _rowBombSprite = rowBombSprite;
        _columnBombSprite = columnBombSprite;
        _radiusBombSprite = radiusBombSprite;

        GameObject tileObject = new GameObject($"Tile_{_instanceCounter++}", typeof(RectTransform), typeof(Image));
        tileObject.transform.SetParent(parent, false);

        RectTransform = tileObject.GetComponent<RectTransform>();
        RectTransform.anchorMin = new Vector2(0.5f, 0.5f);
        RectTransform.anchorMax = new Vector2(0.5f, 0.5f);
        RectTransform.pivot = new Vector2(0.5f, 0.5f);

        // 배경 없이 레이캐스트 대상 역할만 하므로 완전히 투명하게 둔다 - 실제 배경은
        // BoardView.CreateBackgroundGrid가 그리는 고정 레이어가 담당한다.
        Image raycastTarget = tileObject.GetComponent<Image>();
        raycastTarget.color = new Color(0f, 0f, 0f, 0f);

        TileDragInput dragInput = tileObject.AddComponent<TileDragInput>();
        dragInput.Tapped += () => Clicked?.Invoke(this);
        dragInput.DragStarted += OnDragStarted;
        dragInput.Dragging += OnDragging;
        dragInput.DragEnded += OnDragEnded;

        _iconImage = CreateIconImage(RectTransform);

        // 후광은 배지보다 먼저 생성해 형제 순서상 배지 "뒤"에 렌더링되게 한다(SpecialBadgePulse가
        // 여기 붙어 반짝인다 - CreateSpecialGlow 참고).
        _specialGlow = CreateSpecialGlow(RectTransform, specialGlowSprite);

        // 특수 타일 배지는 아이콘 앞에 렌더링되어 완전히 덮어버린다 - 특수 타일은
        // 타일의 재료 모습과 얌전히 공존하는 게 아니라, 물약(라인/컬러/레이디우스 폭탄)으로 시각적
        // 대체하려는 의도다(RefreshSpecialEdges 참고). 스프라이트는 종류마다 달라지므로 생성 시점엔
        // 비워두고 RefreshSpecialEdges에서 매번 지정한다.
        _specialBadge = CreateSpecialBadge(RectTransform);

        // 테두리 프레임과 같이 살짝 커지도록 배지 트랜스폼을 넘겨준다(SpecialBadgePulse 참고,
        // 2026-08-05 - "물약도 같이 커지는건 이상할까요?").
        _specialGlow.GetComponent<SpecialBadgePulse>().SetBadgeTransform(_specialBadge.rectTransform);
    }

    private bool _dragPreviewActive;
    private int _lastDragRow;
    private int _lastDragCol;

    private void OnDragStarted()
    {
        _dragPreviewActive = DragEnabled;
        if (!_dragPreviewActive)
        {
            return;
        }

        _dragRestPosition = RectTransform.anchoredPosition;
    }

    // 드래그 중 매 프레임 호출된다 - 시작 지점 기준 로컬 델타의 우세한 축/방향으로만(대각선 없음)
    // 최대 셀 크기 한 칸 분량까지 자신을 미리 이동시켜 "스왑 미리보기"를 보여준다. 실제 스왑 여부는
    // OnDragEnded에서 결정되며, 여기서는 그저 시각적 예고일 뿐이다.
    private void OnDragging(Vector2 delta)
    {
        if (!_dragPreviewActive)
        {
            return;
        }

        int dRow;
        int dCol;
        float progress;
        ComputeDragDirection(delta, out dRow, out dCol, out progress);
        _lastDragRow = dRow;
        _lastDragCol = dCol;

        Vector2 offset = new Vector2(dCol, -dRow) * (progress * _size);
        RectTransform.anchoredPosition = _dragRestPosition + offset;

        DragPreview?.Invoke(this, dRow, dCol, progress);
    }

    // 아직 손을 떼지 않았더라도, 미리보기가 매칭 불가능한 조합으로 완전히(한 칸만큼) 밀렸다면
    // BoardView가 지금 당장 드롭시키기 위해 호출한다(BoardView.OnViewDragPreview 참고) - 놓지도
    // 않았는데 스왑된 것처럼 보이는 상태로 무한정 멈춰 있는 걸 막기 위함이다. 실제로 손을 뗐을 때와
    // 동일하게 Dragged를 발생시켜 이후 처리(성사/거부 애니메이션)를 그대로 태운다.
    public void ForceDragEnd()
    {
        if (!_dragPreviewActive)
        {
            return;
        }

        _dragPreviewActive = false;
        Dragged?.Invoke(this, _lastDragRow, _lastDragCol);
    }

    // 임계값보다 짧은 드래그는 실수로 스친 것으로 보고 미리보기를 취소한다(자신은 제자리로,
    // BoardView는 DragCanceled를 받아 밀어뒀던 이웃을 되돌린다). 임계값을 넘겼다면 Dragged를
    // 발생시켜 실제 스왑을 요청한다 - 이후 위치 갱신은 성사 여부와 무관하게 AnimateSwapAttempt가
    // (지금 미리보기로 이미 옮겨간 위치에서부터 자연스럽게 이어) 담당한다.
    private void OnDragEnded(Vector2 delta)
    {
        if (!_dragPreviewActive)
        {
            return;
        }

        _dragPreviewActive = false;

        int dRow;
        int dCol;
        float progress;
        ComputeDragDirection(delta, out dRow, out dCol, out progress);

        if (progress < DragThresholdRatio)
        {
            RectTransform.anchoredPosition = _dragRestPosition;
            DragCanceled?.Invoke(this);
            return;
        }

        Dragged?.Invoke(this, dRow, dCol);
    }

    // 델타(드래그 시작 지점 기준 로컬 좌표 변위)의 우세한 축을 골라 인접 칸 방향(dRow, dCol, 항상
    // (0,±1) 또는 (±1,0))과 그 축으로 얼마나 이동했는지를 셀 크기에 대한 비율(0~1로 클램프)로
    // 반환한다. OnDragging(진행 중 미리보기)과 OnDragEnded(최종 판정) 양쪽에서 같은 기준을 쓴다.
    private void ComputeDragDirection(Vector2 delta, out int dRow, out int dCol, out float progress)
    {
        if (Mathf.Abs(delta.x) > Mathf.Abs(delta.y))
        {
            dRow = 0;
            dCol = delta.x > 0f ? 1 : -1;
            progress = _size > 0f ? Mathf.Clamp01(Mathf.Abs(delta.x) / _size) : 0f;
        }
        else
        {
            // UI 로컬 좌표는 위쪽이 +Y이므로, 위로 드래그(+Y)하면 화면상 위 칸(행 번호가 작은 쪽)이다.
            dRow = delta.y > 0f ? -1 : 1;
            dCol = 0;
            progress = _size > 0f ? Mathf.Clamp01(Mathf.Abs(delta.y) / _size) : 0f;
        }
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

    // 특수 타일 배지 - 물약 아이콘(Match3Controller.rowBombSprite/columnBombSprite/radiusBombSprite
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

    // 특수 타일 배지 뒤에서 반짝이는 테두리 프레임 - 타일 칸 자체 크기에 맞춘 정사각형 테두리
    // 스프라이트(안쪽/바깥쪽 모두 투명, 테두리 선만 불투명, SpecialGlow.png)를 알파 0(안 보임)에서
    // 시작해 SpecialBadgePulse가 나타났다 사라지는 식으로 반짝이게 한다. 배지 자체의 알파를
    // 낮추는 방식은 뒤가 어두운 고정 배경이라 오히려 어두워 보였고(2026-08-05, "왜 밝은 느낌이
    // 안나나요"), 꽉 찬 방사형 블롭으로 바꿨더니 이번엔 "네모 박스가 빛나는" 것처럼 어색해 보여
    // 채워진 블롭이 아닌 테두리만 빛나는 프레임 방식으로 다시 바꿨다.
    private static Image CreateSpecialGlow(Transform parent, Sprite glowSprite)
    {
        GameObject glowObject = new GameObject("SpecialGlow", typeof(RectTransform), typeof(Image));
        glowObject.transform.SetParent(parent, false);

        RectTransform rect = glowObject.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);

        Image image = glowObject.GetComponent<Image>();
        image.sprite = glowSprite;
        image.raycastTarget = false;
        image.preserveAspect = true;
        image.color = new Color(1f, 1f, 1f, 0f);

        // 반짝임은 배지가 켜지고 꺼질 때 이 오브젝트도 같이 SetActive되면서 자동으로 켜지고
        // 꺼진다(RefreshSpecialEdges 참고) - ButtonHoverAnimator와 같은 패턴의 독립 컴포넌트.
        glowObject.AddComponent<SpecialBadgePulse>();

        glowObject.SetActive(false);
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

        float specialBadgeSize = size * 0.8f;
        _specialBadge.rectTransform.sizeDelta = new Vector2(specialBadgeSize, specialBadgeSize);
        _specialBadge.rectTransform.anchoredPosition = Vector2.zero;

        // 채워진 블롭이 아니라 타일 칸 테두리를 그대로 따라가는 프레임이라, 칸 자체 크기에 맞춘다
        // (2026-08-05, "네모 박스가 빛나니까 어색합니다" 피드백으로 블롭 -> 테두리 프레임으로 교체).
        float glowSize = size * 1.05f;
        _specialGlow.rectTransform.sizeDelta = new Vector2(glowSize, glowSize);
        _specialGlow.rectTransform.anchoredPosition = Vector2.zero;
    }

    public void SetPosition(Vector2 anchoredPosition)
    {
        RectTransform.anchoredPosition = anchoredPosition;
    }

    public void Refresh(TileState state, Sprite[] sprites)
    {
        if (!state.IsFilled)
        {
            _iconImage.gameObject.SetActive(false);
            _specialBadge.gameObject.SetActive(false);
            _specialGlow.gameObject.SetActive(false);
            return;
        }

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

    // 특수 타일은 재료 아이콘을 완전히 덮는 물약 배지로 표시한다: 행 폭탄은 빨간 물약, 열 폭탄은
    // 초록 물약, 레이디우스 폭탄은 파란 물약(2026-08-04, 행/열을 서로 다른 배지로 분리 - 이전에는
    // 배지를 공유해 활성화 전에 행/열을 구분할 수 없었음. 컬러 폭탄은 활성화 시 어떤 재료였는지
    // 알 방법이 없어 제거함 - `Docs/feature-spec/12-special-tiles.md` 참고).
    private void RefreshSpecialEdges(SpecialKind special)
    {
        Sprite badgeSprite = special switch
        {
            SpecialKind.LineRow => _rowBombSprite,
            SpecialKind.LineColumn => _columnBombSprite,
            SpecialKind.RadiusBomb => _radiusBombSprite,
            _ => null
        };

        if (badgeSprite == null)
        {
            _specialBadge.gameObject.SetActive(false);
            _specialGlow.gameObject.SetActive(false);
            return;
        }

        _specialBadge.sprite = badgeSprite;
        _specialBadge.gameObject.SetActive(true);
        _specialGlow.gameObject.SetActive(true);
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
    // 매치로 제거될 때 살짝 부풀었다가(팝) 줄어들며 사라지고, 동시에 무작위 방향으로 살짝
    // 회전하며 사라진다 - 페이드+팝만으로는 밋밋하다는 피드백으로 회전을 추가(2026-08-04).
    // popScale(1.0 -> popScale로 튀어오르는 최대 배율)은 이번 매치/콤보 규모에 따라 호출자
    // (PuzzleEffectController)가 정해 넘겨준다 - 클수록 더 크게 튄다. 회전 각도는 매번 무작위라
    // (순전히 장식용이라 결정론이 필요 없음) 매치마다 조금씩 다른 느낌을 준다. 배경은 이 타일과
    // 분리된 고정 레이어(BoardView.CreateBackgroundGrid)라 함께 커지거나 회전하지 않는다.
    private const float ClearPopPhaseRatio = 0.3f;
    private const float ClearWobbleAngle = 15f;

    public IEnumerator AnimateFadeOut(float duration, float popScale)
    {
        Color iconStart = _iconImage.color;
        float wobbleTarget = UnityEngine.Random.Range(-ClearWobbleAngle, ClearWobbleAngle);
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);

            float scale;
            if (t < ClearPopPhaseRatio)
            {
                float popT = t / ClearPopPhaseRatio;
                scale = Mathf.LerpUnclamped(1f, popScale, 1f - (1f - popT) * (1f - popT));
            }
            else
            {
                float shrinkT = (t - ClearPopPhaseRatio) / (1f - ClearPopPhaseRatio);
                scale = Mathf.LerpUnclamped(popScale, 0f, shrinkT * shrinkT);
            }

            RectTransform.localScale = Vector3.one * scale;
            RectTransform.localRotation = Quaternion.Euler(0f, 0f, Mathf.LerpUnclamped(0f, wobbleTarget, t));
            _iconImage.color = new Color(iconStart.r, iconStart.g, iconStart.b, Mathf.Lerp(iconStart.a, 0f, t));
            yield return null;
        }

        RectTransform.localScale = Vector3.one;
        RectTransform.localRotation = Quaternion.identity;
        _iconImage.gameObject.SetActive(false);
        _specialBadge.gameObject.SetActive(false);
        _specialGlow.gameObject.SetActive(false);
    }
}
