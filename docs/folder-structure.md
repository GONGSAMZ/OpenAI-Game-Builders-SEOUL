# GONGSAMZ 웹 게임 폴더 구조 안내

이 문서는 파일을 어디에 넣어야 하는지 정하는 약속입니다. 화면을 만드는 코드, 게임 규칙, 이미지와 소리를 분리하면 기능을 추가하거나 고칠 때 다른 부분이 덜 영향을 받습니다.

```text
GONGSAMZ/
├─ src/
│  ├─ client/                 # 브라우저에서 실행되는 화면과 조작
│  │  ├─ game/
│  │  │  ├─ scenes/           # 시작·인게임·결과 같은 화면 단위
│  │  │  ├─ input/            # 키보드, 마우스, 터치 입력
│  │  │  ├─ ui/               # 버튼, 메뉴, 점수판 등 화면 요소
│  │  │  ├─ audio/            # BGM과 효과음 재생
│  │  │  └─ rendering/        # 캐릭터와 맵을 화면에 그리는 코드
│  │  └─ network/             # 서버와 실시간으로 연결하는 코드
│  │
│  ├─ core/                   # 게임의 공통 규칙과 데이터
│  │  ├─ entities/            # 플레이어, 적, 아이템 같은 게임 객체
│  │  ├─ rules/               # 이동, 공격, 점수, 승리 조건
│  │  ├─ state/               # 현재 게임 진행 상태
│  │  ├─ commands/            # 이동·공격 등 플레이어 행동 요청
│  │  └─ config/              # 체력, 속도, 난이도 등의 설정값
│  │
│  └─ server/                 # 온라인 기능이 필요할 때 사용하는 서버 코드
│     ├─ api/                 # 로그인, 랭킹, 저장 요청 처리
│     ├─ rooms/               # 게임 방 생성과 참가자 관리
│     ├─ websocket/           # 실시간 게임 정보 주고받기
│     └─ persistence/         # 계정·기록을 저장하고 불러오기
│
├─ resources/                 # 코드가 아닌 게임 재료
│  ├─ images/                 # 캐릭터, 배경, 아이콘 이미지
│  ├─ sounds/                 # BGM과 효과음 파일
│  ├─ maps/                   # 맵과 스테이지 데이터
│  ├─ fonts/                  # 게임에서 쓸 글꼴
│  └─ locales/                # 한국어, 영어 등 언어별 문구
│
├─ tests/                     # 게임 규칙이 제대로 작동하는지 검사
│  └─ core/
├─ docs/                      # 기획과 개발 안내 문서
├─ tools/                     # 맵 생성, 이미지 변환 같은 보조 도구
├─ platform-server/           # Hive 로그인과 OpenAI API를 처리하는 독립 서버
└─ README.md                  # 프로젝트 소개와 실행 방법
```

## 폴더 사용 규칙

1. **게임 규칙은 `src/core`에 둡니다.**
   - 예: 공격하면 체력이 줄어드는 계산, 아이템을 획득하는 규칙
   - 화면을 그리거나 서버에 연결하는 코드는 넣지 않습니다.

2. **화면과 조작은 `src/client`에 둡니다.**
   - 예: 버튼을 눌렀을 때 메뉴를 열기, 플레이어를 화면에 표시하기

3. **이미지·소리·맵 파일은 `resources`에 둡니다.**
   - 코드 폴더에 에셋 파일을 섞어 넣지 않습니다.

4. **온라인 기능이 생길 때만 `src/server`를 사용합니다.**
   - 싱글플레이 게임을 먼저 만든다면 이 폴더는 비워 두어도 됩니다.

5. **게임 규칙을 바꾸면 `tests/core`에도 검사용 파일을 추가합니다.**
   - 예: 공격력 계산을 수정했다면 결과가 올바른지 확인하는 테스트를 만듭니다.

6. **외부 플랫폼 연동은 `platform-server`에 둡니다.**
   - Hive Client Secret과 OpenAI API Key는 서버 환경변수에서만 사용합니다.
   - Unity WebGL은 HTTP 계약과 브라우저 브리지를 통해 서버에 연결합니다.

## 처음 만들 파일 추천

- `src/client/main.ts`: 브라우저에서 게임을 시작하는 파일
- `src/client/game/scenes/TitleScene.ts`: 시작 화면
- `src/client/game/scenes/GameScene.ts`: 실제 플레이 화면
- `src/core/state/GameState.ts`: 현재 게임 상태를 담는 파일
- `src/core/rules/updateGame.ts`: 한 번의 게임 진행을 계산하는 파일
- `resources/maps/tutorial.json`: 첫 연습 스테이지 데이터
