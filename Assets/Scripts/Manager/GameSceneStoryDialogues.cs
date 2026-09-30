using UnityEngine;

// ────────────────────────────────────────────────────────────────
//  게임의 대본을 전부 여기 한 곳에 모아 둔 파일입니다.
//  게임이 켜질 때 이 내용으로 채워지므로, 인스펙터에서 고친 값은 덮어써집니다.
//  대사를 바꾸려면 이 파일만 고치세요.
//
//  - 대사창 그룹: 회상 영상 도중 대사(Item1~4), 인트로, 사망, 기억 뒤 대사, 마지막 승부
//  - 탐색 구간 글씨: 안내 문구, 열쇠/구급상자 문구, 증거 문서 3개
//  - 검은 화면(기억 파편) 안내 문구
// ────────────────────────────────────────────────────────────────
[RequireComponent(typeof(TypeWriter))]
public class GameSceneStoryDialogues : MonoBehaviour
{
    // 탐색 구간 오브젝트 이름 (씬에서 찾을 때 씀)
    private const string SubwaySegmentName = "FreeRoamSegment_1";   // 기억 1: 지하철
    private const string HospitalSegmentName = "FreeRoamSegment_2"; // 기억 2: 병원
    private const string Hospital2SegmentName = "FreeRoamSegment_3"; // 기억 3: 병원2 (드레일)
    private const string StreetSegmentName = "FreeRoamSegment";     // 기억 4: 거리

    private TypeWriter typeWriter;

    void Awake()
    {
        typeWriter = GetComponent<TypeWriter>();
        ApplyStoryDefaultDialogues();
    }

    public void ApplyStoryDefaultDialogues()
    {
        if (typeWriter == null)
            typeWriter = GetComponent<TypeWriter>();

        typeWriter.EnsureDialogueGroupSize(StoryDialogueIndex.Count);

        ApplyDialogueGroups();
        ApplyExplorationTexts();
    }

