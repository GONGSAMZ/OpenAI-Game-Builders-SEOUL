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
- [ ] Billing에서 가격표·상품·Market PID·PG사를 설정하고 Sandbox 구매 검증
- [ ] 웹 상점 인게임 정보 URL에 `/api/v1/hive/web-shop/in-game-info` 등록
- [ ] 운영 도메인과 HTTPS 확정 후 Production Redirect URI 별도 등록

예상 로컬 콜백 주소:

```text
http://localhost:3000/hive/cb
```

Hive Console에는 실제 배포 환경에서 브라우저가 접근할 수 있는 HTTPS 주소를 등록해야 합니다. `HIVE_CLIENT_SECRET`은 서버의 비밀 환경변수로만 보관하고 게임 빌드나 저장소에 넣지 않습니다.

공식 문서:

- https://developers.hiveplatform.ai/en/latest-version/api/hive-server-api/web-login/integration/getting-started/
- https://developers.hiveplatform.ai/en/latest/api/hive-server-api/web-login/integration/web-login/
- https://developers.hiveplatform.ai/en/v4.26.3.0/api/hive-server-api/web-login/integration/verify-token-user-info/
- https://developers.hiveplatform.ai/en/latest/operation/community-and-webshop/webshop/webshop_index/
