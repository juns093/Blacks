using System.Collections;
using System.IO;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.SceneManagement;

// 엔딩 Timeline 전용 스크립트. 기존 TimelineManager(초반 총알 장전 로직)와는 무관하게 독립적으로 동작합니다.
// 이 스크립트는 엔딩 Timeline을 재생하는 PlayableDirector와 같은 오브젝트(또는 별도 관리 오브젝트)에 붙입니다.
public class EndingDialogueController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private PlayableDirector endingDirector; // 엔딩 Timeline을 재생하는 PlayableDirector
    [SerializeField] private TypeWriter typeWriter;            // 대사 출력을 담당하는 기존 TypeWriter

    [Header("Dialogue Group Index")]
    [Tooltip("타임라인의 Signal Emitter가 대사를 재생하도록 할지 여부. " +
             "PanelDialogueSequencer로 이미지 패널에 맞춰 대사를 띄우는 경우에는 꺼두세요. " +
             "(켜두면 시그널 시점에 대사가 한 번 더 끼어들어 이미지와 어긋납니다)")]
    [SerializeField] private bool playDialogueOnSignal = true;

    [Tooltip("TypeWriter의 Dialogue Groups 배열에서 엔딩 대사가 들어있는 인덱스")]
    [SerializeField] private int endingDialogueGroupIndex = 0;

    [Header("씬 전환 (타임라인 종료 시)")]
    [Tooltip("엔딩 타임라인이 끝난 뒤 이동할 씬 이름 (Build Settings에 등록되어 있어야 함). 비워두면 씬 전환하지 않음")]
    [SerializeField] private string nextSceneName;

    [Tooltip("타임라인이 끝난 뒤 씬 전환까지 추가로 대기하는 시간(초)")]
    [SerializeField] private float delayBeforeSceneChange = 0f;

    private bool dialoguePlayed = false;
    private bool sceneChangeStarted = false;

    void OnEnable()
    {
        if (typeWriter != null)
            typeWriter.OnSequenceFinished += OnEndingDialogueFinished;

        if (endingDirector != null)
            endingDirector.stopped += OnEndingTimelineFinished;
    }

    void OnDisable()
    {
        if (typeWriter != null)
            typeWriter.OnSequenceFinished -= OnEndingDialogueFinished;

        if (endingDirector != null)
            endingDirector.stopped -= OnEndingTimelineFinished;
    }

    // Timeline의 Signal Emitter가 이 메서드를 호출하도록 Signal Receiver에서 연결합니다.
    public void PlayEndingDialogue()
    {
        if (!playDialogueOnSignal)
        {
            // 대사는 PanelDialogueSequencer가 이미지 패널에 맞춰 재생한다.
            // 시그널은 무시하되, 타임라인 종료 시 씬 전환 기능은 그대로 동작한다.
            Debug.Log("[EndingDialogueController] playDialogueOnSignal이 꺼져 있어 시그널 대사 재생을 건너뜁니다.");
            return;
        }

        if (dialoguePlayed) return; // 중복 호출 방지 (Timeline을 되감아도 한 번만 재생)
        dialoguePlayed = true;

        // 타임라인은 멈추지 않고 계속 재생됨. 대사는 그 위에 자막처럼 겹쳐서 재생됨.
        if (typeWriter != null)
            typeWriter.PlayDialogueGroup(endingDialogueGroupIndex);
        else
            Debug.LogWarning("[EndingDialogueController] typeWriter가 연결되어 있지 않습니다!");
    }

    private void OnEndingDialogueFinished()
    {
        // 타임라인을 멈춘 적이 없으므로 Resume()은 필요 없음.
        // 씬 전환은 대사 완료가 아니라 "타임라인 자체가 끝났을 때" 처리합니다 (아래 OnEndingTimelineFinished).
    }

    // 엔딩 타임라인(PlayableDirector) 재생이 완전히 끝났을 때 호출됨
    private void OnEndingTimelineFinished(PlayableDirector director)
    {
        if (sceneChangeStarted) return;

        if (string.IsNullOrEmpty(nextSceneName))
        {
            Debug.LogWarning("[EndingDialogueController] nextSceneName이 비어있어 씬 전환을 스킵합니다.");
            return;
        }

        sceneChangeStarted = true;
        StartCoroutine(LoadNextSceneRoutine());
    }

    private IEnumerator LoadNextSceneRoutine()
    {
        if (delayBeforeSceneChange > 0f)
            yield return new WaitForSeconds(delayBeforeSceneChange);

        if (SceneTransition.Instance != null)
        {
            int buildIndex = GetBuildIndexByName(nextSceneName);
            if (buildIndex >= 0)
            {
                SceneTransition.Instance.LoadScene(buildIndex);
                yield break;
            }

            Debug.LogWarning($"[EndingDialogueController] '{nextSceneName}' 씬을 Build Settings에서 찾지 못해 SceneTransition을 사용하지 못했습니다. 이름으로 직접 로드합니다.");
        }

        SceneManager.LoadScene(nextSceneName);
    }

    // Build Settings에 등록된 씬 이름으로 build index를 찾는 헬퍼
    private int GetBuildIndexByName(string name)
    {
        for (int i = 0; i < SceneManager.sceneCountInBuildSettings; i++)
        {
            string path = SceneUtility.GetScenePathByBuildIndex(i);
            string sceneNameAtIndex = Path.GetFileNameWithoutExtension(path);
            if (sceneNameAtIndex == name)
                return i;
        }
        return -1;
    }
}