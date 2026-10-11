using System;
using System.Collections.Generic;
using Humanoid;
using Unity.MLAgents;
using Unity.MLAgents.Policies;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using BodyPart = Humanoid.BodyPart;
using Object = UnityEngine.Object;

namespace HumanoidEditor
{
    /// <summary>장 하나 = 단계 여러 개.</summary>
    internal class Chapter
    {
        public string title, doc;
        public string[] goals;
        public List<Step> steps = new List<Step>();
    }

    /// <summary>
    /// 단계 하나 = 작은 변경 하나. 할 일(Task)을 위에서부터 차례로 해 나간다.
    /// blank가 있으면 코드 단계다: 빈칸 만들기 → 파일 열기 → 채점 이 할 일 대신 붙는다.
    /// solve는 막혔을 때 [대신 해주기]가 하는 일이다.
    /// </summary>
    internal class Step
    {
        public string title, goal, concept, hint, blank, skeleton, done;
        public Action solve;
        public List<Task> tasks = new List<Task>();

        /// <summary>할 일 추가. check가 없으면 [했어요] 버튼으로 넘어간다.</summary>
        public Step Do(string text, Func<bool> check = null, string button = null, Action action = null)
        {
            tasks.Add(new Task { text = text, check = check, button = button, action = action });
            return this;
        }

        // ── 방금 추가한 할 일이 "어디서" 하는 일인지 (📍 위치 칩 · 하이라이트) ──
        public Step Hier(string path, string how) => Guide("Hierarchy", path, null, "Hierarchy" + (path != null ? " ▸ " + Leaf(path) : "") + " ▸ " + how);
        public Step Insp(string path, Type component, string field) =>
            Guide("Inspector", path, component, "Inspector ▸ " + Leaf(path) + (component != null ? " ▸ " + ObjectNames.NicifyVariableName(component.Name) : "") + " ▸ " + field);
        public Step Game(string how) => Guide("Game", null, null, "Game 창 (한 번 클릭) ▸ " + how);
        public Step Toolbar(string how) => Guide("Toolbar", null, null, "화면 위 가운데 " + how + " 버튼");

        Step Guide(string window, string path, Type component, string where)
        {
            var t = tasks[tasks.Count - 1];
            t.window = window; t.path = path; t.component = component; t.where = where;
            return this;
        }

        static string Leaf(string path) => path.Substring(path.LastIndexOf('/') + 1);
    }

    internal class Task
    {
        public string text, button;
        public Func<bool> check;
        public Action action;
        public string window, path, where;      // 위치 안내 (TutorialGuide)
        public Type component;
    }

    /// <summary>
    /// 튜토리얼 내용. 학습자가 Hierarchy · Inspector 를 직접 만지며 장을 완성한다.
    /// 자세한 설명은 문서(Docs/NN)에 있고 창의 [📖 문서]로 연다.
    /// </summary>
    internal static class TutorialChapters
    {
        const string PracticeScene = "Assets/Humanoid/Practice/Practice.unity";

        // 계층 경로. 조건은 이 이름·위치를 정확히 본다.
        const string Area = HumanoidBuild.AreaName;
        const string Root = Area + "/Humanoid";
        const string Pelvis = Root + "/Pelvis";
        const string Torso = Pelvis + "/Torso";
        const string ThighL = Pelvis + "/ThighL";
        const string Target = Area + "/Target";

        // ── 조건에서 여러 번 쓰는 것 ────────────────────────────────

        static bool Playing => EditorApplication.isPlaying;
        static Transform T(string path) { var g = GameObject.Find(path); return g != null ? g.transform : null; }
        static C Get<C>(string path) where C : Component { var t = T(path); return t != null ? t.GetComponent<C>() : null; }
        static bool Has<C>(string path) where C : Component => Get<C>(path) != null;
        static bool Near(Vector3 a, Vector3 b) => (a - b).magnitude < 0.011f;
        static bool Near(float a, float b) => Mathf.Abs(a - b) < 0.011f;

