# 손님 8명 스토리 컷씬 기획

> 문서 상태: 콘텐츠 기획 v1  
> 기준 문서: [`docs/06_CUSTOMER_STORY_AND_SOUL_DESIGN.md`](../06_CUSTOMER_STORY_AND_SOUL_DESIGN.md)  
> 대상 콘텐츠: 특별 주문 정답 성공 뒤 재생되는 손님 이야기

## 1. 공통 재생 구조

모든 손님 이야기는 `스토리 장면 5개 + 해금 일러스트 1개`로 통일한다. 한 장면은 웹툰처럼 2~4개의 칸으로 나눈 한 화면이며, 플레이어가 클릭하거나 `Space`·`Enter`를 눌렀을 때 다음 화면을 공개한다.

```text
특별 주문 정답 판정
→ 장면 1: 과거 또는 문제의 시작
→ 장면 2: 현재까지 이어진 습관
→ 장면 3: 붕어빵 영혼의 등장과 전환
→ 장면 4: 손님이 선택한 작은 첫 행동
→ 장면 5: 며칠 뒤의 변화
→ 손님 이야기·붕어빵 영혼 해금 일러스트
```

- 자동 재생하지 않는다. 플레이어가 대사를 읽고 직접 넘긴다.
- 내레이션 음성 및 캐릭터 음성은 사용하지 않는다.
- 내레이션 문장은 화면 하단 캡션으로만 표시한다.
- 이미지 안에는 글자를 굽지 않고, 대사·캡션·버튼은 Unity UI로 올린다.
- 효과음은 종이 넘김, 휴대폰 진동, 천, 바느질, 문 열림처럼 장면을 이해시키는 짧은 소리만 선택적으로 사용한다.
- 정답 영혼은 손님의 문제를 해결하거나 치료하지 않는다. 손님이 평소와 다른 작은 행동을 한 번 선택하도록 등을 민다.
- 마지막 장면에서도 갈등이 완전히 사라졌다고 표현하지 않는다. 다음 일반 방문 대사에서 변화가 이어지고 있음을 보여 준다.

## 2. PC 화면 기준

- 기준 화면은 `1920×1080`, 16:9다.
- 만화 칸은 화면 중앙의 안전 영역 안에 배치하고, 핵심 얼굴과 손동작을 화면 가장자리에서 최소 80px 떨어뜨린다.
- 하단 260~320px에는 반투명 대화 패널을 둘 수 있도록 중요한 장면 정보를 피한다.
- 현재 진행은 `1 / 5`처럼 표시한다.
- 기본 포커스는 `다음`이며 마우스 클릭, `Space`, `Enter`로 같은 동작을 수행한다.
- 해금 화면에서 `도감에서 보기`와 `영업으로 돌아가기`를 제공한다.
- 이미 본 이야기는 손님 도감에서 장면 단위로 다시 볼 수 있다.

## 3. 캐릭터별 문서

| 번호 | 손님 | 이야기 제목 | 정답 영혼 | 문서 |
| --- | --- | --- | --- | --- |
| 01 | 정현 | 내일의 나에게 남겨 두기 | 칼퇴대장 크림붕 · 슈크림 + 바삭 | [01_JEONGHYUN.md](01_JEONGHYUN.md) |
| 02 | 하진 | 교과서 밖의 첫 장면 | 샛길요정 민트붕 · 민트 + 말랑 | [02_HAJIN.md](02_HAJIN.md) |
| 03 | 미주 | 내가 먼저 고른 노래 | 취향선언 피자붕 · 피자 + 바삭 | [03_MIJU.md](03_MIJU.md) |
| 04 | 선자 | 주머니에 담아 가는 겨울 | 오늘출발 말차붕 · 녹차 + 바삭 | [04_SUNJA.md](04_SUNJA.md) |
| 05 | 건우 | 오늘만은 어린이 | 놀자대장 초코붕 · 초코 + 말랑 | [05_GEONWOO.md](05_GEONWOO.md) |
| 06 | 태수 | 고친 라디오에 남은 말 | 먼저안아 치즈붕 · 크림치즈 + 말랑 | [06_TAESU.md](06_TAESU.md) |
| 07 | 나리 | 다시 돌아올 주소 | 한자리 고구붕 · 고구마 + 노릇 | [07_NARI.md](07_NARI.md) |
| 08 | 준호 | 기록이 없는 첫 번째 | 첫판환영 팥붕 · 팥 + 말랑 | [08_JUNHO.md](08_JUNHO.md) |

