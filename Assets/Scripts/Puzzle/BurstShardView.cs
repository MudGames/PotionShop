using System.Collections;
using UnityEngine;
using UnityEngine.UI;

// 매치 버스트(BurstEffectView)에서 사방으로 튀어나가는 작은 파편/스파크 하나를 감싼다(2026-08-05,
// "너무 정적입니다. 연출이 좀 더 필요합니다" 피드백 - 제자리에서 커지기만 하던 플래시에 실제로
// 바깥으로 흩어지는 움직임을 더한다). BurstEffectView와 같은 패턴의 순수 C# 클래스 -
// BurstShardPool이 재사용 인스턴스를 관리한다.
public sealed class BurstShardView
{
    private readonly Image _image;
    private readonly RectTransform _rectTransform;

    public GameObject GameObject => _image.gameObject;

    public BurstShardView(Transform parent, Sprite shardSprite)
    {
        GameObject shardObject = new GameObject("BurstShard", typeof(RectTransform), typeof(Image));
        shardObject.transform.SetParent(parent, false);

        _rectTransform = shardObject.GetComponent<RectTransform>();
        _rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
        _rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
        _rectTransform.pivot = new Vector2(0.5f, 0.5f);

        _image = shardObject.GetComponent<Image>();
        _image.sprite = shardSprite;
        _image.raycastTarget = false;
        _image.preserveAspect = true;

        shardObject.SetActive(false);
    }

    public void SetActive(bool active)
    {
        GameObject.SetActive(active);
    }

    // origin에서 direction 방향으로 distance만큼 이즈아웃으로 날아가며(빠르게 나갔다 느려짐),
    // 크기는 startSize에서 점점 작아지고 알파도 같이 빠진다 - 실제로 터져서 흩어지는 느낌.
    public IEnumerator Animate(Vector2 origin, Vector2 direction, float distance, float startSize, float duration)
    {
        _rectTransform.SetAsLastSibling();

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float eased = 1f - (1f - t) * (1f - t);

            _rectTransform.anchoredPosition = origin + direction * (distance * eased);

            float size = Mathf.Lerp(startSize, startSize * 0.25f, t);
            _rectTransform.sizeDelta = new Vector2(size, size);

            Color color = _image.color;
            color.a = 1f - t;
            _image.color = color;

            yield return null;
        }

        SetActive(false);
    }
}
