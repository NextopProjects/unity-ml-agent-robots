using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.PackageManager;
using UnityEngine;

namespace HumanoidEditor
{
    // 휴머노이드 트랙을 .unitypackage 하나로 내보낸다. 다른 Unity 프로젝트로 옮길 때 쓴다.
    //
    // 담을 것만 정해 둔다 (폴더를 통째로 담지 않는다). 학습 결과(results/)처럼 작업 중에 생긴 것이 딸려 들어가지 않게.
    //   담는 것: Docs · Editor · Materials · Scenes · Scripts 폴더,  humanoid_config.yaml · PLAN.md
    //   빼는 것: 그 밖의 모든 것 + 담는 폴더 안이라도 학습 결과 파일(.onnx · .pt …)
    //
    // IncludeDependencies 를 쓰지 않는다. 켜면 URP 설정 같은 프로젝트 에셋까지 딸려 들어간다.
    // 대신 가져갈 프로젝트에 필요한 패키지(ML-Agents · Input System)를 콘솔에 알려 준다.
    public static class HumanoidPackageExporter
    {
        const string Root = "Assets/Humanoid";
        static readonly string[] Folders = { "Docs", "Editor", "Materials", "Scenes", "Scripts" };
        static readonly string[] Files = { "humanoid_config.yaml", "PLAN.md" };
        static readonly string[] ResultExtensions = { ".onnx", ".pt", ".nn", ".json" };     // 학습기가 만드는 파일 (timers.json 등)
        static readonly string[] Required = { "com.unity.ml-agents", "com.unity.inputsystem" };

        [MenuItem("Humanoid/개발자/유니티 패키지로 내보내기…", false, 310)]
        static void ExportWithDialog()
        {
            string path = EditorUtility.SaveFilePanel("유니티 패키지로 내보내기", "", "HumanoidTutorial.unitypackage", "unitypackage");
            if (!string.IsNullOrEmpty(path)) Export(path);
        }

        // 대화상자 없이 내보낸다 (자동화용). 담은 파일 수를 돌려준다.
        public static int Export(string path)
        {
            var paths = new List<string>();
            var skipped = new List<string>();
            foreach (var guid in AssetDatabase.FindAssets("", new[] { Root }))
            {
                string p = AssetDatabase.GUIDToAssetPath(guid);
                if (paths.Contains(p) || skipped.Contains(p) || AssetDatabase.IsValidFolder(p)) continue;
                (Allowed(p) ? paths : skipped).Add(p);
            }

            AssetDatabase.ExportPackage(paths.ToArray(), path, ExportPackageOptions.Default);

            // 뺀 것은 맨 위 폴더 · 파일 단위로 묶어 보여 준다 (results/ 아래 수백 개를 다 늘어놓지 않게)
            var skippedGroups = skipped.Select(p => p.Substring(Root.Length + 1).Split('/')[0]).Distinct().ToList();
            Debug.Log("[Humanoid] 패키지로 내보냈습니다 — 에셋 " + paths.Count + "개 → " + path + "\n" +
                      "  담은 것: " + string.Join(" · ", Folders) + " 폴더, " + string.Join(" · ", Files) + "\n" +
                      "  뺀 것: " + (skipped.Count == 0 ? "없음" : skipped.Count + "개 (" + string.Join(", ", skippedGroups) + ")") + "\n" +
                      "  가져갈 프로젝트에 필요한 것: Unity 6000.3 이상 · " + string.Join(" · ", Required) + " (ML-Agents 4.x)\n" +
                      "  URP가 없으면 머티리얼이 Standard 셰이더로 만들어진다 (색만 다르고 동작은 같다).\n" +
                      "  학습 실행에는 Python ml-agents(mlagents-learn)가 따로 필요하다.");
            return paths.Count;
        }

        static bool Allowed(string assetPath)
        {
            string rel = assetPath.Substring(Root.Length + 1);
            if (ResultExtensions.Contains(Path.GetExtension(rel).ToLowerInvariant())) return false;
            return Files.Contains(rel) || Folders.Any(f => rel.StartsWith(f + "/"));
        }

        // 이 프로젝트에 필요한 패키지가 다 있는지 (내보내기 전에 확인하는 용도)
        [MenuItem("Humanoid/개발자/필요한 패키지 확인", false, 311)]
        static void CheckPackages()
        {
            var request = Client.List(true);
            while (!request.IsCompleted) { }
            var missing = new List<string>(Required);
            if (request.Status == StatusCode.Success)
                foreach (var info in request.Result) missing.Remove(info.name);
            Debug.Log(missing.Count == 0 ? "[Humanoid] 필요한 패키지가 모두 있습니다: " + string.Join(", ", Required)
                                         : "[Humanoid] 빠진 패키지: " + string.Join(", ", missing));
        }
    }
}
