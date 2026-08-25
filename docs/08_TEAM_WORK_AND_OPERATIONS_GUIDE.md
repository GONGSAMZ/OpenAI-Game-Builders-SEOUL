# 팀 작업·운영 인수인계 가이드

이 문서는 팀원이 기존 작업자를 통하지 않고도 Unity 게임, 플랫폼 서버, HIVE 상품, NICEPAY 테스트 결제와 AWS 배포를 안전하게 변경할 수 있도록 만든 작업 지침서다. 아래 상황에 해당하면 작업 전에 관련 절을 먼저 읽고, 완료 후 문서와 이슈의 상태도 함께 갱신한다.

## 1. 먼저 확인할 것

### 기준 위치

| 대상 | 기준 위치 | 설명 |
|---|---|---|
| Unity 원본 | `BungeoppangTycoon/` | Unity Hub에는 이 폴더를 프로젝트로 추가한다. |
| 배포용 WebGL | `platform-server/game-dist/` | AWS에 배포되는 검증된 WebGL 산출물이다. |
| 플랫폼 서버 | `platform-server/` | HIVE 로그인, 세션, 상점, 결제와 OpenAI 서버 프록시를 담당한다. |
| 브라우저 브리지 | `platform-server/public/game-bridge.js` | 웹 페이지와 Unity WebGL 사이의 호출을 연결한다. |
| AWS 인프라 | `infra/aws/` | CloudFront, ALB, ECS, DynamoDB, S3와 배포 역할을 정의한다. |
| DEV 자동 배포 | `.github/workflows/deploy-dev-to-aws.yml` | `DEV` push를 AWS에 자동 반영한다. |
| 운영·복구 절차 | `infra/aws/operations-runbook.md` | 장애 확인, 롤백, 비용과 리소스 종료 절차다. |
| HIVE 상품 세부 규칙 | `skills/hive-store-catalog/SKILL.md` | 상품 PID, 이미지와 카탈로그 동기화 계약이다. |

### 현재 개발 환경

공개 주소는 <https://d1tmcdkh8akpud.cloudfront.net/>다. 실제 배포 상태는 추측하지 말고 다음 API에서 확인한다.

- `/api/v1/version`: 배포된 Git commit SHA
- `/api/v1/config/public`: HIVE, 상점, 카탈로그와 OpenAI 모드
- `/api/v1/health`: 서버 상태

2026-08-19 기준 개발 환경은 HIVE Production 로그인, NICEPAY 테스트 결제, HIVE 카탈로그, 개발 도구 표시, OpenAI mock 모드다. 이 문장을 설정 변경 시 함께 갱신한다.

## 2. 모든 작업의 공통 순서

1. 독립된 로컬 저장소에서 작업한다. Unity의 `Library/`, `Temp/`, `Logs/`와 개인 설정 파일은 커밋하지 않는다.
2. 작업 시작 전에 원격 `DEV`와 자신의 브랜치를 받는다.
3. 다른 팀원의 변경을 지우는 `reset --hard`나 강제 push를 사용하지 않는다.
4. Unity·서버·인프라 중 무엇이 기준 데이터인지 위 표에서 확인한다.
5. 변경 범위에 맞는 검증을 실행한다.
6. push 직전에 `DEV`가 새로 바뀌었는지 다시 확인해 흡수한다.
7. 현재 팀 통합 절차는 검증한 동일 커밋을 `HYUNJIN`에 먼저 push하고, 그다음 `DEV`에 반영하는 것이다.
8. `DEV` 배포가 성공하고 공개 `/api/v1/version`이 같은 SHA인지 확인한다.
9. 완료한 GitHub 이슈의 체크박스, 검증 결과와 남은 문제를 갱신한다.

```powershell
git fetch origin DEV:refs/remotes/origin/DEV HYUNJIN:refs/remotes/origin/HYUNJIN
git status --short --branch
git log --oneline --decorate -10 --all
```

작업 트리에 자신이 만들지 않은 변경이 있으면 임의로 포함하거나 버리지 말고 작성자와 범위를 먼저 확인한다.

## 3. Unity 게임이나 UI를 바꿀 때

