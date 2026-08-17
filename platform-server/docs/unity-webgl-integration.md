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
6. 로그인 성공 후 서버 인벤토리를 5초 간격으로 조회하고, 웹 패널이나 HIVE 결제로 바뀐 `red-bean-coin` 잔액을 `Managers.Game.Money`에 차이만큼 반영합니다.
7. 부모 웹 페이지가 테스트 지급 직후 `PLATFORM_INVENTORY` 메시지를 보내므로 폴링을 기다리지 않고 실행 중인 Unity에도 즉시 반영됩니다.

서버와 WebGL을 동일한 Origin에서 제공하는 구성이 쿠키·CORS·팝업 연동을 가장 단순하게 만듭니다. 별도 도메인으로 배포할 경우 `.env`의 `GAME_ORIGIN`을 정확한 WebGL Origin으로 설정해야 합니다.

HIVE Unity SDK의 현재 배포 대상에는 WebGL이 없으므로 이 프로젝트의 WebGL 빌드는 HIVE Web Login·Web Shop과 HIVE Server API를 브라우저/서버 경계에서 사용합니다. Android·iOS·Windows 네이티브 빌드로 확장할 때는 같은 `GamePlatformClient` 인터페이스 뒤에 HIVE Unity SDK 구현을 추가합니다.