        static bool ChildOf(string path, string parent) => T(path) != null && T(path).parent == T(parent);
        static bool LocalPos(string path, Vector3 v) => T(path) != null && Near(T(path).localPosition, v);
        static bool Box(string path, Vector3 size) => Has<BoxCollider>(path) && Near(Get<BoxCollider>(path).size, size);
        static bool Mass(string path, float m) => Has<Rigidbody>(path) && Near(Get<Rigidbody>(path).mass, m);
        static bool HasMesh(string path) => T(path) != null && T(path).GetComponentInChildren<MeshRenderer>() != null;

        static ConfigurableJoint J(string path) => Get<ConfigurableJoint>(path);
        static bool Connected(string path, string parent) => J(path) != null && J(path).connectedBody == Get<Rigidbody>(parent);
        static bool Anchor(string path, Vector3 v) => J(path) != null && Near(J(path).anchor, v);
        static bool MoveLocked(string path) => J(path) != null && J(path).xMotion == ConfigurableJointMotion.Locked
            && J(path).yMotion == ConfigurableJointMotion.Locked && J(path).zMotion == ConfigurableJointMotion.Locked;
        static bool AngularMotion(string path, bool z) => J(path) != null && J(path).angularXMotion == ConfigurableJointMotion.Limited
            && J(path).angularYMotion == ConfigurableJointMotion.Locked
            && J(path).angularZMotion == (z ? ConfigurableJointMotion.Limited : ConfigurableJointMotion.Locked);
        static bool Limits(string path, float low, float high, float z) => J(path) != null && Near(J(path).lowAngularXLimit.limit, low)
            && Near(J(path).highAngularXLimit.limit, high) && Near(J(path).angularZLimit.limit, z);

        static HumanoidBody Body => Get<HumanoidBody>(Root);
        static BehaviorParameters Brain => Get<BehaviorParameters>(Root);
        static bool IsBehavior(BehaviorType type) => Brain != null && Brain.BehaviorType == type;

        static GameObject G(string path) => T(path) != null ? T(path).gameObject : null;

        /// <summary>[대신 해주기]: 같은 이름이 있으면 지우고 정답으로 다시 만든다.</summary>
        static void Replace(string path, Func<GameObject> make)
        {
            if (G(path) != null) Undo.DestroyObjectImmediate(G(path));
            make();
            if (G(Root) != null) Ch1_Body.AddVisuals(G(Root));
        }

        static void Set<C>(string path, Action<C> change) where C : Component
        {
            var go = G(path);
            if (go == null) return;
            var c = HumanoidBuild.GetOrAdd<C>(go);
            Undo.RecordObject(c, "대신 해주기");
            change(c);
            EditorUtility.SetDirty(c);
        }

        static void OpenPractice()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            if (System.IO.File.Exists(PracticeScene)) { EditorSceneManager.OpenScene(PracticeScene); return; }
            System.IO.Directory.CreateDirectory("Assets/Humanoid/Practice");
            var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
            EditorSceneManager.SaveScene(scene, PracticeScene);
        }

        static void SetBehavior(BehaviorType type) => Set<BehaviorParameters>(Root, b => b.BehaviorType = type);
        static void Copy(string text) { EditorGUIUtility.systemCopyBuffer = text; Debug.Log("[Humanoid] 복사했습니다: " + text); }
        static Step PlayStep(Step s) => s.Do("▶ Play 를 누른다", () => Playing).Toolbar("▶");
        static Step StopStep(Step s, string text = "⏹ 멈춘다") => s.Do(text, () => !Playing).Toolbar("⏹");

        // ── 개발자용 점검: 내용이 말이 되는가 ─────────────────────────

