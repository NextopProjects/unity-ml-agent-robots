using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace HumanoidEditor
{
    // 휴머노이드 튜토리얼 창.  메뉴 > Humanoid > 튜토리얼 열기
    //
    // 위에서 아래로 읽는다:  🎯 목표 → 설명 → ▶ 지금 할 일 → ① ② ③ 할 일 → (끝나면) 다음 단계
    // 할 일은 위에서부터 하나씩 확인한다. 조건이 있는 할 일은 저절로 ✔ 가 되고, 없는 것은 [했어요]로 넘긴다.
    // 진행 상황은 EditorPrefs 에 저장되어 Play·컴파일·재시작 뒤에도 이어진다.
    internal class TutorialWindow : EditorWindow
    {
        static string Key => TutorialChapters.PrefsPrefix;     // 프로젝트마다 따로
        const string DocFolder = "Assets/Humanoid/Docs/";

        int _chapter;
        Vector2 _scroll;
        bool _showHint, _showHelp, _showCases;
        List<BlankCheck.Result> _results;     // 마지막 테스트 결과 (화면 표시용)
        bool _edited, _untouched;              // 시작 코드를 고쳤는가 (파일을 계속 읽지 않도록 저장해 둔다)
        double _nextFileCheck;

        [MenuItem("Humanoid/튜토리얼 열기 %#h", false, 0)]
        static void Open() => GetWindow<TutorialWindow>("휴머노이드 튜토리얼").minSize = new Vector2(560, 480);

        // 처음 프로젝트를 열면 한 번 자동으로 띄운다.
        [InitializeOnLoadMethod]
        static void OpenOnce()
        {
            if (EditorPrefs.GetBool(Key + "opened")) return;
            EditorPrefs.SetBool(Key + "opened", true);
            EditorApplication.delayCall += Open;
        }

        void OnEnable() => _chapter = EditorPrefs.GetInt(Key + "chapter", 0);
        void OnDisable() => TutorialGuide.Set(null);     // 창을 닫으면 하이라이트도 끈다

        // 코드 단계에서 고칠 스크립트 (Project 창에 노랗게 표시)
        static Task CodeFile(Step step) => new Task { window = "Project", path = BlankTool.PathOf(step.blank), where = "Project ▸ " + BlankTool.PathOf(step.blank) };

        // ── 진행 상황 ───────────────────────────────────────────────

        static int StepIndex(int c) => EditorPrefs.GetInt(Key + c + ".step", 0);
        static void SetStep(int c, int s) => EditorPrefs.SetInt(Key + c + ".step", s);
        static bool Flag(string name) => EditorPrefs.GetBool(Key + name);
        static void SetFlag(string name, bool v) => EditorPrefs.SetBool(Key + name, v);

        static string TaskKey(int c, int s, int t) => c + "." + s + "." + t;
        static string BlankKey(Step step, string what) => "blank." + step.blank + "." + what;

        static bool StepDone(int c, int s)
        {
            var step = TutorialChapters.All[c].steps[s];
            if (step.blank != null) return Flag(BlankKey(step, "made")) && Flag(BlankKey(step, "pass"));
            for (int t = 0; t < step.tasks.Count; t++) if (!Flag(TaskKey(c, s, t))) return false;
            return true;
        }

        static bool ChapterDone(int c)
        {
            for (int s = 0; s < TutorialChapters.All[c].steps.Count; s++) if (!StepDone(c, s)) return false;
            return true;
        }

        // 10번/초. 지금 할 일의 조건만 확인한다 (앞 일을 끝내야 다음 일을 본다).
        void OnInspectorUpdate()
        {
            var step = TutorialChapters.All[_chapter].steps[StepIndex(_chapter)];
            if (step.blank != null)
            {
                if (EditorApplication.timeSinceStartup > _nextFileCheck)
                {
                    _edited = !BlankTool.Untouched(step.blank, step.start);
                    _nextFileCheck = EditorApplication.timeSinceStartup + 1.0;
                }
                // 코드를 작성할 차례면 Project 창에서 그 스크립트를 표시한다
                TutorialGuide.Set(Flag(BlankKey(step, "made")) && !Flag(BlankKey(step, "pass")) ? CodeFile(step) : null);
            }
            else
            {
                int t = FirstUndone(step);
                if (t >= 0 && step.tasks[t].check != null && SafeCheck(step.tasks[t]))
                    SetFlag(TaskKey(_chapter, StepIndex(_chapter), t), true);
                TutorialGuide.Set(t >= 0 ? step.tasks[t] : null);     // 지금 할 일의 대상을 하이라이트
            }
            Repaint();
        }

        int FirstUndone(Step step)
        {
            for (int t = 0; t < step.tasks.Count; t++)
                if (!Flag(TaskKey(_chapter, StepIndex(_chapter), t))) return t;
            return -1;
        }

        static bool SafeCheck(Task task)
        {
            try { return task.check(); }
            catch { return false; }     // 씬이 아직 준비되지 않았으면 그냥 '아직'
        }

        // ── 화면 ───────────────────────────────────────────────────

        void OnGUI()
        {
            var chapters = TutorialChapters.All;
            var ch = chapters[_chapter];
            int s = StepIndex(_chapter);
            var step = ch.steps[s];

            // 장 고르기
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            for (int c = 0; c < chapters.Count; c++)
            {
                string label = (ChapterDone(c) ? "✔ " : "") + chapters[c].title.Split(' ')[0] + " " + chapters[c].title.Split(' ')[1];
                if (GUILayout.Toggle(c == _chapter, label, EditorStyles.toolbarButton) && c != _chapter) Select(c);
            }
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("📖 문서", EditorStyles.toolbarButton)) EditorUtility.OpenWithDefaultApp(DocFolder + ch.doc);
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();

            // 왼쪽: 단계 목록
            EditorGUILayout.BeginVertical(GUILayout.Width(170));
            GUILayout.Label(ch.title, EditorStyles.boldLabel);
            for (int i = 0; i < ch.steps.Count; i++)
            {
                string mark = StepDone(_chapter, i) ? "✔" : i == s ? "▶" : "○";
                var style = new GUIStyle(EditorStyles.label) { fontStyle = i == s ? FontStyle.Bold : FontStyle.Normal, wordWrap = true };
                if (GUILayout.Button(mark + " " + ch.steps[i].title, style)) GoTo(i);
            }
            GUILayout.Space(8);
            GUILayout.Label("이 장의 목표", EditorStyles.miniBoldLabel);
            foreach (var g in ch.goals) GUILayout.Label("• " + g, EditorStyles.wordWrappedMiniLabel);
            EditorGUILayout.EndVertical();

            // 오른쪽: 지금 단계
            _scroll = EditorGUILayout.BeginScrollView(_scroll);
            DrawWarnings(step);
            GUILayout.Label("단계 " + (s + 1) + " / " + ch.steps.Count + "  ·  " + step.title, EditorStyles.largeLabel);
            EditorGUILayout.HelpBox("🎯 " + step.goal, MessageType.None);
            if (!string.IsNullOrEmpty(step.concept)) GUILayout.Label(step.concept, new GUIStyle(EditorStyles.helpBox) { wordWrap = true, fontSize = 12, richText = false });
            GUILayout.Space(6);

            if (step.blank != null) DrawBlank(step); else DrawTasks(step);

            if (!string.IsNullOrEmpty(step.hint) && (_showHint = EditorGUILayout.Foldout(_showHint, "💡 힌트", true)))
                EditorGUILayout.HelpBox(step.hint, MessageType.None);

            // 막혔을 때: 이 단계를 정답대로 만들어 준다. 그 뒤 할 일은 저절로 ✔ 가 된다.
            if (step.blank == null && step.solve != null && (_showHelp = EditorGUILayout.Foldout(_showHelp, "막혔을 때", true)))
                if (GUILayout.Button("대신 해주기 — 이 단계를 정답대로 만든다 (Ctrl+Z 로 되돌릴 수 있다)"))
                {
                    step.solve();
                    GUIUtility.ExitGUI();
                }

            GUILayout.Space(10);
            bool done = StepDone(_chapter, s);
            if (done && !string.IsNullOrEmpty(step.done)) EditorGUILayout.HelpBox("✔ 통과! " + step.done, MessageType.Info);

            EditorGUILayout.BeginHorizontal();
            GUI.enabled = s > 0;
            if (GUILayout.Button("◀ 이전 단계", GUILayout.Height(26))) GoTo(s - 1);
            GUI.enabled = done;
            bool last = s == ch.steps.Count - 1;
            if (GUILayout.Button(last ? (_chapter < chapters.Count - 1 ? "다음 장 ▶" : "끝") : "다음 단계 ▶", GUILayout.Height(26)))
            {
                if (!last) GoTo(s + 1);
                else if (_chapter < chapters.Count - 1) Select(_chapter + 1);
            }
            GUI.enabled = true;
            EditorGUILayout.EndHorizontal();

            if (GUILayout.Button("이 단계 다시 하기", EditorStyles.miniButton)) ResetStep(s);
            if (Teacher) DrawTeacher(s);
            EditorGUILayout.EndScrollView();
            EditorGUILayout.EndHorizontal();
        }

        void DrawWarnings(Step step)
        {
            if (EditorUtility.scriptCompilationFailed)
                EditorGUILayout.HelpBox("스크립트에 오류가 있습니다. Console 의 첫 번째 빨간 줄부터 고치세요.", MessageType.Error);
            else if (EditorApplication.isCompiling)
                EditorGUILayout.HelpBox("컴파일 중입니다… 잠시 기다리세요.", MessageType.Info);
            if (step.blank != null && EditorApplication.isPlaying)
                EditorGUILayout.HelpBox("Play 중입니다. 코드를 고치기 전에 ⏹ 로 멈추세요.", MessageType.Warning);
        }

        // 일반 단계: 할 일 목록
        void DrawTasks(Step step)
        {
            int current = FirstUndone(step);
            for (int t = 0; t < step.tasks.Count; t++)
            {
                var task = step.tasks[t];
                bool isDone = Flag(TaskKey(_chapter, StepIndex(_chapter), t));
                bool isNow = t == current;

                EditorGUILayout.BeginHorizontal(isNow ? "SelectionRect" : GUIStyle.none);
                GUILayout.Label(isDone ? "✔" : isNow ? "▶" : (t + 1) + ".", GUILayout.Width(22));
                GUILayout.Label(task.text, new GUIStyle(EditorStyles.label) { wordWrap = true, fontStyle = isNow ? FontStyle.Bold : FontStyle.Normal });
                if (isNow)
                {
                    if (task.button != null && GUILayout.Button(task.button, GUILayout.Width(110))) { task.action(); GUIUtility.ExitGUI(); }
                    if (task.check == null && GUILayout.Button("했어요", GUILayout.Width(60)))
                        SetFlag(TaskKey(_chapter, StepIndex(_chapter), t), true);
                }
                EditorGUILayout.EndHorizontal();
                if (isNow && task.where != null) DrawWhere(task);
            }
        }

        // 코드 실습 단계: 구현 시작 → 직접 작성 → 테스트. 막히면 힌트를 한 단계씩 연다.
        static readonly string[] HintNames = { "방향 · 핵심 개념", "구조와 처리 순서", "핵심 로직", "기본 코드 + 빈칸", "완성 코드 + 해설" };

        void DrawBlank(Step step)
        {
            string id = step.blank;
            if (!Flag(BlankKey(step, "made")) && BlankTool.Started(id)) SetFlag(BlankKey(step, "made"), true);     // 튜토리얼을 시작할 때 이미 시작 코드로 바뀌어 있다
            bool made = Flag(BlankKey(step, "made"));
            bool pass = Flag(BlankKey(step, "pass"));

            // 실습 형태와 요구사항: 무엇을 만들어야 통과인지 먼저 보여 준다
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            GUILayout.Label("🛠 " + (step.mode ?? "처음부터 직접 구현"), EditorStyles.boldLabel);
            GUILayout.Label("테스트가 확인하는 요구사항", EditorStyles.miniBoldLabel);
            foreach (var req in BlankCheck.Requirements(id))
            {
                var mine = _results?.FindAll(r => r.req == req);
                string mark = mine == null || mine.Count == 0 ? "•" : mine.TrueForAll(r => r.pass) ? "✔" : "✘";
                GUILayout.Label(mark + " " + req, EditorStyles.wordWrappedLabel);
            }
            EditorGUILayout.EndVertical();

            BlankRow(made, !made, "① 구현을 시작한다 — 실습 자리가 시작 코드로 바뀐다 (정답은 따로 보관된다)", "구현 시작", () =>
            {
                BlankTool.Make(id, step.start);
                SetFlag(BlankKey(step, "made"), true);
                _edited = false;
                _results = null;
            });
            BlankRow(made && _edited, made && !_edited, "② " + System.IO.Path.GetFileName(BlankTool.PathOf(id)) + " 를 열어 직접 작성하고 저장한다", "📄 파일 열기", () =>
                AssetDatabase.OpenAsset(AssetDatabase.LoadAssetAtPath<MonoScript>(BlankTool.PathOf(id)), BlankTool.Line(id)));
            if (made && !pass) DrawWhere(CodeFile(step));

            bool broken = EditorUtility.scriptCompilationFailed;
            GUI.enabled = made && !EditorApplication.isCompiling && !broken;
            BlankRow(pass, made && _edited && !pass, "③ 테스트를 실행한다 — 요구사항을 모두 만족하면 통과", "▶ 테스트 실행", () =>
            {
                _results = BlankCheck.Grade(id);
                _untouched = BlankTool.Untouched(id, step.start);
                SetFlag(BlankKey(step, "pass"), _results.TrueForAll(r => r.pass));
            });
            GUI.enabled = true;
            if (made && broken)
                EditorGUILayout.HelpBox("컴파일 오류가 있어 테스트할 수 없다 (고치기 전의 코드로 채점된다).\n" +
                                        "Console 의 첫 빨간 줄에 파일 이름과 (줄, 칸)이 있다. 그 줄의 철자 · 세미콜론 · 괄호 짝을 본다.\n" +
                                        "힌트 4단계 코드를 넣었다면 ___ 를 모두 채워야 컴파일된다.", MessageType.Error);

            if (_results != null) DrawResults();
            DrawHints(step, made);

            if (made && GUILayout.Button("처음부터 다시: 시작 코드로 되돌리기", EditorStyles.miniButton)
                && EditorUtility.DisplayDialog("시작 코드로 되돌리기", "지금 작성한 코드가 지워지고 시작 코드로 바뀝니다.", "되돌리기", "취소"))
            {
                BlankTool.Make(id, step.start);
                _results = null;
                GUIUtility.ExitGUI();
            }
        }

        // 테스트 결과: 요구사항별로 묶어, 실패한 것은 원인 · 수정 방향 · 실패한 입력을 보여 준다
        void DrawResults()
        {
            var reqs = _results.Select(r => r.req).Distinct().ToList();
            int okReq = reqs.Count(q => _results.Where(r => r.req == q).All(r => r.pass));
            GUILayout.Label("테스트 결과  요구사항 " + okReq + " / " + reqs.Count + "  ·  케이스 " + _results.Count(r => r.pass) + " / " + _results.Count, EditorStyles.boldLabel);

            if (okReq == reqs.Count)
                EditorGUILayout.HelpBox("✔ 모든 요구사항을 만족했다. 경계 입력까지 통과했으니 실제 학습에 써도 된다.", MessageType.Info);
            else if (_untouched)
                EditorGUILayout.HelpBox("아직 시작 코드 그대로다. 먼저 직접 작성해 본다. 어디서부터 할지 모르겠으면 💡 힌트 1단계부터.", MessageType.Warning);

            foreach (var q in reqs)
            {
                var fails = _results.Where(r => r.req == q && !r.pass).ToList();
                if (fails.Count == 0) continue;
                var f = fails[0];
                string text = "✘ " + q +
                              "\n   실패한 입력: " + f.label + "   기대 " + f.expected + " · 실제 " + f.actual +
                              (f.why != null ? "\n   원인: " + f.why : "") +
                              "\n   수정 방향: " + f.fix;
                EditorGUILayout.HelpBox(text, MessageType.Warning);
            }
            if (okReq < reqs.Count)
                GUILayout.Label("(경계)가 붙은 입력은 흔한 실수가 드러나는 입력이다. 정상 입력만 맞았다면 조건 하나를 빠뜨렸을 수 있다.", EditorStyles.wordWrappedMiniLabel);

            if (_showCases = EditorGUILayout.Foldout(_showCases, "케이스 전부 보기", true))
                foreach (var r in _results)
                {
                    GUI.color = r.pass ? Color.white : new Color(1f, 0.6f, 0.6f);
                    GUILayout.Label((r.pass ? "✔ " : "✘ ") + r.label + "     기대 " + r.expected + "  ·  실제 " + r.actual, EditorStyles.wordWrappedLabel);
                }
            GUI.color = Color.white;
        }

        // 힌트 1~5단계. [다음 힌트]로 하나씩 연다. 4·5단계는 코드를 보여 주고 파일에 넣을 수도 있다.
        void DrawHints(Step step, bool made)
        {
            int level = EditorPrefs.GetInt(Key + BlankKey(step, "hint"), 0);
            GUILayout.Space(6);
            GUILayout.Label("💡 힌트  " + level + " / 5", EditorStyles.boldLabel);
            for (int i = 0; i < level; i++)
            {
                GUILayout.Label((i + 1) + "단계 · " + HintNames[i], EditorStyles.miniBoldLabel);
                if (i < 3) EditorGUILayout.HelpBox(step.hints[i], MessageType.None);
                else if (i == 3) HintCode(step, step.skeleton, "이 코드로 바꾸기",
                    "지금 작성한 코드가 빈칸(___)이 든 기본 코드로 바뀝니다. ___ 를 모두 채워야 컴파일됩니다.", () => BlankTool.Put(step.blank, step.skeleton));
                else
                {
                    HintCode(step, step.answer, "정답을 파일에 넣기",
                        "지금 작성한 코드가 완성 코드로 바뀝니다. 넣은 뒤에도 [▶ 테스트 실행]으로 확인합니다.", () => BlankTool.Restore(step));
                    EditorGUILayout.HelpBox("해설 — " + step.explain, MessageType.None);
                }
            }
            if (level < 5 && GUILayout.Button("다음 힌트 ▶  " + (level + 1) + "단계 · " + HintNames[level]))
                EditorPrefs.SetInt(Key + BlankKey(step, "hint"), level + 1);
        }

        void HintCode(Step step, string code, string button, string warning, System.Action put)
        {
            // 읽기 전용 · 복사 가능. (고칠 수 있는 칸이면 학습자가 칸 안을 고쳐도 파일에는 안 들어가 헷갈린다)
            var style = new GUIStyle(EditorStyles.textArea) { wordWrap = false };
            float height = style.CalcHeight(new GUIContent(code), position.width) + 4f;
            EditorGUILayout.SelectableLabel(code, style, GUILayout.Height(height));
            if (GUILayout.Button(button, EditorStyles.miniButton) && EditorUtility.DisplayDialog(button, warning, "바꾸기", "취소"))
            {
                put();
                SetFlag(BlankKey(step, "made"), true);
                _edited = true;
                _results = null;
                GUIUtility.ExitGUI();
            }
        }

        // 📍 어디서 하는지 + [여기 보기]. 대상은 Hierarchy·Scene·Project 에 노랗게 표시된다.
        static void DrawWhere(Task task)
        {
            EditorGUILayout.BeginHorizontal();
            GUILayout.Space(26);
            GUILayout.Label("📍 " + task.where, new GUIStyle(EditorStyles.miniLabel) { wordWrap = true, normal = { textColor = new Color(1f, 0.75f, 0.1f) } });
            if (task.window != "Toolbar" && GUILayout.Button("📍 여기 보기", EditorStyles.miniButton, GUILayout.Width(80))) { TutorialGuide.Reveal(task); GUIUtility.ExitGUI(); }
            EditorGUILayout.EndHorizontal();
        }

        static void BlankRow(bool done, bool now, string text, string button, System.Action action)
        {
            EditorGUILayout.BeginHorizontal(now ? "SelectionRect" : GUIStyle.none);
            GUILayout.Label(done ? "✔" : now ? "▶" : "  ", GUILayout.Width(22));
            GUILayout.Label(text, new GUIStyle(EditorStyles.label) { wordWrap = true, fontStyle = now ? FontStyle.Bold : FontStyle.Normal });
            // 버튼이 씬·파일을 바꾸면 이번 그리기의 배치가 어긋나므로 바로 빠져나간다
            if (GUILayout.Button(button, GUILayout.Width(110))) { action(); GUIUtility.ExitGUI(); }
            EditorGUILayout.EndHorizontal();
        }

        // ── 이동 ───────────────────────────────────────────────────

        void Select(int c)
        {
            _chapter = c;
            EditorPrefs.SetInt(Key + "chapter", c);
            OnStepChanged();
        }

        void GoTo(int s)
        {
            SetStep(_chapter, Mathf.Clamp(s, 0, TutorialChapters.All[_chapter].steps.Count - 1));
            OnStepChanged();
        }

        void OnStepChanged()
        {
            _results = null;
            _showHint = _showHelp = _showCases = false;
            _nextFileCheck = 0;
            _scroll = Vector2.zero;
        }

        // ── 강사용 자동 완성 ───────────────────────────────────────
        // 수업 준비 · 시연 · 확인용. 학생 화면에는 보이지 않는다 (메뉴로 켤 때만).
        // 단계를 정답대로 만들고(solve · 실습 정답 되돌리기) 할 일을 전부 ✔ 로 표시한다.

        const string TeacherMenu = "Humanoid/개발자/자동 완성 버튼 보이기 (강사용)";
        static bool Teacher => EditorPrefs.GetBool(Key + "teacher");

        [MenuItem(TeacherMenu, false, 299)]
        static void ToggleTeacher()
        {
            EditorPrefs.SetBool(Key + "teacher", !Teacher);
            foreach (var w in Resources.FindObjectsOfTypeAll<TutorialWindow>()) w.Repaint();
        }

        [MenuItem(TeacherMenu, true)]
        static bool ToggleTeacherCheck()
        {
            Menu.SetChecked(TeacherMenu, Teacher);
            return true;
        }

        void DrawTeacher(int s)
        {
            GUILayout.Space(8);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            GUILayout.Label("강사용 — 학생에게는 보이지 않는다 (Humanoid > 개발자 > 자동 완성 버튼 보이기)", EditorStyles.miniBoldLabel);
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("⚡ 이 단계 자동 완성")) { CompleteStep(_chapter, s); GoTo(s + 1); GUIUtility.ExitGUI(); }
            if (GUILayout.Button("⚡ 이 장 전체 자동 완성"))
            {
                var steps = TutorialChapters.All[_chapter].steps;
                for (int i = 0; i < steps.Count; i++) CompleteStep(_chapter, i);
                GoTo(steps.Count - 1);
                GUIUtility.ExitGUI();
            }
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.EndVertical();
        }

        static void CompleteStep(int c, int s)
        {
            var step = TutorialChapters.All[c].steps[s];
            if (step.solve != null && !EditorApplication.isPlaying) step.solve();
            if (step.blank != null)
            {
                BlankTool.Restore(step);            // 정답을 넣는다
                SetFlag(BlankKey(step, "made"), true);
                SetFlag(BlankKey(step, "pass"), true);
            }
            for (int t = 0; t < step.tasks.Count; t++) SetFlag(TaskKey(c, s, t), true);
        }

        void ResetStep(int s)
        {
            var step = TutorialChapters.All[_chapter].steps[s];
            for (int t = 0; t < step.tasks.Count; t++) SetFlag(TaskKey(_chapter, s, t), false);
            if (step.blank != null) ResetBlank(step);
            OnStepChanged();
        }

        static void ResetBlank(Step step)
        {
            SetFlag(BlankKey(step, "made"), false);
            SetFlag(BlankKey(step, "pass"), false);
            EditorPrefs.SetInt(Key + BlankKey(step, "hint"), 0);
        }

        // 개발자용: 모든 장·단계를 차례로 화면에 그려 본다. OnGUI 에서 예외가 나면 Console 에 남는다.
        [MenuItem("Humanoid/개발자/튜토리얼 화면 시험", false, 304)]
        static void DrawAllSteps()
        {
            var w = GetWindow<TutorialWindow>("휴머노이드 튜토리얼");
            int c0 = w._chapter, c = 0, s = 0;
            var saved = new int[TutorialChapters.All.Count];      // 학생의 진행 위치를 지켜 둔다
            for (int i = 0; i < saved.Length; i++) saved[i] = StepIndex(i);
            double next = 0;
            EditorApplication.CallbackFunction tick = null;
            tick = () =>
            {
                if (EditorApplication.timeSinceStartup < next) return;
                next = EditorApplication.timeSinceStartup + 0.2;
                if (c >= TutorialChapters.All.Count)
                {
                    EditorApplication.update -= tick;
                    for (int i = 0; i < saved.Length; i++) SetStep(i, saved[i]);
                    w.Select(c0);
                    Debug.Log("[Humanoid] 튜토리얼 화면 시험 끝 — 위에 예외가 없으면 통과");
                    return;
                }
                w.Select(c); w.GoTo(s); w.Repaint();
                if (++s >= TutorialChapters.All[c].steps.Count) { s = 0; c++; }
            };
            EditorApplication.update += tick;
        }

        [MenuItem("Humanoid/튜토리얼 진행 초기화", false, 50)]
        static void ResetAll()
        {
            for (int c = 0; c < TutorialChapters.All.Count; c++)
            {
                SetStep(c, 0);
                var steps = TutorialChapters.All[c].steps;
                for (int s = 0; s < steps.Count; s++)
                {
                    for (int t = 0; t < steps[s].tasks.Count; t++) SetFlag(TaskKey(c, s, t), false);
                    if (steps[s].blank != null) ResetBlank(steps[s]);
                }
            }
            EditorPrefs.SetInt(Key + "chapter", 0);
            Debug.Log("[Humanoid] 튜토리얼 진행을 초기화했습니다. (작성한 코드는 그대로입니다. 정답으로 되돌리려면 Humanoid > 개발자 > 모든 정답 되돌리기)");
        }
    }
}
