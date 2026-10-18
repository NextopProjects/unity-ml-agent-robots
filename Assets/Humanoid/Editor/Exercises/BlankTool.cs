using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;

namespace HumanoidEditor
{
    // 실습 도구. 저장소의 HumanoidMath.cs 에는 정답이 들어 있다 (어느 장이든 바로 실행되도록).
    // 튜토리얼 창이 실습 자리(함수 본문 · 클래스 멤버)를 시작 코드로 바꾸고, 학습자가 그 자리에 직접 구현한다.
    // 자리는 표시 주석 없이 이름으로 찾는다 — 코드에 앞으로 할 실습이 미리 드러나지 않게.
    // 처음 바꿀 때 정답을 Library/HumanoidBlanks/ 에 보관한다. 보관본이 있다 = 구현 중이다.
    internal static class BlankTool
    {
        public const string MathPath = "Assets/Humanoid/Scripts/HumanoidMath.cs";
        const string SaveFolder = "Library/HumanoidBlanks";

        // 실습 id → 찾을 이름. 함수는 그 본문, 클래스는 Error 앞까지의 멤버가 실습 자리다.
        static readonly Dictionary<string, string> Member = new Dictionary<string, string>
        {
            { "1-1", "CenterOfMass(" }, { "2-1", "ActionToAngle(" }, { "2-2", "AngleToAction(" },
            { "3-1", "DownInBody(" }, { "3-2", "StandReward(" },
            { "4-1", "HeadingRotation(" }, { "4-2", "SpeedReward(" }, { "4-3", "FacingReward(" },
            { "5-1", "class SpeedStats" }, { "5-2", "float Error(" },
        };

        // 구현을 시작한다: 그 자리를 시작 코드로 바꾼다.
        // start 가 null 이면 "처음부터" — 컴파일만 되는 임시값 한 줄(반환형에 맞춘 return)만 남긴다.
        public static void Make(string id, string start)
        {
            string note = start == null ? "여기에 직접 구현한다. 아래 줄은 컴파일되도록 넣어 둔 임시값이다."
                                        : "주어진 코드에서 출발해 직접 완성한다.";
            Put(id, "// " + note + "\n" + (start ?? Placeholder(id)));
        }

        // 그 자리를 code 로 바꾼다 (시작 코드 · 힌트 4단계 코드 · 정답). 정답은 처음 한 번만 보관한다.
        public static void Put(string id, string code) => Edit(id, (body, indent, placeholder) =>
        {
            if (!Started(id))
            {
                Directory.CreateDirectory(SaveFolder);
                File.WriteAllLines(SavePath(id), body);
            }
            return code.Replace("\r\n", "\n").Split('\n').Select(l => l.Length == 0 ? "" : indent + l).ToList();
        });

        // 보관해 둔 정답을 되돌린다 (힌트 5단계 · 강사용 자동 완성).
        public static void Restore(string id) => Edit(id, (body, indent, placeholder) =>
        {
            if (!Started(id)) return null;
            var answer = new List<string>(File.ReadAllLines(SavePath(id)));
            File.Delete(SavePath(id));
            return answer;
        });

        public static bool Started(string id) => File.Exists(SavePath(id));

        // 지금 그 자리의 코드 (들여쓰기를 뺀 것).
        public static string Body(string id)
        {
            string text = "";
            Edit(id, (body, indent, placeholder) => { text = Dedent(body); return null; }, write: false);
            return text;
        }

        // 정답 코드 (힌트 5단계에 보여 준다). 구현 중이면 보관본, 아니면 지금 코드가 정답이다.
        public static string Answer(string id) => Started(id) ? Dedent(File.ReadAllLines(SavePath(id)).ToList()) : Body(id);

        // 시작 코드를 하나도 안 고쳤는가. (공백 · 주석만 있는 줄은 무시)
        public static bool Untouched(string id, string start) => Squash(Code(Body(id))) == Squash(Code(start ?? Placeholder(id)));

        static string Code(string s) => string.Join("\n", s.Split('\n').Where(l => !l.Trim().StartsWith("//")));

        // 컴파일만 되는 임시값: 함수의 반환형에 맞춘 return 한 줄
        static string Placeholder(string id)
        {
            var lines = File.ReadAllLines(MathPath);
            string sig = lines.FirstOrDefault(l => l.Contains(Member[id])) ?? "";
            string type = sig.Contains(Member[id]) ? sig.Substring(0, sig.IndexOf(Member[id])).Trim().Split(' ').Last() : "";   // 이름 바로 앞 = 반환형
            return type == "Vector3" ? "return Vector3.zero;"
                 : type == "Quaternion" ? "return Quaternion.identity;"
                 : type == "float" ? "return 0f;"
                 : "";
        }

