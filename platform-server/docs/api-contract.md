# 웹게임 연동 계약

실제 게임은 서버의 내부 구현을 알 필요 없이 아래 HTTP 계약만 사용합니다. 모든 요청과 응답은 JSON이며 운영 환경에서는 HTTPS를 사용합니다.

## 공개 API

### `GET /api/v1/health`

서버 연결을 확인합니다.

```json
{ "status": "ok", "timestamp": "2026-08-15T00:00:00.000Z" }
```

### `GET /api/v1/config/public`

비밀값을 제외한 현재 연동 모드를 반환합니다.

## Hive 인증

### `GET /api/v1/auth/hive/login`

Hive 로그인 팝업에 사용할 URL을 반환합니다. 브라우저에서는 반드시 `credentials: "include"`로 호출합니다.

```json
{ "loginUrl": "https://sandbox-weblogin.withhive.com/login?param=..." }
```

실제 게임에서는 직접 이 API를 다루기보다 `public/game-bridge.js`의 `loginWithHive()`를 호출합니다.

### `GET /api/v1/auth/session`

헤더에 `Authorization: Bearer <game-session-token>`을 넣습니다. Hive access token이나 refresh token은 게임에 전달하지 않습니다.

### `DELETE /api/v1/auth/session`

현재 게임 세션을 폐기합니다.

## 계정별 도감·스토리 진행

도감과 스토리 진행 API는 모두 게임 세션이 필요하며, 요청에서 PlayerID를 받지 않고 인증된 세션의 `subject`만 사용합니다. 이 경로는 구버전 호환 어댑터이며 기준 데이터는 계정 저장 `PLAYER#<subject> / SAVE#MAIN`입니다. 기존 `PROGRESS#CUSTOMER` 값은 최초 접근 때 단조 병합하고 이관 표시를 남긴 뒤 읽기 기준으로 사용하지 않습니다.

### `GET /api/v1/progress`

로그인한 사용자의 도감 조우 여부와 영구 스토리 진행을 반환합니다.

```json
{
  "schemaVersion": 1,
  "customers": [
    {
      "customerId": "jeonghyeon",
      "met": true,
      "completedTopicIndexes": [0, 1],
      "storyCompleted": false
    }
  ]
}
```

지원하는 `customerId`는 `jeonghyeon`, `hajin`, `miju`, `sunja`, `geonwoo`, `taesu`, `nari`, `junho`다.

### `POST /api/v1/progress/customers/:customerId/met`

인증된 사용자가 해당 손님을 만난 사실을 기록하고 전체 진행 스냅샷을 반환합니다. 한 번 기록된 `met`은 다시 `false`가 되지 않습니다.

### `PUT /api/v1/progress/stories/:customerId`

완료한 대화 주제와 스토리 완료 여부를 서버 상태에 합집합으로 병합하고 전체 진행 스냅샷을 반환합니다.

```json
{
  "completedTopicIndexes": [0, 1, 2],
  "storyCompleted": true
}
```

스토리 갱신은 해당 손님의 `met`도 자동으로 `true`로 만듭니다. 특별 주문 예정일·당일 대화 제한처럼 현재 플레이 세션의 날짜에 종속된 값은 이 계정 API에 저장하지 않습니다.

## OpenAI 프록시

### `POST /api/v1/ai/npc-reaction`

Hive 로그인 후 호출할 수 있습니다.

```json
{
  "situation": "손님이 붕어빵을 기다리고 있다",
  "playerAction": "팥을 듬뿍 넣었다",
  "locale": "ko"
}
```

```json
{
  "text": "손님이 만족스럽게 고개를 끄덕였습니다!",
  "source": "mock",
  "model": "mock"
}
```

현재 엔드포인트는 연동 검증용 예시입니다. 게임 기획이 확정되면 입력과 출력 스키마를 게임 기능에 맞춰 별도 버전으로 추가합니다.

## 사용자별 게임 재화와 웹 상점

### 일반 게임 돈 상점

일반 상점은 아래 API를 사용하며 HIVE 팥 코인·프리미엄 상품과 분리한다. 카탈로그를 제외한 요청은 인증 세션이 필요하고 요청에서 PlayerID를 받지 않는다.

