using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// 轻量视觉小说引擎：《飘》
/// 支持命令：bg / float / time / title / label / jump / choice / hold / end
/// 对话行格式：说话人：文本 ；无冒号行为旁白
/// 新增：主菜单、设置、回主菜单、退出、点击任意位置继续（UI/选项消费点击，避免误继续）
/// </summary>
public class VNEngine : MonoBehaviour
{
    [Header("场景 UI 引用")]
    public Image bgNormal;
    public Image bgGray;
    public Image fog;
    public Image vignette;
    public Text timeText;
    public Text chapterTitle;
    public Image dialogPanel;
    public Text speakerText;
    public Text bodyText;
    public Text hintText;
    public GameObject choiceRoot;
    public Button choiceButtonPrefab;
    public GameObject holdRoot;
    public Image holdRing;
    public Text holdLabel;

    [Header("主菜单")]
    public GameObject mainMenuRoot;
    public Button startButton;
    public Button settingsMenuButton;
    public Button quitMenuButton;
    public Button backFromSettingsButton;
    public GameObject settingsMenuRoot;
    public Slider bgmSlider;
    public Slider sfxSlider;
    public Slider voiceVolumeSlider;
    public Slider textSpeedSlider;
    public Dropdown resolutionDropdown;
    public Slider uiScaleSlider;
    public Dropdown screenFitDropdown;
    public Toggle fullscreenToggle;
    public Button resetButton;
    public CanvasScaler canvasScaler;

    [Header("游戏内")]
    public Button settingsGameButton;
    public Button titleGameButton;
    public GameObject settingsGameRoot;
    public Button closeGameSettingsButton;
    public GameObject endRoot;
    public Button endTitleButton;
    public Button endQuitButton;

    class Cmd { public string op; public string arg; }
    readonly List<Cmd> cmds = new List<Cmd>();
    int pc;
    const string Keywords = "bg float time title label jump choice hold end";

    enum ScreenMode { MainMenu, SettingsMenu, Game, GameSettings, Ending }
    ScreenMode screen = ScreenMode.MainMenu;
    ScreenMode settingsReturn;

    float floatLevel, floatTarget;
    float wobbleT;
    bool typing, waiting, holding, ended;
    float holdProgress;
    string fullText = "";
    Coroutine typer;
    Texture2D grayTex;
    Color bodyBase;
    float bodyAlphaBase;
    bool consumedFrameClick;
    bool hadChoiceThisFrame;
    bool panelBaseInitialized;
    readonly List<RaycastResult> raycastResults = new List<RaycastResult>();
    Color panelWarmRuntime;
    Color panelColdRuntime;

    static readonly Color PanelWarm = new Color(1f, 0.99f, 0.96f, 0.8f);
    static readonly Color PanelCold = new Color(0.84f, 0.88f, 0.94f, 0.88f);
    static readonly Color PanelEdge = new Color(0.62f, 0.68f, 0.76f, 0.95f);

    void Awake()
    {
        LoadSettings();
    }

    void Start()
    {
        // 兼容旧场景：没有主菜单/新 UI 时直接进入游戏，不让空引用卡住
        if (mainMenuRoot == null || startButton == null || dialogPanel == null
            || bodyText == null || hintText == null)
        {
            Debug.LogWarning("[VN] 检测到旧版场景，直接启动游戏。请执行 VN→1.生成场景 以启用主菜单和设置。");
            Parse();
            bodyBase = bodyText ? bodyText.color : Color.black;
            bodyAlphaBase = bodyBase.a;
            MakeRadialSprites();
            ApplySettings();
            screen = ScreenMode.Game;
            SetRoot(choiceRoot, false);
            SetRoot(holdRoot, false);
            SetBg("classroom");
            Next();
            return;
        }

        Parse();
        bodyBase = bodyText.color;
        bodyAlphaBase = bodyBase.a;
        if (dialogPanel)
        {
            panelWarmRuntime = dialogPanel.color;
            panelColdRuntime = new Color(
                Mathf.Clamp01(panelWarmRuntime.r * 0.84f + 0.05f),
                Mathf.Clamp01(panelWarmRuntime.g * 0.89f + 0.06f),
                Mathf.Clamp01(panelWarmRuntime.b * 0.97f + 0.08f),
                Mathf.Clamp01(panelWarmRuntime.a + 0.08f));
        }
        panelBaseInitialized = true;
        MakeRadialSprites();
        BindUI();
        ApplySettings();
        ShowMainMenu();
    }

