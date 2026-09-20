using UnityEditor;
using UnityEngine;
using static RobotArmsEditor.RobotArmsBuild;

namespace RobotArmsEditor
{
    /// <summary>
    /// 1장 — 환경 구성.
    /// 3관절 로봇 팔, 테이블, 물체, 놓을 자리를 배치한다. 아직 스크립트는 붙이지 않는다.
    /// </summary>
    internal static class Ch1_Environment
    {
        [MenuItem("RobotArms/1. 환경 만들기 (씬 구성)", false, 1)]
        static void Build()
        {
            var existing = GameObject.Find(AreaName);
            if (existing != null)
            {
                bool replace = EditorUtility.DisplayDialog(
                    "TrainingArea가 이미 있습니다",
                    "기존 TrainingArea를 지우고 새로 만들까요?",
                    "새로 만들기", "취소");

                if (!replace) return;
                Undo.DestroyObjectImmediate(existing);
            }

            var area = new GameObject(AreaName);
            Undo.RegisterCreatedObjectUndo(area, "RobotArms 환경 만들기");

            // ── 테이블 ──
            // 윗면이 y = 0 이 되도록 둔다. 팔이 좌우로 돌기 때문에 가로로 넉넉하게 잡는다.
            CreateMesh(PrimitiveType.Cube, "Table", area.transform,
                new Vector3(0f, -0.05f, 1.2f), new Vector3(5.0f, 0.1f, 4.0f),
                TableMaterial, keepCollider: true);

            // ── 팔 ──
            var arm = new GameObject("RobotArm");
            arm.transform.SetParent(area.transform, false);

            // Base: 바닥에 고정된 받침대. 2장부터 스크립트가 여기에 붙는다.
            //
            // 중요 — Base 자체는 스케일 1인 "빈 오브젝트"로 둔다.
            // 여기에 직접 원기둥을 붙여 스케일을 주면, 그 밑에 달린 관절과 링크가
            // 전부 같은 비율로 찌그러진다. 그래서 보이는 몸통은 BaseMesh로 따로 뺀다.
            var baseGo = new GameObject("Base");
            baseGo.transform.SetParent(arm.transform, false);

            CreateMesh(PrimitiveType.Cylinder, "BaseMesh", baseGo.transform,
                new Vector3(0f, BaseHeight * 0.5f, 0f),
                new Vector3(0.45f, BaseHeight * 0.5f, 0.45f), ArmBaseMaterial);

            // J0: 좌우 회전 관절. Y축을 기준으로 돈다.
            //     실제 산업용 로봇 팔의 맨 아래 관절과 같은 역할이다.
            //     이것이 있어야 팔이 평면을 벗어나 3차원 공간에서 움직일 수 있다.
            var j0 = new GameObject("J0");
            j0.transform.SetParent(baseGo.transform, false);
            j0.transform.localPosition = new Vector3(0f, BaseHeight, 0f);

            // 돌아가는 턴테이블. 좌우로 돌고 있다는 것을 눈으로 보기 위한 것.
            CreateMesh(PrimitiveType.Cylinder, "J0Mesh", j0.transform,
                new Vector3(0f, 0.02f, 0f), new Vector3(0.40f, 0.02f, 0.40f),
                TurntableMaterial);

            // J1: 어깨. 위아래로 움직인다. X축을 기준으로 돈다.
            var j1 = new GameObject("J1");
            j1.transform.SetParent(j0.transform, false);

            // Link1: 어깨에서 뻗어나가는 막대. 로컬 +Z 방향으로 눕혀 놓는다.
            // (원기둥의 축은 기본이 Y라서, X로 90도 돌려 +Z를 향하게 한다)
            var link1 = CreateMesh(PrimitiveType.Cylinder, "Link1", j1.transform,
                new Vector3(0f, 0f, Link1Length * 0.5f),
                new Vector3(0.13f, Link1Length * 0.5f, 0.13f), UpperArmMaterial);
            link1.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);

            // J2: 팔꿈치. 역시 위아래. Link1 끝에 놓는다.
            var j2 = new GameObject("J2");
            j2.transform.SetParent(j1.transform, false);
            j2.transform.localPosition = new Vector3(0f, 0f, Link1Length);

            var link2 = CreateMesh(PrimitiveType.Cylinder, "Link2", j2.transform,
                new Vector3(0f, 0f, Link2Length * 0.5f),
                new Vector3(0.11f, Link2Length * 0.5f, 0.11f), ForeArmMaterial);
            link2.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);

            // Tip: 팔 끝 = 물체를 집는 지점.
            //
            // Base와 똑같은 이유로 Tip도 스케일 1인 빈 오브젝트로 둔다.
            // 4장에서 물체를 Tip의 자식으로 붙일 텐데, Tip에 스케일이 있으면
            // 물체가 그 비율만큼 쪼그라든다.
            var tip = new GameObject("Tip");
            tip.transform.SetParent(j2.transform, false);
            tip.transform.localPosition = new Vector3(0f, 0f, Link2Length);

            CreateMesh(PrimitiveType.Sphere, "TipMesh", tip.transform,
                Vector3.zero, Vector3.one * 0.14f, TipMaterial);

            // ── 물체 ──
            var obj = CreateMesh(PrimitiveType.Cube, "Object", area.transform,
                new Vector3(0f, SpawnHeight, 1.8f), Vector3.one * 0.2f,
                TargetObjectMaterial, keepCollider: true);
            var rb = obj.AddComponent<Rigidbody>();
            rb.mass = 0.2f;

            // ── 놓을 자리 (6장에서 사용) ──
            CreateMesh(PrimitiveType.Cylinder, "PlaceTarget", area.transform,
                new Vector3(-1.2f, 0.01f, 1.2f), new Vector3(0.35f, 0.01f, 0.35f),
                PlaceTargetMaterial);

            Selection.activeGameObject = area;
            Debug.Log(
                "[RobotArms] 1장 완료 — 환경을 만들었습니다.\n" +
                "  관절 3개 (J0 좌우 / J1 어깨 / J2 팔꿈치) = 3 DOF\n" +
                "  팔이 닿는 최대 거리 = " + Link1Length + " + " + Link2Length + " = " + Reach + "\n" +
                "  다음: 메뉴 > RobotArms > 2. 관절 제어 붙이기");
        }
    }
}
