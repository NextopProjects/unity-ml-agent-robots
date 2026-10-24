using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;

namespace HumanoidEditor
{
    // 실습 도구. 저장소의 스크립트에는 정답이 들어 있다 (어느 장이든 바로 실행되도록).
    // 튜토리얼 창이 실습 자리(메서드 · 속성의 { } 안)를 시작 코드로 바꾸고, 학습자가 그 자리에 직접 구현한다.
    // 자리는 표시 주석 없이 이름으로 찾는다 — 코드에 앞으로 할 실습이 미리 드러나지 않게.
    // 정답은 튜토리얼 데이터(Step.answer)에 있다. 파일 · Library 상태와 상관없이 힌트 5단계가 보이도록.
    internal static class BlankTool
    {
        const string Scripts = "Assets/Humanoid/Scripts/";

        // 실습 id → (파일, 찾을 이름). 이름이 있는 줄 다음의 { } 안이 실습 자리다.
        static readonly Dictionary<string, (string file, string name)> Member = new Dictionary<string, (string, string)>
        {
            { "1-1", ("BodyPart.cs", "void ResetPose(") },
            { "1-2", ("HumanoidBody.cs", "bool HasFallen") },
            { "2-1", ("BodyPart.cs", "void SetTarget(") },
            { "2-2", ("HumanoidBody.cs", "void SetActions(") },
            { "3-1", ("StandAgent.cs", "void OnEpisodeBegin(") },
            { "3-2", ("StandAgent.cs", "void CollectObservations(") },
            { "3-3", ("StandAgent.cs", "void OnActionReceived(") },
            { "4-1", ("WalkAgent.cs", "void CollectObservations(") },
            { "4-2", ("WalkAgent.cs", "float StepReward(") },
            { "4-3", ("WalkAgent.cs", "void OnActionReceived(") },
            { "5-1", ("WalkAgent.cs", "void RecordStats(") },
            { "5-2", ("WalkAgent.cs", "void ReadCurriculum(") },
        };

        public static string PathOf(string id) => Scripts + Member[id].file;

        // 구현을 시작한다: 그 자리를 시작 코드로 바꾼다. start 가 null 이면 "처음부터" — 컴파일용 임시값만 남긴다.
        public static void Make(string id, string start)
        {
            string note = start == null ? "여기에 직접 구현한다." : "주어진 코드에서 출발해 직접 완성한다.";
            string code = start ?? Placeholder(id);
            Put(id, "// " + note + (code.Length > 0 ? "\n" + code : ""));
            Started(id, true);
        }

        // 그 자리를 code 로 바꾼다 (시작 코드 · 힌트 4단계 코드 · 정답).
        public static void Put(string id, string code) => Edit(id, (body, indent) =>
            code.Replace("\r\n", "\n").Split('\n').Select(l => l.Length == 0 ? "" : indent + l).ToList());

        // 시작했는가 — 도구가 시작 코드로 바꾼 적이 있는가. 프로젝트마다 따로 기록한다.
        public static bool Started(string id) => EditorPrefs.GetBool(TutorialChapters.PrefsPrefix + "started." + id);
        static void Started(string id, bool value) => EditorPrefs.SetBool(TutorialChapters.PrefsPrefix + "started." + id, value);

        // 지금 그 자리의 코드 (들여쓰기를 뺀 것).
        public static string Body(string id)
        {
            string text = "";
            Edit(id, (body, indent) => { text = Dedent(body); return null; });
            return text;
        }

        // 시작 코드를 하나도 안 고쳤는가. (공백 · 주석만 있는 줄은 무시)
        public static bool Untouched(string id, string start) => Same(Body(id), start ?? Placeholder(id));

        public static bool Same(string a, string b) => Squash(Code(a)) == Squash(Code(b));

        // 컴파일만 되는 임시값: 반환형에 맞춘 return 한 줄 (void 면 없음)
        static string Placeholder(string id)
        {
            string sig = File.ReadAllLines(PathOf(id)).FirstOrDefault(l => l.Contains(Member[id].name)) ?? "";
            string type = sig.Contains(Member[id].name) ? sig.Substring(0, sig.IndexOf(Member[id].name)).Trim().Split(' ').Last() : "";
            return type == "float" ? "return 0f;" : type == "bool" ? "return false;" : "";
        }