## 4. 공통 제작 완료 조건

- [x] 특별 주문 대사의 단서가 정답 맛과 굽기 성격을 유추할 수 있게 한다.
- [x] 5개 장면에서 과거, 현재, 전환, 첫 행동, 이후 변화가 구분된다.
- [x] 각 화면은 정지 이미지로도 인물과 행동을 이해할 수 있다.
- [x] 손님과 영혼의 외형이 캐릭터 시트와 일치한다.
- [x] 마지막 일러스트에 만족한 손님, 완성 붕어빵, 정답 영혼이 모두 보인다.
- [x] 대사와 캡션은 이미지와 분리해 수정 가능하다.
- [x] 음성이 없어도 읽는 순서와 감정 흐름이 명확하다.
- [ ] 도감 해금과 저장이 해금 일러스트 진입 시점에 한 번만 처리된다.

## 5. Figma 컷씬 시안

2026-08-17 기준으로 선자를 포함한 손님 8명의 `장면 5개 + 해금 1개` 화면을 만들었다. 이번 작업에서 추가한 7명은 모두 `1920×1080` 화면이며, 화면 클릭 또는 `Space`·`Enter`로 다음 장면으로 이동한다. 마지막 화면은 전환 없이 도감 연결을 구현할 자리로 남겨 두었다.

| 손님 | Figma 시작 화면 | 로컬 이미지 폴더 |
| --- | --- | --- |
| 정현 | [01 · 삭제된 저녁](https://www.figma.com/design/Yrun8rClSF4bLDLSsDDjuQ/GONGSAMZ-%C2%B7-8-Customers-Character-Sheets?node-id=78-3) | `resources/story-cutscenes/jeonghyeon/v1/` |
| 하진 | [01 · 40초 안의 이야기](https://www.figma.com/design/Yrun8rClSF4bLDLSsDDjuQ/GONGSAMZ-%C2%B7-8-Customers-Character-Sheets?node-id=78-56) | `resources/story-cutscenes/hajin/v1/` |
| 미주 | [01 · 보내지 못한 한 표](https://www.figma.com/design/Yrun8rClSF4bLDLSsDDjuQ/GONGSAMZ-%C2%B7-8-Customers-Character-Sheets?node-id=78-109) | `resources/story-cutscenes/miju/v1/` |
| 선자 | [01 · 겨울의 기억](https://www.figma.com/design/Yrun8rClSF4bLDLSsDDjuQ/GONGSAMZ-%C2%B7-8-Customers-Character-Sheets?node-id=53-17) | `resources/story-cutscenes/sunja/v2/` |
| 건우 | [01 · 냉장고 할 일 표](https://www.figma.com/design/Yrun8rClSF4bLDLSsDDjuQ/GONGSAMZ-%C2%B7-8-Customers-Character-Sheets?node-id=78-162) | `resources/story-cutscenes/geonwoo/v1/` |
| 태수 | [01 · 듣지 않은 대답](https://www.figma.com/design/Yrun8rClSF4bLDLSsDDjuQ/GONGSAMZ-%C2%B7-8-Customers-Character-Sheets?node-id=78-215) | `resources/story-cutscenes/taesu/v1/` |
| 나리 | [01 · 돌려준 열쇠들](https://www.figma.com/design/Yrun8rClSF4bLDLSsDDjuQ/GONGSAMZ-%C2%B7-8-Customers-Character-Sheets?node-id=78-268) | `resources/story-cutscenes/nari/v1/` |
| 준호 | [01 · 마지막으로 멈춘 시간](https://www.figma.com/design/Yrun8rClSF4bLDLSsDDjuQ/GONGSAMZ-%C2%B7-8-Customers-Character-Sheets?node-id=78-321) | `resources/story-cutscenes/junho/v1/` |

이미지 생성 기준, 참조 이미지, 재생성용 프롬프트 조립 규칙은 [`resources/story-cutscenes/all-customers/README.md`](../../resources/story-cutscenes/all-customers/README.md)에 기록한다.
