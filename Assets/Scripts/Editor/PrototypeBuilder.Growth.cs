using System.IO;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 생명체 성장 노드 (토끼). 메뉴: Roguelike/Setup Rabbit Growth (재실행해도 안전: 노드 데이터는 비어 있을 때만 채움).
/// 만드는 것: UI 스프라이트, 직선 당근 프리팹(CarrotShot), RabbitGrowth.asset(노드 30개, 당근 외곽선 배치), HUD_GrowthTree 프리팹,
/// 성장 재화(성장의 씨앗) 자원 정의, 토끼 CreatureDef.growth / WardConfig.growthTreePrefab 연결, HUD_SkillSelect 줄에 '추천' 표시 추가.
/// </summary>
public static partial class PrototypeBuilder
{
    const string GrowthSpriteDir = Root + "/Prefabs/UI/Sprites/Growth";
    /// <summary>성장 config / 평타 에셋 폴더 (Resources/Config/Growth).</summary>
    const string GrowthConfigDir = Root + "/Resources/Config/Growth";
    const string RabbitGrowthPath = GrowthConfigDir + "/RabbitGrowth.asset";

    static readonly Color Brown = new Color(0.42f, 0.29f, 0.32f), SoftBrown = new Color(0.52f, 0.42f, 0.47f),
        Cream = new Color(1f, 0.98f, 0.95f), PinkBtn = new Color(1f, 0.64f, 0.69f), SeedColor = new Color(1f, 0.82f, 0.5f);

    [MenuItem("Roguelike/Setup Rabbit Growth")] static void SetupRabbitGrowth() => SetupRabbitGrowth(false);
    [MenuItem("Roguelike/Setup Rabbit Growth (노드 기본값으로 다시 채움)")] static void SetupRabbitGrowthReset() => SetupRabbitGrowth(true);

    static void SetupRabbitGrowth(bool resetNodes)
    {
        var sp = BuildGrowthSprites();
        var shot = BuildCarrotShotPrefab();

        var g = AssetDatabase.LoadAssetAtPath<RabbitGrowthConfig>(RabbitGrowthPath);
        if (!g) { g = ScriptableObject.CreateInstance<RabbitGrowthConfig>(); AssetDatabase.CreateAsset(g, RabbitGrowthPath); }
        if (resetNodes || g.nodes == null || g.nodes.Length == 0) g.nodes = RabbitNodes();
        g.creatureName = "토끼"; g.title = "토끼의 성장";
        g.silhouette = sp.carrot;
        g.rabbit.shotPrefab = shot;
        EditorUtility.SetDirty(g);

        var cfg = AssetDatabase.LoadAssetAtPath<GameConfig>(Root + "/Resources/GameConfig.asset");
        // 성장 재화 자원 정의
        var res = cfg.meta.resources ?? new ResourceDef[0];
        if (System.Array.FindIndex(res, r => r != null && r.type == ResourceType.GrowthSeed) < 0)
        {
            ArrayUtility.Add(ref res, new ResourceDef { type = ResourceType.GrowthSeed, name = "성장의 씨앗", color = SeedColor });
            cfg.meta.resources = res;
            EditorUtility.SetDirty(cfg.meta);
        }
        // 토끼 연결
        if (cfg.ward.creatures != null)
            foreach (var c in cfg.ward.creatures)
                if (c != null && (c.name == "토끼" || (c.model && c.model.name.Contains("Rabbit")))) c.growth = g;
        cfg.ward.growthTreePrefab = BuildGrowthTreeUI(sp).GetComponent<GrowthTreeUI>();
        EditorUtility.SetDirty(cfg.ward);
        PatchSkillSelectRecommend(sp);
        AssetDatabase.SaveAssets();
        Debug.Log("[PrototypeBuilder] 토끼 성장 노드 설정 완료 (로비에서 K)");
    }

    // ===================== 노드 데이터 (당근 외곽선) =====================

