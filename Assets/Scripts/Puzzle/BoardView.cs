using System;
using System.Collections;
using System.Collections.Generic;
using Puzzle.Core;
using UnityEngine;
using UnityEngine.UI;

// 퍼즐의 타일 그리드를 담당한다: 셀 레이아웃 계산, 풀링된 TileView들, 그리고 Board의 상태를 그 위에
// 렌더링/애니메이션하는 것까지. 게임 규칙이나 입력 의미는 전혀 모른다 - 그저 "셀 크기가 얼마인지",
// "각 셀이 지금 어떻게 보여야 하는지", "변경 사항을 어떻게 애니메이션할지"만 알 뿐이다. TileClicked/
// TileSwiped를 발생시켜서 TileController가 원시 탭/드래그를 스왑 요청으로 변환할 수 있게 하는데,
// 이때 BoardView는 "스왑"이 무엇인지조차 알 필요가 없다.
public sealed class BoardView
{
    private readonly Transform _container;
    private readonly float _cellSpacingRatio;
    private readonly TileViewPool _pool;

    // 채워진 칸이 보여주는 중립적인 "슬롯" 배경색 - 재료별 색상 팔레트는 없고, 재료 스프라이트만
    // 으로 타일의 정체성을 나타낸다. 칸의 고정 배경(CreateBackgroundGrid)에서만 쓰인다 - 말(아이콘/
    // 배지, TileView)은 더 이상 배경을 그리지 않는다(아래 _backgroundRoot 참고).
    private static readonly Color TileBackgroundColor = new Color(0.16f, 0.14f, 0.18f, 1.0f);

    private TileView[,] _views;
    private Sprite[] _sprites;
    private int _rows;
    private int _columns;
    private float _cellSize;
    private float _cellSpacing;

    // 칸 배경(빈 슬롯 사각형) 고정 레이어 - 절대 움직이지 않는다. TileView(말)는 스왑/캐스케이드
    // 때마다 다른 칸으로 재배정되며 이리저리 움직이지만, 배경은 오직 칸 자체에 속해서 한 번
    // 배치되면 Build()가 다시 불릴 때까지 그대로 있는다(2026-08-04 - "타일의 배경은 고정이어야
    // 한다"는 피드백. 이전에는 배경(TileView._image)이 말과 같은 RectTransform에 있어서 드래그
    // 미리보기/스왑/캐스케이드 애니메이션 중 배경까지 함께 움직여 보였음).
    private Transform _backgroundRoot;
    private Image[,] _backgroundSlots;

    // 드래그로 미리 밀어둔 인접 칸(스왑 미리보기 대상)과, 그 칸의 원래 위치 - 방향이 바뀌거나
    // 드래그가 취소/커밋되면 정리된다(OnViewDragPreview/OnViewDragCanceled/OnViewDragged 참고).
    private TileView _previewNeighbor;
    private Vector2 _previewNeighborRestPosition;

    private bool _inputEnabled = true;

    // 드래그 미리보기가 한 칸만큼(progress>=1) 완전히 밀렸을 때, 그 스왑이 실제로 성사될지(매치가
    // 만들어지거나 스페셜을 활성화하는지)를 물어보는 콜백 - Match3Controller가 GridController 생성
    // 후 지연 연결한다(TileController 생성자의 isSpecialTile/isBlockedTile predicate와 같은 패턴).
    // null이면(아직 레벨이 준비되지 않은 등) 강제 드롭을 하지 않는다.
    public Func<GridCell, GridCell, bool> CanAcceptSwap { get; set; }

    // TileController.Enabled와 함께 맞춰 켜고 끈다(Match3Controller 참고) - 캐스케이드 애니메이션
    // 재생 중에는 드래그 미리보기 자체를 시작하지 않게 해서, 애니메이션 도중 시작된 드래그가
    // TileController에 조용히 무시된 채 미리보기 위치에 어긋나 남는 것을 막는다.
    public bool InputEnabled
    {
        get => _inputEnabled;
        set
        {
            _inputEnabled = value;
            if (_views == null)
            {
                return;
            }

            for (int row = 0; row < _rows; row++)
            {
                for (int col = 0; col < _columns; col++)
                {
                    _views[row, col].DragEnabled = value;
                }
            }
        }
    }

