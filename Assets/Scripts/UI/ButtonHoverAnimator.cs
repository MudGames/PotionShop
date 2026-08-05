using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

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

    // 호버 중엔 버튼 배경 전체가 이 색으로 바뀐다(2026-08-05, "테두리가 아니라 버튼 전체
    // 색상이 변해야 합니다" 요청 - 별도 테두리 오브젝트를 얹는 대신 버튼 자신의 배경
    // Image.color를 직접 바꾸는 방식으로 정리). Image.color는 원본 스프라이트에 곱연산(multiply)
    // 되므로 R/G/B 중 하나라도 1보다 작으면 그 채널만큼 원본보다 어두워진다 - 보라빛
    // (R/G가 1 미만)으로 바꿨더니 "너무 어둡다"는 피드백을 받아, R/G는 최대(1)로 유지하고
    // B만 살짝 낮춰 실제로 밝아 보이는 따뜻한 골드빛 화이트로 재변경(2026-08-05).
    private static readonly Color HoverTint = new Color(1f, 0.96f, 0.8f, 1f);

    private Vector3 _baseScale;
    private Coroutine _routine;
    private Image _buttonImage;
    private Color _baseColor;

    private void Awake()
    {
        _baseScale = transform.localScale;
    }

    // 호버 시 색을 바꿀 버튼 자신의 배경 Image를 나중에 주입받는다(2026-08-05) - 원래는 별도
    // 테두리 오브젝트를 마우스를 올렸을 때만 보여주는 방식이었는데("마우스를 올렸을 때만 물약
    // 테두리랑 같은 테두리가 생기게 해주세요"), 스케일/알파 타이밍이 버튼 바운스와 계속 어긋나는
    // 문제를 겪다("테두리 바운스를 버튼에 맞춰야 합니다", "테두리 알파값 조정되는 시간이 버튼
    // 바운스랑 안맞아서 어색합니다") "테두리가 아니라 버튼 전체 색상이 변해야 합니다" 요청으로
    // 테두리 오브젝트 자체를 없애고 버튼 배경색을 직접 바꾸는 지금 방식으로 정리했다.
    public void SetButtonImage(Image buttonImage)
    {
        _buttonImage = buttonImage;
        _baseColor = buttonImage.color;
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        RestartRoutine(HoverRoutine());
        if (_buttonImage != null)
        {
            _buttonImage.color = HoverTint;
        }
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        RestartRoutine(ScaleTo(_baseScale, TweenDuration));
        if (_buttonImage != null)
        {
            _buttonImage.color = _baseColor;
        }
    }

    private void OnDisable()
    {
        if (_routine != null)
        {
            StopCoroutine(_routine);
            _routine = null;
        }

        transform.localScale = _baseScale;

        if (_buttonImage != null)
        {
            _buttonImage.color = _baseColor;
        }
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
    // 끊길 때까지) 그 주변에서 사인파로 계속 두근거린다. 배경색은 이 바운스와 무관하게 호버
    // 진입/이탈 시점에 한 번씩만 바뀐다(OnPointerEnter/OnPointerExit 참고).
    private IEnumerator HoverRoutine()
    {
        yield return ScaleTo(_baseScale * HoverScale, TweenDuration);

        float t = 0.0f;
        while (true)
        {
            t += Time.deltaTime;
            float sin = Mathf.Sin(t / PulsePeriod * Mathf.PI * 2.0f);
            transform.localScale = _baseScale * (HoverScale + sin * PulseAmplitude);
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
