# OpenAI Game Builders 웹 연동 베이스캠프

Unity 붕어빵 게임을 같은 도메인에 제공하면서 HIVE Web Login, 웹 상점, OpenAI API 경계를 검증하는 독립 실행형 서버입니다. 기본값은 외부 계정과 API 키가 필요 없는 `mock` 모드입니다.

## 현재 준비된 것

- TypeScript/Express 서버와 `/api/v1/health`
- HIVE 통합 Web Login URL 생성, 콜백 디코딩, `/token` 서버 검증
- Hive 계정 없이 시험하는 mock 로그인
- 외부 토큰을 브라우저에 노출하지 않는 서버 게임 세션
- OpenAI Responses API 서버 프록시와 mock 응답
- mock 마켓, 중복 구매 방지, 메모리/DynamoDB 아이템 저장소
- HIVE 웹 상점 연결과 인게임 정보 조회 API
- `/game/` Unity WebGL 제공 및 포털 iframe 임베드
- 브라우저용 `game-bridge.js` 및 통합 포털
- Unity 프로젝트에 적용된 WebGL `.jslib`/C# 어댑터
- Docker 및 GitHub Actions 기본 설정

## 로컬 실행

Node.js 22 이상과 pnpm이 필요합니다.

```bash
pnpm install
cp .env.example .env
pnpm dev
```

Windows PowerShell에서는 다음처럼 환경 파일을 복사할 수 있습니다.

```powershell
Copy-Item .env.example .env
pnpm dev
```

`.env`의 `GAME_BUILD_DIR`을 기존 Unity WebGL 빌드 경로로 두고 브라우저에서 `http://localhost:3000`을 엽니다.

1. 포털 안에서 Unity 게임 실행
2. HIVE mock 가입·로그인
3. 장인 상점에서 데모 구매 후 보유 아이템 확인
4. NPC 반응 생성

## 실제 서비스 전환

### Hive Sandbox

`.env`에서 `HIVE_MODE=sandbox`로 바꾸고 HIVE Console에서 받은 네 가지 값을 채웁니다. 현재 HIVE 통합 Web Login은 OAuth Client Secret을 사용하는 `/token` 검증 방식입니다. 자세한 내용은 [HIVE Console 체크리스트](docs/hive-console-checklist.md)를 참고합니다.

### HIVE 웹 상점

WebGL에는 모바일 HIVE SDK IAP를 억지로 넣지 않습니다. HIVE 관리형 웹 상점을 생성한 뒤 `STORE_MODE=hive-web-shop`, `HIVE_WEB_SHOP_URL=https://shop.withhive.com/...`를 설정합니다. 포털은 실제 구매를 HIVE 웹 상점으로 보내고 `/api/v1/hive/web-shop/in-game-info`가 수령 계정을 연결합니다. 상품·PG·아이템 지급 설정은 HIVE Console에서 별도로 완료해야 합니다.

### OpenAI API

`.env`에서 `OPENAI_MODE=live`로 바꾸고 `OPENAI_API_KEY`를 서버 환경변수로 설정합니다. API 키는 브라우저, Unity 프로젝트, Git 저장소에 넣지 않습니다. 구현은 OpenAI의 서버용 JavaScript SDK와 Responses API를 사용합니다.

OpenAI 공식 문서:

- https://developers.openai.com/api/docs/quickstart
- https://developers.openai.com/api/reference/overview#authentication

## 실제 게임 연결

- 엔진과 무관한 HTTP 계약: [API 계약](docs/api-contract.md)
- Unity WebGL: [Unity 연결 방법](docs/unity-webgl-integration.md)
- 브라우저 JavaScript: `public/game-bridge.js`의 `GameBridgeClient` 사용

권장 배치:

```text
브라우저/Unity WebGL
  └─ game-bridge.js
       └─ 이 서버
            ├─ Hive Web Login
            └─ OpenAI Responses API
```

## 현재 제한 사항

- 게임 세션은 단일 프로세스 메모리 방식이라 서버 재배포 시 다시 로그인해야 합니다. 상점 아이템은 AWS에서 DynamoDB에 유지됩니다.
- AI 엔드포인트는 연동 검증용 NPC 반응 예시입니다. 기획 확정 후 게임 기능에 맞춘 입출력 계약으로 버전 관리합니다.
- 실제 HIVE 연동은 Console Web Login AppID, 보안 키, 웹 상점/PG 설정이 준비된 뒤 Sandbox에서 검증해야 합니다.
- 게임 세이브·점수·랭킹 DB는 게임 기획과 데이터 소유권이 확정된 후 추가합니다.
