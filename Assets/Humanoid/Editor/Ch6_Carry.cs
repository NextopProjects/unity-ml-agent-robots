using Humanoid;
using Unity.MLAgents.Policies;
using UnityEditor;
using UnityEngine;
using static HumanoidEditor.HumanoidBuild;

namespace HumanoidEditor
{
    // 상자 · 작업대 두 개를 놓고 걷기 에이전트를 옮기기 에이전트(CarryAgent)로 바꾼다. 설명은 Docs/06-집어서-옮기기.md
    // 튜토리얼 창의 [대신 해주기]가 조각(Table · Marker · Box · Agent)을 하나씩 쓰고, 메뉴는 전체를 한 번에 만든다.
    internal static class Ch6_Carry
    {
        [MenuItem("Humanoid/6. 집어서 옮기기 환경 만들기", false, 7)]
        static void Setup()
        {
            var root = Find("Humanoid");
            if (root == null) return;
            var area = root.parent;

            Table(area, "PickTable", new Vector3(0f, 0f, 0.45f));
            Marker(Table(area, "PlaceTable", new Vector3(0.8f, 0f, 0.45f)));
            Box(area);
            Agent(root);
            Debug.Log("[Humanoid] 집어서 옮기기 — 관측 65 / 행동 18. mlagents-learn Assets/Humanoid/humanoid_carry_config.yaml --run-id=carry");
        }

        // 걷기 에이전트 → 옮기기 에이전트. 상자 · 작업대 · 표시를 연결하고 두뇌 이름 · 관측 수를 바꾼다. 4~5장의 걷기 목표는 지운다.
        public static void Agent(Transform root)
        {
            var area = root.parent;
            var go = root.gameObject;
            var target = area.Find("Target");
            if (target != null) Undo.DestroyObjectImmediate(target.gameObject);

            // 새 Agent 를 먼저 붙이고 옛 것을 지운다 (DecisionRequester 가 Agent 를 요구한다)
            var carry = GetOrAdd<CarryAgent>(go);
            foreach (var a in go.GetComponents<StandAgent>())
                if (!(a is CarryAgent)) Undo.DestroyObjectImmediate(a);
            Undo.RecordObject(carry, "옮기기 에이전트");
            carry.box = area.Find("Box").GetComponent<CarryBox>();
            carry.pickTable = area.Find("PickTable");
            carry.placeTable = area.Find("PlaceTable");
            carry.placeMarker = carry.placeTable.Find("PlaceMarker").GetComponent<Renderer>();
            carry.pushInterval = 0;
            carry.MaxStep = 3000;        // 60초. 상자를 떨어뜨리거나 넘어지거나 성공하면 그 전에 끝난다

            var bp = go.GetComponent<BehaviorParameters>();
            Undo.RecordObject(bp, "두뇌 설정");
            bp.BehaviorName = "HumanoidCarry";
            bp.BrainParameters.VectorObservationSize = 65;   // 서기 48 + 상자 · 놓을 자리 17

            EditorUtility.SetDirty(go);
        }

        // 작업대: 윗면 높이 1.06 (서서 팔을 앞으로 뻗은 높이에 상자 가운데가 오게).
        // 움직이지 않는(kinematic) Rigidbody 를 단다 — Rigidbody 가 없으면 BodyPart 가 "바닥"으로 보아, 팔이 닿기만 해도 넘어짐이 된다.
        // 윗면 0.25 (상자 0.30 보다 좁다). 놓을 작업대만 0.5 로 넓혀 봤더니 몸 앞에 붙어 부딪혀 오히려 나빠졌다
        // (실측: 난이도 0 성공 75% → 50% · 넘어짐 3% → 15~20%). 그래서 둘 다 0.25.
        public static Transform Table(Transform area, string name, Vector3 local)
        {
            var old = area.Find(name);
            if (old != null) Undo.DestroyObjectImmediate(old.gameObject);
            var t = GameObject.CreatePrimitive(PrimitiveType.Cube);
            t.name = name;
            t.transform.SetParent(area, false);
            t.transform.localPosition = local + Vector3.up * 0.53f;
            t.transform.localScale = new Vector3(0.25f, 1.06f, 0.25f);
            t.GetComponent<Renderer>().sharedMaterial = Mat("M_CarryTable", new Color(0.45f, 0.4f, 0.35f));
            t.AddComponent<Rigidbody>().isKinematic = true;
            Undo.RegisterCreatedObjectUndo(t, name);
            return t.transform;
        }

        // 놓을 자리 표시: 작업대 윗면의 얇은 원판 (콜라이더 없음). 색 = 지난 판 결과
        public static Renderer Marker(Transform place)
        {
            var m = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            m.name = "PlaceMarker";
            m.transform.SetParent(place, false);
            m.transform.localPosition = new Vector3(0f, 0.5f, 0f);
            m.transform.localScale = new Vector3(0.3f / place.localScale.x, 0.005f, 0.3f / place.localScale.z);     // 부모 크기로 나눠 지름 0.3 (= 성공 반경 0.15)
            m.GetComponent<Renderer>().sharedMaterial = Mat("M_PlaceMarker", Color.gray);
            Object.DestroyImmediate(m.GetComponent<Collider>());
            Undo.RegisterCreatedObjectUndo(m, "놓을 자리 표시");
            return m.GetComponent<Renderer>();
        }

        // 상자 겉면: 마찰 1.0 (고무를 씌운 상자). 기본값 0.6 이면 들고 옮기는 동안 몸이 흔들려 양팔 사이에서 미끄러진다
        // (실측: 옮기는 판의 30~47% 가 떨어뜨림으로 끝났다). Maximum — 닿은 쪽 중 큰 마찰을 쓴다.
        public static PhysicsMaterial Grip()
        {
            const string path = "Assets/Humanoid/Materials/PM_CarryBox.asset";
            var m = AssetDatabase.LoadAssetAtPath<PhysicsMaterial>(path);
            if (m != null) return m;
            m = new PhysicsMaterial("PM_CarryBox") { staticFriction = 1f, dynamicFriction = 1f, frictionCombine = PhysicsMaterialCombine.Maximum };
            AssetDatabase.CreateAsset(m, path);
            return m;
        }

        // 상자 0.30 × 0.20 × 0.20 · 1kg. 양팔 사이(아래팔 안쪽 간격 약 0.39)에 조여서 들 수 있는 너비 (실측: 0.26~0.34, 1~2kg 버팀)
        public static CarryBox Box(Transform area)
        {
            var old = area.Find("Box");
            if (old != null) Undo.DestroyObjectImmediate(old.gameObject);
            var b = GameObject.CreatePrimitive(PrimitiveType.Cube);
            b.name = "Box";
            b.transform.SetParent(area, false);
            b.transform.localPosition = new Vector3(0f, 1.16f, 0.45f);
            b.transform.localScale = new Vector3(0.30f, 0.20f, 0.20f);
            b.GetComponent<Renderer>().sharedMaterial = Mat("M_CarryBox", new Color(0.95f, 0.6f, 0.2f));
            b.AddComponent<Rigidbody>().mass = 1f;
            b.GetComponent<Collider>().sharedMaterial = Grip();
            var box = b.AddComponent<CarryBox>();
            Undo.RegisterCreatedObjectUndo(b, "상자");
            return box;
        }
    }
}