- `GET /api/v1/game-store/catalog` — 일반 상점 상품, 가격, 카테고리, 소유 유형과 효과 반환
- `GET /api/v1/game-store/me` — revision, 일반 돈, 해금 이력, 다음 날 선택 소, 보유 도구, 예약 효과와 상품 상태 반환
- `POST /api/v1/game-store/purchases` — `productId`, `expectedRevision`과 UUID `Idempotency-Key`로 구매
- `POST /api/v1/game-run/start-day` — revision과 UUID 멱등 키로 서버 영업일·`runId` 발급
- `POST /api/v1/game-run/checkpoint` — 서버 발급 `runId`에 안전 지점의 시간·매출·사용량을 저장
- `POST /api/v1/game-run/settle-day` — `runId`, 소별 판매·사용량과 총계를 제출하고 서버가 가격표로 매출·원가·잔액·다음 날짜 계산
- `POST /api/v1/save/reset-run` — 일반 진행만 초기화하고 계정 영역과 HIVE 보유품 보존

구매 상태는 `owned`, `purchasable`, `locked`, `insufficient-funds`다. 비로그인 Unity 클라이언트는 공개 카탈로그를 자체 `login-required` 보기 전용 상태로 표시한다. 변경 API는 최신 `expectedRevision`을 요구하며 충돌 시 `409 SAVE_CONFLICT`와 서버 프로필을 반환한다.

### `GET /api/v1/store/catalog`

상품 목록과 현재 `mock`/`nicepay-test`/`hive-web-shop` 모드를 반환합니다. `STORE_CATALOG_SOURCE=hive`이면 로그인 PlayerID로 HIVE Web PG 상품 목록을 조회하고 5분간 캐시합니다. 로그인 전이나 초기 장애에는 기존 세 상품을 사용하며, 정상 동기화 뒤 장애가 발생하면 마지막 정상 캐시를 유지합니다.

```json
{
  "mode": "nicepay-test",
  "source": "hive",
  "updatedAt": "2026-08-19T00:00:00.000Z",
  "ignoredProductCount": 0,
  "products": []
}
```

새 상품 PID는 `...coin.<item-id>.<quantity>`, `...equipment.<item-id>.1`, `...item.<item-id>.<quantity>` 규칙을 사용합니다. 규칙에 맞지 않는 PID는 지급 사고 방지를 위해 제외됩니다. 상품 이미지는 CloudFront `/store-products/<marketPid>.png`에서 읽고 실패 시 Unity 기본 이미지를 사용합니다.

### NICEPAY 테스트 결제

- `POST /api/v1/store/nicepay/orders` — 로그인 계정과 서버 카탈로그 가격으로 일회성 주문 생성
- `GET /api/v1/store/nicepay/checkout?orderId=...` — NICEPAY 공식 테스트 결제창 시작
- `POST /api/v1/store/nicepay/callback` — 인증 서명·주문 금액·샌드박스 승인 결과를 검증하고 사용자별 아이템 지급

지급 키는 NICEPAY `tid`이므로 동일 콜백이나 재시도에서도 한 번만 지급됩니다. HIVE 전체
PG/영수증/결제 알림 검증은 현재 개발 범위에서 영구 보류합니다.

### `GET /api/v1/store/me`

로그인한 사용자의 서버 보유 아이템, 장착 상태와 개발용 테스트 포인트 지갑을 반환합니다. `red-bean-coin`은 일반 게임 돈 및 테스트 포인트와 분리된 계정 재화입니다.

```json
{
  "inventory": [
    { "itemId": "red-bean-coin", "quantity": 650 },
    { "itemId": "golden-pan", "quantity": 1 }
  ],
  "equipment": {
    "moldSkin": "golden-pan"
  },
  "wallet": {
    "testPoints": 6700
  }
}
```

### `GET /api/v1/store/purchases?limit=20&cursor=<opaque>`

