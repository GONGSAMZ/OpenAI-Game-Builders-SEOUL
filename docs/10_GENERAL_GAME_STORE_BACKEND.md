# 일반 상점·계정 저장 백엔드 가이드

## 목적

이 문서는 Figma와 Unity의 `내일 장사 준비` UI를 일반 게임 돈 기반 상점에 연결할 때 지켜야 할 서버 계약을 정리한다. HIVE 프리미엄 상점과 일반 상점은 결제 재화, 상품 ID, 저장 위치와 구매 흐름을 공유하지 않는다.

## 현재 UI 대조표

팀원이 만든 `UI_Store.prefab`의 화면 구조는 바꾸지 않고 아래 이름을 서버 상품 ID에 연결한다.

| Unity 카드 | 서버 상품 ID | 효과 |
| --- | --- | --- |
| `RedBeanCard` | `filling-red-bean` | 팥 해금, 신규 계정 기본 보유 |
| `CustardCard` | `filling-custard` | 슈크림 해금 |
| `ChocolateCard` | `filling-nutella` | 초코 해금 |
| `CreamCheeseCard` | `filling-cream-cheese` | 크림치즈 해금 |
| `GoldenPanCard` | `item-double-golden-mold` | 같은 단계의 인접 두 틀 동시 조리·뒤집기 |
| `DualPourCard` | `item-dual-pour` | 인접 유효 틀까지 반죽 동시 붓기 |
| `CookingFeverCard` | `item-cooking-fever` | 다음 날 첫 30초 굽기 시간 20% 단축 |
| `NextItemCard` | 없음 | 준비 중 표시 전용 |

카드 내부의 `ProductNameText`, `ProductDescriptionText`, `PriceText`, `PurchaseButton`, `Label`, `PurchaseSurface`를 데이터 바인딩에 사용한다. UI 담당자가 이름이나 계층을 바꾸면 이 문서와 `UI_Store.cs`의 매핑을 함께 갱신한다. 새 디자인을 코드가 임의로 재생성하지 않는다.

## 재화와 저장 경계

- 일반 상점은 `run.money`만 사용한다. 팥 코인은 표시할 수 있지만 차감하지 않는다.
- 일반 재료·영구 도구·예약 효과는 `PLAYER#<subject> / SAVE#MAIN`의 `run` 영역에 저장한다.
- 계정 도감·스토리·업적·설정은 같은 프로필의 `account`·`settings` 영역에 유지한다.
- HIVE 팥 코인, 프리미엄 `golden-pan`, 장착 상태와 구매 원장은 기존 별도 상점 레코드를 기준으로 한다.
- 전체 프로필 PUT은 `nextDay`, `money`, `unlockedFillingIds`, `ownedGameplayItemIds`, `queuedDayEffects`를 변경할 수 없다. 이 값은 전용 구매·정산·초기화 API만 변경한다.

SaveProfile v6의 신규 계정은 팥만 기본 보유한다. 기존 v5 이하 계정이 이미 보유한 슈크림·초코·크림치즈는 정규화 과정에서 회수하지 않는다. 진행 초기화는 `run`만 초기값으로 바꾸며 계정 영역과 HIVE 보유품은 유지한다.

## API 사용 순서

1. 화면을 열 때 공개 `GET /api/v1/game-store/catalog`를 호출한다.
2. 로그인 상태면 `GET /api/v1/game-store/me`를 추가 호출한다. 비로그인은 카탈로그만 표시하고 버튼을 `로그인 필요`로 잠근다.
3. 구매는 UUID `Idempotency-Key`, `productId`, 화면에서 받은 `expectedRevision`으로 `POST /api/v1/game-store/purchases`를 호출한다.
4. 하루 종료는 절대 잔액 대신 날짜·매출·재료비·판매량·손님 수를 `POST /api/v1/game-run/settle-day`에 보낸다. 서버가 잔액과 다음 날짜를 계산한다.
5. 새 게임은 `POST /api/v1/save/reset-run`을 사용한다. 전체 프로필 PUT으로 초기화를 흉내 내지 않는다.

구매·정산·초기화는 DynamoDB 조건부 트랜잭션으로 저장 프로필과 멱등 영수증을 함께 기록한다. 동일 키·동일 입력 재시도는 한 번만 반영하며, 같은 키를 다른 입력이나 계정에서 재사용하면 `IDEMPOTENCY_CONFLICT`다. 일반 상점 영수증은 감사와 중복 방지 전용이며 HIVE 구매 내역 UI에는 노출하지 않는다.

## 게임 효과 규칙

- 재료 선택과 주문 생성은 `unlockedFillingIds` 집합을 기준으로 한다.
- 황금 2구 틀은 인접 두 붕어빵의 단계와 진행 가능 상태가 같을 때만 둘을 함께 진행한다. 조건이 맞지 않으면 클릭한 틀만 처리한다.
- 동시 붓기는 실제로 빈 인접 틀에 적용된 횟수만큼 기존 재료비 계산을 각각 실행한다.
- 조리 피버는 예약된 `targetDay`의 게임 시간 0초 이상 30초 미만에서만 0.8 배율을 적용한다.
- 프리미엄 `golden-pan` 0.8과 피버 0.8은 곱연산해 0.64가 된다.
- 굽기 배율은 윗반죽을 붓고 굽기를 시작할 때 한 번 저장한다. 이후 장비나 시간이 바뀌어도 해당 붕어빵의 성공·타는 판정 시간은 바뀌지 않는다.

## 변경 전 검수

- 최신 `DEV`와 Figma에서 카드 이름, 상품 수, 프리팹 계층과 이미 구현된 효과를 다시 대조한다.
- UI 담당자의 최신 결과가 이 문서와 충돌하면 UI 코드를 덮어쓰지 말고 상품 ID와 동작을 팀에서 다시 확정한다.
- 서버 `pnpm check`, Unity EditMode, Unity `6000.3.22f1` WebGL 빌드를 모두 통과시킨다.
- 일반 돈과 팥 코인, 일반 황금 2구 틀과 프리미엄 `golden-pan`이 섞이지 않는지 두 계정으로 확인한다.
- 위 계정 격리 회귀는 `platform-server`에서 `pnpm test:account-scope`로 자동 확인한다. 세부 항목과 JSON 출력은 [계정 단위 데이터 테스트 툴](11_ACCOUNT_SCOPE_TEST_TOOL.md)을 따른다.