        // 실습 자리 첫 줄의 줄 번호(1부터). 파일을 그 줄에서 열 때 쓴다.
        public static int Line(string id)
        {
            if (!Member.ContainsKey(id) || !File.Exists(PathOf(id))) return 1;
            var lines = new List<string>(File.ReadAllLines(PathOf(id)));
            return Find(lines, id, out int begin, out _) ? begin + 1 : 1;
        }

        // 실습 자리 = lines[begin .. end-1]. 이름이 있는 줄 다음의 { 와 짝이 되는 } 사이.
        static bool Find(List<string> lines, string id, out int begin, out int end)
        {
            begin = end = -1;
            string name = Member[id].name;
            int sig = lines.FindIndex(l => l.Contains(name) && !l.TrimStart().StartsWith("//"));
            if (sig < 0) return false;
            int open = lines.FindIndex(sig, l => l.Trim() == "{");
            if (open < 0) return false;

            int depth = 0;
            for (int i = open; i < lines.Count; i++)
            {
                depth += lines[i].Count(c => c == '{') - lines[i].Count(c => c == '}');
                if (depth != 0) continue;
                begin = open + 1;
                end = i;
                return true;
            }
            return false;
        }

        // 실습 자리를 change 결과로 바꾼다. null이면 그대로 둔다. (indent = 자리의 들여쓰기)
        static void Edit(string id, System.Func<List<string>, string, List<string>> change)
        {
            string path = PathOf(id);
            string raw = File.ReadAllText(path, Encoding.UTF8);
            string newline = raw.Contains("\r\n") ? "\r\n" : "\n";     // 줄바꿈을 지켜야 git diff가 번지지 않는다
            var lines = new List<string>(raw.Replace("\r\n", "\n").Split('\n'));
            if (!Find(lines, id, out int begin, out int end)) return;

            string open = lines[begin - 1];
            string indent = open.Substring(0, open.Length - open.TrimStart().Length) + "    ";
            var body = lines.GetRange(begin, end - begin);
            var result = change(body, indent);
            if (result == null) return;

            lines.RemoveRange(begin, body.Count);
            lines.InsertRange(begin, result);
            File.WriteAllText(path, string.Join(newline, lines), new UTF8Encoding(false));
            AssetDatabase.ImportAsset(path);     // 컴파일이 다시 돈다
        }

        public static string Dedent(IEnumerable<string> body)
        {
            var list = body.ToList();
            int cut = list.Where(l => l.Trim().Length > 0).Select(l => l.Length - l.TrimStart().Length).DefaultIfEmpty(0).Min();
            return string.Join("\n", list.Select(l => l.Length >= cut ? l.Substring(cut) : l.TrimStart()));
        }

        static string Code(string s) => string.Join("\n", s.Replace("\r\n", "\n").Split('\n').Where(l => !l.Trim().StartsWith("//")));
        static string Squash(string s) => new string(s.Where(c => !char.IsWhiteSpace(c)).ToArray());

        // ── 시작 · 되돌리기 ──

        static IEnumerable<Step> Exercises => TutorialChapters.All.SelectMany(c => c.steps).Where(s => s.blank != null);

        // 아직 시작하지 않은 실습을 전부 시작 코드로. 튜토리얼을 시작할 때 부른다 — 앞으로 할 실습의 정답이 파일에 보이지 않게.
        // 이미 시작한 실습은 건드리지 않는다. 학습자가 쓴 코드를 덮지 않도록.
        public static void StartAll()
        {
            foreach (var s in Exercises) if (!Started(s.blank)) Make(s.blank, s.start);
        }

        // 정답을 파일에 넣는다 (힌트 5단계 · 강사용 자동 완성). 정답은 데이터에서 온다.
        public static void Restore(Step step) => Put(step.blank, step.answer);

        [MenuItem("Humanoid/개발자/모든 실습 시작 코드로", false, 300)]
        static void MakeAll() { foreach (var s in Exercises) Make(s.blank, s.start); }

        // 저장소 상태로: 정답을 넣고 '시작함' 기록도 지운다.
        [MenuItem("Humanoid/개발자/모든 정답 되돌리기", false, 301)]
        static void RestoreAll() { foreach (var s in Exercises) { Restore(s); Started(s.blank, false); } }
    }
}
