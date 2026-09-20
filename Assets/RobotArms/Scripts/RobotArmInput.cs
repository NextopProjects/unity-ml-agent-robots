using UnityEngine;
using UnityEngine.InputSystem;

namespace RobotArms
{
    /// <summary>
    /// 키보드 조작을 한 곳에 모아둔다. (챕터 2)
    ///
    /// 챕터 2의 <see cref="ManualArmDriver"/>와
    /// 챕터 3의 <see cref="RobotArmAgent.Heuristic"/>이 **똑같이** 이걸 쓴다.
    /// 조작 키를 바꾸고 싶으면 여기만 고치면 된다.
    ///
    /// RollerBall과 같은 새 Input System(<c>Keyboard.current</c>)을 쓴다.
    /// </summary>
    public static class RobotArmInput
    {
        /// <summary>관절1(어깨). Q = 위로, A = 아래로.</summary>
        public static float Joint1 => Axis(Key.Q, Key.A);

        /// <summary>관절2(팔꿈치). W = 위로, S = 아래로.</summary>
        public static float Joint2 => Axis(Key.W, Key.S);

        /// <summary>관절3(베이스 회전). 챕터 7의 3D 확장에서만 쓴다.</summary>
        public static float Joint3 => Axis(Key.E, Key.D);

        /// <summary>잡기. Space를 누르고 있는 동안 잡는다. (챕터 4부터)</summary>
        public static bool Grip => Keyboard.current != null && Keyboard.current.spaceKey.isPressed;

        /// <summary>자세 초기화. R을 누른 순간 한 번.</summary>
        public static bool ResetPressed =>
            Keyboard.current != null && Keyboard.current.rKey.wasPressedThisFrame;

        /// <summary>도움말 켜기/끄기. F1을 누른 순간 한 번.</summary>
        public static bool ToggleHelpPressed =>
            Keyboard.current != null && Keyboard.current.f1Key.wasPressedThisFrame;

        // ── 화면에 표시할 안내 문구 ──────────────────────────────────
        // 조작 키와 안내 문구를 같은 파일에 두어야 서로 어긋나지 않는다.

        /// <summary>i번 관절을 움직이는 키 이름.</summary>
        public static string KeyLabel(int index)
        {
            switch (index)
            {
                case 0: return "Q / A";
                case 1: return "W / S";
                case 2: return "E / D";
                default: return "-";
            }
        }

        public const string GripKeyLabel = "Space";
        public const string ResetKeyLabel = "R";
        public const string ToggleHelpKeyLabel = "F1";

        /// <summary>i번 관절의 입력값. 없으면 0.</summary>
        public static float Joint(int index)
        {
            switch (index)
            {
                case 0: return Joint1;
                case 1: return Joint2;
                case 2: return Joint3;
                default: return 0f;
            }
        }

        static float Axis(Key positive, Key negative)
        {
            var kb = Keyboard.current;
            if (kb == null) return 0f;

            return (kb[positive].isPressed ? 1f : 0f) - (kb[negative].isPressed ? 1f : 0f);
        }
    }
}
