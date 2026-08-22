---
date: 2026-08-21
member: 이혜연
task_id: settings-options-and-runtime-error-fix
category: UI-아트 | 디버깅 | QA
codex_role: 아트·UI 보조 | 오류 분석 | 코드 초안
status: 완료
related_commits: 없음
related_files: BungeoppangTycoon/Assets/Resources/Prefabs/UI/UI_SettingsOptions.prefab, BungeoppangTycoon/Assets/Scripts/UI/UI_Scene/UI_Game.cs, BungeoppangTycoon/Assets/Scripts/Story/CustomerStoryProgress.cs
---

# 가게 설정 UI 프리팹 제작 및 게임 시작 오류 수정

## 1. 작업 목표

- 게임의 수채화·종이 질감 톤에 맞는 `UI_SettingsOptions` 프리팹을 실제 Unity 화면에 구성한다.
- 설정 화면에 음량, 키보드 안내, 게임 플레이 초기화, 닫기 동작을 배치한다.
- 게임 시작 중 반복되던 UI 텍스트 참조 오류와 저장 서비스 초기화 오류를 해결한다.

## 2. Codex에 준 맥락과 요청

- 사용자는 Figma 설정 화면과 비슷한 구조를 Unity 프리팹으로 만들고, 목업 데이터가 적용된 화면을 캡처해 시각적으로 다시 확인·수정해 달라고 요청했다.
- 이후 Unity Console에서 `UI_Game.GetTMP(timeText)` 관련 경고와 `CustomerStoryProgress.SaveData`의 `NullReferenceException` 로그를 제공하고 수정을 승인했다.

## 3. Codex의 기여

- Editor 빌더를 작성해 설정 UI 프리팹을 재생성할 수 있게 구성했다.
- 게임의 기존 종이 배경, 청록색 테두리, 주황색 설정 아이콘, 갈색 닫기 아이콘을 사용해 다음 UI를 구성했다.
  - 전체 음량 슬라이더와 증감 버튼
  - 키보드 조작 안내 토글 및 `Space`, `1–8` 키 안내
  - 게임 플레이 초기화 영역과 초기화 버튼
  - 조작법 다시 보기와 닫기 버튼
- `UI_Game`에서 시간·돈 텍스트를 초기화 시점에 한 번 찾아 보관하고, 없는 텍스트는 건너뛰도록 수정했다.
- `CustomerStoryProgress`가 저장 서비스 인스턴스가 만들어지기 전에 데이터를 읽지 않도록 `SaveService.Data` 경로를 사용하게 수정했다.
- 비활성 UI 자식도 바인딩할 수 있도록 `UI_Base`의 배열 크기 및 자식 탐색 방식을 바로잡았다.

## 4. 사람이 직접 판단하거나 수정한 부분

- 사용자는 설정 화면이 게임 톤에 어울리고 사용자가 바로 이해할 수 있어야 한다는 방향을 확정했다.
- 사용자의 피드백에 따라 제목·닫기 버튼의 위치, 닫기 버튼 배경 이미지, 하단 버튼 위치를 조정했다.
- 게임 플레이 초기화 기능의 실제 데이터 정책과 버튼 연결은 기존 기획 및 구현 범위를 유지했다. 이번 작업에서는 프리팹과 기존 팝업 연결을 정리했다.

## 5. 결과 및 검증

- Unity Editor에서 `UI_SettingsOptions` 프리팹을 생성하고 1920×1080 미리보기 캡처를 확인했다.
- 캡처를 검토하며 제목이 종이 영역 안에 놓이도록 조정하고, 키보드 토글 상태와 하단 버튼이 패널 안에서 잘리지 않도록 수정했다.
- Unity Play Mode에서 인트로의 `Click To Start`를 눌러 게임 화면과 튜토리얼 표시까지 진행했다.
- 제공된 두 오류는 재현되지 않았다. 최종 확인 시 Console 오류 수는 0이었다.
- URP Compatibility Mode 안내 경고 1건은 남아 있으며, 이번 UI·저장 초기화 수정과는 관련이 없다.

## 6. 증거

- Codex 캡처: 없음
- 게임 결과: [설정 UI 1920×1080 미리보기](../evidence/game/2026-08-21-settings-options-1920x1080.png)
- 관련 커밋: 없음
- 관련 파일:
  - `C:/DevHub/02_GameDev/GONGSAMZ/BungeoppangTycoon/Assets/Resources/Prefabs/UI/UI_SettingsOptions.prefab`
  - `C:/DevHub/02_GameDev/GONGSAMZ/BungeoppangTycoon/Assets/Editor/SettingsOptionsFigmaPrefabBuilder.cs`
  - `C:/DevHub/02_GameDev/GONGSAMZ/BungeoppangTycoon/Assets/Scripts/UI/UI_SettingsPopups.cs`
  - `C:/DevHub/02_GameDev/GONGSAMZ/BungeoppangTycoon/Assets/Scripts/UI/UI_Scene/UI_Game.cs`
  - `C:/DevHub/02_GameDev/GONGSAMZ/BungeoppangTycoon/Assets/Scripts/Story/CustomerStoryProgress.cs`
  - `C:/DevHub/02_GameDev/GONGSAMZ/BungeoppangTycoon/Assets/Scripts/UI/UI_Base.cs`

## 7. 실패 또는 배운 점

- 처음에는 사용 중인 TMP 폰트에 없는 톱니바퀴 글리프를 텍스트로 넣어 경고가 발생했다. 해당 텍스트는 제거하고 기존 설정 아이콘 에셋을 사용했다.
- 프리뷰 캡처에서 카메라가 이미 해제된 RenderTexture를 참조하는 오류가 한 번 발생했다. RenderTexture 제거 전에 카메라 연결을 해제하도록 수정했다.
- 프리팹의 시각적 완성도는 코드 생성만으로 확정하지 않고, 실제 캡처를 보고 위치를 반복 조정해야 했다.

## 8. 기여 효과

- 설정 UI를 다시 만들 때 Editor 메뉴로 같은 구조를 재생성할 수 있어 반복 배치 작업을 줄인다.
- 게임 시작 시 반복적으로 콘솔을 채우던 두 오류를 제거해, 이후 실제 오류를 구분하기 쉬워졌다.
