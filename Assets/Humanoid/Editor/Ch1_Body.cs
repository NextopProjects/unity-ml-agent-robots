using Humanoid;
using UnityEditor;
using UnityEngine;
using static HumanoidEditor.HumanoidBuild;
using BodyPart = Humanoid.BodyPart;     // UnityEditor에도 같은 이름이 있다

namespace HumanoidEditor
{
    // 학습 환경(영역 · 바닥 · 휴머노이드 부품 12 · 관절 11). 관절에 힘은 아직 없다(래그돌).
    // 튜토리얼 창의 [대신 해주기]가 조각 하나씩 쓰고, 메뉴는 전체를 한 번에 만든다(정답 씬).
    internal static class Ch1_Body
    {
        //   머리 꼭대기 1.69
        //   몸통 1.05~1.45   ← 허리 관절 1.04
        //   위팔 1.16~1.42   ← 어깨 관절 1.42 (좌우 ±0.23)
        //   아래팔 0.90~1.16 ← 팔꿈치 관절 1.16
        //   골반 0.88~1.04   ← 엉덩이 관절 0.90 (좌우 ±0.10)
        //   허벅지 0.48~0.90 ← 무릎 관절 0.48
        //   정강이 0.08~0.48 ← 발목 관절 0.08
        //   발 0~0.06
        // 모든 부품이 회전 없이(정면 +Z) 서 있다 → "관절 각도 0 = 차렷 자세"

        [MenuItem("Humanoid/1. 학습 환경 만들기 (정답 씬)", false, 1)]
        static void Build()
        {
            var old = GameObject.Find(AreaName);
            if (old != null) Undo.DestroyObjectImmediate(old);     // Ctrl+Z로 되돌릴 수 있다

            var area = Area();
            Ground(area);
            var root = Root(area);
            var pelvis = Pelvis(root);
            var torso = Torso(pelvis);
            WaistJoint(torso);
            var thighL = Thigh(pelvis, -0.10f);
            HipJoint(thighL);
            var upperArmL = UpperArm(torso, -0.23f);
            ShoulderJoint(upperArmL);
            RestOfLegs(thighL);
            RestOfArms(upperArmL);
            Head(torso);
            AddVisuals(root);
            Wire(root);
            PlaceCamera();

            Selection.activeGameObject = root;
            Debug.Log("[Humanoid] 1장 정답 씬 — 부품 12 · 관절 11(18축) · 44kg. Play하면 래그돌처럼 무너지는 것이 정상입니다.");
        }

        // ── 조각 ────────────────────────────────────────────────────

        public static GameObject Area()
        {
            var area = new GameObject(AreaName);
            Undo.RegisterCreatedObjectUndo(area, "학습 영역");
            return area;
        }

        // 바닥: 윗면 y = 0. Rigidbody가 없으니 BodyPart가 "바닥"으로 알아본다.
        public static GameObject Ground(GameObject area)
        {
            var ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
            ground.name = "Ground";
            ground.transform.SetParent(area.transform, false);
            ground.transform.localPosition = new Vector3(0f, -0.5f, 0f);
            ground.transform.localScale = new Vector3(40f, 1f, 40f);
            ground.GetComponent<Renderer>().sharedMaterial = Mat("M_HumanoidGround", new Color(0.55f, 0.58f, 0.6f));
            Undo.RegisterCreatedObjectUndo(ground, "바닥");
            return ground;
        }

        // Humanoid: 스케일 1인 빈 오브젝트. 몸 전체를 다루는 스크립트가 붙는다.
        public static GameObject Root(GameObject area)
        {
            var root = new GameObject("Humanoid");
            root.transform.SetParent(area.transform, false);
            Undo.RegisterCreatedObjectUndo(root, "Humanoid");
            return root;
        }

        public static GameObject Pelvis(GameObject root) =>
            Part("Pelvis", root, new Vector3(0f, 0.96f, 0f), new Vector3(0.30f, 0.16f, 0.18f), 8f);

        // 몸통(머리 포함 9kg).
        public static GameObject Torso(GameObject pelvis) =>
            Part("Torso", pelvis, new Vector3(0f, 1.25f, 0f), new Vector3(0.32f, 0.40f, 0.20f), 9f);

        // 허리: 앞으로 40 · 뒤로 20 · 좌우 20
        public static void WaistJoint(GameObject torso) =>
            Joint(torso, torso.transform.parent.gameObject, 1.04f, -40f, 20f, 20f);

