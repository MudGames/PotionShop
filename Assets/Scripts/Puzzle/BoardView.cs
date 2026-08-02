using System;
using System.Collections;
using System.Collections.Generic;
using Puzzle.Core;
using UnityEngine;

// 퍼즐의 타일 그리드를 담당한다: 셀 레이아웃 계산, 풀링된 TileView들, 그리고 Board의 상태를 그 위에
// 렌더링/애니메이션하는 것까지. 게임 규칙이나 입력 의미는 전혀 모른다 - 그저 "셀 크기가 얼마인지",
// "각 셀이 지금 어떻게 보여야 하는지", "변경 사항을 어떻게 애니메이션할지"만 알 뿐이다. TileClicked를
// 발생시켜서 TileController가 원시 클릭을 스왑 요청으로 변환할 수 있게 하는데, 이때 BoardView는
// "스왑"이 무엇인지조차 알 필요가 없다.
public sealed class BoardView
{
    private readonly Transform _container;
    private readonly float _cellSpacingRatio;
    private readonly TileViewPool _pool;

    private TileView[,] _views;
    private Sprite[] _sprites;
    private int _rows;
    private int _columns;
    private float _cellSize;
    private float _cellSpacing;

    public event Action<GridCell> TileClicked;

    public BoardView(Transform container, float cellSpacingRatio, Sprite selectionSprite, float selectionScale, Vector2 selectionOffset, Sprite lineBombSprite, Sprite colorBombSprite, Sprite radiusBombSprite)
    {
        _container = container;
        _cellSpacingRatio = cellSpacingRatio;
        _pool = new TileViewPool(container, selectionSprite, selectionScale, selectionOffset, lineBombSprite, colorBombSprite, radiusBombSprite);
    }

    public void Build(int rows, int columns, Board board, Sprite[] sprites)
    {
        _rows = rows;
        _columns = columns;
        _sprites = sprites;

        _pool.ReturnAll();
        _views = new TileView[rows, columns];

        CalculateTileLayout();

        for (int row = 0; row < rows; row++)
        {
            for (int col = 0; col < columns; col++)
            {
                TileView view = _pool.Rent();
                view.SetCell(new GridCell(row, col));
                view.SetSize(_cellSize);
                view.SetPosition(GetTilePosition(row, col));
                view.SetHighlight(false);
                view.Clicked -= OnViewClicked;
                view.Clicked += OnViewClicked;
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

    public void SetHighlight(GridCell cell, bool highlighted)
    {
        _views[cell.Row, cell.Col].SetHighlight(highlighted);
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
    public IEnumerator AnimateClear(IReadOnlyList<GridCell> clearedCells, float duration)
    {
        List<IEnumerator> routines = new List<IEnumerator>(clearedCells.Count);
        foreach (GridCell cell in clearedCells)
        {
            routines.Add(_views[cell.Row, cell.Col].AnimateFadeOut(duration));
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