    private void ApplyDialogueGroups()
    {
        // 표기: 주인공 대사는 이름 없이, 다른 사람은 "이름: 대사", 상황 설명은 (괄호)

        // ── PROLOGUE + FIRST ROUND ──
        typeWriter.SetDialogueGroup(StoryDialogueIndex.IntroMain, "Intro_Main", new[]
        {
            "여긴...",
            "어디야?",
            "???: 일어났네.",
            "당신 누구야?",
            "???: 그건 나중에.",
            "뭐?",
            "???: 일단 앉아.",
            "날 납치한 거야?",
            "???: 그렇게 생각해도 돼.",
            "미친...",
            "저건 뭐야?",
            "???: 총.",
            "알아.",
            "???: 그럼 설명할 필요 없겠네.",
            "설마...",
            "???: 러시안 룰렛.",
            "장난하는 거지?",
            "???: 아니.",
            "왜 하필 나야?",
            "???: ...",
            "???: 네가 여기 있으니까.",
            "???: 먼저 해.",
            "싫다면?",
            "???: 그럼 내가 하지.",
            "...",
            "좋아."
        });

        // ── 1~2판에서 ???를 쐈을 때: 처음부터 다시 ──
        typeWriter.SetDialogueGroup(StoryDialogueIndex.WhoKilledFirst, "Who_Killed_First", new[]
        {
            "...",
            "분명히 쐈는데...",
            "???: 아직이야.",
            "???: 넌 아직 아무것도 기억하지 못했어."
        });

        typeWriter.SetDialogueGroup(StoryDialogueIndex.WhoKilledSecond, "Who_Killed_Second", new[]
        {
            "또...",
            "???: 몇 번을 쏴도 마찬가지야.",
            "???: 기억해 내기 전까지는."
        });

        // ── 1판에서 죽음: 짧은 환각 (법정) ──
        typeWriter.SetDialogueGroup(StoryDialogueIndex.PlayerDeathFirst, "Death_1_Court", new[]
        {
            "(법정. 책상 위에 서류가 놓여 있다.)",
            "낯선 목소리: 판사님.",
            "누구...",
            "낯선 목소리: 여기만 처리해주시면 됩니다."
        });

        typeWriter.SetDialogueGroup(StoryDialogueIndex.BackToPresent, "Back_To_Present", new[]
        {
            "하...",
            "???: 괜찮아?",
            "방금 뭐였지?",
            "???: 뭐가?",
            "사람이 있었어.",
            "???: 환각일 거야.",
            "..."
        });

        // ── ITEM 1: 휴대폰 (누르면 → 지하철 기억) ──
        typeWriter.SetDialogueGroup(StoryDialogueIndex.ItemUseFirst, "Item_1_Phone", new[]
        {
            "이거...",
            "???: 왜?",
            "누구 휴대폰이지?",
            "???: 확인해 봐.",
            "(휴대폰 화면 - Drail 제약과 주고받은 연락 기록)",
            "Drail 제약...",
            "이름이 익숙한데.",
            "???: 그래?",
            "모르겠어.",
            "???: 그럼 넘어가.",
            "잠깐.",
            "이 번호...",
            "내 연락처에 있었어.",
            "???: ..."
        });

        // ── SECOND ROUND ──
        typeWriter.SetDialogueGroup(StoryDialogueIndex.RoundStartSecond, "Round_2", new[]
        {
            "너 Drail 제약 알아?",
            "???: 알아.",
            "뭐 하는 곳이야?",
            "???: 제약회사.",
            "그런 건 나도 알아.",
            "???: 그럼 더 물어볼 필요 없겠네.",
            "...",
            "넌 내가 뭘 모르는지 알고 있지?",
            "???: 조금.",
            "뭔데?",
            "???: 게임부터 하자."
        });

        // ── 2판에서 죽음: 환각 (병원, 17번) ──
        typeWriter.SetDialogueGroup(StoryDialogueIndex.PlayerDeathSecond, "Death_2_Hospital", new[]
        {
            "(병원 복도. 한 여성이 침대에 누워 있다.)",
            "저 사람은...",
            "(모니터 - 17번)",
            "17..."
        });

        // ── ITEM 2: 혈액 (누르면 → 병원 기억) ──
        typeWriter.SetDialogueGroup(StoryDialogueIndex.ItemUseSecond, "Item_2_Blood", new[]
        {
            "이거 피야?",
            "???: 응.",
            "누구 거야?",
            "???: 17번.",
            "피험자?",
            "???: 그래.",
            "왜 내가 그걸 알고 있지...",
            "???: ...",
            "나 이 사건을 알아?",
            "???: 조금씩 기억나나 보네.",
            "대답해.",
            "???: 네가 맡았던 사건이야."
        });

        // ── ITEM 3: 약 (누르면 → 병원2 / 드레일의 연구실 기억) ──
        typeWriter.SetDialogueGroup(StoryDialogueIndex.ItemUseThird, "Item_3_Drug", new[]
        {
            "Psilo-D.",
            "이게 임상시험에 쓰인 약이야?",
            "???: 그래.",
            "무슨 약인데?",
            "???: 다이어트 약이라고 했지.",
            "\"했다고\"?",
            "???: 처음에는.",
            "그럼 실제로는?",
            "???: 사람들이 죽었어.",
            "(약병을 내려다본다.)",
            "...",
            "몇 명이나?",
            "???: 많아."
        });

        // ── 2판 죽음 도중: 드레일 이야기 (예전 THIRD ROUND) ──
        typeWriter.SetDialogueGroup(StoryDialogueIndex.RoundStartThird, "Round_3", new[]
        {
            "드레일.",
            "???: 왜?",
            "이 약 만든 사람이지?",
            "???: 그래.",
            "그 사람도 이걸 알고 있었어?",
            "???: 당연하지.",
            "그런데도 계속했어?",
            "???: 그 사람한테는 실패가 아니었으니까.",
            "..."
        });

        // ── 2판 죽음 도중 (드레일 이야기 뒤): 환각 (연구실, 드레일) ──
        typeWriter.SetDialogueGroup(StoryDialogueIndex.PlayerDeathThird, "Death_3_Lab", new[]
        {
            "(연구실.)",
            "드레일: 결과가 나쁘지 않군.",
            "...",
            "드레일: 조금만 조정하면 돼.",
            "사람이 죽었는데?",
            "(드레일이 주인공을 바라본다.)",
            "드레일: 그래서요?"
        });

        // ── ITEM 4: 구급상자 (누르면 → 거리 기억) ──
        typeWriter.SetDialogueGroup(StoryDialogueIndex.ItemUseForth, "Item_4_FirstAid", new[]
        {
            "이건 왜 있는 거야?",
            "???: 필요할 수도 있으니까.",
            "누가 준비한 건데?",
            "???: ...",
            "너야?",
            "???: 아니.",
            "(구급상자를 바라본다.)",
            "이상하네."
        });

        // ── TRAIL 등장 ──
        typeWriter.SetDialogueGroup(StoryDialogueIndex.TrailEnter, "Trail_Enter", new[]
        {
            "(문이 열린다.)",
            "트레일: 오랜만입니다.",
            "...",
            "당신...",
            "트레일: 기억나십니까?",
            "트레일.",
            "트레일: 네.",
            "Drail 제약 대표.",
            "트레일: 정확합니다.",
            "내가 당신을 왜 알고 있지?",
            "트레일: 재판 때문이죠."
        });

        // ── FOURTH ROUND - 트레일 ──
        typeWriter.SetDialogueGroup(StoryDialogueIndex.RoundStartFourth, "Round_4_Trail", new[]
        {
            "그 사건...",
            "트레일: Drail 제약 임상시험 사건.",
            "내가 판사였지.",
            "트레일: 네.",
            "결과는...",
            "트레일: 무죄였습니다.",
            "...",
            "트레일: 제가 부탁드렸잖습니까.",
            "무슨 부탁?",
            "(트레일이 웃는다.)",
            "트레일: 기억 안 나십니까?",
            "...",
            "트레일: 증거도 정리해 드렸고.",
            "...",
            "트레일: 대가도 드렸습니다.",
            "돈...",
            "트레일: 네.",
            "내가 돈을 받고...",
            "트레일: 판결을 내렸죠.",
            "닥쳐.",
            "트레일: 왜요?",
            "트레일: 이제 기억났잖습니까."
        });

        // ── 트레일을 죽임 → ENDING 2 ──
        typeWriter.SetDialogueGroup(StoryDialogueIndex.TrailKilledEnding, "Trail_Killed_Ending", new[]
        {
            "(트레일이 쓰러진다.)",
            "...",
            "???: 가자.",
            "어디로?",
            "???: 여기서 나가.",
            "너는?",
            "???: 난 할 일이 남았어.",
            "뭔데?",
            "(???가 대답하지 않는다.)"
        });

        // ── 트레일에게 죽음: 환각 (법정, 봉투) ──
        typeWriter.SetDialogueGroup(StoryDialogueIndex.PlayerDeathFourth, "Death_4_Envelope", new[]
        {
            "(법정. 주인공 앞에 서류가 놓여 있다.)",
            "트레일: 여기만 서명해 주시면 됩니다.",
            "이게 뭐죠?",
            "트레일: 증거 목록입니다.",
            "이걸 없애면...",
            "트레일: 문제없습니다.",
            "(봉투가 책상 위에 놓인다.)",
            "트레일: 약속드린 금액입니다.",
            "(봉투를 바라본다.)",
            "(잠시 후, 봉투를 가져간다.)"
        });

        typeWriter.SetDialogueGroup(StoryDialogueIndex.AfterFourthDeath, "After_Death_4", new[]
        {
            "...",
            "내가 그랬구나."
        });

        // ── 새로운 아이템: 17번의 사진과 기록 ──
        typeWriter.SetDialogueGroup(StoryDialogueIndex.NewItem, "New_Item_Record", new[]
        {
            "이건...",
            "???: 가져.",
            "뭔데?",
            "???: 보면 알 거야.",
            "(피험자 17번의 사진과 기록.)",
            "...",
            "여자였네.",
            "???: 그래.",
            "19살...",
            "???: 그래.",
            "사망 판정...",
            "내가 했어.",
            "???: ..."
        });

        // ── FIFTH ROUND (옆에 트레일의 시체) ──
        typeWriter.SetDialogueGroup(StoryDialogueIndex.RoundStartFifth, "Round_5", new[]
        {
            "(트레일의 시체가 옆에 있다.)",
            "...",
            "처음부터 이럴 생각이었어?",
            "???: 응.",
            "트레일도 죽이고.",
            "???: 그래.",
            "나도 죽일 거고?",
            "???: 그래.",
            "그럼 왜 지금까지 살려둔 거야?",
            "???: 확인할 게 있었어.",
            "뭘?",
            "???: 네가 어디까지 기억하는지.",
            "...",
            "너...",
            "피험자 17번.",
            "???: ...",
            "그 사람과 무슨 관계야?",
            "???: 내 딸이야.",
            "...",
            "???: 19살이었어.",
            "미안하다.",
            "???: 그 말 들으려고 여기까지 온 거 아니야.",
            "그럼 뭘 원하는데?",
            "???: 네 목숨.",
            "???: 시작하자."
        });

        // ── ???에게 사망 → 사망 엔딩 ──
        typeWriter.SetDialogueGroup(StoryDialogueIndex.PlayerDeathFifth, "Death_5_Ending", new[]
        {
            "???: 끝났어."
        });

        // ── ???를 죽임 → 문서 확인 ──
        typeWriter.SetDialogueGroup(StoryDialogueIndex.WhoKilledFinal, "Who_Killed_Final", new[]
        {
            "(총을 내려놓는다.)",
            "(주변에 흩어진 문서들이 보인다.)",
            "(문서를 하나씩 확인한다.)"
        });

        // 문서가 충분한 경우 → 자수 엔딩
        typeWriter.SetDialogueGroup(StoryDialogueIndex.ConfessionA, "Ending_Confession_A", new[]
        {
            "이걸 가지고 나가면...",
            "전부 밝힐 수 있어.",
            "...",
            "나도 포함해서."
        });

        typeWriter.SetDialogueGroup(StoryDialogueIndex.ConfessionB, "Ending_Confession_B", new[]
        {
            "(경찰서.)",
            "(서류를 내민다.)",
            "제가 알고 있는 사건입니다.",
            "그리고 제가 숨긴 사건이기도 합니다."
        });

        // 문서가 부족한 경우 → 괴물 엔딩
        typeWriter.SetDialogueGroup(StoryDialogueIndex.MonsterA, "Ending_Monster_A", new[]
        {
            "이걸로는 부족해.",
            "(약병을 바라본다.)",
            "..."
        });

        typeWriter.SetDialogueGroup(StoryDialogueIndex.MonsterB, "Ending_Monster_B", new[]
        {
            "드레일: 조금만 더 기다려보죠.",
            "(시야가 흐려진다.)",
            "안 돼..."
        });

        // (쓰지 않음) 예전 마지막 승부 규칙 설명
        typeWriter.SetDialogueGroup(StoryDialogueIndex.FinalRules, "Final_Rules_Unused", new string[0]);

        // ── 회상 영상 도중 대사 (타임라인 마커) ──
        typeWriter.SetDialogueGroup(0, "Item1", new[]
        {
            "누구시죠?",
            "트레일: Drail 제약회사의 사장입니다.",
            "트레일: 재판과 관련해 부탁드릴 게 있습니다.",
            "무슨 사건이죠?",
            "트레일: 저희 회사의 임상시험과 관련된 사건입니다.",
            "트레일: 이번 재판을 조용히 끝내주셨으면 합니다.",
            "대가가 있다는 말입니까?",
            "트레일: 물론입니다.",
            "......",
            "그렇다면 이야기가 쉽겠군요.",
            "트레일: 사건 자료는 역 근처 공중화장실, 맨 안쪽 칸에 두었습니다.",
            "트레일: 읽어 보시면... 무엇을 덮어야 할지 아실 겁니다."
        });

        typeWriter.SetDialogueGroup(1, "Item2", new[]
        {
            "(도대체 어떤 사건이길래...)",
            "......",
            "저건 뭐지?",
            "짐승의 발...?",
            "설마 저게..."
        });

        typeWriter.SetDialogueGroup(2, "Item3", new[]
        {
            "드레일: 오... 드디어 오셨군요.",
            "당신이 드레일인가?",
            "드레일: 이 연구의 책임자죠.",
            "드레일: 처음에는 단순한 약이었습니다. 살을 빼는 약.",
            "드레일: 결과가 아주 흥미롭게 나왔습니다.",
            "그 과정에서 사람이 죽었지?",
            "드레일: 몇 명 죽은 게 뭐가 중요합니까?",
            "드레일: 중요한 건 결과입니다.",
            "......",
            "나는 이걸 보고도...",
            "아무것도 하지 않았어."
        });

        typeWriter.SetDialogueGroup(3, "Item4", new[]
        {
            "재판은 끝났습니다.",
            "트레일: 역시 당신에게 맡기길 잘했군요.",
            "제 보수는?",
            "트레일: 구급상자에 넣어 두었습니다.",
            "트레일: 눈에 띄지 않을 겁니다.",
            "......",
            "찾아가도록 하죠."
        });
    }