        // 머리는 몸통에 붙은 장식이다(관절 없음).
        // 콜라이더는 둔다. 머리가 바닥에 닿으면 넘어진 것이다.
        public static void Head(GameObject torso)
        {
            var head = new GameObject("Head");
            head.transform.SetParent(torso.transform, false);
            head.transform.localPosition = new Vector3(0f, 0.33f, 0f);
            head.AddComponent<SphereCollider>().radius = 0.11f;
            Visual(PrimitiveType.Sphere, head.transform, Vector3.zero, Vector3.one * 0.22f, Mat("M_HumanoidHead", new Color(0.92f, 0.85f, 0.75f)));
            // 얼굴 띠: 어느 쪽이 앞인지 보이게
            Visual(PrimitiveType.Cube, head.transform, new Vector3(0f, 0.02f, 0.09f), new Vector3(0.16f, 0.05f, 0.05f), Mat("M_HumanoidFace", Color.black));
            Undo.RegisterCreatedObjectUndo(head, "머리");
        }

        // 위팔 1.5kg. 몸통 옆면(±0.16)에서 0.03 띄운다 — 붙이면 아래팔이 몸통에 걸린다.
        public static GameObject UpperArm(GameObject torso, float x) =>
            Part("UpperArm" + (x < 0 ? "L" : "R"), torso, new Vector3(x, 1.29f, 0f), new Vector3(0.08f, 0.26f, 0.08f), 1.5f);

        // 어깨: 뒤로 60 · 앞으로 120 · 좌우 30. 팔도 다리처럼 아래로 매달려 있어 X+ = 팔을 앞으로.
        public static void ShoulderJoint(GameObject upperArm) =>
            Joint(upperArm, upperArm.transform.parent.gameObject, 1.42f, -60f, 120f, 30f);

        // 왼 위팔 아래(아래팔)와 오른팔 전체 — 다리와 같은 이유로 한 번에 만든다.
        public static void RestOfArms(GameObject upperArmL)
        {
            var torso = upperArmL.transform.parent.gameObject;
            Forearm(upperArmL, -0.23f);
            var upperArmR = UpperArm(torso, 0.23f);
            ShoulderJoint(upperArmR);
            Forearm(upperArmR, 0.23f);
        }

        // 아래팔 1kg. 팔꿈치는 앞으로 접기만(1축) 0 ~ 130. 무릎(-120 ~ 0)과 접히는 방향이 반대다.
        public static GameObject Forearm(GameObject upperArm, float x)
        {
            var forearm = Part("Forearm" + (x < 0 ? "L" : "R"), upperArm, new Vector3(x, 1.03f, 0f), new Vector3(0.07f, 0.26f, 0.07f), 1f);
            Joint(forearm, upperArm, 1.16f, 0f, 130f, 0f);
            return forearm;
        }

        public static GameObject Thigh(GameObject pelvis, float x) =>
            Part("Thigh" + (x < 0 ? "L" : "R"), pelvis, new Vector3(x, 0.69f, 0f), new Vector3(0.12f, 0.42f, 0.12f), 5f);

        // 엉덩이: 뒤로 30 · 앞으로 90 · 좌우 25
        public static void HipJoint(GameObject thigh) =>
            Joint(thigh, thigh.transform.parent.gameObject, 0.90f, -30f, 90f, 25f);

        // 왼 허벅지 아래(정강이·발)와 오른다리 전체 — 같은 방법의 반복이라 한 번에 만든다.
        public static void RestOfLegs(GameObject thighL)
        {
            var pelvis = thighL.transform.parent.gameObject;
            LowerLeg(thighL, -0.10f);
            var thighR = Thigh(pelvis, 0.10f);
            HipJoint(thighR);
            LowerLeg(thighR, 0.10f);
        }

        // 정강이 → 발. 무릎은 접기만(1축), 발목 앞뒤 35 · 좌우 20.
        public static GameObject LowerLeg(GameObject thigh, float x)
        {
            string side = x < 0 ? "L" : "R";
            var shin = Part("Shin" + side, thigh, new Vector3(x, 0.28f, 0f), new Vector3(0.10f, 0.40f, 0.10f), 3f);
            Joint(shin, thigh, 0.48f, -120f, 0f, 0f);

            // 발 3kg: 사람 비율(0.6kg)보다 훨씬 무겁다. 1kg이면 44kg을 떠받치는 발목 관절을
            // 물리 엔진이 다 풀지 못해 무릎만 굽혀도 발목이 꺾여 넘어진다. (실측: 오차 29도 → 2도)
            var foot = Part("Foot" + side, shin, new Vector3(x, 0.03f, 0.04f), new Vector3(0.12f, 0.06f, 0.24f), 3f);
            Joint(foot, shin, 0.08f, -35f, 35f, 20f);
            foot.GetComponent<BodyPart>().isFoot = true;
            return foot;
        }

