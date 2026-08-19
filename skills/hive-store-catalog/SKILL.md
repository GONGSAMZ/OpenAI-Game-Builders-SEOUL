---
name: hive-store-catalog
description: HIVE 콘솔의 PC 결제 상품을 붕어빵 타이쿤 인게임 상점과 동기화하고, Market PID 지급 규칙·상품 이미지·AWS 배포를 안전하게 운영한다. HIVE 상품 추가·수정, 인게임 상점 자동 반영, 상품 이미지 업로드, 지급 누락, catalog source 또는 ignoredProductCount 점검 요청에 사용한다.
---

# HIVE 상점 카탈로그 운영

HIVE 콘솔을 상품 정보의 원본으로 사용하고 AWS 서버가 지급 정책을 검증하도록 유지한다. 인증키와 지급 판정은 브라우저·Unity에 두지 않는다.

## 작업 전 확인

1. `HYUNJIN` 작업 트리가 깨끗한지 확인한다.
2. 최신 원격 `DEV`를 받아 팀원 변경을 먼저 흡수한다.
3. `STORE_CATALOG_SOURCE=hive`, `HIVE_BILLING_APP_ID`, AWS Secrets Manager의 `HIVE_BILLING_AUTH_KEY` 설정 여부를 확인한다. 비밀값 자체는 출력하거나 커밋하지 않는다.
4. 상세 API·환경변수·장애 동작이 필요하면 [catalog-contract.md](references/catalog-contract.md)를 읽는다.

## 상품 등록

HIVE 콘솔의 빌링 > 인앱 결제 > 마켓 PID 등록에서 PC 결제 AppID 상품을 소모성 상품으로 등록한다. 이름·설명·Price Tier를 입력하고 다음 PID 형식 중 하나를 사용한다.

```text
com.gongsamz.bungeoppang.coin.<item-id>.<quantity>
com.gongsamz.bungeoppang.equipment.<item-id>.1
com.gongsamz.bungeoppang.item.<item-id>.<quantity>
```

예시:

```text
com.gongsamz.bungeoppang.coin.red-bean-coin.1000
com.gongsamz.bungeoppang.equipment.golden-pan.1
```

- 소문자 영문·숫자·하이픈으로 `item-id`를 작성한다.
- `coin` 상품의 `item-id`는 `-coin`으로 끝낸다.
- `equipment` 수량은 반드시 `1`로 등록한다.
- 기존 `redbean100`, `redbean550`, `goldenpan` PID는 레거시 호환 대상으로 유지한다.
- 규칙에 맞지 않는 PID는 지급 사고를 막기 위해 인게임 목록에서 제외된다.
- 새로운 게임 효과가 필요한 아이템은 자동 등록 전에 Unity와 서버 기능을 먼저 구현한다.

## 상품 이미지

512×512 PNG를 AWS 서비스 스택 출력 `StoreProductImageBucketName` 버킷에 아래 키로 업로드한다.

```text
store-products/<전체-market-pid>.png
```

`Content-Type=image/png`, `Cache-Control=public,max-age=300`을 적용한다. CloudFront의 `/store-products/` 경로가 비공개 S3 버킷을 읽는다. 이미지가 없거나 로딩에 실패하면 Unity 기본 이미지가 표시된다.

## 검증과 배포

1. `platform-server`에서 `pnpm check`와 `pnpm build`를 실행한다.
2. Unity `6000.3.22f1`로 스크립트 컴파일과 WebGL 빌드를 검증한다.
3. 로그인 후 `/api/v1/store/catalog`의 `source`가 `hive` 또는 `hive-cache`인지 확인한다.
4. `ignoredProductCount`가 `0`인지 확인한다. 0이 아니면 HIVE PID 규칙을 수정한다.
5. 인게임 카드의 이름·가격·이미지와 결제 후 사용자별 지급 수량을 확인한다.
6. 검증된 한 커밋을 `HYUNJIN`에 먼저 푸시한 뒤 같은 커밋을 `DEV`에 반영한다.
7. AWS Actions 성공, 공개 health revision, 기본 상품 이미지 URL을 확인한다.

## 안전 규칙

- 클라이언트가 보낸 상품명·가격·지급 아이템을 신뢰하지 않는다.
- HIVE 상품 조회와 결제 검증에는 서버 보관 인증키만 사용한다.
- 알 수 없는 PID를 임의의 아이템으로 지급하지 않는다.
- HIVE 조회 장애 시 해당 PlayerID의 마지막 정상 캐시를 사용하고, 캐시가 없으면 코드에 포함된 기존 세 상품으로 폴백한다.
- NICEPAY 테스트 결제와 HIVE 영수증 검증의 멱등성 로직을 변경하지 않는다.