    static GrowthNodeDef[] RabbitNodes()
    {
        var list = new System.Collections.Generic.List<GrowthNodeDef>(30);
        int sid = 0;
        string Stat(Vector2 p, int cost, StatType st, string req)
        {
            string id = "s" + (++sid);
            var (name, desc, v) = st switch
            {
                StatType.Damage => ("단단한 당근", "공격력 +0.3", 0.3f),
                StatType.WardMaxHp => ("통통한 볼", "체력 +4", 4f),
                StatType.WardSpeed => ("가벼운 발", "이동 속도 +0.08", 0.08f),
                StatType.AttackSpeed => ("재빠른 손", "공격 속도 +2%", 0.02f),
                StatType.AttackRange => ("긴 귀", "사거리 +0.4", 0.4f),
                StatType.WardRegen => ("포근한 털", "초당 회복 +0.05", 0.05f),
                _ => ("큰 당근", "공격 범위 +3%", 0.03f), // Area
            };
            list.Add(new GrowthNodeDef { id = id, name = name, desc = desc, kind = GrowthNodeKind.Stat, position = p, cost = cost, stat = st, value = v, requires = Req(req) });
            return id;
        }
        string Node(string id, GrowthNodeKind k, GrowthEffect e, string name, string desc, Vector2 p, int cost, params string[] req)
        {
            list.Add(new GrowthNodeDef { id = id, name = name, desc = desc, kind = k, effect = e, position = p, cost = cost, requires = req });
            return id;
        }

        // 몸통 옆선 (위 0.60 → 끝 0.05, 살짝 볼록)
        Vector2 Side(float t, bool right)
        {
            float x = Mathf.Lerp(0.30f, 0.5f, t) - 0.04f * Mathf.Sin(Mathf.PI * t);
            return new Vector2(right ? 1f - x : x, Mathf.Lerp(0.60f, 0.05f, t));
        }

        string root = Stat(new Vector2(0.5f, 0.60f), 1, StatType.Damage, null);
        string[] tips = new string[2];
        for (int side = 0; side < 2; side++)
        {
            bool r = side == 1;
            float sx = r ? 1f : -1f;
            StatType[] st = r
                ? new[] { StatType.WardMaxHp, StatType.AttackSpeed, StatType.Damage, StatType.WardSpeed, StatType.Area, StatType.Damage, StatType.WardRegen }
                : new[] { StatType.WardMaxHp, StatType.WardSpeed, StatType.Damage, StatType.AttackSpeed, StatType.AttackRange, StatType.Damage, StatType.WardMaxHp };
            string prev = Stat(new Vector2(0.5f + 0.1f * sx, 0.60f), 1, st[0], root); // 윗변
            prev = Stat(new Vector2(0.5f + 0.2f * sx, 0.60f), 1, st[1], prev);        // 모서리
            int k = 2;
            for (int i = 1; i <= 6; i++)
            {
                var p = Side(i / 7f, r);
                if (i == 3)
                    prev = r
                        ? Node("main_small", GrowthNodeKind.Main, GrowthEffect.SmallCarrots, "소형 당근",
                            "평타가 부채꼴로 작은 당근 3발이 됩니다.\n적과 가까울수록 피해가 커집니다.\n적중 시 확률적으로 당근이 튕겨 다른 적을 맞힙니다. ('팅기는 당근'이 있으면 확률 증가)", p, 5, prev)
                        : Node("main_pierce", GrowthNodeKind.Main, GrowthEffect.PierceCarrot, "관통 당근",
                            "평타가 곧게 날아가는 관통 당근이 됩니다.\n최대 3명을 관통하고, 관통할수록 피해가 줄어듭니다.\n확률적으로 당근이 적에게 꼬칩니다. ('당근 꼬치'가 있으면 꼬치는 순간 범위 폭발)", p, 5, prev);
                else prev = Stat(p, i < 3 ? 2 : 3, st[k++], prev);
            }
            tips[side] = prev;
        }
        Node("watcher_rain", GrowthNodeKind.Watcher, GrowthEffect.CarrotRain, "당근 폭격",
            "감시자의 폭격 스킬이 발동할 때\n맵 곳곳에 당근이 하늘에서 떨어집니다.", new Vector2(0.5f, 0.05f), 6, tips[0], tips[1]);

        // 잎 3갈래 (뿌리 → 잎 끝 = 패시브)
        (Vector2 dir, StatType[] st, string id, GrowthEffect e, string name, string desc)[] leaves =
        {
            (new Vector2(-0.06f, 0.075f), new[] { StatType.Damage, StatType.AttackSpeed, StatType.Area }, "p_skewer", GrowthEffect.CarrotSkewer, "당근 꼬치",
                "적중 시 낮은 확률로 당근이 적에게 꼬칩니다.\n꼬친 적은 바로 죽지 않고, 평타를 한 번 더 맞으면 범위 폭발하며 터집니다."),
            (new Vector2(0f, 0.085f), new[] { StatType.WardMaxHp, StatType.WardRegen, StatType.WardMaxHp }, "p_lifesteal", GrowthEffect.Lifesteal, "당근 영양",
                "평타로 준 피해의 일부만큼 토끼가 회복합니다."),
            (new Vector2(0.06f, 0.075f), new[] { StatType.WardSpeed, StatType.AttackRange, StatType.Damage }, "p_bounce", GrowthEffect.BounceCarrot, "팅기는 당근",
                "적중 시 낮은 확률로 당근이 튕겨 다른 적을 맞힙니다."),
        };
        foreach (var lf in leaves)
        {
            string prev = root;
            for (int i = 0; i < 3; i++) prev = Stat(new Vector2(0.5f, 0.60f) + lf.dir * (i + 1), i + 1, lf.st[i], prev);
            Node(lf.id, GrowthNodeKind.Passive, lf.e, lf.name, lf.desc, new Vector2(0.5f, 0.60f) + lf.dir * 4.15f, 4, prev);
        }
        return list.ToArray();
    }