    // ────────────────────────────────────────────────────────────
    //  탐색 구간 / 검은 화면 글씨
    // ────────────────────────────────────────────────────────────
    private void ApplyExplorationTexts()
    {
        // ── 검은 화면(기억 파편) 안내 ──
        var fragmentScreen = FindFirstObjectByType<MemoryFragmentScreen>(FindObjectsInactive.Include);
        if (fragmentScreen != null)
            fragmentScreen.SetClickHint("기억 파편을 눌러 기억을 되찾으세요.");

        // ── 기억 1: 지하철 ──
        //  둘러보며 단서(선택) → 걷다가 갑자기 전화벨 → [E] 전화 받기 → First 영상(통화 장면)
        //  → 영상이 멈추고 다시 탐색: 화장실 맨 안쪽 칸의 사건 자료 → 중요한 곳에 줄 긋기
        //    (2개 그으면 문을 쾅쾅 두드림 + 역무원 "역 마감합니다" → "잠시만요" → 나머지 2개)
        //  → 승강장으로 돌아가기 → 영상 이어서(테이블 복귀) → 추리 질문 → Item_Use_First 대사
        FlashbackFreeRoamSegment subway = FindSegment(SubwaySegmentName);
        if (subway != null)
        {
            subway.SetHintTexts(
                controls: "WASD 이동  /  마우스 시점  /  Tab 기억 노트",
                explore: "주변을 둘러보자",
                call: "전화가 울린다",
                callPromptText: "[E] 전화 받기");

            FreeRoamPickupItem[] m1 = subway.OptionalClues;
            SetClue(m1, 0, "m1_card", "트레일의 명함",
                "Drail 제약 대표이사 트레일.\n\n" +
                "뒷면에 손글씨로 적혀 있다.\n" +
                "'판사님, 조용히 끝나면 섭섭지 않게 사례하겠습니다.'",
                "명함이다. 뒷면에 뭔가 적혀 있어.");
            SetClue(m1, 1, "m1_news", "구겨진 신문",
                "[사회] Drail 제약 임상시험 참가자 잇단 사망\n\n" +
                "유족들은 '약 때문'이라고 주장.\n" +
                "회사 측은 '관련 없다'며 부인.\n" +
                "첫 재판은 다음 주.",
                "신문이다. Drail... 임상시험?");
            SetClue(m1, 2, "m1_schedule", "재판 일정 메모",
                "Drail 제약 사건 - 담당 판사: 나\n\n" +
                "선고일에 붉은 동그라미가 쳐져 있다.\n" +
                "그 옆에 내 글씨로 '조용히'.",
                "내 글씨야... '조용히'?");

            // 통화 뒤: 화장실에 둔 사건 자료
            subway.SetAfterCallTexts(
                find: "역 화장실 맨 안쪽 칸에서 사건 자료를 찾으세요",
                back: "승강장으로 돌아가세요");

            InteractSpot caseFile = subway.AfterCallInteract;
            if (caseFile != null)
            {
                caseFile.SetTexts(
                    newPrompt: "[E] 사건 자료 읽기",
                    newMessage: "...이걸 덮으라는 거군.",
                    title: "사건 자료 (화장실)",
                    text: "트레일이 화장실에 두고 간 봉투. 안에는 현금 다발 사진도 함께 들어 있었다.");

                if (caseFile.UnderlineDocument != null)
                    caseFile.UnderlineDocument.SetContent("[대외비] Drail 제약 임상시험 사건 요약", new[]
                    {
                        "사건번호 2024고합17 - 피고 Drail 제약",
                        "*체중감량 신약 'Psilo-D' 3상 임상시험 중 참가자 3명 사망",
                        "시험 기간: 8주 / 참가자 24명",
                        "*사망자 중 1명은 19세 여성, 피험자 17번",
                        "유족 측: '약물 부작용으로 인한 사망' 주장",
                        "*회사 내부 보고서에 '부작용 은폐' 지시 기록 있음",
                        "담당 연구 책임자: 드레일 박사",
                        "*선고 전까지 모든 기록은 본사를 거쳐 법원에 제출",
                        "- 판사님의 현명한 판단을 기대합니다. T"
                    },
                    wrong: "...이건 중요하지 않아.",
                    done: "...이 정도면 됐어. 이제 뭘 덮어야 할지 알겠어.");

                // 줄을 2개 그으면 칸막이 문을 쾅쾅 두드린다 (소리 점프 스퀘어) → 역무원 대사 → 마저 긋기
                if (caseFile.UnderlineDocument != null)
                    caseFile.UnderlineDocument.SetInterruption(2, new[]
                    {
                        "역무원: 아저씨! 역 마감합니다. 나오셔야 해요!",
                        "잠, 잠시만요...!"
                    });
            }

            // 영상(통화)이 끝나고 테이블로 돌아온 뒤에 묻는다.
            if (subway.Deduction != null)
                subway.Deduction.SetContent(
                    "트레일이 나에게 원한 것은?",
                    new[] { "재판을 조용히 끝내 주는 것", "임상시험을 중단시키는 것", "유족에게 사과하는 것" },
                    0,
                    "...아니야. 그 전화에서 뭐라고 했더라.",
                    "그래... 트레일은 재판을 덮어 달라고 했어.");
        }

        // ── 기억 2: 병원 탐색 ──
        FlashbackFreeRoamSegment hospital = FindSegment(HospitalSegmentName);
        if (hospital != null)
        {
            hospital.SetHintTexts(
                controls: "WASD 이동  /  마우스 시점  /  F 손전등  /  Tab 기억 노트",
                pickup: "Drail 실험실 열쇠를 찾으세요",
                evidence: "증거를 찾아 없애세요  ({0}/{1})",
                evidenceFocus: "[E] 살펴보기",
                evidenceDone: "...이제 아무도 모를 거야.",
                final: "이제 여기서 나가야 해.",
                finalReached: "여기구나...");

            // 열쇠: 북동쪽 잠긴 방(사망 진단서가 있는 방)을 연다. (문은 기억 노트의 "m2_key"를 확인)
            if (hospital.PickupItem != null)
            {
                hospital.PickupItem.SetClue("m2_key", "Drail 실험실 열쇠",
                    "'기록 보관실'이라고 적힌 꼬리표가 달린 열쇠.\n\n" +
                    "병원 북쪽 잠긴 방의 열쇠인 것 같다.", 0);
                hospital.PickupItem.SetPickupMessage("Drail 실험실 열쇠...\n이걸로 잠긴 방을 열 수 있겠어.");
            }

            FreeRoamPickupItem[] m2 = hospital.OptionalClues;
            SetClue(m2, 0, "m2_nurse", "간호사의 쪽지",
                "17번 환자 보호자(아버지)가 오늘도 찾아왔다.\n" +
                "딸을 보여 달라고 복도에서 소리쳤다.\n\n" +
                "윗선 지시로 면회 금지.\n" +
                "...저 사람, 계속 이러다 무슨 일 날 것 같다.",
                "간호사가 남긴 쪽지다.");
            SetClue(m2, 1, "m2_order", "Drail 공문",
                "[대외비]\n\n" +
                "임상시험 관련 모든 기록은\n" +
                "법원 제출 전 반드시 본사 검토를 거칠 것.\n\n" +
                "- 대표이사 트레일",
                "회사 공문이다. 법원 제출 전에... 검토?");

            // 영상(구울)이 끝나고 테이블로 돌아온 뒤에 묻는다.
            if (hospital.Deduction != null)
                hospital.Deduction.SetContent(
                    "나는 이 병원에서 무엇을 했나?",
                    new[] { "임상시험 기록을 조작하고 증거를 없앴다", "환자들을 끝까지 치료했다", "기자에게 증거를 넘겼다" },
                    0,
                    "...아니야. 그 서류들에 무슨 짓을 했는지 떠올려 봐.",
                    "그래... 내가 기록을 고치고, 진단서를 찢었어.");

            // 증거는 FreeRoamSegment_2의 Evidence Items 순서대로 1, 2, 3
            EvidenceItem[] evidence = hospital.EvidenceItems;

            SetEvidence(evidence, 0,
                "Drail 제약 - 배합 기록 #M-07",
                "원료: 환각성 버섯 추출물 (Psilo-D) 38%\n" +
                "안정제 12% / 정제수 50%\n\n" +
                "※ 투여 후 12시간 이내 급성 부작용 다수 보고.\n" +
                "※ 동물실험 단계에서 변이 확인됨.\n\n" +
                "책임 연구원: 드레일",
                "원료: 식물성 추출물 38%\n" +
                "안정제 12% / 정제수 50%\n\n" +
                "※ 특이사항 없음.\n\n\n" +
                "책임 연구원: 드레일",
                "...버섯에 대한 기록은 남기면 안 돼.");

            SetEvidence(evidence, 1,
                "임상시험 3상 - 피험자 경과 기록",
                "피험자 24명\n" +
                "이상 반응 9명 / 사망 3명\n\n" +
                "사망 원인: 약물 투여 후 급성 신체 변이\n" +
                "피험자 17번 - 형태 변화 관찰됨\n\n" +
                "판정: 시험 즉시 중단 요망",
                "피험자 24명\n" +
                "이상 반응 0명 / 사망 0명\n\n\n\n" +
                "판정: 안전성 확인",
                "숫자 몇 개만 바꾸면 돼.");

            // 찢어 없애기: "조작 후 내용" 칸 = 찢은 뒤 남는 한 줄
            SetEvidence(evidence, 2,
                "사망 진단서 - 피험자 17번",
                "성명: 이○○ (19세, 여)\n\n" +
                "사망 원인: 임상시험 약물에 의한 급성 장기 부전\n\n" +
                "담당의 소견:\n" +
                "약물과 사망 사이의 인과관계가 명백함.",
                "찢어서... 없애버렸다.",
                "이 아이의 죽음도...\n없었던 일로 만들면 돼.");
        }

        // ── 기억 3: 병원2 ──
        //  복도 북쪽 끝에서 시작 → 동쪽 방에서 출입 카드(구울 점프 스퀘어) → 잠긴 문을 카드로 열기
        //  → 남쪽 연구실 도착 → Third 영상(드레일 등장) → 테이블로 돌아와 추리 질문 → Item_Use_Third
        FlashbackFreeRoamSegment hospital2 = FindSegment(Hospital2SegmentName);
        if (hospital2 != null)
        {
            hospital2.SetHintTexts(
                controls: "WASD 이동  /  마우스 시점  /  Tab 기억 노트",
                pickup: "연구실 출입 카드를 찾으세요",
                final: "드레일의 연구실로 가세요",
                finalReached: "여기가... 드레일의 연구실.");

            if (hospital2.PickupItem != null)
            {
                hospital2.PickupItem.SetClue("m3_keycard", "연구실 출입 카드",
                    "Drail 제약 연구동 출입 카드.\n\n" +
                    "소유자: 드레일 박사\n" +
                    "출입 가능 구역: 지하 연구실", 0);
                hospital2.PickupItem.SetPickupMessage("출입 카드다. 이걸로 연구실 문을 열 수 있을 거야.");
            }

            FreeRoamPickupItem[] m3 = hospital2.OptionalClues;
            SetClue(m3, 0, "m3_log", "실험 일지",
                "Psilo-D 3차 투여 기록\n\n" +
                "피험자 17번 - 피부 변색, 손톱이 짐승 발톱처럼 자람.\n" +
                "체중 감소 효과는 탁월.\n\n" +
                "드레일 박사 소견: '예상 밖의 성과. 계속 진행.'",
                "실험 일지... 17번?");
            SetClue(m3, 1, "m3_roster", "피험자 명단",
                "3상 피험자 24명\n\n" +
                "17번 이○○ (19세, 여) - 이름 옆에 붉은 X 표시.\n" +
                "그 밑에 '비공개 처리' 도장.",
                "명단이다. 17번에 X 표시가 있어.");
            SetClue(m3, 2, "m3_memo", "트레일의 메모",
                "판사님이 오시면 연구실로 안내할 것.\n" +
                "우리 '결과물'을 직접 보여 드려라.\n" +
                "그래야 입을 다물 테니까.\n\n" +
                "- T",
                "트레일의 메모... 나를 기다리고 있었어.");

            // 영상(드레일)이 끝나고 테이블로 돌아온 뒤에 묻는다.
            if (hospital2.Deduction != null)
                hospital2.Deduction.SetContent(
                    "드레일의 실험은 사람들에게 무슨 일을 일으켰나?",
                    new[] { "살을 빼는 약이 사람을 괴물로 바꿨다", "아무 부작용 없이 끝났다", "피험자들이 모두 병이 나았다" },
                    0,
                    "...아니야. 실험 일지를 다시 떠올려 봐.",
                    "그래... 그 약이 사람을 괴물로 만들었어.");
        }

        // ── 기억 4: 거리 ──
        //  (Forth 영상이 멈춘 뒤) 목격자를 피해 돈 가방 + 차 키 → 경적으로 차 찾기 → 차 타고 떠남
        //  → 영상 끝, 테이블로 돌아온 뒤 추리 질문 → Item_Use_Forth
        FlashbackFreeRoamSegment street = FindSegment(StreetSegmentName);
        if (street != null)
        {
            street.SetHintTexts(
                controls: "WASD 이동  /  마우스 시점  /  Tab 기억 노트",
                clues: "사람들 눈을 피해 구급상자와 차 키를 찾으세요  ({0}/{1})",
                search: "[E] 차 키 버튼  -  가까울수록 경적이 크게 들린다",
                found: "찾았다...");

            FreeRoamPickupItem[] m4 = street.ClueItems;
            SetClue(m4, 0, "m4_money", "구급상자 속 돈",
                "구급상자 안에 붕대 대신 현금 다발이 빽빽하게 들어 있다.\n\n" +
                "안쪽 주머니에 쪽지 한 장.\n" +
                "'약속대로입니다. 앞으로도 잘 부탁드립니다. - T'",
                "구급상자... 안에 든 건 돈이야. 이게 그 '보수'인가.");
            SetClue(m4, 1, "m4_key", "차 키",
                "내 차 키.\n\n" +
                "열쇠고리에 법원 직원 주차증이 달려 있다.",
                "차 키다. 이제 차만 찾으면 돼.");

            FreeRoamPickupItem[] m4opt = street.OptionalClues;
            SetClue(m4opt, 0, "m4_verdict", "판결문 초안",
                "피고 Drail 제약 - 무죄.\n\n" +
                "'임상시험 약물과 참가자 사망 사이의\n" +
                " 인과관계를 인정할 증거가 없다.'\n\n" +
                "맨 아래에 내 서명.",
                "판결문... 내 서명이 있어.");

            // 영상이 끝나고 테이블로 돌아온 뒤에 묻는다.
            if (street.Deduction != null)
                street.Deduction.SetContent(
                    "나는 그 돈을 받고 무엇을 했나?",
                    new[] { "증거가 없다며 무죄를 선고했다", "돈을 돌려주고 회사를 고발했다", "유족에게 돈을 나눠 줬다" },
                    0,
                    "...아니야. 그 판결문의 서명을 떠올려 봐.",
                    "그래... 나는 돈을 받고 무죄를 선고했어.");
        }
    }

