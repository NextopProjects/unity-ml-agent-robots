using Humanoid;
using UnityEditor;
using UnityEngine;
using static HumanoidEditor.HumanoidBuild;

namespace HumanoidEditor
{
    // 학습 영역을 9개(3×3)로 복제한다. 학습 코드는 한 줄도 바꾸지 않는다.
    // Behavior Name이 같은 에이전트는 ML-Agents가 한 두뇌로 묶어 함께 학습한다 → 경험이 9배로 쌓인다.
    // 복제는 몸 · 에이전트 구성을 다 마친 뒤 마지막에 한다. (메뉴는 이름으로 첫 번째 영역만 찾는다)
    internal static class Ch5_Parallel
    {
        [MenuItem("Humanoid/5. 학습 영역 9개로 복제", false, 5)]
        static void Clone()
        {
            var origin = GameObject.Find(AreaName);
            if (origin == null) { Debug.LogError("[Humanoid] 먼저 1~4장 메뉴를 실행하세요."); return; }
            RemoveClones();

            for (int i = 1; i < 9; i++)
            {
                var copy = Object.Instantiate(origin);
                copy.name = AreaName + " " + i;
                copy.transform.position = new Vector3(i % 3 * 45f, 0f, i / 3 * 45f);   // 바닥이 40×40이라 45씩 띄운다
                // 화면 UI는 원본 하나만. (StandAgent는 키보드가 없어도 동작한다)
                foreach (var k in copy.GetComponentsInChildren<KeyboardControl>()) Object.DestroyImmediate(k);
                Undo.RegisterCreatedObjectUndo(copy, "학습 영역 복제");
            }
            Debug.Log("[Humanoid] 학습 영역 9개. 되돌리려면 'Humanoid > 학습 영역 1개로'. 저장소에는 1개 상태로 둔다.");
        }

        [MenuItem("Humanoid/학습 영역 1개로", false, 6)]
        static void RemoveClones()
        {
            foreach (var go in Object.FindObjectsByType<GameObject>(FindObjectsSortMode.None))
                if (go != null && go.transform.parent == null && go.name.StartsWith(AreaName + " "))  // 앞서 지운 영역의 자식은 null
                    Undo.DestroyObjectImmediate(go);
        }
    }
}
