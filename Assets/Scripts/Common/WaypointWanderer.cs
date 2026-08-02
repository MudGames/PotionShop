using UnityEngine;

// 구간 사이에 랜덤한 멈춤을 두는 좌우 웨이포인트 배회 로직 - 배회하는 모든 배경 캐릭터(Witch,
// BlackCat)가 공유하는 "한 지점까지 걷기 / 임의의 시간만큼 쉬기 / 되돌아 걷기" 형태를
// 담당한다. 상태/타이머/웨이포인트 관리만 여기서 맡고, 실제 이동 방식(물리 기반이냐 transform
// 직접 조작이냐)과 애니메이션은 캐릭터마다 다르므로 호출하는 쪽에 맡긴다.
public sealed class WaypointWanderer
{
    public enum WanderState
    {
        Walking,
        Pausing
    }

    private readonly Transform _leftWaypoint;
    private readonly Transform _rightWaypoint;
    private readonly float _pauseDurationMin;
    private readonly float _pauseDurationMax;
    private readonly float _arrivalThreshold;

    private float _pauseTimer;

    public WanderState State { get; private set; }
    public Transform TargetWaypoint { get; private set; }

    public WaypointWanderer(Transform leftWaypoint, Transform rightWaypoint, float pauseDurationMin, float pauseDurationMax, float arrivalThreshold)
    {
        _leftWaypoint = leftWaypoint;
        _rightWaypoint = rightWaypoint;
        _pauseDurationMin = pauseDurationMin;
        _pauseDurationMax = pauseDurationMax;
        _arrivalThreshold = arrivalThreshold;
    }

    // 첫 구간을 시작하며 rightWaypoint를 향해 걷는다(두 캐릭터의 기존 동작과 동일하게 맞춤).
    public void Begin()
    {
        TargetWaypoint = _rightWaypoint;
        State = WanderState.Walking;
    }

    // State == Walking인 동안 매 프레임 호출하며, 움직이는 대상의 현재 X 위치를 전달한다.
    // 도착이 감지된 프레임에 true를 반환한다(State가 Pausing으로 바뀜) - 이때 대기/앉기 애니메이션을 재생하는 식으로 반응하면 된다.
    public bool TickWalking(float currentX)
    {
        if (Mathf.Abs(currentX - TargetWaypoint.position.x) > _arrivalThreshold)
        {
            return false;
        }

        State = WanderState.Pausing;
        _pauseTimer = Random.Range(_pauseDurationMin, _pauseDurationMax);
        return true;
    }

    // State == Pausing인 동안 매 프레임 호출한다.
    // 멈춤이 끝나는 프레임에 true를 반환한다(State가 Walking으로 바뀌고 TargetWaypoint가 반대쪽으로 바뀜) - 이때 걷기 애니메이션을 재개하는 식으로 반응하면 된다.
    public bool TickPausing(float deltaTime)
    {
        _pauseTimer -= deltaTime;
        if (_pauseTimer > 0.0f)
        {
            return false;
        }

        TargetWaypoint = TargetWaypoint == _rightWaypoint ? _leftWaypoint : _rightWaypoint;
        State = WanderState.Walking;
        return true;
    }
}
