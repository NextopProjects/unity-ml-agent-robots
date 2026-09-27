using System.IO;
using UnityEditor;
using UnityEngine;

namespace RobotArmsEditor
{
    /// <summary>
    /// 챕터별 빌더들이 함께 쓰는 치수와 도우미 함수.
    ///
    /// 팔의 치수를 바꾸고 싶으면 여기 상수만 고치면 된다.
    /// </summary>
    internal static class RobotArmsBuild
    {
        // ── 팔 치수 ──────────────────────────────────────────────────
        public const float BaseHeight = 0.30f;      // 받침대 높이 = 어깨 높이
        public const float Link1Length = 1.50f;     // 팔 윗부분
        public const float Link2Length = 1.20f;     // 팔 아랫부분
        public const float Reach = Link1Length + Link2Length;   // 2.7

        // ── 물체가 나타나는 구역 ──────────────────────────────────────
        // 팔이 접혔을 때 닿는 최소 거리가 약 1.17이라 1.2부터 시작한다.
        public static readonly Vector2 SpawnDistanceRange = new Vector2(1.2f, 2.4f);

        // 좌우 각도. 팔이 좌우로 돌 수 있으므로 부채꼴 구역이 된다.
        public static readonly Vector2 SpawnYawRange = new Vector2(-60f, 60f);

        public const float SpawnHeight = 0.10f;     // 물체(한 변 0.2) 절반만큼 테이블 위로

        public const string AreaName = "TrainingArea";
        const string MaterialFolder = "Assets/RobotArms/Materials";

        // ── 머티리얼 ─────────────────────────────────────────────────
        // 이름만 봐도 어디에 쓰이는지 알 수 있게 용도로 이름을 짓는다.
        // (색상 코드로 이름을 지으면 나중에 색을 바꿀 때 이름이 거짓말이 된다)

        public static Material TableMaterial        => Mat("M_Table",        0.72f, 0.72f, 0.75f);
        public static Material ArmBaseMaterial      => Mat("M_ArmBase",      0.35f, 0.35f, 0.40f);
        public static Material TurntableMaterial    => Mat("M_Turntable",    0.55f, 0.45f, 0.30f);
        public static Material UpperArmMaterial     => Mat("M_UpperArm",     0.30f, 0.55f, 0.85f);
        public static Material ForeArmMaterial      => Mat("M_ForeArm",      0.35f, 0.70f, 0.95f);
        public static Material TipMaterial          => Mat("M_Tip",          0.95f, 0.80f, 0.25f);
        public static Material GripperMaterial      => Mat("M_Gripper",      0.80f, 0.80f, 0.85f);
        public static Material GripPadMaterial      => Mat("M_GripPad",      0.25f, 0.25f, 0.28f);
        public static Material TargetObjectMaterial => Mat("M_TargetObject", 0.95f, 0.50f, 0.20f);
        public static Material PlaceTargetMaterial  => Mat("M_PlaceTarget",  0.35f, 0.85f, 0.45f);

        // ── 씬에서 부품 찾기 ─────────────────────────────────────────

        /// <summary>이름으로 찾는다. 계층 구조가 조금 바뀌어도 경로가 안 깨지도록 이름으로 찾는다.</summary>
        public static Transform Find(string name)
        {
            var areaGo = GameObject.Find(AreaName);
            if (areaGo == null) return null;

            foreach (var t in areaGo.GetComponentsInChildren<Transform>(true))
            {
                if (t.name == name) return t;
            }
            return null;
        }

        /// <summary>필요한 부품이 다 있는지 확인한다. 없으면 안내 창을 띄운다.</summary>
        public static bool RequireParts(params string[] names)
        {
            if (GameObject.Find(AreaName) == null)
            {
                EditorUtility.DisplayDialog("TrainingArea가 없습니다",
                    "먼저 'RobotArms > 1. 환경 만들기'를 실행하세요.", "확인");
                return false;
            }

            foreach (var n in names)
            {
                if (Find(n) == null)
                {
                    EditorUtility.DisplayDialog("씬 구조가 다릅니다",
                        "'" + n + "' 을(를) 찾지 못했습니다.\n" +
                        "'1. 환경 만들기'로 다시 구성해 보세요.", "확인");
                    return false;
                }
            }
            return true;
        }

        public static T GetOrAdd<T>(GameObject go) where T : Component
        {
            var c = go.GetComponent<T>();
            if (c == null) c = Undo.AddComponent<T>(go);
            return c;
        }

        // ── 오브젝트 만들기 ──────────────────────────────────────────

        public static GameObject CreateMesh(PrimitiveType type, string name, Transform parent,
            Vector3 localPosition, Vector3 localScale, Material material, bool keepCollider = false)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPosition;
            go.transform.localScale = localScale;

            if (material != null) go.GetComponent<Renderer>().sharedMaterial = material;

            if (!keepCollider)
            {
                // 팔은 Transform으로 직접 회전시키는 "운동학" 방식이라 물리가 필요 없다.
                // 콜라이더를 남겨두면 물체를 엉뚱하게 밀어내므로 지운다.
                // (5장에서 그리퍼 손가락에는 콜라이더를 일부러 붙인다)
                var collider = go.GetComponent<Collider>();
                if (collider != null) Object.DestroyImmediate(collider);
            }

            return go;
        }

        /// <summary>용도 이름으로 머티리얼 에셋을 만들어 재사용한다.</summary>
        static Material Mat(string name, float r, float g, float b)
        {
            Directory.CreateDirectory(MaterialFolder);
            string path = MaterialFolder + "/" + name + ".mat";

            var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing != null) return existing;

            var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            if (shader == null) return null;

            var material = new Material(shader) { color = new Color(r, g, b) };
            AssetDatabase.CreateAsset(material, path);
            return material;
        }
    }
}
