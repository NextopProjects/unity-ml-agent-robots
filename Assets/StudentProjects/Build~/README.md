# 기획서 생성 스크립트 (강사용)

`Games/<과제>.pdf` 와 `Games/<과제>-기획서.md` 는 **같은 데이터에서 만든다.** 내용을 고칠 때는 PDF 를 직접 고치지 말고 데이터를 고친 뒤 다시 만든다.
폴더 이름 끝의 `~` 는 Unity 가 이 폴더를 가져오지 않게 하는 표시다.

| 파일 | 하는 일 |
|---|---|
| `games2.js` (A) · `games2b.js` (B~M) | 과제별 내용 — 개요 · 플레이 과정 4컷 · 점수와 승패 · 관측 · 행동 · 보상 · 개발 단계 … |
| `build2.js` | 데이터 → 슬라이드(pptx, 과제당 21쪽). 표지 · 목차 · 4개 장 |
| `scenes.js` | 과제별 장면 그림 (도형으로 그림) |
| `deck.js` | 슬라이드 공통 디자인 (어두운 배경 · 카드 · 강조색 하나) |
| `makemd.js` | 데이터 → 기획서 md |
| `export2.ps1` | PowerPoint 로 pptx → PDF (그리고 `-Sheets` 면 미리보기 PNG) |
| `simgame.js` | `Play~/` 의 HTML 게임 규칙만 꺼내 무작위 · 규칙 모드로 판을 돌려 본다 |

## 다시 만들기 (Windows · PowerPoint · Node 필요)

```
cd Assets/StudentProjects/Build~
npm install
node build2.js                 # out2/*.pptx  (과제 하나만: node build2.js A)
node makemd.js ../Games        # ../Games/*-기획서.md
powershell -File export2.ps1 -OutPdfDir ..\Games        # ../Games/*.pdf
powershell -File export2.ps1 -Sheets                    # png2/ 에 미리보기 (확인용)
```

글꼴은 **Noto Sans KR** (Google Fonts) 이 설치돼 있어야 한다. PDF 에는 글꼴이 들어가므로 보는 쪽에는 필요 없다.

## HTML 게임 점검

```
node simgame.js "../Play~/A-동전-모으기.html" 30
```

무작위 모드는 거의 실패하고, 규칙 모드는 대부분 성공해야 '학습 전 → 학습 후' 비교가 된다.
실측 (30~80판): A 규칙 평균 15.9개 · B 30/30 · C 30/30 · D 30/30 버팀 · E 38/40 · F 30/30 · G 13/60 (손 규칙으로는 주차가 어렵다) · H 35/40 · I 3000 스텝 버팀 · J 27/30 · K 30/30 · L 단계 3 도달 · M(따라 하기) 시연 20판 → 3/30.