    void BindUI()
    {
        if (startButton) startButton.onClick.AddListener(StartGame);
        if (settingsMenuButton) settingsMenuButton.onClick.AddListener(OpenSettingsFromMenu);
        if (quitMenuButton) quitMenuButton.onClick.AddListener(QuitGame);
        if (backFromSettingsButton) backFromSettingsButton.onClick.AddListener(CloseSettings);
        if (settingsGameButton) settingsGameButton.onClick.AddListener(OpenSettingsFromGame);
        if (titleGameButton) titleGameButton.onClick.AddListener(ShowMainMenu);
        if (closeGameSettingsButton) closeGameSettingsButton.onClick.AddListener(CloseSettings);
        if (endTitleButton) endTitleButton.onClick.AddListener(ShowMainMenu);
        if (endQuitButton) endQuitButton.onClick.AddListener(QuitGame);
        if (resetButton) resetButton.onClick.AddListener(ResetSettings);

        if (bgmSlider) bgmSlider.onValueChanged.AddListener(v => { PlayerPrefs.SetFloat("BGM", v); ApplySettings(); });
        if (sfxSlider) sfxSlider.onValueChanged.AddListener(v => { PlayerPrefs.SetFloat("SFX", v); ApplySettings(); });
        if (voiceVolumeSlider) voiceVolumeSlider.onValueChanged.AddListener(v => { PlayerPrefs.SetFloat("Voice", v); ApplySettings(); });
        if (textSpeedSlider) textSpeedSlider.onValueChanged.AddListener(v => { PlayerPrefs.SetFloat("TextSpeed", v); ApplySettings(); });
        if (uiScaleSlider) uiScaleSlider.onValueChanged.AddListener(v => { PlayerPrefs.SetFloat("UiScale", v); ApplySettings(); });
        if (screenFitDropdown) screenFitDropdown.onValueChanged.AddListener(v => { PlayerPrefs.SetInt("ScreenFit", v); ApplySettings(); });
        if (fullscreenToggle) fullscreenToggle.onValueChanged.AddListener(v => { PlayerPrefs.SetInt("Fullscreen", v ? 1 : 0); ApplySettings(); });
        if (resolutionDropdown) resolutionDropdown.onValueChanged.AddListener(v => { PlayerPrefs.SetInt("Resolution", v); ApplySettings(); });
    }

    void LoadSettings()
    {
        if (!PlayerPrefs.HasKey("BGM")) PlayerPrefs.SetFloat("BGM", 0.5f);
        if (!PlayerPrefs.HasKey("SFX")) PlayerPrefs.SetFloat("SFX", 0.7f);
        if (!PlayerPrefs.HasKey("Voice")) PlayerPrefs.SetFloat("Voice", 0.7f);
        if (!PlayerPrefs.HasKey("TextSpeed")) PlayerPrefs.SetFloat("TextSpeed", 1f);
        if (!PlayerPrefs.HasKey("UiScale")) PlayerPrefs.SetFloat("UiScale", 1f);
        if (!PlayerPrefs.HasKey("ScreenFit")) PlayerPrefs.SetInt("ScreenFit", 1);
        if (!PlayerPrefs.HasKey("Fullscreen")) PlayerPrefs.SetInt("Fullscreen", 1);
        if (!PlayerPrefs.HasKey("Resolution")) PlayerPrefs.SetInt("Resolution", 0);
        PlayerPrefs.Save();
    }

    void ResetSettings()
    {
        PlayerPrefs.SetFloat("BGM", 0.5f);
        PlayerPrefs.SetFloat("SFX", 0.7f);
        PlayerPrefs.SetFloat("Voice", 0.7f);
        PlayerPrefs.SetFloat("TextSpeed", 1f);
        PlayerPrefs.SetFloat("UiScale", 1f);
        PlayerPrefs.SetInt("ScreenFit", 1);
        PlayerPrefs.SetInt("Fullscreen", 1);
        PlayerPrefs.SetInt("Resolution", 0);
        PlayerPrefs.Save();
        ApplySettings();
    }

