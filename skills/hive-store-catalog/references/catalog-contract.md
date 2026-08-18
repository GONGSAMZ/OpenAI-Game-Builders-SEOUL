# HIVE 카탈로그 계약

## 데이터 흐름

```text
HIVE 콘솔 Market PID
  → HIVE Web PG 상품 목록 API
  → AWS StoreCatalogService 5분 캐시
  → GET /api/v1/store/catalog
  → Unity 동적 상품 카드
```

HIVE API가 제공하는 값은 PID, 가격, 표시 가격, 이름, 설명, 상품 유형이다. 인게임 지급 `itemId`와 수량은 서버가 Market PID 규칙으로 판정한다. HIVE Web PG 목록에는 이미지 URL이 없으므로 이미지는 별도 S3 버킷에서 제공한다.

## 환경변수

| 이름 | 값 | 용도 |
|---|---|---|
| `STORE_CATALOG_SOURCE` | `static` 또는 `hive` | 상품 목록 원본 선택 |
| `STORE_CATALOG_CACHE_SECONDS` | 기본 `300` | HIVE 정상 응답 캐시 |
| `STORE_PRODUCT_IMAGE_BASE_URL` | 선택 | 기본값 `${PUBLIC_BASE_URL}/store-products` |
| `HIVE_BILLING_APP_ID` | PC 결제 AppID | 상품 목록 조회 대상 |
| `HIVE_BILLING_AUTH_KEY` | AWS 비밀값 | HIVE 서버 API 인증 |

## 카탈로그 응답

```json
{
  "mode": "nicepay-test",
  "source": "hive",
  "updatedAt": "2026-08-19T00:00:00.000Z",
  "ignoredProductCount": 0,
  "products": []
}
```

`source` 의미:

- `static`: 코드 기본 목록을 사용한다.
- `hive`: HIVE에서 새로 동기화했다.
- `hive-cache`: 유효한 정상 캐시를 사용한다.
- `hive-stale-cache`: HIVE 장애 시 만료된 마지막 정상 캐시를 유지한다.
- `static-fallback`: 로그인 전 또는 초기 HIVE 장애로 기존 세 상품을 사용한다.

HIVE 상품 API는 PlayerID를 요구하므로 로그인된 요청에서 목록을 갱신한다. 캐시는 PlayerID별로 분리한다. 로그인 전에는 기존 세 상품을 표시하고, 로그인 성공 시 Unity가 카탈로그를 다시 조회한다.

## 이미지 운영

- CloudFormation 출력: `StoreProductImageBucketName`
- 객체 키: `<marketPid>.png`
- 공개 URL: `${PUBLIC_BASE_URL}/store-products/<marketPid>.png`
- 권장 크기: 512×512 PNG
- CloudFront 기본 TTL: 300초, 최대 3600초

기본 세 이미지는 DEV 배포 워크플로가 자동 업로드한다. 추가 상품 이미지는 버킷에 같은 PID 이름으로 업로드하며 코드나 WebGL을 다시 빌드하지 않는다.

## 공식 문서

- HIVE Web PG 상품 목록: https://developers.hiveplatform.ai/ja/v4.25.7.0/api/hive-server-api/billing/web-pg-payment/
- HIVE 빌링 개요: https://developers.hiveplatform.ai/ko/latest/dev/billing/
- HIVE Market PID 운영: https://developers.hiveplatform.ai/en/v4.25.7.0/operation/billing/marketpid/
