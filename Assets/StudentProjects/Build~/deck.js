// deck.js — dark, simple deck helpers for pptxgenjs (house style for any .pptx).
// Usage:
//   const { createDeck } = require("./deck");
//   const d = createDeck();            // { pres, C, F, MONO, W, H, M, ...helpers }
//   const s = d.newSlide("speaker notes here");
//   d.title(s, "제목", "English subtitle");
//   ...
//   await d.save("/mnt/user-data/outputs/deck.pptx");
//
// Every helper takes the slide as its first argument and uses inches on a
// 10 x 5.625 (16:9) canvas. Nothing here is domain specific except the
// defaults of cycle(); override its labels for other subjects.

const pptxgen = require("pptxgenjs");

function createDeck(opts = {}) {
  const pres = new pptxgen();
  pres.layout = "LAYOUT_16x9"; // 10 x 5.625 in
  pres.lang = opts.lang || "ko-KR";

  // ---------- design tokens ----------
  const C = Object.assign({
    BG: "15161B",     // slide background
    CARD: "21232B",   // default card
    CARD2: "2C2F3A",  // raised card / highlight bar / muted box
    TEXT: "EDEEF0",   // body text
    MUTED: "9AA0AB",  // secondary text, footers, arrows
    ACC: "F2B65B",    // amber accent — one per slide idea, chapter numbers, key sentence
    ENV: "6C9BD2",    // cool blue — the "other side" of a pair, secondary tag
    POS: "6CC59B",    // positive / success / reward +
    NEG: "E06F63",    // negative / warning / reward -
  }, opts.colors || {});
  const F = opts.font || "Noto Sans KR";     // house font (Google Fonts); install on the presenting PC. LibreOffice falls back to Noto Sans CJK KR
  const MONO = opts.mono || "Consolas";
  const W = 10, H = 5.625, M = 0.6;         // canvas + horizontal margin

  let pageNo = 0;
  let chapterLabel = "";

  // ---------- primitives ----------
  function txt(s, text, x, y, w, h, o = {}) {
    s.addText(text, Object.assign({
      x, y, w, h, fontFace: F, fontSize: 15, color: C.TEXT,
      isTextBox: true, margin: 0, valign: "top", align: "left",
    }, o));
  }
  function rect(s, x, y, w, h, fill, o = {}) {
    s.addShape(pres.shapes.ROUNDED_RECTANGLE, Object.assign({
      x, y, w, h, fill: { color: fill }, line: { color: fill, width: 0 }, rectRadius: 0.08,
    }, o));
  }
  function circle(s, x, y, d, fill) {
    s.addShape(pres.shapes.OVAL, { x, y, w: d, h: d, fill: { color: fill }, line: { color: fill, width: 0 } });
  }
  function arrow(s, x1, y1, x2, y2, color, o = {}) {
    s.addShape(pres.shapes.LINE, Object.assign({
      x: Math.min(x1, x2), y: Math.min(y1, y2),
      w: Math.abs(x2 - x1), h: Math.abs(y2 - y1),
      line: { color, width: 2, endArrowType: "triangle" },
      flipH: x2 < x1, flipV: y2 < y1,
    }, o));
  }

  // ---------- slide scaffolding ----------
  // Every content slide: dark bg, chapter label bottom-left, page number bottom-right,
  // speaker notes attached (lecture script lives ONLY in notes, never on the slide).
  function newSlide(notes) {
    const s = pres.addSlide();
    s.background = { color: C.BG };
    pageNo++;
    if (chapterLabel) txt(s, chapterLabel, M, 5.22, 5, 0.25, { fontSize: 9, color: C.MUTED });
    txt(s, String(pageNo), W - M - 0.6, 5.22, 0.6, 0.25, { fontSize: 9, color: C.MUTED, align: "right" });
    if (notes) s.addNotes(notes);
    return s;
  }
  function setChapter(label) { chapterLabel = label; }

  // Title (26pt bold) + optional muted English/one-line subtitle (12pt). Content starts at y ≈ 1.35–1.45.
  function title(s, ko, en) {
    txt(s, ko, M, 0.38, 8.8, 0.55, { fontSize: 26, bold: true });
    if (en) txt(s, en, M, 0.92, 8.8, 0.3, { fontSize: 12, color: C.MUTED });
  }

  // Bullet list. Items must NOT contain "\n" (each newline becomes its own bulleted paragraph).
  function bullets(s, items, x, y, w, h, size = 14, color = C.TEXT) {
    const arr = items.map((t, i) => ({
      text: t, options: { bullet: { indent: 14 }, breakLine: i < items.length - 1, paraSpaceAfter: 8 },
    }));
    s.addText(arr, { x, y, w, h, fontFace: F, fontSize: size, color, isTextBox: true, margin: 0, valign: "top" });
  }

  // Small pill label (e.g. example-source tag). Text is dark on the colored pill.
  function tag(s, text, x, y, w, color) {
    rect(s, x, y, w, 0.28, color, { rectRadius: 0.14 });
    txt(s, text, x, y, w, 0.28, { fontSize: 10, bold: true, color: C.BG, align: "center", valign: "middle" });
  }

  // Full-width emphasis bar at the bottom of a slide: one key sentence in accent color.
  function bottomBar(s, text, y = 4.45, h = 0.6, size = 14) {
    rect(s, M, y, 8.8, h, C.CARD2);
    txt(s, text, M + 0.3, y, 8.2, h, { fontSize: size, bold: true, color: C.ACC, align: "center", valign: "middle" });
  }

  // Cover slide: big title, English, accent tagline, muted footer line.
  function cover(ko, en, tagline, footer, notes) {
    chapterLabel = "";
    const s = newSlide(notes);
    txt(s, ko, M, 1.7, 8.8, 0.9, { fontSize: 44, bold: true });
    if (en) txt(s, en, M, 2.6, 8.8, 0.4, { fontSize: 18, color: C.MUTED });
    if (tagline) txt(s, tagline, M, 3.3, 8.8, 0.4, { fontSize: 16, color: C.ACC });
    if (footer) txt(s, footer, M, 4.6, 8.8, 0.3, { fontSize: 11, color: C.MUTED });
    return s;
  }

  // Chapter/section divider: "01" in accent, title, English, thin rule, then up to two label/text rows.
  // rows: [["학습 목표", "..."], ["답할 질문", "..."]] or [["핵심", "..."]] — any labels; pass [] for none.
  function chapter(num, ko, en, rows, notes) {
    chapterLabel = `${num}. ${ko}`;
    const s = newSlide(notes);
    txt(s, String(num).padStart(2, "0"), M, 1.3, 2, 1.2, { fontSize: 64, bold: true, color: C.ACC });
    txt(s, ko, M, 2.55, 8.8, 0.7, { fontSize: 34, bold: true });
    if (en) txt(s, en, M, 3.25, 8.8, 0.35, { fontSize: 14, color: C.MUTED });
    rect(s, M, 3.85, 8.8, 0.02, C.CARD2);
    (rows || []).slice(0, 2).forEach((r, i) => {
      const y = 4.05 + i * 0.4;
      txt(s, r[0], M, y, 1.2, 0.3, { fontSize: 11, color: C.ACC, bold: true });
      txt(s, r[1], M + 1.2, y, 7.6, i === 0 ? 0.35 : 0.6, { fontSize: 13, color: i === 0 ? C.TEXT : C.MUTED });
    });
    return s;
  }

  // Row of N equal cards spanning the content width.
  // items: [{ head, headColor?, body, foot? }]  (head = 14–16pt accent, body = 12.5pt, foot = 11pt muted)
  // Use 3 cards for concepts, 4 for steps, 5 max. Set numbered:true for 1..N circles.
  function cardRow(s, items, y, h, o = {}) {
    const n = items.length, gap = o.gap ?? 0.2;
    const w = (8.8 - gap * (n - 1)) / n;
    items.forEach((it, i) => {
      const x = M + i * (w + gap);
      rect(s, x, y, w, h, o.fill || C.CARD);
      let ty = y + 0.15;
      if (o.numbered) {
        circle(s, x + 0.2, y + 0.2, 0.42, it.headColor || C.ACC);
        txt(s, String(i + 1), x + 0.2, y + 0.2, 0.42, 0.42, { fontSize: 13, bold: true, color: C.BG, align: "center", valign: "middle" });
        ty = y + 0.75;
      }
      txt(s, it.head, x + 0.2, ty, w - 0.4, 0.4, { fontSize: o.headSize || 14, bold: true, color: it.headColor || C.ACC });
      txt(s, it.body, x + 0.2, ty + 0.45, w - 0.4, h - (ty - y) - 0.5 - (it.foot ? 0.6 : 0), { fontSize: o.bodySize || 12.5 });
      if (it.foot) txt(s, it.foot, x + 0.2, y + h - 0.7, w - 0.4, 0.6, { fontSize: 11, color: C.MUTED });
      if (o.arrows && i < n - 1) txt(s, "›", x + w, y + h / 2 - 0.25, gap, 0.5, { fontSize: 22, color: C.MUTED, align: "center" });
    });
  }

  // Two side-by-side cards (comparison / left-right). Each: { head, headColor?, tag?, tagColor?, bullets?, body? }
  function twoCards(s, left, right, y, h) {
    [left, right].forEach((c, i) => {
      const x = M + i * 4.5;
      rect(s, x, y, 4.3, h, C.CARD);
      let ty = y + 0.15;
      if (c.tag) { tag(s, c.tag, x + 0.25, ty, c.tagW || 1.1, c.tagColor || C.ACC); ty += 0.45; }
      if (c.head) { txt(s, c.head, x + 0.25, ty, 3.8, 0.4, { fontSize: 15, bold: true, color: c.headColor || C.TEXT }); ty += 0.45; }
      if (c.bullets) { bullets(s, c.bullets, x + 0.25, ty, 3.8, h - (ty - y) - 0.2, 12.5); }
      else if (c.body) { txt(s, c.body, x + 0.25, ty, 3.8, h - (ty - y) - 0.2, { fontSize: 12.5, color: c.bodyColor || C.TEXT }); }
    });
  }

  // Code block: dark card + monospace lines. lines: [{ t, c? }] where c is a hex color for that line.
  function codeBlock(s, lines, x, y, w, h, size = 11) {
    rect(s, x, y, w, h, C.CARD);
    const arr = lines.map((l, i) => ({ text: l.t, options: { color: l.c || C.TEXT, breakLine: i < lines.length - 1 } }));
    s.addText(arr, { x: x + 0.2, y: y + 0.15, w: w - 0.4, h: h - 0.3, fontFace: MONO, fontSize: size, isTextBox: true, margin: 0, valign: "top", lineSpacingMultiple: 1.15 });
  }

  // Tables: dark cells, BG-colored 1.5pt borders as gutters, accent bold header on CARD2.
  function tableStyle(colW, o = {}) {
    return {
      colW, fontFace: F, fontSize: o.fontSize || 12, color: C.TEXT, valign: "middle",
      border: { type: "solid", color: C.BG, pt: 1.5 }, fill: { color: C.CARD }, margin: o.margin ?? 0.08,
      rowH: o.rowH,
    };
  }
  function headerRow(cells) {
    return cells.map((c) => ({ text: c, options: { bold: true, color: C.ACC, fill: { color: C.CARD2 } } }));
  }
  function cell(t, o = {}) { return { text: t, options: o }; }
  // Convenience: addTable with style. rows = [headerRow([...]), [cell(), ...], ...]
  function table(s, rows, x, y, w, colW, o = {}) {
    s.addTable(rows, Object.assign({ x, y, w }, tableStyle(colW, o)));
  }

  // Native line chart in the house style (transparent, muted axes, accent series).
  function lineChart(s, series, x, y, w, h, o = {}) {
    s.addChart(pres.charts.LINE, series, Object.assign({
      x, y, w, h,
      chartColors: o.colors || [C.ACC, C.ENV, C.POS, C.NEG], lineSize: 3,
      lineDataSymbol: o.symbol || "none", lineDataSymbolSize: 6,
      showLegend: series.length > 1, legendPos: "b", legendColor: C.MUTED, legendFontSize: 10, legendFontFace: F,
      showTitle: false,
      catAxisLabelColor: C.MUTED, valAxisLabelColor: C.MUTED, catAxisLabelFontSize: 10, valAxisLabelFontSize: 10,
      catAxisLabelFontFace: F, valAxisLabelFontFace: F,
      valGridLine: { color: C.CARD2, size: 0.5 }, catGridLine: { style: "none" },
      plotArea: { fill: { color: C.BG } }, chartArea: { fill: { color: C.BG }, roundedCorners: false },
      catAxisLineShow: false, valAxisLineShow: false,
      showCatAxisTitle: !!o.xTitle, catAxisTitle: o.xTitle, catAxisTitleColor: C.MUTED, catAxisTitleFontSize: 10, catAxisTitleFontFace: F,
      showValAxisTitle: !!o.yTitle, valAxisTitle: o.yTitle, valAxisTitleColor: C.MUTED, valAxisTitleFontSize: 10, valAxisTitleFontFace: F,
    }, o.chart || {}));
  }

  // Two-box feedback cycle (A —top arrow→ B, B —bottom arrow→ A). The signature recurring diagram.
  // hl: "all" | "left" | "right" | "top" | "bottom"  — highlights one part, greys the rest.
  // Reuse the SAME diagram across a chapter, changing only hl, so learners see one structure.
  function cycle(s, x, y, w, h, hl = "all", o = {}) {
    const small = o.small || false;
    const L = Object.assign({ title: "A", sub: "" }, o.left || {});
    const R = Object.assign({ title: "B", sub: "" }, o.right || {});
    const topLabel = o.topLabel || "→", bottomLabel = o.bottomLabel || "←";
    const bw = small ? w * 0.36 : w * 0.3, bh = h * 0.42, by = y + h * 0.29;
    const on = (k) => hl === "all" || hl === k;
    const fs = small ? 10 : 15, fs2 = small ? 7.5 : 11, pad = small ? 0.15 : 0;
    rect(s, x, by, bw, bh, on("left") ? C.ACC : C.CARD2);
    txt(s, [{ text: L.title, options: { bold: true, breakLine: !!L.sub } }, { text: L.sub, options: { fontSize: fs2 } }],
      x, by, bw, bh, { fontSize: fs, color: on("left") ? C.BG : C.TEXT, align: "center", valign: "middle" });
    rect(s, x + w - bw, by, bw, bh, on("right") ? C.ENV : C.CARD2);
    txt(s, [{ text: R.title, options: { bold: true, breakLine: !!R.sub } }, { text: R.sub, options: { fontSize: fs2 } }],
      x + w - bw, by, bw, bh, { fontSize: fs, color: on("right") ? C.BG : C.TEXT, align: "center", valign: "middle" });
    const ax1 = x + bw + 0.08, ax2 = x + w - bw - 0.08;
    const y1 = by + bh * 0.28, y2 = by + bh * 0.72;
    const tc = on("top") ? C.ACC : C.MUTED, bc = on("bottom") ? C.ACC : C.MUTED;
    arrow(s, ax1, y1, ax2, y1, tc, { line: { color: tc, width: on("top") && hl !== "all" ? 3 : 2, endArrowType: "triangle" } });
    arrow(s, ax2, y2, ax1, y2, bc, { line: { color: bc, width: on("bottom") && hl !== "all" ? 3 : 2, endArrowType: "triangle" } });
    txt(s, topLabel, ax1 - pad, y1 - (small ? 0.24 : 0.32), ax2 - ax1 + 2 * pad, 0.3, { fontSize: fs2 + 1, color: hl === "all" ? C.TEXT : tc, align: "center", bold: on("top") && hl !== "all" });
    txt(s, bottomLabel, ax1 - pad, y2 + 0.05, ax2 - ax1 + 2 * pad, 0.3, { fontSize: fs2 + 1, color: hl === "all" ? C.TEXT : bc, align: "center", bold: on("bottom") && hl !== "all" });
  }

  // Term slide: big term left (Korean 30pt + English 13pt accent), definition, two tagged examples,
  // accent takeaway box, and a small diagram card on the right (drawFn(s, x, y, w, h) draws into it).
  function termSlide(ko, en, def, examples, point, drawFn, caption, notes) {
    const s = newSlide(notes);
    txt(s, ko, M, 0.45, 4.6, 0.6, { fontSize: 30, bold: true });
    txt(s, en, M, 1.05, 4.6, 0.3, { fontSize: 13, color: C.ACC });
    txt(s, def, M, 1.55, 4.6, 0.9, { fontSize: 14.5 });
    (examples || []).slice(0, 2).forEach((ex, i) => {
      const y = 2.6 + i * 0.7;
      tag(s, ex.tag, M, y, ex.tagW || 1.0, ex.color || (i === 0 ? C.ACC : C.ENV));
      txt(s, ex.text, M + (ex.tagW || 1.0) + 0.15, y - 0.02, 4.45 - (ex.tagW || 1.0), 0.6, { fontSize: 12.5 });
    });
    rect(s, M, 4.15, 4.6, 0.85, C.CARD2);
    txt(s, point, M + 0.2, 4.15, 4.2, 0.85, { fontSize: 12.5, color: C.ACC, valign: "middle" });
    rect(s, 5.5, 1.2, 3.9, 2.9, C.CARD);
    if (drawFn) drawFn(s, 5.7, 1.45, 3.5, 2.4);
    if (caption) txt(s, caption, 5.5, 4.25, 3.9, 0.75, { fontSize: 10, color: C.MUTED });
    return s;
  }

  // Q&A grid: items [[question, answer]] in two columns of up to 6 rows.
  function qaGrid(s, items, y0 = 1.3, rowH = 0.62) {
    items.forEach((q, i) => {
      const perCol = Math.ceil(items.length / 2);
      const col = i < perCol ? 0 : 1, row = i < perCol ? i : i - perCol;
      const x = M + col * 4.5, y = y0 + row * rowH;
      rect(s, x, y, 4.3, rowH - 0.08, C.CARD);
      txt(s, `${i + 1}. ${q[0]}`, x + 0.15, y + 0.04, 4.0, 0.25, { fontSize: 10, bold: true, color: C.ACC });
      txt(s, q[1], x + 0.15, y + 0.28, 4.0, 0.25, { fontSize: 9.5 });
    });
  }

  // ---------- additional general-purpose layouts ----------

  // Numbered grid (agenda / key questions / requirements): 2 columns × ≤6 rows.
  function numberedGrid(s, items, y0 = 1.4, rowH = 0.6) {
    const perCol = Math.ceil(items.length / 2);
    items.forEach((q, i) => {
      const col = i < perCol ? 0 : 1, row = i < perCol ? i : i - perCol;
      const x = M + col * 4.5, y = y0 + row * rowH;
      rect(s, x, y, 4.3, rowH - 0.1, C.CARD);
      txt(s, String(i + 1).padStart(2, "0"), x + 0.15, y, 0.4, rowH - 0.1, { fontSize: 11, color: C.ACC, bold: true, valign: "middle" });
      txt(s, q, x + 0.55, y, 3.65, rowH - 0.1, { fontSize: 11.5, valign: "middle" });
    });
  }

  // Key statement: one big sentence in a CARD2 box, then up to 3 keyword cards.
  // keywords: [{ head, sub?, body }]
  function statement(s, text, keywords = [], y = 1.4) {
    rect(s, M, y, 8.8, 1.1, C.CARD2);
    txt(s, text, M + 0.3, y, 8.2, 1.1, { fontSize: 19, bold: true, align: "center", valign: "middle" });
    const n = keywords.length; if (!n) return;
    const w = (8.8 - 0.2 * (n - 1)) / n;
    keywords.forEach((k, i) => {
      const x = M + i * (w + 0.2), ky = y + 1.4;
      rect(s, x, ky, w, 1.55, C.CARD);
      txt(s, k.head, x + 0.2, ky + 0.15, w - 0.4, 0.4, { fontSize: 15, bold: true });
      if (k.sub) txt(s, k.sub, x + 0.2, ky + 0.52, w - 0.4, 0.3, { fontSize: 11, color: C.ACC });
      txt(s, k.body, x + 0.2, ky + (k.sub ? 0.85 : 0.6), w - 0.4, 0.65, { fontSize: 11.5, color: C.MUTED });
    });
  }

  // KPI / big-number cards. items: [{ value, label, delta?, deltaColor? }] (3–4 items)
  function kpiRow(s, items, y = 1.5, h = 1.9) {
    const n = items.length, gap = 0.2, w = (8.8 - gap * (n - 1)) / n;
    items.forEach((k, i) => {
      const x = M + i * (w + gap);
      rect(s, x, y, w, h, C.CARD);
      txt(s, k.value, x + 0.25, y + 0.25, w - 0.5, 0.8, { fontSize: 36, bold: true, color: k.color || C.ACC });
      txt(s, k.label, x + 0.25, y + 1.05, w - 0.5, 0.35, { fontSize: 12.5 });
      if (k.delta) txt(s, k.delta, x + 0.25, y + 1.4, w - 0.5, 0.3, { fontSize: 11, color: k.deltaColor || C.MUTED });
    });
  }

  // Horizontal timeline / roadmap. items: [{ label, date?, done? }] (3–7). Current/done markers use POS/ACC.
  function timeline(s, items, y = 2.3, o = {}) {
    const x0 = o.x0 || 1.3, xe = o.xe || 8.7, n = items.length;
    rect(s, x0, y + 0.28, xe - x0, 0.05, C.CARD2);
    items.forEach((it, i) => {
      const cx = n === 1 ? x0 : x0 + (i * (xe - x0)) / (n - 1);
      const col = it.done ? C.POS : (it.current ? C.ACC : C.CARD2);
      s.addShape(pres.shapes.OVAL, { x: cx - 0.17, y: y + 0.13, w: 0.34, h: 0.34, fill: { color: col }, line: { color: it.done || it.current ? C.BG : C.MUTED, width: 2 } });
      if (it.date) txt(s, it.date, cx - 0.8, y - 0.35, 1.6, 0.3, { fontSize: 10, color: C.MUTED, align: "center" });
      txt(s, it.label, cx - 0.85, y + 0.6, 1.7, 0.7, { fontSize: 11.5, bold: !!it.current, color: it.current ? C.ACC : C.TEXT, align: "center" });
    });
  }

  // Left-to-right flow of boxes with arrows. boxes: [{ title, sub?, color? }] (3–5). Optional feedback caption below.
  function flow(s, boxes, y = 1.75, o = {}) {
    const n = boxes.length, gap = o.gap || 0.45, bh = o.h || 1.15;
    const bw = (8.8 - gap * (n - 1)) / n;
    boxes.forEach((b, i) => {
      const x = M + i * (bw + gap);
      const fill = b.color || C.CARD2, dark = fill !== C.CARD2 && fill !== C.CARD;
      rect(s, x, y, bw, bh, fill);
      txt(s, [{ text: b.title, options: { bold: true, fontSize: 14, breakLine: !!b.sub } }, { text: b.sub || "", options: { fontSize: 10.5 } }],
        x, y, bw, bh, { fontSize: 14, color: dark ? C.BG : C.TEXT, align: "center", valign: "middle" });
      if (i < n - 1) arrow(s, x + bw + 0.05, y + bh / 2, x + bw + gap - 0.05, y + bh / 2, C.MUTED);
    });
    if (o.feedback) {
      const fy = y + bh + 0.35, x1 = M + bw / 2, x2 = M + (n - 1) * (bw + gap) + bw / 2;
      rect(s, x1, fy, x2 - x1, 0.02, C.POS);
      arrow(s, x2, y + bh + 0.05, x2, fy, C.POS);
      arrow(s, x1, fy, x1, y + bh + 0.05, C.POS);
      txt(s, o.feedback, x1, fy + 0.08, x2 - x1, 0.3, { fontSize: 11, color: C.POS, align: "center" });
    }
  }

  // Layered stack (architecture): layers top→bottom. items: [{ title, body?, color? }] (3–5)
  function layers(s, items, x = M, y = 1.4, w = 8.8, h = 3.5) {
    const n = items.length, gap = 0.12, lh = (h - gap * (n - 1)) / n;
    items.forEach((it, i) => {
      const ly = y + i * (lh + gap), fill = it.color || C.CARD, dark = fill !== C.CARD && fill !== C.CARD2;
      rect(s, x, ly, w, lh, fill);
      txt(s, it.title, x + 0.3, ly, 2.6, lh, { fontSize: 14, bold: true, color: dark ? C.BG : C.ACC, valign: "middle" });
      if (it.body) txt(s, it.body, x + 3.0, ly, w - 3.3, lh, { fontSize: 11.5, color: dark ? C.BG : C.TEXT, valign: "middle" });
    });
  }

  // 2×2 quadrant. quads: [TL, TR, BL, BR] as { title, body }; axes: { x: [left, right], y: [bottom, top] }
  function quadrant(s, quads, axes, x = 1.3, y = 1.4, w = 7.4, h = 3.4) {
    const qw = (w - 0.15) / 2, qh = (h - 0.15) / 2;
    quads.forEach((q, i) => {
      const qx = x + (i % 2) * (qw + 0.15), qy = y + Math.floor(i / 2) * (qh + 0.15);
      rect(s, qx, qy, qw, qh, i === 1 ? C.CARD2 : C.CARD);
      txt(s, q.title, qx + 0.2, qy + 0.15, qw - 0.4, 0.35, { fontSize: 13.5, bold: true, color: C.ACC });
      txt(s, q.body, qx + 0.2, qy + 0.55, qw - 0.4, qh - 0.7, { fontSize: 11, color: C.TEXT });
    });
    if (axes) {
      if (axes.x) { txt(s, axes.x[0], x, y + h + 0.05, qw, 0.3, { fontSize: 10, color: C.MUTED, align: "center" }); txt(s, axes.x[1], x + qw + 0.15, y + h + 0.05, qw, 0.3, { fontSize: 10, color: C.MUTED, align: "center" }); }
      if (axes.y) { txt(s, axes.y[1], x - 0.75, y, 0.7, qh, { fontSize: 10, color: C.MUTED, align: "right", valign: "middle" }); txt(s, axes.y[0], x - 0.75, y + qh + 0.15, 0.7, qh, { fontSize: 10, color: C.MUTED, align: "right", valign: "middle" }); }
    }
  }

  // Checklist / summary list with check circles. items: [{ text, done? }] or strings.
  function checklist(s, items, x = M, y = 1.45, w = 8.8, rowH = 0.55) {
    items.forEach((it, i) => {
      const t = typeof it === "string" ? { text: it, done: true } : it;
      const ry = y + i * rowH;
      rect(s, x, ry, w, rowH - 0.1, C.CARD);
      circle(s, x + 0.18, ry + (rowH - 0.1) / 2 - 0.15, 0.3, t.done ? C.POS : C.CARD2);
      if (t.done) txt(s, "✓", x + 0.18, ry + (rowH - 0.1) / 2 - 0.15, 0.3, 0.3, { fontSize: 11, bold: true, color: C.BG, align: "center", valign: "middle" });
      txt(s, t.text, x + 0.65, ry, w - 0.85, rowH - 0.1, { fontSize: 12.5, valign: "middle", color: t.done ? C.TEXT : C.MUTED });
    });
  }

  // Card grid (2 rows × 3 cols or 2×2). items: [{ head, body }]; cols 2 or 3.
  function cardGrid(s, items, cols = 3, y = 1.4, h = 3.6) {
    const rows = Math.ceil(items.length / cols), gap = 0.2;
    const w = (8.8 - gap * (cols - 1)) / cols, ch = (h - gap * (rows - 1)) / rows;
    items.forEach((it, i) => {
      const x = M + (i % cols) * (w + gap), cy = y + Math.floor(i / cols) * (ch + gap);
      rect(s, x, cy, w, ch, C.CARD);
      txt(s, it.head, x + 0.2, cy + 0.15, w - 0.4, 0.35, { fontSize: 13.5, bold: true, color: it.color || C.ACC });
      txt(s, it.body, x + 0.2, cy + 0.55, w - 0.4, ch - 0.7, { fontSize: 11.5 });
    });
  }

  // Native bar chart (vertical). series: [{ name, labels, values }]
  function barChart(s, series, x, y, w, h, o = {}) {
    s.addChart(pres.charts.BAR, series, Object.assign({
      x, y, w, h, barDir: o.horizontal ? "bar" : "col", barGapWidthPct: 60,
      chartColors: o.colors || [C.ACC, C.ENV, C.POS, C.NEG],
      showLegend: series.length > 1, legendPos: "b", legendColor: C.MUTED, legendFontSize: 10, legendFontFace: F, showTitle: false,
      catAxisLabelColor: C.MUTED, valAxisLabelColor: C.MUTED, catAxisLabelFontSize: 10, valAxisLabelFontSize: 10,
      catAxisLabelFontFace: F, valAxisLabelFontFace: F,
      valGridLine: { color: C.CARD2, size: 0.5 }, catGridLine: { style: "none" },
      plotArea: { fill: { color: C.BG } }, chartArea: { fill: { color: C.BG }, roundedCorners: false },
      catAxisLineShow: false, valAxisLineShow: false,
      showValue: !!o.showValue, dataLabelColor: C.TEXT, dataLabelFontSize: 10, dataLabelFontFace: F,
    }, o.chart || {}));
  }

  // Native doughnut for shares. data: { labels, values }
  function doughnutChart(s, data, x, y, w, h, o = {}) {
    s.addChart(pres.charts.DOUGHNUT, [{ name: o.name || "share", labels: data.labels, values: data.values }], Object.assign({
      x, y, w, h, holeSize: 60,
      chartColors: o.colors || [C.ACC, C.ENV, C.POS, C.NEG, C.MUTED, C.CARD2],
      showLegend: true, legendPos: "r", legendColor: C.TEXT, legendFontSize: 11, legendFontFace: F,
      showPercent: true, showValue: false, dataLabelColor: C.BG, dataLabelFontSize: 10, dataLabelFontFace: F,
      showTitle: false, plotArea: { fill: { color: C.BG } }, chartArea: { fill: { color: C.BG }, roundedCorners: false },
      dataBorder: { pt: 2, color: C.BG },
    }, o.chart || {}));
  }

  // Picture inside a card frame with caption (for screenshots / user-provided images only).
  function picture(s, path, x, y, w, h, caption) {
    rect(s, x - 0.1, y - 0.1, w + 0.2, h + 0.2 + (caption ? 0.35 : 0), C.CARD);
    s.addImage({ path, x, y, w, h, sizing: { type: "contain", w, h } });
    if (caption) txt(s, caption, x, y + h + 0.08, w, 0.3, { fontSize: 10, color: C.MUTED, align: "center" });
  }

  // Closing slide: short title, muted line, optional contact/next-step line.
  function closing(ko, sub, foot, notes) {
    const s = newSlide(notes);
    txt(s, ko, M, 1.9, 8.8, 0.9, { fontSize: 40, bold: true });
    if (sub) txt(s, sub, M, 2.8, 8.8, 0.4, { fontSize: 16, color: C.MUTED });
    if (foot) txt(s, foot, M, 4.4, 8.8, 0.35, { fontSize: 12, color: C.ACC });
    return s;
  }

  async function save(path) {
    await pres.writeFile({ fileName: path });
    return { path, slides: pageNo };
  }

  return {
    pres, C, F, MONO, W, H, M,
    txt, rect, circle, arrow, newSlide, setChapter, title, bullets, tag, bottomBar,
    cover, chapter, cardRow, twoCards, codeBlock, tableStyle, headerRow, cell, table,
    lineChart, cycle, termSlide, qaGrid,
    numberedGrid, statement, kpiRow, timeline, flow, layers, quadrant, checklist, cardGrid,
    barChart, doughnutChart, picture, closing, save,
    get pageNo() { return pageNo; },
  };
}

module.exports = { createDeck };