    public event Action<GridCell> TileClicked;

    // 드래그로 인접 칸 스왑이 요청됐을 때 - TileView.Dragged가 준 방향 오프셋으로 계산한 목적지가
    // 보드 범위를 벗어나면(가장자리 칸에서 바깥쪽으로 드래그한 경우) 발생시키지 않는다.
    public event Action<GridCell, GridCell> TileSwiped;

    public BoardView(Transform container, float cellSpacingRatio, Sprite rowBombSprite, Sprite columnBombSprite, Sprite radiusBombSprite)
    {
        _container = container;
        _cellSpacingRatio = cellSpacingRatio;
        _pool = new TileViewPool(container, rowBombSprite, columnBombSprite, radiusBombSprite);
    }

    public void Build(int rows, int columns, Board board, Sprite[] sprites)
    {
        _rows = rows;
        _columns = columns;
        _sprites = sprites;

        _pool.ReturnAll();
        _views = new TileView[rows, columns];

        CalculateTileLayout();
        CreateBackgroundGrid();

        for (int row = 0; row < rows; row++)
        {
            for (int col = 0; col < columns; col++)
            {
                TileView view = _pool.Rent();
                view.SetCell(new GridCell(row, col));
                view.SetSize(_cellSize);
                view.SetPosition(GetTilePosition(row, col));
                view.DragEnabled = _inputEnabled;
                view.Clicked -= OnViewClicked;
                view.Clicked += OnViewClicked;
                view.Dragged -= OnViewDragged;
                view.Dragged += OnViewDragged;
                view.DragPreview -= OnViewDragPreview;
                view.DragPreview += OnViewDragPreview;
                view.DragCanceled -= OnViewDragCanceled;
                view.DragCanceled += OnViewDragCanceled;
                _views[row, col] = view;
            }
        }

        RefreshAll(board);
    }

    public void RefreshAll(Board board)
    {
        for (int row = 0; row < _rows; row++)
        {
            for (int col = 0; col < _columns; col++)
            {
                _views[row, col].Refresh(board.Get(row, col), _sprites);
            }
        }
    }

    public void Refresh(GridCell cell, TileState state)
    {
        _views[cell.Row, cell.Col].Refresh(state, _sprites);
    }

    public void RefreshSpawnedSpecials(IReadOnlyList<(GridCell Cell, SpecialKind Kind)> spawned, Board boardSnapshot)
    {
        foreach ((GridCell cell, SpecialKind _) in spawned)
        {
            Refresh(cell, boardSnapshot.Get(cell));
        }
    }

    // 이 캐스케이드 스텝에서 실제로 바뀐 셀(제거됨/이동해 온 도착지/새로 채워짐)만 최종 상태로
    // 맞춘다 - 이전에는 매 스텝마다 RefreshAll로 보드 전체(rows*columns칸)를 다시 그렸으나, 위
    // AnimateClear/AnimateGravity가 이미 이 셀들의 최종 모습을 정확히 반영하므로 안전망도 실제로
    // 바뀐 위치만 확인하면 충분하다(2026-08-04, 캐스케이드가 잦은 매치3 특성상 매 스텝 보드 전체를
    // 훑는 건 불필요한 낭비였음).
    public void RefreshChangedCells(CascadeStepInfo step)
    {
        Board board = step.BoardSnapshot;

        foreach (GridCell cell in step.ClearedCells)
        {
            Refresh(cell, board.Get(cell));
        }

        foreach ((GridCell From, GridCell To) move in step.Moves)
        {
            Refresh(move.To, board.Get(move.To));
        }

        foreach ((GridCell Cell, int TypeIndex) fill in step.Fills)
        {
            Refresh(fill.Cell, board.Get(fill.Cell));
        }
    }

    public Vector2 GetCellPosition(GridCell cell)
    {
        return GetTilePosition(cell.Row, cell.Col);
    }

