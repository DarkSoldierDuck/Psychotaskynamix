using UnityEngine;
using UnityEngine.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using System.IO;

/// <summary>
/// 菜单工具：一键生成《飘》的场景（Canvas、UI、引擎绑定），并打包 Windows exe。
/// </summary>
public static class BuildScene
{
    const string ColText = "3a3a3a";
    const string ColPanel = "fffdf6cc"; // RRGGBBAA

    [MenuItem("VN/1. 生成场景")]
    public static void Generate()
    {
        var go = new GameObject("Canvas");
        var canvas = go.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        var scaler = go.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        go.AddComponent<GraphicRaycaster>();
        if (Object.FindObjectOfType<UnityEngine.EventSystems.EventSystem>() == null)
        {
            var es = new GameObject("EventSystem",
                typeof(UnityEngine.EventSystems.EventSystem),
                typeof(UnityEngine.EventSystems.StandaloneInputModule));
        }

        // —— 背景两层：正常 + 灰度副本 ——
        var bgN = NewImg(go.transform, "BgNormal", 0, Color.white);
        Stretch(bgN);
        var bgG = NewImg(go.transform, "BgGray", 1, Color.clear);
        Stretch(bgG);

        // 雾化层（白，遮罩感）
        var fog = NewImg(go.transform, "Fog", 3, Color.clear);
        Stretch(fog);

        // 时间戳（右上角）
        var time = NewText(go.transform, "TimeText", "", 30, TextAnchor.UpperRight,
            new Vector2(-40, 40), new Vector2(400, 50), Color.black);
        time.rectTransform.anchorMin = time.rectTransform.anchorMax = new Vector2(1, 1);

        // 章节标题（居中偏上）
        var title = NewText(go.transform, "ChapterTitle", "", 54, TextAnchor.MiddleCenter,
            new Vector2(0, 180), new Vector2(1200, 100), Color.black);

        // —— 对话框 ——
        var panel = NewImg(go.transform, "DialogPanel", 10, ParseHex(ColPanel));
        Stretch(panel);
        panel.rectTransform.anchorMin = new Vector2(0, 0);
        panel.rectTransform.anchorMax = new Vector2(1, 0);
        panel.rectTransform.offsetMin = new Vector2(140, 40);
        panel.rectTransform.offsetMax = new Vector2(-140, 300);

        var speaker = NewText(panel.transform, "Speaker", "", 34, TextAnchor.LowerLeft,
            new Vector2(28, 210), new Vector2(900, 50), Color.black);

        var body = NewText(panel.transform, "Body", "", 34, TextAnchor.UpperLeft,
            new Vector2(28, 30), new Vector2(1400, 190), ParseHex(ColText));

        // 继续提示（右下小三角）
        var hint = NewText(panel.transform, "Hint", "▼", 26, TextAnchor.LowerRight,
            new Vector2(-60, 8), new Vector2(80, 40), new Color(0, 0, 0, 0.5f));
        hint.rectTransform.anchorMin = hint.rectTransform.anchorMax = new Vector2(1, 0);
        hint.rectTransform.anchoredPosition = new Vector2(-60, 8);

        // —— 选项 ——
        var choiceRoot = new GameObject("ChoiceRoot", typeof(RectTransform));
        choiceRoot.transform.SetParent(go.transform, false);
        var crt = (RectTransform)choiceRoot.transform;
        crt.anchorMin = new Vector2(0.5f, 0.5f);
        crt.anchorMax = crt.anchorMin;
        crt.sizeDelta = new Vector2(520, 320);
        var layout = choiceRoot.AddComponent<VerticalLayoutGroup>();
        layout.spacing = 18;
        layout.childAlignment = TextAnchor.MiddleCenter;
        layout.childControlHeight = false;
        layout.childForceExpandHeight = false;

        var btnPrefab = MakeButtonPrefab();

        // —— 接地长按 ——
        var holdRoot = new GameObject("HoldRoot", typeof(RectTransform));
        holdRoot.transform.SetParent(go.transform, false);
        var hrt = (RectTransform)holdRoot.transform;
        hrt.anchorMin = hrt.anchorMax = new Vector2(0.5f, 0.5f);
        hrt.sizeDelta = new Vector2(600, 400);
        var holdImg = NewImg(holdRoot.transform, "HoldRing", 0, Color.white);
        holdImg.type = Image.Type.Filled;
        holdImg.fillMethod = Image.FillMethod.Radial360;
        holdImg.rectTransform.sizeDelta = new Vector2(180, 180);
        holdImg.rectTransform.anchoredPosition = new Vector2(0, 80);
        holdImg.color = new Color(0.9f, 0.75f, 0.55f);
        var holdLabel = NewText(holdRoot.transform, "HoldLabel", "", 30, TextAnchor.UpperCenter,
            new Vector2(0, -140), new Vector2(560, 160), Color.black);

        // 暗角（最上层）
        var vig = NewImg(go.transform, "Vignette", 50, Color.clear);
        Stretch(vig);

        // 引擎绑定
        var eng = go.AddComponent<VNEngine>();
        eng.bgNormal = bgN;
        eng.bgGray = bgG;
        eng.fog = fog;
        eng.vignette = vig;
        eng.timeText = time;
        eng.chapterTitle = title;
        eng.speakerText = speaker;
        eng.bodyText = body;
        eng.hintText = hint;
        eng.choiceRoot = choiceRoot;
        eng.choiceButtonPrefab = btnPrefab;
        eng.holdRoot = holdRoot;
        eng.holdRing = holdImg;
        eng.holdLabel = holdLabel;

        if (!Directory.Exists("Assets/Scenes")) Directory.CreateDirectory("Assets/Scenes");
        EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene(),
            "Assets/Scenes/VN.unity");
        Debug.Log("[VN] 场景已生成: Assets/Scenes/VN.unity");
    }

    [MenuItem("VN/2. 打包 Windows exe")]
    public static void Build()
    {
        var dir = Path.Combine("..", "Build");
        Directory.CreateDirectory(dir);
        var opts = new BuildPlayerOptions
        {
            scenes = new[] { "Assets/Scenes/VN.unity" },
            locationPathName = Path.Combine(dir, "飘.exe"),
            target = BuildTarget.StandaloneWindows64,
        };
        var report = BuildPipeline.BuildPlayer(opts);
        Debug.Log("[VN] 打包结果: " + report.summary.result);
    }

    static Image NewImg(Transform parent, string name, int sibling, Color col)
    {
        var g = new GameObject(name, typeof(RectTransform), typeof(Image));
        g.transform.SetParent(parent, false);
        var img = g.GetComponent<Image>();
        img.color = col;
        g.transform.SetSiblingIndex(sibling);
        return img;
    }

    static Text NewText(Transform parent, string name, string content, int size,
        TextAnchor align, Vector2 pos, Vector2 sizeD, Color col)
    {
        var g = new GameObject(name, typeof(RectTransform), typeof(Text));
        g.transform.SetParent(parent, false);
        var t = g.GetComponent<Text>();
        t.text = content;
        t.fontSize = size;
        t.alignment = align;
        t.color = col;
        t.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (t.font == null) t.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        t.rectTransform.anchoredPosition = pos;
        t.rectTransform.sizeDelta = sizeD;
        return t;
    }

    static void Stretch(Image img) { Stretch(img.rectTransform); }

    static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }

    static Button MakeButtonPrefab()
    {
        var g = new GameObject("ChoiceButton", typeof(RectTransform), typeof(Image), typeof(Button));
        var img = g.GetComponent<Image>();
        img.color = ParseHex(ColPanel);
        var rt = (RectTransform)g.transform;
        rt.sizeDelta = new Vector2(520, 64);
        var le = g.AddComponent<LayoutElement>();
        le.preferredHeight = 64;
        var t = NewText(g.transform, "Label", "选项", 30, TextAnchor.MiddleCenter,
            Vector2.zero, new Vector2(520, 64), Color.black);
        Stretch(t.rectTransform);
        return g.GetComponent<Button>();
    }

    static Color ParseHex(string hex)
    {
        // "RRGGBBAA" 形式（如 fffdf6cc）
        if (hex.Length == 8)
        {
            float r = int.Parse(hex.Substring(0, 2), System.Globalization.NumberStyles.HexNumber) / 255f;
            float g = int.Parse(hex.Substring(2, 2), System.Globalization.NumberStyles.HexNumber) / 255f;
            float b = int.Parse(hex.Substring(4, 2), System.Globalization.NumberStyles.HexNumber) / 255f;
            float a = int.Parse(hex.Substring(6, 2), System.Globalization.NumberStyles.HexNumber) / 255f;
            return new Color(r, g, b, a);
        }
        Color c;
        ColorUtility.TryParseHtmlString("#" + hex, out c);
        return c;
    }
}
