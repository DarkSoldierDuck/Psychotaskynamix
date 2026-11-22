using UnityEngine;
using UnityEngine.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using System.IO;

/// <summary>
/// 菜单工具：一键生成《飘》的场景（Canvas、UI、主菜单、设置、引擎绑定），并打包 Windows exe。
/// 注意：生成场景会覆盖现有 VN 场景，请在 Unity 菜单 VN → 1. 生成场景 后重新打包。
/// </summary>
public static class BuildScene
{
    const string ColText = "3a3a3a";
    const string ColPanel = "fffdf6cc"; // RRGGBBAA
    static Font font;

    [MenuItem("VN/1. 生成场景")]
    public static void Generate()
    {
        font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (font == null) font = Resources.GetBuiltinResource<Font>("Arial.ttf");

        // 清理旧 Canvas / EventSystem，避免两个 VNEngine 叠在一起
        // 先删所有 Canvas / EventSystem，防止上一次生成残留第二套 UI 和第二个 VNEngine
        var oldCanvases = Object.FindObjectsOfType<Canvas>();
        foreach (var c in oldCanvases) Object.DestroyImmediate(c.gameObject);
        var oldEventSystems = Object.FindObjectsOfType<UnityEngine.EventSystems.EventSystem>();
        foreach (var es in oldEventSystems) Object.DestroyImmediate(es.gameObject);

        var go = new GameObject("Canvas");
        var canvas = go.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        var scaler = go.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        go.AddComponent<GraphicRaycaster>();
        new GameObject("EventSystem",
            typeof(UnityEngine.EventSystems.EventSystem),
            typeof(UnityEngine.EventSystems.StandaloneInputModule));

        // —— 背景：正常 + 灰度 + 雾 + 暗角 ——
        var bgN = NewImg(go.transform, "BgNormal", 0, Color.white);
        Stretch(bgN);
        var bgG = NewImg(go.transform, "BgGray", 1, Color.clear);
        Stretch(bgG);
        var fog = NewImg(go.transform, "Fog", 3, Color.clear);
        Stretch(fog);

        // 时间戳 / 章节标题
        var time = NewText(go.transform, "TimeText", "", 30, TextAnchor.UpperRight,
            new Vector2(-40, 40), new Vector2(400, 50), new Color(0.1f, 0.1f, 0.1f, 0.85f));
        time.rectTransform.anchorMin = time.rectTransform.anchorMax = new Vector2(1, 1);
        var title = NewText(go.transform, "ChapterTitle", "", 54, TextAnchor.MiddleCenter,
            new Vector2(0, 180), new Vector2(1200, 100), new Color(0.1f, 0.1f, 0.1f, 0.9f));

        // —— 对话框 ——
        var panel = NewImg(go.transform, "DialogPanel", 10, ParseHex(ColPanel));
        Stretch(panel);
        panel.rectTransform.anchorMin = Vector2.zero;
        panel.rectTransform.anchorMax = new Vector2(1, 0);
        panel.rectTransform.offsetMin = new Vector2(140, 40);
        panel.rectTransform.offsetMax = new Vector2(-140, 300);
        var speaker = NewText(panel.transform, "Speaker", "", 34, TextAnchor.LowerLeft,
            new Vector2(28, 210), new Vector2(900, 50), Color.black);
        var body = NewText(panel.transform, "Body", "", 34, TextAnchor.UpperLeft,
            new Vector2(28, 30), new Vector2(1400, 190), ParseHex(ColText));
        var hint = NewText(panel.transform, "Hint", "▼", 26, TextAnchor.LowerRight,
            new Vector2(-60, 8), new Vector2(80, 40), new Color(0, 0, 0, 0.5f));
        hint.rectTransform.anchorMin = hint.rectTransform.anchorMax = new Vector2(1, 0);
        hint.rectTransform.anchoredPosition = new Vector2(-60, 8);
        var panelOutline = panel.gameObject.AddComponent<Outline>();
        panelOutline.effectColor = new Color(0.62f, 0.68f, 0.76f, 0f);
        panelOutline.effectDistance = new Vector2(2f, -2f);

        // 游戏内右上角按钮：设置 / 主菜单
        var settingsGame = MakeSmallButton(go.transform, "BtnSettingsGame", "设置", new Vector2(-260, -42));
        var titleGame = MakeSmallButton(go.transform, "BtnTitleGame", "主界面", new Vector2(-80, -42));

        // —— 选项 ——
        var choiceRoot = new GameObject("ChoiceRoot", typeof(RectTransform));
        choiceRoot.transform.SetParent(go.transform, false);
        choiceRoot.transform.SetSiblingIndex(20);
        var crt = (RectTransform)choiceRoot.transform;
        crt.anchorMin = crt.anchorMax = new Vector2(0.5f, 0.55f);
        crt.sizeDelta = new Vector2(620, 320);
        var clayout = choiceRoot.AddComponent<VerticalLayoutGroup>();
        clayout.spacing = 18;
        clayout.childAlignment = TextAnchor.MiddleCenter;
        clayout.childControlHeight = false;
        clayout.childForceExpandHeight = false;
        var btnPrefab = MakeChoiceButtonPrefab();

        // —— 接地长按 ——
        var holdRoot = NewPanel(go.transform, "HoldRoot", 21, new Color(0.93f, 0.92f, 0.88f, 0.92f));
        var hrt = holdRoot.GetComponent<RectTransform>();
        hrt.anchorMin = hrt.anchorMax = new Vector2(0.5f, 0.5f);
        hrt.sizeDelta = new Vector2(620, 420);
        var holdImg = NewImg(holdRoot.transform, "HoldRing", 0, new Color(0.9f, 0.75f, 0.55f, 1f));
        holdImg.type = Image.Type.Filled;
        holdImg.fillMethod = Image.FillMethod.Radial360;
        holdImg.rectTransform.sizeDelta = new Vector2(180, 180);
        holdImg.rectTransform.anchoredPosition = new Vector2(0, 80);
        var holdLabel = NewText(holdRoot.transform, "HoldLabel", "", 30, TextAnchor.UpperCenter,
            new Vector2(0, -140), new Vector2(560, 160), new Color(0.18f, 0.18f, 0.18f, 1f));

        // 暗角（最上层，但仍低于菜单遮罩）
        var vig = NewImg(go.transform, "Vignette", 50, Color.clear);
        Stretch(vig);

        // —— 主菜单 ——
        var mainMenu = NewPanel(go.transform, "MainMenuRoot", 60, new Color(0.06f, 0.07f, 0.09f, 0.55f));
        var mmTitle = NewText(mainMenu.transform, "Title", "飘", 120, TextAnchor.MiddleCenter,
            new Vector2(0, 240), new Vector2(900, 200), new Color(1f, 0.98f, 0.93f, 0.98f));
        var mmSub = NewText(mainMenu.transform, "SubTitle", "一段关于解离的互动短篇", 34, TextAnchor.MiddleCenter,
            new Vector2(0, 110), new Vector2(900, 70), new Color(0.95f, 0.95f, 0.95f, 0.9f));
        var startBtn = MakeMenuButton(mainMenu.transform, "BtnStart", "开始游戏", new Vector2(0, -20));
        var setMenuBtn = MakeMenuButton(mainMenu.transform, "BtnSettings", "设　置", new Vector2(0, -130));
        var quitMenuBtn = MakeMenuButton(mainMenu.transform, "BtnQuit", "退　出", new Vector2(0, -240));
        var mmHint = NewText(mainMenu.transform, "Hint", "点击对话或空格继续 · Esc 打开设置", 24, TextAnchor.LowerCenter,
            new Vector2(0, 36), new Vector2(1000, 50), new Color(1f, 1f, 1f, 0.7f));
        mmHint.rectTransform.anchorMin = mmHint.rectTransform.anchorMax = new Vector2(0.5f, 0);

        // —— 主菜单设置面板 ——
        var settingsMenu = NewPanel(go.transform, "SettingsMenuRoot", 70, new Color(0.07f, 0.08f, 0.1f, 0.88f));
        NewText(settingsMenu.transform, "Header", "设　置", 56, TextAnchor.MiddleCenter,
            new Vector2(0, 360), new Vector2(600, 100), Color.white);
        var sliders = new GameObject("Sliders", typeof(RectTransform));
        sliders.transform.SetParent(settingsMenu.transform, false);
        var srt = (RectTransform)sliders.transform;
        srt.anchorMin = srt.anchorMax = new Vector2(0.5f, 0.5f);
        srt.sizeDelta = new Vector2(760, 520);
        var bgm = MakeSlider(sliders.transform, "BGM", "音乐音量", 0.5f, -40);
        var sfx = MakeSlider(sliders.transform, "SFX", "音效音量", 0.7f, -130);
        var voice = MakeSlider(sliders.transform, "Voice", "语音音量", 0.7f, -220);
        var speed = MakeSlider(sliders.transform, "TextSpeed", "文字速度", 1f, -310);
        var uiScale = MakeSlider(sliders.transform, "UiScale", "界面大小", 1f, -385);
        uiScale.minValue = 0.7f;
        uiScale.maxValue = 1.3f;
        var fit = MakeDropdown(sliders.transform, "ScreenFit", "画面适配",
            new[] { "按宽度", "按高度", "完整显示" }, 1, -465);
        var res = MakeDropdown(sliders.transform, "Resolution", "分辨率",
            new[] { "1920×1080", "1280×720", "1600×900" }, 0, -535);
        var fs = MakeToggle(sliders.transform, "Fullscreen", "全屏", true, new Vector2(270, -535));
        var resetBtn = MakeSmallButton(settingsMenu.transform, "BtnReset", "恢复默认", new Vector2(-300, -440));
        var backMenu = MakeMenuButton(settingsMenu.transform, "BtnBack", "返　回", new Vector2(0, -440));

        // —— 游戏内设置面板 ——
        var settingsGamePanel = NewPanel(go.transform, "SettingsGameRoot", 71, new Color(0.07f, 0.08f, 0.1f, 0.88f));
        NewText(settingsGamePanel.transform, "Header", "设　置", 56, TextAnchor.MiddleCenter,
            new Vector2(0, 360), new Vector2(600, 100), Color.white);
        var sgSliders = new GameObject("Sliders", typeof(RectTransform));
        sgSliders.transform.SetParent(settingsGamePanel.transform, false);
        var sgrt = (RectTransform)sgSliders.transform;
        sgrt.anchorMin = sgrt.anchorMax = new Vector2(0.5f, 0.5f);
        sgrt.sizeDelta = new Vector2(760, 520);
        MakeSlider(sgSliders.transform, "BGM", "音乐音量", 0.5f, -40);
        MakeSlider(sgSliders.transform, "SFX", "音效音量", 0.7f, -130);
        MakeSlider(sgSliders.transform, "Voice", "语音音量", 0.7f, -220);
        MakeSlider(sgSliders.transform, "TextSpeed", "文字速度", 1f, -310);
        var sgUiScale = MakeSlider(sgSliders.transform, "UiScale", "界面大小", 1f, -385);
        sgUiScale.minValue = 0.7f;
        sgUiScale.maxValue = 1.3f;
        MakeDropdown(sgSliders.transform, "ScreenFit", "画面适配",
            new[] { "按宽度", "按高度", "完整显示" }, 1, -465);
        MakeDropdown(sgSliders.transform, "Resolution", "分辨率",
            new[] { "1920×1080", "1280×720", "1600×900" }, 0, -535);
        MakeToggle(sgSliders.transform, "Fullscreen", "全屏", true, new Vector2(270, -535));
        var backGame = MakeMenuButton(settingsGamePanel.transform, "BtnBack", "返　回", new Vector2(0, -440));

        // —— 结尾 UI ——
        var endRoot = NewPanel(go.transform, "EndRoot", 80, new Color(0.05f, 0.06f, 0.08f, 0.6f));
        NewText(endRoot.transform, "TheEnd", "— 完 —", 100, TextAnchor.MiddleCenter,
            new Vector2(0, 160), new Vector2(800, 180), new Color(1f, 0.98f, 0.93f, 0.98f));
        NewText(endRoot.transform, "Thanks", "谢谢你陪她走到这里。", 36, TextAnchor.MiddleCenter,
            new Vector2(0, 20), new Vector2(900, 80), new Color(1f, 1f, 1f, 0.9f));
        var endTitle = MakeMenuButton(endRoot.transform, "BtnTitle", "返回主界面", new Vector2(-180, -190));
        var endQuit = MakeMenuButton(endRoot.transform, "BtnQuit", "退出游戏", new Vector2(180, -190));

        // 引擎绑定
        var eng = go.AddComponent<VNEngine>();
        eng.bgNormal = bgN;
        eng.bgGray = bgG;
        eng.fog = fog;
        eng.vignette = vig;
        eng.timeText = time;
        eng.chapterTitle = title;
        eng.dialogPanel = panel;
        eng.speakerText = speaker;
        eng.bodyText = body;
        eng.hintText = hint;
        eng.choiceRoot = choiceRoot;
        eng.choiceButtonPrefab = btnPrefab;
        eng.holdRoot = holdRoot;
        eng.holdRing = holdImg;
        eng.holdLabel = holdLabel;
        eng.mainMenuRoot = mainMenu;
        eng.startButton = startBtn;
        eng.settingsMenuButton = setMenuBtn;
        eng.quitMenuButton = quitMenuBtn;
        eng.settingsMenuRoot = settingsMenu;
        eng.backFromSettingsButton = backMenu;
        eng.bgmSlider = bgm;
        eng.sfxSlider = sfx;
        eng.voiceVolumeSlider = voice;
        eng.textSpeedSlider = speed;
        eng.uiScaleSlider = uiScale;
        eng.screenFitDropdown = fit;
        eng.resolutionDropdown = res;
        eng.canvasScaler = go.GetComponent<CanvasScaler>();
        eng.fullscreenToggle = fs;
        eng.resetButton = resetBtn;
        eng.settingsGameButton = settingsGame;
        eng.titleGameButton = titleGame;
        eng.settingsGameRoot = settingsGamePanel;
        eng.closeGameSettingsButton = backGame;
        eng.endRoot = endRoot;
        eng.endTitleButton = endTitle;
        eng.endQuitButton = endQuit;

        if (!Directory.Exists("Assets/Scenes")) Directory.CreateDirectory("Assets/Scenes");
        EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene(), "Assets/Scenes/VN.unity");
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

