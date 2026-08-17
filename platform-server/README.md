# OpenAI Game Builders 웹 연동 베이스캠프

Unity 붕어빵 게임을 같은 도메인에 제공하면서 HIVE Web Login, 웹 상점, OpenAI API 경계를 검증하는 독립 실행형 서버입니다. 기본값은 외부 계정과 API 키가 필요 없는 `mock` 모드입니다.

## 현재 준비된 것

- TypeScript/Express 서버와 `/api/v1/health`
- HIVE 통합 Web Login URL 생성, 콜백 디코딩, `/token` 서버 검증
- Hive 계정 없이 시험하는 mock 로그인
- 외부 토큰을 브라우저에 노출하지 않는 서버 게임 세션과 DynamoDB 세션 영속화
- OpenAI Responses API 서버 프록시와 mock 응답
- 사용자별 테스트 포인트 mock 결제·충전, 중복 차감 방지, 메모리/DynamoDB 아이템 저장소
- 일반 게임 돈과 분리된 팥 코인 및 사용자별 황금 틀 장착 상태
- HIVE 웹 상점 연결, 인게임 정보 조회, 결제 알림·미소비 주문·영수증 검증·지급 완료 API
- `/game/` Unity WebGL 제공 및 루트 화면 iframe 임베드
- 상단 헤더와 게임만 표시하는 최소 웹 셸
- 브라우저용 `game-bridge.js`와 Unity 통합 API
- Unity 프로젝트에 적용된 WebGL `.jslib`/C# 어댑터
- Docker 및 GitHub Actions 기본 설정
- Unity 6.3 WebGL 소스/산출물 SHA-256 매니페스트 검증
- 배포 revision API, smoke test, ECS 자동 롤백과 수동 immutable 이미지 롤백

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

`.env`의 `GAME_BUILD_DIR`을 기존 Unity WebGL 빌드 경로로 두고 브라우저에서 `http://localhost:3000`을 엽니다. 루트 화면에는 상단 헤더와 Unity 게임이 표시됩니다. `STORE_DEV_TOOLS=true`인 개발 환경에서만 게임 바깥 오른쪽에 테스트 결제 패널이 나타납니다. 로그인 사용자는 10,000P로 시작하고, 개발용 충전 또는 1,100P·5,500P·3,300P 상품 결제를 실행할 수 있습니다. 서버의 사용자별 잔액·인벤토리는 실행 중인 게임과 즉시 동기화됩니다.

## 실제 서비스 전환

### Hive Sandbox

`.env`에서 `HIVE_MODE=sandbox`로 바꾸고 HIVE Console에서 받은 네 가지 값을 채웁니다. 현재 HIVE 통합 Web Login은 OAuth Client Secret을 사용하는 `/token` 검증 방식입니다. 자세한 내용은 [HIVE Console 체크리스트](docs/hive-console-checklist.md)를 참고합니다.

### HIVE 웹 상점

HIVE Unity SDK가 WebGL을 빌드 대상으로 제공하지 않으므로 WebGL 게임은 브라우저 브리지에서 HIVE Web Login과 관리형 웹 상점을 사용하고, 서버는 HIVE Billing Server API를 사용합니다. `STORE_MODE=hive-web-shop`, `HIVE_WEB_SHOP_URL`, `HIVE_BILLING_APP_ID`, `HIVE_BILLING_AUTH_KEY`를 설정합니다. 게임 상점의 `팥 코인 충전` 버튼은 HIVE 웹 상점을 열며 서버는 결제 알림을 받은 뒤 PlayerID의 미소비 주문과 영수증을 검증하고, DynamoDB에 한 번만 지급한 뒤 HIVE에 지급 완료를 전송합니다.

개발 환경에서는 `STORE_DEV_TOOLS=true`를 사용합니다. 정식 배포 전에는 반드시 `STORE_DEV_TOOLS=false`로 바꿔 테스트 지급 UI와 API를 함께 비활성화합니다. 상품·PG·콜백 URL과 Billing 인증 키 설정은 [HIVE Console 체크리스트](docs/hive-console-checklist.md)를 따릅니다.

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

- 로컬 기본값에서는 세션·상점 데이터를 메모리에 저장하고, AWS `DATA_STORE=dynamodb` 환경에서는 둘 다 DynamoDB에 유지합니다.
- AI 엔드포인트는 연동 검증용 NPC 반응 예시입니다. 기획 확정 후 게임 기능에 맞춘 입출력 계약으로 버전 관리합니다.
- 실제 HIVE 유료 결제는 Console Web Login AppID, Billing 인증 키, 웹 상점/PG/상품 설정이 준비된 뒤 Sandbox에서 최종 검증해야 합니다.
- 결제 취소 알림은 현재 지급하지 않고 정상 응답만 하며, 이미 사용된 재화의 환불 회수 정책은 정식 출시 전 별도 설계가 필요합니다.
- 게임 세이브·점수·랭킹 DB는 게임 기획과 데이터 소유권이 확정된 후 추가합니다.
