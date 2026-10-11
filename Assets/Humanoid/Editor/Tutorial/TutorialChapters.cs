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
        static bool IsStand => Has<StandAgent>(Root) && !Has<WalkAgent>(Root);
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

            // ═══════════════════════════════════════════════════════════
            // 2장 — 기초 움직임
            // ═══════════════════════════════════════════════════════════
            ch = new Chapter { title = "2. 기초 움직임", doc = "02-기초-움직임.md",
                goals = new[] { "관절 모터(PD)가 목표 각도로 당기는 힘이라는 것을 안다", "행동(-1~1) ↔ 관절 각도를 변환한다", "모터만으로는 밀면 넘어진다는 것을 확인한다" } };
            list.Add(ch);

            s = new Step { title = "모터 켜기", goal = "관절에 목표 각도로 당기는 힘을 준다",
                concept = "힘 = spring × (목표 각도 − 지금 각도) − damper × 회전 속도\n" +
                          "AI가 정하는 것은 '목표 각도'뿐이다. 힘은 이 스프링이 낸다. (PD 제어)\n" +
                          "damper 는 출렁임을 잡는다. 보통 spring 의 1/10.\n\n" +
                          "실측(무릎 굽히기를 2초에 걸쳐): spring 300은 2.1초, 1000은 3.6초에 넘어졌다. 4000은 버틴다.",
                solve = () => Set<HumanoidBody>(Root, b => { b.spring = 4000f; b.damper = 400f; }) };
            s.Do("Humanoid 의 Humanoid Body 에서 Spring 4000", () => Body != null && Near(Body.spring, 4000f)).Insp(Root, typeof(HumanoidBody), "Spring");
            ch.steps.Add(s.Do("Damper 400", () => Body != null && Near(Body.damper, 400f)).Insp(Root, typeof(HumanoidBody), "Damper"));

            ch.steps.Add(new Step { title = "빈칸 2-1  행동 → 각도", blank = "2-1",
                goal = "행동(-1~1)을 관절 목표 각도로 바꾼다. 0은 언제나 0도(차렷)",
                concept = "action  -1 ─── 0 ─── +1\nangle   low    0     high\n\n" +
                          "양쪽을 따로 늘린다. low는 음수라는 것에 주의.\n" +
                          "범위 밖 행동(3, -1.5 …)은 -1~1로 자른다.\n\n" +
                          "범위 가운데를 0으로 두면 무릎(-120~0)의 0이 -60도 = 반쯤 주저앉은 자세가 된다.\n" +
                          "0 = 차렷으로 두면 학습이 서 있는 자세에서 시작한다.\n" +
                          "이 함수가 키보드와 AI의 숫자를 관절로 보낸다.",
                skeleton = "action = Mathf.Clamp(___, -1f, 1f);\nreturn action >= 0f ? ___ : ___;",
                hint = "양수면 action * high,  음수면 -action * low.  예: action -0.5, low -30 → 0.5 × (-30) = -15",
                done = "이제 키보드로 이 함수를 써 본다." });

            s = new Step { title = "키보드 조작 붙이기", goal = "키보드로 관절 12축을 움직이고 화면에 각도를 띄운다",
                concept = "키보드가 만드는 것도 AI와 같은 '행동 12개(-1~1)'다. 3장부터는 AI가 이 자리에 들어온다.\n" +
                          "  ←→ 관절 고르기 · ↑↓ 움직이기 · 1 차렷 · 2 무릎 굽히기 · H 매달기 · P 밀기 · R 처음으로\n" +
                          "행동 순서: 허리 앞뒤·좌우 | 왼 엉덩이 앞뒤·좌우, 무릎, 발목 앞뒤·좌우 | 오른쪽 같은 순서",
                solve = () => Set<KeyboardControl>(Root, k => k.driveBody = true) };
            ch.steps.Add(s.Do("Humanoid 에 Add Component > Keyboard Control", () => Has<KeyboardControl>(Root)).Insp(Root, typeof(KeyboardControl), "Add Component ▸ Keyboard Control"));

            s = new Step { title = "손으로 관절 돌려 보기", goal = "AI보다 먼저, 사람이 몸을 움직여 확인한다",
                concept = "화면 왼쪽 위에 관절마다 목표/실제 각도가 보인다.\n" +
                          "공중에 매달면 넘어질 걱정 없이 관절만 볼 수 있다.\n" +
                          "자세는 2초에 걸쳐 바꾼다. 한 번에 바꾸면 그 움직임에 몸이 밀려 넘어진다.",
                hint = "Game 창을 한 번 클릭해야 키보드 입력이 들어간다." };
            PlayStep(s);
            s.Do("H 로 공중에 매단다", () => Playing && Body != null && Body.pelvis != null && Body.pelvis.rb != null && Body.pelvis.rb.isKinematic).Game("H");
            s.Do("←→ 로 관절을 고르고 ↑↓ 로 끝까지 돌린다. 목표와 실제가 같이 움직인다", () => Playing && Has<KeyboardControl>(Root) && Get<KeyboardControl>(Root).actions != null && Array.Exists(Get<KeyboardControl>(Root).actions, a => Mathf.Abs(a) > 0.95f)).Game("← → 로 고르기, ↑ ↓ 로 돌리기");
            ch.steps.Add(s.Do("H 로 내리고 2 를 눌러 무릎을 굽힌다 — 넘어지지 않는다", () => Playing && Body != null && Body.pelvis.rb != null && !Body.pelvis.rb.isKinematic && Body.Angle(4) < -30f && !Body.HasFallen).Game("H, 2"));

            ch.steps.Add(new Step { title = "빈칸 2-2  각도 → -1~1", blank = "2-2",
                goal = "관절 각도를 -1~1로 되돌린다 (2-1의 거꾸로)",
                concept = "3장에서 AI에게 '지금 자세'를 보여줄 때 쓴다.\n" +
                          "각도를 그대로 넣으면 무릎(-120)과 발목(±35)의 크기가 달라 큰 숫자가 학습을 지배한다.\n" +
                          "\"범위의 몇 %까지 갔나\"로 바꿔 넣는다.\n\n" +
                          "나눌 범위가 0이면(무릎의 high) 0을 돌려준다. 0으로 나누면 NaN이 되어 학습이 망가진다.\n" +
                          "관절은 한계를 살짝 넘기도 하니 결과를 -1~1로 자른다.",
                skeleton = "float range = angle >= 0f ? ___ : ___;\nif (range <= 0f) return 0f;\nreturn Mathf.Clamp(___, -1f, 1f);",
                hint = "range 는 양수가 되게: high 또는 -low.  마지막은 angle / range" });

            s = new Step { title = "밀어서 넘어뜨리기", goal = "관절이 단단한 것과 균형을 잡는 것이 다르다는 것을 본다",
                concept = "모터는 '관절 각도'를 지킬 뿐 '몸 전체가 쓰러지는지'는 모른다. (실측: 몸통을 미는 세기)\n" +
                          "  10 N·s  앞 버팀 · 옆 버팀 · 뒤 넘어짐 (뒤꿈치가 짧다)\n" +
                          "  20 N·s  앞 · 옆 · 뒤 모두 넘어짐\n\n" +
                          "넘어지지 않으려면 발을 내딛거나 발목·엉덩이를 순간적으로 틀어야 한다. 그 판단을 3장에서 AI가 배운다.\n" +
                          "R 을 누르면 몸과 목표 각도가 모두 차렷으로 돌아간다.",
                done = "2장 끝 — 강화학습 전 환경 구성이 끝났다. 이 문제를 AI에게 맡긴다." };
            PlayStep(s).Do("1 로 차렷 자세에서 P 로 민다 → 넘어진다").Game("1, P");
            s.Do("R 로 다시 세운다").Game("R");
            ch.steps.Add(StopStep(s));

            // ═══════════════════════════════════════════════════════════
            // 3장 — 서기 학습
            // ═══════════════════════════════════════════════════════════
            ch = new Chapter { title = "3. 서기 학습", doc = "03-서기-학습.md",
                goals = new[] { "에이전트 · 두뇌(Behavior Parameters) · 판단 주기를 직접 붙인다", "기울기를 관측하고, 서 있기를 보상으로 표현한다", "밀려도 버티는 정책을 학습한다" } };
            list.Add(ch);

            ch.steps.Add(new Step { title = "빈칸 3-1  몸이 느끼는 아래쪽", blank = "3-1",
                goal = "세상의 아래 (0,-1,0)을 몸 기준 좌표로 바꾼다",
                concept = "AI가 '얼마나 기울었나'를 알아야 버틸 수 있다. 사람의 귓속 평형감각과 같은 정보다.\n" +
                          "똑바로 서면 (0,-1,0), 앞으로 숙이면 아래가 몸의 앞쪽(+z)에 있다.\n\n" +
                          "회전 * 벡터 는 몸 기준 → 세상 기준. 그 반대는 Quaternion.Inverse(회전) * 벡터.\n" +
                          "몸 기준으로 주면 어느 쪽을 보고 서 있든 같은 상황에 같은 숫자가 나온다.",
                skeleton = "return ___ * Vector3.down;",
                hint = "Quaternion.Inverse(bodyRotation) * Vector3.down" });

            s = new Step { title = "에이전트 붙이기", goal = "몸에 '두뇌가 붙을 자리' Stand Agent 를 단다",
                concept = "Stand Agent 의 네 함수 (RollerBall · RobotArms 와 같다)\n" +
                          "  OnEpisodeBegin       몸과 목표 각도를 차렷으로 되돌린다\n" +
                          "  CollectObservations  36개: 골반 높이 1 · 기울기 3 · 속도 3 · 회전 속도 3 · 관절 각도 12 · 관절 속도 12 · 발 접촉 2\n" +
                          "  OnActionReceived     행동 12개 → 관절. 3초마다 몸통을 무작위로 0~20 N·s 민다. 넘어지면 판을 끝낸다\n" +
                          "  Heuristic            키보드 행동을 그대로 쓴다\n\n" +
                          "안 밀면 '가만히 있기'만 배운다. 2장에서 봤듯 그것만으로도 서 있기 때문이다.\n" +
                          "Max Step 1000 = 20초. 넘어지지 않아도 여기서 판이 끝난다.",
                solve = () => { var go = G(Root); if (!IsStand) { Undo.AddComponent<StandAgent>(go); foreach (var w in go.GetComponents<WalkAgent>()) Undo.DestroyObjectImmediate(w); } Set<StandAgent>(Root, agent => agent.MaxStep = 1000); } };
            s.Do("Humanoid 에 Add Component > Stand Agent  (Behavior Parameters 가 같이 붙는다)", () => IsStand).Insp(Root, typeof(StandAgent), "Add Component ▸ Stand Agent");
            ch.steps.Add(s.Do("Stand Agent 의 Max Step 1000", () => IsStand && Get<StandAgent>(Root).MaxStep == 1000).Insp(Root, typeof(StandAgent), "Max Step"));

            s = new Step { title = "두뇌 설정", goal = "Behavior Parameters 에 이름 · 관측 수 · 행동 수를 넣는다",
                concept = "Behavior Name  학습기(yaml)가 이 이름으로 두뇌를 찾는다 → HumanoidStand\n" +
                          "Space Size     관측 개수. CollectObservations 에서 넣는 개수와 반드시 같아야 한다 → 36\n" +
                          "Continuous     연속 행동 개수 = 관절 12축 → 12\n\n" +
                          "개수가 다르면 Play 하는 순간 에러가 난다.",
                solve = () => Set<BehaviorParameters>(Root, b => { b.BehaviorName = "HumanoidStand"; b.BrainParameters.VectorObservationSize = 36; b.BrainParameters.ActionSpec = Unity.MLAgents.Actuators.ActionSpec.MakeContinuous(12); }) };
            s.Do("Behavior Name: HumanoidStand", () => Brain != null && Brain.BehaviorName == "HumanoidStand").Insp(Root, typeof(BehaviorParameters), "Behavior Name");
            s.Do("Vector Observation > Space Size: 36", () => Brain != null && Brain.BrainParameters.VectorObservationSize == 36).Insp(Root, typeof(BehaviorParameters), "Vector Observation ▸ Space Size");
            ch.steps.Add(s.Do("Actions > Continuous Actions: 12 (Discrete Branches 0)", () => Brain != null && Brain.BrainParameters.ActionSpec.NumContinuousActions == 12 && Brain.BrainParameters.ActionSpec.NumDiscreteActions == 0).Insp(Root, typeof(BehaviorParameters), "Actions ▸ Continuous Actions"));

            s = new Step { title = "판단 주기", goal = "AI가 1초에 몇 번 판단할지 정한다",
                concept = "물리 50Hz, Decision Period 5 → AI는 1초에 10번 판단한다.\n" +
                          "판단 사이에는 직전 목표 각도를 유지하고(Take Actions Between Decisions), 모터가 버틴다.\n" +
                          "사람도 근육 하나하나를 매 순간 의식하지 않는다.",
                solve = () => Set<DecisionRequester>(Root, d => { d.DecisionPeriod = 5; d.TakeActionsBetweenDecisions = true; }) };
            s.Do("Humanoid 에 Add Component > Decision Requester", () => Has<DecisionRequester>(Root)).Insp(Root, typeof(DecisionRequester), "Add Component ▸ Decision Requester");
            ch.steps.Add(s.Do("Decision Period 5 · Take Actions Between Decisions 켬", () => Has<DecisionRequester>(Root) && Get<DecisionRequester>(Root).DecisionPeriod == 5 && Get<DecisionRequester>(Root).TakeActionsBetweenDecisions).Insp(Root, typeof(DecisionRequester), "Decision Period · Take Actions Between Decisions"));

            s = new Step { title = "키보드는 행동만", goal = "몸은 에이전트가 움직이게 하고, 키보드는 행동만 만들게 한다",
                concept = "둘 다 몸을 움직이면 서로 덮어쓴다.\n" +
                          "Drive Body 를 끄면 키보드는 행동 12개만 만들고, Heuristic Only 일 때 에이전트가 그것을 쓴다.\n" +
                          "화면 표시(각도 · 보상)는 그대로 나온다.",
                solve = () => Set<KeyboardControl>(Root, k => k.driveBody = false) };
            ch.steps.Add(s.Do("Keyboard Control 의 Drive Body 체크를 끈다", () => Has<KeyboardControl>(Root) && !Get<KeyboardControl>(Root).driveBody).Insp(Root, typeof(KeyboardControl), "Drive Body"));

            ch.steps.Add(new Step { title = "빈칸 3-2  서기 보상", blank = "3-2",
                goal = "똑바른 정도 × 높이 비율 (0~1)",
                concept = "똑바른 정도 = up.y 를 0~1로 자른 값,  높이 비율 = height / standHeight 를 0~1로 자른 값.\n\n" +
                          "더하지 않고 곱한다. 더하면 '쪼그려 앉아 똑바로'나 '기운 채 높이'도 절반 점수를 받는다.\n" +
                          "넘어지면 판을 끝낸다. 벌점은 없다 — 앞으로 받을 점수가 사라지는 것이 벌이다.\n" +
                          "할인율 γ: 먼 미래 보상일수록 γ를 한 번씩 더 곱해 깎는다. 매 스텝 1점이면 합은 1/(1-γ) = 100 (γ=0.99). 넘어지면 이 100점이 사라진다.",
                skeleton = "return Mathf.Clamp01(___) * Mathf.Clamp01(___);",
                hint = "Mathf.Clamp01(up.y) * Mathf.Clamp01(height / standHeight)" });

            s = new Step { title = "Heuristic 으로 먼저 돌려 보기", goal = "학습 전에 에러 없이 판이 돌아가는지 확인한다",
                concept = "Heuristic Only = AI 대신 키보드가 행동을 낸다.\n" +
                          "3초마다 밀려서 넘어지면 판이 끝나고 차렷 자세로 다시 선다. 화면 왼쪽 위에 에피소드 수와 보상이 보인다.",
                hint = "관측 관련 에러가 나면 Space Size(36)와 코드의 AddObservation 개수가 같은지 본다." };
            s.Do("Behavior Type 을 Heuristic Only 로 바꾼다", () => IsBehavior(BehaviorType.HeuristicOnly), "바꾸기", () => SetBehavior(BehaviorType.HeuristicOnly)).Insp(Root, typeof(BehaviorParameters), "Behavior Type");
            PlayStep(s).Do("밀려서 넘어지고 판이 새로 시작되는 것을 본다", () => Playing && Has<StandAgent>(Root) && Get<StandAgent>(Root).CompletedEpisodes >= 1).Game("관찰 (왼쪽 위 에피소드 수)");
            StopStep(s);
            ch.steps.Add(s.Do("Behavior Type 을 Default 로 되돌린다", () => !Playing && IsBehavior(BehaviorType.Default), "되돌리기", () => SetBehavior(BehaviorType.Default)).Insp(Root, typeof(BehaviorParameters), "Behavior Type"));

            s = new Step { title = "학습하기", goal = "mlagents-learn 으로 서기를 학습한다",
                concept = "터미널(프로젝트 폴더)에서:\n  run_env.bat\n  mlagents-learn Assets/Humanoid/humanoid_config.yaml --run-id=stand\n" +
                          "'Start training by pressing Play' 가 뜨면 Unity에서 Play.\n\n" +
                          "실측(빌드 4개 병렬): 20만 스텝 80 → 100만 420 → 200만 약 780.  행동 0이면 350.\n" +
                          "처음엔 행동을 크게 흔들어 보기 때문에 '가만히 있기'보다 점수가 낮다. 정상이다.",
                hint = "파이썬이 안 붙으면 Behavior Name(HumanoidStand)과 yaml 키 이름이 같은지 본다." };
            s.Do("학습 설정(yaml)을 열어 HumanoidStand 부분을 읽는다", null, "yaml 열기", () => EditorUtility.OpenWithDefaultApp("Assets/Humanoid/humanoid_config.yaml"));
            s.Do("학습 명령을 복사해 터미널에서 실행한다", null, "명령 복사", () => Copy("mlagents-learn Assets/Humanoid/humanoid_config.yaml --run-id=stand"));
            s.Do("Unity에서 Play → 학습기가 연결된다", () => Playing && Academy.IsInitialized && Academy.Instance.IsCommunicatorOn).Toolbar("▶");
            ch.steps.Add(s.Do("tensorboard --logdir results 로 보상이 오르는 것을 본다"));

            s = new Step { title = "학습된 모델 평가", goal = "학습 결과를 탐험 없이 확인한다",
                concept = "results/stand/HumanoidStand.onnx 를 Assets 아래로 끌어와 Behavior Parameters 의 Model 칸에 넣는다.\n\n" +
                          "실측(30판): 행동 0  평균 350 · 끝까지 버팀 1/30   →   학습 후  평균 848 · 22/30",
                done = "3장 끝. 이제 걷는다." };
            s.Do("Model 칸에 학습된 .onnx 를 넣는다", () => Brain != null && Brain.Model != null).Insp(Root, typeof(BehaviorParameters), "Model ← .onnx 끌어다 놓기");
            s.Do("Behavior Type 을 Inference Only 로 바꾼다", () => IsBehavior(BehaviorType.InferenceOnly), "바꾸기", () => SetBehavior(BehaviorType.InferenceOnly)).Insp(Root, typeof(BehaviorParameters), "Behavior Type");
            PlayStep(s);
            ch.steps.Add(s.Do("P 로 직접 밀어 본다 — 대부분 버틴다").Game("P"));

            // ═══════════════════════════════════════════════════════════
            // 4장 — 걷기 보상
            // ═══════════════════════════════════════════════════════════
            ch = new Chapter { title = "4. 걷기 보상", doc = "04-걷기-보상.md",
                goals = new[] { "목표 상자와 걷기 에이전트를 직접 붙인다", "몸이 기울어도 변하지 않는 '보는 방향'을 계산한다", "속도 × 방향 보상으로 스스로 걷는 정책을 학습한다 (최종 과제)" } };
            list.Add(ch);

            ch.steps.Add(new Step { title = "빈칸 4-1  좌우 회전만", blank = "4-1",
                goal = "바라보는 방향에서 앞뒤 기울기를 버리고 좌우 회전만 남긴다",
                concept = "목표 방향을 '내가 보는 방향' 기준으로 관측한다. 목표가 오른쪽이면 언제나 x가 양수.\n" +
                          "그런데 걷는 몸은 앞뒤로 기운다. 기운 정면을 쓰면 '땅을 보는 방향'이 된다.\n" +
                          "그래서 forward.y = 0 으로 만든 뒤 LookRotation.\n" +
                          "바로 위/아래를 볼 때는 수평 성분이 없으니 Quaternion.identity.",
                skeleton = "forward.y = ___;\nif (forward.sqrMagnitude < 1e-6f) return Quaternion.identity;\nreturn Quaternion.LookRotation(___);",
                hint = "forward.y = 0f;  …  Quaternion.LookRotation(forward.normalized)" });

            s = new Step { title = "목표 상자", goal = "걸어갈 목표를 학습 영역에 놓는다",
                concept = "에이전트가 판마다 이 상자를 몸 주변 4~8m 무작위 자리로 옮긴다. 닿으면 +1 하고 다음 자리로.\n" +
                          "콜라이더는 지운다. 닿음은 거리로 판정하므로, 콜라이더가 있으면 걸려 넘어지기만 한다.",
                solve = () => Replace(Target, () => { var t = GameObject.CreatePrimitive(PrimitiveType.Cube); t.name = "Target"; t.transform.SetParent(T(Area), false); t.transform.localPosition = new Vector3(0f, 0.25f, 6f); t.transform.localScale = Vector3.one * 0.5f; Object.DestroyImmediate(t.GetComponent<Collider>()); t.GetComponent<Renderer>().sharedMaterial = HumanoidBuild.Mat("M_WalkTarget", new Color(0.95f, 0.3f, 0.3f)); return t; }) };
            s.Do("HumanoidArea 아래에 3D Object > Cube, 이름 Target", () => ChildOf(Target, Area)).Hier(Area, "우클릭 ▸ 3D Object ▸ Cube ▸ 이름 Target");
            s.Do("Position (0, 0.25, 6) · Scale (0.5, 0.5, 0.5)", () => LocalPos(Target, new Vector3(0f, 0.25f, 6f)) && Near(T(Target).localScale, Vector3.one * 0.5f)).Insp(Target, typeof(Transform), "Position · Scale");
            ch.steps.Add(s.Do("Box Collider 를 지운다 (⋮ > Remove Component)", () => T(Target) != null && T(Target).GetComponent<Collider>() == null).Insp(Target, typeof(BoxCollider), "⋮ ▸ Remove Component"));

            s = new Step { title = "걷기 에이전트로 바꾸기", goal = "Stand Agent 를 Walk Agent 로 바꾼다",
                concept = "Walk Agent 는 Stand Agent 를 상속한다. 관측 36 · 행동 12 · 넘어지면 끝내기는 그대로이고\n" +
                          "관측 3개(목표 방향 2 + 목표 속도 1)와 보상만 바뀐다. 한 번에 하나씩.\n\n" +
                          "Decision Requester 가 에이전트를 필요로 해서, 새 것을 먼저 붙이고 옛 것을 지운다.\n" +
                          "걷기를 배우는 동안은 밀지 않는다 (Push Interval 0). 한 판 40초 (Max Step 2000).",
                solve = () => { var go = G(Root); if (!Has<WalkAgent>(Root)) Undo.AddComponent<WalkAgent>(go); foreach (var a in go.GetComponents<StandAgent>()) if (!(a is WalkAgent)) Undo.DestroyObjectImmediate(a); Set<WalkAgent>(Root, w => { w.target = T(Target); w.targetSpeed = 1f; w.pushInterval = 0; w.MaxStep = 2000; }); } };
            s.Do("Humanoid 에 Add Component > Walk Agent", () => Has<WalkAgent>(Root)).Insp(Root, typeof(WalkAgent), "Add Component ▸ Walk Agent");
            s.Do("Stand Agent 를 지운다 (⋮ > Remove Component)", () => T(Root) != null && T(Root).GetComponents<StandAgent>().Length == 1).Insp(Root, typeof(StandAgent), "Stand Agent 의 ⋮ ▸ Remove Component");
            s.Do("Walk Agent 의 Target 칸에 Target 을 끌어다 놓는다", () => Has<WalkAgent>(Root) && Get<WalkAgent>(Root).target == T(Target)).Insp(Root, typeof(WalkAgent), "Target ← Hierarchy 의 Target 을 끌어다 놓기");
            ch.steps.Add(s.Do("Target Speed 1 · Push Interval 0 · Max Step 2000", () => Has<WalkAgent>(Root) && Near(Get<WalkAgent>(Root).targetSpeed, 1f) && Get<WalkAgent>(Root).pushInterval == 0 && Get<WalkAgent>(Root).MaxStep == 2000).Insp(Root, typeof(WalkAgent), "Target Speed · Push Interval · Max Step"));

            s = new Step { title = "두뇌 설정", goal = "걷기용 이름과 관측 수로 바꾼다",
                concept = "Behavior Name 을 바꾸면 서기와 다른 두뇌로 학습된다 → HumanoidWalk\n" +
                          "관측 39 = 서기 36 + 목표 방향 2 + 목표 속도 1. 행동은 그대로 12.",
                solve = () => Set<BehaviorParameters>(Root, b => { b.BehaviorName = "HumanoidWalk"; b.BrainParameters.VectorObservationSize = 39; }) };
            s.Do("Behavior Name: HumanoidWalk", () => Brain != null && Brain.BehaviorName == "HumanoidWalk").Insp(Root, typeof(BehaviorParameters), "Behavior Name");
            ch.steps.Add(s.Do("Space Size: 39", () => Brain != null && Brain.BrainParameters.VectorObservationSize == 39).Insp(Root, typeof(BehaviorParameters), "Vector Observation ▸ Space Size"));

            ch.steps.Add(new Step { title = "빈칸 4-2  속도 보상", blank = "4-2",
                goal = "목표 속도로 목표 쪽으로 가면 1, 멈추면 0",
                concept = "d = |velocity − direction × targetSpeed| 를 0~targetSpeed 로 자르고,  (1 − (d/targetSpeed)²)²\n" +
                          "위아래 속도(velocity.y)는 버린다 — 떨어지는 속도는 걷기가 아니다.\n" +
                          "몸 전체 속도는 1장 무게중심 함수에 속도를 넣어 구한다.",
                skeleton = "velocity.y = 0f;\nfloat d = Mathf.Clamp(___, 0f, targetSpeed);\nfloat r = 1f - (d / targetSpeed) * (d / targetSpeed);\nreturn ___;",
                hint = "(velocity - direction * targetSpeed).magnitude  …  return r * r;" });

            ch.steps.Add(new Step { title = "빈칸 4-3  방향 보상", blank = "4-3",
                goal = "목표 쪽을 보면 1, 옆이면 0.5, 반대면 0",
                concept = "(내적 + 1) / 2.  둘 다 y를 버리고 길이 1로 만든 뒤 내적한다.\n\n" +
                          "보상 = 속도 × 방향. 곱하면 둘 다 만족해야 점수가 난다.\n" +
                          "더하면 '목표를 보고 서 있기'나 '뒷걸음질'도 절반을 받아 AI가 쉬운 쪽만 한다.",
                skeleton = "forward.y = 0f;\ndirection.y = 0f;\nreturn (Vector3.Dot(___, ___) + 1f) * 0.5f;",
                hint = "Vector3.Dot(forward.normalized, direction.normalized)" });

            s = new Step { title = "서 있으면 0점", goal = "학습 전 상태를 확인한다",
                concept = "Heuristic Only 로 Play하면 키보드로 아무리 움직여도 보상이 거의 0이다.\n" +
                          "사람이 손으로 12축을 맞춰 걷게 만들 수는 없다. 그래서 학습한다." };
            s.Do("Behavior Type 을 Heuristic Only 로 바꾼다", () => IsBehavior(BehaviorType.HeuristicOnly), "바꾸기", () => SetBehavior(BehaviorType.HeuristicOnly)).Insp(Root, typeof(BehaviorParameters), "Behavior Type");
            PlayStep(s).Do("화면의 보상이 거의 0인 것을 본다", () => Playing && Has<WalkAgent>(Root) && Get<WalkAgent>(Root).StepCount > 100).Game("관찰 (왼쪽 위 보상)");
            StopStep(s);
            ch.steps.Add(s.Do("Behavior Type 을 Default 로 되돌린다", () => !Playing && IsBehavior(BehaviorType.Default), "되돌리기", () => SetBehavior(BehaviorType.Default)).Insp(Root, typeof(BehaviorParameters), "Behavior Type"));

            s = new Step { title = "걷기 학습", goal = "스스로 걷는 정책을 학습한다",
                concept = "mlagents-learn Assets/Humanoid/humanoid_config.yaml --run-id=walk\n" +
                          "걷기는 오래 걸린다. 5장의 병렬 학습(영역 9개 · 빌드 4개)을 먼저 하면 그만큼 빠르다.\n\n" +
                          "실측(에이전트 36개): 100만 37 → 300만 531(급상승) → 1000만 약 1430, 약 50분.\n" +
                          "평가: 목표 1.0 m/s · 10판 · 넘어짐 0 · 40초에 목표 5.5개 도달.",
                done = "4장 끝 — 최종 과제 완성. 5장은 더 빨리 · 더 넓게 · 제대로 재는 법이다." };
            s.Do("학습 명령을 복사해 실행한다", null, "명령 복사", () => Copy("mlagents-learn Assets/Humanoid/humanoid_config.yaml --run-id=walk"));
            ch.steps.Add(s.Do("학습된 모델로 Play → 빨간 상자를 향해 걷고, 닿으면 다음 상자로 방향을 튼다"));

            return list;
        }
    }
}