    // 단서 오브젝트(주우면 기억 노트에 적힘). message = 주웠을 때 화면 아래 한 줄
    private static void SetClue(FreeRoamPickupItem[] items, int index, string id, string title, string text, string message)
    {
        if (items == null || index >= items.Length || items[index] == null)
        {
            Debug.LogWarning($"[GameSceneStoryDialogues] 단서 {index + 1}번 '{title}'을(를) 넣을 오브젝트가 없습니다.");
            return;
        }
        items[index].SetClue(id, title, text, 0);
        items[index].SetPickupMessage(message);
    }

    private static void SetEvidence(EvidenceItem[] items, int index, string title, string original,
                                    string tampered, string thought)
    {
        if (items == null || index >= items.Length || items[index] == null)
        {
            Debug.LogWarning($"[GameSceneStoryDialogues] 증거 {index + 1}번이 FreeRoamSegment_2에 없어 문구를 넣지 못했습니다.");
            return;
        }
        items[index].SetTexts(title, original, tampered, thought);
    }

    private static FlashbackFreeRoamSegment FindSegment(string objectName)
    {
        foreach (var seg in FindObjectsByType<FlashbackFreeRoamSegment>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            if (seg.gameObject.name == objectName)
                return seg;

        Debug.LogWarning($"[GameSceneStoryDialogues] '{objectName}' 탐색 구간을 찾지 못해 안내 문구를 넣지 못했습니다.");
        return null;
    }
}