    static GameObject NewPanel(Transform parent, string name, int sibling, Color col)
    {
        var g = NewImg(parent, name, sibling, col).gameObject;
        return g;
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
        t.font = font;
        t.raycastTarget = false;
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

    static Button MakeChoiceButtonPrefab()
    {
        var g = new GameObject("ChoiceButton", typeof(RectTransform), typeof(Image), typeof(Button));
        var img = g.GetComponent<Image>();
        img.color = ParseHex(ColPanel);
        var rt = (RectTransform)g.transform;
        rt.sizeDelta = new Vector2(600, 66);
        var le = g.AddComponent<LayoutElement>();
        le.preferredHeight = 66;
        var t = NewText(g.transform, "Label", "选项", 30, TextAnchor.MiddleCenter,
            Vector2.zero, new Vector2(600, 66), Color.black);
        Stretch(t.rectTransform);
        var b = g.GetComponent<Button>();
        var colors = b.colors;
        colors.highlightedColor = new Color(0.95f, 0.9f, 0.75f, 1f);
        colors.pressedColor = new Color(0.85f, 0.78f, 0.62f, 1f);
        colors.disabledColor = new Color(0.6f, 0.6f, 0.6f, 0.35f);
        b.colors = colors;
        return b;
    }

    static Button MakeMenuButton(Transform parent, string name, string text, Vector2 pos)
    {
        var g = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
        g.transform.SetParent(parent, false);
        var rt = (RectTransform)g.transform;
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = pos;
        rt.sizeDelta = new Vector2(360, 76);
        var img = g.GetComponent<Image>();
        img.color = new Color(0.98f, 0.96f, 0.9f, 0.92f);
        var b = g.GetComponent<Button>();
        var colors = b.colors;
        colors.highlightedColor = new Color(1f, 0.92f, 0.72f, 1f);
        colors.pressedColor = new Color(0.82f, 0.74f, 0.58f, 1f);
        b.colors = colors;
        var t = NewText(g.transform, "Label", text, 34, TextAnchor.MiddleCenter,
            Vector2.zero, new Vector2(360, 76), new Color(0.12f, 0.12f, 0.12f, 1f));
        Stretch(t.rectTransform);
        return b;
    }

    static Button MakeSmallButton(Transform parent, string name, string text, Vector2 pos)
    {
        var g = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
        g.transform.SetParent(parent, false);
        var rt = (RectTransform)g.transform;
        rt.anchorMin = rt.anchorMax = new Vector2(1, 1);
        rt.anchoredPosition = pos;
        rt.sizeDelta = new Vector2(150, 54);
        var img = g.GetComponent<Image>();
        img.color = new Color(0.98f, 0.96f, 0.9f, 0.82f);
        var b = g.GetComponent<Button>();
        var t = NewText(g.transform, "Label", text, 24, TextAnchor.MiddleCenter,
            Vector2.zero, new Vector2(150, 54), new Color(0.12f, 0.12f, 0.12f, 1f));
        Stretch(t.rectTransform);
        return b;
    }

    static Slider MakeSlider(Transform parent, string name, string label, float value, float y)
    {
        var g = new GameObject(name, typeof(RectTransform));
        g.transform.SetParent(parent, false);
        var rt = (RectTransform)g.transform;
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = new Vector2(0, y);
        rt.sizeDelta = new Vector2(760, 70);
        NewText(g.transform, "Label", label, 26, TextAnchor.MiddleLeft,
            new Vector2(-350, 16), new Vector2(220, 40), Color.white);
        var sld = new GameObject("Slider", typeof(RectTransform), typeof(Slider));
        sld.transform.SetParent(g.transform, false);
        var srt = (RectTransform)sld.transform;
        srt.anchorMin = srt.anchorMax = new Vector2(0.5f, 0.5f);
        srt.anchoredPosition = new Vector2(70, 0);
        srt.sizeDelta = new Vector2(500, 36);
        var bg = NewImg(sld.transform, "Background", 0, new Color(0.3f, 0.3f, 0.3f, 0.9f));
        Stretch(bg.rectTransform);
        var fillArea = new GameObject("Fill Area", typeof(RectTransform));
        fillArea.transform.SetParent(sld.transform, false);
        var fart = (RectTransform)fillArea.transform;
        fart.anchorMin = new Vector2(0, 0); fart.anchorMax = new Vector2(1, 1);
        fart.offsetMin = new Vector2(6, 0); fart.offsetMax = new Vector2(-6, 0);
        var fill = NewImg(fillArea.transform, "Fill", 0, new Color(0.9f, 0.72f, 0.45f, 1f));
        Stretch(fill.rectTransform);
        var handleArea = new GameObject("Handle Slide Area", typeof(RectTransform));
        handleArea.transform.SetParent(sld.transform, false);
        var hArt = (RectTransform)handleArea.transform;
        hArt.anchorMin = Vector2.zero; hArt.anchorMax = Vector2.one;
        hArt.offsetMin = new Vector2(8, 0); hArt.offsetMax = new Vector2(-8, 0);
        var handle = NewImg(handleArea.transform, "Handle", 0, new Color(1f, 0.96f, 0.88f, 1f));
        handle.rectTransform.sizeDelta = new Vector2(28, 28);
        var slider = sld.GetComponent<Slider>();
        slider.fillRect = fill.rectTransform;
        slider.handleRect = handle.rectTransform;
        slider.targetGraphic = handle;
        slider.minValue = name == "TextSpeed" ? 0.25f : 0f;
        slider.maxValue = name == "TextSpeed" ? 3f : 1f;
        slider.value = value;
        return slider;
    }

    static Dropdown MakeDropdown(Transform parent, string name, string label, string[] options, int value, float y)
    {
        var g = new GameObject(name, typeof(RectTransform));
        g.transform.SetParent(parent, false);
        var rt = (RectTransform)g.transform;
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = new Vector2(0, y);
        rt.sizeDelta = new Vector2(760, 70);
        NewText(g.transform, "Label", label, 26, TextAnchor.MiddleLeft,
            new Vector2(-350, 16), new Vector2(220, 40), Color.white);
        var dd = new GameObject("Dropdown", typeof(RectTransform), typeof(Image), typeof(Dropdown));
        dd.transform.SetParent(g.transform, false);
        var drt = (RectTransform)dd.transform;
        drt.anchorMin = drt.anchorMax = new Vector2(0.5f, 0.5f);
        drt.anchoredPosition = new Vector2(70, 0);
        drt.sizeDelta = new Vector2(500, 50);
        dd.GetComponent<Image>().color = new Color(0.95f, 0.93f, 0.88f, 0.95f);
        var caption = NewText(dd.transform, "Label", options[0], 24, TextAnchor.MiddleLeft,
            new Vector2(14, 0), new Vector2(450, 50), new Color(0.1f, 0.1f, 0.1f, 1f));
        caption.rectTransform.anchorMin = new Vector2(0, 0);
        caption.rectTransform.anchorMax = new Vector2(1, 1);
        caption.rectTransform.offsetMin = new Vector2(14, 0);
        caption.rectTransform.offsetMax = new Vector2(-40, 0);
        var arrow = NewText(dd.transform, "Arrow", "▼", 20, TextAnchor.MiddleRight,
            new Vector2(-16, 0), new Vector2(30, 50), new Color(0.1f, 0.1f, 0.1f, 1f));
        arrow.rectTransform.anchorMin = new Vector2(1, 0);
        arrow.rectTransform.anchorMax = new Vector2(1, 1);
        arrow.rectTransform.anchoredPosition = new Vector2(-16, 0);
        var dropdown = dd.GetComponent<Dropdown>();
        dropdown.captionText = caption;
        dropdown.value = value;
        foreach (var o in options) dropdown.options.Add(new Dropdown.OptionData(o));
        dropdown.value = value;
        return dropdown;
    }

    static Toggle MakeToggle(Transform parent, string name, string label, bool value, Vector2 pos)
    {
        var g = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Toggle));
        g.transform.SetParent(parent, false);
        var rt = (RectTransform)g.transform;
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = pos;
        rt.sizeDelta = new Vector2(180, 50);
        g.GetComponent<Image>().color = new Color(0.95f, 0.93f, 0.88f, 0.95f);
        var check = new GameObject("Checkmark", typeof(RectTransform), typeof(Image));
        check.transform.SetParent(g.transform, false);
        var ci = check.GetComponent<Image>();
        ci.color = new Color(0.2f, 0.45f, 0.75f, 1f);
        Stretch(ci.rectTransform);
        var t = NewText(g.transform, "Label", label, 24, TextAnchor.MiddleCenter,
            Vector2.zero, new Vector2(180, 50), new Color(0.1f, 0.1f, 0.1f, 1f));
        Stretch(t.rectTransform);
        var toggle = g.GetComponent<Toggle>();
        toggle.graphic = ci;
        toggle.isOn = value;
        return toggle;
    }

    static Color ParseHex(string hex)
    {
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
