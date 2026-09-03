# Unity WebGL deployment package

현재 검증된 `BungeoppangTycoon/Builds/WebGL` 생성물을 추적합니다. 기본 DEV 배포는 이 패키지를 검증한 뒤 서버 이미지에 포함하므로 Unity 계정 로그인이 필요하지 않습니다.

`UNITY_CI_ENABLED=true`로 설정하면 GitHub Actions가 Unity 6.3 WebGL을 다시 빌드하고, 해당 실행에서 생성된 패키지를 대신 배포합니다. 로컬 빌드를 기본 패키지로 갱신할 때는 `platform-server`에서 `pnpm unity:stage`와 `pnpm unity:verify`를 실행한 뒤 `build-manifest.json`과 산출물을 함께 커밋합니다.
