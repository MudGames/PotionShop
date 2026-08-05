using System;
using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;

// 타일 하나의 포인터 입력(탭 vs 드래그)을 감지해 TileView에 알려주는 보조 컴포넌트 - TileView가
// MonoBehaviour가 아니라서 EventSystems 인터페이스를 직접 구현할 수 없다(ButtonHoverAnimator와
// 같은 이유로 별도 컴포넌트로 분리). 화면 좌표를 이 타일의 부모(보드 컨테이너) 기준 로컬 좌표
// 델타로 미리 변환해서 넘겨준다 - TileView는 셀 크기(로컬 캔버스 단위)와 비교하기만 하면 된다.
// 드래그 도중 매 프레임(Dragging) 델타를 흘려보내 TileView가 스왑 미리보기 연출을 할 수 있게
// 한다(2026-08-04 추가) - 방향 판정 자체는 여전히 TileView가 담당한다.
//
// IDragHandler(OnDrag)를 반드시 구현해야 한다 - PointerInputModule이 포인터를 누르는 시점에
// ExecuteEvents.GetEventHandler<IDragHandler>로 "이 오브젝트가 드래그 가능한가"를 판정해
// pointerEvent.pointerDrag를 정하고, 그게 null이면 ProcessDrag가 즉시 리턴해버려서
// OnBeginDrag/OnEndDrag가 아예 호출되지 않는다(IBeginDragHandler/IEndDragHandler만으로는
// 판정 대상이 안 됨) - "마우스로 드래그가 안 된다"는 2026-08-04 버그의 원인.
//
// 같은 제스처에서 드래그가 이미 시작됐다면 그 뒤에 오는 OnPointerClick은 무시한다(2026-08-04
// 추가) - EventSystem의 클릭/드래그 판정과 무관하게, 같은 손 제스처에서 "이동"과 "탭"이 동시에
// 인정되면 안 된다는 걸 이 컴포넌트 스스로도 보장한다. 이게 없으면 특수 타일을 드래그로 옮기려는
// 제스처가 동시에 탭으로도 인식돼, 옮기려던 자리(원래 위치)에서 즉시 활성화(TileController.
// SpecialActivationRequested)돼 버리는 문제가 있었다 - 일반 타일은 탭 처리 결과가 선택음
// 재생뿐이라 티가 안 났지만, 특수 타일은 제자리에서 즉시 터지는 것으로 눈에 띄게 나타났다.
public sealed class TileDragInput : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerClickHandler, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    public event Action Tapped;
    public event Action DragStarted;
    public event Action<Vector2> Dragging;
    public event Action<Vector2> DragEnded;

    // 손가락/마우스를 대는 순간 살짝 눌리는 느낌을 줘서 "이 타일이 눌렸다"는 걸 바로 알 수 있게
    // 한다(2026-08-05, "타일을 선택했을 때 누르고 인식할 수 있도록 효과가 있어야 합니다" 요청).
    // 탭인지 드래그인지 판정되기 전인 PointerDown 시점부터 시작해, 손을 떼는 순간(OnPointerUp -
    // 탭/드래그 어느 쪽으로 끝나든 항상 호출됨) 원래 크기로 돌아온다. TileDragInput은 TileView와
    // 같은 GameObject/RectTransform에 붙어 있어 ButtonHoverAnimator와 같은 패턴으로 자기
    // Transform을 직접 스케일할 수 있다 - TileView(MonoBehaviour 아님)를 거칠 필요가 없다.
    private const float PressedScale = 0.9f;
    private const float PressTweenDuration = 0.08f;

    private Vector2 _dragStartScreenPosition;
    private Camera _dragCamera;
    private bool _isDragging;
    private Coroutine _pressRoutine;

    public void OnPointerDown(PointerEventData eventData)
    {
        _isDragging = false;
        RestartPressRoutine(ScaleTo(PressedScale, PressTweenDuration));
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        RestartPressRoutine(ScaleTo(1f, PressTweenDuration));
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (_isDragging)
        {
            return;
        }

        Tapped?.Invoke();
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        _isDragging = true;
        _dragStartScreenPosition = eventData.position;
        _dragCamera = eventData.pressEventCamera;
        DragStarted?.Invoke();
    }

    public void OnDrag(PointerEventData eventData)
    {
        Dragging?.Invoke(ComputeLocalDelta(eventData.position));
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        DragEnded?.Invoke(ComputeLocalDelta(eventData.position));
    }

    // 드래그 시작 지점 기준, 이 타일의 부모(보드 컨테이너) 로컬 좌표계에서의 변위 - 캔버스
    // 스케일과 무관하게 셀 크기(로컬 단위)와 정확히 비교할 수 있다.
    private Vector2 ComputeLocalDelta(Vector2 currentScreenPosition)
    {
        RectTransform reference = transform.parent as RectTransform;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(reference, _dragStartScreenPosition, _dragCamera, out Vector2 startLocal);
        RectTransformUtility.ScreenPointToLocalPointInRectangle(reference, currentScreenPosition, _dragCamera, out Vector2 currentLocal);
        return currentLocal - startLocal;
    }

    // 풀로 반환되며 비활성화될 때(눌린 채로 애니메이션이 끊기는 등) 눌린 스케일이 그대로 남지
    // 않게 되돌린다.
    private void OnDisable()
    {
        if (_pressRoutine != null)
        {
            StopCoroutine(_pressRoutine);
            _pressRoutine = null;
        }

        transform.localScale = Vector3.one;
    }

    private void RestartPressRoutine(IEnumerator routine)
    {
        if (_pressRoutine != null)
        {
            StopCoroutine(_pressRoutine);
        }

        _pressRoutine = StartCoroutine(routine);
    }

    private IEnumerator ScaleTo(float targetScale, float duration)
    {
        Vector3 start = transform.localScale;
        Vector3 target = Vector3.one * targetScale;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            transform.localScale = Vector3.LerpUnclamped(start, target, t);
            yield return null;
        }

        transform.localScale = target;
    }
}
