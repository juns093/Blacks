using UnityEngine;

// 타임라인의 Activation Track이 이미지 패널을 켜는 순간에 맞춰 대사를 재생하는 스크립트.
//
// ── 왜 필요한가 ──
// TypeWriter는 한 그룹의 대사들을 delayBetweenDialogues(초) 간격으로 혼자 넘깁니다.
// StoryScene의 경우 Signal Emitter가 0초에 있어서 대사 3줄이 0/2/4초에 다 지나가 버리는데,
// 정작 이미지 패널은 34.9초 / 39.9초 / 48.1초에 등장해서 서로 완전히 어긋났습니다.
//
// 이 스크립트는 패널이 "꺼짐 -> 켜짐"으로 바뀌는 순간을 감지해서
// 그 패널에 지정된 대사 그룹을 재생합니다. 즉 이미지와 대사가 항상 붙어 다닙니다.
//
// ── 쓰는 법 (앞부분은 자동, 뒷부분만 이미지에 맞추는 방식) ──
//  1. 대사를 그룹으로 나눕니다.
//       그룹 0 = 0초 Signal Emitter가 재생할 앞부분 (여러 줄이 자동으로 넘어감)
//       그룹 1 = 첫 번째 이미지가 켜질 때 재생할 줄들
//       그룹 2 = 두 번째 이미지가 켜질 때 ... (이하 동일)
//  2. 빈 오브젝트에 이 스크립트를 붙이고 typeWriter를 연결합니다.
//  3. 타임라인이 켜고 끄는 이미지 패널을 panel1~panel4에 순서대로 넣고,
//     각각 재생할 대사 그룹 인덱스를 지정합니다. (안 쓰는 칸은 비워두면 무시됩니다)
//  4. EndingDialogueController는 playDialogueOnSignal을 켜 두고
//     endingDialogueGroupIndex를 0으로 두면 앞부분이 0초에 자동으로 나갑니다.
//     반대로 처음부터 전부 이미지에 맞추고 싶다면 playDialogueOnSignal을 끄면 됩니다.
public class PanelDialogueSequencer : MonoBehaviour
{
    [Header("연동")]
    [Tooltip("대사를 출력할 TypeWriter")]
    [SerializeField] private TypeWriter typeWriter;

    [Header("패널 1")]
    [SerializeField] private GameObject panel1;
    [SerializeField] private int groupIndex1 = 0;

    [Header("패널 2")]
    [SerializeField] private GameObject panel2;
    [SerializeField] private int groupIndex2 = 1;

    [Header("패널 3")]
    [SerializeField] private GameObject panel3;
    [SerializeField] private int groupIndex3 = 2;

    [Header("패널 4 (선택)")]
    [SerializeField] private GameObject panel4;
    [SerializeField] private int groupIndex4 = 3;

    [Header("설정")]
    [Tooltip("각 패널마다 한 번씩만 재생할지 여부. 타임라인을 되감아도 중복 재생되지 않습니다.")]
    [SerializeField] private bool playOnlyOnce = true;

    // 직전 프레임의 활성 상태 (꺼짐 -> 켜짐 전환을 잡기 위해)
    private readonly bool[] wasActive = new bool[4];
    private readonly bool[] played = new bool[4];

    private GameObject[] panels;
    private int[] groupIndexes;

    private void Start()
    {
        panels = new[] { panel1, panel2, panel3, panel4 };
        groupIndexes = new[] { groupIndex1, groupIndex2, groupIndex3, groupIndex4 };

        if (typeWriter == null)
            Debug.LogWarning("[PanelDialogueSequencer] typeWriter가 연결되어 있지 않습니다! 인스펙터에서 연결하세요.");

        // 시작 시점의 상태를 기준선으로 잡는다.
        // 이렇게 해야 씬 로드 직후의 활성 상태를 "방금 켜졌다"로 오해하지 않는다.
        int count = 0;
        for (int i = 0; i < panels.Length; i++)
        {
            wasActive[i] = panels[i] != null && panels[i].activeInHierarchy;
            if (panels[i] != null) count++;
        }

        Debug.Log($"[PanelDialogueSequencer] 패널 {count}개 감시 시작.");
    }

    private void Update()
    {
        if (typeWriter == null || panels == null) return;

        for (int i = 0; i < panels.Length; i++)
        {
            GameObject panel = panels[i];
            if (panel == null) continue;

            bool now = panel.activeInHierarchy;

            // 꺼져 있다가 방금 켜진 순간에만 반응한다.
            if (now && !wasActive[i])
            {
                if (playOnlyOnce && played[i])
                {
                    Debug.Log($"[PanelDialogueSequencer] '{panel.name}'은 이미 재생했으므로 건너뜁니다.");
                }
                else
                {
                    played[i] = true;
                    Debug.Log($"[PanelDialogueSequencer] '{panel.name}'이(가) 켜졌습니다 " +
                              $"-> 대사 그룹 {groupIndexes[i]}번 재생");
                    typeWriter.PlayDialogueGroup(groupIndexes[i]);
                }
            }

            wasActive[i] = now;
        }
    }
}