    // 컨테이너의 실제 크기(창 리사이즈, 화면 회전 등)가 바뀐 뒤 호출된다 - 셀 크기는 Build() 때
    // 딱 한 번만 계산해두므로, 그대로는 리사이즈에 반응하지 않는다. 보드가 아직 만들어지기 전이면
    // 조용히 무시한다(Match3Controller.OnRectTransformDimensionsChange 참고).
    public void RefreshLayout()
    {
        if (_views == null)
        {
            return;
        }

        CalculateTileLayout();

        for (int row = 0; row < _rows; row++)
        {
            for (int col = 0; col < _columns; col++)
            {
                TileView view = _views[row, col];
                view.SetSize(_cellSize);
                view.SetPosition(GetTilePosition(row, col));

                RectTransform backgroundRect = _backgroundSlots[row, col].rectTransform;
                backgroundRect.sizeDelta = new Vector2(_cellSize, _cellSize);
                backgroundRect.anchoredPosition = GetTilePosition(row, col);
            }
        }
    }

    // 칸 배경(빈 슬롯) 고정 레이어를 새로 만든다 - Build()가 다시 호출될 때마다(레벨/스테이지가
    // 바뀌어 그리드 크기가 달라질 수 있으므로) 이전 배경을 통째로 지우고 다시 그린다. TileView(말)
    // 들보다 먼저 만들어 SetAsFirstSibling으로 맨 뒤에 렌더링되게 한다.
    private void CreateBackgroundGrid()
    {
        if (_backgroundRoot != null)
        {
            UnityEngine.Object.Destroy(_backgroundRoot.gameObject);
        }

        GameObject rootObject = new GameObject("TileBackgrounds", typeof(RectTransform));
        rootObject.transform.SetParent(_container, false);
        rootObject.transform.SetAsFirstSibling();
        _backgroundRoot = rootObject.transform;

        _backgroundSlots = new Image[_rows, _columns];

        for (int row = 0; row < _rows; row++)
        {
            for (int col = 0; col < _columns; col++)
            {
                GameObject slotObject = new GameObject($"Slot_{row}_{col}", typeof(RectTransform), typeof(Image));
                slotObject.transform.SetParent(_backgroundRoot, false);

                RectTransform rect = slotObject.GetComponent<RectTransform>();
                rect.anchorMin = new Vector2(0.5f, 0.5f);
                rect.anchorMax = new Vector2(0.5f, 0.5f);
                rect.pivot = new Vector2(0.5f, 0.5f);
                rect.sizeDelta = new Vector2(_cellSize, _cellSize);
                rect.anchoredPosition = GetTilePosition(row, col);

                Image image = slotObject.GetComponent<Image>();
                image.color = TileBackgroundColor;
                image.raycastTarget = false;

                _backgroundSlots[row, col] = image;
            }
        }
    }

    // 두 타일을 서로의 위치로 슬라이드시킨다. commit이 true이면(스왑이 매치를 만들었거나 스페셜을
    // 활성화한 경우), 내부 TileView *슬롯 참조*도 함께 교체된다 - 즉 슬롯 a는 그 자리로 슬라이드해 온
    // view가 무엇이든 영구적으로 그 view가 되며, 각 view 자신의 Cell도 그에 맞게 갱신된다. 그렇게 하지
    // 않으면 지금 슬롯 a에 있는 타일을 클릭해도 여전히 슬롯 b로 보고될 것이다. commit이 false이면
    // 대신 원래 있던 위치로 다시 슬라이드해 돌아간다.
    public IEnumerator AnimateSwapAttempt(GridCell a, GridCell b, float duration, bool commit)
    {
        TileView viewA = _views[a.Row, a.Col];
        TileView viewB = _views[b.Row, b.Col];
        Vector2 posA = GetTilePosition(a.Row, a.Col);
        Vector2 posB = GetTilePosition(b.Row, b.Col);

        yield return RunParallel(viewA.AnimateMoveTo(posB, duration), viewB.AnimateMoveTo(posA, duration));

        if (commit)
        {
            viewA.SetCell(b);
            viewB.SetCell(a);
            _views[a.Row, a.Col] = viewB;
            _views[b.Row, b.Col] = viewA;
            yield break;
        }

        yield return RunParallel(viewA.AnimateMoveTo(posA, duration), viewB.AnimateMoveTo(posB, duration));
    }

