using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;

namespace HumanoidEditor
{
    /// <summary>
    /// 빈칸 도구. 저장소의 HumanoidMath.cs 에는 정답이 들어 있다 (어느 장이든 바로 실행되도록).
    /// 튜토리얼 창이 빈칸 하나씩 정답을 // TODO + 임시값으로 바꾸고, 필요하면 되돌린다.
    /// 지운 정답은 Library/HumanoidBlanks/ 에 보관한다.
    /// </summary>
    internal static class BlankTool
    {
        public const string MathPath = "Assets/Humanoid/Scripts/HumanoidMath.cs";
        const string SaveFolder = "Library/HumanoidBlanks";

        /// <summary>정답을 지우고 빈칸으로 만든다. 이미 빈칸이면 아무것도 안 한다.</summary>
        public static void Make(string id) => Edit(id, (body, indent, placeholder) =>
        {
            if (IsBlank(body)) return null;
            Directory.CreateDirectory(SaveFolder);
            File.WriteAllLines(SavePath(id), body);
            return new List<string> { indent + "// TODO: 빈칸 " + id + " — 튜토리얼 창의 설명을 보고 채우세요.", indent + placeholder };
        });

        /// <summary>보관해 둔 정답을 되돌린다.</summary>
        public static void Restore(string id) => Edit(id, (body, indent, placeholder) =>
        {
            if (!File.Exists(SavePath(id))) return null;
            var answer = new List<string>(File.ReadAllLines(SavePath(id)));
            File.Delete(SavePath(id));
            return answer;
        });

        /// <summary>아직 TODO 상태인가.</summary>
        public static bool IsOpen(string id)
        {
            bool open = false;
            Edit(id, (body, indent, placeholder) => { open = IsBlank(body); return null; }, write: false);
            return open;
        }

        /// <summary>빈칸 첫 줄의 줄 번호(1부터). 파일을 그 줄에서 열 때 쓴다.</summary>
        public static int Line(string id)
        {
            var lines = File.ReadAllLines(MathPath);
            for (int i = 0; i < lines.Length; i++)
                if (lines[i].Trim() == "// ▼ 빈칸 " + id) return i + 2;
            return 1;
        }

        /// <summary>"// ▼ 빈칸 id" 와 "// ▲ 빈칸 id | 임시값" 사이를 change 결과로 바꾼다. null이면 그대로 둔다.</summary>
        static void Edit(string id, System.Func<List<string>, string, string, List<string>> change, bool write = true)
        {
            string raw = File.ReadAllText(MathPath, Encoding.UTF8);
            string newline = raw.Contains("\r\n") ? "\r\n" : "\n";     // 줄바꿈을 지켜야 git diff가 번지지 않는다
            var lines = new List<string>(raw.Replace("\r\n", "\n").Split('\n'));

            int begin = lines.FindIndex(l => l.Trim() == "// ▼ 빈칸 " + id);
            if (begin < 0) return;
            int end = lines.FindIndex(begin, l => l.Trim().StartsWith("// ▲ 빈칸 " + id));
            string endLine = lines[end].Trim();

            var body = lines.GetRange(begin + 1, end - begin - 1);
            var result = change(body, lines[begin].Substring(0, lines[begin].IndexOf('/')),
                                endLine.Substring(endLine.IndexOf('|') + 1).Trim());
            if (result == null || !write) return;

            lines.RemoveRange(begin + 1, body.Count);
            lines.InsertRange(begin + 1, result);
            File.WriteAllText(MathPath, string.Join(newline, lines), new UTF8Encoding(false));
            AssetDatabase.ImportAsset(MathPath);     // 컴파일이 다시 돈다
        }

        static bool IsBlank(List<string> body) => body.Exists(l => l.Trim().StartsWith("// TODO"));
        static string SavePath(string id) => SaveFolder + "/빈칸 " + id + ".txt";

        // ── 개발자용: 빈칸 검증 (모두 빈칸 → 전체 채점은 실패, 모두 되돌리기 → 전체 채점은 통과) ──

        public static readonly string[] All = { "1-1", "2-1", "2-2", "3-1", "3-2", "4-1", "4-2", "4-3" };

        [MenuItem("Humanoid/개발자/모든 빈칸 만들기", false, 300)]
        static void MakeAll() { foreach (var id in All) Make(id); }

        [MenuItem("Humanoid/개발자/모든 정답 되돌리기", false, 301)]
        static void RestoreAll() { foreach (var id in All) Restore(id); }
    }
}