        [MenuItem("Humanoid/개발자/튜토리얼 점검", false, 303)]
        static void Audit()
        {
            var problems = new List<string>();
            foreach (var ch in All)
                foreach (var st in ch.steps)
                {
                    string where = ch.title + " / " + st.title;
                    if (string.IsNullOrEmpty(st.goal)) problems.Add(where + ": 목표 없음");
                    if (st.blank != null)
                    {
                        if (string.IsNullOrEmpty(st.skeleton) || string.IsNullOrEmpty(st.hint)) problems.Add(where + ": 뼈대/힌트 없음");
                        if (BlankCheck.Grade(st.blank).Count < 3) problems.Add(where + ": 채점 케이스 3개 미만");
                        if (BlankTool.Line(st.blank) <= 1) problems.Add(where + ": HumanoidMath.cs 에 빈칸 표시가 없음");
                    }
                    else if (st.tasks.Count == 0) problems.Add(where + ": 할 일 없음");
                    foreach (var t in st.tasks)
                    {
                        if (t.button != null && t.action == null) problems.Add(where + ": 버튼에 동작 없음 — " + t.text);
                        if (t.check != null && t.button == null && t.where == null) problems.Add(where + ": 위치 안내(📍) 없음 — " + t.text);
                        if (t.path != null && t.window == "Inspector" && !t.path.StartsWith(Area) && t.path != "Main Camera") problems.Add(where + ": 경로가 학습 영역 밖 — " + t.path);
                        try { t.check?.Invoke(); }
                        catch (Exception e) { problems.Add(where + ": 조건에서 예외 " + e.GetType().Name + " — " + t.text); }
                    }
                }
            if (problems.Count == 0) Debug.Log("[Humanoid] 튜토리얼 점검 — 문제 0건 (" + All.Count + "장)");
            else Debug.LogWarning("[Humanoid] 튜토리얼 점검 — 문제 " + problems.Count + "건\n  " + string.Join("\n  ", problems));
        }

        /// <summary>
        /// 개발자용: 지금 열린 씬에서 [대신 해주기]를 처음부터 차례로 실행하며
        /// 각 단계의 조건(Play 가 필요 없는 것)이 전부 통과하는지 본다. 조건과 정답이 어긋나면 잡힌다.
        /// </summary>
        [MenuItem("Humanoid/개발자/대신 해주기로 끝까지 (조건 검증)", false, 305)]
        static void SolveAll()
        {
            var problems = new List<string>();
            foreach (var ch in All)
                foreach (var st in ch.steps)
                {
                    if (st.solve == null) continue;
                    st.solve();
                    foreach (var t in st.tasks)
                    {
                        if (t.check == null || t.text.Contains("Play") || t.text.Contains("⏹") || t.text.Contains("씬")) continue;
                        bool ok;
                        try { ok = t.check(); } catch { ok = false; }
                        if (!ok) problems.Add(ch.title + " / " + st.title + " — " + t.text);
                    }
                }
            if (problems.Count == 0) Debug.Log("[Humanoid] 대신 해주기 검증 — 모든 조건 통과");
            else Debug.LogWarning("[Humanoid] 대신 해주기 검증 — 통과하지 못한 조건 " + problems.Count + "개\n  " + string.Join("\n  ", problems));
        }

        // ── 내용 ───────────────────────────────────────────────────

        public static readonly List<Chapter> All = Build();

