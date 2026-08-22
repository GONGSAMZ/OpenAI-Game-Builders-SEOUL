# Story Cutscenes v2

이 폴더는 손님 8명의 통일 화풍 컷씬 원본을 보관한다. 각 손님 폴더에는 `story-01.png`부터 `story-05.png`, `unlock.png`까지 6장이 있으며 모든 파일은 1920×1080 PNG다.

## 기준

- 기준 화풍: `cover-thumbnail-customer-ensemble-1920x1080.png`의 따뜻한 겨울 수채화·구아슈 표현
- 장면 분리: 크림색 직선 만화 칸만 사용하며 블러·페더·그라데이션 경계는 사용하지 않음
- 이야기 화면: 하단 300px UI 안전 영역 확보
- 해금 화면: 손님, 속이 보이는 정답 붕어빵, 해당 붕어혼을 같은 조명 안에 배치
- 이미지 내부 텍스트 없음. 제목·대사·진행 표시는 Figma 캡션 컴포넌트가 담당

## 캐릭터별 파일

- `jeonghyeon/`
- `hajin/`
- `miju/`
- `sunja/`
- `geonwoo/`
- `taesu/`
- `nari/`
- `junho/`

## Figma

- 작업 파일: https://www.figma.com/design/Yrun8rClSF4bLDLSsDDjuQ/GONGSAMZ-%C2%B7-8-Customers-Character-Sheets?node-id=78-55
- `Archive · Cutscenes v1`: 교체 전 48장 보존
- `00 · Cutscene Art Direction`: 표지, 공식 캐릭터 시트, 붕어혼, 대표 컷 기준, 8명 첫 장면·해금 비교 시트
- 라이브 컷씬 페이지: 손님마다 정확히 6개 프레임, 기존 클릭·키보드 프로토타입 연결 유지

## 최신 이야기 반영

- 태수: 라디오 소재 제거, 딸에게 짧은 사과 문자를 보내는 이야기
- 나리: 이사 소재 제거, 배달 앱을 끄고 저녁 약속을 지키는 이야기
- 준호: 도예 소재 제거, 국가대표가 된 친구에게 축하와 질투를 함께 말하는 이야기

정답 붕어혼 이름과 조합은 `docs/06_CUSTOMER_STORY_AND_SOUL_DESIGN.md`를 기준으로 한다.

## 검수 파일

- `_qa-first-and-unlock.png`: 8명의 첫 장면과 해금 장면 비교
- `_qa-all-48.png`: 전체 48장 비교
- `_qa-figma-*.png`: Figma에서 캡션까지 포함해 다시 내보낸 표본

생성 도구: OpenAI 이미지 생성. 최종 크기 정규화와 Figma 배치는 Codex가 수행했다.
