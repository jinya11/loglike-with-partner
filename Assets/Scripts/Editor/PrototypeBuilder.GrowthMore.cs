using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 마법사·새 소녀 성장 노드. 메뉴: Roguelike/Setup Creature Growth (토끼·마법사·새) — 토끼 설정(UI 프리팹 포함)도 함께 다시 실행.
/// 노드 데이터는 비어 있을 때만 채움 (인스펙터 편집 보존). 마법사 = 마법진(바깥 고리 + 안쪽 고리 + 가운데 메인 4 + 중앙 패시브),
/// 새 소녀 = 새 실루엣(src_bird.png) 외곽선을 따라 30개 (효과는 자리만, 미구현).
/// </summary>
public static partial class PrototypeBuilder
{
    const string GrowthDataDir = Root + "/Creatures/Growth";

    [MenuItem("Roguelike/Setup Creature Growth (토끼·마법사·새)")]
    static void SetupAllGrowth()
    {
        SetupRabbitGrowth(false);
        if (!AssetDatabase.IsValidFolder(GrowthDataDir)) AssetDatabase.CreateFolder(Root + "/Creatures", "Growth");
        var magic = Tex("G_MagicCircle", 512, 512, MagicCircle, Vector4.zero);
        var contour = BirdContour(out var birdSprite, out var birdAspect);

        var wiz = GrowthAsset<WizardGrowthConfig>("WizardGrowth");
        if (wiz.nodes == null || wiz.nodes.Length == 0) wiz.nodes = WizardNodes();
        wiz.creatureName = "마녀"; wiz.title = "마법사의 성장"; wiz.displayName = "마법사";
        wiz.boardSize = new Vector2(900, 900); wiz.silhouette = magic;
        wiz.recommendSkills = true; wiz.recommendedCategory = WatcherSkillCategory.유도;
        wiz.mainColor = new Color(0.78f, 0.62f, 1f); // 마법사 메인 = 라벤더
        EditorUtility.SetDirty(wiz);

        var bird = GrowthAsset<BirdGrowthConfig>("BirdGrowth");
        bool placeholder = bird.nodes != null && System.Array.Exists(bird.nodes, n => n != null && n.desc == "(효과 준비 중)"); // 예전 자리만 있던 노드는 새로 채움
        if ((bird.nodes == null || bird.nodes.Length == 0 || placeholder) && contour != null) bird.nodes = BirdNodes(contour);
        bird.creatureName = "새 소녀"; bird.title = "새 소녀의 성장"; bird.displayName = "새 소녀";
        bird.boardSize = new Vector2(780f, 780f * birdAspect); bird.silhouette = birdSprite;
        bird.recommendSkills = true; bird.recommendedCategory = WatcherSkillCategory.폭격;
        bird.mainColor = new Color(0.62f, 0.86f, 1f); // 새 소녀 메인 = 하늘색
        bird.bird.featherPrefab = BuildFeatherPrefab();
        bird.bird.stackHudPrefab = BuildBirdStackHud();
        EditorUtility.SetDirty(bird);

        string atkPath = GrowthConfigDir + "/Witch_MagicBolt.asset";
        var atk = AssetDatabase.LoadAssetAtPath<MagicBoltAttack>(atkPath);
        if (!atk)
        {
            atk = ScriptableObject.CreateInstance<MagicBoltAttack>();
            atk.displayName = "마법 탄"; atk.description = "가장 가까운 적에게 곧게 날아가는 약한 마법 탄 (성장 노드 효과가 덧붙음)";
            AssetDatabase.CreateAsset(atk, atkPath);
        }
        string fPath = GrowthConfigDir + "/Bird_Feather.asset";
        var featherAtk = AssetDatabase.LoadAssetAtPath<FeatherAttack>(fPath);
        if (!featherAtk)
        {
            featherAtk = ScriptableObject.CreateInstance<FeatherAttack>();
            featherAtk.displayName = "깃털 던지기"; featherAtk.description = "적 방향으로 옆으로 휘며 날아가는 깃털";
            AssetDatabase.CreateAsset(featherAtk, fPath);
        }

        var cfg = AssetDatabase.LoadAssetAtPath<GameConfig>(Root + "/Resources/GameConfig.asset");
        foreach (var c in cfg.ward.creatures)
        {
            if (c == null) continue;
            if (c.name == "마녀") { c.growth = wiz; if (!c.attack || c.attack is MagicBoltAttack) c.attack = atk; }
            else if (c.name == "새 소녀") { c.growth = bird; if (!c.attack || c.attack is FeatherAttack) c.attack = featherAtk; }
        }
        EditorUtility.SetDirty(cfg.ward);
        AssetDatabase.SaveAssets();
        Debug.Log("[PrototypeBuilder] 생명체 성장 노드 설정 완료 (토끼·마법사·새 소녀)");
    }

