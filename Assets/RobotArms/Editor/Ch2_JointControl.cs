using RobotArms;
using UnityEditor;
using UnityEngine;
using static RobotArmsEditor.RobotArmsBuild;

namespace RobotArmsEditor
{
    /// <summary>
    /// 2장 — 관절 제어 붙이기.
    /// 키보드로 3개 관절을 움직일 수 있게 만든다. 아직 AI는 없다.
    /// </summary>
    internal static class Ch2_JointControl
    {
        [MenuItem("RobotArms/2. 관절 제어 붙이기", false, 2)]
        static void Setup()
        {
            if (!RequireParts("Base", "J0", "J1", "J2", "Tip", "Object")) return;

            var baseTf = Find("Base");
            var arm = GetOrAdd<RobotArmController>(baseTf.gameObject);
            Undo.RecordObject(arm, "관절 제어 설정");

            arm.tip = Find("Tip");
            arm.reach = Reach;
            arm.spawnDistanceRange = SpawnDistanceRange;
            arm.spawnYawRange = SpawnYawRange;
            arm.spawnHeight = SpawnHeight;

            // 관절 각도 범위만으로는 팔이 테이블을 뚫는 것을 막을 수 없다.
            // 두 관절의 "조합"에 따라 팔 끝이 바닥 아래로 내려가기 때문이다.
            arm.blockPenetration = true;
            arm.floorHeight = 0f;           // 테이블 윗면
            arm.clearance = 0.07f;          // 링크 굵기 + 팔 끝 구 반지름만큼 여유
            arm.obstacles = new[] { Find("Object") };
            arm.obstacleRadius = 0.16f;
            arm.tipExemptDistance = 0.28f;  // 물체를 집으러 가는 것은 막지 않는다
            arm.samplesPerLink = 6;

            // 배열 순서 = 액션 순서 = 키보드 순서다.
            //   0번 → Q/A,  1번 → W/S,  2번 → E/D
            //
            // 계층 구조에서는 J0가 맨 위(Base 바로 아래)에 있지만,
            // 배열에서는 맨 뒤에 둔다. 사람이 가장 많이 쓰는 어깨·팔꿈치를
            // 앞쪽 키에 배치하는 편이 조작하기 편하기 때문이다.
            arm.joints = new[]
            {
                new RobotArmController.Joint
                {
                    label = "어깨",
                    pivot = Find("J1"),     // 어깨 (위아래)
                    axis = Vector3.left,    // 각도가 +일 때 팔이 위로 올라간다
                    minAngle = -10f,
                    maxAngle = 100f,
                    maxSpeed = 120f,
                    startAngle = 45f,
                },
                new RobotArmController.Joint
                {
                    label = "팔꿈치",
                    pivot = Find("J2"),     // 팔꿈치 (위아래)
                    axis = Vector3.left,

                    // 아래로만 접힌다. 사람 팔에도 위로 꺾이는 팔꿈치는 없다.
                    // 위쪽을 열어두면 같은 지점에 가는 자세가 둘씩 생겨
                    // AI가 뒤져야 할 경우의 수만 넓어진다.
                    minAngle = -130f,
                    maxAngle = 0f,
                    maxSpeed = 180f,
                    startAngle = -60f,
                },
                new RobotArmController.Joint
                {
                    label = "좌우 회전",
                    pivot = Find("J0"),     // 좌우 회전
                    axis = Vector3.up,      // Y축 = 좌우
                    minAngle = -120f,
                    maxAngle = 120f,
                    maxSpeed = 120f,
                    startAngle = 0f,
                },
            };

            var driver = GetOrAdd<ManualArmDriver>(baseTf.gameObject);
            Undo.RecordObject(driver, "키보드 조작 설정");
            driver.distanceTo = Find("Object");
            driver.successDistance = 0.25f;
            driver.enabled = true;

            // 화면에 조작법과 관절 각도를 띄운다.
            // 각도를 보면서 움직여야 가동 범위가 어디까지인지 감이 온다.
            var hud = GetOrAdd<ArmControlHud>(baseTf.gameObject);
            Undo.RecordObject(hud, "조작 안내 UI 설정");
            hud.target = Find("Object");
            hud.successDistance = 0.25f;
            hud.showHelp = true;
            hud.fontSize = 15;

            EditorUtility.SetDirty(arm);
            EditorUtility.SetDirty(driver);
            EditorUtility.SetDirty(hud);
            Selection.activeGameObject = baseTf.gameObject;

            Debug.Log(
                "[RobotArms] 2장 완료 — 키보드로 팔을 움직일 수 있습니다. (3 DOF)\n" +
                "  Q/A = 어깨,  W/S = 팔꿈치,  E/D = 좌우 회전,  R = 자세 초기화\n" +
                "  Play하면 화면 왼쪽 위에 조작법과 관절 각도가 표시됩니다. (F1로 켜고 끄기)\n" +
                "  팔이 테이블 아래로 내려가거나 물체를 뚫으려 하면 그 방향으로는 멈춥니다.\n" +
                "  Play를 누르고 물체까지 팔 끝을 가져가 보세요. (Console에 '도달!'이 찍힙니다)\n" +
                "  사람이 못 하면 AI도 못 합니다. 여기서 반드시 확인하고 넘어가세요.");
        }
    }
}
