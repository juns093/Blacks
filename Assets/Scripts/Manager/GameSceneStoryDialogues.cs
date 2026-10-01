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
        // 표기: 주인공 대사는 이름 없이, 다른 사람은 "이름: 대사"
        //       '@'로 시작하는 줄은 글자가 아니라 카메라 연출 신호 (DialogueCues 참고)
        //         @focus gun / opponent / who / trail / corpse  → 그쪽으로 고개를 돌리고 렌즈를 당긴다
        //         @back                                         → 원래 자리로
        //       상황 설명 (괄호) 줄은 쓰지 않는다. 화면(연출)으로 보여 준다.
        //
        // ── 이야기 ──
        //  드레일 박사는 다이어트 약이라고 속이고, 버섯(Psilo-D)으로 몸을 바꿔 괴물을 만드는 임상시험을 몰래 했다.
        //  사람들이 하나둘 실종되자 경찰이 움직이기 시작했다.
        //  Drail 대표 트레일은 판사(주인공)에게 증거 인멸과 무죄 선고를 청탁했고, 판사는 돈에 눈이 멀어 망설임 없이 그렇게 했다.
        //  그 임상시험에서 딸(피험자 17번)을 잃은 아버지(???)는 복수를 다짐했다.
        //  ???는 먼저 드레일을 납치했고, 그의 연구실에서 환각제를 가져왔다. 드레일은 ???의 손에 죽었다.
        //  그리고 판사를 납치해 이곳으로 데려왔다.
        //  실탄 안에는 그 환각제가 들어 있다. (버섯 Psilo-D와는 다른 약) 맞으면 죽지 않고, 대신 자기가 한 일을 보게 된다.
        //  (마지막 판의 탄은 약이 아니라 진짜다)

        // ── PROLOGUE + FIRST ROUND ──
        typeWriter.SetDialogueGroup(StoryDialogueIndex.IntroMain, "Intro_Main", new[]
        {
            "......",
            "여긴... 어디야.",
            "@focus opponent",
            "???: 일어났군.",
            "당신 누구야? 이거 풀어.",
            "???: 앉아.",
            "내가 누군지 알고 이러는 거야? 나 판사야.",
            "???: 알아. 그래서 데려온 거야.",
            "...돈이야? 얼마면 돼. 원하는 만큼 줄게.",
            "???: 역시 그 말부터 나오는군.",
            "@focus gun",
            "???: 러시안 룰렛이야.",
            "???: 빨간 탄은 실탄, 초록 탄은 빈 탄. 몇 발 들었는지는 보여 주지. 순서는 몰라.",
            "???: 네 차례엔 너 자신이나 나를 쏴. 너를 쏴서 빈 탄이면 한 번 더 쏠 수 있어.",
            "@back",
            "이게 무슨 짓이야...",
            "???: 하나 더. 실탄 안에는 환각제가 들어 있어.",
            "환각제?",
            "???: 맞아도 죽진 않아. 대신 보게 되지.",
            "???: 어디서 구했는지는... 곧 알게 될 거야.",
            "???: 먼저 쏴."
        });

        // ── 1~3판에서 ???를 죽임 → ENDING 1 ──
        typeWriter.SetDialogueGroup(StoryDialogueIndex.EndingOne, "Ending_1", new[]
        {
            "......",
            "...약이라며. 죽진 않는다며.",
            "하...",
            "...내가 쏜 거야. 저 사람이 누군지도 모르는데.",
            "...나가야 해. 여기서."
        });

        // ── 아이템을 하나라도 쓴 뒤(2~3판) ???를 죽임: 쓰러진 채 딸을 그리는 마지막 말 → 이어서 ENDING 1 ──
        typeWriter.SetDialogueGroup(StoryDialogueIndex.EndingOneDying, "Ending_1_Dying", new[]
        {
            "@focus opponent",
            "???: ......",
            "???: 이상하네... 하나도 안 아파.",
            "???: 그 애도 어릴 때 넘어지면... 꼭 그렇게 말했어. 하나도 안 아프다고.",
            "???: 무릎이 다 까져서는... 웃으면서.",
            "???: 그날도... 금방 온다고 했는데.",
            "???: 현관 불을... 켜 둬야 하는데.",
            "???: 이제... 마중 나갈게."
        });

        // ── 맞았을 때의 환각: 지금은 4판(트레일에게 맞음)만 쓴다. 1~3판은 대사 없이 화면만 일그러진다. ──
        typeWriter.SetDialogueGroup(StoryDialogueIndex.PlayerDeathFirst, "Unused_Death_1", new string[0]);
        typeWriter.SetDialogueGroup(StoryDialogueIndex.PlayerDeathSecond, "Unused_Death_2", new string[0]);
        typeWriter.SetDialogueGroup(StoryDialogueIndex.PlayerDeathThird, "Unused_Death_3", new string[0]);

        typeWriter.SetDialogueGroup(StoryDialogueIndex.BackToPresent, "Unused_Back", new string[0]);

        // ── ITEM 1: 휴대폰 (누르면 → 지하철 기억) ──
        typeWriter.SetDialogueGroup(StoryDialogueIndex.ItemUseFirst, "Item_1_Phone", new[]
        {
            "나... 맞았는데. 살아 있어?",
            "???: 말했잖아. 환각제라고.",
            "???: 그 휴대폰, 네 거야.",
            "통화 기록... Drail 제약?",
            "???: 그날 밤, 지하철역에서 받은 전화야.",
            "???: 기억해 봐."
        });

        // ── SECOND ROUND ──
        typeWriter.SetDialogueGroup(StoryDialogueIndex.RoundStartSecond, "Round_2", new[]
        {
            "트레일... 그 사람이 나한테 전화를 했어.",
            "???: 그래서?",
            "재판을 조용히 끝내 달라더군.",
            "???: 그리고 넌?",
            "......",
            "그게 뭐. 판사가 청탁 하나 받는 게 그렇게 대단한 일이야?",
            "???: 그때도 그렇게 생각했겠지.",
            "@focus gun",
            "???: 계속해."
        });

        // ── ITEM 2: 혈액 (누르면 → 병원 기억) ──
        typeWriter.SetDialogueGroup(StoryDialogueIndex.ItemUseSecond, "Item_2_Blood", new[]
        {
            "피...? 누구 거야.",
            "???: 라벨을 봐. 17번.",
            "17번이 뭔데.",
            "???: 피험자 번호.",
            "Drail 제약 임상시험...",
            "???: 다이어트 약이라고 사람들을 모았지.",
            "???: 그리고 그 사람들이 하나둘 사라졌어.",
            "...실종?",
            "???: 경찰이 움직이기 시작했을 때, 넌 어디 있었을까."
        });

        // ── ITEM 3: 약 (누르면 → 병원2 / 드레일의 연구실 기억) ──
        //    총알 속 환각제와 버섯(Psilo-D)은 다른 약이다. 환각제는 ???가 드레일을 납치해서 가져왔다.
        typeWriter.SetDialogueGroup(StoryDialogueIndex.ItemUseThird, "Item_3_Drug", new[]
        {
            "Psilo-D... 이게 총알에 든 그 약이야?",
            "???: 아니. 이건 버섯이고, 총알에 든 건 환각제야.",
            "???: 둘 다 드레일한테서 나온 거지만.",
            "드레일...",
            "???: 다이어트 약이라고 팔았지. 안에 든 건 몸을 바꿔 버리는 버섯.",
            "???: 환각제는 그 사람 연구실 금고에 있더군. 그 사람을 데려왔을 때.",
            "데려와? 드레일도... 여기로?",
            "???: 너처럼.",
            "그럼 드레일은 지금 어디 있는데.",
            "???: 죽었어. 내 손에.",
            "......",
            "???: 너도 봤잖아. 그 연구실에서."
        });

        // ── THIRD ROUND ──
        typeWriter.SetDialogueGroup(StoryDialogueIndex.RoundStartThird, "Round_3", new[]
        {
            "드레일... 그 사람은 처음부터 알고 있었어.",
            "???: 그래.",
            "그럼 그 사람 잘못이지. 난 아니야.",
            "???: 넌 그걸 다 보고도 서명했어.",
            "...증거가 없었어.",
            "???: 네가 없앴으니까.",
            "......",
            "@focus gun",
            "???: 다음 판."
        });

        // ── ITEM 4: 구급상자 (누르면 → 거리 기억) ──
        typeWriter.SetDialogueGroup(StoryDialogueIndex.ItemUseForth, "Item_4_FirstAid", new[]
        {
            "구급상자...? 안에 든 건 붕대가 아니라 돈이야.",
            "???: 기억나?",
            "...내 몫.",
            "???: 얼마였는지도 기억나나?",
            "......",
            "???: 사람 목숨 값치고는 싸더군."
        });

        // ── TRAIL 등장 (암전 뒤 트레일이 맞은편에 앉아 있다. ???는 옆에 서 있다) ──
        typeWriter.SetDialogueGroup(StoryDialogueIndex.TrailEnter, "Trail_Enter", new[]
        {
            "트레일: 이거 놔! 내가 누군지 알아?",
            "@focus opponent",
            "트레일...?",
            "트레일: 판사님? 판사님이 왜 여기...",
            "@focus who",
            "???: 둘이 아는 사이지.",
            "???: 이번 판은 둘이서 해. 난 옆에서 볼 테니까.",
            "@back"
        });

        // ── FOURTH ROUND - 트레일 ──
        typeWriter.SetDialogueGroup(StoryDialogueIndex.RoundStartFourth, "Round_4_Trail", new[]
        {
            "@focus opponent",
            "트레일: 판사님, 저 사람 말 듣지 마세요. 같이 나갈 방법을...",
            "그날 밤 나한테 전화한 거, 당신이지.",
            "트레일: ...그게 지금 중요합니까?",
            "증거도 정리해 주고, 돈도 줬지.",
            "트레일: 판사님도 좋다고 받으셨잖아요.",
            "트레일: 사람들이 사라지고 경찰이 냄새를 맡기 시작했을 때,",
            "트레일: 판사님이 무죄만 선고하면 끝나는 일이었습니다.",
            "트레일: 그리고 판사님은 그렇게 하셨죠. 망설이지도 않고.",
            "......",
            "@focus who",
            "???: 시작해.",
            "@back"
        });

        // ── 트레일을 죽임 → ENDING 2 ──
        typeWriter.SetDialogueGroup(StoryDialogueIndex.TrailKilledEnding, "Trail_Killed_Ending", new[]
        {
            "......",
            "@focus who",
            "???: 이걸로 하나.",
            "이제 됐지? 날 보내 줘.",
            "???: 그래. 가.",
            "...진짜로?",
            "???: 넌 오늘 사람을 죽였어. 네 손으로.",
            "???: 그 무게를 안고 살아. 네가 덮어 버린 사람들처럼.",
            "@back"
        });

        // ── 트레일에게 맞음: 환각 (판사실, 돈 봉투) ──
        typeWriter.SetDialogueGroup(StoryDialogueIndex.PlayerDeathFourth, "Death_4_Envelope", new[]
        {
            "트레일: 판사님, 증거 목록입니다. 표시된 것만 빼 주시면 됩니다.",
            "실종자 가족들이 시끄럽던데.",
            "트레일: 경찰 쪽은 저희가 정리하겠습니다. 판사님은 판결만.",
            "트레일: 약속드린 금액입니다.",
            "좋습니다. 무죄로 하죠."
        });

        typeWriter.SetDialogueGroup(StoryDialogueIndex.AfterFourthDeath, "After_Death_4", new[]
        {
            "...내가 그랬어.",
            "망설이지도 않았어."
        });

        // ── 새로운 아이템: 17번의 사진과 기록 ──
        typeWriter.SetDialogueGroup(StoryDialogueIndex.NewItem, "New_Item_Record", new[]
        {
            "???: 이것도 봐.",
            "???: 피험자 17번. 열아홉 살.",
            "...이 사람.",
            "???: 내 딸이야.",
            "......",
            "???: 살 좀 빼고 싶다고 했어. 그게 다였어.",
            "???: 연락이 끊기고, 경찰이 수사를 시작했고... 넌 그걸 덮었지.",
            "???: 딸을 묻던 날, 다짐했어."
        });

        // ── FIFTH ROUND (옆에 트레일의 시체) ──
        typeWriter.SetDialogueGroup(StoryDialogueIndex.RoundStartFifth, "Round_5", new[]
        {
            "@focus corpse",
            "...죽였어?",
            "@focus opponent",
            "???: 그래.",
            "처음부터 이럴 생각이었지. 트레일도, 나도.",
            "???: 처음부터.",
            "그럼 그 약은 왜 쓴 건데. 그냥 죽이면 됐잖아.",
            "???: 네가 무슨 짓을 했는지 알고 죽어야 하니까.",
            "......",
            "미안하다고 하면 돼? 미안해. 됐어?",
            "???: 아니.",
            "@focus gun",
            "???: 이번 탄에는 약이 없어.",
            "@back",
            "???: 시작하자."
        });

        // ── ???에게 죽음 → 사망 엔딩 (화면은 이미 검다) ──
        typeWriter.SetDialogueGroup(StoryDialogueIndex.PlayerDeathFifth, "Death_5_Ending", new[]
        {
            "???: 끝났어.",
            "???: 이제... 집에 가자."
        });

        // ── ???를 죽임 → 기억 속에서 모은 문서에 따라 자수 / 괴물 ──
        typeWriter.SetDialogueGroup(StoryDialogueIndex.WhoKilledFinal, "Who_Killed_Final", new[]
        {
            "@focus opponent",
            "......",
            "@back",
            "끝났어...",
            "기억이... 전부 돌아왔어."
        });

        // 문서를 충분히 모음 → 자수 엔딩
        typeWriter.SetDialogueGroup(StoryDialogueIndex.ConfessionA, "Ending_Confession_A", new[]
        {
            "통화 기록, 사건 자료, 실험 일지, 판결문...",
            "전부 기억나. 어디에 뭐가 있는지까지.",
            "이걸 말하면 트레일도, 드레일도...",
            "...나도."
        });

        typeWriter.SetDialogueGroup(StoryDialogueIndex.ConfessionB, "Ending_Confession_B", new[]
        {
            "자수하러 왔습니다.",
            "Drail 제약 임상시험 사건. 실종된 사람들.",
            "제가 덮었습니다.",
            "돈을 받고, 증거를 없애고, 무죄를 선고했습니다."
        });

        // 모자람 → 괴물 엔딩
        typeWriter.SetDialogueGroup(StoryDialogueIndex.MonsterA, "Ending_Monster_A", new[]
        {
            "흩어진 기억뿐이야. 증거라고 할 만한 건 없어.",
            "아무도 모르겠지.",
            "Psilo-D... 아직 남아 있네.",
            "살아서 나가려면... 뭐든 해야지."
        });

        typeWriter.SetDialogueGroup(StoryDialogueIndex.MonsterB, "Ending_Monster_B", new[]
        {
            "손이... 왜 이래.",
            "드레일: 결과가 흥미롭군요, 판사님.",
            "하... 하하하..."
        });

        typeWriter.SetDialogueGroup(StoryDialogueIndex.FinalRules, "Unused_FinalRules", new string[0]);

        // ── 회상 영상 도중 대사 (타임라인 마커) ──
        // 기억 1: 지하철역, 트레일의 전화
        typeWriter.SetDialogueGroup(0, "Item1", new[]
        {
            "여보세요.",
            "트레일: 판사님, 늦은 시간에 죄송합니다. Drail 제약의 트레일입니다.",
            "Drail 제약... 다음 주 재판 때문이군요.",
            "트레일: 역시 빠르시네요. 요즘 임상시험 참가자 몇 명이 연락이 안 돼서, 경찰이 귀찮게 굴고 있습니다.",
            "트레일: 재판만 조용히 끝나면 다 정리될 일입니다.",
            "그래서 원하는 게 뭡니까.",
            "트레일: 증거 몇 개를 빼 주시고, 무죄를. 물론 사례는 섭섭지 않게 하겠습니다.",
            "얼마나요?",
            "트레일: 판사님이 생각하시는 것보다 많이.",
            "......좋습니다.",
            "트레일: 사건 자료는 역 앞 공중화장실, 맨 안쪽 칸에 두었습니다.",
            "트레일: 보시면 무엇을 지워야 할지 아실 겁니다."
        });

        // 기억 2: 병원, 복도 끝의 무언가 (영상이 보여 준다)
        typeWriter.SetDialogueGroup(1, "Item2", new[]
        {
            "......",
            "저건... 사람이야?",
            "실종된 사람들이... 여기 있었어."
        });

        // 기억 3: 드레일의 연구실
        typeWriter.SetDialogueGroup(2, "Item3", new[]
        {
            "드레일: 오셨군요, 판사님. 트레일 대표가 말씀 많이 하셨습니다.",
            "당신이 드레일.",
            "드레일: 다이어트 약이라고 하면 다들 줄을 서더군요. 덕분에 표본은 넉넉했습니다.",
            "표본?",
            "드레일: 버섯은 몸을 바꿉니다. 지방만이 아니라... 전부요.",
            "사람이 죽었다던데.",
            "드레일: 몇 명은 버티지 못했죠. 나머지는... 보셨잖습니까.",
            "......",
            "내 알 바 아니지. 난 판결만 내리면 되니까."
        });

        // 기억 4: 거리, 판결 뒤 보수
        typeWriter.SetDialogueGroup(3, "Item4", new[]
        {
            "트레일: 무죄 판결, 깔끔했습니다. 역시 판사님이시더군요.",
            "보수는.",
            "트레일: 거리에 구급상자로 두었습니다. 사람들 눈에 띄지 않게 가져가십시오.",
            "......",
            "좋은 거래였습니다."
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
        //  → 영상이 멈추고 다시 탐색: 계단으로 역 밖에 나감 → 역 앞 공중화장실 맨 안쪽 칸의 사건 자료 → 중요한 곳에 줄 긋기
        //    (2개 그으면 문을 쾅쾅 두드림 + 관리인 "화장실 마감합니다" → "잠시만요" → 나머지 2개)
        //  → 역으로 돌아가기 → 영상 이어서(테이블 복귀)
        FlashbackFreeRoamSegment subway = FindSegment(SubwaySegmentName);
        if (subway != null)
        {
            subway.SetHintTexts(
                controls: "WASD 이동  /  마우스 시점  /  Tab 기억 노트",
                explore: "주변을 둘러보자",
                call: "전화가 울린다",
                callPromptText: "[E] 전화 받기");
            subway.SetCollectHint("역을 둘러보며 단서를 모으세요  ({0}/{1})");

            FreeRoamPickupItem[] m1 = subway.OptionalClues;
            SetClue(m1, 0, "m1_card", "트레일의 명함",
                "Drail 제약 대표이사 트레일.\n\n" +
                "뒷면에 손글씨로 적혀 있다.\n" +
                "'판사님, 조용히 끝나면 섭섭지 않게 사례하겠습니다.'",
                "명함이다. 뒷면에 뭔가 적혀 있어.");
            SetClue(m1, 1, "m1_news", "구겨진 신문",
                "[사회] Drail 제약 임상시험 참가자 잇단 실종\n\n" +
                "다이어트 신약 시험에 참가한 시민들이 연락 두절.\n" +
                "경찰, 회사 연구동 수사 착수.\n" +
                "회사 측은 '관련 없다'며 부인. 첫 재판은 다음 주.",
                "신문이다. Drail... 임상시험?");
            SetClue(m1, 2, "m1_schedule", "재판 일정 메모",
                "Drail 제약 사건 - 담당 판사: 나\n\n" +
                "선고일에 붉은 동그라미가 쳐져 있다.\n" +
                "그 옆에 내 글씨로 '조용히'.",
                "내 글씨야... '조용히'?");

            // 통화 뒤: 화장실에 둔 사건 자료
            subway.SetAfterCallTexts(
                exit: "초록색 EXIT 표시를 따라 계단으로 올라가 역 밖으로 나가세요",
                find: "역 앞 공중화장실, 맨 안쪽 칸에서 사건 자료를 찾으세요",
                back: "역으로 돌아가세요");

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
                        "관리인: 아저씨! 화장실 마감합니다. 나오셔야 해요!",
                        "잠, 잠시만요...!"
                    });
            }

        }

        // ── 기억 2: 병원 탐색 ──
        FlashbackFreeRoamSegment hospital = FindSegment(HospitalSegmentName);
        if (hospital != null)
        {
            hospital.SetHintTexts(
                controls: "WASD 이동  /  마우스 시점  /  F 손전등  /  Tab 기억 노트",
                pickup: "동쪽 병동에서 Drail 실험실 열쇠를 찾으세요",
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
        //  → 남쪽 연구실 도착 → Third 영상(드레일 등장) → 테이블로 돌아온다
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

        }

        // ── 기억 4: 거리 ──
        //  (Forth 영상이 멈춘 뒤) 목격자를 피해 돈 가방 + 차 키 → 경적으로 차 찾기 → 차 타고 떠남
        //  → 영상 끝, 테이블로 돌아온다
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
