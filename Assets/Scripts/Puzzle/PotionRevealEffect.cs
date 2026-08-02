using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

// 스테이지 클리어 직후, 노트 패널이 슬라이드해 올라오기 전에 잠깐 재생되는 짧은 시각 연출: 물약이
// 섞인 색을 보여주는 단순한 색상 사각형으로, "방금 모은 재료들이 이렇게 변했다"는 느낌을 주기 위한
// 것이지, 텍스트 요약으로 조용히 넘어가기 위한 것이 아니다. 별도로 임포트한 아트 대신 스프라이트
// 없는 Image(퍼즐 타일과 같은 placeholder 스타일의 단색 사각형)를 사용하므로, 이후 게임 전체가
// 어떤 아트 스타일로 정착하든 충돌하지 않으며, 특정 스프라이트 애셋이나 Unity 내장 리소스의
// 존재 여부에도 의존하지 않는다.
public sealed class PotionRevealEffect
{
    private const float ScaleInDuration = 0.35f;
    private const float HoldDuration = 0.6f;
    private const float FadeOutDuration = 0.35f;

    private readonly MonoBehaviour _coroutineHost;
    private readonly GameObject _root;
    private readonly Image _dropImage;
    private readonly CanvasGroup _canvasGroup;

    public PotionRevealEffect(MonoBehaviour coroutineHost, Transform parent)
    {
        _coroutineHost = coroutineHost;

        _root = new GameObject("PotionRevealEffect", typeof(RectTransform), typeof(CanvasGroup));
        _root.transform.SetParent(parent, false);

        RectTransform rootRect = _root.GetComponent<RectTransform>();
        rootRect.anchorMin = new Vector2(0.5f, 0.5f);
        rootRect.anchorMax = new Vector2(0.5f, 0.5f);
        rootRect.sizeDelta = new Vector2(180.0f, 180.0f);
        rootRect.anchoredPosition = Vector2.zero;

        _canvasGroup = _root.GetComponent<CanvasGroup>();
        _canvasGroup.alpha = 0.0f;
        _canvasGroup.blocksRaycasts = false;
        _canvasGroup.interactable = false;

        GameObject imageObject = new GameObject("Drop", typeof(RectTransform), typeof(Image));
        imageObject.transform.SetParent(_root.transform, false);

        RectTransform imageRect = imageObject.GetComponent<RectTransform>();
        imageRect.anchorMin = Vector2.zero;
        imageRect.anchorMax = Vector2.one;
        imageRect.offsetMin = Vector2.zero;
        imageRect.offsetMax = Vector2.zero;

        _dropImage = imageObject.GetComponent<Image>();

        _root.SetActive(false);
    }

    public void Play(Color potionColor, Action onComplete)
    {
        _dropImage.color = potionColor;
        _root.transform.localScale = Vector3.zero;
        _canvasGroup.alpha = 0.0f;
        _root.SetActive(true);

        _coroutineHost.StartCoroutine(PlayRoutine(onComplete));
    }

    private IEnumerator PlayRoutine(Action onComplete)
    {
        yield return Animate(ScaleInDuration, t =>
        {
            _root.transform.localScale = Vector3.one * Mathf.SmoothStep(0.0f, 1.0f, t);
            _canvasGroup.alpha = Mathf.SmoothStep(0.0f, 1.0f, t);
        });

        yield return new WaitForSeconds(HoldDuration);

        yield return Animate(FadeOutDuration, t => _canvasGroup.alpha = Mathf.SmoothStep(1.0f, 0.0f, t));

        _root.SetActive(false);
        onComplete?.Invoke();
    }

    private static IEnumerator Animate(float duration, Action<float> onProgress)
    {
        float elapsed = 0.0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            onProgress(Mathf.Clamp01(elapsed / duration));
            yield return null;
        }

        onProgress(1.0f);
    }
}
