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

4. 빈 GameObject에 `GamePlatformClient`를 붙이고, 서버 주소를 HTTPS 배포 주소로 설정합니다.
5. 로그인 버튼에서 `LoginWithHive()`를 호출합니다.
6. `LoginSucceeded` 이벤트 이후 AI나 저장 API를 호출합니다.

서버와 WebGL을 동일한 Origin에서 제공하는 구성이 쿠키·CORS·팝업 연동을 가장 단순하게 만듭니다. 별도 도메인으로 배포할 경우 `.env`의 `GAME_ORIGIN`을 정확한 WebGL Origin으로 설정해야 합니다.

현재 어댑터는 어느 Scene에도 자동으로 연결하지 않아 기존 플레이에는 영향을 주지 않습니다. 실제 로그인 UI를 붙일 때 빈 GameObject에 `GamePlatformClient`를 추가하고 게임의 씬 관리 방식에 맞춰 호출합니다.
