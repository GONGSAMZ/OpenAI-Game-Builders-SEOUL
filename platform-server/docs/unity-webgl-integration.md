# Unity WebGL 연결 방법

웹 연동 어댑터는 Unity 프로젝트의 다음 경로에 적용되어 있습니다.

- `BungeoppangTycoon/Assets/Plugins/WebGL/GameBridge.jslib`
- `BungeoppangTycoon/Assets/Scripts/Platform/GamePlatformClient.cs`

1. `GameBridge.jslib`은 브라우저의 Hive 로그인 팝업과 Unity `SendMessage`를 연결합니다.
2. `GamePlatformClient.cs`는 발급된 게임 세션을 보관하고 서버 API를 호출합니다.
3. Unity WebGL 템플릿의 게임 로더보다 먼저 다음 스크립트를 추가합니다.

```html
<script src="https://YOUR-SERVER/game-bridge.js"></script>
```

4. `GamePlatformClient`는 WebGL 시작 시 `@GamePlatformClient` GameObject로 자동 생성되어 씬을 이동해도 유지됩니다.
5. 로그인 버튼에서 `LoginWithHive()`를, 인게임 상점의 충전 버튼에서 `OpenHiveWebShop()`을 호출합니다.
6. 로그인 성공 후 서버 인벤토리·장착 상태·테스트 포인트를 즉시 조회하고 5초 간격으로 다시 동기화합니다.
7. `red-bean-coin`은 HUD와 하루 종료 상점에 별도 표시하며 `Managers.Game.Money`에는 더하지 않습니다.
8. 부모 웹 페이지가 테스트 포인트 충전 또는 결제 직후 `PLATFORM_INVENTORY` 메시지를 보내므로 폴링을 기다리지 않고 실행 중인 Unity에도 즉시 반영됩니다.
9. 황금 틀은 계정별 장착·해제가 가능하며, 장착 시 모든 조리 틀의 외형과 새로 시작하는 붕어빵의 굽기 시간 배율(0.8)을 바꿉니다.

## 검증된 WebGL 패키지 갱신

Unity 6000.3.22f1에서 `WebGLBuildCommand.Build`를 실행한 뒤 서버 디렉터리에서 아래 명령을 사용합니다.

```bash
pnpm unity:stage
pnpm unity:verify
```

`unity:stage`는 `BungeoppangTycoon/Builds/WebGL`을 `platform-server/game-dist`로 옮기고 `build-manifest.json`에 Unity 버전, Unity 소스 트리 SHA-256, 각 WebGL 파일의 크기·SHA-256을 기록합니다. `unity:verify`는 소스나 산출물이 매니페스트와 다르면 실패하며 DEV AWS 배포에서도 같은 검사를 실행합니다.

서버와 WebGL을 동일한 Origin에서 제공하는 구성이 쿠키·CORS·팝업 연동을 가장 단순하게 만듭니다. 별도 도메인으로 배포할 경우 `.env`의 `GAME_ORIGIN`을 정확한 WebGL Origin으로 설정해야 합니다.

HIVE Unity SDK의 현재 배포 대상에는 WebGL이 없으므로 이 프로젝트의 WebGL 빌드는 HIVE Web Login·Web Shop과 HIVE Server API를 브라우저/서버 경계에서 사용합니다. Android·iOS·Windows 네이티브 빌드로 확장할 때는 같은 `GamePlatformClient` 인터페이스 뒤에 HIVE Unity SDK 구현을 추가합니다.
