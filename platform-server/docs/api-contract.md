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

### `GET /api/v1/store/catalog`

공개 상품 목록과 현재 `mock`/`hive-web-shop` 모드를 반환합니다.

### `GET /api/v1/store/me`

로그인한 사용자의 서버 보유 아이템과 장착 상태를 반환합니다. `red-bean-coin`은 일반 게임 돈과 분리된 계정 재화입니다.

```json
{
  "inventory": [
    { "itemId": "red-bean-coin", "quantity": 650 },
    { "itemId": "golden-pan", "quantity": 1 }
  ],
  "equipment": {
    "moldSkin": "golden-pan"
  }
}
```

### `PUT /api/v1/store/equipment/mold`

인증 세션의 사용자에게만 황금 틀 장착 상태를 저장합니다. 요청에서 PlayerID를 받지 않습니다.

```json
{ "itemId": "golden-pan" }
```

장착 해제는 `{ "itemId": null }`을 보냅니다. 응답은 `GET /api/v1/store/me`와 같은 최신 `inventory`·`equipment`입니다. 세션 없음은 `401`, 지원하지 않는 아이템은 `400`, 미보유 장착은 `409`를 반환합니다.

### `POST /api/v1/store/mock-purchases`

`STORE_MODE=mock` 전용 데모 지급 API입니다. 같은 UUID `idempotencyKey`는 한 번만 지급됩니다.

```json
{ "productId": "red-bean-100", "idempotencyKey": "12e68262-ff70-42b7-ae95-18e89b7bbbd8" }
```

### `POST /api/v1/store/dev-grants`

`STORE_DEV_TOOLS=true`에서만 열리는 개발용 지급 API입니다. 로그인 사용자의 `red-bean-coin`만 지급하며 UUID 중복 호출은 한 번만 반영됩니다. 정식 배포에서는 `STORE_DEV_TOOLS=false`로 UI와 API를 함께 닫습니다.

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

`cancelled` 알림은 현재 지급 없이 확인 응답만 합니다. 이미 소비한 재화를 회수하는 정책은 이 초기 개발 범위에 포함되지 않습니다.

황금 틀 장착 상태는 DynamoDB에서 `PLAYER#<subject> / EQUIPMENT#MOLD` 레코드로 사용자별 저장됩니다. 팥 코인 소비 API는 현재 범위에 포함되지 않습니다.

## 오류 형식

```json
{
  "error": {
    "code": "INVALID_REQUEST",
    "message": "요청 데이터 형식이 올바르지 않습니다."
  }
}
```
