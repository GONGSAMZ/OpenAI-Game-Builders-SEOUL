# 따끈따끈 붕어빵 🐟

<div align="center">
  <img src="BungeoppangTycoon/Assets/Resources/Sprites/UI/TitleTextImg.png" alt="따끈따끈 붕어빵 로고" width="360" />
  <br />
  <br />
  <b>손님 주문을 놓치지 않고, 따뜻한 붕어빵 가게를 5일간 운영하는 Unity 2D 경영 게임</b>
  <br />
  <sub>Bungeoppang Tycoon · Solo Project · Unity 6.3 LTS (6000.3.22f1) · C#</sub>
</div>

<br />

## 👤 프로젝트 정보

| 항목 | 내용 |
| --- | --- |
| 개발 인원 | 1인 개발 |
| 개발 기간 | 2025.05.04 – 2025.06.12 |
| 수업명 | 2D프로그래밍 |
| 게임 로직 · UI | 직접 코딩 |
| 이미지 에셋 | ChatGPT를 활용해 직접 제작 |
| 실행 영상 | [YouTube에서 보기](https://youtu.be/cmlOsWBZXNY?si=I9tat9NlssRLAlLh) |

## 🎮 프로젝트 소개

**따끈따끈 붕어빵**은 겨울 저녁의 붕어빵 가게를 운영하는 2D 요리·타임 매니지먼트 게임입니다.
플레이어는 반죽과 속재료를 조합해 붕어빵을 만들고, 기다리는 손님의 주문에 맞춰 완성품을 전달합니다.

하루 장사가 끝나면 매출에서 재료비를 계산하고 다음 날을 준비합니다. 5일 차에 보유 금액이 **40,000원 초과**이면 클리어 엔딩을 볼 수 있습니다.

## ✨ 주요 기능

| 기능 | 설명 |
| --- | --- |
| 주문 시스템 | 손님마다 서로 다른 속재료와 수량을 주문합니다. |
| 단계형 조리 | 반죽 → 속재료 → 윗반죽 → 굽기 순서로 붕어빵을 완성합니다. |
| 굽기 판정 | 알맞게 구우면 더 좋은 결과를 얻고, 너무 오래 두면 과하게 구워집니다. |
| 드래그 앤 드롭 | 완성된 붕어빵을 진열대에 놓거나 손님에게 직접 전달합니다. |
| 대기 시간 | 손님은 오래 기다리면 화가 나서 떠나므로 주문 처리 순서가 중요합니다. |
| 일일 정산 | 매출, 재료비, 판매 수량을 계산하며 가게를 운영합니다. |
| 멀티 엔딩 | 파산, 일반, 클리어의 세 가지 엔딩을 제공합니다. |

## 🕹️ 플레이 방법

1. 시작 화면을 클릭해 게임을 시작합니다.
2. **주전자**를 선택한 뒤 붕어빵 틀을 클릭해 반죽을 올립니다.
3. 원하는 **속재료**를 선택하고 반죽을 클릭해 속을 채웁니다.
4. 다시 주전자를 선택해 윗반죽을 올리고, 시간에 맞춰 구워 완성합니다.
5. 완성된 붕어빵을 손님에게 드래그해 전달합니다.
6. 오후 6시부터 11시까지 장사를 마친 뒤 일일 정산을 확인하고 다음 날로 넘어갑니다.

## 🖼️ 엔딩 장면

<div align="center">
  <img src="BungeoppangTycoon/Assets/Resources/Sprites/Ending/ClearEndingScene.png" alt="클리어 엔딩의 붕어빵 가게" width="720" />
</div>

## 🛠️ 기술 스택

- **Engine:** Unity 6.3 LTS (6000.3.22f1)
- **Language:** C#
- **Rendering:** Universal Render Pipeline (URP) 2D
- **UI:** UGUI, TextMeshPro

## 🌐 웹 플랫폼 연동

Unity 게임과 독립된 `platform-server`에서 HIVE Web Login, 장인 상점, OpenAI API를 처리합니다. 서버가 같은 도메인의 `/game/`에서 WebGL 빌드를 제공하며, 루트 화면은 상단 헤더와 게임만 표시합니다. HIVE·상점·OpenAI 기능은 서버 API와 Unity 브리지에 유지하고 게임 UI에서 호출할 수 있습니다. 외부 비밀키는 Unity나 브라우저에 포함하지 않고 서버 환경변수로만 관리합니다.

| 구성 | 위치 | 역할 |
| --- | --- | --- |
| 플랫폼 서버 | `platform-server/` | HIVE 로그인, 게임 세션, 마켓, OpenAI 프록시 |
| 브라우저 브리지 | `platform-server/public/game-bridge.js` | 로그인 팝업과 WebGL 통신 |
| Unity WebGL 플러그인 | `BungeoppangTycoon/Assets/Plugins/WebGL/` | JavaScript와 Unity `SendMessage` 연결 |
| Unity API 클라이언트 | `BungeoppangTycoon/Assets/Scripts/Platform/` | 게임 세션과 서버 API 호출 |
| AWS 자동 배포 | `infra/aws/`, `.github/workflows/deploy-dev-to-aws.yml` | DEV → WebGL 빌드 → ECS/CloudFront 배포 |
| HIVE 상품 운영 스킬 | `skills/hive-store-catalog/` | 콘솔 상품 자동 동기화, PID 지급 규칙, 이미지 운영 |

서버는 기본적으로 Hive와 OpenAI를 `mock` 모드로 실행하므로 외부 키 없이 전체 연결 흐름을 확인할 수 있습니다.

```powershell
Set-Location platform-server
Copy-Item .env.example .env
pnpm install
pnpm dev
```

브라우저에서 `http://localhost:3000`을 열면 상단 헤더 아래에서 WebGL 게임이 실행됩니다. HIVE mock 가입·로그인, 상점 mock 구매, AI mock 응답은 Unity 브리지 또는 API로 검증합니다. 상세 설정은 [`platform-server/README.md`](platform-server/README.md), AWS 구성은 [`docs/05_WEB_PLATFORM_AWS_DEPLOYMENT.md`](docs/05_WEB_PLATFORM_AWS_DEPLOYMENT.md)를 참고하세요.

## 📁 프로젝트 구조

```text
BungeoppangTycoon/
├─ Assets/
│  ├─ Scenes/          # 시작 및 게임 씬
│  ├─ Scripts/
│  │  ├─ Controllers/  # 붕어빵, 손님, 틀, 도구 동작
│  │  ├─ Managers/     # 게임 상태, UI, 리소스 관리
│  │  └─ UI/           # 주문, 일일 정산, 엔딩 화면
│  └─ Resources/       # 스프라이트, 사운드, 프리팹, 데이터
├─ Packages/
└─ ProjectSettings/

platform-server/
├─ src/                 # Hive/OpenAI 서버 코드
├─ public/              # 연동 검증 화면과 브라우저 브리지
├─ tests/               # 서버 통합 테스트
└─ docs/                # API와 Console 설정 문서
```

## ▶️ 실행 방법

1. Unity Hub에서 이 저장소의 `BungeoppangTycoon` 폴더를 프로젝트로 추가합니다.
2. **Unity 6.3 LTS (6000.3.22f1)** 버전으로 프로젝트를 엽니다.
3. `Assets/Scenes/IntroScene.unity`를 열고 Play 버튼을 누릅니다.

---

<div align="center">
  추운 겨울밤, 가장 바쁜 붕어빵 가게의 사장이 되어 보세요. 🔥
</div>
