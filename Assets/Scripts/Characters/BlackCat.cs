using UnityEngine;

// WaypointWanderer(Witch.cs와 공유)를 이용한 좌우 배회. Rigidbody2D를 거치지 않고
// transform.position을 직접(Mathf.MoveTowards) 움직인다.
// 애니메이션은 제대로 된 Animator + AnimatorController(BlackCat.controller: int "State"
// 파라미터로 전환되는 Sit/WalkEast/WalkWest 상태)로 재생되며, 반전(flip)은 전혀 사용하지
// 않는다(전용 WalkEast/WalkWest 클립 사용) - 이것이 실제로 예전의 "점프" 버그를 고친
// 원인이었고(반전을 아예 쓰지 않은 상태로 테스트해서 확인함), 그래서 CharacterRenderer의
// 반전 기반 방식은 의도적으로 사용하지 않는다.
[RequireComponent(typeof(Animator))]
public sealed class BlackCat : MonoBehaviour
{
    // BlackCat.controller의 상태값과 반드시 일치해야 함: 0 = Sit, 1 = WalkEast, 2 = WalkWest.
    private const int AnimStateSit = 0;
    private const int AnimStateWalkEast = 1;
    private const int AnimStateWalkWest = 2;
    private static readonly int StateParam = Animator.StringToHash("State");

    [SerializeField]
    private Transform leftWaypoint;
    [SerializeField]
    private Transform rightWaypoint;
    [SerializeField]
    private float wanderSpeed = 0.5f;
    [SerializeField]
    private float pauseDurationMin = 2.0f;
    [SerializeField]
    private float pauseDurationMax = 4.0f;
    [SerializeField]
    private float arrivalThreshold = 0.05f;

    private Animator _animator;
    private WaypointWanderer _wanderer;

    private void Awake()
    {
        _animator = GetComponent<Animator>();
        _wanderer = new WaypointWanderer(leftWaypoint, rightWaypoint, pauseDurationMin, pauseDurationMax, arrivalThreshold);
    }

    private void Start()
    {
        _wanderer.Begin();
        UpdateWalkAnimation();
    }

    private void Update()
    {
        if (_wanderer.State == WaypointWanderer.WanderState.Walking)
        {
            UpdateWalking();
        }
        else if (_wanderer.TickPausing(Time.deltaTime))
        {
            UpdateWalkAnimation();
        }
    }

    private void UpdateWalking()
    {
        // 이번 프레임의 deltaTime을 clamp한다 - 에디터가 잠깐 멈추는 경우(포커스 잃음, 긴 GC
        // 정지, 브레이크포인트) Time.deltaTime이 순간적으로 몇 초까지 튈 수 있는데, 이걸 그대로
        // 두면 고양이가 눈에 보이게 걷는 대신 한 프레임 만에 목표 지점 근처까지 이동해버린다.
        float clampedDeltaTime = Mathf.Min(Time.deltaTime, 1.0f / 30.0f);

        Vector3 position = transform.position;
        position.x = Mathf.MoveTowards(position.x, _wanderer.TargetWaypoint.position.x, wanderSpeed * clampedDeltaTime);
        transform.position = position;

        if (_wanderer.TickWalking(position.x))
        {
            _animator.SetInteger(StateParam, AnimStateSit);
        }
    }

    private void UpdateWalkAnimation()
    {
        bool movingWest = _wanderer.TargetWaypoint.position.x < transform.position.x;
        _animator.SetInteger(StateParam, movingWest ? AnimStateWalkWest : AnimStateWalkEast);
    }
}