        // 실습 자리 첫 줄의 줄 번호(1부터). 파일을 그 줄에서 열 때 쓴다.
        public static int Line(string id)
        {
            var lines = new List<string>(File.ReadAllLines(MathPath));
            return Find(lines, id, out int begin, out _) ? begin + 1 : 1;
        }

        // 실습 자리 = lines[begin .. end-1]. 함수는 { } 사이, 클래스는 { 다음부터 Error 앞 빈 줄 전까지.
        static bool Find(List<string> lines, string id, out int begin, out int end)
        {
            begin = end = -1;
            if (!Member.TryGetValue(id, out string name)) return false;
            int sig = lines.FindIndex(l => l.Contains(name) && !l.TrimStart().StartsWith("//"));
            if (sig < 0) return false;
            int open = lines.FindIndex(sig, l => l.Trim() == "{");
            if (open < 0) return false;

            int depth = 0, close = -1;
            for (int i = open; i < lines.Count && close < 0; i++)
            {
                depth += lines[i].Count(c => c == '{') - lines[i].Count(c => c == '}');
                if (depth == 0) close = i;
            }
            if (close < 0) return false;

            begin = open + 1;
            end = close;
            if (name.StartsWith("class "))
            {
                int next = lines.FindIndex(begin, l => l.Contains(Member["5-2"]));
                if (next > 0 && next < close) end = next;
                while (end > begin && lines[end - 1].Trim().Length == 0) end--;     // Error 앞 빈 줄은 자리 밖에 둔다
            }
            return true;
        }

        // 실습 자리를 change 결과로 바꾼다. null이면 그대로 둔다. (indent = 자리의 들여쓰기)
        static void Edit(string id, System.Func<List<string>, string, string, List<string>> change, bool write = true)
        {
            string raw = File.ReadAllText(MathPath, Encoding.UTF8);
            string newline = raw.Contains("\r\n") ? "\r\n" : "\n";     // 줄바꿈을 지켜야 git diff가 번지지 않는다
            var lines = new List<string>(raw.Replace("\r\n", "\n").Split('\n'));
            if (!Find(lines, id, out int begin, out int end)) return;

            string open = lines[begin - 1];
            string indent = open.Substring(0, open.Length - open.TrimStart().Length) + "    ";
            var body = lines.GetRange(begin, end - begin);
            var result = change(body, indent, null);
            if (result == null || !write) return;

            lines.RemoveRange(begin, body.Count);
            lines.InsertRange(begin, result);
            File.WriteAllText(MathPath, string.Join(newline, lines), new UTF8Encoding(false));
            AssetDatabase.ImportAsset(MathPath);     // 컴파일이 다시 돈다
        }

        static string Dedent(List<string> body)
        {
            int cut = body.Where(l => l.Trim().Length > 0).Select(l => l.Length - l.TrimStart().Length).DefaultIfEmpty(0).Min();
            return string.Join("\n", body.Select(l => l.Length >= cut ? l.Substring(cut) : l.TrimStart()));
        }

        static string Squash(string s) => new string(s.Where(c => !char.IsWhiteSpace(c)).ToArray());
        static string SavePath(string id) => SaveFolder + "/실습 " + id + ".txt";

        // ── 개발자용: 시작 코드 검증 (모두 시작 → 전체 채점은 실패, 모두 되돌리기 → 전체 채점은 통과) ──

        static IEnumerable<Step> Exercises => TutorialChapters.All.SelectMany(c => c.steps).Where(s => s.blank != null);

        // 아직 시작하지 않은 실습을 전부 시작 코드로. 튜토리얼을 시작할 때 부른다 — 앞으로 할 실습의 정답이 파일에 보이지 않게.
        // 이미 시작한 실습(보관본이 있음)은 건드리지 않는다. 학습자가 쓴 코드를 덮지 않도록.
        public static void StartAll()
        {
            foreach (var s in Exercises) if (!Started(s.blank)) Make(s.blank, s.start);
        }

        [MenuItem("Humanoid/개발자/모든 실습 시작 코드로", false, 300)]
        static void MakeAll() { foreach (var s in Exercises) Make(s.blank, s.start); }

        [MenuItem("Humanoid/개발자/모든 정답 되돌리기", false, 301)]
        static void RestoreAll() { foreach (var s in Exercises) Restore(s.blank); }
    }
}
