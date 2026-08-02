using UnityEngine;

[RequireComponent(typeof(MovementRigidbody))]
[RequireComponent(typeof(CharacterRenderer))]
public class Witch : MonoBehaviour
{
    private enum State
    {
        Standing,
        Walking,
        Pausing,
        Reacting
    }

    [Tooltip("꺼두면 웨이포인트 왕복 없이 제자리에 서 있기만 함(가게 앞에 서 있는 연출용) - 리액션은 그대로 동작함")]
    [SerializeField]
    private bool wanderEnabled = false;

    [SerializeField]
    private Transform leftWaypoint;
    [SerializeField]
    private Transform rightWaypoint;
    [SerializeField]
    private float wanderSpeed = 1.0f;
    [SerializeField]
    private float pauseDurationMin = 2.0f;
    [SerializeField]
    private float pauseDurationMax = 4.0f;
    [SerializeField]
    private float arrivalThreshold = 0.1f;

    [Space]
    [SerializeField]
    private float speechBubbleHeight = 0.35f;
    [SerializeField]
    private int speechBubbleSortingOrder = 10;
    [SerializeField]
    private Sprite leftActionSprite;
    [SerializeField]
    private Sprite rightActionSprite;

    [Space]
    [Tooltip("스테이지 시작 시 보여줄 리액션 (예: emote_question)")]
    [SerializeField]
    private Sprite orderRequestedSprite;
    [Tooltip("퍼즐 주문 완료 시 즉시 보여줄 리액션 (예: emote_faceHappy)")]
    [SerializeField]
    private Sprite orderClearedSprite;
    [Tooltip("퍼즐 게임 오버 시 즉시 보여줄 리액션 (예: emote_faceSad)")]
    [SerializeField]
    private Sprite gameOverSprite;
    [SerializeField]
    private float reactionDuration = 2.0f;

    // Match3Controller를 직접 참조하지 않는다 - 이 세 채널을 구독해 스스로 반응한다
    // (CLAUDE.md 이벤트 채널 아키텍처 원칙 참고).
    [Space]
    [SerializeField]
    private VoidEventChannel levelStartedChannel;
    [SerializeField]
    private VoidEventChannel orderClearedChannel;
    [SerializeField]
    private VoidEventChannel gameOverChannel;

    private MovementRigidbody _movementRigidbody;
    private CharacterRenderer _characterRenderer;
    private SpriteRenderer _speechBubbleRenderer;
    private WaypointWanderer _wanderer;

    private State _state;
    private float _reactionTimer;
    private bool _isFlipped;

    private void Awake()
    {
        _movementRigidbody = GetComponent<MovementRigidbody>();
        _characterRenderer = GetComponent<CharacterRenderer>();
        _movementRigidbody.MoveSpeed = wanderSpeed;
        _wanderer = new WaypointWanderer(leftWaypoint, rightWaypoint, pauseDurationMin, pauseDurationMax, arrivalThreshold);

        CreateSpeechBubble();
    }

    private void OnEnable()
    {
        levelStartedChannel.OnRaised += ShowOrderRequestedReaction;
        orderClearedChannel.OnRaised += ShowOrderClearedReaction;
        gameOverChannel.OnRaised += ShowGameOverReaction;
    }

    private void OnDisable()
    {
        levelStartedChannel.OnRaised -= ShowOrderRequestedReaction;
        orderClearedChannel.OnRaised -= ShowOrderClearedReaction;
        gameOverChannel.OnRaised -= ShowGameOverReaction;
    }

    private void CreateSpeechBubble()
    {
        GameObject speechBubbleObject = new GameObject("SpeechBubble");
        speechBubbleObject.transform.SetParent(transform, false);
        speechBubbleObject.transform.localPosition = new Vector3(0.0f, speechBubbleHeight, 0.0f);

        _speechBubbleRenderer = speechBubbleObject.AddComponent<SpriteRenderer>();
        _speechBubbleRenderer.sortingOrder = speechBubbleSortingOrder;

        speechBubbleObject.SetActive(false);
    }

    private void Start()
    {
        if (wanderEnabled)
        {
            _wanderer.Begin();
            _state = State.Walking;
        }
        else
        {
            EnterStanding();
        }
    }

