using Humanoid;
using Unity.MLAgents.Policies;
using UnityEditor;
using UnityEngine;
using static HumanoidEditor.HumanoidBuild;

namespace HumanoidEditor
{
    /// <summary>4장 — 서기 에이전트를 걷기 에이전트로 바꾸고 목표를 놓는다. 설명은 Docs/04-걷기-보상.md</summary>
    internal static class Ch4_Walk
    {
        [MenuItem("Humanoid/4. 걷기 에이전트로 바꾸기", false, 4)]
        static void Setup()
        {
            var root = Find("Humanoid");
            if (root == null) return;
            var go = root.gameObject;

            // DecisionRequester가 Agent를 요구해서, 새 Agent를 먼저 붙이고 옛 것을 지운다.
            var walk = GetOrAdd<WalkAgent>(go);
            foreach (var a in go.GetComponents<StandAgent>())
                if (!(a is WalkAgent)) Undo.DestroyObjectImmediate(a);

            var target = root.parent.Find("Target");    // 처음엔 없다
            if (target == null)
            {
                var t = GameObject.CreatePrimitive(PrimitiveType.Cube);
                t.name = "Target";
                t.transform.SetParent(root.parent, false);
                t.transform.localPosition = new Vector3(0f, 0.25f, 6f);
                t.transform.localScale = Vector3.one * 0.5f;
                t.GetComponent<Renderer>().sharedMaterial = Mat("M_WalkTarget", new Color(0.95f, 0.3f, 0.3f));
                Object.DestroyImmediate(t.GetComponent<Collider>());   // 닿음 판정은 거리로 한다. 걸려 넘어지지 않게
                Undo.RegisterCreatedObjectUndo(t, "목표");
                target = t.transform;
            }

            walk.target = target;
            walk.targetSpeed = 1f;
            walk.pushInterval = 0;      // 한 번에 하나씩: 걷기를 배우는 동안은 밀지 않는다
            walk.MaxStep = 2000;        // 40초

            var bp = go.GetComponent<BehaviorParameters>();
            bp.BehaviorName = "HumanoidWalk";
            bp.BrainParameters.VectorObservationSize = 39;      // 36 + 목표 방향 2 + 목표 속도 1

            EditorUtility.SetDirty(go);
            Debug.Log("[Humanoid] 4장 완료 — 관측 39 / 행동 12. mlagents-learn Assets/Humanoid/humanoid_config.yaml --run-id=walk");
        }
    }
}
