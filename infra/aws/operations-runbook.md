# AWS 운영·복구 런북

## 운영 구성

- `openai-game-builders-seoul-bootstrap`: GitHub OIDC 역할, immutable ECR, DynamoDB, Secrets Manager, ECS 역할, SNS 운영 알림, 월 $30 Budget을 소유합니다.
- `openai-game-builders-seoul-service`: VPC, ALB, CloudFront, ECS Fargate, CloudWatch Logs/5xx Alarm을 소유합니다.
- 공개 경로: CloudFront HTTPS → ALB HTTP → ECS `web:3000`
- 상품 이미지 경로: CloudFront `/store-products/*` → 비공개 S3 버킷 (`StoreProductImageBucketName` 출력)
- 데이터: 마켓·인벤토리·로그인 세션은 DynamoDB에 저장합니다. 세션은 `expiresAtEpoch` TTL로 만료됩니다.
- 복구: DynamoDB PITR은 항상 켜며, 배포 전에 Actions가 PITR/TTL 상태를 검사합니다.

## 정상 배포와 확인

`DEV`에 push하면 `Deploy DEV to AWS`가 다음 순서로 실행됩니다.

1. Unity CI가 켜져 있으면 Unity 6.3 WebGL을 새로 빌드하고, 아니면 저장소의 검증된 WebGL 산출물을 사용합니다.
2. Node 타입 검사·테스트·빌드를 통과시킵니다.
3. `${GITHUB_SHA}-${GITHUB_RUN_ATTEMPT}` immutable ECR 태그를 push합니다.
4. CloudFormation으로 ECS task definition/service를 갱신합니다.
5. `/health`, `/version`, 포털, Unity 로더, HIVE production 로그인 URL smoke test를 통과시킵니다.

현재 배포 확인:

```bash
curl https://d1tmcdkh8akpud.cloudfront.net/api/v1/health
curl https://d1tmcdkh8akpud.cloudfront.net/api/v1/version
```

두 응답의 `revision`은 배포한 Git commit SHA와 같아야 합니다.

## 롤백

ECS deployment circuit breaker가 새 task를 정상 상태로 만들지 못하면 이전 task definition으로 자동 롤백합니다.

이미 정상 배포된 과거 이미지로 명시적으로 되돌릴 때는 GitHub Actions에서 `Roll back DEV on AWS`를 열고 브랜치를 `DEV`로 선택한 뒤 과거 ECR 태그(예: `<40자리 SHA>-1`)를 입력합니다. 워크플로가 CloudFormation을 갱신하고 `/health`의 revision까지 검증합니다.

이미지 태그는 성공한 `Deploy DEV to AWS` 실행의 Summary 또는 ECR에서 확인합니다. ECR은 최근 20개 immutable 이미지를 보존합니다.

## 로그와 알림

- CloudWatch Logs: `/ecs/openai-game-builders-seoul`, 보존 14일
- 서버 로그: `requestId`, method, path, status, duration, revision을 JSON 한 줄로 기록하며 토큰·쿠키·query는 기록하지 않습니다.
- CloudWatch Alarm: `openai-game-builders-seoul-http-5xx`가 5분 안에 5xx 1회 이상이면 SNS `openai-game-builders-seoul-operations`로 알립니다.
- AWS Budget: 월 $30, 예측 비용 80% 초과 시 같은 SNS topic으로 알립니다.

SNS topic에는 운영자 이메일 구독을 추가하고 수신 메일의 Confirm subscription을 눌러야 외부 이메일 알림이 완성됩니다. 구독이 없어도 Alarm/Budget 상태와 SNS publish는 AWS Console에서 확인할 수 있습니다.

## 데이터 복구

DynamoDB Console에서 테이블의 `Backups` → `Point-in-time recovery` → `Restore`로 장애 직전 시각의 새 테이블을 만듭니다. 복원은 원본을 덮어쓰지 않습니다. 복원 테이블을 검증한 뒤 GitHub repository variable `AWS_DYNAMODB_TABLE`을 새 이름으로 바꾸고 `DEV` 배포를 다시 실행합니다.

세션 tombstone과 만료 데이터는 TTL이 비동기로 정리합니다. TTL 삭제 지연 중에도 서버는 `expiresAtEpoch`를 검사하므로 만료·로그아웃 세션을 인증에 사용하지 않습니다.

## 캐시 정책

CloudFront는 managed `CachingDisabled` 정책으로 원본 응답을 전달하므로 배포 후 invalidation이 필요하지 않습니다. 포털 및 Unity `index.html`/version API는 `no-store`, 압축 Unity 산출물은 최대 1시간 캐시입니다. 새 빌드는 파일명과 배포 revision으로 식별합니다.

## 비용과 종료 기준

- 월 Budget 상한: $30
- 예측 비용이 $24(80%)를 넘거나 대회 운영이 끝나면 service stack 유지 여부를 즉시 검토합니다.
- 완전 종료 시 `openai-game-builders-seoul-service` stack을 삭제해 Fargate, ALB, CloudFront, VPC 비용을 중지합니다.
- 데이터 보존이 필요하면 bootstrap stack은 유지합니다. DynamoDB, ECR, Secrets Manager에는 retain 정책이 적용되어 있습니다.

## 브랜치 정책 결정

해커톤 중에는 팀의 직접 통합과 `DEV` 자동 배포가 현재 작업 방식이므로 `DEV`/`HYUNJIN` branch protection은 적용하지 않습니다. 제출 동결 시점에는 `DEV`에 required Actions check와 force-push 금지를 켜는 것으로 재검토합니다.

## Unity CI 활성화

GameCI Personal license 방식은 GitHub Actions secret `UNITY_LICENSE`, `UNITY_EMAIL`, `UNITY_PASSWORD`가 모두 필요합니다. 세 값을 repository secret에 등록한 뒤 repository variable `UNITY_CI_ENABLED=true`로 변경합니다. 다음 `DEV` push에서 Unity `6000.3.22f1` 빌드와 브리지 주입 검사를 통과해야 활성화 완료로 봅니다. 인증값은 코드·로그·문서에 남기지 않습니다.