로그인한 현재 계정의 구매 시도를 최신순으로 반환합니다. 기본 20개, 최대 50개이며 `nextCursor`가 있을 때만 다음 페이지를 요청합니다. cursor에는 계정 정보와 무결성 서명이 포함되어 다른 계정에서 재사용하거나 내용을 위조할 수 없습니다.

```json
{
  "purchases": [
    {
      "purchaseId": "public-id",
      "provider": "nicepay-test",
      "productId": "golden-pan",
      "productName": "황금 붕어빵 틀",
      "itemId": "golden-pan",
      "quantity": 1,
      "amount": 3300,
      "currency": "KRW",
      "status": "succeeded",
      "createdAt": "2026-08-21T00:00:00.000Z",
      "updatedAt": "2026-08-21T00:01:00.000Z"
    }
  ],
  "nextCursor": null
}
```

상태는 `pending`, `succeeded`, `failed`, `cancelled`, `expired`입니다. NICEPAY 주문과 Mock 포인트 구매를 기록하며 개발용 직접 지급·테스트 포인트 충전은 제외합니다. HIVE 항목은 영수증 검증을 통과한 결제만 기록합니다. 영수증, 토큰, 거래 원문과 내부 `subject`는 응답에 포함하지 않습니다.

### `PUT /api/v1/store/equipment/mold`

인증 세션의 사용자에게만 황금 틀 장착 상태를 저장합니다. 요청에서 PlayerID를 받지 않습니다.

```json
{ "itemId": "golden-pan" }
```

장착 해제는 `{ "itemId": null }`을 보냅니다. 응답은 `GET /api/v1/store/me`와 같은 최신 `inventory`·`equipment`입니다. 세션 없음은 `401`, 지원하지 않는 아이템은 `400`, 미보유 장착은 `409`를 반환합니다.

### `POST /api/v1/store/mock-purchases`

`STORE_MODE=mock` 전용 테스트 결제 API입니다. 상품의 `testPointPrice`를 로그인 사용자의 테스트 포인트에서 차감한 뒤 아이템을 지급합니다. 같은 UUID `idempotencyKey`는 한 번만 차감·지급되며 잔액 부족은 `409`를 반환합니다.

```json
{ "productId": "red-bean-100", "idempotencyKey": "12e68262-ff70-42b7-ae95-18e89b7bbbd8" }
```

### `POST /api/v1/store/dev-test-points`

`STORE_DEV_TOOLS=true`에서만 열리는 개발용 테스트 포인트 충전 API입니다. 로그인 사용자에게만 적용되며 같은 UUID는 한 번만 반영됩니다.

```json
{ "amount": 10000, "idempotencyKey": "86031de5-0cf2-45e9-b0f4-a2e00defc307" }
```

### `POST /api/v1/store/dev-grants`

`STORE_DEV_TOOLS=true`에서만 열리는 개발용 지급 API입니다. 로그인 사용자의 `red-bean-coin`만 지급하며 UUID 중복 호출은 한 번만 반영됩니다. 외부 웹 지급 UI는 제공하지 않으며 정식 배포에서는 `STORE_DEV_TOOLS=false`로 API를 닫습니다.

```json
{ "productId": "red-bean-100", "idempotencyKey": "33a1454b-180b-4ae1-b92a-cae426265b87" }
```

### `POST /api/v1/hive/web-shop/in-game-info`

HIVE 관리형 웹 상점이 구매 수령 서버·캐릭터를 조회하는 서버 간 API입니다. HIVE Console의 인게임 정보 URL에 등록합니다.

### `POST /api/v1/hive/web-shop/payment-notifications`

HIVE 웹 상점의 결제 알림 URL입니다. `STORE_MODE=hive-web-shop`에서만 처리합니다. `paid` 알림을 받으면 다음 순서로 처리합니다.

1. 알림의 AppID, Market ID 15, 상품 PID와 PlayerID를 검사합니다.
2. 해당 PlayerID의 HIVE 미소비 주문을 조회해 주문번호·서버·상품·수량·`purchase_bypass_info`를 대조합니다.
3. HIVE 영수증 검증 API로 거래 ID를 얻습니다.
4. 거래 ID를 멱등성 키로 사용해 해당 PlayerID의 인벤토리에 한 번만 지급합니다.
5. HIVE 아이템 지급 완료 API를 호출해 거래를 완료합니다.

