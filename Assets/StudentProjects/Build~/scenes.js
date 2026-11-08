// 과제별 장면 그림 (표지 · 컨셉 · 등장 요소 · 학습 환경 쪽에 반복해서 쓴다)
// ---------------------------------------------------------------- 장면 그림
function scene(d, s, kind, x, y, w, h, mini = false) {
  const { C, pres } = d;
  const k = Math.min(w, h);
  const X = (u) => x + u * w, Y = (v) => y + v * h;
  const ru = (r) => (r * k) / w, rv = (r) => (r * k) / h;
  const sh = (t, o) => s.addShape(t, o);
  const box = (u, v, uw, vh, c, o = {}) => d.rect(s, X(u), Y(v), uw * w, vh * h, c, Object.assign({ rectRadius: 0.04 }, o));
  const dot = (u, v, r, c) => { const D = 2 * r * k; d.circle(s, X(u) - D / 2, Y(v) - D / 2, D, c); };
  const ring = (u, v, r, c, dash, wd = 1.5) => {
    const D = 2 * r * k;
    sh(pres.shapes.OVAL, { x: X(u) - D / 2, y: Y(v) - D / 2, w: D, h: D, fill: { color: C.BG, transparency: 100 }, line: { color: c, width: wd, dashType: dash ? "dash" : "solid" } });
  };
  const lineIn = (x1, y1, x2, y2, c, o = {}) => {
    const ln = { color: c, width: o.width || 1.5, dashType: o.dash ? "dash" : "solid" };
    if (o.arrow) ln.endArrowType = "triangle";
    sh(pres.shapes.LINE, { x: Math.min(x1, x2), y: Math.min(y1, y2), w: Math.max(Math.abs(x2 - x1), 0.001), h: Math.max(Math.abs(y2 - y1), 0.001), flipH: x2 < x1, flipV: y2 < y1, line: ln });
  };
  const line = (u1, v1, u2, v2, c, o) => lineIn(X(u1), Y(v1), X(u2), Y(v2), c, o);
  const rot = (cx, cy, bw, bh, deg, c, o = {}) => sh(pres.shapes.RECTANGLE, Object.assign({ x: cx - bw / 2, y: cy - bh / 2, w: bw, h: bh, rotate: deg, fill: { color: c }, line: { color: c, width: 0 } }, o));
  const label = (t, u, v, c = C.MUTED, size = 8.5, uw = 0.36, o = {}) => {
    if (mini) return;
    d.txt(s, t, X(u) - (uw * w) / 2, Y(v), uw * w, 0.24, Object.assign({ fontSize: size, color: c, align: "center" }, o));
  };
  const floor = () => box(0, 0, 1, 1, C.BG);
  const walls = () => { const t = 0.018; box(0, 0, 1, t * w / h, C.MUTED); box(0, 1 - t * w / h, 1, t * w / h, C.MUTED); box(0, 0, t, 1, C.MUTED); box(1 - t, 0, t, 1, C.MUTED); };

  switch (kind) {
    case "roller": {
      floor();
      dot(0.3, 0.6, 0.09, C.ACC);
      box(0.75 - ru(0.07), 0.4 - rv(0.07), ru(0.14), rv(0.14), C.ENV);
      line(0.3 + ru(0.12), 0.6 - rv(0.05), 0.75 - ru(0.11), 0.4 + rv(0.03), C.ACC, { dash: true, arrow: true });
      break;
    }
    case "coins": {
      floor();
      dot(0.28, 0.64, 0.075, C.ACC);
      dot(0.7, 0.3, 0.05, C.POS);
      ring(0.8, 0.72, 0.05, C.POS, true);
      line(0.28 + ru(0.1), 0.64 - rv(0.07), 0.7 - ru(0.08), 0.3 + rv(0.05), C.ACC, { dash: true, arrow: true });
      label("공", 0.28, 0.64 + rv(0.1), C.TEXT);
      label("동전 +1", 0.7, 0.3 - rv(0.07) - 0.25 / h, C.POS);
      label("먹으면 다른 자리에 다시 생김", 0.74, 0.72 + rv(0.08), C.MUTED, 8, 0.5);
      break;
    }
    case "stop": {
      floor();
      ring(0.78, 0.5, 0.16, C.POS, false, 2);
      const ghosts = [[0.1, 0.13], [0.3, 0.1], [0.48, 0.07], [0.62, 0.04]];
      ghosts.forEach(([u, len]) => {
        dot(u, 0.5, 0.05, C.CARD2);
        if (!mini) line(u - ru(0.03), 0.5 + rv(0.1), u - ru(0.03) + ru(len * 1.4), 0.5 + rv(0.1), C.MUTED, { arrow: true, width: 1.25 });
      });
      dot(0.78, 0.5, 0.055, C.ACC);
      label("가속", 0.2, 0.5 - rv(0.1) - 0.25 / h);
      label("감속", 0.55, 0.5 - rv(0.1) - 0.25 / h);
      label("정지", 0.78, 0.5 - rv(0.16) - 0.26 / h, C.ACC, 9);
      label("목표 칸 — 멈춰야 성공", 0.72, 0.5 + rv(0.18), C.POS, 8, 0.5);
      break;
    }
    case "grid": {
      // 범례(약 1.2in)까지 합친 폭이 상자 안에 들어오게 격자 크기를 정하고 가운데에 둔다
      const n = 5, legW = mini ? 0 : 1.3, cs = Math.min((0.82 * k) / n, (w - legW - 0.1) / n);
      const gx = x + (w - (n * cs + legW)) / 2, gy = y + h / 2 - (n * cs) / 2;
      const path = ["1,4", "2,4", "3,4", "4,4", "4,3", "4,2", "4,1"], wallC = ["2,1", "2,2"];
      for (let r = 0; r < n; r++) for (let c = 0; c < n; c++) {
        const key = `${c},${r}`;
        let col = C.BG;
        if (key === "0,4") col = C.ACC; else if (key === "4,0") col = C.POS; else if (wallC.includes(key)) col = C.NEG;
        d.rect(s, gx + c * cs + 0.02, gy + r * cs + 0.02, cs - 0.04, cs - 0.04, col, { rectRadius: 0.03 });
        if (path.includes(key)) d.circle(s, gx + c * cs + cs / 2 - 0.04, gy + r * cs + cs / 2 - 0.04, 0.08, C.MUTED);
      }
      if (!mini) {
        const lx = gx + n * cs + 0.15;
        [[C.ACC, "말"], [C.POS, "목표 칸"], [C.MUTED, "지나는 길"], [C.NEG, "벽 칸 (확장)"]].forEach(([c, t], i) => {
          d.rect(s, lx, gy + 0.1 + i * 0.42, 0.18, 0.18, c, { rectRadius: 0.03 });
          d.txt(s, t, lx + 0.28, gy + 0.06 + i * 0.42, 1.1, 0.26, { fontSize: 9, color: C.TEXT });
        });
      }
      break;
    }
    case "pole": {
      floor();
      box(0.06, 0.8, 0.88, 0.02, C.MUTED);
      const cw = 0.3 * k, ch = 0.11 * k, cx = X(0.5), top = Y(0.8) - ch - 0.05 * k;
      d.rect(s, cx - cw / 2, top, cw, ch, C.ACC, { rectRadius: 0.03 });
      d.circle(s, cx - cw / 2 + 0.03 * k, top + ch - 0.005, 0.06 * k, C.MUTED);
      d.circle(s, cx + cw / 2 - 0.09 * k, top + ch - 0.005, 0.06 * k, C.MUTED);
      const L = 0.58 * k, a = (14 * Math.PI) / 180;
      lineIn(cx, top, cx, top - L * 0.95, C.MUTED, { dash: true, width: 1 });
      rot(cx + (L / 2) * Math.sin(a), top - (L / 2) * Math.cos(a), 0.045 * k, L, 14, C.ENV);
      d.circle(s, cx - 0.03 * k, top - 0.03 * k, 0.06 * k, C.TEXT);
      if (!mini) {
        d.txt(s, "θ", cx + 0.04, top - L * 0.55, 0.3, 0.25, { fontSize: 11, color: C.TEXT });
        lineIn(cx - 0.1, Y(0.86), cx - 0.1 - 0.35 * k, Y(0.86), C.ACC, { arrow: true, width: 2 });
        lineIn(cx + 0.1, Y(0.86), cx + 0.1 + 0.35 * k, Y(0.86), C.ACC, { arrow: true, width: 2 });
      }
      label("막대 (경첩)", 0.78, 0.18, C.ENV, 9);
      label("수레를 좌우로 민다", 0.5, 0.89, C.ACC, 8.5, 0.5);
      label("넘어지면 끝", 0.2, 0.18, C.NEG, 9);
      break;
    }
    case "rays": {
      floor();
      const pil = [[0.44, 0.27], [0.56, 0.66], [0.74, 0.4], [0.32, 0.82]], pr = 0.055;
      pil.forEach(([u, v]) => dot(u, v, pr, C.NEG));
      const tu = 0.9, tv = 0.55;
      box(tu - ru(0.06), tv - rv(0.06), ru(0.12), rv(0.12), C.POS);
      const ax = X(0.13), ay = Y(0.52), R = 0.62 * w;
      for (let i = -3; i <= 3; i++) {
        const ang = (i * 17 * Math.PI) / 180, dx = Math.cos(ang), dy = Math.sin(ang);
        // 상자 밖으로 나가지 않게 가장자리에서 자른다
        let t = Math.min(R, dx > 0 ? (x + w - 0.05 - ax) / dx : R, dy > 0 ? (y + h - 0.05 - ay) / dy : dy < 0 ? (y + 0.05 - ay) / dy : R), col = C.MUTED;
        pil.forEach(([u, v]) => {
          const px = X(u) - ax, py = Y(v) - ay, proj = px * dx + py * dy, rr = pr * k;
          const dist2 = px * px + py * py - proj * proj;
          if (proj > 0 && dist2 < rr * rr) { const tt = proj - Math.sqrt(rr * rr - dist2); if (tt < t) { t = tt; col = C.NEG; } }
        });
        lineIn(ax, ay, ax + dx * t, ay + dy * t, col, { width: col === C.MUTED ? 0.75 : 1.5, dash: col === C.MUTED });
      }
      dot(0.13, 0.52, 0.06, C.ACC);
      label("광선 센서", 0.13, 0.52 + rv(0.08), C.ACC, 8.5, 0.3);
      label("기둥 (태그)", 0.44, 0.27 - rv(0.06) - 0.25 / h, C.NEG, 8.5, 0.3);
      label("목표", tu, tv + rv(0.08), C.POS, 8.5, 0.2);
      break;
    }
    case "push": {
      floor(); walls();
      box(0.66, 0.1, 0.26, 0.34, C.POS, { fill: { color: C.POS, transparency: 65 } });
      label("골", 0.79, 0.2, C.POS, 10, 0.2);
      const bu = 0.5, bv = 0.56, bs = 0.15;
      box(bu - ru(bs / 2), bv - rv(bs / 2), ru(bs), rv(bs), C.ENV);
      dot(0.22, 0.78, 0.06, C.ACC);
      line(0.22 + ru(0.08), 0.78 - rv(0.05), bu - ru(0.1), bv + rv(0.04), C.ACC, { arrow: true, width: 2 });
      line(bu + ru(0.1), bv - rv(0.08), 0.75, 0.32, C.POS, { arrow: true, dash: true, width: 2 });
      label("공", 0.22, 0.78 + rv(0.08), C.TEXT, 8.5, 0.15);
      label("상자", bu, bv + rv(0.1), C.ENV, 8.5, 0.2);
      break;
    }
    case "park": {
      floor();
      const sw = 0.19 * k, shh = 0.34 * k, scx = X(0.7), scy = Y(0.34);
      [-1, 1].forEach((sgn) => d.rect(s, scx + sgn * (sw + 0.06 * k) - sw / 2 + 0.02, scy - shh / 2 + 0.02, sw - 0.04, shh - 0.04, C.CARD2, { rectRadius: 0.04 }));
      sh(pres.shapes.RECTANGLE, { x: scx - sw / 2, y: scy - shh / 2, w: sw, h: shh, fill: { color: C.BG, transparency: 100 }, line: { color: C.POS, width: 1.75, dashType: "dash" } });
      d.rect(s, scx - sw / 2, scy - shh / 2 - 0.02, sw, 0.04, C.POS, { rectRadius: 0 });
      const ccx = X(0.2), ccy = Y(0.66), deg = 60, a = (deg * Math.PI) / 180;
      rot(ccx, ccy, 0.15 * k, 0.27 * k, deg, C.ACC);
      const fx = ccx + Math.sin(a) * 0.1 * k, fy = ccy - Math.cos(a) * 0.1 * k;
      d.circle(s, fx - 0.03, fy - 0.03, 0.06, C.BG);
      // 오른쪽으로 나가다 위로 꺾어 칸 아래쪽 입구로 들어간다
      const pts = [[ccx + Math.sin(a) * 0.19 * k, ccy - Math.cos(a) * 0.19 * k], [X(0.5), Y(0.72)], [scx, Y(0.8)], [scx, scy + shh * 0.6]];
      for (let i = 0; i < pts.length - 1; i++) lineIn(pts[i][0], pts[i][1], pts[i + 1][0], pts[i + 1][1], C.MUTED, { dash: true, width: 1.25, arrow: i === pts.length - 2 });
      if (!mini) d.txt(s, "주차 칸 (위쪽 선 = 앞)", scx - 1.0, scy - shh / 2 - 0.3, 2.0, 0.24, { fontSize: 8, color: C.POS, align: "center" });
      label("방향까지 맞춘다", 0.5, 0.88, C.TEXT, 8.5, 0.4);
      label("차", 0.2, 0.66 + rv(0.2), C.ACC, 9, 0.12);
      break;
    }
    case "lander": {
      box(0, 0.86, 1, 0.14, C.BG, { rectRadius: 0 });
      box(0.56, 0.82, 0.26, 0.05, C.POS, { rectRadius: 0.02 });
      const lx = X(0.36), ly = Y(0.34), lw = 0.2 * k, lh = 0.12 * k;
      lineIn(lx - lw * 0.35, ly + lh * 0.3, lx - lw * 0.55, ly + lh * 1.05, C.ENV, { width: 2 });
      lineIn(lx + lw * 0.35, ly + lh * 0.3, lx + lw * 0.55, ly + lh * 1.05, C.ENV, { width: 2 });
      rot(lx, ly, lw, lh, -10, C.ENV);
      sh(pres.shapes.ISOSCELES_TRIANGLE, { x: lx - 0.035 * k, y: ly + lh * 0.55, w: 0.07 * k, h: 0.12 * k, rotate: 180, fill: { color: C.ACC }, line: { color: C.ACC, width: 0 } });
      if (!mini) {
        const vx = lx + lw * 0.95;
        lineIn(vx, ly - 0.05, vx, ly + 0.2 * k, C.MUTED, { arrow: true, width: 1.5 });
        d.txt(s, "떨어지는 속도", vx + 0.08, ly, 1.1, 0.24, { fontSize: 8, color: C.MUTED });
        d.txt(s, "추력", lx - lw * 0.6 - 0.7, ly + lh * 0.9, 0.6, 0.24, { fontSize: 8.5, color: C.ACC, align: "right" });
      }
      label("중력 ↓", 0.1, 0.05, C.MUTED, 8.5, 0.18);
      label("패드 — 천천히, 똑바로", 0.69, 0.89, C.POS, 8, 0.45);
      break;
    }
    case "paddle": {
      box(0, 0.9, 1, 0.1, C.BG, { rectRadius: 0 });
      box(0.37, 0.78, 0.26, 0.045, C.ACC, { rectRadius: 0.02 });
      for (let t = 0.15; t < 0.95; t += 0.14) d.circle(s, X(0.2 + 0.3 * t) - 0.025, Y(0.2 + 0.54 * t * t) - 0.025, 0.05, C.MUTED);
      for (let t = 0.15; t < 0.8; t += 0.16) d.circle(s, X(0.5 + 0.3 * t) - 0.025, Y(0.74 - 0.6 * (2 * t - t * t)) - 0.025, 0.05, C.CARD2);
      dot(0.2, 0.2, 0.055, C.ENV);
      label("어디에 떨어질까?", 0.66, 0.12, C.TEXT, 9, 0.4);
      label("패들", 0.5, 0.83, C.ACC, 8.5, 0.2);
      label("바닥에 닿으면 끝", 0.82, 0.91, C.NEG, 8, 0.32);
      break;
    }
    case "tag": {
      floor(); walls();
      dot(0.7, 0.36, 0.065, C.ENV);
      dot(0.32, 0.68, 0.065, C.NEG);
      line(0.32 + ru(0.09), 0.68 - rv(0.06), 0.7 - ru(0.1), 0.36 + rv(0.07), C.NEG, { arrow: true, dash: true, width: 2 });
      line(0.7 + ru(0.08), 0.36 - rv(0.05), 0.86, 0.16, C.ENV, { arrow: true, width: 2 });
      label("도망자", 0.7, 0.36 + rv(0.09), C.ENV, 9, 0.2);
      label("술래", 0.32, 0.68 + rv(0.09), C.NEG, 9, 0.2);
      label("벽", 0.06, 0.06, C.MUTED, 8, 0.1);
      break;
    }
    case "arm": {
      floor();
      const bu = 0.5, bv = 0.56, Ro = 0.38, Ri = 0.11;
      ring(bu, bv, Ro, C.MUTED, true, 1);
      ring(bu, bv, Ri, C.MUTED, true, 1);
      const bx = X(bu), by = Y(bv), L1 = 0.22 * k, L2 = 0.16 * k;
      const a1 = (-62 * Math.PI) / 180, a2 = a1 + (58 * Math.PI) / 180;
      const jx = bx + L1 * Math.cos(a1), jy = by + L1 * Math.sin(a1), tx = jx + L2 * Math.cos(a2), ty = jy + L2 * Math.sin(a2);
      const btn = (205 * Math.PI) / 180, btx = bx + 0.27 * k * Math.cos(btn), bty = by + 0.27 * k * Math.sin(btn);
      d.circle(s, btx - 0.05 * k, bty - 0.05 * k, 0.1 * k, C.POS);
      if (!mini) lineIn(tx, ty, btx + 0.06 * k, bty - 0.04 * k, C.MUTED, { dash: true, arrow: true, width: 1 });
      lineIn(bx, by, jx, jy, C.ENV, { width: 7 });
      lineIn(jx, jy, tx, ty, C.ENV, { width: 5 });
      d.circle(s, bx - 0.05 * k, by - 0.05 * k, 0.1 * k, C.TEXT);
      d.circle(s, jx - 0.03 * k, jy - 0.03 * k, 0.06 * k, C.TEXT);
      d.circle(s, tx - 0.035 * k, ty - 0.035 * k, 0.07 * k, C.ACC);
      if (!mini) {
        d.txt(s, "버튼", btx - 0.3, bty + 0.08 * k, 0.6, 0.24, { fontSize: 8.5, color: C.POS, align: "center" });
        d.txt(s, "점선 = 닿는 범위의 안쪽 · 바깥 한계", x + 0.1, y + 0.08, 2.6, 0.24, { fontSize: 8, color: C.MUTED });
        d.txt(s, "팔 끝", tx + 0.05, ty - 0.28, 0.6, 0.24, { fontSize: 8.5, color: C.ACC });
      }
      break;
    }
    case "stairs": {
      const steps = [[0.06, 0.64, "쉬움"], [0.37, 0.44, "중간"], [0.68, 0.24, "어려움"]];
      steps.forEach(([u, v, t], i) => {
        box(u, v, 0.26, 0.92 - v, i === 2 ? C.ENV : C.CARD2);
        label(t, u + 0.13, v + 0.05, i === 2 ? C.BG : C.TEXT, 10, 0.26, { bold: true });
      });
      dot(0.19, 0.64 - rv(0.065), 0.055, C.ACC);
      line(0.3, 0.6, 0.45, 0.4, C.POS, { arrow: true, width: 2 });
      line(0.61, 0.4, 0.76, 0.2, C.POS, { arrow: true, width: 2 });
      label("성공률이 넘으면 다음 단계", 0.38, 0.28, C.POS, 8.5, 0.5);
      label("기본값 = 가장 어려운 단계", 0.81, 0.08, C.MUTED, 8, 0.4);
      break;
    }
    case "demo": {
      const bw = 0.38, bh = 0.3, items = [[0.06, 0.1, "사람 · 키보드", C.ACC], [0.56, 0.1, "시연 기록 (.demo)", C.BG], [0.56, 0.6, "PPO + BC / GAIL", C.ENV], [0.06, 0.6, "에이전트", C.POS]];
      items.forEach(([u, v, t, c]) => {
        box(u, v, bw, bh, c);
        d.txt(s, t, X(u), Y(v), bw * w, bh * h, { fontSize: mini ? 8.5 : 10.5, bold: true, color: c === C.BG ? C.TEXT : C.BG, align: "center", valign: "middle" });
      });
      line(0.06 + bw + 0.01, 0.25, 0.55, 0.25, C.MUTED, { arrow: true, width: 2 });
      line(0.75, 0.1 + bh + 0.02, 0.75, 0.59, C.MUTED, { arrow: true, width: 2 });
      line(0.55, 0.75, 0.06 + bw + 0.01, 0.75, C.MUTED, { arrow: true, width: 2 });
      label("녹화", 0.5, 0.25 - 0.27 / h, C.MUTED, 8, 0.14);
      label("섞어서 학습", 0.87, 0.47, C.MUTED, 8, 0.22);
      break;
    }
  }
}

module.exports = { scene };