    void ApplySettings()
    {
        if (bgmSlider) bgmSlider.value = PlayerPrefs.GetFloat("BGM");
        if (sfxSlider) sfxSlider.value = PlayerPrefs.GetFloat("SFX");
        if (voiceVolumeSlider) voiceVolumeSlider.value = PlayerPrefs.GetFloat("Voice");
        if (textSpeedSlider) textSpeedSlider.value = PlayerPrefs.GetFloat("TextSpeed");
        if (uiScaleSlider) uiScaleSlider.value = PlayerPrefs.GetFloat("UiScale");
        if (screenFitDropdown) screenFitDropdown.value = Mathf.Clamp(PlayerPrefs.GetInt("ScreenFit"), 0, screenFitDropdown.options.Count - 1);
        if (fullscreenToggle) fullscreenToggle.isOn = PlayerPrefs.GetInt("Fullscreen") == 1;
        if (resolutionDropdown) resolutionDropdown.value = Mathf.Clamp(PlayerPrefs.GetInt("Resolution"), 0, resolutionDropdown.options.Count - 1);

        if (canvasScaler == null) canvasScaler = FindObjectOfType<CanvasScaler>();
        if (canvasScaler)
        {
            canvasScaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            canvasScaler.referenceResolution = new Vector2(1920, 1080);
            int fit = PlayerPrefs.GetInt("ScreenFit");
            canvasScaler.screenMatchMode = fit == 2 ? CanvasScaler.ScreenMatchMode.Expand
                                                   : CanvasScaler.ScreenMatchMode.Shrink;
            if (fit <= 1) canvasScaler.matchWidthOrHeight = fit == 0 ? 0f : 1f;
            canvasScaler.scaleFactor = PlayerPrefs.GetFloat("UiScale");
        }

        bool fs = PlayerPrefs.GetInt("Fullscreen") == 1;
        int r = PlayerPrefs.GetInt("Resolution");
        if (r == 1) Screen.SetResolution(1280, 720, fs);
        else if (r == 2) Screen.SetResolution(1600, 900, fs);
        else Screen.SetResolution(1920, 1080, fs);
    }

    // ---------- 界面切换 ----------
    void SetRoot(GameObject root, bool on) { if (root) root.SetActive(on); }

    void ShowMainMenu()
    {
        screen = ScreenMode.MainMenu;
        StopAllCoroutines();
        typer = null;
        typing = waiting = holding = ended = false;
        floatLevel = floatTarget = 0f;
        ApplyFloat();
        SetRoot(mainMenuRoot, true);
        SetRoot(settingsMenuRoot, false);
        SetRoot(settingsGameRoot, false);
        SetRoot(endRoot, false);
        if (settingsGameButton) settingsGameButton.gameObject.SetActive(false);
        if (titleGameButton) titleGameButton.gameObject.SetActive(false);
        SetRoot(choiceRoot, false);
        SetRoot(holdRoot, false);
        SetRoot(dialogPanel.gameObject, false);
        if (timeText) timeText.gameObject.SetActive(false);
        if (chapterTitle) chapterTitle.gameObject.SetActive(false);
        if (bgNormal) bgNormal.sprite = Resources.Load<Sprite>("Art/bg_classroom");
        if (bgGray) { bgGray.sprite = null; bgGray.color = Color.clear; }
        if (fog) fog.color = Color.clear;
        if (vignette) vignette.color = Color.clear;
        if (bodyText) bodyText.text = "";
        if (speakerText) speakerText.text = "";
    }

    public void StartGame()
    {
        SetRoot(mainMenuRoot, false);
        SetRoot(settingsMenuRoot, false);
        SetRoot(settingsGameRoot, false);
        SetRoot(endRoot, false);
        if (settingsGameButton) settingsGameButton.gameObject.SetActive(true);
        if (titleGameButton) titleGameButton.gameObject.SetActive(true);
        SetRoot(dialogPanel.gameObject, true);
        screen = ScreenMode.Game;
        ended = false;
        pc = 0;
        cmds.Clear();
        Parse();
        SetBg("classroom");
        Next();
    }

    void OpenSettingsFromMenu()
    {
        settingsReturn = ScreenMode.MainMenu;
        SetRoot(mainMenuRoot, false);
        SetRoot(settingsMenuRoot, true);
        screen = ScreenMode.SettingsMenu;
    }

    void OpenSettingsFromGame()
    {
        settingsReturn = ScreenMode.Game;
        SetRoot(settingsGameRoot, true);
        screen = ScreenMode.GameSettings;
    }

    void CloseSettings()
    {
        SetRoot(settingsMenuRoot, false);
        SetRoot(settingsGameRoot, false);
        screen = settingsReturn;
        if (screen == ScreenMode.MainMenu) SetRoot(mainMenuRoot, true);
    }

