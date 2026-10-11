using System.Collections.Generic;
using UnityEditor;
using UnityEditor.PackageManager;
using UnityEngine;

namespace HumanoidEditor
{
    /// <summary>
    /// 휴머노이드 트랙을 .unitypackage 하나로 내보낸다. 다른 Unity 프로젝트로 옮길 때 쓴다.
    ///
    /// 담는 것: Assets/Humanoid 전부 (스크립트 · 에디터 도구 · 튜토리얼 · 문서 · 씬 · 머티리얼 · yaml)
    /// 빼는 것: Assets/Humanoid/Practice (학습자가 만든 실습 씬. 가져간 곳에서 새로 만든다)
    ///
    /// IncludeDependencies 를 쓰지 않는다. 켜면 URP 설정 같은 프로젝트 에셋까지 딸려 들어간다.
    /// 대신 가져갈 프로젝트에 필요한 패키지(ML-Agents · Input System)를 콘솔에 알려 준다.
    /// </summary>
    public static class HumanoidPackageExporter
    {
        const string Root = "Assets/Humanoid";
        static readonly string[] Required = { "com.unity.ml-agents", "com.unity.inputsystem" };

        [MenuItem("Humanoid/개발자/유니티 패키지로 내보내기…", false, 310)]
        static void ExportWithDialog()
        {
            string path = EditorUtility.SaveFilePanel("유니티 패키지로 내보내기", "", "HumanoidTutorial.unitypackage", "unitypackage");
            if (!string.IsNullOrEmpty(path)) Export(path);
        }

        /// <summary>대화상자 없이 내보낸다 (자동화용). 담은 파일 수를 돌려준다.</summary>
        public static int Export(string path)
        {
            var paths = new List<string>();
            foreach (var guid in AssetDatabase.FindAssets("", new[] { Root }))
            {
                string p = AssetDatabase.GUIDToAssetPath(guid);
                if (p.StartsWith(Root + "/Practice") || paths.Contains(p)) continue;
                paths.Add(p);
            }

            AssetDatabase.ExportPackage(paths.ToArray(), path, ExportPackageOptions.Default);
            Debug.Log("[Humanoid] 패키지로 내보냈습니다 — 에셋 " + paths.Count + "개 → " + path + "\n" +
                      "  가져갈 프로젝트에 필요한 것: Unity 6000.3 이상 · " + string.Join(" · ", Required) + " (ML-Agents 4.x)\n" +
                      "  URP가 없으면 머티리얼이 Standard 셰이더로 만들어진다 (색만 다르고 동작은 같다).\n" +
                      "  학습 실행에는 Python ml-agents(mlagents-learn)가 따로 필요하다.");
            return paths.Count;
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
