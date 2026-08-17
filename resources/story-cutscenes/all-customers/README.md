# 손님 컷씬 생성 기록

> 생성일: 2026-08-17  
> 범위: 7명 신규 제작 42장 + 선자 통일본 6장 = 총 48장  
> Figma 파일: [GONGSAMZ · 8 Customers Character Sheets](https://www.figma.com/design/Yrun8rClSF4bLDLSsDDjuQ/GONGSAMZ-%C2%B7-8-Customers-Character-Sheets?node-id=0-1)

## 결과

- 모든 원본은 `1536×1024`, RGB PNG다.
- 모든 Figma 화면은 `1920×1080`이며, 3:2 원본을 중앙 `1620×1080` 영역에 배치했다.
- 각 인물 페이지는 6개 화면을 가지며, 1~5 화면에는 클릭 및 `Space`·`Enter` 전환이 연결되어 있다.
- 대사와 캡션은 이미지에 포함하지 않고 Figma의 `Cutscene UI / Caption Panel` 인스턴스로 분리했다.
- 7명 전체 비교 이미지는 [`all-seven-contact-sheet.png`](all-seven-contact-sheet.png), 선자 통일본 비교 이미지는 [`../sunja/v2/sunja-v2-contact-sheet.png`](../sunja/v2/sunja-v2-contact-sheet.png)다.

## 공식 참조 이미지

| 손님 | 캐릭터 참조 | 붕어혼 위치 |
| --- | --- | --- |
| 정현 | `assets/figma-8-customers/01_JeongHyun.png` | `special-souls-v3.png` 위 1 |
| 하진 | `resources/character-art/hajin/Hajin-uniform-v3-front-cutout.png` | 위 2 |
| 미주 | `assets/figma-8-customers/03_MiJu.png` | 위 3 |
| 선자 | `assets/figma-8-customers/04_Sunja.png` | 위 4 |
| 건우 | `assets/figma-8-customers/05_Geonwoo.png` | 아래 1 |
| 태수 | `assets/figma-8-customers/06_Taesu.png` | 아래 2 |
| 나리 | `assets/figma-8-customers/07_Nari.png` | 아래 3 |
| 준호 | `assets/figma-8-customers/08_Junho.png` | 아래 4 |

붕어혼 공통 참조는 `resources/character-art/bungeoppang-system/special-souls-v3.png`다. 선자 v2는 먼저 완성된 7명 중 태수 장면의 짙은 겨울 질감과 각 장면의 직전 승인 이미지를 참조해 화풍을 통일했다.

## 재생성용 프롬프트 조립 규칙

아래 공통 프롬프트에 각 `docs/cutscenes/` 문서의 장면별 화면 구성과 제작 단서를 붙인다. 실제 생성에서는 장면 1에 공식 캐릭터 시트와 선자 화풍 이미지를, 장면 2 이후에는 공식 캐릭터 시트와 바로 앞 승인 장면을 참조로 사용했다. 붕어혼이 나오는 장면 3과 해금 일러스트에는 붕어혼 공통 참조를 추가했다.

```text
Create a single 3:2 horizontal Korean indie-game story cutscene illustration.
No text, no letters, no UI. Match the warm hand-painted watercolor-and-gouache
storybook style of the approved Sunja cutscene. Preserve the customer's exact
identity, age, hairstyle, clothing, body proportions, and recurring props from
the official Figma character sheet. Keep the emotional beat readable without
speech bubbles. Do not bake captions, logos, checkerboard, or an outer border
into the image.
```

해금 일러스트에는 다음 조건을 추가한다.

```text
Create a polished FINAL UNLOCK ILLUSTRATION. Show the satisfied customer holding
the correct fish-shaped pastry with its filling clearly visible. Show ONLY that
customer's assigned soul mascot behind them, preserving its distinctive Figma
silhouette while keeping recognizable bungeoppang features. Use a hero
composition with clean negative space for later UI overlay.
```

## 장면별 원문 기획

- 정현: [`docs/cutscenes/01_JEONGHYUN.md`](../../../docs/cutscenes/01_JEONGHYUN.md)
- 하진: [`docs/cutscenes/02_HAJIN.md`](../../../docs/cutscenes/02_HAJIN.md)
- 미주: [`docs/cutscenes/03_MIJU.md`](../../../docs/cutscenes/03_MIJU.md)
- 건우: [`docs/cutscenes/05_GEONWOO.md`](../../../docs/cutscenes/05_GEONWOO.md)
- 태수: [`docs/cutscenes/06_TAESU.md`](../../../docs/cutscenes/06_TAESU.md)
- 나리: [`docs/cutscenes/07_NARI.md`](../../../docs/cutscenes/07_NARI.md)
- 준호: [`docs/cutscenes/08_JUNHO.md`](../../../docs/cutscenes/08_JUNHO.md)
- 선자 통일본: [`docs/cutscenes/04_SUNJA.md`](../../../docs/cutscenes/04_SUNJA.md)

## 검증 기록

- `asset_report.py --expect-size 1536x1024`: 총 48개 전부 PASS
- 파일 수: 각 인물 폴더 6개 원본 PNG
- Figma: 7페이지, 42프레임, 각 프레임 이미지 1개 + 캡션 인스턴스 1개
- 업로드 과정에서 생긴 복사본 84개는 최종 화면 생성 후 원본 페이지에서 제거했다.