    // 제거된 모든 셀을 동시에 페이드아웃시킨 다음, 빈 상태의 비주얼로 즉시 전환한다.
    public IEnumerator AnimateClear(IReadOnlyList<GridCell> clearedCells, float duration, float popScale)
    {
        List<IEnumerator> routines = new List<IEnumerator>(clearedCells.Count);
        foreach (GridCell cell in clearedCells)
        {
            routines.Add(_views[cell.Row, cell.Col].AnimateFadeOut(duration, popScale));
        }

        yield return RunAllParallel(routines);
    }

    // 중력을 애니메이션한다: Move의 목적지 view는 원래 셀 위치에서 슬라이드해 들어오고, Fill의
    // view는 자신의 최종 위치 바로 위에서 떨어져 내려온다. 둘 다 이 스텝 전체에 걸쳐 동시에 실행된다.
    // 내용은 이 스텝의 최종(BoardSnapshot) 값으로 미리 설정해 두므로, 애니메이션은 순전히 위치
    // 이동일 뿐이다 - 슬라이드 도중 각 view가 보여주는 내용이 바뀌는 일은 없다.
    public IEnumerator AnimateGravity(
        IReadOnlyList<(GridCell From, GridCell To)> moves,
        IReadOnlyList<(GridCell Cell, int TypeIndex)> fills,
        Board boardSnapshot,
        float duration)
    {
        List<IEnumerator> routines = new List<IEnumerator>(moves.Count + fills.Count);

        foreach ((GridCell from, GridCell to) in moves)
        {
            TileView view = _views[to.Row, to.Col];
            Vector2 restingPosition = GetTilePosition(to.Row, to.Col);
            view.Refresh(boardSnapshot.Get(to), _sprites);
            view.SetPosition(GetTilePosition(from.Row, from.Col));
            routines.Add(view.AnimateMoveTo(restingPosition, duration));
        }

        foreach ((GridCell cell, int _) in fills)
        {
            TileView view = _views[cell.Row, cell.Col];
            Vector2 restingPosition = GetTilePosition(cell.Row, cell.Col);
            view.Refresh(boardSnapshot.Get(cell), _sprites);
            view.SetPosition(new Vector2(restingPosition.x, restingPosition.y + _cellSize * 1.5f));
            routines.Add(view.AnimateMoveTo(restingPosition, duration));
        }

        yield return RunAllParallel(routines);
    }

    private void CalculateTileLayout()
    {
        RectTransform containerRect = _container as RectTransform;
        float availableWidth = containerRect.rect.width;
        float availableHeight = containerRect.rect.height;

        float widthBasedSize = availableWidth / (_columns + (_columns - 1) * _cellSpacingRatio);
        float heightBasedSize = availableHeight / (_rows + (_rows - 1) * _cellSpacingRatio);

        _cellSize = Mathf.Min(widthBasedSize, heightBasedSize);
        _cellSpacing = _cellSize * _cellSpacingRatio;
    }

    private Vector2 GetTilePosition(int row, int col)
    {
        float totalWidth = _columns * _cellSize + (_columns - 1) * _cellSpacing;
        float totalHeight = _rows * _cellSize + (_rows - 1) * _cellSpacing;
        float startX = -totalWidth / 2.0f + _cellSize / 2.0f;
        float startY = totalHeight / 2.0f - _cellSize / 2.0f;

        return new Vector2(
            startX + col * (_cellSize + _cellSpacing),
            startY - row * (_cellSize + _cellSpacing));
    }

    private void OnViewClicked(TileView view)
    {
        TileClicked?.Invoke(view.Cell);
    }