    public void QuitGame()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    // ---------- 剧本解析 ----------
    void Parse()
    {
        TextAsset ta = Resources.Load<TextAsset>("Script/main");
        string[] lines = ta.text.Replace("\r\n", "\n").Split('\n');
        string[] kws = Keywords.Split(' ');
        foreach (string raw in lines)
        {
            string line = raw.Trim();
            if (line.Length == 0 || line.StartsWith("#")) continue;
            int sp = line.IndexOf(' ');
            string head = sp < 0 ? line : line.Substring(0, sp);
            string rest = sp < 0 ? "" : line.Substring(sp + 1);
            bool isKw = false;
            foreach (string k in kws)
                if (head == k) { isKw = true; break; }
            if (isKw)
                cmds.Add(new Cmd { op = head, arg = rest });
            else
            {
                int ci = line.IndexOf('：');
                if (ci > 0 && ci < 8)
                    cmds.Add(new Cmd { op = "_say", arg = line.Substring(0, ci) + "\u0001" + line.Substring(ci + 1) });
                else
                    cmds.Add(new Cmd { op = "_narr", arg = line });
            }
        }
    }

    int IndexOfLabel(string name)
    {
        for (int i = 0; i < cmds.Count; i++)
            if (cmds[i].op == "label" && cmds[i].arg == name)
                return i;
        return pc;
    }

    // ---------- 主流程 ----------
    void Next()
    {
        if (hintText) hintText.gameObject.SetActive(false);
        while (pc < cmds.Count)
        {
            Cmd c = cmds[pc++];
            switch (c.op)
            {
                case "bg": SetBg(c.arg); break;
                case "float": floatTarget = Mathf.Clamp01(float.Parse(c.arg)); break;
                case "time": ShowTime(c.arg); break;
                case "title": StartCoroutine(ShowTitle(c.arg)); break;
                case "label": break;
                case "jump": pc = IndexOfLabel(c.arg); break;
                case "choice": ShowChoices(c.arg); return;
                case "hold": ShowHold(c.arg); return;
                case "end": ShowEnd(); return;
                case "_say":
                    {
                        string[] p = c.arg.Split('\u0001');
                        ShowDialogue(p[0], p[1]);
                        return;
                    }
                case "_narr":
                    ShowDialogue("", c.arg);
                    return;
            }
        }
        ShowEnd();
    }

    void ShowEnd()
    {
        ended = true;
        typing = waiting = holding = false;
        if (hintText)
        {
            if (hintText) hintText.gameObject.SetActive(true);
            hintText.text = "— 完 —";
        }
        SetRoot(choiceRoot, false);
        SetRoot(holdRoot, false);
        if (!endRoot)
        {
            // 旧场景没有结尾菜单：停在提示文字，Esc 回主菜单无效时可按退出键 Alt+F4
            if (hintText) hintText.text = "— 完 —\n按 Alt+F4 退出";
            screen = ScreenMode.Ending;
            return;
        }
        SetRoot(dialogPanel.gameObject, false);
        SetRoot(endRoot, true);
        screen = ScreenMode.Ending;
    }

