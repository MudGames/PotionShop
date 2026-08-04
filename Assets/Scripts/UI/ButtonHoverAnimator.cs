using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;

// 버튼에 마우스를 올리면 살짝 커진 뒤 그 자리에서 숨쉬듯 계속 두근거리고, 벗어나면 원래 크기로
// 돌아온다 - 정적 이미지(Start.png/Quit.png)만으로는 버튼처럼 느껴지지 않아서 MenuManager가
// 절차적으로 만드는 시작하기/종료 버튼에 붙여 쓴다. 타일 애니메이션(TileView.AnimateMoveTo)과
// 같은 Coroutine 기반 수동 Tween 방식을 따른다(DOTween은 설치돼 있지만 이 프로젝트 어디서도
// 아직 쓰이지 않아, 기존 관례에 맞춰 Coroutine을 선택함).
public sealed class ButtonHoverAnimator : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    private const float HoverScale = 1.08f;
    private const float PulseAmplitude = 0.03f;
    private const float PulsePeriod = 0.9f;
    private const float TweenDuration = 0.15f;

    private Vector3 _baseScale;
    private Coroutine _routine;

    private void Awake()
    {
        _baseScale = transform.localScale;
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        RestartRoutine(HoverRoutine());
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        RestartRoutine(ScaleTo(_baseScale, TweenDuration));
    }

    private void OnDisable()
    {
        if (_routine != null)
        {
            StopCoroutine(_routine);
            _routine = null;
        }

        transform.localScale = _baseScale;
    }

    private void RestartRoutine(IEnumerator routine)
    {
        if (_routine != null)
        {
            StopCoroutine(_routine);
        }

        _routine = StartCoroutine(routine);
    }

    // 목표 크기까지 커진 다음, 마우스가 계속 올라가 있는 동안(OnDisable/다음 RestartRoutine으로
    // 끊길 때까지) 그 주변에서 사인파로 계속 두근거린다.
    private IEnumerator HoverRoutine()
    {
        yield return ScaleTo(_baseScale * HoverScale, TweenDuration);

        float t = 0.0f;
        while (true)
        {
            t += Time.deltaTime;
            float pulse = Mathf.Sin(t / PulsePeriod * Mathf.PI * 2.0f) * PulseAmplitude;
            transform.localScale = _baseScale * (HoverScale + pulse);
            yield return null;
        }
    }

    private IEnumerator ScaleTo(Vector3 target, float duration)
    {
        Vector3 start = transform.localScale;
        float elapsed = 0.0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float eased = 1f - (1f - t) * (1f - t);
            transform.localScale = Vector3.LerpUnclamped(start, target, eased);
            yield return null;
        }

        transform.localScale = target;
    }
}