    private void OnViewDragged(TileView view, int dRow, int dCol)
    {
        // 이 시점부터는(성사되든 거부되든) AnimateSwapAttempt가 두 타일의 위치를 이어받아
        // 애니메이션한다 - 미리보기로 이미 옮겨둔 위치에서 자연스럽게 이어지므로 여기서 되돌릴
        // 필요가 없다. 추적 상태만 정리한다.
        _previewNeighbor = null;

        GridCell from = view.Cell;
        GridCell to = new GridCell(from.Row + dRow, from.Col + dCol);

        if (to.Row < 0 || to.Row >= _rows || to.Col < 0 || to.Col >= _columns)
        {
            // 보드 밖으로는 스왑할 수 없으므로 아무도 이어받지 않는다 - 미리보기로 옮겨간 자신을
            // 직접 되돌린다.
            view.SetPosition(GetTilePosition(from.Row, from.Col));
            return;
        }

        TileSwiped?.Invoke(from, to);
    }

    // 드래그 중 매 프레임 호출된다 - 드래그된 타일이 향하는 인접 칸을 반대 방향으로 같은 만큼 밀어,
    // 두 타일이 서로 자리를 바꾸는 도중처럼 보이게 한다("스왑 미리보기"). 드래그 도중 방향이
    // 바뀌거나 보드 경계를 벗어나면 이전에 밀어뒀던 이웃을 제자리로 되돌린다. 미리보기가 한 칸만큼
    // (progress>=1) 완전히 밀렸는데 그 스왑이 매치를 만들지 못한다면, 손을 놓기도 전에 강제로
    // 드롭시킨다(2026-08-04 - 매칭도 안 되는데 스왑된 것처럼 보이는 상태로 무한정 붙잡고 있을 수
    // 있었던 문제).
    private void OnViewDragPreview(TileView view, int dRow, int dCol, float progress)
    {
        GridCell from = view.Cell;
        GridCell to = new GridCell(from.Row + dRow, from.Col + dCol);
        bool inBounds = to.Row >= 0 && to.Row < _rows && to.Col >= 0 && to.Col < _columns;
        TileView neighbor = inBounds ? _views[to.Row, to.Col] : null;

        if (_previewNeighbor != null && _previewNeighbor != neighbor)
        {
            _previewNeighbor.SetPosition(_previewNeighborRestPosition);
            _previewNeighbor = null;
        }

        if (neighbor == null)
        {
            return;
        }

        if (_previewNeighbor != neighbor)
        {
            _previewNeighbor = neighbor;
            _previewNeighborRestPosition = GetTilePosition(to.Row, to.Col);
        }

        Vector2 offset = new Vector2(-dCol, dRow) * (progress * _cellSize);
        neighbor.SetPosition(_previewNeighborRestPosition + offset);

        if (progress >= 1f && CanAcceptSwap != null && !CanAcceptSwap(from, to))
        {
            view.ForceDragEnd();
        }
    }

    // 임계값 미만으로 끝나 스왑이 성립되지 않은 드래그 - 드래그된 타일 자신은 TileView.OnDragEnded가
    // 이미 되돌렸으므로, 여기서는 미리 밀어뒀던 이웃만 있으면 제자리로 되돌린다.
    private void OnViewDragCanceled(TileView view)
    {
        if (_previewNeighbor != null)
        {
            _previewNeighbor.SetPosition(_previewNeighborRestPosition);
            _previewNeighbor = null;
        }
    }

    // 두 enumerator를 한 프레임씩 서로 맞춰가며 수동으로 진행시켜, 둘 다 끝날 때까지 반복한다 -
    // 이렇게 하면 여러 TileView 애니메이션을 동시에 실행하면서도 MonoBehaviour가 각각을 따로
    // StartCoroutine으로 돌릴 필요가 없다.
    private static IEnumerator RunParallel(IEnumerator a, IEnumerator b)
    {
        bool aDone = false;
        bool bDone = false;

        while (!aDone || !bDone)
        {
            if (!aDone)
            {
                aDone = !a.MoveNext();
            }

            if (!bDone)
            {
                bDone = !b.MoveNext();
            }

            yield return null;
        }
    }

    private static IEnumerator RunAllParallel(List<IEnumerator> routines)
    {
        if (routines.Count == 0)
        {
            yield break;
        }

        bool anyRunning = true;
        while (anyRunning)
        {
            anyRunning = false;
            for (int i = 0; i < routines.Count; i++)
            {
                if (routines[i].MoveNext())
                {
                    anyRunning = true;
                }
            }

            yield return null;
        }
    }
}