1. Unity Hub에서 `BungeoppangTycoon`을 Unity `6000.3.22f1`로 연다.
2. 필요한 씬·프리팹·스크립트만 수정한다. Unity를 열었다는 이유만으로 대량 재저장된 파일은 변경 이유를 확인한다.
3. Editor Play Mode에서 수정 기능과 시작 → 주문 → 조리 → 판매 → 정산 흐름을 확인한다.
4. WebGL 빌드를 `BungeoppangTycoon/Builds/WebGL`에 생성한다. 자동 빌드 진입점은 `WebGLBuildCommand.Build`다.
5. 플랫폼 서버에서 빌드를 배포 패키지로 옮기고 해시를 검증한다.

```powershell
Set-Location platform-server
pnpm unity:stage
pnpm unity:verify
```

`unity:stage`는 `platform-server/game-dist`를 갱신하면서 Unity 버전, Unity 소스 트리 SHA-256과 산출물 해시를 `build-manifest.json`에 기록한다. Unity 원본을 바꾸고 기존 `game-dist`만 그대로 push하면 CI가 실패해야 정상이다.

배치 빌드는 Unity를 처음부터 WebGL 대상으로 시작해야 한다. `Build` 안에서 플랫폼을 바꾸면 Unity가 WebGL 전용 심볼을 재컴파일하기 전에 빌드가 이어질 수 있으므로 다음처럼 `-buildTarget WebGL`을 지정한다. GitHub Actions의 `targetPlatform: WebGL`도 같은 목적이다.

```powershell
& 'C:\Program Files\Unity\Hub\Editor\6000.3.22f1\Editor\Unity.exe' `
  -batchmode -nographics -quit -buildTarget WebGL `
  -projectPath (Resolve-Path BungeoppangTycoon) `
  -executeMethod WebGLBuildCommand.Build `
  -logFile unity-webgl-build.log
```

UI를 변경했다면 최소 1280×720, 1920×1080, 2560×1080에서 잘림·겹침·마우스 입력을 확인한다. 로그인 문자 입력 중 게임 조작이 실행되지 않는지도 확인한다.

## 4. 플랫폼 서버나 API를 바꿀 때

로컬 환경변수는 `platform-server/.env.example`을 복사해 사용한다. 실제 키는 `.env`, Unity, 브라우저 JavaScript, 문서와 Git에 넣지 않는다.

```powershell
Set-Location platform-server
Copy-Item .env.example .env
pnpm install
pnpm check
pnpm build
pnpm unity:verify
```

- API 계약을 바꾸면 `platform-server/docs/api-contract.md`도 갱신한다.
- 로그인·구매·지급 API는 요청에서 임의의 PlayerID를 받아 다른 사용자의 데이터를 바꾸게 만들지 않는다.
- 결제와 지급에는 거래 ID 또는 UUID 멱등성 키를 유지한다.
- 팥 코인은 일반 게임 돈 `Managers.Game.Money`와 섞지 않는다.
- 로그인 세션, 팥 코인, 인벤토리와 장착 상태의 운영 기준은 DynamoDB다.

### 도감·스토리 계정 동기화

- 현재 단일 기준은 DynamoDB `PLAYER#<subject> / SAVE#MAIN`의 `SaveProfile v6`다. 영업 진행, 일반 돈, 일반 상점 보유 상태, 업적, 손님 도감·스토리, 영혼 도감, 누적 통계와 설정을 revision 충돌 검사로 저장한다. 일반 경제 필드는 전용 조건부 트랜잭션 API만 변경한다.
- `/api/v1/progress`와 두 변경 경로는 구버전 호환용이다. 내부에서는 `SAVE#MAIN`만 갱신하며 기존 `PROGRESS#CUSTOMER`는 최초 접근 때 단조 병합한 뒤 기준 데이터로 사용하지 않는다.
- `masterVolume`, `keyboardHintsEnabled`, `tutorialCompleted`도 계정 설정이다. 구버전 계정에 서버 설정이 없을 때만 기존 PlayerPrefs를 한 번 승격하고, 서버 값이 있으면 서버를 우선한다.
- 손님 ID는 `jeonghyeon`이 표준이다. 기존 `jeonghyun` 값은 완료 항목의 합집합과 횟수·날짜 최댓값으로 병합한다.
- 로그인·로그아웃·계정 전환 중에는 이전 계정 저장을 화면에 남기거나 새 계정에 업로드하지 않는다. 네트워크 실패 시에도 해당 계정 캐시 또는 빈 기본값만 사용한다.
- 팥 코인, 인벤토리, 구매 내역과 황금 틀 장착은 `SAVE#MAIN`에 넣지 않고 서버 권한의 별도 레코드를 유지한다.

