using UnityEngine;

namespace RobotArms
{
    /// <summary>
    /// 손가락 하나가 물체에 닿았는지만 기록한다. (5장)
    ///
    /// 하는 일이 이것뿐이다. 잡을지 말지는 에이전트가 판단한다.
    /// "감지하는 것"과 "판단하는 것"을 나눠두면, 나중에 손가락을 늘리거나
    /// 물체를 바꿔도 에이전트를 고치지 않아도 된다.
    ///
    /// ── 왜 OnCollisionEnter / OnTriggerEnter를 쓰지 않는가 ───────────
    ///
    /// 처음에는 트리거 콜백으로 만들었다. 그런데 전혀 감지되지 않았다.
    /// 원인은 <b>잠든 Rigidbody</b>였다.
    ///
    ///   물체가 테이블 위에서 멈춰 있으면 물리 엔진이 sleep 상태로 재운다.
    ///   잠든 물체에 콜라이더를 밀어 넣어도 <b>충돌/트리거 이벤트가 오지 않는다.</b>
    ///   학습 중에는 물체가 거의 항상 잠들어 있으니 영영 잡을 수 없게 된다.
    ///
    /// 그래서 콜백을 기다리지 않고 <b>매 스텝 직접 겹침을 검사</b>한다.
    ///   - 잠들어 있든 말든 상관없다
    ///   - 물체를 순간이동시켜도 즉시 반영된다
    ///   - 프레임 순서에 의존하지 않는다
    /// </summary>
    public class FingerContact : MonoBehaviour
    {
        [Tooltip("이 물체에 닿았는지만 본다. 테이블이나 팔은 무시한다.")]
        public Transform target;

        [Tooltip("손가락 표면에서 이만큼 떨어져 있어도 '닿았다'고 본다.\n" +
                 "손가락이 실제 충돌 콜라이더라 물체를 파고들 수 없으므로, " +
                 "표면이 맞닿은 상태를 잡으려면 약간의 여유가 필요하다.")]
        public float contactMargin = 0.015f;

        /// <summary>지금 물체에 닿아 있는가.</summary>
        public bool Touching { get; private set; }

        // 손가락은 부품 여러 개(팔·패드·갈고리)로 이루어져 있다.
        // 그 중 하나라도 물체에 닿으면 닿은 것으로 본다.
        BoxCollider[] _boxes;
        Collider _targetCollider;
        readonly Collider[] _hits = new Collider[8];

        void Awake()
        {
            _boxes = GetComponentsInChildren<BoxCollider>();
        }

        /// <summary>
        /// 지금 닿아 있는지 다시 계산한다.
        /// 손가락을 움직인 <see cref="PincerGripper"/>가 매 스텝 불러준다.
        /// </summary>
        public void UpdateContact()
        {
            Touching = false;

            if (target == null) return;
            if (_boxes == null || _boxes.Length == 0) _boxes = GetComponentsInChildren<BoxCollider>();

            if (_targetCollider == null)
            {
                _targetCollider = target.GetComponent<Collider>();
                if (_targetCollider == null) return;
            }

            foreach (var box in _boxes)
            {
                if (box == null) continue;

                var t = box.transform;
                Vector3 center = t.TransformPoint(box.center);
                Vector3 half = Vector3.Scale(box.size, t.lossyScale) * 0.5f
                               + Vector3.one * contactMargin;

                int count = Physics.OverlapBoxNonAlloc(
                    center, half, _hits, t.rotation, ~0, QueryTriggerInteraction.Collide);

                for (int i = 0; i < count; i++)
                {
                    if (_hits[i] == _targetCollider) { Touching = true; return; }
                }
            }
        }

        /// <summary>에피소드가 새로 시작될 때 상태를 지운다.</summary>
        public void ResetContact()
        {
            Touching = false;
        }
    }
}