    static T GrowthAsset<T>(string name) where T : CreatureGrowthConfig
    {
        string path = $"{GrowthConfigDir}/{name}.asset";
        var g = AssetDatabase.LoadAssetAtPath<T>(path);
        if (g) return g;
        g = ScriptableObject.CreateInstance<T>();
        AssetDatabase.CreateAsset(g, path);
        return g;
    }

    static Vector2 Polar(float r, float deg) => new Vector2(0.5f + r * Mathf.Cos(deg * Mathf.Deg2Rad), 0.5f + r * Mathf.Sin(deg * Mathf.Deg2Rad));

    // ===================== 마법사 (마법진) =====================

    static GrowthNodeDef[] WizardNodes()
    {
        var list = new List<GrowthNodeDef>(30);
        (string n, string d, StatType s, float v)[] stats =
        {
            ("마력", "공격력 +0.25", StatType.Damage, 0.25f), ("빠른 영창", "공격 속도 +2%", StatType.AttackSpeed, 0.02f),
            ("두꺼운 망토", "체력 +4", StatType.WardMaxHp, 4f), ("별빛 시야", "사거리 +0.4", StatType.AttackRange, 0.4f),
            ("가벼운 신발", "이동 속도 +0.08", StatType.WardSpeed, 0.08f), ("휴식의 차", "초당 회복 +0.05", StatType.WardRegen, 0.05f),
            ("넓은 마법진", "공격 범위 +3%", StatType.Area, 0.03f),
        };
        int si = 0;
        GrowthNodeDef Stat(string id, Vector2 p, int cost, params string[] req)
        {
            var s = stats[si++ % stats.Length];
            var nd = new GrowthNodeDef { id = id, name = s.n, desc = s.d, kind = GrowthNodeKind.Stat, stat = s.s, value = s.v, position = p, cost = cost, requires = req };
            list.Add(nd);
            return nd;
        }

        // 바깥 고리 16 (아래에서 시작, 양옆으로 퍼짐). 위쪽(8번)은 감시자 노드
        const int O = 16;
        string Out(int i) => "o" + ((i % O + O) % O);
        for (int i = 0; i < O; i++)
        {
            var p = Polar(0.44f, -90f + i * 22.5f);
            string[] req = i == 0 ? new string[0] : new[] { Out(i - 1), Out(i + 1) };
            if (i == 8)
                list.Add(new GrowthNodeDef
                {
                    id = Out(i), name = "잔상 마탄", kind = GrowthNodeKind.Watcher, effect = GrowthEffect.ArcaneOrbs, position = p, cost = 6, requires = req,
                    desc = "감시자의 유도 스킬(디코이·조종)을 쓰면\n끌려온 적들에게 거대 마법 투사체를 발사합니다.",
                });
            else Stat(Out(i), p, 1 + Mathf.Min(i, O - i) / 3, req);
        }
        // 안쪽 고리 8: 바깥 두 노드 사이
        for (int j = 0; j < 8; j++) Stat("i" + j, Polar(0.27f, -78.75f + j * 45f), 3, Out(2 * j), Out(2 * j + 1));
        // 메인 4: 가운데 (안쪽 두 노드 사이)
        (string id, GrowthEffect e, string n, string d)[] mains =
        {
            ("main_chain", GrowthEffect.ArcaneChain, "연쇄", "평타가 적에게 맞으면 번개가\n주변 적에게 연쇄됩니다. (최대 4명)"),
            ("main_burn", GrowthEffect.ArcaneBurn, "연소", "평타가 적을 불태워\n1.2초 동안 총 5의 피해를 줍니다."),
            ("main_freeze", GrowthEffect.ArcaneFreeze, "빙결", "평타가 명중하면 확률적으로\n적을 1.5초 동안 얼립니다."),
            ("main_curse", GrowthEffect.ArcaneCurse, "저주", "평타가 명중하면 확률적으로 저주를 걸어\n처치될 때까지 받는 피해가 10% 늘어납니다."),
        };
        for (int m = 0; m < 4; m++)
            list.Add(new GrowthNodeDef
            {
                id = mains[m].id, name = mains[m].n, desc = mains[m].d, kind = GrowthNodeKind.Main, effect = mains[m].e,
                position = Polar(0.13f, -56.25f + m * 90f), cost = 5, requires = new[] { "i" + (2 * m), "i" + (2 * m + 1) },
            });
        // 중앙 패시브: 모든 메인 효과 (클리어 조건)
        list.Add(new GrowthNodeDef
        {
            id = "p_arcane", name = "마도의 극의", kind = GrowthNodeKind.Passive, effect = GrowthEffect.ArcaneAll, position = new Vector2(0.5f, 0.5f),
            cost = 10, requiredClears = 1, requires = new[] { "main_chain", "main_burn", "main_freeze", "main_curse" },
            desc = "연쇄·연소·빙결·저주가 모두 적용되고,\n연쇄가 터질 때 맞은 적 모두에게\n모든 효과가 함께 터집니다.",
        });
        return list.ToArray();
    }