`cancelled` 알림은 현재 지급과 구매 내역 기록 없이 확인 응답만 합니다. 이미 소비한 재화를 회수하는 정책은 이 초기 개발 범위에 포함되지 않습니다.

황금 틀 장착 상태는 DynamoDB에서 `PLAYER#<subject> / EQUIPMENT#MOLD`, 테스트 포인트는 `PLAYER#<subject> / WALLET#TEST_POINTS` 레코드로 사용자별 저장됩니다. 팥 코인 소비 API는 현재 범위에 포함되지 않습니다.

## 오류 형식

```json
{
  "error": {
    "code": "INVALID_REQUEST",
    "message": "요청 데이터 형식이 올바르지 않습니다."
  }
}
```
# 플레이 저장

로그인 계정의 게임 진행은 인앱 구매 인벤토리와 별도 문서로 저장한다.

### `GET /api/v1/save/profile`

- 인증 필요
- 응답: `{ "profile": SaveProfile | null }`
- 계정 저장이 아직 없으면 `profile`은 `null`이다.

### `PUT /api/v1/save/profile`

- 인증 필요
- 요청: `{ "expectedRevision": number, "profile": SaveProfile }`
- 성공하면 revision을 1 증가시킨 최신 `profile`을 반환한다.
- 다른 기기에서 먼저 저장했다면 `409 SAVE_CONFLICT`와 서버의 최신 `profile`을 반환한다.
- 클라이언트는 충돌 시 서버 데이터를 우선하고 로컬 데이터는 백업한다.

`SaveProfile` v8은 `run`, `account`, `settings`를 저장합니다. `run`에는 일반 돈, 영구 해금 이력 `unlockedFillingIds`, 다음 영업일 선택 `selectedFillingIds`, 일반 상점 도구·예약 효과와 진행 중인 `activeDay` 안전 체크포인트가 있고 `account`에는 업적, 손님 도감·스토리, 영혼 도감과 누적 통계가 있습니다. `settings`에는 `masterVolume`, `keyboardHintsEnabled`, `tutorialCompleted`가 있습니다. 일반 경제·누적 통계·업적과 `activeDay`는 서버 권한 필드이며 전용 영업 트랜잭션 API만 변경합니다. 팥 코인·HIVE 구매·프리미엄 장비는 이 문서가 아니라 기존 서버 권한의 상점 레코드를 기준으로 합니다.

정산 API는 서버 발급 `runId`, 서버 시각상 가능한 손님 수, 손님당 주문 수, 선택한 소, 반죽·소 사용량과 서버 카탈로그 가격을 함께 검증합니다. 클라이언트가 임의의 절대 잔액·누적 통계·업적을 제출해도 반영하지 않습니다. 이 검증은 현재 싱글 플레이 계정 경제의 무결성 경계이며, 향후 경쟁 랭킹에는 서버 관측형 주문·판매 이벤트 원장을 별도로 추가해야 합니다.

v2 계정은 최초 최신 클라이언트 접근 때만 기존 PlayerPrefs 설정을 채웁니다. 서버에 이미 설정이 있으면 서버 값이 우선합니다. 손님 ID `jeonghyun`은 `jeonghyeon`으로 병합하며 복구 가능한 양쪽 진행을 합집합·최댓값으로 유지합니다. 신규 v8 계정의 첫날은 팥만 기본 선택합니다. 기존 계정의 해금 이력은 회수하지 않지만 v7 이관 때 매일 선택된 것으로 취급하지 않습니다. `specialOrderState`는 빈 값·`scheduled`·`retry`만 허용하고 서버 왕복 후 그대로 보존합니다.

운영 게임 세션은 7일 TTL이며 남은 시간이 절반 이하일 때 인증 API 사용으로 연장됩니다. 명시적 로그아웃과 장기 미사용은 만료되며, 일시적인 네트워크 오류나 5xx만으로 클라이언트 계정 상태를 지우지 않습니다. HIVE 로그인 nonce는 운영 DynamoDB에서 한 번만 원자적으로 소비합니다.