    // ---------- 背景与效果 ----------
    void SetBg(string name)
    {
        Texture2D tex = Resources.Load<Texture2D>("Art/" + name);
        if (tex == null) return;
        if (bgNormal) bgNormal.sprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), 100f);
        MakeGray(tex);
    }

    void MakeGray(Texture2D src)
    {
        if (grayTex != null) Destroy(grayTex);
        Color[] px = src.GetPixels();
        for (int i = 0; i < px.Length; i++)
        {
            float g = px[i].r * 0.299f + px[i].g * 0.587f + px[i].b * 0.114f;
            px[i] = new Color(g, g, g, px[i].a);
        }
        grayTex = new Texture2D(src.width, src.height, TextureFormat.RGBA32, false);
        grayTex.SetPixels(px);
        grayTex.Apply();
        if (bgGray) bgGray.sprite = Sprite.Create(grayTex, new Rect(0, 0, src.width, src.height), new Vector2(0.5f, 0.5f), 100f);
    }

    Sprite RadialSprite(Color col, float innerA, float edgeA)
    {
        int n = 256;
        Texture2D tex = new Texture2D(n, n, TextureFormat.RGBA32, false);
        Color32[] px = new Color32[n * n];
        Vector2 c = new Vector2(n / 2f, n / 2f);
        byte r = (byte)(col.r * 255f), g = (byte)(col.g * 255f), b = (byte)(col.b * 255f);
        for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float t = Mathf.Clamp01(Vector2.Distance(new Vector2(x, y), c) / (n * 0.5f));
                float a = Mathf.Lerp(innerA, edgeA, t * t);
                px[y * n + x] = new Color32(r, g, b, (byte)(a * 255f));
            }
        tex.SetPixels32(px);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), 100f);
    }

    void MakeRadialSprites()
    {
        if (fog) fog.sprite = RadialSprite(Color.white, 0.45f, 0.4f);
        if (vignette) vignette.sprite = RadialSprite(Color.black, 0f, 0.5f);
    }

    void ApplyFloat()
    {
        if (bgGray)
        {
            Color c = bgGray.color; c.a = floatLevel * 0.75f; bgGray.color = c;
        }
        if (fog)
        {
            Color c = fog.color; c.a = Mathf.Clamp01(floatLevel * 0.6f); fog.color = c;
        }
        wobbleT += Time.deltaTime * (1f + floatLevel * 3f);
        float a = bodyAlphaBase * (1f - floatLevel * 0.2f)
                  - Mathf.Max(0f, Mathf.Sin(wobbleT * 2.1f)) * 0.09f * floatLevel;
        if (bodyText) bodyText.color = new Color(bodyBase.r, bodyBase.g, bodyBase.b, Mathf.Clamp01(a));
        if (dialogPanel)
        {
            Color target = floatLevel > 0.35f ? panelColdRuntime : panelWarmRuntime;
            if (!panelBaseInitialized)
            {
                panelWarmRuntime = dialogPanel.color;
                panelColdRuntime = PanelCold;
                panelBaseInitialized = true;
            }
            dialogPanel.color = Color.Lerp(panelWarmRuntime, target, Mathf.Clamp01(floatLevel));
            var outline = dialogPanel.GetComponent<Outline>();
            if (outline == null)
            {
                outline = dialogPanel.gameObject.AddComponent<Outline>();
                outline.effectDistance = new Vector2(2f, -2f);
            }
            Color ec = PanelEdge;
            ec.a = Mathf.Clamp01(floatLevel * 1.2f) * 0.95f;
            outline.effectColor = ec;
        }
    }

    // ---------- 对话 ----------
    void ShowDialogue(string speaker, string text)
    {
        if (typer != null) StopCoroutine(typer);
        fullText = text;
        if (speakerText) speakerText.gameObject.SetActive(speaker.Length > 0);
        if (speakerText) speakerText.text = speaker;
        if (bodyText) bodyText.text = "";
        typing = true;
        waiting = false;
        typer = StartCoroutine(TypeLine(text));
    }

    IEnumerator TypeLine(string text)
    {
        float speed = Mathf.Max(0.25f, PlayerPrefs.GetFloat("TextSpeed", 1f));
        float delay = 0.035f * (1f + floatLevel * 2.2f) / speed;
        var sb = new System.Text.StringBuilder();
        foreach (char ch in text)
        {
            sb.Append(ch);
            if (bodyText) bodyText.text = sb.ToString();
            float d = delay;
            if ("，。、？！…—".IndexOf(ch) >= 0) d += 0.12f / speed;
            yield return new WaitForSeconds(d);
        }
        typing = false;
        waiting = true;
        if (hintText) hintText.gameObject.SetActive(true);
    }

    void CompleteLine()
    {
        if (typer != null) StopCoroutine(typer);
        if (bodyText) bodyText.text = fullText;
        typing = false;
        waiting = true;
        if (hintText) hintText.gameObject.SetActive(true);
    }

    // ---------- 命令 UI ----------
    void ShowTime(string t)
    {
        if (timeText) timeText.gameObject.SetActive(true);
        timeText.text = t;
        StartCoroutine(FadeIn(timeText, 0.85f));
    }

    IEnumerator ShowTitle(string t)
    {
        Color c0 = chapterTitle.color;
        if (chapterTitle) chapterTitle.gameObject.SetActive(true);
        chapterTitle.text = t;
        for (float f = 0; f < 1f; f += Time.deltaTime * 1.2f)
        {
            chapterTitle.color = new Color(c0.r, c0.g, c0.b, f);
            yield return null;
        }
        yield return new WaitForSeconds(1.4f);
        for (float f = 1f; f > 0f; f -= Time.deltaTime * 1.2f)
        {
            chapterTitle.color = new Color(c0.r, c0.g, c0.b, f);
            yield return null;
        }
        chapterTitle.color = c0;
        if (chapterTitle) chapterTitle.gameObject.SetActive(false);
    }

    IEnumerator FadeIn(Graphic g, float target)
    {
        Color c0 = g.color;
        for (float f = 0; f < target; f += Time.deltaTime * 2f)
        {
            g.color = new Color(c0.r, c0.g, c0.b, f);
            yield return null;
        }
    }

    void ShowChoices(string spec)
    {
        if (!choiceRoot || !choiceButtonPrefab) { Next(); return; }
        choiceRoot.SetActive(true);
        foreach (Transform t in choiceRoot.transform) Destroy(t.gameObject);
        string[] opts = spec.Split('|');
        bool disabled = floatLevel > 0.55f;
        hadChoiceThisFrame = true;
        foreach (string o in opts)
        {
            int arrow = o.IndexOf("->");
            string label = arrow < 0 ? o.Trim() : o.Substring(0, arrow).Trim();
            string target = arrow < 0 ? "" : o.Substring(arrow + 2).Trim();
            Button b = Instantiate(choiceButtonPrefab, choiceRoot.transform);
            Text bt = b.GetComponentInChildren<Text>();
            bt.text = label;
            b.interactable = !disabled;
            string tgt = target;
            b.onClick.AddListener(delegate
            {
                choiceRoot.SetActive(false);
                if (tgt.Length > 0) pc = IndexOfLabel(tgt);
                Next();
            });
        }
    }

    void ShowHold(string text)
    {
        if (!holdRoot || !holdRing || !holdLabel) { Next(); return; }
        holdRoot.SetActive(true);
        holdLabel.text = text + "\n（按住鼠标左键，直到圆环闭合）";
        holding = true;
        holdProgress = 0f;
        holdRing.fillAmount = 0f;
    }

    void UpdateHold()
    {
        if (Input.GetMouseButton(0)) holdProgress += Time.deltaTime / 2.5f;
        else holdProgress -= Time.deltaTime * 0.15f;
        holdProgress = Mathf.Clamp01(holdProgress);
        holdRing.fillAmount = holdProgress;
        if (holdProgress >= 1f)
        {
            holding = false;
            holdRoot.SetActive(false);
            floatTarget = 0f;
            Next();
        }
    }

    // ---------- 输入 ----------
    bool OverUI()
    {
        if (EventSystem.current == null) return false;
        var ped = new PointerEventData(EventSystem.current) { position = Input.mousePosition };
        raycastResults.Clear();
        EventSystem.current.RaycastAll(ped, raycastResults);
        foreach (var r in raycastResults)
        {
            if (r.gameObject == null) continue;
            string n = r.gameObject.name;
            if (n == "BgNormal" || n == "BgGray" || n == "Fog" || n == "Vignette") continue;
            if (n == "DialogPanel" || n == "Speaker" || n == "Body" || n == "Hint") continue;
            return true;
        }
        return false;
    }

    void Update()
    {
        consumedFrameClick = false;
        hadChoiceThisFrame = false;
        floatLevel = Mathf.MoveTowards(floatLevel, floatTarget, Time.deltaTime * 0.3f);
        ApplyFloat();

        if (Input.GetMouseButtonDown(0) && OverUI() && (!choiceRoot || !choiceRoot.activeSelf))
            consumedFrameClick = true;

        if (screen == ScreenMode.Ending)
        {
            if (Input.GetKeyDown(KeyCode.Escape)) ShowMainMenu();
            return;
        }
        if (screen == ScreenMode.GameSettings)
        {
            if (Input.GetKeyDown(KeyCode.Escape)) CloseSettings();
            return;
        }
        if (screen != ScreenMode.Game) return;

        if (holding)
        {
            UpdateHold();
            return;
        }
        if (choiceRoot && choiceRoot.activeSelf) return; // 选项界面只能点按钮，不能"继续"

        bool click = (Input.GetMouseButtonDown(0) && !consumedFrameClick)
                     || Input.GetKeyDown(KeyCode.Space)
                     || Input.GetKeyDown(KeyCode.Return);
        if (Input.GetKeyDown(KeyCode.Escape)) { OpenSettingsFromGame(); return; }
        if (!click) return;

        if (typing) CompleteLine();
        else if (waiting) Next();
    }

}
