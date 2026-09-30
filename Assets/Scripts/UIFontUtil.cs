using UnityEngine;
using UnityEngine.UI;

// 코드로 만드는 UI에 쓸 폰트를 고른다.
// 지정한 폰트가 없으면 씬의 대사창과 같은 폰트(DOSMyungjo)를 찾아 쓰고, 그것도 없으면 기본 폰트.
public static class UIFontUtil
{
    private const string PreferredFontName = "DOSMyungjo";
    private static Font cached;

    public static Font Resolve(Font preferred)
    {
        if (preferred != null) return preferred;
        if (cached != null) return cached;

        foreach (var t in Object.FindObjectsByType<Text>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (t.font != null && t.font.name == PreferredFontName)
            {
                cached = t.font;
                return cached;
            }
        }

        return Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
    }
}