        // 몸 스크립트(HumanoidBody)를 붙이고 부품을 연결한다.
        public static void Wire(GameObject root)
        {
            var body = GetOrAdd<HumanoidBody>(root);
            Undo.RecordObject(body, "부품 연결");
            body.pelvis = Part(root, "Pelvis");
            body.torso = Part(root, "Torso");
            body.footL = Part(root, "FootL");
            body.footR = Part(root, "FootR");
        }

        public static void PlaceCamera()
        {
            var cam = Camera.main;
            if (cam == null) return;
            Undo.RecordObject(cam.transform, "카메라");
            cam.transform.position = new Vector3(2.4f, 1.5f, 3.2f);
            cam.transform.LookAt(new Vector3(0f, 0.9f, 0f));
        }

        // 보이는 모양이 없는 부품에 상자 모양을 붙인다. 크기는 콜라이더와 같게.
        public static void AddVisuals(GameObject root)
        {
            foreach (var part in root.GetComponentsInChildren<BodyPart>())
            {
                if (part.transform.Find(part.name + "Mesh") != null) continue;
                string n = part.name;
                var mat = n.StartsWith("Foot") ? Mat("M_HumanoidFoot", new Color(0.2f, 0.2f, 0.22f))
                        : n.EndsWith("L") ? Mat("M_HumanoidLegL", new Color(0.95f, 0.6f, 0.25f))
                        : n.EndsWith("R") ? Mat("M_HumanoidLegR", new Color(0.35f, 0.8f, 0.45f))
                        : Mat("M_HumanoidBody", new Color(0.25f, 0.5f, 0.85f));
                var box = part.GetComponent<BoxCollider>();
                Visual(PrimitiveType.Cube, part.transform, box != null ? box.center : Vector3.zero, box != null ? box.size : Vector3.one * 0.1f, mat);
            }
        }

        // ── 내부 ────────────────────────────────────────────────────

        static BodyPart Part(GameObject root, string name)
        {
            foreach (var p in root.GetComponentsInChildren<BodyPart>()) if (p.name == name) return p;
            return null;
        }

        // 부품 = 스케일 1인 본체(Rigidbody + 상자 콜라이더 + BodyPart). 모양은 AddVisuals가 붙인다.
        static GameObject Part(string name, GameObject parent, Vector3 center, Vector3 size, float mass)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent.transform, false);
            go.transform.position = parent.transform.root.TransformPoint(center);
            go.AddComponent<BoxCollider>().size = size;     // 스케일 대신 size. 스케일을 주면 자식이 찌그러진다
            go.AddComponent<Rigidbody>().mass = mass;
            go.AddComponent<BodyPart>();
            Undo.RegisterCreatedObjectUndo(go, name);
            return go;
        }

        // 관절. axis = X(앞뒤 굽히기), secondaryAxis = Y(비틀기, 잠금), 나머지 Z = 좌우.
        // 범위는 "관절 공간" 각도. X+ = 다리를 앞으로 / 몸통을 뒤로 / 발끝을 위로.
        static void Joint(GameObject child, GameObject parent, float pivotY, float lowX, float highX, float z)
        {
            var j = child.AddComponent<ConfigurableJoint>();
            j.connectedBody = parent.GetComponent<Rigidbody>();
            // anchor는 자식 기준 로컬 좌표. 관절 점 = (부품의 x, pivotY, 0)
            Vector3 p = child.transform.position, root = child.transform.root.position;
            j.anchor = child.transform.InverseTransformPoint(new Vector3(p.x, root.y + pivotY, root.z));
            j.xMotion = j.yMotion = j.zMotion = ConfigurableJointMotion.Locked;   // 이동 없이 돌기만
            j.angularXMotion = ConfigurableJointMotion.Limited;
            j.angularYMotion = ConfigurableJointMotion.Locked;
            j.angularZMotion = z > 0f ? ConfigurableJointMotion.Limited : ConfigurableJointMotion.Locked;
            j.lowAngularXLimit = new SoftJointLimit { limit = lowX };
            j.highAngularXLimit = new SoftJointLimit { limit = highX };
            j.angularZLimit = new SoftJointLimit { limit = z };
        }
    }
}
