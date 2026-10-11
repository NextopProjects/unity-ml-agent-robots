using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace HumanoidEditor
{
    /// <summary>
    /// 휴머노이드 튜토리얼 창.  메뉴 > Humanoid > 튜토리얼 열기
    ///
    /// 위에서 아래로 읽는다:  🎯 목표 → 설명 → ▶ 지금 할 일 → ① ② ③ 할 일 → (끝나면) 다음 단계
    /// 할 일은 위에서부터 하나씩 확인한다. 조건이 있는 할 일은 저절로 ✔ 가 되고, 없는 것은 [했어요]로 넘긴다.
    /// 진행 상황은 EditorPrefs 에 저장되어 Play·컴파일·재시작 뒤에도 이어진다.
    /// </summary>
    internal class TutorialWindow : EditorWindow
    {
        const string Key = "Humanoid.Tutorial.";
        const string DocFolder = "Assets/Humanoid/Docs/";

        int _chapter;
        Vector2 _scroll;
        bool _showSkeleton, _showHint, _showHelp;
        List<BlankCheck.Result> _results;     // 마지막 채점 결과 (화면 표시용)
        bool _blankOpen;                       // 빈칸에 아직 TODO가 있는가 (파일을 계속 읽지 않도록 저장해 둔다)
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

        static readonly Task MathFile = new Task { window = "Project", path = BlankTool.MathPath, where = "Project ▸ Assets/Humanoid/Scripts/HumanoidMath.cs" };

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
                    _blankOpen = BlankTool.IsOpen(step.blank);
                    _nextFileCheck = EditorApplication.timeSinceStartup + 1.0;
                }
                // 코드를 고칠 차례면 Project 창에서 HumanoidMath.cs 를 표시한다
                TutorialGuide.Set(Flag(BlankKey(step, "made")) && _blankOpen ? MathFile : null);
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

        // 코드 단계: 빈칸 만들기 → 파일 열기 → 채점
        void DrawBlank(Step step)
        {
            bool made = Flag(BlankKey(step, "made"));
            bool pass = Flag(BlankKey(step, "pass"));
            bool filled = made && !_blankOpen;

            BlankRow(made, !made, "① 빈칸을 만든다 — 정답이 // TODO 로 바뀐다", "빈칸 만들기", () =>
            {
                BlankTool.Make(step.blank);
                SetFlag(BlankKey(step, "made"), true);
                _blankOpen = true;
                _results = null;
            });
            BlankRow(filled, made && !filled, "② HumanoidMath.cs 를 열어 TODO 두 줄을 지우고 답을 써서 저장한다", "📄 파일 열기", () =>
                AssetDatabase.OpenAsset(AssetDatabase.LoadAssetAtPath<MonoScript>(BlankTool.MathPath), BlankTool.Line(step.blank)));
            if (made && !filled) DrawWhere(MathFile);
            GUI.enabled = made && !EditorApplication.isCompiling;
            BlankRow(pass, filled && !pass, "③ 채점한다 — 모든 케이스를 통과해야 한다", "▶ 채점하기", () =>
            {
                _blankOpen = BlankTool.IsOpen(step.blank);
                _results = BlankCheck.Grade(step.blank);
                SetFlag(BlankKey(step, "pass"), _results.TrueForAll(r => r.pass) && !_blankOpen);
            });
            GUI.enabled = true;

            if (_results != null)
            {
                int ok = _results.FindAll(r => r.pass).Count;
                GUILayout.Label("채점 결과  " + ok + " / " + _results.Count, EditorStyles.boldLabel);
                foreach (var r in _results)
                {
                    GUI.color = r.pass ? Color.white : new Color(1f, 0.6f, 0.6f);
                    GUILayout.Label((r.pass ? "✔ " : "✘ ") + r.label + "     기대 " + r.expected + "  ·  실제 " + r.actual, EditorStyles.wordWrappedLabel);
                }
                GUI.color = Color.white;
                if (ok < _results.Count) EditorGUILayout.HelpBox("(경계)가 붙은 케이스는 실수가 드러나는 입력입니다. 정상 입력만 맞았다면 조건 하나를 빠뜨렸을 수 있습니다.", MessageType.None);
                if (_blankOpen) EditorGUILayout.HelpBox("파일에 아직 // TODO 가 남아 있습니다. 답을 쓴 뒤 TODO 줄과 임시값 줄을 지우세요.", MessageType.Warning);
            }

            if (_showSkeleton = EditorGUILayout.Foldout(_showSkeleton, "🦴 뼈대 보기 (___ 를 채운다)", true))
                EditorGUILayout.TextArea(step.skeleton, EditorStyles.textArea);

            if (GUILayout.Button("막혔을 때: 정답 되돌리기 (빈칸 대신 정답을 다시 넣는다)", EditorStyles.miniButton))
            {
                BlankTool.Restore(step.blank);
                _blankOpen = false;
                _results = null;
            }
        }

        /// <summary>📍 어디서 하는지 + [여기 보기]. 대상은 Hierarchy·Scene·Project 에 노랗게 표시된다.</summary>
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
            _showSkeleton = _showHint = _showHelp = false;
            _nextFileCheck = 0;
            _scroll = Vector2.zero;
        }

        // ── 강사용 자동 완성 ───────────────────────────────────────
        // 수업 준비 · 시연 · 확인용. 학생 화면에는 보이지 않는다 (메뉴로 켤 때만).
        // 단계를 정답대로 만들고(solve · 빈칸 정답 되돌리기) 할 일을 전부 ✔ 로 표시한다.

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
                BlankTool.Restore(step.blank);      // 빈칸을 만들어 두었으면 정답을 되돌린다
                SetFlag(BlankKey(step, "made"), true);
                SetFlag(BlankKey(step, "pass"), true);
            }
            for (int t = 0; t < step.tasks.Count; t++) SetFlag(TaskKey(c, s, t), true);
        }

        void ResetStep(int s)
        {
            var step = TutorialChapters.All[_chapter].steps[s];
            for (int t = 0; t < step.tasks.Count; t++) SetFlag(TaskKey(_chapter, s, t), false);
            if (step.blank != null) { SetFlag(BlankKey(step, "made"), false); SetFlag(BlankKey(step, "pass"), false); }
            OnStepChanged();
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
                    if (steps[s].blank != null) { SetFlag(BlankKey(steps[s], "made"), false); SetFlag(BlankKey(steps[s], "pass"), false); }
                }
            }
            EditorPrefs.SetInt(Key + "chapter", 0);
            Debug.Log("[Humanoid] 튜토리얼 진행을 초기화했습니다. (코드의 빈칸은 그대로입니다. 되돌리려면 Humanoid > 개발자 > 모든 정답 되돌리기)");
        }
    }
}