## 5. HIVE Console에 상품을 추가하거나 수정할 때

HIVE 상품 목록은 로그인한 PlayerID로 서버가 읽는다. 상품명, 설명과 가격을 HIVE Console에서 바꾸면 Unity 상점 카드도 서버 캐시 만료 후 갱신되므로 카드 추가용 코드를 별도로 만들지 않는다.

### Market PID 규칙

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

- 현재 서버는 `consumable` 상품만 지급 대상으로 사용한다.
- 규칙에 맞지 않는 상품은 지급 사고를 막기 위해 인게임 카탈로그에서 제외된다.
- 기존 세 PID `redbean100`, `redbean550`, `goldenpan`은 하위 호환 규칙으로 계속 지원한다.
- 가격과 지급 수량을 바꿀 때는 표시만 보지 말고 테스트 주문의 서버 스냅샷도 확인한다.

### 상품 이미지

HIVE 상품 목록 API에는 인게임 카드용 이미지가 없으므로 이미지는 AWS 서비스 스택 출력 `StoreProductImageBucketName`의 비공개 S3 버킷에 별도로 올린다.

```text
S3 객체 키: store-products/<전체-market-pid>.png
공개 URL:   https://d1tmcdkh8akpud.cloudfront.net/store-products/<전체-market-pid>.png
```

512×512 PNG, `Content-Type=image/png`, `Cache-Control=public,max-age=300`을 사용한다. 이미지가 없거나 로딩에 실패하면 Unity 기본 이미지가 표시된다.

### 등록 후 검증

1. HIVE로 로그인한다.
2. 장인 마켓을 다시 열거나 새로고침한다.
3. 최대 5분 캐시 후 새 카드·상품명·가격·이미지를 확인한다.
4. `/api/v1/store/catalog`의 `source`가 `hive` 또는 `hive-cache`인지 확인한다.
5. `ignoredProductCount`가 증가했다면 PID, 상품 유형과 지급 수량을 점검한다.

`static-fallback`은 로그인하지 않았거나 HIVE 조회에 실패한 상태다. 이때 기존 세 상품을 표시하는 것은 정상적인 장애 폴백이다.

## 6. NICEPAY 테스트 결제를 확인할 때

현재 `nicepay-test`는 개발용이며 실제 제출 결제 정책과 별개다.

1. HIVE 로그인 계정을 사용한다.
2. 팥 코인 100, 팥 코인 550, 황금 틀을 각각 한 번씩 결제한다.
3. 결제 성공 페이지가 원래 게임 창으로 돌아오는지 확인한다.
4. `/api/v1/store/me`와 게임 HUD의 팥 코인·인벤토리가 일치하는지 확인한다.
5. 새로고침·로그아웃·재로그인 뒤에도 같은 계정에만 지급 내역이 유지되는지 확인한다.
6. 황금 틀은 장착·해제, 금색 외형, 성공 판정 4.8초, 타는 판정 12초를 확인한다.
7. 장인 마켓의 `구매 내역` 탭에서 성공·실패·취소·만료·대기 상태, 더 보기와 오류 재시도를 확인한다.
8. 계정 A의 구매 내역 cursor를 계정 B가 사용할 수 없고 응답에 subject·영수증·토큰이 없는지 확인한다.

결제 창의 성공 표시만으로 지급 완료라고 판단하지 않는다. 서버 인벤토리 반영까지가 완료 기준이다. 일반 게임 돈이 팥 코인 지급으로 변하면 회귀 오류다.

HIVE PG 전체 영수증 검증과 HIVE 결제 알림 E2E는 팀 결정으로 영구 보류 중이다. 이 범위를 다시 시작하려면 먼저 GitHub 이슈의 범위와 운영 책임을 명시적으로 변경한다.

## 7. HIVE 로그인이나 콘솔 설정을 바꿀 때