    static Color MagicCircle(int x, int y)
    {
        const float S = 512f;
        Vector2 p = new Vector2((x + 0.5f) / S, (y + 0.5f) / S);
        float r = Vector2.Distance(p, new Vector2(0.5f, 0.5f)), px = 1f / S, d = 1f;
        foreach (var (rr, w) in new[] { (0.47f, 3f), (0.44f, 1.5f), (0.4f, 1.5f), (0.27f, 2f), (0.13f, 1.5f) }) d = Mathf.Min(d, Mathf.Abs(r - rr) - w * px);
        for (int t = 0; t < 2; t++) // 육망성 (삼각형 두 개)
            for (int k = 0; k < 3; k++)
            {
                Vector2 a = Polar(0.4f, 90f + t * 180f + k * 120f), b = Polar(0.4f, 90f + t * 180f + (k + 1) * 120f), ab = b - a;
                float h = Mathf.Clamp01(Vector2.Dot(p - a, ab) / ab.sqrMagnitude);
                d = Mathf.Min(d, Vector2.Distance(p, a + ab * h) - 1.5f * px);
            }
        return new Color(1, 1, 1, Mathf.Clamp01(0.5f - d / px));
    }

    // ===================== 새 소녀 (새 실루엣) =====================

    /// <summary>src_bird.png → 부드러운 실루엣 스프라이트 + 외곽선(정규화 좌표, 순서대로).</summary>
    static List<Vector2> BirdContour(out Sprite sprite, out float aspect)
    {
        sprite = null; aspect = 1f;
        string src = GrowthSpriteDir + "/src_bird.png";
        if (!File.Exists(src)) { Debug.LogWarning("새 실루엣 원본 없음: " + src); return null; }
        var t = new Texture2D(2, 2);
        t.LoadImage(File.ReadAllBytes(src));
        int w = t.width, h = t.height;
        var m = new bool[w, h];
        var px = t.GetPixels();
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++) { var c = px[y * w + x]; m[x, y] = c.a > 0.5f && (c.r + c.g + c.b) < 1.5f; }
        Object.DestroyImmediate(t);
        aspect = h / (float)w;

