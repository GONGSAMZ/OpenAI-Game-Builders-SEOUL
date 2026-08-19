# Hive Console 준비 체크리스트

`HIVE_MODE=mock`에서는 아래 값 없이 전체 로컬 흐름을 시험할 수 있습니다. 실제 Sandbox 전환 시 다음 항목을 준비합니다.

- [ ] Hive Console 프로젝트 생성
- [ ] 용도가 Community 또는 Website인 Web Login AppID 생성
- [ ] App Center → Security Key Settings에서 OAuth 2.0 Client ID와 Client Secret 발급
- [ ] Authentication → Login Settings에서 사용할 IdP와 Redirect URI 등록
- [ ] HIVE 회원가입을 허용하려면 Sign-up/Membership 등록 옵션 활성화
- [ ] 서버 콜백 주소를 Redirect URI로 등록
- [ ] Google 또는 Apple 등 IdP 콘솔에도 Hive가 안내하는 Redirect URI 등록
- [ ] `.env`의 `HIVE_MODE=sandbox` 설정
- [ ] `HIVE_APP_ID`, `HIVE_CLIENT_ID`, `HIVE_CLIENT_SECRET`, `HIVE_REDIRECT_URI` 입력
- [ ] Sandbox 로그인 → 콜백 → 통합 Web Login `/token` 검증 → 게임 세션 발급 확인
- [ ] Community & Web Shop에서 사이트와 웹 상점 주소 생성
- [ ] Billing 인증 키를 발급하고 AWS Secrets Manager의 `HIVE_BILLING_AUTH_KEY`에 저장
- [ ] `HIVE_BILLING_APP_ID`를 웹 상점 Billing AppID로 설정
- [ ] GitHub 변수 `STORE_CATALOG_SOURCE=hive` 설정
- [ ] 새 Market PID를 `...coin.<item-id>.<quantity>`, `...equipment.<item-id>.1`, `...item.<item-id>.<quantity>` 규칙으로 등록
- [ ] 서비스 스택 출력 `StoreProductImageBucketName` 버킷에 `store-products/<전체-market-pid>.png` 키로 512px 상품 이미지 업로드
- [ ] Billing에서 가격표·상품·Market PID·PG사를 설정하고 Sandbox 구매 검증
- [ ] 상품 PID `com.gongsamz.bungeoppang.redbean100`을 팥 코인 100개 상품에 연결
- [ ] 상품 PID `com.gongsamz.bungeoppang.redbean550`을 팥 코인 550개 상품에 연결
- [ ] 웹 상점 인게임 정보 URL에 `/api/v1/hive/web-shop/in-game-info` 등록
- [ ] 웹 상점 결제 알림 URL에 `/api/v1/hive/web-shop/payment-notifications` 등록
- [ ] Sandbox 결제 후 HIVE 미소비 주문 조회 → 영수증 검증 → 사용자별 지급 → 지급 완료 확인
- [ ] 정식 배포 변수 `STORE_DEV_TOOLS=false` 확인
- [ ] 운영 도메인과 HTTPS 확정 후 Production Redirect URI 별도 등록

예상 로컬 콜백 주소:

```text
http://localhost:3000/hive/cb
```

Hive Console에는 실제 배포 환경에서 브라우저가 접근할 수 있는 HTTPS 주소를 등록해야 합니다. `HIVE_CLIENT_SECRET`은 서버의 비밀 환경변수로만 보관하고 게임 빌드나 저장소에 넣지 않습니다.

현재 DEV CloudFront 기준 등록 후보:

```text
https://d1tmcdkh8akpud.cloudfront.net/api/v1/hive/web-shop/in-game-info
https://d1tmcdkh8akpud.cloudfront.net/api/v1/hive/web-shop/payment-notifications
```

`HIVE_BILLING_AUTH_KEY`도 서버 비밀값이며 GitHub 변수나 저장소에 평문으로 넣지 않습니다. AWS에는 기존 애플리케이션 비밀 JSON의 키로 저장합니다.

공식 문서:

- https://developers.hiveplatform.ai/en/latest-version/api/hive-server-api/web-login/integration/getting-started/
- https://developers.hiveplatform.ai/en/latest/api/hive-server-api/web-login/integration/web-login/
- https://developers.hiveplatform.ai/en/v4.26.3.0/api/hive-server-api/web-login/integration/verify-token-user-info/
- https://developers.hiveplatform.ai/en/latest/operation/community-and-webshop/webshop/webshop_index/
- https://developers.hiveplatform.ai/en/latest-version/api/hive-server-api/billing/pg-payment/
- https://developers.hiveplatform.ai/en/latest/api/hive-server-api/billing/verify-receipt/
- https://developers.hiveplatform.ai/en/latest-version/api/hive-server-api/billing/item-result/
