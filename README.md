# 따끈따끈 붕어빵 🐟

<div align="center">
  <img src="BungeoppangTycoon/Assets/Resources/Sprites/UI/TitleTextImg.png" alt="따끈따끈 붕어빵 로고" width="360" />
  <br />
  <br />
  <b>손님 주문을 놓치지 않고, 따뜻한 붕어빵 가게를 5일간 운영하는 Unity 2D 경영 게임</b>
  <br />
  <sub>Bungeoppang Tycoon · Solo Project · Unity 6000.0.39f1 · C#</sub>
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

- **Engine:** Unity 6000.0.39f1
- **Language:** C#
- **Rendering:** Universal Render Pipeline (URP) 2D
- **UI:** UGUI, TextMeshPro

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
```

## ▶️ 실행 방법

1. Unity Hub에서 이 저장소의 `BungeoppangTycoon` 폴더를 프로젝트로 추가합니다.
2. **Unity 6000.0.39f1** 버전으로 프로젝트를 엽니다.
3. `Assets/Scenes/IntroScene.unity`를 열고 Play 버튼을 누릅니다.

---

<div align="center">
  추운 겨울밤, 가장 바쁜 붕어빵 가게의 사장이 되어 보세요. 🔥
</div>
