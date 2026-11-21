using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 轻量视觉小说引擎：《飘》
/// 支持命令：bg / float / time / title / label / jump / choice / hold / end
/// 对话行格式：说话人：文本 ；无冒号行为旁白
/// </summary>
public class VNEngine : MonoBehaviour
{
    [Header("UI 引用")]
    public Image bgNormal;
    public Image bgGray;          // 灰度副本，透明度=飘离程度
    public Image fog;             // 雾化层
    public Image vignette;        // 暗角
    public Text timeText;
    public Text chapterTitle;
    public Text speakerText;
    public Text bodyText;
    public Text hintText;
    public GameObject choiceRoot;
    public Button choiceButtonPrefab;
    public GameObject holdRoot;
    public Image holdRing;
    public Text holdLabel;

    class Cmd { public string op; public string arg; }
    readonly List<Cmd> cmds = new List<Cmd>();
    int pc;
    const string Keywords = "bg float time title label jump choice hold end";

    float floatLevel, floatTarget;
    float wobbleT;
    bool typing, waiting, holding;
    float holdProgress;
    string fullText = "";
    Coroutine typer;
    Texture2D grayTex;
    Color bodyBase;
    float bodyAlphaBase;

    void Start()
    {
        Parse();
        bodyBase = bodyText.color;
        bodyAlphaBase = bodyBase.a;
        MakeRadialSprites();
        timeText.gameObject.SetActive(false);
        chapterTitle.gameObject.SetActive(false);
        hintText.gameObject.SetActive(false);
        choiceRoot.SetActive(false);
        holdRoot.SetActive(false);
        Next();
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
        hintText.gameObject.SetActive(false);
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
                case "end":
                    hintText.gameObject.SetActive(true);
                    hintText.text = "— 完 —";
                    return;
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
    }

    // ---------- 背景与效果 ----------
    void SetBg(string name)
    {
        Texture2D tex = Resources.Load<Texture2D>("Art/" + name);
        if (tex == null) return;
        bgNormal.sprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), 100f);
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
        bgGray.sprite = Sprite.Create(grayTex, new Rect(0, 0, src.width, src.height), new Vector2(0.5f, 0.5f), 100f);
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
                float t = Vector2.Distance(new Vector2(x, y), c) / (n * 0.5f);
                t = Mathf.Clamp01(t);
                float a = Mathf.Lerp(innerA, edgeA, t * t);
                px[y * n + x] = new Color32(r, g, b, (byte)(a * 255f));
            }
        tex.SetPixels32(px);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), 100f);
    }

    void MakeRadialSprites()
    {
        fog.sprite = RadialSprite(Color.white, 0.45f, 0.4f);
        vignette.sprite = RadialSprite(Color.black, 0f, 0.5f);
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
        float a = bodyAlphaBase * (1f - floatLevel * 0.25f)
                  - Mathf.Max(0f, Mathf.Sin(wobbleT * 2.1f)) * 0.12f * floatLevel;
        bodyText.color = new Color(bodyBase.r, bodyBase.g, bodyBase.b, Mathf.Clamp01(a));
    }

    // ---------- 对话 ----------
    void ShowDialogue(string speaker, string text)
    {
        if (typer != null) StopCoroutine(typer);
        fullText = text;
        speakerText.gameObject.SetActive(speaker.Length > 0);
        speakerText.text = speaker;
        bodyText.text = "";
        typing = true;
        waiting = false;
        typer = StartCoroutine(TypeLine(text));
    }

    IEnumerator TypeLine(string text)
    {
        float delay = 0.035f * (1f + floatLevel * 2.2f);
        System.Text.StringBuilder sb = new System.Text.StringBuilder();
        foreach (char ch in text)
        {
            sb.Append(ch);
            bodyText.text = sb.ToString();
            float d = delay;
            if ("，。、？！…—".IndexOf(ch) >= 0) d += 0.12f;
            yield return new WaitForSeconds(d);
        }
        typing = false;
        waiting = true;
        hintText.gameObject.SetActive(true);
    }

    void CompleteLine()
    {
        if (typer != null) StopCoroutine(typer);
        bodyText.text = fullText;
        typing = false;
        waiting = true;
        hintText.gameObject.SetActive(true);
    }

    // ---------- 命令 UI ----------
    void ShowTime(string t)
    {
        timeText.gameObject.SetActive(true);
        timeText.text = t;
        StartCoroutine(FadeIn(timeText, 0.85f));
    }

    IEnumerator ShowTitle(string t)
    {
        Color c0 = chapterTitle.color;
        chapterTitle.gameObject.SetActive(true);
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
        chapterTitle.gameObject.SetActive(false);
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
        choiceRoot.SetActive(true);
        foreach (Transform t in choiceRoot.transform) Destroy(t.gameObject);
        string[] opts = spec.Split('|');
        bool disabled = floatLevel > 0.55f;
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
        holdRoot.SetActive(true);
        holdLabel.text = text + "\n（按住鼠标，直到圆环闭合）";
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
    void Update()
    {
        floatLevel = Mathf.MoveTowards(floatLevel, floatTarget, Time.deltaTime * 0.3f);
        ApplyFloat();

        if (holding)
        {
            UpdateHold();
            return;
        }

        bool click = Input.GetMouseButtonDown(0)
                     || Input.GetKeyDown(KeyCode.Space)
                     || Input.GetKeyDown(KeyCode.Return);
        if (!click) return;

        if (typing) CompleteLine();
        else if (waiting) Next();
    }
}