    static string[] Req(string id) => id == null ? new string[0] : new[] { id };

    // ===================== 스프라이트 =====================

    struct GrowthSprites { public Sprite dot, ring, glow, round, dash, bg, carrot; }

    static GrowthSprites BuildGrowthSprites()
    {
        if (!AssetDatabase.IsValidFolder(GrowthSpriteDir)) AssetDatabase.CreateFolder(Root + "/Prefabs/UI/Sprites", "Growth");
        var s = new GrowthSprites
        {
            dot = Tex("G_Dot", 64, 64, (x, y) => A(31.5f - R(x, y, 64)), Vector4.zero),
            ring = Tex("G_Ring", 64, 64, (x, y) => { float r = R(x, y, 64); return A(Mathf.Min(31.5f - r, r - 26f)); }, Vector4.zero),
            glow = Tex("G_Glow", 128, 128, (x, y) => { float k = Mathf.Max(0f, 1f - R(x, y, 128) / 63.5f); return new Color(1, 1, 1, k * k); }, Vector4.zero),
            round = Tex("G_Round", 64, 64, (x, y) =>
            {
                float cx = Mathf.Clamp(x + 0.5f, 24f, 40f), cy = Mathf.Clamp(y + 0.5f, 24f, 40f);
                return A(24f - Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(cx, cy)));
            }, new Vector4(26, 26, 26, 26)),
            dash = Tex("G_Dash", 20, 6, (x, y) => new Color(1, 1, 1, x < 11 ? (y == 0 || y == 5 ? 0.45f : 1f) : 0f), Vector4.zero, true),
            bg = Tex("G_Bg", 8, 256, (x, y) =>
            {
                float v = y / 255f;
                Color top = new Color(0.88f, 0.84f, 0.98f), mid = new Color(1f, 0.87f, 0.91f), bot = new Color(1f, 0.95f, 0.88f);
                return v > 0.5f ? Color.Lerp(mid, top, (v - 0.5f) * 2f) : Color.Lerp(bot, mid, v * 2f);
            }, Vector4.zero),
            carrot = Tex("G_CarrotSilhouette", 410, 480, CarrotSilhouette, Vector4.zero),
        };
        return s;
    }

    static float R(int x, int y, int size) => Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(size * 0.5f, size * 0.5f));
    static Color A(float a) => new Color(1, 1, 1, Mathf.Clamp01(a + 0.5f));

    /// <summary>당근 실루엣 (노드 레이아웃과 같은 좌표: 몸통 0.60~0.05, 잎 3갈래). 2x2 슈퍼샘플.</summary>
    static Color CarrotSilhouette(int x, int y)
    {
        const float W = 410f, H = 480f;
        int hit = 0;
        for (int sy = 0; sy < 2; sy++)
            for (int sx = 0; sx < 2; sx++)
            {
                float u = (x + 0.25f + sx * 0.5f) / W, v = (y + 0.25f + sy * 0.5f) / H;
                bool inside = false;
                if (v >= 0.03f && v <= 0.62f)
                {
                    float t = Mathf.Clamp01((0.62f - v) / 0.59f);
                    float xl = Mathf.Lerp(0.29f, 0.5f, t) - 0.045f * Mathf.Sin(Mathf.PI * t);
                    inside = Mathf.Abs(u - 0.5f) <= 0.5f - xl;
                }
                if (!inside)
                {
                    Vector2 p = new Vector2(u * W, v * H), b = new Vector2(0.5f * W, 0.6f * H);
                    foreach (var tip in new[] { new Vector2(0.24f, 0.93f), new Vector2(0.5f, 0.97f), new Vector2(0.76f, 0.93f) })
                    {
                        Vector2 e = new Vector2(tip.x * W, tip.y * H), ab = e - b;
                        float k = Mathf.Clamp01(Vector2.Dot(p - b, ab) / ab.sqrMagnitude);
                        float rad = Mathf.Lerp(26f, 12f, k) * (0.7f + 0.6f * Mathf.Sin(Mathf.PI * Mathf.Min(1f, k * 1.2f)));
                        if (Vector2.Distance(p, b + ab * k) <= rad) { inside = true; break; }
                    }
                }
                if (inside) hit++;
            }
        return new Color(1, 1, 1, hit / 4f);
    }

    static Sprite Tex(string name, int w, int h, System.Func<int, int, Color> f, Vector4 border, bool repeat = false)
    {
        string path = $"{GrowthSpriteDir}/{name}.png";
        if (!File.Exists(path))
        {
            var t = new Texture2D(w, h, TextureFormat.RGBA32, false);
            var px = new Color[w * h];
            for (int y = 0; y < h; y++) for (int x = 0; x < w; x++) px[y * w + x] = f(x, y);
            t.SetPixels(px); t.Apply();
            File.WriteAllBytes(path, t.EncodeToPNG());
            Object.DestroyImmediate(t);
            AssetDatabase.ImportAsset(path);
        }
        var imp = (TextureImporter)AssetImporter.GetAtPath(path);
        imp.textureType = TextureImporterType.Sprite;
        imp.spriteImportMode = SpriteImportMode.Single;
        imp.mipmapEnabled = false;
        imp.alphaIsTransparency = true;
        imp.wrapMode = repeat ? TextureWrapMode.Repeat : TextureWrapMode.Clamp;
        imp.spriteBorder = border;
        imp.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }

    // ===================== 직선 당근 프리팹 =====================

    static CarrotShot BuildCarrotShotPrefab()
    {
        string path = $"{CarrotDir}/CarrotShot.prefab";
        var existing = AssetDatabase.LoadAssetAtPath<CarrotShot>(path);
        if (existing) return existing; // 프리팹을 직접 다듬었을 수 있으니 덮어쓰지 않음
        var src = AssetDatabase.LoadAssetAtPath<GameObject>($"{CarrotDir}/Carrot.prefab");
        var go = (GameObject)PrefabUtility.InstantiatePrefab(src);
        PrefabUtility.UnpackPrefabInstance(go, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
        go.name = "CarrotShot";
        Object.DestroyImmediate(go.GetComponent<ThrownProjectile>());
        var spinner = go.transform.Find("Spinner");
        // 가장 긴 축을 진행 방향(+Z)으로 (뾰족한 끝이 뒤를 보면 Spinner를 Y 180도 돌리면 됨)
        var b = new Bounds(go.transform.position, Vector3.zero);
        foreach (var r in go.GetComponentsInChildren<Renderer>()) b.Encapsulate(r.bounds);
        Vector3 sz = b.size;
        Quaternion rot = sz.y >= sz.x && sz.y >= sz.z ? Quaternion.Euler(90f, 0f, 0f) : sz.x >= sz.z ? Quaternion.Euler(0f, 90f, 0f) : Quaternion.identity;
        foreach (Transform ch in spinner) { ch.localRotation = rot * ch.localRotation; ch.localPosition = rot * ch.localPosition; }
        spinner.localScale = Vector3.one * 0.75f;
        var cs = go.AddComponent<CarrotShot>();
        Set(cs, "spinner", spinner);
        var p = PrefabUtility.SaveAsPrefabAsset(go, path);
        Object.DestroyImmediate(go);
        return p.GetComponent<CarrotShot>();
    }

    // ===================== UI 프리팹 =====================

    static GameObject BuildGrowthTreeUI(GrowthSprites sp)
    {
        GrowthFonts();
        var canvas = NewCanvas("HUD_GrowthTree");
        canvas.GetComponent<Canvas>().sortingOrder = 60;
        var t = canvas.transform;

        var panel = Stretch("Panel", t);
        var bgImg = Img(panel, Color.white); bgImg.sprite = sp.bg;
        Deco(panel, sp.glow, new Vector2(-560, 300), 1000, new Color(1f, 0.72f, 0.84f, 0.55f));
        Deco(panel, sp.glow, new Vector2(520, -280), 1100, new Color(0.8f, 0.74f, 1f, 0.5f));
        Deco(panel, sp.glow, new Vector2(-120, -40), 900, new Color(1f, 0.95f, 0.75f, 0.45f));

        // 보드 (빈 곳 클릭 = 선택 해제)
        var viewport = Stretch("Viewport", panel);
        var vpImg = Img(viewport, new Color(1, 1, 1, 0f));
        var bgBtn = viewport.gameObject.AddComponent<Button>(); bgBtn.transition = Selectable.Transition.None; bgBtn.targetGraphic = vpImg;
        var board = UI("Board", viewport, C, C, new Vector2(0, -20), new Vector2(820, 960));
        var sil = Stretch("Silhouette", board); var silImg = Img(sil, new Color(1f, 0.78f, 0.62f, 0.3f)); silImg.sprite = sp.carrot; silImg.raycastTarget = false;
        var linkRoot = Stretch("Links", board);
        var link = UI("LinkTemplate", linkRoot, C, C, Vector2.zero, new Vector2(100, 6), new Vector2(0f, 0.5f));
        var linkImg = Img(link, Color.white); linkImg.sprite = sp.dash; linkImg.type = Image.Type.Tiled; linkImg.raycastTarget = false;
        var nodeRoot = Stretch("Nodes", board);

        // 노드 원본
        var node = UI("NodeTemplate", nodeRoot, C, C, Vector2.zero, new Vector2(40, 40));
        var glow = UI("Glow", node, new Vector2(-0.8f, -0.8f), new Vector2(1.8f, 1.8f), Vector2.zero, Vector2.zero);
        var glowImg = Img(glow, new Color(1f, 0.6f, 0.65f, 0.5f)); glowImg.sprite = sp.glow; glowImg.raycastTarget = false;
        var ring = UI("Ring", node, new Vector2(-0.25f, -0.25f), new Vector2(1.25f, 1.25f), Vector2.zero, Vector2.zero);
        var ringImg = Img(ring, new Color(1f, 0.97f, 0.98f)); ringImg.sprite = sp.ring; ringImg.raycastTarget = false;
        var dotRt = Stretch("Dot", node);
        var dot = Img(dotRt, Color.white); dot.sprite = sp.dot;
        var sh = dotRt.gameObject.AddComponent<Shadow>(); sh.effectColor = new Color(0.55f, 0.38f, 0.5f, 0.35f); sh.effectDistance = new Vector2(0, -3);
        var shine = UI("Shine", node, new Vector2(0.22f, 0.52f), new Vector2(0.46f, 0.76f), Vector2.zero, Vector2.zero);
        var shineImg = Img(shine, new Color(1, 1, 1, 0.55f)); shineImg.sprite = sp.dot; shineImg.raycastTarget = false;
        var nodeBtn = node.gameObject.AddComponent<Button>(); nodeBtn.targetGraphic = dot;
        var cb = nodeBtn.colors; cb.highlightedColor = new Color(1f, 0.93f, 0.95f); cb.pressedColor = new Color(0.85f, 0.8f, 0.85f); nodeBtn.colors = cb;
        var view = node.gameObject.AddComponent<GrowthNodeView>();
        view.button = nodeBtn; view.dot = dot; view.ring = ring.gameObject; view.glow = glowImg;
        ring.gameObject.SetActive(false);

        // 제목 줄: [<] 토끼의 성장 [>] (글자 길이에 맞춰 화살표가 붙음)
        var titleRow = UI("TitleRow", panel, TL, TL, new Vector2(48, -44), new Vector2(800, 70), TL);
        var hlg = titleRow.gameObject.AddComponent<HorizontalLayoutGroup>();
        hlg.spacing = 14; hlg.childAlignment = TextAnchor.MiddleLeft;
        hlg.childControlWidth = true; hlg.childControlHeight = true; hlg.childForceExpandWidth = false; hlg.childForceExpandHeight = false;
        titleRow.gameObject.AddComponent<ContentSizeFitter>().horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
        Button Arrow(string n, string s)
        {
            var rt = UI(n, titleRow, TL, TL, Vector2.zero, new Vector2(56, 56));
            var le = rt.gameObject.AddComponent<LayoutElement>(); le.preferredWidth = 56; le.preferredHeight = 56;
            var img = Img(rt, Cream); img.sprite = sp.dot;
            var b = rt.gameObject.AddComponent<Button>(); b.targetGraphic = img;
            Txt("Label", rt, s, 34, TextAlignmentOptions.Center, Brown, Vector2.zero, Vector2.one, new Vector2(0, 2), Vector2.zero);
            return b;
        }
        var prevBtn = Arrow("PrevButton", "<");
        var title = Txt("Title", titleRow, "토끼의 성장", 52, TextAlignmentOptions.Left, Brown, TL, TL, Vector2.zero, new Vector2(400, 70), TL);
        var nextBtn = Arrow("NextButton", ">");

        // 오른쪽 위: 성장 재화 버튼 + 드롭다운
        var cur = UI("CurrencyButton", panel, Vector2.one, Vector2.one, new Vector2(-48, -40), new Vector2(240, 66), Vector2.one);
        var curImg = Img(cur, Cream); curImg.sprite = sp.round; curImg.type = Image.Type.Sliced;
        SoftShadow(cur);
        var curBtn = cur.gameObject.AddComponent<Button>(); curBtn.targetGraphic = curImg;
        var icon = UI("Icon", cur, new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(38, 0), new Vector2(30, 30));
        Img(icon, SeedColor).sprite = sp.dot;
        Txt("Label", cur, "성장 재화", 26, TextAlignmentOptions.Center, Brown, Vector2.zero, Vector2.one, new Vector2(20, 0), new Vector2(-40, 0));
        var drop = UI("Dropdown", cur, new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, -10), new Vector2(240, 70), new Vector2(0.5f, 1));
        var dropImg = Img(drop, Color.white); dropImg.sprite = sp.round; dropImg.type = Image.Type.Sliced;
        SoftShadow(drop);
        var dIcon = UI("Icon", drop, new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(38, 0), new Vector2(26, 26));
        Img(dIcon, SeedColor).sprite = sp.dot;
        var curText = Txt("Amount", drop, "성장의 씨앗  {0}", 24, TextAlignmentOptions.Center, Brown, Vector2.zero, Vector2.one, new Vector2(20, 0), new Vector2(-40, 0));
        drop.gameObject.SetActive(false);

        var close = UI("CloseButton", panel, Vector2.one, Vector2.one, new Vector2(-304, -40), new Vector2(120, 66), Vector2.one);
        var closeImg = Img(close, Cream); closeImg.sprite = sp.round; closeImg.type = Image.Type.Sliced;
        SoftShadow(close);
        var closeBtn = close.gameObject.AddComponent<Button>(); closeBtn.targetGraphic = closeImg;
        Txt("Label", close, "닫기", 26, TextAlignmentOptions.Center, SoftBrown, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

        // 오른쪽 설명 패널 (흰색 둥근 사각)
        var det = UI("DetailPanel", panel, new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(-64, -20), new Vector2(560, 700), new Vector2(1, 0.5f));
        var detImg = Img(det, Color.white); detImg.sprite = sp.round; detImg.type = Image.Type.Sliced;
        SoftShadow(det);
        var kind = Txt("Kind", det, "메인", 26, TextAlignmentOptions.Left, PinkBtn, TL, TL, new Vector2(44, -40), new Vector2(470, 36), TL);
        var name = Txt("Name", det, "관통 당근", 46, TextAlignmentOptions.Left, Brown, TL, TL, new Vector2(44, -80), new Vector2(470, 64), TL);
        var line = UI("Divider", det, TL, TL, new Vector2(44, -158), new Vector2(472, 4), TL);
        Img(line, new Color(1f, 0.86f, 0.88f));
        var desc = Txt("Desc", det, "설명", 27, TextAlignmentOptions.TopLeft, SoftBrown, TL, TL, new Vector2(44, -184), new Vector2(472, 330), TL, false);
        desc.lineSpacing = 12f;
        var cost = Txt("Cost", det, "필요 {0}   보유 {1}", 26, TextAlignmentOptions.Center, SoftBrown, BC, BC, new Vector2(0, 150), new Vector2(470, 40));
        var condition = Txt("Condition", det, "조건: {0}(으)로 {1}회 클리어  ({2}/{1})", 24, TextAlignmentOptions.Center, PinkBtn, BC, BC, new Vector2(0, 196), new Vector2(470, 36));
        condition.gameObject.SetActive(false);
        var up = UI("UpgradeButton", det, BC, BC, new Vector2(0, 54), new Vector2(460, 80));
        var upImg = Img(up, PinkBtn); upImg.sprite = sp.round; upImg.type = Image.Type.Sliced;
        var upBtn = up.gameObject.AddComponent<Button>(); upBtn.targetGraphic = upImg;
        var ucb = upBtn.colors; ucb.highlightedColor = new Color(1.05f, 1f, 1f); ucb.disabledColor = new Color(0.8f, 0.78f, 0.82f, 0.85f); upBtn.colors = ucb;
        var upLabel = Txt("Label", up, "강화하기", 32, TextAlignmentOptions.Center, Color.white, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        det.gameObject.SetActive(false);

        // 툴팁 (필요 재화)
        var tip = UI("Tooltip", panel, C, C, Vector2.zero, new Vector2(170, 50), new Vector2(0.5f, 0f));
        var tipImg = Img(tip, new Color(1, 1, 1, 0.97f)); tipImg.sprite = sp.round; tipImg.type = Image.Type.Sliced; tipImg.raycastTarget = false;
        SoftShadow(tip);
        var tipIcon = UI("Icon", tip, new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(30, 0), new Vector2(22, 22));
        var tipIconImg = Img(tipIcon, SeedColor); tipIconImg.sprite = sp.dot; tipIconImg.raycastTarget = false;
        var tipText = Txt("Text", tip, "필요 {0}", 24, TextAlignmentOptions.Center, Brown, Vector2.zero, Vector2.one, new Vector2(14, 0), new Vector2(-28, 0));
        tip.gameObject.SetActive(false);

        link.gameObject.SetActive(false); node.gameObject.SetActive(false);

        var ui = canvas.AddComponent<GrowthTreeUI>();
        var so = new SerializedObject(ui);
        void P(string f, Object v) => so.FindProperty(f).objectReferenceValue = v;
        P("panel", panel.gameObject); P("title", title); P("board", board); P("silhouette", silImg);
        P("linkTemplate", linkImg); P("nodeTemplate", view); P("backgroundButton", bgBtn); P("closeButton", closeBtn);
        P("tooltip", tip); P("tooltipText", tipText);
        P("detailPanel", det); P("detailKind", kind); P("detailName", name); P("detailDesc", desc); P("detailCost", cost);
        P("upgradeButton", upBtn); P("upgradeLabel", upLabel);
        P("prevButton", prevBtn); P("nextButton", nextBtn); P("boardGroup", board.gameObject.AddComponent<CanvasGroup>()); P("detailCondition", condition);
        P("currencyButton", curBtn); P("currencyDropdown", drop.gameObject); P("currencyText", curText);
        so.ApplyModifiedPropertiesWithoutUndo();
        panel.gameObject.SetActive(false); // 편집 시 Hierarchy에서 켜서 보기 (런타임에 자동으로 숨김)
        return Save(canvas, "UI/HUD_GrowthTree");
    }

    /// <summary>UI 폰트 (Prefabs/UI/Fonts의 페이퍼로지 SDF).</summary>
    static void GrowthFonts()
    {
        fontRegular = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(Root + "/Prefabs/UI/Fonts/Paperlogy-4Regular SDF.asset");
        fontBold = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(Root + "/Prefabs/UI/Fonts/Paperlogy-7Bold SDF.asset");
    }

    static void Deco(Transform parent, Sprite s, Vector2 pos, float size, Color c)
    {
        var rt = UI("Glow", parent, C, C, pos, new Vector2(size, size));
        var i = Img(rt, c); i.sprite = s; i.raycastTarget = false;
    }

    static void SoftShadow(RectTransform rt)
    {
        var s = rt.gameObject.AddComponent<Shadow>();
        s.effectColor = new Color(0.6f, 0.42f, 0.55f, 0.22f);
        s.effectDistance = new Vector2(0, -5);
    }

    /// <summary>HUD_SkillSelect의 줄 원본에 작은 '추천' 표시 추가 (이미 있으면 그대로).</summary>
    static void PatchSkillSelectRecommend(GrowthSprites sp)
    {
        string path = Root + "/Prefabs/UI/HUD_SkillSelect.prefab";
        if (!AssetDatabase.LoadAssetAtPath<GameObject>(path)) return;
        var root = PrefabUtility.LoadPrefabContents(path);
        try
        {
            var row = root.GetComponentInChildren<SkillSelectRow>(true);
            if (row && !row.recommend)
            {
                GrowthFonts();
                var rt = UI("Recommend", row.transform, TL, TL, new Vector2(10, 6), new Vector2(64, 28), new Vector2(0, 0.5f));
                var img = Img(rt, PinkBtn); img.sprite = sp.round; img.type = Image.Type.Sliced; img.raycastTarget = false;
                Txt("Label", rt, "추천", 18, TextAlignmentOptions.Center, Color.white, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
                rt.gameObject.SetActive(false);
                row.recommend = rt.gameObject;
                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }
}
