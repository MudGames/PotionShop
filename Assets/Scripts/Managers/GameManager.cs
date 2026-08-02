using System.Collections.Generic;
using Puzzle.Core;
using UnityEngine;

// 씬을 넘나들며 유지되고(DontDestroyOnLoad), 플레이어가 현재 어느 스테이지에 있는지와
// 노트북의 기록을 기억한다. Match3Controller는 무엇을 만들어야 하는지 알기 위해 CurrentLevel을
// 읽고, 플레이어가 노트 패널을 닫으면 AdvanceStage()를 호출한다 - 지금은 전부 하나의 퍼즐 씬
// 안에서 이루어지며 씬 전환은 없다.
//
// Match3Controller가 직접 호출하지 않는다 - OrderClearedChannel을 스스로 구독해 주문 완료를
// 감지하고 RecordStageCleared()를 호출한다(CLAUDE.md 이벤트 채널 아키텍처 원칙 참고).
public sealed class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    [SerializeField]
    private StageSequence stageSequence;
    [SerializeField]
    private VoidEventChannel orderClearedChannel;

    private int _currentStageIndex;
    private readonly List<string> _completedStageTitles = new List<string>();

    public LevelData CurrentLevel =>
        stageSequence != null && stageSequence.stages.Length > 0
            ? stageSequence.stages[Mathf.Clamp(_currentStageIndex, 0, stageSequence.stages.Length - 1)]
            : null;

    // 노트 패널이 보여주는 대상 - 방금 클리어한 스테이지이며, CurrentLevel이 아니다(노트 패널이
    // 읽을 시점에는 이미 다음 스테이지로 넘어가 있을 수 있기 때문).
    public LevelData LastCompletedLevel { get; private set; }

    // 이번 세션에서 지금까지 클리어한 모든 스테이지의 제목, 오래된 순서대로 - 노트의 목차에
    // 해당한다. 이 오브젝트가 DontDestroyOnLoad로 유지되므로 씬이 바뀌어도 함께 유지된다.
    public IReadOnlyList<string> CompletedStageTitles => _completedStageTitles;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void OnEnable()
    {
        orderClearedChannel.OnRaised += RecordStageCleared;
    }

    private void OnDisable()
    {
        orderClearedChannel.OnRaised -= RecordStageCleared;
    }

    private void RecordStageCleared()
    {
        LastCompletedLevel = CurrentLevel;
        if (!string.IsNullOrEmpty(LastCompletedLevel?.title))
        {
            _completedStageTitles.Add(LastCompletedLevel.title);
        }
    }

    public void AdvanceStage()
    {
        if (stageSequence != null && _currentStageIndex < stageSequence.stages.Length - 1)
        {
            _currentStageIndex++;
        }
        // 이미 마지막 스테이지라면 그대로 머물러 다시 플레이한다. 별도의 "캠페인 완료" 연출은
        // 지금은 범위 밖이다 - 실제로 스테이지가 여러 개 소진될 만큼 늘어나면 그때 다시 다룬다.
    }
}
