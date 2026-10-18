using Humanoid;
using UnityEditor;
using static HumanoidEditor.HumanoidBuild;

namespace HumanoidEditor
{
    // 관절에 모터를 켜고 키보드 조작을 붙인다. 설명은 Docs/02-기초-움직임.md
    internal static class Ch2_Joint
    {
        [MenuItem("Humanoid/2. 기초 움직임 붙이기 (정답 씬)", false, 2)]
        static void Setup()
        {
            var root = Find("Humanoid");
            if (root == null) return;

            var body = root.GetComponent<HumanoidBody>();
            Undo.RecordObject(body, "관절 모터");
            // 실측(무릎 굽히기를 2초에 걸쳐): 300은 2.1초, 1000은 3.6초에 넘어졌다. 4000은 버틴다.
            body.spring = 4000f;
            body.damper = 400f;

            GetOrAdd<KeyboardControl>(root.gameObject).driveBody = true;
            Selection.activeGameObject = root.gameObject;
            UnityEngine.Debug.Log("[Humanoid] 2장 정답 씬 — 모터 4000/400 · 키보드 조작. Play 후 H로 매달아 관절을 하나씩 돌려 보세요.");
        }
    }
}
