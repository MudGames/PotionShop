using System.Collections;
using UnityEngine;
using UnityEngine.UI;

// 매치로 타일이 사라지는 순간 겹쳐 보이는 짧은 빛 번쩍임 하나를 감싼다(2026-08-05, "타일이
// 매치했을 때 터지는 연출이 필요합니다" 요청). TileView.AnimateFadeOut의 팝/회전/페이드는 타일
// 자신의 아이콘 연출이고, 이건 그 위에 얹히는 별도의 "펑" 레이어다. TileView와 같은 패턴의
// 순수 C# 클래스(MonoBehaviour 아님) - BurstEffectPool이 재사용 인스턴스를 관리한다.
public sealed class BurstEffectView
{
    private readonly Image _image;
    private readonly RectTransform _rectTransform;

    public GameObject GameObject => _image.gameObject;

    public BurstEffectView(Transform parent, Sprite burstSprite)
    {
        GameObject burstObject = new GameObject("BurstEffect", typeof(RectTransform), typeof(Image));
        burstObject.transform.SetParent(parent, false);

        _rectTransform = burstObject.GetComponent<RectTransform>();
        _rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
        _rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
        _rectTransform.pivot = new Vector2(0.5f, 0.5f);

        _image = burstObject.GetComponent<Image>();
        _image.sprite = burstSprite;
        _image.raycastTarget = false;
        _image.preserveAspect = true;

        burstObject.SetActive(false);
    }

    public void SetActive(bool active)
    {
        GameObject.SetActive(active);
    }

    // 작게 시작해 peakSize까지 이즈아웃으로 커지며 알파가 1에서 0으로 빠르게 빠진다 - 빛이 확
    // 퍼졌다 사그라드는 느낌. 타일들과 같은 부모 아래 있으므로 매번 맨 앞으로 올려 타일 위에
    // 그려지게 한다.
    private const float StartSizeRatio = 0.3f;

    public IEnumerator Animate(Vector2 anchoredPosition, float peakSize, float duration)
    {
        _rectTransform.anchoredPosition = anchoredPosition;
        _rectTransform.SetAsLastSibling();

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float eased = 1f - (1f - t) * (1f - t);

            float size = Mathf.LerpUnclamped(peakSize * StartSizeRatio, peakSize, eased);
            _rectTransform.sizeDelta = new Vector2(size, size);

            Color color = _image.color;
            color.a = (1f - t) * (1f - t);
            _image.color = color;

            yield return null;
        }

        SetActive(false);
    }
}
