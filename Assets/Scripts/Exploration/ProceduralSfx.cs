using UnityEngine;

// 소리 파일이 없을 때 코드로 만들어 쓰는 효과음.
public static class ProceduralSfx
{
    private static AudioClip phoneRing;

    /// <summary>
    /// 옛날 휴대폰 벨소리. "띠리리리-" 1.6초 울리고 2.4초 쉬는 4초짜리 (반복 재생용).
    /// </summary>
    public static AudioClip PhoneRing()
    {
        if (phoneRing != null) return phoneRing;

        const int rate = 44100;
        const float length = 4f;
        const float ringLength = 1.6f;
        int n = Mathf.CeilToInt(length * rate);
        var data = new float[n];

        for (int i = 0; i < n; i++)
        {
            float t = (float)i / rate;
            if (t > ringLength) break;

            // 두 음을 빠르게 번갈아 → 떨리는 벨소리
            bool high = Mathf.FloorToInt(t * 24f) % 2 == 0;
            float f = high ? 1320f : 1100f;
            float tone = Mathf.Sign(Mathf.Sin(2f * Mathf.PI * f * t)) * 0.25f + Mathf.Sin(2f * Mathf.PI * f * t) * 0.35f;

            // 두 번 끊어서 울림 (0~0.7초, 0.9~1.6초)
            float gate = (t < 0.7f || (t > 0.9f && t < 1.6f)) ? 1f : 0f;
            float edge = Mathf.Clamp01(Mathf.Min(t % 0.8f, 0.8f - t % 0.8f) * 60f);
            data[i] = tone * gate * edge * 0.6f;
        }

        phoneRing = AudioClip.Create("PhoneRing", n, 1, rate, false);
        phoneRing.SetData(data, 0);
        return phoneRing;
    }

    private static AudioClip doorCreak;
    private static AudioClip doorThud;

    /// <summary>문이 열릴 때 "끼이익" 하는 소리 (약 0.9초).</summary>
    public static AudioClip DoorCreak()
    {
        if (doorCreak != null) return doorCreak;

        const int rate = 44100;
        const float length = 0.9f;
        int n = Mathf.CeilToInt(length * rate);
        var data = new float[n];
        var rng = new System.Random(7);
        float phase = 0f, lp = 0f;

        for (int i = 0; i < n; i++)
        {
            float t = (float)i / rate;
            float k = t / length;

            // 경첩이 긁히는 소리: 흔들리며 올라갔다 내려오는 높은 음 + 거친 잡음
            float f = 380f + 260f * Mathf.Sin(k * Mathf.PI) + 40f * Mathf.Sin(t * 37f);
            phase += 2f * Mathf.PI * f / rate;
            float saw = (phase / Mathf.PI) % 2f - 1f;
            float noise = (float)rng.NextDouble() * 2f - 1f;
            lp += (noise - lp) * 0.08f;

            // 긁히는 느낌을 위해 짧게 끊어지는 게이트
            float grain = 0.55f + 0.45f * Mathf.Sin(t * 2f * Mathf.PI * 34f);
            float env = Mathf.Clamp01(k * 12f) * Mathf.Clamp01((1f - k) * 5f);
            data[i] = (saw * 0.22f + lp * 0.5f) * grain * env * 0.7f;
        }

        doorCreak = AudioClip.Create("DoorCreak", n, 1, rate, false);
        doorCreak.SetData(data, 0);
        return doorCreak;
    }

    /// <summary>문이 닫히거나 벽에 부딪히는 "쿵" 소리 (약 0.5초).</summary>
    public static AudioClip DoorThud()
    {
        if (doorThud != null) return doorThud;

        const int rate = 44100;
        const float length = 0.5f;
        int n = Mathf.CeilToInt(length * rate);
        var data = new float[n];
        var rng = new System.Random(11);
        float lp = 0f;

        for (int i = 0; i < n; i++)
        {
            float t = (float)i / rate;
            float noise = (float)rng.NextDouble() * 2f - 1f;
            lp += (noise - lp) * 0.05f;
            float body = Mathf.Sin(2f * Mathf.PI * (70f - 30f * t) * t);
            float env = Mathf.Exp(-t * 11f);
            float click = t < 0.012f ? noise * (1f - t / 0.012f) : 0f;
            data[i] = (body * 0.7f + lp * 1.4f + click * 0.5f) * env * 0.8f;
        }

        doorThud = AudioClip.Create("DoorThud", n, 1, rate, false);
        doorThud.SetData(data, 0);
        return doorThud;
    }
}
