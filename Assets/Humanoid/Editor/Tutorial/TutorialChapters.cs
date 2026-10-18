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
    // 장 하나 = 단계 여러 개.
    internal class Chapter
    {
        public string title, doc;
        public string[] goals;
        public List<Step> steps = new List<Step>();
    }

    // 단계 하나 = 작은 변경 하나. 할 일(Task)을 위에서부터 차례로 해 나간다.
    // blank가 있으면 코드 실습 단계다: 구현 시작 → 직접 작성 → 테스트 가 할 일 대신 붙는다.
    //   start    시작 코드. null 이면 "처음부터"(임시값 한 줄만). mode 는 창에 보이는 실습 형태
    //   hints    힌트 1~3단계 (방향·개념 / 구조·순서 / 핵심 로직).  skeleton = 4단계(빈칸 코드), explain = 5단계 해설
    // solve는 막혔을 때 [대신 해주기]가 하는 일이다.
    internal class Step
    {
        public string title, goal, concept, hint, blank, skeleton, done;
        public string start, mode, explain;
        public string[] hints;
        public Action solve;
        public List<Task> tasks = new List<Task>();

        // 할 일 추가. check가 없으면 [했어요] 버튼으로 넘어간다.
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

    // 튜토리얼 내용. 학습자가 Hierarchy · Inspector 를 직접 만지며 장을 완성한다.
    // 자세한 설명은 문서(Docs/NN)에 있고 창의 [📖 문서]로 연다.
    internal static class TutorialChapters
    {
        const string PracticeScene = "Assets/Humanoid/Scenes/Humanoid.unity";     // 빈 실습 씬. 정답은 Humanoid_Answer.unity

        // 계층 경로. 조건은 이 이름·위치를 정확히 본다.
        const string Area = HumanoidBuild.AreaName;
        const string Root = Area + "/Humanoid";
        const string Pelvis = Root + "/Pelvis";
        const string Torso = Pelvis + "/Torso";
        const string ThighL = Pelvis + "/ThighL";
        const string UpperArmL = Torso + "/UpperArmL";
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

        // [대신 해주기]: 같은 이름이 있으면 지우고 정답으로 다시 만든다.
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

        // 실습 씬을 열고, 코드 실습도 처음 상태(시작 코드)로 만든다. 앞으로 할 실습의 정답이 파일에 보이지 않게.
        static void OpenPractice()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            BlankTool.StartAll();
            if (System.IO.File.Exists(PracticeScene)) { EditorSceneManager.OpenScene(PracticeScene); return; }
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
                        if (st.hints == null || st.hints.Length != 3 || string.IsNullOrEmpty(st.skeleton) || string.IsNullOrEmpty(st.explain)) problems.Add(where + ": 힌트 1~5단계가 다 있지 않음");
                        if (!st.skeleton.Contains("___")) problems.Add(where + ": 힌트 4단계 코드에 빈칸(___)이 없음");
                        if (BlankCheck.Grade(st.blank).Count < 3) problems.Add(where + ": 테스트 케이스 3개 미만");
                        if (BlankTool.Line(st.blank) <= 1) problems.Add(where + ": HumanoidMath.cs 에서 실습 자리를 찾지 못함");
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

        // 개발자용: 지금 열린 씬에서 [대신 해주기]를 처음부터 차례로 실행하며
        // 각 단계의 조건(Play 가 필요 없는 것)이 전부 통과하는지 본다. 조건과 정답이 어긋나면 잡힌다.
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
            // 학습 환경 구성
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
                          "실습은 Assets/Humanoid/Scenes/Humanoid.unity 에서 한다. 카메라와 조명만 있는 빈 씬에서 처음부터 직접 만든다.\n" +
                          "막히면 완성된 정답 씬 Scenes/Humanoid_Answer.unity 를 열어 비교해 본다.\n\n" +
                          "버튼을 누르면 Scripts/HumanoidMath.cs 의 함수들도 시작 코드로 바뀐다. 각 장의 코드 실습에서 하나씩 직접 구현한다.",
                done = "빈 씬이 준비됐다. 학습할 무대부터 만든다." };
            ch.steps.Add(s.Do("실습 씬(Scenes/Humanoid.unity)을 연다", () => EditorSceneManager.GetActiveScene().path == PracticeScene, "실습 씬 열기", OpenPractice));

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

            s = new Step { title = "몸통", goal = "골반 위에 몸통 부품을 만든다 (머리 무게 포함 9kg)",
                concept = "자식의 Position 은 부모 기준(로컬)이다. 몸통 가운데는 세상에서 y = 1.25,\n" +
                          "골반이 0.96 에 있으니 로컬로는 1.25 − 0.96 = 0.29.\n\n" +
                          "위가 무겁다(몸통 9kg + 팔 5kg). 사람도 그렇다. 그래서 서 있는 것 자체가 '거꾸로 세운 막대'를 버티는 일이다.",
                solve = () => Replace(Torso, () => Ch1_Body.Torso(G(Pelvis))) };
            s.Do("Pelvis 아래에 빈 오브젝트 Torso, Position (0, 0.29, 0)", () => ChildOf(Torso, Pelvis) && LocalPos(Torso, new Vector3(0f, 0.29f, 0f))).Hier(Pelvis, "우클릭 ▸ Create Empty ▸ 이름 Torso, Position");
            s.Do("Box Collider, Size (0.32, 0.4, 0.2)", () => Box(Torso, new Vector3(0.32f, 0.40f, 0.20f))).Insp(Torso, typeof(BoxCollider), "Add Component ▸ Box Collider ▸ Size");
            s.Do("Rigidbody, Mass 9", () => Mass(Torso, 9f)).Insp(Torso, typeof(Rigidbody), "Add Component ▸ Rigidbody ▸ Mass");
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

            s = new Step { title = "왼쪽 위팔과 어깨 관절", goal = "팔 하나를 직접 만들어 본다 — 다리와 같은 방법",
                concept = "팔은 몸통에 매달린다. 몸통 기준으로 왼쪽(-x) 0.23, 위로 0.04 (위팔 가운데 1.29 − 몸통 1.25).\n" +
                          "몸통 옆면(0.16)에서 조금 띄운다. 붙이면 아래팔이 몸통에 걸려 팔이 제대로 내려오지 않는다.\n" +
                          "어깨 관절은 위팔 위 끝 (0, 0.13, 0). 범위는 뒤로 60 · 앞으로 120 · 좌우 30.\n" +
                          "팔도 다리처럼 아래로 매달려 있어 X+ 가 '팔을 앞으로'다. 그래서 Low -60 · High 120.\n\n" +
                          "팔은 걷는 데 꼭 필요하지 않다. 대신 AI가 팔을 휘둘러 균형을 잡을 수 있다(사람도 그렇다).\n" +
                          "그 대가로 행동이 6개 늘어난다 (어깨 2축 × 2 + 팔꿈치 1축 × 2).",
                solve = () => { Replace(UpperArmL, () => Ch1_Body.UpperArm(G(Torso), -0.23f)); Ch1_Body.ShoulderJoint(G(UpperArmL)); } };
            s.Do("Torso 아래에 UpperArmL, Position (-0.23, 0.04, 0)", () => ChildOf(UpperArmL, Torso) && LocalPos(UpperArmL, new Vector3(-0.23f, 0.04f, 0f))).Hier(Torso, "우클릭 ▸ Create Empty ▸ 이름 UpperArmL, Position");
            s.Do("Box Collider (0.08, 0.26, 0.08) · Rigidbody Mass 1.5 · Body Part", () => Box(UpperArmL, new Vector3(0.08f, 0.26f, 0.08f)) && Mass(UpperArmL, 1.5f) && Has<BodyPart>(UpperArmL)).Insp(UpperArmL, typeof(BoxCollider), "Add Component ▸ Box Collider · Rigidbody · Body Part");
            s.Do("Configurable Joint · Connected Body = Torso · Anchor (0, 0.13, 0)", () => Connected(UpperArmL, Torso) && Anchor(UpperArmL, new Vector3(0f, 0.13f, 0f))).Insp(UpperArmL, typeof(ConfigurableJoint), "Add Component ▸ Configurable Joint ▸ Connected Body · Anchor");
            s.Do("Motion 전부 Locked · Angular X Limited · Y Locked · Z Limited", () => MoveLocked(UpperArmL) && AngularMotion(UpperArmL, true)).Insp(UpperArmL, typeof(ConfigurableJoint), "Motion · Angular Motion");
            s.Do("Low -60 · High 120 · Angular Z 30", () => Limits(UpperArmL, -60f, 120f, 30f)).Insp(UpperArmL, typeof(ConfigurableJoint), "Low · High Angular X Limit, Angular Z Limit");
            ch.steps.Add(s.Do("보이는 모양을 붙인다", () => HasMesh(UpperArmL), "모양 붙이기", () => Ch1_Body.AddVisuals(G(Root))).Hier(UpperArmL, "오른쪽 [모양 붙이기] 버튼"));

            s = new Step { title = "나머지 다리 · 팔 · 머리", goal = "같은 방법을 반복하는 부분은 버튼으로 마저 만든다",
                concept = "남은 부품은 방금 한 것과 같은 방법이다.\n" +
                          "  정강이 3kg   무릎 관절은 1축(접기만): Angular Z 도 Locked, 범위 -120 ~ 0\n" +
                          "  발 3kg       발목 앞뒤 ±35 · 좌우 ±20.  Body Part 의 Is Foot 을 켠다 (발은 닿아도 넘어진 게 아니다)\n" +
                          "  아래팔 1kg   팔꿈치도 1축: 범위 0 ~ 130. 무릎과 반대로 앞으로 접힌다\n" +
                          "  오른쪽       왼쪽을 +x 쪽으로\n" +
                          "  머리         관절 없는 장식. 콜라이더만 있다 — 머리가 바닥에 닿으면 넘어진 것이다\n\n" +
                          "발이 3kg 인 이유: 사람 비율(0.6kg)이면 44kg을 떠받치는 발목 관절을 물리 엔진이 다 풀지 못해\n" +
                          "무릎만 굽혀도 발목이 꺾여 넘어진다. (실측: 1kg 오차 29° → 3kg 2°)\n" +
                          "아래팔은 1kg 이어도 괜찮다. 몸무게를 떠받치지 않는다.",
                done = "부품 12개 · 관절 11개(18축) · 44kg 의 몸이 완성됐다." };
            s.Do("정강이 · 발 · 오른다리를 만든다", () => T(ThighL + "/ShinL/FootL") != null && T(Pelvis + "/ThighR/ShinR/FootR") != null,
                 "다리 만들기", () => { if (G(Pelvis + "/ThighR") == null) Ch1_Body.RestOfLegs(G(ThighL)); Ch1_Body.AddVisuals(G(Root)); }).Hier(Pelvis, "오른쪽 [다리 만들기] 버튼");
            s.Do("아래팔 · 오른팔을 만든다", () => T(UpperArmL + "/ForearmL") != null && T(Torso + "/UpperArmR/ForearmR") != null,
                 "팔 만들기", () => { if (G(Torso + "/UpperArmR") == null) Ch1_Body.RestOfArms(G(UpperArmL)); Ch1_Body.AddVisuals(G(Root)); }).Hier(Torso, "오른쪽 [팔 만들기] 버튼");
            s.Do("머리(장식)를 붙인다", () => T(Torso + "/Head") != null, "머리 붙이기", () => Ch1_Body.Head(G(Torso))).Hier(Torso, "오른쪽 [머리 붙이기] 버튼");
            s.solve = () => { if (G(Pelvis + "/ThighR") == null) Ch1_Body.RestOfLegs(G(ThighL)); if (G(Torso + "/UpperArmR") == null) Ch1_Body.RestOfArms(G(UpperArmL)); if (T(Torso + "/Head") == null) Ch1_Body.Head(G(Torso)); Ch1_Body.AddVisuals(G(Root)); };
            ch.steps.Add(s.Do("Hierarchy 에서 Pelvis → Torso → UpperArmL → ForearmL 로 이어진 것을 확인한다").Hier(UpperArmL + "/ForearmL", "▶ 를 눌러 펼쳐 본다"));

            s = new Step { title = "몸 스크립트 연결", goal = "몸 전체를 다루는 Humanoid Body 를 붙이고 부품을 연결한다",
                concept = "Humanoid Body 는 키보드(2장)든 AI(3장~)든 몸을 움직일 때 늘 거치는 곳이다.\n" +
                          "  처음 자세로 되돌리기 · 무게중심 · 넘어졌는가 · 행동 18개 → 관절 목표\n" +
                          "골반 · 몸통 · 양발은 따로 쓸 일이 있어 칸에 직접 연결한다.",
                hint = "Hierarchy 에서 부품을 Inspector 의 칸으로 끌어다 놓는다.",
                solve = () => Ch1_Body.Wire(G(Root)) };
            s.Do("Humanoid 에 Add Component > Humanoid Body", () => Body != null).Insp(Root, typeof(HumanoidBody), "Add Component ▸ Humanoid Body");
            ch.steps.Add(s.Do("Pelvis · Torso · Foot L · Foot R 칸에 같은 이름의 부품을 끌어다 놓는다", () => Body != null
                && Body.pelvis == Get<BodyPart>(Pelvis) && Body.torso == Get<BodyPart>(Torso)
                && Body.footL == Get<BodyPart>(ThighL + "/ShinL/FootL") && Body.footR == Get<BodyPart>(Pelvis + "/ThighR/ShinR/FootR")).Insp(Root, typeof(HumanoidBody), "Pelvis · Torso · Foot L · Foot R 칸"));

            s = new Step { title = "래그돌 관찰", goal = "모터 없는 몸이 어떻게 무너지는지 본다",
                concept = "아직 관절에 힘이 없다. 실 끊긴 인형처럼 무너지는 것이 정상이다. (실측: 1.3초 만에 바닥)\n" +
                          "날아가거나 부품이 떨어져 나가지 않고, 무릎이 앞으로 꺾이지 않으면 몸이 제대로 만들어진 것이다.\n" +
                          "이상하게 움직이면 관절의 Anchor · Limit 를 다시 본다.",
                done = "몸이 물리로 움직인다. 이 몸의 '무게중심'을 계산해 보자." };
            PlayStep(s).Do("몸이 무너지는 모습을 본다").Game("관찰");
            ch.steps.Add(StopStep(s));

            ch.steps.Add(new Step { title = "실습 1-1  무게중심", blank = "1-1",
                goal = "부품 위치와 질량으로 몸 전체의 무게중심을 구하는 함수를 직접 만든다",
                concept = "CenterOfMass(positions, masses) — 부품 위치 배열과 질량 배열을 받아 무게중심(Vector3)을 돌려준다.\n" +
                          "  예) (0,2,0)에 3kg, (0,0,0)에 1kg → (0, 1.5, 0)\n" +
                          "  부품이 하나도 없으면 (0,0,0)\n" +
                          "\n" +
                          "테스트를 통과한 뒤 Play 하면 Scene 뷰에 노란 점(무게중심)이 보인다. 넘어질 때 이 점이 발 밖으로 나간다.\n" +
                          "4장에서는 같은 함수에 위치 대신 속도를 넣어 '몸 전체의 속도'를 잰다.",
                hints = new[] {
                    "무게중심은 '질량으로 가중한 평균'이다. 무거운 부품일수록 결과가 그쪽으로 끌려간다.\n위치를 그냥 평균 내면 3kg 과 1kg 을 똑같이 친다.",
                    "① 위치×질량을 모을 Vector3 합계, 질량을 모을 float 합계를 만든다\n② for 문으로 모든 부품을 돈다\n③ 반복이 끝나면 합계 ÷ 질량 합계를 돌려준다\n④ 질량 합계가 0이면 나누지 않고 Vector3.zero",
                    "COM = (m1·p1 + m2·p2 + …) / (m1 + m2 + …)\n반복 안:  sum += positions[i] * masses[i];   totalMass += masses[i];\n마지막:   totalMass > 0 일 때만 sum / totalMass" },
                skeleton = "Vector3 sum = Vector3.zero;\nfloat totalMass = 0f;\nfor (int i = 0; i < positions.Length; i++)\n{\n    sum += ___;\n    totalMass += ___;\n}\nreturn ___;",
                explain = "질량 가중 평균을 반복 한 번으로 구한다. Vector3 × float 은 x·y·z 각각에 곱한다.\n마지막 줄의 조건(삼항 연산자 ? :)은 부품이 없을 때 0으로 나눠 NaN 이 되는 것을 막는다.\nNaN 이 관측에 섞이면 학습 전체가 망가진다.",
                done = "1장 끝 — 학습 환경이 준비됐다. 다음은 이 몸을 움직인다." });

            // ═══════════════════════════════════════════════════════════
            // 기초 움직임
            // ═══════════════════════════════════════════════════════════
            ch = new Chapter { title = "2. 기초 움직임", doc = "02-기초-움직임.md",
                goals = new[] { "관절 모터(PD)가 목표 각도로 당기는 힘이라는 것을 안다", "행동(-1~1) ↔ 관절 각도를 변환한다", "모터만으로는 밀면 넘어진다는 것을 확인한다" } };
            list.Add(ch);

            s = new Step { title = "모터 켜기", goal = "관절에 목표 각도로 당기는 힘을 준다",
                concept = "힘 = spring × (목표 각도 − 지금 각도) − damper × 회전 속도\n" +
                          "AI가 정하는 것은 '목표 각도'뿐이다. 힘은 이 스프링이 낸다. (PD 제어)\n" +
                          "damper 는 출렁임을 잡는다. 보통 spring 의 1/10.\n\n" +
                          "실측(무릎 굽히기를 2초에 걸쳐): spring 300은 2.3초, 1000은 3.9초에 넘어졌다. 4000은 버틴다.",
                solve = () => Set<HumanoidBody>(Root, b => { b.spring = 4000f; b.damper = 400f; }) };
            s.Do("Humanoid 의 Humanoid Body 에서 Spring 4000", () => Body != null && Near(Body.spring, 4000f)).Insp(Root, typeof(HumanoidBody), "Spring");
            ch.steps.Add(s.Do("Damper 400", () => Body != null && Near(Body.damper, 400f)).Insp(Root, typeof(HumanoidBody), "Damper"));

            ch.steps.Add(new Step { title = "실습 2-1  행동 → 각도", blank = "2-1",
                goal = "행동(-1~1)을 관절 목표 각도로 바꾸는 함수를 직접 만든다",
                concept = "ActionToAngle(action, low, high) — 행동을 목표 각도(도)로. 0은 언제나 0도(차렷).\n" +
                          "  -1 → low,   0 → 0,   +1 → high      (low 는 음수)\n" +
                          "  예) 엉덩이(-30~90): +1 → 90,  -0.5 → -15\n" +
                          "  범위 밖 행동(예: 3)이 와도 low~high 를 넘지 않아야 한다\n" +
                          "\n" +
                          "이 함수가 키보드와 AI의 숫자를 관절로 보낸다. 범위 가운데를 0으로 두면 무릎(-120~0)이 -60도,\n" +
                          "즉 반쯤 주저앉은 자세로 학습이 시작된다.",
                hints = new[] {
                    "양쪽을 따로 늘린다. 0~+1 은 0~high 로, -1~0 은 low~0 으로.\n범위 전체를 직선 하나로 늘리면 0이 0도가 되지 않는다.",
                    "① 행동을 -1~1 로 자른다\n② 양수인지 음수인지에 따라 다른 한계를 곱한다",
                    "자르기:  Mathf.Clamp(action, -1f, 1f)\n양수:   action * high\n음수:   -action * low      (예: -0.5 → 0.5 × (-30) = -15)" },
                skeleton = "action = Mathf.Clamp(___, -1f, 1f);\nreturn action >= 0f ? ___ : ___;",
                explain = "Clamp 로 범위 밖 행동을 막고, 삼항 연산자로 두 구간을 나눈다.\n무릎처럼 high 가 0이면 + 쪽 행동은 모두 0도가 된다 (경계 케이스).",
                done = "이제 키보드로 이 함수를 써 본다." });

            s = new Step { title = "키보드 조작 붙이기", goal = "키보드로 관절 18축을 움직이고 화면에 각도를 띄운다",
                concept = "키보드가 만드는 것도 AI와 같은 '행동 18개(-1~1)'다. 3장부터는 AI가 이 자리에 들어온다.\n" +
                          "  ←→ 관절 고르기 · ↑↓ 움직이기 · 1 차렷 · 2 무릎 굽히기 · H 매달기 · P 밀기 · R 처음으로\n" +
                          "행동 순서 = Hierarchy 위에서 아래 순서:\n" +
                          "  허리 앞뒤·좌우 | 왼 어깨 앞뒤·좌우, 팔꿈치 | 오른팔 | 왼 엉덩이 앞뒤·좌우, 무릎, 발목 앞뒤·좌우 | 오른다리",
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
            ch.steps.Add(s.Do("H 로 내리고 2 를 눌러 무릎을 굽힌다 — 넘어지지 않는다", () => Playing && Body != null && Body.pelvis.rb != null && !Body.pelvis.rb.isKinematic && Body.Angle(10) < -30f && !Body.HasFallen).Game("H, 2"));

            ch.steps.Add(new Step { title = "실습 2-2  각도 → -1~1", blank = "2-2",
                start = "return angle >= 0f ? angle / high : angle / -low;",  mode = "기능 추가",
                goal = "나눗셈만 하는 함수에 경계 처리 두 가지를 덧붙인다",
                concept = "AngleToAction(angle, low, high) — 각도를 -1~1 로 되돌린다 (2-1 의 거꾸로). 3장에서 AI에게 '지금 자세'를 보여줄 때 쓴다.\n" +
                          "각도를 그대로 넣으면 무릎(-120)과 발목(±35)의 크기가 달라 큰 숫자가 학습을 지배한다.\n" +
                          "\n" +
                          "시작 코드는 나눗셈만 한다. 두 기능을 덧붙인다:\n" +
                          "  ① 결과를 -1~1 로 자른다 — 관절은 한계를 살짝 넘기도 한다\n" +
                          "  ② 나눌 범위가 0이면 0을 돌려준다 — 무릎의 high 는 0. 0으로 나누면 무한대·NaN 이 된다\n" +
                          "고치기 전에 [▶ 테스트 실행]으로 어느 요구사항이 깨지는지 먼저 본다.",
                hints = new[] {
                    "있는 식은 맞다. 이상한 입력(한계를 넘은 각도, 범위 0)에서 깨지는 것만 막으면 된다.\n고치기 전에 테스트를 돌려 실패한 요구사항을 확인한다.",
                    "① 나눌 값(range)을 변수로 먼저 뺀다\n② range 가 0 이하면 바로 0 을 돌려준다\n③ 나눈 결과를 Clamp 로 자른다",
                    "float range = angle >= 0f ? high : -low;\nif (range <= 0f) return 0f;\nreturn Mathf.Clamp(angle / range, -1f, 1f);" },
                skeleton = "float range = angle >= 0f ? ___ : ___;\nif (range <= 0f) return 0f;\nreturn Mathf.Clamp(___, -1f, 1f);",
                explain = "range 를 변수로 빼면 '0인지 검사'와 '나누기'에 같은 값을 쓸 수 있다. 검사는 반드시 나누기보다 먼저 온다.\n기존 기능은 그대로 두고 경계만 덧붙이는 것 — 실제 코드를 고칠 때 가장 흔한 작업이다." });

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
            // 서기 학습
            // ═══════════════════════════════════════════════════════════
            ch = new Chapter { title = "3. 서기 학습", doc = "03-서기-학습.md",
                goals = new[] { "에이전트 · 두뇌(Behavior Parameters) · 판단 주기를 직접 붙인다", "기울기를 관측하고, 서 있기를 보상으로 표현한다", "밀려도 버티는 정책을 학습한다" } };
            list.Add(ch);

            ch.steps.Add(new Step { title = "실습 3-1  몸이 느끼는 아래쪽", blank = "3-1",
                goal = "세상의 아래 (0,-1,0)을 몸 기준 좌표로 바꾸는 함수를 직접 만든다",
                concept = "DownInBody(bodyRotation) — 세상의 아래 (0,-1,0)을 몸 기준 좌표로 바꿔 돌려준다. AI의 평형감각이다.\n" +
                          "  똑바로 → (0,-1,0)    앞으로 숙임 → 아래가 몸의 앞쪽(+z)    거꾸로 → (0,1,0)\n" +
                          "\n" +
                          "몸 기준으로 주면 어느 쪽을 보고 서 있든 같은 자세엔 같은 숫자가 나온다.",
                hints = new[] {
                    "회전(Quaternion) × 벡터 는 '몸 기준 → 세상 기준'으로 돌린다. 지금 필요한 것은 그 반대 방향이다.",
                    "① 몸 회전의 역회전을 구한다\n② 역회전 × 세상의 아래 벡터를 돌려준다 — 한 줄이면 된다",
                    "역회전:      Quaternion.Inverse(bodyRotation)\n세상의 아래:  Vector3.down" },
                skeleton = "return ___ * Vector3.down;",
                explain = "Inverse(회전) * 벡터 = 세상 벡터를 몸 좌표로. 그냥 bodyRotation * Vector3.down 으로 쓰면\n숙였을 때 z 부호가 반대로 나온다 (케이스 '앞으로 60도 숙임')." });

            s = new Step { title = "에이전트 붙이기", goal = "몸에 '두뇌가 붙을 자리' Stand Agent 를 단다",
                concept = "Stand Agent 의 네 함수 (RollerBall · RobotArms 와 같다)\n" +
                          "  OnEpisodeBegin       몸과 목표 각도를 차렷으로 되돌린다\n" +
                          "  CollectObservations  48개: 골반 높이 1 · 기울기 3 · 속도 3 · 회전 속도 3 · 관절 각도 18 · 관절 속도 18 · 발 접촉 2\n" +
                          "  OnActionReceived     행동 18개 → 관절. 3초마다 몸통을 무작위로 0~20 N·s 민다. 넘어지면 판을 끝낸다\n" +
                          "  Heuristic            키보드 행동을 그대로 쓴다\n\n" +
                          "안 밀면 '가만히 있기'만 배운다. 2장에서 봤듯 그것만으로도 서 있기 때문이다.\n" +
                          "Max Step 1000 = 20초. 넘어지지 않아도 여기서 판이 끝난다.",
                solve = () => { var go = G(Root); if (!IsStand) { Undo.AddComponent<StandAgent>(go); foreach (var w in go.GetComponents<WalkAgent>()) Undo.DestroyObjectImmediate(w); } Set<StandAgent>(Root, agent => agent.MaxStep = 1000); } };
            s.Do("Humanoid 에 Add Component > Stand Agent  (Behavior Parameters 가 같이 붙는다)", () => IsStand).Insp(Root, typeof(StandAgent), "Add Component ▸ Stand Agent");
            ch.steps.Add(s.Do("Stand Agent 의 Max Step 1000", () => IsStand && Get<StandAgent>(Root).MaxStep == 1000).Insp(Root, typeof(StandAgent), "Max Step"));

            s = new Step { title = "두뇌 설정", goal = "Behavior Parameters 에 이름 · 관측 수 · 행동 수를 넣는다",
                concept = "Behavior Name  학습기(yaml)가 이 이름으로 두뇌를 찾는다 → HumanoidStand\n" +
                          "Space Size     관측 개수. CollectObservations 에서 넣는 개수와 반드시 같아야 한다 → 48\n" +
                          "Continuous     연속 행동 개수 = 관절 18축 → 18\n\n" +
                          "개수가 다르면 Play 하는 순간 에러가 난다.",
                solve = () => Set<BehaviorParameters>(Root, b => { b.BehaviorName = "HumanoidStand"; b.BrainParameters.VectorObservationSize = 48; b.BrainParameters.ActionSpec = Unity.MLAgents.Actuators.ActionSpec.MakeContinuous(18); }) };
            s.Do("Behavior Name: HumanoidStand", () => Brain != null && Brain.BehaviorName == "HumanoidStand").Insp(Root, typeof(BehaviorParameters), "Behavior Name");
            s.Do("Vector Observation > Space Size: 48", () => Brain != null && Brain.BrainParameters.VectorObservationSize == 48).Insp(Root, typeof(BehaviorParameters), "Vector Observation ▸ Space Size");
            ch.steps.Add(s.Do("Actions > Continuous Actions: 18 (Discrete Branches 0)", () => Brain != null && Brain.BrainParameters.ActionSpec.NumContinuousActions == 18 && Brain.BrainParameters.ActionSpec.NumDiscreteActions == 0).Insp(Root, typeof(BehaviorParameters), "Actions ▸ Continuous Actions"));

            s = new Step { title = "판단 주기", goal = "AI가 1초에 몇 번 판단할지 정한다",
                concept = "물리 50Hz, Decision Period 5 → AI는 1초에 10번 판단한다.\n" +
                          "판단 사이에는 직전 목표 각도를 유지하고(Take Actions Between Decisions), 모터가 버틴다.\n" +
                          "사람도 근육 하나하나를 매 순간 의식하지 않는다.",
                solve = () => Set<DecisionRequester>(Root, d => { d.DecisionPeriod = 5; d.TakeActionsBetweenDecisions = true; }) };
            s.Do("Humanoid 에 Add Component > Decision Requester", () => Has<DecisionRequester>(Root)).Insp(Root, typeof(DecisionRequester), "Add Component ▸ Decision Requester");
            ch.steps.Add(s.Do("Decision Period 5 · Take Actions Between Decisions 켬", () => Has<DecisionRequester>(Root) && Get<DecisionRequester>(Root).DecisionPeriod == 5 && Get<DecisionRequester>(Root).TakeActionsBetweenDecisions).Insp(Root, typeof(DecisionRequester), "Decision Period · Take Actions Between Decisions"));

            s = new Step { title = "키보드는 행동만", goal = "몸은 에이전트가 움직이게 하고, 키보드는 행동만 만들게 한다",
                concept = "둘 다 몸을 움직이면 서로 덮어쓴다.\n" +
                          "Drive Body 를 끄면 키보드는 행동 18개만 만들고, Heuristic Only 일 때 에이전트가 그것을 쓴다.\n" +
                          "화면 표시(각도 · 보상)는 그대로 나온다.",
                solve = () => Set<KeyboardControl>(Root, k => k.driveBody = false) };
            ch.steps.Add(s.Do("Keyboard Control 의 Drive Body 체크를 끈다", () => Has<KeyboardControl>(Root) && !Get<KeyboardControl>(Root).driveBody).Insp(Root, typeof(KeyboardControl), "Drive Body"));

            ch.steps.Add(new Step { title = "실습 3-2  서기 보상", blank = "3-2",
                goal = "서 있는 정도를 0~1 점수로 주는 보상 함수를 직접 만든다",
                concept = "StandReward(up, height, standHeight) — 서 있는 정도를 0~1 로.\n" +
                          "  up 은 몸통의 위쪽 방향(길이 1), height 는 골반 높이, standHeight 는 서 있을 때 높이(0.96)\n" +
                          "  똑바르고 제 높이 → 1   쪼그려 절반 높이 → 0.5   누움 → 0   거꾸로 → 0   더 높이 떠도 1을 넘지 않음\n" +
                          "\n" +
                          "넘어지면 판을 끝낸다. 벌점은 없다 — 앞으로 받을 점수가 사라지는 것이 벌이다.\n" +
                          "할인율 γ=0.99 면 매 스텝 1점의 합은 1/(1-γ) = 100. 넘어지면 이 100점이 사라진다.",
                hints = new[] {
                    "두 가지를 본다: '얼마나 똑바른가'와 '얼마나 높은가'. 둘 다 0~1 로 만든 뒤 합친다.\n더하면 '누운 채 높이'도 절반 점수를 받는다.",
                    "① 똑바른 정도 = up 의 y 성분 (위를 보면 1, 누우면 0, 거꾸로면 음수)\n② 높이 비율 = height / standHeight\n③ 둘 다 0~1 로 자른다\n④ 곱한다",
                    "Mathf.Clamp01(up.y) * Mathf.Clamp01(height / standHeight)" },
                skeleton = "return Mathf.Clamp01(___) * Mathf.Clamp01(___);",
                explain = "Clamp01 은 0~1 로 자른다. 곱하면 하나라도 0이면 0점 —\n쪼그려 앉아 똑바로 있기, 기운 채 높이 있기를 동시에 막는다." });

            s = new Step { title = "Heuristic 으로 먼저 돌려 보기", goal = "학습 전에 에러 없이 판이 돌아가는지 확인한다",
                concept = "Heuristic Only = AI 대신 키보드가 행동을 낸다.\n" +
                          "3초마다 밀려서 넘어지면 판이 끝나고 차렷 자세로 다시 선다. 화면 왼쪽 위에 에피소드 수와 보상이 보인다.",
                hint = "관측 관련 에러가 나면 Space Size(48)와 코드의 AddObservation 개수가 같은지 본다." };
            s.Do("Behavior Type 을 Heuristic Only 로 바꾼다", () => IsBehavior(BehaviorType.HeuristicOnly), "바꾸기", () => SetBehavior(BehaviorType.HeuristicOnly)).Insp(Root, typeof(BehaviorParameters), "Behavior Type");
            PlayStep(s).Do("밀려서 넘어지고 판이 새로 시작되는 것을 본다", () => Playing && Has<StandAgent>(Root) && Get<StandAgent>(Root).CompletedEpisodes >= 1).Game("관찰 (왼쪽 위 에피소드 수)");
            StopStep(s);
            ch.steps.Add(s.Do("Behavior Type 을 Default 로 되돌린다", () => !Playing && IsBehavior(BehaviorType.Default), "되돌리기", () => SetBehavior(BehaviorType.Default)).Insp(Root, typeof(BehaviorParameters), "Behavior Type"));

            s = new Step { title = "학습하기", goal = "mlagents-learn 으로 서기를 학습한다",
                concept = "터미널(프로젝트 폴더)에서:\n  run_env.bat\n  mlagents-learn Assets/Humanoid/humanoid_config.yaml --run-id=stand\n" +
                          "'Start training by pressing Play' 가 뜨면 Unity에서 Play.\n\n" +
                          "실측(에이전트 36개): 20만 스텝 77 → 200만 618 → 400만 약 860, 약 15분.  행동 0이면 343.\n" +
                          "처음엔 행동을 크게 흔들어 보기 때문에 '가만히 있기'보다 점수가 낮다. 정상이다.",
                hint = "파이썬이 안 붙으면 Behavior Name(HumanoidStand)과 yaml 키 이름이 같은지 본다." };
            s.Do("학습 설정(yaml)을 열어 HumanoidStand 부분을 읽는다", null, "yaml 열기", () => EditorUtility.OpenWithDefaultApp("Assets/Humanoid/humanoid_config.yaml"));
            s.Do("학습 명령을 복사해 터미널에서 실행한다", null, "명령 복사", () => Copy("mlagents-learn Assets/Humanoid/humanoid_config.yaml --run-id=stand"));
            s.Do("Unity에서 Play → 학습기가 연결된다", () => Playing && Academy.IsInitialized && Academy.Instance.IsCommunicatorOn).Toolbar("▶");
            ch.steps.Add(s.Do("tensorboard --logdir results 로 보상이 오르는 것을 본다"));

            s = new Step { title = "학습된 모델 평가", goal = "학습 결과를 탐험 없이 확인한다",
                concept = "results/stand/HumanoidStand.onnx 를 Assets 아래로 끌어와 Behavior Parameters 의 Model 칸에 넣는다.\n" +
                          "직접 학습하지 않았으면 미리 학습해 둔 Assets/Humanoid/results/HumanoidStand.onnx 를 써도 된다.\n" +
                          "★ 관측 · 행동 수가 다른 모델(예: 몸을 바꾸기 전에 학습한 것)을 넣으면 Unity 가 통째로 꺼진다.\n\n" +
                          "실측(30판): 행동 0  평균 343 · 끝까지 버팀 1/30   →   400만 스텝 학습 후  평균 927 · 28/30\n" +
                          "(200만 스텝에서 멈추면 692 · 15/30. 행동이 18개라 오래 배워야 한다)",
                done = "3장 끝. 이제 걷는다." };
            s.Do("Model 칸에 학습된 .onnx 를 넣는다", () => Brain != null && Brain.Model != null).Insp(Root, typeof(BehaviorParameters), "Model ← .onnx 끌어다 놓기");
            s.Do("Behavior Type 을 Inference Only 로 바꾼다", () => IsBehavior(BehaviorType.InferenceOnly), "바꾸기", () => SetBehavior(BehaviorType.InferenceOnly)).Insp(Root, typeof(BehaviorParameters), "Behavior Type");
            PlayStep(s);
            ch.steps.Add(s.Do("P 로 직접 밀어 본다 — 대부분 버틴다").Game("P"));

            // ═══════════════════════════════════════════════════════════
            // 걷기 보상
            // ═══════════════════════════════════════════════════════════
            ch = new Chapter { title = "4. 걷기 보상", doc = "04-걷기-보상.md",
                goals = new[] { "목표 상자와 걷기 에이전트를 직접 붙인다", "몸이 기울어도 변하지 않는 '보는 방향'을 계산한다", "속도 × 방향 보상으로 스스로 걷는 정책을 학습한다 (최종 과제)" } };
            list.Add(ch);

            ch.steps.Add(new Step { title = "실습 4-1  좌우 회전만", blank = "4-1",
                start = "forward.y = 0f;     // 앞뒤 기울기를 버린다 (주어진 코드)\nreturn Quaternion.identity;",  mode = "일부 코드 제공",
                goal = "주어진 첫 줄에서 출발해, 바라보는 방향의 좌우 회전만 남기는 함수를 완성한다",
                concept = "HeadingRotation(forward) — 바라보는 방향에서 좌우 회전만 남긴 회전을 돌려준다.\n" +
                          "목표 방향을 '내가 보는 방향' 기준으로 관측할 때 쓴다. 걷는 몸은 앞뒤로 기우는데,\n" +
                          "기운 정면을 그대로 쓰면 '땅을 보는 방향'이 된다.\n" +
                          "  정면 → 회전 없음   오른쪽(+x) → 좌우 90도   숙인 채 오른쪽 → 좌우 90도만   바로 아래 → 회전 없음\n" +
                          "\n" +
                          "첫 줄(y 버리기)은 주어져 있다. 나머지를 작성한다.",
                hints = new[] {
                    "y 를 버린 forward 는 수평 방향이다. 그 방향을 '바라보는' 회전을 만들면 된다.\n단, 바로 위/아래를 보면 y 를 버린 뒤 길이가 0이라 방향이 없다.",
                    "① (주어짐) y = 0\n② 길이가 거의 0이면 Quaternion.identity\n③ 아니면 그 방향을 보는 회전을 돌려준다",
                    "길이 검사:    forward.sqrMagnitude < 1e-6f\n보는 회전:    Quaternion.LookRotation(forward.normalized)" },
                skeleton = "forward.y = ___;\nif (forward.sqrMagnitude < 1e-6f) return Quaternion.identity;\nreturn Quaternion.LookRotation(___);",
                explain = "LookRotation(방향)은 +z 가 그 방향을 보게 하는 회전이다. 길이 0 벡터를 넣으면 경고와 함께 엉뚱한 값이 나오므로 먼저 검사한다.\nsqrMagnitude 는 제곱근을 구하지 않아 magnitude 보다 빠르다." });

            s = new Step { title = "목표 상자", goal = "걸어갈 목표를 학습 영역에 놓는다",
                concept = "에이전트가 판마다 이 상자를 몸 주변 4~8m 무작위 자리로 옮긴다. 닿으면 +1 하고 다음 자리로.\n" +
                          "콜라이더는 지운다. 닿음은 거리로 판정하므로, 콜라이더가 있으면 걸려 넘어지기만 한다.",
                solve = () => Replace(Target, () => { var t = GameObject.CreatePrimitive(PrimitiveType.Cube); t.name = "Target"; t.transform.SetParent(T(Area), false); t.transform.localPosition = new Vector3(0f, 0.25f, 6f); t.transform.localScale = Vector3.one * 0.5f; Object.DestroyImmediate(t.GetComponent<Collider>()); t.GetComponent<Renderer>().sharedMaterial = HumanoidBuild.Mat("M_WalkTarget", new Color(0.95f, 0.3f, 0.3f)); return t; }) };
            s.Do("HumanoidArea 아래에 3D Object > Cube, 이름 Target", () => ChildOf(Target, Area)).Hier(Area, "우클릭 ▸ 3D Object ▸ Cube ▸ 이름 Target");
            s.Do("Position (0, 0.25, 6) · Scale (0.5, 0.5, 0.5)", () => LocalPos(Target, new Vector3(0f, 0.25f, 6f)) && Near(T(Target).localScale, Vector3.one * 0.5f)).Insp(Target, typeof(Transform), "Position · Scale");
            ch.steps.Add(s.Do("Box Collider 를 지운다 (⋮ > Remove Component)", () => T(Target) != null && T(Target).GetComponent<Collider>() == null).Insp(Target, typeof(BoxCollider), "⋮ ▸ Remove Component"));

            s = new Step { title = "걷기 에이전트로 바꾸기", goal = "Stand Agent 를 Walk Agent 로 바꾼다",
                concept = "Walk Agent 는 Stand Agent 를 상속한다. 관측 48 · 행동 18 · 넘어지면 끝내기는 그대로이고\n" +
                          "관측 3개(목표 방향 2 + 목표 속도 1)와 보상만 바뀐다. 한 번에 하나씩.\n\n" +
                          "Decision Requester 가 에이전트를 필요로 해서, 새 것을 먼저 붙이고 옛 것을 지운다.\n" +
                          "걷기를 배우는 동안은 밀지 않는다 (Push Interval 0).\n\n" +
                          "판이 끝나는 때: 넘어짐 · 목표마다 주는 제한시간(목표 속도로 가는 시간 × 2 + 5초) 안에 못 닿음.\n" +
                          "닿으면 다음 목표와 새 제한시간을 받으니, 잘 걸으면 판이 계속된다. Max Step 5000(100초)은 그때의 안전 상한이다.",
                solve = () => { var go = G(Root); if (!Has<WalkAgent>(Root)) Undo.AddComponent<WalkAgent>(go); foreach (var a in go.GetComponents<StandAgent>()) if (!(a is WalkAgent)) Undo.DestroyObjectImmediate(a); Set<WalkAgent>(Root, w => { w.target = T(Target); w.targetSpeed = 1f; w.pushInterval = 0; w.MaxStep = 5000; }); } };
            s.Do("Humanoid 에 Add Component > Walk Agent", () => Has<WalkAgent>(Root)).Insp(Root, typeof(WalkAgent), "Add Component ▸ Walk Agent");
            s.Do("Stand Agent 를 지운다 (⋮ > Remove Component)", () => T(Root) != null && T(Root).GetComponents<StandAgent>().Length == 1).Insp(Root, typeof(StandAgent), "Stand Agent 의 ⋮ ▸ Remove Component");
            s.Do("Walk Agent 의 Target 칸에 Target 을 끌어다 놓는다", () => Has<WalkAgent>(Root) && Get<WalkAgent>(Root).target == T(Target)).Insp(Root, typeof(WalkAgent), "Target ← Hierarchy 의 Target 을 끌어다 놓기");
            ch.steps.Add(s.Do("Target Speed 1 · Push Interval 0 · Max Step 5000", () => Has<WalkAgent>(Root) && Near(Get<WalkAgent>(Root).targetSpeed, 1f) && Get<WalkAgent>(Root).pushInterval == 0 && Get<WalkAgent>(Root).MaxStep == 5000).Insp(Root, typeof(WalkAgent), "Target Speed · Push Interval · Max Step"));

            s = new Step { title = "두뇌 설정", goal = "걷기용 이름과 관측 수로 바꾼다",
                concept = "Behavior Name 을 바꾸면 서기와 다른 두뇌로 학습된다 → HumanoidWalk\n" +
                          "관측 51 = 서기 48 + 목표 방향 2 + 목표 속도 1. 행동은 그대로 18.",
                solve = () => Set<BehaviorParameters>(Root, b => { b.BehaviorName = "HumanoidWalk"; b.BrainParameters.VectorObservationSize = 51; }) };
            s.Do("Behavior Name: HumanoidWalk", () => Brain != null && Brain.BehaviorName == "HumanoidWalk").Insp(Root, typeof(BehaviorParameters), "Behavior Name");
            ch.steps.Add(s.Do("Space Size: 51", () => Brain != null && Brain.BrainParameters.VectorObservationSize == 51).Insp(Root, typeof(BehaviorParameters), "Vector Observation ▸ Space Size"));

            ch.steps.Add(new Step { title = "실습 4-2  속도 보상", blank = "4-2",
                start = "velocity.y = 0f;     // 떨어지는 속도는 걷기가 아니다 (주어진 코드)\nreturn 0f;",  mode = "일부 코드 제공",
                goal = "주어진 첫 줄에서 출발해, 목표 속도에 가까울수록 높은 점수를 주는 함수를 완성한다",
                concept = "SpeedReward(velocity, direction, targetSpeed) — 목표 속도로 목표 쪽으로 가면 1, 멈추거나 반대로 가면 0.\n" +
                          "  목표 속도(벡터) = direction × targetSpeed,  d = 실제 속도와의 차이 크기 (0~targetSpeed 로 자른다)\n" +
                          "  점수 = (1 − (d/targetSpeed)²)²\n" +
                          "  그대로 → 1   멈춤 → 0   절반 속도 → 0.5625   반대로 달림 → 0\n" +
                          "\n" +
                          "몸 전체 속도는 1장 무게중심 함수에 속도를 넣어 구한다. 첫 줄은 주어져 있다.",
                hints = new[] {
                    "'얼마나 틀렸나'를 먼저 구한다. 실제 속도와 목표 속도(벡터)의 차이의 크기가 d 다.\nd 가 0이면 1점, targetSpeed 이상이면 0점.",
                    "① (주어짐) y 버리기\n② 목표 속도 벡터 = direction * targetSpeed\n③ d = 두 벡터 차이의 크기, 0~targetSpeed 로 자르기\n④ r = 1 − (d/targetSpeed)²\n⑤ r 을 제곱해서 돌려준다",
                    "float d = Mathf.Clamp((velocity - direction * targetSpeed).magnitude, 0f, targetSpeed);\nfloat r = 1f - (d / targetSpeed) * (d / targetSpeed);\nreturn r * r;" },
                skeleton = "velocity.y = 0f;\nfloat d = Mathf.Clamp(___, 0f, targetSpeed);\nfloat r = 1f - (d / targetSpeed) * (d / targetSpeed);\nreturn ___;",
                explain = "d 를 targetSpeed 로 자르기 때문에 아무리 틀려도 0 아래로 내려가지 않는다 (반대로 달림 케이스).\n제곱을 두 번 해서 목표 근처에서는 점수가 완만하고 멀어질수록 빨리 떨어진다." });

            ch.steps.Add(new Step { title = "실습 4-3  방향 보상", blank = "4-3",
                goal = "몸이 목표 쪽을 볼수록 높은 점수를 주는 함수를 직접 만든다",
                concept = "FacingReward(forward, direction) — 몸이 목표 쪽을 보면 1, 옆이면 0.5, 반대면 0.\n" +
                          "  고개를 숙인 것(위아래 성분)은 따지지 않는다\n" +
                          "\n" +
                          "걷기 보상 = 속도(4-2) × 방향(4-3). 곱하면 둘 다 만족해야 점수가 난다.\n" +
                          "더하면 '목표를 보고 서 있기'나 '뒷걸음질'도 절반을 받아 AI가 쉬운 쪽만 한다.",
                hints = new[] {
                    "두 방향이 얼마나 같은지는 내적(Vector3.Dot)으로 잰다.\n길이 1 벡터끼리 내적은 같으면 1, 수직이면 0, 반대면 -1.",
                    "① 두 벡터의 y 를 버린다\n② 길이를 1로 만든다\n③ 내적한다 (-1~1)\n④ 0~1 로 옮긴다",
                    "Vector3.Dot(forward.normalized, direction.normalized) 의 결과 -1~1 을 (값 + 1) * 0.5 로 0~1 로." },
                skeleton = "forward.y = 0f;\ndirection.y = 0f;\nreturn (Vector3.Dot(___, ___) + 1f) * 0.5f;",
                explain = "y 를 버린 뒤 normalized 해야 숙인 몸통도 길이 1이 된다 (경계 케이스 '고개 숙임').\n(값 + 1) / 2 는 -1~1 을 0~1 로 옮기는 흔한 방법이다." });

            s = new Step { title = "서 있으면 0점", goal = "학습 전 상태를 확인한다",
                concept = "Heuristic Only 로 Play하면 키보드로 아무리 움직여도 보상이 거의 0이다.\n" +
                          "사람이 손으로 18축을 맞춰 걷게 만들 수는 없다. 그래서 학습한다." };
            s.Do("Behavior Type 을 Heuristic Only 로 바꾼다", () => IsBehavior(BehaviorType.HeuristicOnly), "바꾸기", () => SetBehavior(BehaviorType.HeuristicOnly)).Insp(Root, typeof(BehaviorParameters), "Behavior Type");
            PlayStep(s).Do("화면의 보상이 거의 0인 것을 본다", () => Playing && Has<WalkAgent>(Root) && Get<WalkAgent>(Root).StepCount > 100).Game("관찰 (왼쪽 위 보상)");
            StopStep(s);
            ch.steps.Add(s.Do("Behavior Type 을 Default 로 되돌린다", () => !Playing && IsBehavior(BehaviorType.Default), "되돌리기", () => SetBehavior(BehaviorType.Default)).Insp(Root, typeof(BehaviorParameters), "Behavior Type"));

            s = new Step { title = "걷기 학습", goal = "스스로 걷는 정책을 학습한다",
                concept = "mlagents-learn Assets/Humanoid/humanoid_config.yaml --run-id=walk\n" +
                          "걷기는 오래 걸린다. 5장의 병렬 학습(영역 9개 · 빌드 4개)을 먼저 하면 그만큼 빠르다.\n\n" +
                          "실측(에이전트 36개): 100만 33 → 300만 381(급상승) → 1000만 약 3050, 약 52분. 목표를 맞힐수록 판이 길어져 보상도 계속 오른다.\n" +
                          "평가: 목표 1.0 m/s · 10판 · 7판이 100초 상한까지 버팀 · 판당 목표 11.7개 · 넘어짐 3.",
                done = "4장 끝 — 최종 과제 완성. 5장은 더 빨리 · 더 넓게 · 제대로 재는 법이다." };
            s.Do("학습 명령을 복사해 실행한다", null, "명령 복사", () => Copy("mlagents-learn Assets/Humanoid/humanoid_config.yaml --run-id=walk"));
            ch.steps.Add(s.Do("학습된 모델로 Play → 빨간 상자를 향해 걷고, 닿으면 다음 상자로 방향을 튼다"));

            // ═══════════════════════════════════════════════════════════
            // 학습과 평가
            // ═══════════════════════════════════════════════════════════
            ch = new Chapter { title = "5. 학습과 평가", doc = "05-학습과-평가.md",
                goals = new[] { "코드 수정 없이 병렬 학습한다", "커리큘럼으로 목표 속도를 올린다", "보상 말고 진짜 목표를 지표로 잰다" } };
            list.Add(ch);

            ch.steps.Add(new Step { title = "실습 5-1  SpeedStats 클래스", blank = "5-1",
                start = "public int Count => 0;\npublic float Average => 0f;\n\npublic void Add(float value)\n{\n}\n\npublic void Clear()\n{\n}",  mode = "처음부터 (클래스 설계)",
                goal = "한 판의 속도를 기록하는 작은 클래스를 직접 설계하고 구현한다",
                concept = "SpeedStats — Walk Agent 가 매 스텝 Add 하고, 판이 끝나면 Average 를 TensorBoard 의 Walk/Speed 에 남긴 뒤 Clear 한다.\n" +
                          "  Add(value)   값을 하나 더한다\n" +
                          "  Count        지금까지 더한 개수\n" +
                          "  Average      지금까지의 평균 (아무것도 없으면 0)\n" +
                          "  Clear()      처음 상태로\n" +
                          "\n" +
                          "값을 전부 저장하지 않는다 (한 판 최대 5000 스텝 × 에이전트 36개).\n" +
                          "멤버의 이름과 모양은 Walk Agent 가 쓰고 있어 주어져 있다. 무엇을 기억할지(필드)와 동작(메서드 속)을 직접 설계한다.",
                hints = new[] {
                    "클래스 = 상태(기억할 값) + 동작(메서드). 평균을 내려면 무엇을 기억해야 할까?\n값 전부가 아니라 '개수'와 '지금까지의 평균' 두 개면 충분하다.",
                    "① Count · Average 가 값을 저장하는 속성이 되게 바꾼다 (예: public int Count { get; private set; })\n② Add: 개수를 1 늘리고 평균을 갱신한다\n③ Clear: 둘 다 0 으로",
                    "누적 평균:  새 평균 = 옛 평균 + (새 값 − 옛 평균) / 새 개수\n개수를 먼저 늘린 뒤 나눈다 (첫 값에서 0으로 나누지 않게).\n바깥에서 값을 바꾸지 못하게 set 은 private." },
                skeleton = "public int Count { get; private set; }\npublic float Average { get; private set; }\n\npublic void Add(float value)\n{\n    Count___;\n    Average += ___;\n}\n\npublic void Clear()\n{\n    ___\n}",
                explain = "{ get; private set; } 은 읽기는 누구나, 쓰기는 이 클래스만 하는 속성이다.\n누적 평균은 합계를 따로 들고 있지 않아 값이 아무리 많아도 메모리가 늘지 않는다.\nCount 를 먼저 늘려야 첫 값에서 0으로 나누지 않는다. Clear 에서 Count 를 안 지우면 다음 판 평균이 틀어진다." });

            ch.steps.Add(new Step { title = "실습 5-2  속도 오차 기능", blank = "5-2",
                start = "return Mathf.Abs(Average - targetSpeed) / targetSpeed;",  mode = "기능 추가",
                goal = "SpeedStats 의 오차 기능에 '목표 0' 처리를 덧붙인다",
                concept = "SpeedStats.Error(targetSpeed) — |평균 − 목표| / 목표. 0이면 완벽, 1이면 100% 틀림 (두 배 빨라도 오차).\n" +
                          "시작 코드는 식만 있다. 목표 속도가 0 이하면 0으로 나누게 된다 → 그때는 0을 돌려주도록 덧붙인다.\n" +
                          "5-1 의 Average 를 쓰므로 5-1 을 먼저 통과해야 한다.\n" +
                          "\n" +
                          "보상이 올라도 실제로는 이상하게 걸을 수 있다. 진짜 하고 싶은 것을 따로 잰다:\n" +
                          "  Walk/Speed · Walk/SpeedError · Walk/FallRate · Walk/Targets",
                hints = new[] {
                    "있는 식은 맞다. 목표 0 이 들어오면 무한대가 나온다. 먼저 테스트로 확인한다.",
                    "① 맨 앞에서 targetSpeed 가 0 이하인지 검사\n② 그렇다면 바로 0 을 돌려준다\n③ 아니면 원래 식",
                    "if (targetSpeed <= 0f) return 0f;   를 원래 줄 앞에 넣는다." },
                skeleton = "if (targetSpeed <= ___) return ___;\nreturn Mathf.Abs(Average - targetSpeed) / targetSpeed;",
                explain = "'이른 반환(early return)'으로 예외 상황을 먼저 걸러 내면 본래 식은 손대지 않아도 된다." });

            s = new Step { title = "병렬 학습", goal = "학습 영역을 9개로 복제한다",
                concept = "Behavior Name 이 같은 에이전트는 한 두뇌로 묶인다. 코드는 그대로, 경험은 9배.\n" +
                          "1장에서 모든 것을 HumanoidArea 하나에 담은 이유가 이것이다.\n" +
                          "직접 하려면 HumanoidArea 를 Ctrl+D 로 복제해 45m씩 띄운다(바닥이 40m). 8번 반복이라 버튼으로.\n" +
                          "빌드해서 --num-envs=4 로 띄우면 9 × 4 = 에이전트 36개.",
                solve = () => EditorApplication.ExecuteMenuItem("Humanoid/5. 학습 영역 9개로 복제") };
            ch.steps.Add(s.Do("학습 영역을 9개로 만든다", () => GameObject.Find(Area + " 8") != null, "9개로 복제", () => EditorApplication.ExecuteMenuItem("Humanoid/5. 학습 영역 9개로 복제")).Hier(Area, "오른쪽 [9개로 복제] 버튼"));

            s = new Step { title = "커리큘럼으로 학습", goal = "목표 속도를 0.5 → 1.0 → 1.5 로 올리며 학습한다",
                concept = "yaml 맨 아래 environment_parameters 가 커리큘럼이다. 보상이 threshold 를 넘으면 다음 단계로.\n\n" +
                          "1.0 으로만 학습한 모델을 평가하면(실측): 0.5 → 9/10 넘어짐 · 1.0 → 3/10 · 1.5 → 8/10.\n" +
                          "배운 속도에서만 잘한다. 여러 속도를 쓰려면 여러 속도로 배워야 한다.\n\n" +
                          "File > Build Profiles 로 빌드한 뒤:\n  mlagents-learn Assets/Humanoid/humanoid_config.yaml --run-id=walk_curriculum --env=<빌드>.exe --no-graphics --num-envs=4",
                hint = "Lesson Number 가 안 오르면 threshold 가 보상보다 높다. 'worker in use' 오류는 --base-port=5020 처럼 바꾼다." };
            s.Do("yaml 을 열어 environment_parameters 를 읽는다", null, "yaml 열기", () => EditorUtility.OpenWithDefaultApp("Assets/Humanoid/humanoid_config.yaml"));
            s.Do("빌드한다 (씬 하나만 넣는다)");
            s.Do("학습 명령을 복사해 실행한다", null, "명령 복사", () => Copy("mlagents-learn Assets/Humanoid/humanoid_config.yaml --run-id=walk_curriculum --env=Build/Humanoid.exe --no-graphics --num-envs=4"));
            ch.steps.Add(s.Do("TensorBoard 에서 Environment/Lesson Number 와 Walk/… 지표를 본다"));

            s = new Step { title = "평가하고 정리하기", goal = "학습된 모델을 목표 속도별로 평가하고 영역을 1개로 되돌린다",
                concept = "학습 중 점수는 탐험 때문에 실력보다 낮다. 평가는 Inference Only 로 따로 한다.\n" +
                          "Walk Agent 의 Target Speed 를 0.5 / 1.0 / 1.5 로 바꿔 가며 화면의 평균 속도 · 도달 수를 적는다.",
                done = "휴머노이드 트랙 끝! 스스로 걷는 정책을 만들고, 평가까지 했다.",
                solve = () => EditorApplication.ExecuteMenuItem("Humanoid/학습 영역 1개로") };
            s.Do("목표 속도별 평가표를 채운다 (문서 05의 표)");
            ch.steps.Add(s.Do("학습 영역을 1개로 되돌린다", () => GameObject.Find(Area + " 1") == null, "1개로", () => EditorApplication.ExecuteMenuItem("Humanoid/학습 영역 1개로")).Hier(Area, "오른쪽 [1개로] 버튼"));

            return list;
        }
    }
}
