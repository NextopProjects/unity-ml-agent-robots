// F 상자 밀어 넣기 · M 시연 학습이 함께 쓰는 '게임 규칙'
window.PushGame = (function () {
  const { rnd, clamp, C } = RLPlay;
  const F = 10, W = 4.6, BR = 0.5, AR = 0.4, GS = 1.0; // 힘 · 벽 안쪽 · 상자/공 반지름 · 골 반 폭
  const inGoal = (s) => Math.abs(s.b[0] - s.g[0]) < GS && Math.abs(s.b[1] - s.g[1]) < GS;
  const gd = (s) => Math.hypot(s.g[0] - s.b[0], s.g[1] - s.b[1]);
  const def = {
    view: { x0: -5.5, x1: 5.5, y0: -5.5, y1: 5.5 }, maxStep: 1500, decisionPeriod: 5,
    obs: ["상자 − 공 x", "상자 − 공 z", "골 − 상자 x", "골 − 상자 z", "공 속도 x", "공 속도 z", "상자 속도 x", "상자 속도 z"],
    act: { kind: "continuous", names: ["x 방향 힘", "z 방향 힘"] },
    keys: [["W", "+z"], ["A", "−x"], ["S", "−z"], ["D", "+x"]],
    heuristic: (k) => [(k.has("D") ? 1 : 0) - (k.has("A") ? 1 : 0), (k.has("W") ? 1 : 0) - (k.has("S") ? 1 : 0)],
    reset(s) {
      s.b = [rnd(-3, 3), rnd(-3, 3)]; s.bv = [0, 0]; s.v = [0, 0];
      do { s.g = [rnd(-3.5, 3.5), rnd(-3.5, 3.5)]; } while (Math.hypot(s.g[0] - s.b[0], s.g[1] - s.b[1]) < 2.5);
      do { s.p = [rnd(-4, 4), rnd(-4, 4)]; } while (Math.hypot(s.p[0] - s.b[0], s.p[1] - s.b[1]) < 1.5);
      s.prev = gd(s); s.acc = 0; // 진전 보상용 '이전 거리' — 판마다 다시 잰다
    },
    observe: (s) => [s.b[0] - s.p[0], s.b[1] - s.p[1], s.g[0] - s.b[0], s.g[1] - s.b[1], s.v[0], s.v[1], s.bv[0], s.bv[1]],
    step(s, a, dt, E) {
      for (let i = 0; i < 2; i++) { s.v[i] += a[i] * F * dt; s.v[i] *= 1 - 0.8 * dt; s.p[i] += s.v[i] * dt; s.bv[i] *= 1 - 2.5 * dt; s.b[i] += s.bv[i] * dt; }
      // 공 ↔ 상자 충돌: 겹친 만큼 밀어내고, 공의 속도 일부를 상자에 넘긴다
      const e = [s.b[0] - s.p[0], s.b[1] - s.p[1]], r = Math.hypot(...e), m = BR + AR;
      if (r < m && r > 1e-6) {
        const n = [e[0] / r, e[1] / r], push = m - r;
        s.b[0] += n[0] * push * 0.5; s.b[1] += n[1] * push * 0.5; s.p[0] -= n[0] * push * 0.5; s.p[1] -= n[1] * push * 0.5;
        const rel = (s.v[0] - s.bv[0]) * n[0] + (s.v[1] - s.bv[1]) * n[1];
        if (rel > 0) { for (let i = 0; i < 2; i++) { s.bv[i] += n[i] * rel * 0.6; s.v[i] -= n[i] * rel * 0.6; } }
      }
      for (const [q, qv, rr] of [[s.p, s.v, AR], [s.b, s.bv, BR]]) for (let i = 0; i < 2; i++) { // 벽
        if (q[i] > W + 0.4 - rr) { q[i] = W + 0.4 - rr; qv[i] = -qv[i] * 0.3; }
        if (q[i] < -W - 0.4 + rr) { q[i] = -W - 0.4 + rr; qv[i] = -qv[i] * 0.3; }
      }
      if (inGoal(s)) return { reward: 1, event: "상자 골인", done: "상자 골인 — 성공" };
      const now = gd(s), r2 = clamp((s.prev - now) * 0.5, -0.02, 0.02); s.prev = now; s.acc += r2;
      if (E.step % 50 === 49 && Math.abs(s.acc) > 0.002) { const v = s.acc; s.acc = 0; return { reward: r2, event: "진전 보상 (최근 50스텝 합)", logValue: v }; }
      return { reward: r2 };
    },
    rule(s) { // 상자 뒤(골 반대편)로 돌아간 뒤 골 쪽으로 민다
      const gdir = [s.g[0] - s.b[0], s.g[1] - s.b[1]], L = Math.hypot(...gdir) || 1, u = [gdir[0] / L, gdir[1] / L];
      const behind = [s.b[0] - u[0] * 1.1, s.b[1] - u[1] * 1.1];
      const tob = [s.b[0] - s.p[0], s.b[1] - s.p[1]], ahead = tob[0] * u[0] + tob[1] * u[1]; // 공이 상자 뒤에 있나
      const side = Math.hypot(s.p[0] - behind[0], s.p[1] - behind[1]);
      let tgt;
      if (side > 0.45 || ahead < 0.6) { // 아직 뒤가 아니면 상자를 비켜 돌아간다
        tgt = behind.slice();
        const perp = [-u[1], u[0]], sd = (s.p[0] - s.b[0]) * perp[0] + (s.p[1] - s.b[1]) * perp[1];
        if (ahead < 0.3) { tgt[0] += perp[0] * Math.sign(sd || 1) * 1.4; tgt[1] += perp[1] * Math.sign(sd || 1) * 1.4; }
      } else tgt = [s.b[0] + u[0] * 2, s.b[1] + u[1] * 2];
      const d = [tgt[0] - s.p[0], tgt[1] - s.p[1]], dl = Math.hypot(...d) || 1, sp = Math.min(3, dl * 2.5);
      return [0, 1].map((i) => clamp((d[i] / dl * sp - s.v[i]) * 1.2, -1, 1));
    },
    draw(g, s, E) {
      g.rect(-5, -5, 10, 10, C.MUTED); g.rect(-W - 0.4, -W - 0.4, 2 * W + 0.8, 2 * W + 0.8, C.BG);
      g.rect(s.g[0] - GS, s.g[1] - GS, 2 * GS, 2 * GS, C.POS, { alpha: 0.25 }); g.outline(s.g[0] - GS, s.g[1] - GS, 2 * GS, 2 * GS, C.POS);
      g.line(s.b[0], s.b[1], s.g[0], s.g[1], C.ENV, { dash: [5, 5], width: 1.5 });
      g.line(s.p[0], s.p[1], s.b[0], s.b[1], C.ENV, { dash: [2, 4], width: 1 });
      g.box(s.b[0], s.b[1], 1, 1, 0, C.ENV);
      g.circle(s.p[0], s.p[1], AR, C.ACC);
      g.line(s.p[0], s.p[1], s.p[0] + E.lastAct[0] * 1.2, s.p[1] + E.lastAct[1] * 1.2, C.ACC, { arrow: true, width: 3 });
      g.text(`상자 ↔ 골 거리 ${gd(s).toFixed(2)} — 줄어든 만큼 보상 (한 스텝 ±0.02 상한)`, 0, -5.25, C.MUTED, 12);
    },
    rewards: [["상자가 골에 가까워짐", "+진전 × 0.5", "계속 (한 스텝 ±0.02 상한)"], ["상자 중심이 골 칸 안", "+1", "판 끝 — 성공"], ["1500 스텝 지남", "0", "판 끝 (Max Step)"]],
  };
  return def;
})();
