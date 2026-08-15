# 따끈따끈 붕어빵 코드 구조 해설

> 이 문서는 이미 만들어진 게임이 **어떤 파일들이 협력해 실행되는지** 빠르게 파악하기 위한 안내서다. 기능을 새로 설계하는 문서가 아니라, 현재 코드의 흐름을 읽는 지도에 가깝다.

## 1. 전체 구조

```text
Managers
  ├─ GameManagerEx : 게임 진행, 돈, 주문, 하루, 엔딩
  ├─ UIManager     : 화면 생성과 닫기
  ├─ ResourceManager : 프리팹·이미지·데이터 불러오기
  └─ InputManager  : 입력 처리 보조

Controllers
  ├─ FishBunController : 붕어빵 조리와 드래그
  ├─ CustomerController : 손님 주문과 반응
  ├─ ToolController : 조리 도구 선택
  ├─ MoldController : 붕어빵 틀
  └─ DisplateController : 완성품 진열 공간

UI
  ├─ UI_Intro, UI_Game, UI_Store
  ├─ UI_DayEnd, UI_Ending
  └─ UI_Settings, UI_HowToPlay
```

## 2. 게임이 시작되는 흐름

1. 씬의 `@Managers` 오브젝트가 `Managers`를 실행한다.
2. `Managers`는 `GameManagerEx`, `UIManager`, `ResourceManager` 등을 준비한다.
3. `GameManagerEx.InitGame()`이 게임에 필요한 초기 상태를 만든다.
4. UIManager가 인트로 또는 게임 화면 UI를 생성한다.
5. 매 프레임 `Managers`가 `GameManagerEx.OnUpdate()`를 호출해 게임 시간을 진행한다.

**중심 파일:** `Assets/Scripts/Managers/Managers.cs`

## 3. 붕어빵 조리 흐름

```text
도구 선택 → 붕어빵 틀 클릭 → 반죽 넣기 → 재료 넣기 → 굽기
→ 완성품을 손님에게 드래그
```

| 단계 | 담당 파일 | 하는 일 |
| --- | --- | --- |
| 도구 선택 | `ToolController.cs` | 현재 선택된 도구를 기억한다. |
| 조리 상태 변경 | `FishBunController.cs` | 반죽, 재료, 굽기, 완성 상태를 바꾼다. |
| 틀 처리 | `MoldController.cs` | 조리 가능한 붕어빵 틀을 관리한다. |
| 진열 | `DisplateController.cs` | 완성된 붕어빵을 올려둘 공간을 처리한다. |

## 4. 주문과 손님 흐름

1. `CustomerController`가 손님의 주문을 만든다.
2. 주문 정보는 `GameManagerEx`의 주문 목록에 등록된다.
3. `UI_Game`이 주문 목록을 화면에 표시한다.
4. 플레이어가 붕어빵을 손님에게 전달한다.
5. `CustomerController`가 주문과 붕어빵 상태를 확인한다.
6. `GameManagerEx`가 돈, 판매 수, 주문 목록을 갱신한다.
7. 손님의 화남 수치가 한계에 도달하면 손님이 떠난다.

**관련 파일:**

- `Assets/Scripts/Controllers/CustomerController.cs`
- `Assets/Scripts/Managers/GameManagerEx.cs`
- `Assets/Scripts/UI/UI_Scene/UI_Game.cs`
- `Assets/Scripts/UI/UI_Order.cs`

## 5. 하루 진행과 엔딩 흐름

```text
하루 시작 → 영업 시간 감소 → 마감 시간 안내 → 하루 종료 UI
→ 다음 날 시작 또는 엔딩 판단
```

- 날짜와 남은 시간은 `GameManagerEx`가 관리한다.
- 마감 시간 안내는 `UI_AlertClosingTime`이 보여 준다.
- 하루 종료 결과는 `UI_DayEnd`가 보여 준다.
- 최종 엔딩은 `UI_Ending`이 표시한다.

## 6. 데이터와 리소스

| 종류 | 위치 | 용도 |
| --- | --- | --- |
| 게임 규칙 상수 | `Assets/Scripts/Define.cs` | 가격, 문자열, 엔딩 기준 등 |
| 붕어빵 정보 | `Assets/Scripts/Data/FishBunData.cs` | 붕어빵 상태와 데이터 |
| 손님 데이터 | `Assets/Resources/Data/SO/` | 손님별 정보 |
| 이미지·프리팹 | `Assets/Resources/` | UI, 스프라이트, 프리팹 |

`ResourceManager`는 `Resources` 폴더 아래의 프리팹과 이미지를 불러오는 역할을 한다.

## 7. 처음 읽을 때 추천 순서

1. `Managers.cs` — 게임 시작점
2. `GameManagerEx.cs` — 게임 전체 진행
3. `FishBunController.cs` — 조리 기능
4. `CustomerController.cs` — 주문과 결과
5. `UI_Game.cs` — 게임 화면 갱신
6. `ResourceManager.cs`, `UIManager.cs` — 리소스와 UI 생성 방식

## 8. 읽을 때 확인할 질문

- 주문이 새로 생길 때 어떤 코드가 UI 갱신을 알리는가?
- 돈은 어떤 경우에 증가하거나 감소하는가?
- 붕어빵의 조리 상태는 어디서 바뀌는가?
- 손님이 떠날 때 주문 목록과 화면은 어떻게 정리되는가?
- 하루가 끝났을 때 다음 날과 엔딩을 나누는 조건은 무엇인가?

이 질문의 답을 직접 코드에서 찾아보면, 이 프로젝트의 핵심 흐름을 빠르게 이해할 수 있다.
