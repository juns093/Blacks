using UnityEngine;

// 프로젝트에 차 문 / 시동 / 출발 소리가 없어서 코드로 만드는 임시 효과음.
// FlashbackFreeRoamSegment 인스펙터에 실제 클립을 넣으면 이건 쓰이지 않습니다.
public static class ProceduralCarAudio
{
    private const int Rate = 44100;

    private static AudioClip door;
    private static AudioClip engine;

    /// <summary>문 닫히는 '쿵' 소리 (약 0.4초)</summary>
    public static AudioClip Door()
    {
        if (door != null) return door;

        var rnd = new System.Random(7);
        int total = Mathf.CeilToInt(Rate * 0.4f);
        var data = new float[total];
        float lp = 0f;

        for (int i = 0; i < total; i++)
        {
            float t = i / (float)Rate;
            float noise = (float)rnd.NextDouble() * 2f - 1f;
            lp += 0.07f * (noise - lp);                       // 둔탁하게 깎은 잡음 = 문짝이 울리는 소리

            float thump = Mathf.Sin(2f * Mathf.PI * 62f * t) * Mathf.Exp(-t * 26f);   // 낮은 '쿵'
            float body = lp * Mathf.Exp(-t * 16f) * 3f;
            float latch = t < 0.006f ? noise * 0.5f * (1f - t / 0.006f) : 0f;          // 걸쇠 '딸깍'

            data[i] = Mathf.Clamp(thump * 0.9f + body * 0.6f + latch, -1f, 1f) * 0.8f;
        }

        door = AudioClip.Create("ProceduralCarDoor", total, 1, Rate, false);
        door.SetData(data, 0);
        return door;
    }

    /// <summary>시동 걸기 -> 공회전 -> 가속하며 멀어지는 엔진 소리 (약 4초)</summary>
    public static AudioClip EngineStartAndDriveAway()
    {
        if (engine != null) return engine;

        const float duration = 4f;
        const float crankEnd = 0.7f;   // 셀 모터 '끼릭끼릭'
        const float idleEnd = 1.5f;    // 시동 걸린 뒤 잠깐 공회전

        var rnd = new System.Random(11);
        int total = Mathf.CeilToInt(Rate * duration);
        var data = new float[total];
        float phase = 0f, crankPhase = 0f, lpNoise = 0f, lpOut = 0f;

        for (int i = 0; i < total; i++)
        {
            float t = i / (float)Rate;
            float noise = (float)rnd.NextDouble() * 2f - 1f;
            lpNoise += 0.05f * (noise - lpNoise);
            float s = 0f;

            if (t < crankEnd)
            {
                // 셀 모터: 짧게 끊기는 윙윙 소리
                crankPhase += 170f / Rate;
                float saw = 2f * (crankPhase - Mathf.Floor(crankPhase)) - 1f;
                float pulse = Mathf.Clamp01(Mathf.Sin(2f * Mathf.PI * 8f * t) * 3f);
                s = (saw * 0.3f + lpNoise * 0.4f) * pulse * 0.55f;
            }

            if (t >= crankEnd - 0.05f)
            {
                // 엔진 폭발 주기(Hz). 공회전 -> 가속
                float freq = t < idleEnd
                    ? 30f
                    : Mathf.Lerp(30f, 78f, Mathf.SmoothStep(0f, 1f, (t - idleEnd) / (duration - idleEnd)));
                phase += freq / Rate;
                float p = phase - Mathf.Floor(phase);

                float saw = 2f * p - 1f;
                float h2 = Mathf.Sin(2f * Mathf.PI * 2f * phase);
                float firing = 0.55f + 0.45f * Mathf.Sin(2f * Mathf.PI * phase);   // 실린더가 터지는 박자
                float rumble = (saw * 0.45f + h2 * 0.25f + lpNoise * 0.5f) * firing;

                float catchIn = Mathf.SmoothStep(0f, 1f, (t - (crankEnd - 0.05f)) / 0.25f);
                s += rumble * catchIn;
            }

            // 마지막 구간은 멀어지면서 사라진다
            float driveAway = 1f - Mathf.SmoothStep(0f, 1f, (t - 2.2f) / (duration - 2.2f));
            s *= driveAway;

            lpOut += 0.35f * (s - lpOut);   // 거친 고음을 살짝 깎는다
            data[i] = Mathf.Clamp(lpOut, -1f, 1f) * 0.85f;
        }

        engine = AudioClip.Create("ProceduralCarEngine", total, 1, Rate, false);
        engine.SetData(data, 0);
        return engine;
    }
}
