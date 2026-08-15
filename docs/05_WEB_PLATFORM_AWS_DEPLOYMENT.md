# WebGL 플랫폼과 AWS 자동 배포

## 구현 범위

- Unity 6.3 LTS WebGL 빌드를 서버의 `/game/`에 제공
- 메인 화면에는 64px 상단 헤더와 게임 iframe만 표시
- HIVE 가입·로그인, 장인 상점, OpenAI 기능은 서버 API와 Unity 브리지로 제공
- HIVE 통합 Web Login과 HIVE 관리형 웹 상점 연결
- mock 구매의 중복 지급 방지와 AWS DynamoDB 영속화
- `DEV` 푸시 시 Unity 빌드 → Docker → ECR → ECS Fargate → CloudFront HTTPS 자동 배포

HIVE는 웹 로그인과 `shop.withhive.com/{keyword}` 웹 상점을 제공하지만 Unity WebGL/Node 서버의 범용 호스팅은 제공하지 않습니다. Crossplay Launcher 파일 서버는 Windows 실행 파일 배포용이므로 이 대회의 WebGL 제출 경로로 사용하지 않습니다.

## 배포 구조

```text
GitHub DEV push
  -> GameCI Unity 6000.3.22f1 WebGL build
  -> platform-server Docker image
  -> Amazon ECR
  -> ECS Fargate (Express + Unity static files)
  -> ALB
  -> CloudFront HTTPS public URL
       -> HIVE Web Login / Web Shop
       -> DynamoDB inventory
```

## 최초 AWS 부트스트랩

AWS 계정에서 한 번만 `infra/aws/bootstrap.yml`을 배포합니다. 이 스택은 GitHub OIDC 역할, ECR, DynamoDB, ECS 역할, Secrets Manager 비밀 저장소를 만듭니다. 장기 AWS Access Key를 GitHub에 저장하지 않습니다.

```bash
aws cloudformation deploy \
  --stack-name openai-game-builders-seoul-bootstrap \
  --template-file infra/aws/bootstrap.yml \
  --capabilities CAPABILITY_NAMED_IAM \
  --parameter-overrides DeployBranch=DEV
```

계정에 `token.actions.githubusercontent.com` OIDC Provider가 이미 있으면 `CreateGitHubOidcProvider=false`를 추가합니다.

스택 Outputs를 GitHub 저장소 Actions variables에 다음 이름으로 등록합니다.

| GitHub variable | Bootstrap output |
| --- | --- |
| `AWS_REGION` | 스택을 만든 리전, 권장 `ap-northeast-2` |
| `AWS_ROLE_ARN` | `AwsRoleArn` |
| `AWS_ECR_REPOSITORY` | `EcrRepository` |
| `AWS_DYNAMODB_TABLE` | `DynamoDbTable` |
| `AWS_RUNTIME_SECRET_ARN` | `RuntimeSecretArn` |
| `AWS_TASK_EXECUTION_ROLE_ARN` | `TaskExecutionRoleArn` |
| `AWS_TASK_ROLE_ARN` | `TaskRoleArn` |

Unity GameCI용 `UNITY_LICENSE`, `UNITY_EMAIL`, `UNITY_PASSWORD`는 GitHub Actions secrets로 등록합니다. 값이 없으면 DEV 워크플로는 실패하지 않고 배포 단계를 건너뜁니다.

## HIVE와 OpenAI 실연동 전환

초기 AWS 배포는 모두 mock으로 실행합니다. CloudFormation service stack Output의 `HiveRedirectUri`를 HIVE Console에 먼저 등록한 뒤 Secrets Manager `/openai-game-builders-seoul/runtime` JSON에 다음 키를 저장합니다.

- `HIVE_APP_ID`
- `HIVE_CLIENT_ID`
- `HIVE_CLIENT_SECRET`
- `OPENAI_API_KEY`

그 후 GitHub variables를 설정합니다.

| Variable | Sandbox 예시 |
| --- | --- |
| `HIVE_MODE` | `sandbox` |
| `STORE_MODE` | `hive-web-shop` |
| `HIVE_WEB_SHOP_URL` | HIVE가 발급한 `https://shop.withhive.com/...` |
| `OPENAI_MODE` | `live` 또는 `mock` |
| `USE_RUNTIME_SECRETS` | `true` |

실결제 상품은 HIVE Console의 Billing/Web Shop에서 PG사, 가격표, Product ID/Market PID, 아이템 지급을 설정하고 Sandbox 구매로 검증한 뒤 Live로 전환합니다.

## 운영 메모

- CloudFront 기본 도메인으로 HTTPS를 제공하므로 별도 도메인 없이 HIVE Redirect URI를 등록할 수 있습니다.
- ALB, Fargate, CloudFront, DynamoDB에는 AWS 요금이 발생할 수 있습니다.
- 세션은 프로토타입 범위에서 메모리 저장이므로 배포 후 다시 로그인할 수 있습니다. 구매 아이템은 DynamoDB에 유지됩니다.
- 인프라 변경 전 `infra/aws/service.yml`의 비용과 IAM 범위를 팀에서 검토합니다.
