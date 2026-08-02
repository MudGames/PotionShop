using System;
using System.Collections;
using Puzzle.Core;
using UnityEngine;

// 호스트가 되는 MonoBehaviour의 코루틴에서 GridController.SwapResult를 시간에 따라 재생하며,
// 아래의 타이밍으로 BoardView의 애니메이션 메서드들을 구동한다. 게임 규칙은 전혀 모른다 -
// GridController가 이미 결정한 일들을 어떤 순서로 재생할지만 알 뿐이다.
public sealed class PuzzleEffectController
{
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

            yield return _boardView.AnimateClear(step.ClearedCells, _clearDuration);
            _boardView.RefreshSpawnedSpecials(step.SpawnedSpecials, step.BoardSnapshot);

            yield return _boardView.AnimateGravity(step.Moves, step.Fills, step.BoardSnapshot, _moveDuration);

            // 안전망: 모든 셀을 이 스텝의 정확한 최종 상태로 즉시 맞춘다. 위 애니메이션은 Core가
            // clear/move/fill로 보고한 셀들만 다루는데, 이는 원래 변경된 모든 셀과 일치해야 정상이다 -
            // 이 코드는 혹시라도 그게 어긋났을 때조차 시각적 정확성을 보장하기 위한 것일 뿐이다.
            _boardView.RefreshAll(step.BoardSnapshot);

            yield return new WaitForSeconds(_stepPauseDuration);
        }

        onComplete?.Invoke();
    }
}
