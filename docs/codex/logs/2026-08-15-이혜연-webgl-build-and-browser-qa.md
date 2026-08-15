---
date: 2026-08-15
member: 이혜연
task_id: webgl-build-and-browser-qa
category: 구현
codex_role: 오류 분석 | 테스트 설계
status: 완료
related_commits: DEV 브랜치의 WebGL 작업 커밋
related_files: C:\DevHub\02_GameDev\GONGSAMZ\BungeoppangTycoon\ProjectSettings\ProjectSettings.asset; C:\DevHub\02_GameDev\GONGSAMZ\BungeoppangTycoon\Assets\Editor\WebGLBuildCommand.cs; C:\DevHub\02_GameDev\GONGSAMZ\BungeoppangTycoon\Assets\Scripts\
---

# 붕어빵 타이쿤 WebGL 빌드 및 브라우저 검증

## 1. 작업 목표

Unity 게임을 브라우저에서 실행할 수 있도록 WebGL 설정을 적용하고, 실제 빌드와 로컬 브라우저 실행까지 검증한다.

## 2. Codex에 준 맥락과 요청

- 실제 Unity 프로젝트 경로: `C:\DevHub\02_GameDev\GONGSAMZ\BungeoppangTycoon`
- WebGL 설정 적용, 빌드 생성, 오류 분석·수정, 브라우저 확인, 작업 로그 작성을 요청받았다.
- 프로젝트 요구 Unity 버전은 `6000.0.39f1`이었으나, 로컬에는 WebGL 모듈이 포함된 `6000.3.21f1`이 설치되어 있었다.

## 3. Codex의 기여

- 프로젝트 설정과 `Packages`를 `C:\DevHub\02_GameDev\GONGSAMZ\_codex-backups\webgl-prebuild-20260815-01`에 백업했다.
- WebGL 설정에서 파일 해시 이름, Gzip 압축, 브라우저 해제 대체 기능, 초기 메모리 128MB를 적용했다.
- `Assets/Editor/WebGLBuildCommand.cs`를 추가해 WebGL 대상 전환과 빌드를 재현 가능하게 만들었다.
- Unity 6.3 계열에서 URP 호환 모드가 막히는 빌드 오류를 분석하고, `URP_COMPATIBILITY_MODE` 기호와 WebGL 대상 재컴파일로 해결했다.
- 브라우저 콘솔의 `U+FFFD` 글꼴 경고를 분석했다. 원인은 21개 C# 파일이 CP949 인코딩으로 저장되어 WebGL 컴파일 시 한글 문자열이 깨진 것이었고, 원본 `Assets/Scripts` 백업 후 UTF-8로 변환했다.

## 4. 사람이 직접 판단하거나 수정한 부분

- 사용자가 WebGL 설정, 빌드, 오류 수정, 브라우저 검증, 기록 작성을 요청해 작업 범위를 승인했다.
- 별도의 사람이 직접 수정한 게임 규칙·장면·UI 배치 변경은 없다.

## 5. 결과 및 검증

- Unity `6000.3.21f1`과 WebGL 모듈로 WebGL 빌드가 성공했다.
- 최종 빌드 출력: `C:\DevHub\02_GameDev\GONGSAMZ\BungeoppangTycoon\Builds\WebGL`
- Unity 빌드 로그의 결과: `Build Successful`, 빌드 크기 `51,107,811` bytes.
- `http://127.0.0.1:8080/` 로컬 웹 서버에서 로딩 막대가 사라지고 Unity 캔버스가 표시되는 것을 확인했다.
- 시작 화면과 게임 장면 전환을 확인했다. 수정 후 게임 화면의 금액은 `0 원`으로 표시됐고, 브라우저 콘솔 오류·경고는 0건이었다.

## 6. 증거

- Codex 캡처: 없음
- 게임 결과: [수정 후 WebGL 게임 화면](../evidence/game/2026-08-15-webgl-browser-game-scene.png)
- 관련 커밋: DEV 브랜치의 WebGL 작업 커밋
- 관련 파일:
  - `C:\DevHub\02_GameDev\GONGSAMZ\BungeoppangTycoon\ProjectSettings\ProjectSettings.asset`
  - `C:\DevHub\02_GameDev\GONGSAMZ\BungeoppangTycoon\Assets\Editor\WebGLBuildCommand.cs`
  - `C:\DevHub\02_GameDev\GONGSAMZ\BungeoppangTycoon\Assets\Scripts\`

## 7. 실패 또는 배운 점

- 처음에는 설치된 Unity가 프로젝트 요구 버전보다 새 버전이라 URP 호환 모드 오류가 발생했다. WebGL 대상으로 먼저 전환해 URP 편집기 코드를 다시 컴파일한 뒤 해결했다.
- 일부 C# 파일이 UTF-8이 아니면 Unity WebGL에서 문자열이 대체 문자로 컴파일될 수 있다. 한국어 문자열이 있는 Unity 스크립트는 UTF-8로 통일한다.

## 8. 기여 효과

- 게임을 로컬 브라우저에서 실행 가능한 WebGL 결과물로 만들었다.
- 빌드 오류와 글자 깨짐을 실제 빌드·브라우저 검증으로 확인하고 수정해, 이후 웹 배포 전 점검 범위를 줄이는 데 도움을 준다.
