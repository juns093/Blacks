// 대사 그룹 번호. (TypeWriter의 dialogueGroups 칸 번호)
// 0~3은 회상 영상(타임라인) 도중 대사(Item1~4)가 쓴다.
public static class StoryDialogueIndex
{
    // ── 시작 ──
    public const int IntroMain = 4;          // PROLOGUE + FIRST ROUND
    public const int EndingOne = 5;          // 1~3판에서 ???를 죽임 → 엔딩 1
    public const int WhoKilledFirst = EndingOne; // (예전 이름)
    public const int EndingOneDying = 6;     // 아이템을 하나라도 쓴 뒤(2~3판) ???를 죽임 → 딸을 그리는 마지막 말    // (쓰지 않음)

    // ── 아이템을 눌렀을 때 (기억으로 들어가기 전) ──
    public const int ItemUseFirst = 7;       // 휴대폰
    public const int ItemUseSecond = 8;      // 혈액
    public const int ItemUseThird = 9;       // 약
    public const int ItemUseForth = 10;      // 구급상자

    // ── 죽었을 때의 환각 ──
    public const int PlayerDeathFirst = 11;  // 1판: 법정 (낯선 목소리)
    public const int PlayerDeathSecond = 12; // 2판: 병원 (17번)
    public const int PlayerDeathThird = 13;  // 3판: 연구실 (드레일)
    public const int PlayerDeathFourth = 14; // 4판: 법정 (트레일의 봉투)
    public const int PlayerDeathFifth = 15;  // 5판: ???에게 사망 → 사망 엔딩

    public const int FinalRules = 16;        // (지금은 쓰지 않음)

    // ── 판 사이 ──
    public const int BackToPresent = 17;     // 1판 환각 뒤 "하... 괜찮아?"
    public const int RoundStartSecond = 18;
    public const int RoundStartThird = 19;
    public const int TrailEnter = 20;        // 트레일 등장
    public const int RoundStartFourth = 21;  // 트레일과의 판
    public const int AfterFourthDeath = 22;  // "내가 그랬구나"
    public const int NewItem = 23;           // 17번의 사진과 기록
    public const int RoundStartFifth = 24;   // 옆에 트레일의 시체

    // ── 엔딩 ──
    public const int TrailKilledEnding = 25; // 엔딩 2
    public const int WhoKilledFinal = 26;    // 5판에서 ???를 죽임 → 문서 확인
    public const int ConfessionA = 27;
    public const int ConfessionB = 28;       // 경찰서
    public const int MonsterA = 29;
    public const int MonsterB = 30;          // 드레일의 목소리

    public const int Count = 31;

    public static int Normalize(int index)
    {
        // 예전 버전(10~16) -> 현재 버전 자동 변환은 제거했다.
        // 새 인덱스(4~14)와 범위가 겹쳐서, 정상 값이 엉뚱한 그룹으로 바뀌었기 때문.
        return index;
    }
}