        const int Up = 4; // 부드러운 실루엣 (원본 4배, 이웃 평균)
        sprite = Tex("G_BirdSilhouette", w * Up, h * Up, (x, y) =>
        {
            float fx = (x + 0.5f) / Up - 0.5f, fy = (y + 0.5f) / Up - 0.5f;
            int x0 = Mathf.FloorToInt(fx), y0 = Mathf.FloorToInt(fy);
            float tx = fx - x0, ty = fy - y0;
            float M(int a, int b) => a >= 0 && b >= 0 && a < w && b < h && m[a, b] ? 1f : 0f;
            float v = Mathf.Lerp(Mathf.Lerp(M(x0, y0), M(x0 + 1, y0), tx), Mathf.Lerp(M(x0, y0 + 1), M(x0 + 1, y0 + 1), tx), ty);
            return new Color(1, 1, 1, Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.3f, 0.7f, v)));
        }, Vector4.zero);

        // Moore 이웃 외곽선 추적 (시계 방향 이웃 순서)
        var N = new[] { new Vector2Int(-1, 0), new Vector2Int(-1, 1), new Vector2Int(0, 1), new Vector2Int(1, 1), new Vector2Int(1, 0), new Vector2Int(1, -1), new Vector2Int(0, -1), new Vector2Int(-1, -1) };
        bool In(Vector2Int q) => q.x >= 0 && q.y >= 0 && q.x < w && q.y < h && m[q.x, q.y];
        Vector2Int s = new Vector2Int(-1, -1);
        for (int y = 0; y < h && s.x < 0; y++) for (int x = 0; x < w; x++) if (m[x, y]) { s = new Vector2Int(x, y); break; }
        if (s.x < 0) return null;
        var path = new List<Vector2Int> { s };
        Vector2Int p = s; int bi = 0;
        for (int guard = 0; guard < 20000; guard++)
        {
            int found = -1;
            for (int k = 1; k <= 8; k++) { int i = (bi + k) % 8; if (In(p + N[i])) { found = i; break; } }
            if (found < 0) break;
            Vector2Int back = p + N[(found + 7) % 8], np = p + N[found];
            bi = System.Array.IndexOf(N, back - np);
            p = np;
            if (p == s) break;
            path.Add(p);
        }
        var res = new List<Vector2>(path.Count);
        foreach (var q in path) res.Add(new Vector2((q.x + 0.5f) / w, (q.y + 0.5f) / h));
        return res;
    }

    static GrowthNodeDef[] BirdNodes(List<Vector2> contour, int count = 30)
    {
        // 둘레를 같은 간격으로 count개
        var cum = new float[contour.Count + 1];
        for (int i = 0; i < contour.Count; i++) cum[i + 1] = cum[i] + Vector2.Distance(contour[i], contour[(i + 1) % contour.Count]);
        var pts = new Vector2[count];
        for (int k = 0, j = 0; k < count; k++)
        {
            float target = cum[contour.Count] * k / count;
            while (j < contour.Count - 1 && cum[j + 1] < target) j++;
            float seg = cum[j + 1] - cum[j];
            pts[k] = Vector2.Lerp(contour[j], contour[(j + 1) % contour.Count], seg > 0f ? (target - cum[j]) / seg : 0f);
        }
        // 특별 노드: 가장 위(메인), 가장 왼쪽/오른쪽(메인/패시브), 가장 아래 = 시작, 시작 반대편 = 감시자
        int Arg(System.Func<Vector2, float> f) { int b = 0; for (int i = 1; i < count; i++) if (f(pts[i]) > f(pts[b])) b = i; return b; }
        int start = Arg(v => -v.y), top = Arg(v => v.y), left = Arg(v => -v.x), right = Arg(v => v.x);
        var kind = new GrowthNodeKind?[count];
        void Mark(int i, GrowthNodeKind k) { while (kind[i % count] != null || i % count == start) i++; kind[i % count] = k; }
        Mark(top, GrowthNodeKind.Main); Mark(left, GrowthNodeKind.Main); Mark(right, GrowthNodeKind.Passive);
        Mark(start + count / 2, GrowthNodeKind.Watcher); Mark(start + count / 4, GrowthNodeKind.Passive);
        Mark(start + count * 3 / 4, GrowthNodeKind.Main); Mark(start + count / 8, GrowthNodeKind.Passive);

        (string n, string d, StatType s, float v)[] stats =
        {
            ("가벼운 깃털", "이동 속도 +0.08", StatType.WardSpeed, 0.08f), ("날카로운 부리", "공격력 +0.3", StatType.Damage, 0.3f),
            ("포근한 깃털", "체력 +4", StatType.WardMaxHp, 4f), ("빠른 날갯짓", "공격 속도 +2%", StatType.AttackSpeed, 0.02f),
            ("먼 시야", "사거리 +0.4", StatType.AttackRange, 0.4f), ("따뜻한 둥지", "초당 회복 +0.05", StatType.WardRegen, 0.05f),
        };
        (string n, string d, GrowthEffect e)[] birdMains =
        {
            ("날개짓", "평타에 맞은 적을 공중으로 띄웁니다.\n떠 있는 적은 움직이거나 공격하지 못합니다.", GrowthEffect.WingLift),
            ("돌진", "공격받을 때 확률적으로 피해를 무시하고\n바람과 함께 앞으로 돌진해 경로의 적에게 피해를 줍니다.", GrowthEffect.WingDash),
            ("거침없는 날개", "평타가 명중하면 바닥에 날개가 확정으로 떨어집니다.\n날개를 주우면 그 자리에서 동서남북으로 날개가 포물선을 그리며 퍼져 피해를 줍니다.", GrowthEffect.ScatterWings),
        };
        (string n, string d, GrowthEffect e)[] birdPassives =
        {
            ("쉼 없는 날갯짓", "공격받지 않으면 공격 속도가 계속 올라갑니다.\n공격받으면 쌓인 단계가 1만 줄어듭니다. (캐릭터 옆에 표시)", GrowthEffect.Momentum),
            ("떨어진 날개", "평타가 명중하면 확률적으로 그 자리에 날개가 남습니다.\n새 소녀나 감시자가 주우면 잠시 이동 속도와 공격 속도가 오릅니다.", GrowthEffect.FeatherDrop),
            ("바람의 가호", "이동 속도와 공격 속도가 빠를수록\n받는 피해가 줄어듭니다.", GrowthEffect.SpeedGuard),
        };
        var list = new GrowthNodeDef[count];
        int si = 0, mi = 0, pi = 0;
        for (int i = 0; i < count; i++)
        {
            int dist = Mathf.Min((i - start + count) % count, (start - i + count) % count);
            var nd = new GrowthNodeDef
            {
                id = "b" + i, position = pts[i],
                requires = i == start ? new string[0] : new[] { "b" + ((i + count - 1) % count), "b" + ((i + 1) % count) },
            };
            switch (kind[i])
            {
                case GrowthNodeKind.Main:
                    var mm = birdMains[mi++ % birdMains.Length];
                    nd.kind = GrowthNodeKind.Main; nd.name = mm.n; nd.desc = mm.d; nd.effect = mm.e; nd.cost = 5; break;
                case GrowthNodeKind.Passive:
                    var pp = birdPassives[pi++ % birdPassives.Length];
                    nd.kind = GrowthNodeKind.Passive; nd.name = pp.n; nd.desc = pp.d; nd.effect = pp.e; nd.cost = 4; break;
                case GrowthNodeKind.Watcher:
                    nd.kind = GrowthNodeKind.Watcher; nd.name = "폭풍 날개"; nd.effect = GrowthEffect.BirdGust; nd.cost = 6;
                    nd.desc = "감시자의 폭격 스킬이 발동할 때 새 소녀가 폭격 범위 안에 있으면,\n바라보는 가로 방향으로 돌풍이 뻗어 나가 적을 멀리 날려 보냅니다. (피해 없음)";
                    break;
                default:
                    var s = stats[si++ % stats.Length];
                    nd.kind = GrowthNodeKind.Stat; nd.name = s.n; nd.desc = s.d; nd.stat = s.s; nd.value = s.v; nd.cost = 1 + dist / 4;
                    break;
            }
            list[i] = nd;
        }
        return list;
    }

    /// <summary>깃털 투사체 프리팹 (납작한 캡슐, 무광 크림 분홍). 이미 있으면 그대로 (직접 다듬었을 수 있음).</summary>
    static FeatherShot BuildFeatherPrefab()
    {
        string path = GrowthDataDir + "/Feather.prefab";
        var existing = AssetDatabase.LoadAssetAtPath<FeatherShot>(path);
        if (existing) return existing;
        string matPath = GrowthDataDir + "/M_Feather.mat";
        var mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
        if (!mat)
        {
            mat = new Material(Shader.Find("Universal Render Pipeline/Lit")) { enableInstancing = true };
            mat.SetColor("_BaseColor", new Color(1f, 0.93f, 0.9f)); mat.SetFloat("_Smoothness", 0.15f);
            AssetDatabase.CreateAsset(mat, matPath);
        }
        var root = new GameObject("Feather");
        var model = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        model.name = "Model";
        Object.DestroyImmediate(model.GetComponent<Collider>());
        model.transform.SetParent(root.transform, false);
        model.transform.localRotation = Quaternion.Euler(90f, 0f, 0f); // 긴 축 = 진행 방향(+Z)
        model.transform.localScale = new Vector3(0.18f, 0.3f, 0.05f);
        var r = model.GetComponent<Renderer>(); r.sharedMaterial = mat; r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        var fs = root.AddComponent<FeatherShot>();
        Set(fs, "model", model.transform);
        var p = PrefabUtility.SaveAsPrefabAsset(root, path);
        Object.DestroyImmediate(root);
        return p.GetComponent<FeatherShot>();
    }

    /// <summary>캐릭터 옆 공속 스택 표시 (월드 공간 Canvas 프리팹 HUD_BirdStacks).</summary>
    static BirdStackHud BuildBirdStackHud()
    {
        GrowthFonts();
        var sp = BuildGrowthSprites();
        var go = new GameObject("HUD_BirdStacks", typeof(RectTransform));
        var canvas = go.AddComponent<Canvas>(); canvas.renderMode = RenderMode.WorldSpace; canvas.sortingOrder = 10;
        var rt = (RectTransform)go.transform; rt.sizeDelta = new Vector2(140, 60); rt.localScale = Vector3.one * 0.01f;
        go.AddComponent<Billboard>();
        var pill = Stretch("Pill", go.transform);
        var img = Img(pill, new Color(1f, 0.98f, 0.96f, 0.92f)); img.sprite = sp.round; img.type = UnityEngine.UI.Image.Type.Sliced; img.raycastTarget = false;
        var icon = UI("Icon", go.transform, new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(30, 0), new Vector2(26, 26));
        var ii = Img(icon, new Color(0.62f, 0.86f, 1f)); ii.sprite = sp.dot; ii.raycastTarget = false;
        var txt = Txt("Count", go.transform, "x{0}", 34, TMPro.TextAlignmentOptions.Center, Brown, Vector2.zero, Vector2.one, new Vector2(16, 0), new Vector2(-40, 0));
        var hud = go.AddComponent<BirdStackHud>();
        Set(hud, "text", txt);
        return Save(go, "UI/HUD_BirdStacks").GetComponent<BirdStackHud>();
    }
}