    private void Update()
    {
        switch (_state)
        {
            case State.Walking:
                UpdateWalking();
                break;
            case State.Pausing:
                UpdatePausing();
                break;
            case State.Reacting:
                UpdateReacting();
                break;
            case State.Standing:
                break; // 리액션이 끼어들기 전까지는 그저 가만히 서 있는다
        }
    }

    private void EnterStanding()
    {
        _state = State.Standing;
        _characterRenderer.SetDirection(0);
        _characterRenderer.OnMovement(0.0f);
        _characterRenderer.OnFootStepEffect(false);
        _characterRenderer.SetSitting(false);
    }

    // 지금 하고 있는 행동(걷기 또는 대기 중 멈춤)을 중단시키고 퍼즐 이벤트에 즉시 반응하게 한다 -
    // 퍼즐이 자기 HUD에만 말을 거는 게 아니라, "방금 퍼즐에서 뭔가 했다"는 사실을 화면 속
    // 캐릭터와 직접 연결해주는 부분이다. 반응이 끝나면 다시 배회를 재개한다.
    private void ShowOrderRequestedReaction()
    {
        ShowReaction(orderRequestedSprite);
    }

    private void ShowOrderClearedReaction()
    {
        ShowReaction(orderClearedSprite);
    }

    private void ShowGameOverReaction()
    {
        ShowReaction(gameOverSprite);
    }

    private void ShowReaction(Sprite sprite)
    {
        if (sprite == null)
        {
            return;
        }

        _state = State.Reacting;
        _reactionTimer = reactionDuration;

        _characterRenderer.OnMovement(0.0f);
        _characterRenderer.OnFootStepEffect(false);

        _speechBubbleRenderer.sprite = sprite;
        Transform bubbleTransform = _speechBubbleRenderer.transform;
        bubbleTransform.localScale = new Vector3(_isFlipped ? -1.0f : 1.0f, 1.0f, 1.0f);
        bubbleTransform.gameObject.SetActive(true);
    }

    private void UpdateReacting()
    {
        _reactionTimer -= Time.deltaTime;
        if (_reactionTimer > 0.0f)
        {
            return;
        }

        _speechBubbleRenderer.gameObject.SetActive(false);

        if (wanderEnabled)
        {
            _state = State.Walking; // wanderer가 현재 목표로 삼고 있는 웨이포인트를 향해 배회를 재개한다
        }
        else
        {
            EnterStanding();
        }
    }

    private void FixedUpdate()
    {
        if (_state != State.Walking)
        {
            return;
        }

        float directionX = Mathf.Sign(_wanderer.TargetWaypoint.position.x - _movementRigidbody.Rigidbody.position.x);
        _movementRigidbody.MoveTo(new Vector2(directionX, 0.0f));
    }

    private void UpdateWalking()
    {
        bool isFlipped = _wanderer.TargetWaypoint.position.x < transform.position.x;
        _isFlipped = isFlipped;
        _characterRenderer.SpriteFlipX(isFlipped);
        _characterRenderer.SetDirection(0); // 항상 좌우(Side/South)만 사용 — 북쪽 아트는 여기서 필요 없음
        _characterRenderer.OnMovement(1.0f);
        _characterRenderer.OnFootStepEffect(true);
        _characterRenderer.SetSitting(false);

        if (_wanderer.TickWalking(_movementRigidbody.Rigidbody.position.x))
        {
            EnterPausing();
        }
    }

    private void EnterPausing()
    {
        _state = State.Pausing;

        _characterRenderer.OnMovement(0.0f);
        _characterRenderer.OnFootStepEffect(false);
        _characterRenderer.SetSitting(true);

        bool isAtRightWaypoint = _wanderer.TargetWaypoint == rightWaypoint;
        _speechBubbleRenderer.sprite = isAtRightWaypoint ? rightActionSprite : leftActionSprite;
        // 부모(캐릭터)의 좌우 반전 스케일을 상쇄해 말풍선 이미지가 항상 좌우 반전 없이 보이게 함
        Transform bubbleTransform = _speechBubbleRenderer.transform;
        bubbleTransform.localScale = new Vector3(_isFlipped ? -1.0f : 1.0f, 1.0f, 1.0f);
        bubbleTransform.gameObject.SetActive(true);
    }

    private void UpdatePausing()
    {
        if (!_wanderer.TickPausing(Time.deltaTime))
        {
            return;
        }

        _speechBubbleRenderer.gameObject.SetActive(false);
        _state = State.Walking;
    }
}