        static List<Chapter> Build()
        {
            var list = new List<Chapter>();
            Chapter ch;
            Step s;

            // ═══════════════════════════════════════════════════════════
            // 1장 — 학습 환경 구성
            // ═══════════════════════════════════════════════════════════
            ch = new Chapter { title = "1. 학습 환경 구성", doc = "01-학습-환경-구성.md",
                goals = new[] { "학습 영역과 바닥을 만든다", "휴머노이드의 계층(Hierarchy)을 부품과 관절로 직접 쌓는다", "몸이 물리로 움직인다는 것을 확인한다" } };
            list.Add(ch);

            s = new Step { title = "시작하기", goal = "이 창을 읽는 법을 알고, 실습 씬을 만든다",
                concept = "창은 위에서 아래로 읽는다.\n" +
                          "  🎯 목표       이 단계에서 무엇을 하는지\n" +
                          "  ▶ 파란 칸     지금 할 일. 언제나 이것 하나만 하면 된다\n" +
                          "  ✔            끝낸 일. 대부분 저절로 확인된다. 확인할 수 없는 일은 [했어요]\n" +
                          "  다음 단계     할 일을 다 하면 켜진다\n" +
                          "막히면 💡 힌트 → 그래도 안 되면 '막혔을 때' 를 펼쳐 [대신 해주기]. 진행은 자동 저장된다.\n\n" +
                          "실습은 Assets/Humanoid/Practice/Practice.unity 에서 한다. 처음부터 직접 만든다.",
                done = "빈 씬이 준비됐다. 학습할 무대부터 만든다." };
            ch.steps.Add(s.Do("실습 씬을 만든다", () => EditorSceneManager.GetActiveScene().path == PracticeScene, "실습 씬 만들기", OpenPractice));

            s = new Step { title = "학습 영역 만들기", goal = "모든 것을 담을 빈 오브젝트 HumanoidArea 를 만든다",
                concept = "바닥 · 몸 · 목표를 전부 이 오브젝트 아래에 둔다.\n" +
                          "5장에서 이것 하나를 복제하면 학습 환경이 통째로 9개가 된다 (병렬 학습).\n" +
                          "코드와 메뉴가 이 이름(HumanoidArea)으로 찾으므로 철자를 정확히 쓴다.",
                hint = "Hierarchy 빈 곳 우클릭 > Create Empty. 이름은 F2 로 바꾼다.",
                solve = () => Replace(Area, Ch1_Body.Area) };
            s.Do("Hierarchy 빈 곳을 우클릭 > Create Empty 하고 이름을 HumanoidArea 로 바꾼다", () => T(Area) != null && T(Area).parent == null).Hier(null, "빈 곳 우클릭 ▸ Create Empty ▸ 이름 HumanoidArea");
            ch.steps.Add(s.Do("Inspector 에서 Position 을 (0, 0, 0) 으로 만든다", () => T(Area) != null && Near(T(Area).position, Vector3.zero)).Insp(Area, typeof(Transform), "Position"));

            s = new Step { title = "바닥 만들기", goal = "윗면이 y = 0 인 40 × 40 바닥을 만든다",
                concept = "Cube 의 두께는 1 이다. 가운데를 y = -0.5 에 두면 윗면이 정확히 y = 0 이 된다.\n" +
                          "4장에서 목표가 4~8m 거리에 놓이므로 넉넉하게 40 × 40.\n\n" +
                          "바닥에는 Rigidbody 를 붙이지 않는다. 움직이지 않는 콜라이더 = 바닥 —\n" +
                          "몸의 부품(BodyPart)은 이 규칙으로 '바닥에 닿았다'를 알아본다.",
                hint = "HumanoidArea 를 우클릭 > 3D Object > Cube 로 만들면 처음부터 자식으로 들어간다.",
                solve = () => Replace(Area + "/Ground", () => Ch1_Body.Ground(G(Area))) };
            s.Do("HumanoidArea 를 우클릭 > 3D Object > Cube 하고 이름을 Ground 로 바꾼다", () => ChildOf(Area + "/Ground", Area)).Hier(Area, "우클릭 ▸ 3D Object ▸ Cube ▸ 이름 Ground");
            s.Do("Position (0, -0.5, 0)", () => LocalPos(Area + "/Ground", new Vector3(0f, -0.5f, 0f))).Insp(Area + "/Ground", typeof(Transform), "Position");
            ch.steps.Add(s.Do("Scale (40, 1, 40)", () => T(Area + "/Ground") != null && Near(T(Area + "/Ground").localScale, new Vector3(40f, 1f, 40f))).Insp(Area + "/Ground", typeof(Transform), "Scale"));

            s = new Step { title = "카메라 놓기", goal = "휴머노이드가 설 자리를 비스듬히 내려다보게 한다",
                concept = "몸은 원점에 키 1.7m 로 선다. 카메라를 앞 오른쪽 위 (2.4, 1.5, 3.2) 에 두고 몸 가운데(0, 0.9, 0)를 보게 한다.\n" +
                          "앞쪽(+Z)에서 보기 때문에 얼굴이 카메라를 향한다.",
                hint = "Rotation 은 (8.5, 217, 0). 직접 맞추기 어려우면 대신 해주기.",
                solve = Ch1_Body.PlaceCamera };
            s.Do("Main Camera 의 Position (2.4, 1.5, 3.2)", () => Camera.main != null && (Camera.main.transform.position - new Vector3(2.4f, 1.5f, 3.2f)).magnitude < 0.05f).Insp("Main Camera", typeof(Transform), "Position");
            ch.steps.Add(s.Do("Rotation (8.5, 217, 0)", () => Camera.main != null && Quaternion.Angle(Camera.main.transform.rotation, Quaternion.Euler(8.5f, 216.9f, 0f)) < 3f).Insp("Main Camera", typeof(Transform), "Rotation"));

            s = new Step { title = "Humanoid 뿌리", goal = "몸 전체를 담을 빈 오브젝트 Humanoid 를 만든다",
                concept = "부품들은 이 아래에 계층으로 이어진다. 몸 전체를 다루는 스크립트도 여기에 붙는다.\n\n" +
                          "★ 스케일은 반드시 (1, 1, 1). 여기에 스케일을 주면 아래에 달린 부품이 전부 같이 찌그러진다.",
                hint = "HumanoidArea 우클릭 > Create Empty.",
                solve = () => Replace(Root, () => Ch1_Body.Root(G(Area))) };
            s.Do("HumanoidArea 아래에 빈 오브젝트를 만들고 이름을 Humanoid 로", () => ChildOf(Root, Area)).Hier(Area, "우클릭 ▸ Create Empty ▸ 이름 Humanoid");
            ch.steps.Add(s.Do("Position (0, 0, 0) · Scale (1, 1, 1)", () => LocalPos(Root, Vector3.zero) && Near(T(Root).localScale, Vector3.one)).Insp(Root, typeof(Transform), "Position · Scale"));

            s = new Step { title = "골반 — 첫 번째 부품", goal = "물리로 움직이는 부품 하나를 만든다",
                concept = "부품 = Rigidbody(무게·중력) + Collider(모양·충돌) + Body Part(바닥 접촉 기록 · 처음 자세 기억).\n" +
                          "크기는 Scale 이 아니라 Box Collider 의 Size 로 정한다. 부품 자체는 스케일 1을 유지한다.\n" +
                          "보이는 상자는 Collider 크기에 맞춰 버튼이 붙여 준다.\n\n" +
                          "골반은 몸의 뿌리라 관절이 없다. 높이 0.96 = 다리 길이만큼 위.",
                hint = "Inspector 맨 아래 Add Component 에서 Box Collider / Rigidbody / Body Part 를 검색한다.",
                solve = () => Replace(Pelvis, () => Ch1_Body.Pelvis(G(Root))) };
            s.Do("Humanoid 아래에 빈 오브젝트 Pelvis 를 만든다", () => ChildOf(Pelvis, Root)).Hier(Root, "우클릭 ▸ Create Empty ▸ 이름 Pelvis");
            s.Do("Position (0, 0.96, 0)", () => LocalPos(Pelvis, new Vector3(0f, 0.96f, 0f))).Insp(Pelvis, typeof(Transform), "Position");
            s.Do("Add Component > Box Collider, Size (0.3, 0.16, 0.18)", () => Box(Pelvis, new Vector3(0.30f, 0.16f, 0.18f))).Insp(Pelvis, typeof(BoxCollider), "맨 아래 Add Component ▸ Box Collider ▸ Size");
            s.Do("Add Component > Rigidbody, Mass 8", () => Mass(Pelvis, 8f)).Insp(Pelvis, typeof(Rigidbody), "Add Component ▸ Rigidbody ▸ Mass");
            s.Do("Add Component > Body Part", () => Has<BodyPart>(Pelvis)).Insp(Pelvis, typeof(BodyPart), "Add Component ▸ Body Part");
            ch.steps.Add(s.Do("보이는 모양을 붙인다", () => HasMesh(Pelvis), "모양 붙이기", () => Ch1_Body.AddVisuals(G(Root))).Hier(Pelvis, "오른쪽 [모양 붙이기] 버튼"));

            s = new Step { title = "몸통", goal = "골반 위에 몸통 부품을 만든다 (팔·머리 무게 포함 14kg)",
                concept = "자식의 Position 은 부모 기준(로컬)이다. 몸통 가운데는 세상에서 y = 1.25,\n" +
                          "골반이 0.96 에 있으니 로컬로는 1.25 − 0.96 = 0.29.\n\n" +
                          "위가 무겁다. 사람도 그렇다. 그래서 서 있는 것 자체가 '거꾸로 세운 막대'를 버티는 일이다.",
                solve = () => Replace(Torso, () => Ch1_Body.Torso(G(Pelvis))) };
            s.Do("Pelvis 아래에 빈 오브젝트 Torso, Position (0, 0.29, 0)", () => ChildOf(Torso, Pelvis) && LocalPos(Torso, new Vector3(0f, 0.29f, 0f))).Hier(Pelvis, "우클릭 ▸ Create Empty ▸ 이름 Torso, Position");
            s.Do("Box Collider, Size (0.32, 0.4, 0.2)", () => Box(Torso, new Vector3(0.32f, 0.40f, 0.20f))).Insp(Torso, typeof(BoxCollider), "Add Component ▸ Box Collider ▸ Size");
            s.Do("Rigidbody, Mass 14", () => Mass(Torso, 14f)).Insp(Torso, typeof(Rigidbody), "Add Component ▸ Rigidbody ▸ Mass");
            s.Do("Body Part", () => Has<BodyPart>(Torso)).Insp(Torso, typeof(BodyPart), "Add Component ▸ Body Part");
            ch.steps.Add(s.Do("보이는 모양을 붙인다", () => HasMesh(Torso), "모양 붙이기", () => Ch1_Body.AddVisuals(G(Root))).Hier(Torso, "오른쪽 [모양 붙이기] 버튼"));

            s = new Step { title = "허리 관절", goal = "몸통을 골반에 관절(Configurable Joint)로 잇는다",
                concept = "관절은 '어디서, 어느 방향으로, 얼마나' 돌 수 있는지 정한다.\n" +
                          "  Connected Body  누구와 잇나 → Pelvis\n" +
                          "  Anchor          관절 점 (자기 기준) → 몸통 아래 끝 (0, -0.21, 0)\n" +
                          "  X/Y/Z Motion    이동. 전부 Locked — 빠지지 않고 돌기만\n" +
                          "  Angular X       앞뒤로 굽히기 (Limited) · Angular Y 비틀기 (Locked) · Angular Z 좌우 (Limited)\n" +
                          "  Limit           앞으로 40 · 뒤로 20 · 좌우 20\n\n" +
                          "Unity 관절 공간에서 X+ 는 '몸통을 뒤로'다. 그래서 Low -40(앞) · High 20(뒤).\n" +
                          "모든 부품이 회전 없이 서 있으니 관절 각도 0 = 차렷 자세.",
                hint = "Pelvis 를 Connected Body 칸으로 끌어다 놓는다. Limit 은 Low Angular X Limit > Limit 처럼 접힌 칸 안에 있다.",
                solve = () => { if (J(Torso) != null) Undo.DestroyObjectImmediate(J(Torso)); Ch1_Body.WaistJoint(G(Torso)); } };
            s.Do("Torso 에 Add Component > Configurable Joint", () => J(Torso) != null).Insp(Torso, typeof(ConfigurableJoint), "Add Component ▸ Configurable Joint");
            s.Do("Connected Body 에 Pelvis 를 끌어다 놓는다", () => Connected(Torso, Pelvis)).Insp(Torso, typeof(ConfigurableJoint), "Connected Body ← Hierarchy 의 Pelvis 를 끌어다 놓기");
            s.Do("Anchor (0, -0.21, 0)", () => Anchor(Torso, new Vector3(0f, -0.21f, 0f))).Insp(Torso, typeof(ConfigurableJoint), "Anchor");
            s.Do("X · Y · Z Motion 을 모두 Locked", () => MoveLocked(Torso)).Insp(Torso, typeof(ConfigurableJoint), "X Motion · Y Motion · Z Motion");
            s.Do("Angular X Motion = Limited · Angular Y = Locked · Angular Z = Limited", () => AngularMotion(Torso, true)).Insp(Torso, typeof(ConfigurableJoint), "Angular X / Y / Z Motion");
            ch.steps.Add(s.Do("Low Angular X Limit -40 · High Angular X Limit 20 · Angular Z Limit 20", () => Limits(Torso, -40f, 20f, 20f)).Insp(Torso, typeof(ConfigurableJoint), "Low · High Angular X Limit ▸ Limit, Angular Z Limit ▸ Limit"));

            s = new Step { title = "왼쪽 허벅지와 엉덩이 관절", goal = "다리 하나를 직접 만들어 본다",
                concept = "골반 기준으로 왼쪽(-x) 0.1, 아래로 0.27 (허벅지 가운데 0.69 − 골반 0.96).\n" +
                          "엉덩이 관절은 허벅지 위 끝 (0, 0.21, 0). 범위는 뒤로 30 · 앞으로 90 · 좌우 25.\n" +
                          "다리에서는 X+ 가 '다리를 앞으로'다. 그래서 Low -30 · High 90.",
                solve = () => { Replace(ThighL, () => Ch1_Body.Thigh(G(Pelvis), -0.10f)); Ch1_Body.HipJoint(G(ThighL)); } };
            s.Do("Pelvis 아래에 ThighL, Position (-0.1, -0.27, 0)", () => ChildOf(ThighL, Pelvis) && LocalPos(ThighL, new Vector3(-0.10f, -0.27f, 0f))).Hier(Pelvis, "우클릭 ▸ Create Empty ▸ 이름 ThighL, Position");
            s.Do("Box Collider (0.12, 0.42, 0.12) · Rigidbody Mass 5 · Body Part", () => Box(ThighL, new Vector3(0.12f, 0.42f, 0.12f)) && Mass(ThighL, 5f) && Has<BodyPart>(ThighL)).Insp(ThighL, typeof(BoxCollider), "Add Component ▸ Box Collider · Rigidbody · Body Part");
            s.Do("Configurable Joint · Connected Body = Pelvis · Anchor (0, 0.21, 0)", () => Connected(ThighL, Pelvis) && Anchor(ThighL, new Vector3(0f, 0.21f, 0f))).Insp(ThighL, typeof(ConfigurableJoint), "Add Component ▸ Configurable Joint ▸ Connected Body · Anchor");
            s.Do("Motion 전부 Locked · Angular X Limited · Y Locked · Z Limited", () => MoveLocked(ThighL) && AngularMotion(ThighL, true)).Insp(ThighL, typeof(ConfigurableJoint), "Motion · Angular Motion");
            s.Do("Low -30 · High 90 · Angular Z 25", () => Limits(ThighL, -30f, 90f, 25f)).Insp(ThighL, typeof(ConfigurableJoint), "Low · High Angular X Limit, Angular Z Limit");
            ch.steps.Add(s.Do("보이는 모양을 붙인다", () => HasMesh(ThighL), "모양 붙이기", () => Ch1_Body.AddVisuals(G(Root))).Hier(ThighL, "오른쪽 [모양 붙이기] 버튼"));

            s = new Step { title = "나머지 다리와 머리", goal = "같은 방법을 반복하는 부분은 버튼으로 마저 만든다",
                concept = "남은 부품은 방금 한 것과 같은 방법이다.\n" +
                          "  정강이 3kg  무릎 관절은 1축(접기만): Angular Z 도 Locked, 범위 -120 ~ 0\n" +
                          "  발 3kg      발목 앞뒤 ±35 · 좌우 ±20.  Body Part 의 Is Foot 을 켠다 (발은 닿아도 넘어진 게 아니다)\n" +
                          "  오른다리    왼다리를 +x 쪽으로\n\n" +
                          "발이 3kg 인 이유: 사람 비율(0.6kg)이면 44kg을 떠받치는 발목 관절을 물리 엔진이 다 풀지 못해\n" +
                          "무릎만 굽혀도 발목이 꺾여 넘어진다. (실측: 1kg 오차 29° → 3kg 2°)\n\n" +
                          "머리와 팔은 관절 없는 장식이다. 관절을 주면 행동이 늘고 학습만 오래 걸린다.",
                done = "부품 8개 · 관절 7개(12축) · 44kg 의 몸이 완성됐다." };
            s.Do("정강이 · 발 · 오른다리를 만든다", () => T(Root) != null && T(Root).GetComponentsInChildren<BodyPart>().Length == 8,
                 "나머지 만들기", () => { if (G(Pelvis + "/ThighR") == null) Ch1_Body.RestOfLegs(G(ThighL)); Ch1_Body.AddVisuals(G(Root)); }).Hier(Pelvis, "오른쪽 [나머지 만들기] 버튼");
            s.Do("머리 · 팔(장식)을 붙인다", () => T(Torso + "/Head") != null, "머리·팔 붙이기", () => Ch1_Body.HeadAndArms(G(Torso))).Hier(Torso, "오른쪽 [머리·팔 붙이기] 버튼");
            s.solve = () => { if (G(Pelvis + "/ThighR") == null) Ch1_Body.RestOfLegs(G(ThighL)); if (T(Torso + "/Head") == null) Ch1_Body.HeadAndArms(G(Torso)); Ch1_Body.AddVisuals(G(Root)); };
            ch.steps.Add(s.Do("Hierarchy 에서 Pelvis → ThighL → ShinL → FootL 로 이어진 것을 확인한다").Hier(ThighL + "/ShinL/FootL", "▶ 를 눌러 펼쳐 본다"));

            s = new Step { title = "몸 스크립트 연결", goal = "몸 전체를 다루는 Humanoid Body 를 붙이고 부품을 연결한다",
                concept = "Humanoid Body 는 키보드(2장)든 AI(3장~)든 몸을 움직일 때 늘 거치는 곳이다.\n" +
                          "  처음 자세로 되돌리기 · 무게중심 · 넘어졌는가 · 행동 12개 → 관절 목표\n" +
                          "골반 · 몸통 · 양발은 따로 쓸 일이 있어 칸에 직접 연결한다.",
                hint = "Hierarchy 에서 부품을 Inspector 의 칸으로 끌어다 놓는다.",
                solve = () => Ch1_Body.Wire(G(Root)) };
            s.Do("Humanoid 에 Add Component > Humanoid Body", () => Body != null).Insp(Root, typeof(HumanoidBody), "Add Component ▸ Humanoid Body");
            ch.steps.Add(s.Do("Pelvis · Torso · Foot L · Foot R 칸에 같은 이름의 부품을 끌어다 놓는다", () => Body != null
                && Body.pelvis == Get<BodyPart>(Pelvis) && Body.torso == Get<BodyPart>(Torso)
                && Body.footL == Get<BodyPart>(ThighL + "/ShinL/FootL") && Body.footR == Get<BodyPart>(Pelvis + "/ThighR/ShinR/FootR")).Insp(Root, typeof(HumanoidBody), "Pelvis · Torso · Foot L · Foot R 칸"));

            s = new Step { title = "래그돌 관찰", goal = "모터 없는 몸이 어떻게 무너지는지 본다",
                concept = "아직 관절에 힘이 없다. 실 끊긴 인형처럼 무너지는 것이 정상이다. (실측: 1.5초 만에 바닥)\n" +
                          "날아가거나 부품이 떨어져 나가지 않고, 무릎이 앞으로 꺾이지 않으면 몸이 제대로 만들어진 것이다.\n" +
                          "이상하게 움직이면 관절의 Anchor · Limit 를 다시 본다.",
                done = "몸이 물리로 움직인다. 이 몸의 '무게중심'을 계산해 보자." };
            PlayStep(s).Do("몸이 무너지는 모습을 본다").Game("관찰");
            ch.steps.Add(StopStep(s));

            ch.steps.Add(new Step { title = "빈칸 1-1  무게중심", blank = "1-1",
                goal = "부품 위치와 질량으로 무게중심을 계산한다",
                concept = "COM = (m1·p1 + m2·p2 + …) / (m1 + m2 + …)\n" +
                          "그냥 위치의 평균이 아니다. 무거운 부품일수록 그쪽으로 끌려간다.\n" +
                          "질량 합이 0이면 0으로 나누지 말고 (0,0,0)을 돌려준다.\n\n" +
                          "채점을 통과한 뒤 Play 하면 Scene 뷰에 노란 점(무게중심)이 보인다. 넘어질 때 이 점이 발 밖으로 나간다.\n" +
                          "4장에서는 같은 함수에 위치 대신 속도를 넣어 '몸 전체의 속도'를 잰다.",
                skeleton = "Vector3 sum = Vector3.zero;\nfloat totalMass = 0f;\nfor (int i = 0; i < positions.Length; i++)\n{\n    sum += ___;\n    totalMass += ___;\n}\nreturn ___;",
                hint = "sum += positions[i] * masses[i];  마지막은  totalMass > 0f ? sum / totalMass : Vector3.zero",
                done = "1장 끝 — 학습 환경이 준비됐다. 다음은 이 몸을 움직인다." });

            return list;
        }
    }
}