- HIVE Web Login 자격 증명과 Billing 인증 키는 AWS Secrets Manager `/openai-game-builders-seoul/runtime`에서 관리한다.
- GitHub 변수에는 모드와 비밀이 아닌 식별자만 둔다.
- Redirect URI는 현재 CloudFront 도메인의 `/hive/cb`다.
- 로그인 성공 후 HttpOnly 세션 쿠키와 DynamoDB 세션이 모두 생성돼야 한다.
- 팝업 메시지가 끊겨도 `/api/v1/auth/session`으로 로그인 상태를 복구해야 한다.
- Sandbox와 Production 자격 증명을 섞지 않는다.

세부 콘솔 항목은 `platform-server/docs/hive-console-checklist.md`를 따른다.

## 8. OpenAI 기능을 실제 게임에 연결할 때

현재 AWS는 OpenAI mock 모드이며 `GamePlatformClient.CreateNpcReaction`과 서버 `/api/v1/ai/npc-reaction` 골격만 있다. 실제 기능을 만들 때는 다음을 모두 완료한다.

1. 손님 반응, 대화 또는 특별 주문 등 플레이어가 확인할 실제 Unity 화면과 호출 시점을 정의한다.
2. 서버 입출력 계약과 실패 시 고정 폴백 문구를 정한다.
3. 서버 테스트와 Unity 오류 처리를 추가한다.
4. `OPENAI_API_KEY`를 Secrets Manager에 저장한다. 키를 클라이언트나 Git에 넣지 않는다.
5. GitHub 변수 `OPENAI_MODE=live`를 설정하고 DEV 배포한다.
6. 호출 제한, 지연 시간, 비용과 부적절한 응답 폴백을 확인한다.

API만 `live`로 바꾸고 게임에서 사용하지 않는 상태는 기능 완료가 아니다.

## 9. AWS에 배포할 때

일반 배포에는 AWS 콘솔 로그인이 필요하지 않다. 검증된 변경을 `DEV`에 push하면 GitHub OIDC가 자동 배포를 실행한다. `preflight` 뒤에는 Unity 준비, 플랫폼 서버 검증, bootstrap 데이터 인프라 갱신이 병렬로 시작된다. Unity 소스가 바뀐 커밋은 정확히 그 원본으로 WebGL을 새로 빌드하고, Unity 소스가 바뀌지 않은 서버·워크플로 전용 커밋은 저장된 `game-dist`의 소스·산출물 해시를 재검증해 불필요한 Unity 빌드만 생략한다. 해시가 다르면 배포는 실패해야 정상이다.

검증된 Unity 산출물은 작업 간 artifact로 전달된다. 컨테이너 이미지는 서버 검증 결과를 기다리는 동안 미리 빌드하되, 서버·계정 격리 테스트나 인프라 검증이 실패하면 ECS 배포 단계가 실행되지 않는다. 서비스 반영 후 공개 API·게임·HIVE 검증과 상점 이미지 게시·검증도 병렬로 실행된다.

루트 포털은 서버의 `APP_REVISION`을 CSS, JavaScript와 게임 iframe URL에 자동으로 붙인다. 배포 뒤 화면만 이전 버전이면 먼저 페이지를 새로고침하고 HTML의 `?v=<commit SHA>`와 `/api/v1/version`이 같은지 확인한다. 포털 고정 파일은 `no-cache`로 재검증되므로 날짜나 임의 문자열을 `index.html`에 하드코딩하지 않는다.

구매 내역은 DynamoDB `SubjectCreatedAtIndex`를 사용한다. 워크플로의 `update-infrastructure`와 `verify-runtime-infrastructure`가 bootstrap 스택을 갱신하고 인덱스·PITR·TTL을 확인하며, 이 검증과 이미지 빌드가 모두 성공한 뒤에만 서버를 배포한다.

Unity Personal 라이선스용 GitHub Actions secrets `UNITY_LICENSE`, `UNITY_EMAIL`, `UNITY_PASSWORD`와 AWS 변수 중 하나라도 없으면 preflight가 즉시 실패해야 정상이다. 누락된 Unity 빌드를 건너뛰고 이전 WebGL을 배포하도록 설정하지 않는다.

배포 성공 조건:

- `prepare-unity`의 WebGL 빌드 또는 기존 빌드 해시 검증
- `verify-platform-server`의 서버·계정 격리 테스트
- `update-infrastructure`와 `verify-runtime-infrastructure`
- `build-image`의 검증된 Unity artifact 포함 이미지 게시
- `deploy-service`의 ECS Fargate 반영
- 병렬 `publish-store-assets`와 `verify-public-core`
- `deployment-summary`

배포 뒤 `/api/v1/version`의 SHA가 push한 커밋과 같아야 한다. 실패하면 먼저 Actions의 실패 단계와 CloudFormation 이벤트를 읽는다. 원인을 확인하지 않고 반복 실행하거나 정상 브랜치를 강제 덮어쓰지 않는다.

`infra/aws/bootstrap.yml`이 바뀌어 GitHub 배포 역할 자체에 새 권한이 필요할 때만 AWS 관리자 로그인이 필요하다. 과거 이미지로 되돌릴 때는 `.github/workflows/rollback-dev-aws.yml`과 운영 Runbook을 사용한다.

## 10. 팀원을 AWS·HIVE 관리자로 초대할 때

콘솔 멤버 초대는 Git commit으로 완료되지 않으므로 반드시 `초대 발송`, `수락`, `권한 확인` 세 단계를 기록한다.

- AWS는 가능하면 IAM Identity Center의 개인 계정과 permission set을 사용한다. 동등한 운영 권한이 필요하면 팀 합의 후 관리자 permission set을 배정한다.
- 장기 Access Key를 만들어 메신저로 전달하지 않는다.
- HIVE Console에서는 프로젝트 멤버로 초대하고 App Center, Authentication, Billing/Web Shop 권한이 실제로 열리는지 팀원 계정으로 확인한다.
- 이메일 주소, 임시 암호, 복구 코드와 비밀키는 저장소나 이슈에 기록하지 않는다.
- 완료 후 GitHub 이슈에는 이메일 대신 담당자 GitHub ID, 수락 여부와 권한 범위만 남긴다.

## 11. 제출 후보를 만들 때

현재 개발 도구와 테스트 결제를 그대로 정식 제출판이라고 부르지 않는다. 제출 후보에서는 다음을 확인한다.

- `STORE_DEV_TOOLS=false`로 외부 테스트 포인트 버튼 숨김
- `STORE_MODE`를 제출 정책에 맞게 확정
- OpenAI 실제 기능과 `OPENAI_MODE=live` 검증
- HIVE 로그인·로그아웃과 게스트 플레이 검증
- 시작부터 정산·다음 날·파산 엔딩까지 전체 플레이
- 계정별 저장·재접속, 결제 상품 3종과 황금 틀 장착 검증
- Chrome·Edge의 목표 해상도에서 WebGL 입력과 성능 확인
- 브라우저 콘솔, ECS 로그와 CloudWatch 5xx 확인
- 최종 SHA, 테스트 결과, 알려진 문제와 롤백 대상을 이슈에 기록

## 12. 현재 완료된 기반과 남은 큰 작업

### 완료된 기반

- HIVE Production 회원가입·로그인·로그아웃과 DynamoDB 세션
- 사용자별 팥 코인·인벤토리·황금 틀 장착 저장
- NICEPAY 테스트 결제 후 사용자별 지급
- SaveProfile v6 기반 영업 진행·일반 상점·업적·도감·스토리·영혼·설정의 계정 저장
- 사용자별 구매 내역 API와 인게임 상품/구매 내역 탭
- HIVE Console 상품의 인게임 카탈로그 자동 구성
- 정확한 DEV Unity 소스를 매번 새로 빌드하는 WebGL 해시 검증과 `DEV` → AWS 자동 배포·롤백

### 아직 완료로 보면 안 되는 작업

- OpenAI live 기능의 실제 게임 사용
- 랭킹 API와 인게임 랭킹 UI
- 정현 외 손님의 특별 주문·이야기 콘텐츠
- 레시피 해금 상점
- 전체 Editor/WebGL 회귀 테스트와 출시 후보 QA
- 제출판의 개발 도구 제거
- AWS·HIVE 팀원 관리자 초대의 수락·권한 확인 기록

기능 상태가 바뀌면 이 절과 관련 GitHub 이슈를 같은 커밋에서 갱신한다.
