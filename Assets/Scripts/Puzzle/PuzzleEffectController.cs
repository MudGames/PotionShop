using System;
using System.Collections;
using Puzzle.Core;
using UnityEngine;

// 호스트가 되는 MonoBehaviour의 코루틴에서 GridController.SwapResult를 시간에 따라 재생하며,
// 아래의 타이밍으로 BoardView의 애니메이션 메서드들을 구동한다. 게임 규칙은 전혀 모른다 -
// GridController가 이미 결정한 일들을 어떤 순서로 재생할지만 알 뿐이다.
public sealed class PuzzleEffectController
{
    // 제거 팝 연출(TileView.AnimateFadeOut)의 크기 배율 - 매치/콤보 규모가 클수록 더 크게 튄다
    // (2026-08-04, "손맛"이 밋밋하다는 피드백에 이어 "좀 더 동적이면 좋겠다"는 후속 피드백으로
    // 추가). 캐스케이드 스텝(step.StepIndex >= 1, 즉 연쇄로 이어진 매치)이거나 이번 스텝에서
    // 한 번에 제거되는 칸이 많을수록(스페셜 타일 폭발 포함) 더 크게 튄다 - 어떤 run이 몇 칸짜리인지
    // 셀 단위로 정확히 추적하지는 않고, 스텝 전체의 제거 칸 수를 규모의 근사치로 쓴다.
    private const float ClearPopScaleDefault = 1.25f;
    private const float ClearPopScaleMedium = 1.4f;
    private const float ClearPopScaleLarge = 1.6f;

    private readonly MonoBehaviour _coroutineHost;
    private readonly BoardView _boardView;
    private readonly float _swapDuration;
    private readonly float _clearDuration;
    private readonly float _moveDuration;
    private readonly float _stepPauseDuration;

    public PuzzleEffectController(
        MonoBehaviour coroutineHost,
        BoardView boardView,
        float swapDuration,
        float clearDuration,
        float moveDuration,
        float stepPauseDuration)
    {
        _coroutineHost = coroutineHost;
        _boardView = boardView;
        _swapDuration = swapDuration;
        _clearDuration = clearDuration;
        _moveDuration = moveDuration;
        _stepPauseDuration = stepPauseDuration;
    }

    public void Play(GridCell a, GridCell b, SwapResult result, Action onComplete)
    {
        _coroutineHost.StartCoroutine(PlayRoutine(a, b, result, onComplete));
    }

    // 제자리에서 활성화되는 단독 스페셜 타일용(GridController.TryActivateSpecial 참고) - 슬라이드해
    // 갈 두 번째 타일이 없으므로, Play가 사용하는 것과 같은 연쇄 재생으로 바로 건너뛴다.
    public void PlayActivation(SwapResult result, Action onComplete)
    {
        _coroutineHost.StartCoroutine(PlayCascadeRoutine(result, onComplete));
    }

    private IEnumerator PlayRoutine(GridCell a, GridCell b, SwapResult result, Action onComplete)
    {
        if (!result.Accepted)
        {
            AudioManager.Instance?.PlaySwapFail();
        }

        // 스왑이 결국 유효한 것으로 판명되든 아니든, 두 타일은 항상 먼저 서로를 향해 슬라이드한다 -
        // 거부되면 AnimateSwapAttempt가 확정하는 대신 다시 원래대로 슬라이드해 되돌린다.
        yield return _boardView.AnimateSwapAttempt(a, b, _swapDuration, commit: result.Accepted);

        yield return PlayCascadeRoutine(result, onComplete);
    }

    private IEnumerator PlayCascadeRoutine(SwapResult result, Action onComplete)
    {
        if (!result.Accepted)
        {
            onComplete?.Invoke();
            yield break;
        }

        foreach (CascadeStepInfo step in result.Steps)
        {
            // step 0 = 스왑 자체가 만든 매치("교환 성공"), step 1+ = 캐스케이드로 이어진 연쇄 매치
            // ("캐스케이드 연쇄") - 07-sound.md의 트리거 구분과 맞춘 것.
            if (step.StepIndex == 0)
            {
                AudioManager.Instance?.PlayMatch();
            }
            else
            {
                AudioManager.Instance?.PlayCascade();
            }

            yield return _boardView.AnimateClear(step.ClearedCells, _clearDuration, ComputeClearPopScale(step));
            _boardView.RefreshSpawnedSpecials(step.SpawnedSpecials, step.BoardSnapshot);

            yield return _boardView.AnimateGravity(step.Moves, step.Fills, step.BoardSnapshot, _moveDuration);

            // 안전망: 이 스텝에서 실제로 바뀐 셀(제거/이동 도착지/새로 채워짐)만 정확한 최종 상태로
            // 즉시 맞춘다 - BoardView.RefreshChangedCells 참고. 매치3 특성상 캐스케이드 스텝이
            // 잦으므로, 바뀌지 않은 나머지 셀까지 매번 훑는 RefreshAll은 낭비였다(2026-08-04).
            _boardView.RefreshChangedCells(step);

            yield return new WaitForSeconds(_stepPauseDuration);
        }

        onComplete?.Invoke();
    }

    private static float ComputeClearPopScale(CascadeStepInfo step)
    {
        if (step.StepIndex >= 1 || step.ClearedCells.Count >= 5)
        {
            return ClearPopScaleLarge;
        }

        if (step.ClearedCells.Count == 4)
        {
            return ClearPopScaleMedium;
        }

        return ClearPopScaleDefault;
    }
}
