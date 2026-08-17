# 선자 스토리 컷씬 이미지 시안 v2

선자 컷씬을 나머지 7명과 같은 시각 언어로 다시 만든 버전이다. v1의 이야기와 대사는 유지하고, 장면의 밝기·인물 비율·배경 밀도·해금 일러스트 구성을 통일했다.

## 변경 기준

- 밝고 단순한 동화책 톤에서 짙은 겨울 조명과 촘촘한 수채·과슈 질감으로 변경했다.
- 인물을 귀여운 축약 비율보다 나머지 손님 컷씬의 성숙한 비율에 맞췄다.
- `차가운 기억 → 멈춘 밤 → 녹색 각성 → 열린 아침 → 밝은 해금`의 색 흐름을 강화했다.
- 보라색 코트, 녹색 천, 목도리의 보라색 안감 주머니, 사진 방향을 장면 사이에서 이어지게 했다.
- 조리 결과와 영혼 모두 녹차 속이 분명히 보이게 했다.

## 파일

| 순서 | 파일 | 장면 |
| --- | --- | --- |
| 1 | `sunja-story-01-winter-memory-v2.png` | 젊은 선자와 남편이 녹색 천을 함께 두르는 겨울의 기억 |
| 2 | `sunja-story-02-stopped-sewing-v2.png` | 끝내지 않으려고 보라색 코트를 반복해 수선하는 밤 |
| 3 | `sunja-story-03-matcha-awakening-v2.png` | 녹차 속과 향에서 오늘출발 말차붕이 나타나는 전환 |
| 4 | `sunja-story-04-new-sewing-v2.png` | 새 천을 펼치고 추억을 담을 주머니를 만드는 아침 |
| 5 | `sunja-story-05-walking-forward-v2.png` | 사진을 넣은 목도리를 두르고 산책길로 나가는 변화 |
| 해금 | `sunja-unlock-illustration-v2.png` | 만족한 선자, 녹차 붕어빵, 오늘출발 말차붕의 해금 화면 |

`sunja-v2-contact-sheet.png`는 여섯 장의 흐름을 비교하기 위한 검수표이고, `figma-sunja-cutscene-page-v2-qa.png`는 Figma 적용 결과다.

## 제작 및 검증 기록

- 생성일: 2026-08-17
- 생성 도구: Codex 내장 OpenAI 이미지 생성 도구
- 캐릭터 참조: `assets/figma-8-customers/04_Sunja.png`
- 화풍 참조: `resources/story-cutscenes/taesu/v1/taesu-story-05-same-frequency.png` 및 바로 앞 승인 장면
- 영혼 참조: `resources/character-art/bungeoppang-system/special-souls-v3.png` 위 4
- 형식: PNG, RGB, `1536 × 1024`, 불투명 배경
- 자동 검사: 6개 원본 모두 `asset_report.py --expect-size 1536x1024` 통과
- Figma 적용: 기존 이미지 레이어 `53:18`, `53:26`, `53:34`, `53:42`, `53:50`, `53:58`의 이미지만 교체했다. 캡션과 클릭 전환은 유지했다.

