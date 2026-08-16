# Figma 손님 캐릭터 8명 Unity 추가 기록

## 결과

- 기존 캐릭터 원본 `JeongHyun.png`, `HaYoung.png`, `MiJu.png`는 변경하지 않는다.
- Figma에서 추출한 8명은 `Assets/Resources/Sprites/Customers/Figma8`에 별도 추가한다.
- 모든 파일은 `1536×1024` RGBA PNG이며, 한 파일에 표정 3개가 들어 있다.
- 표정 순서는 `기본 / 기쁨 / 실망`이다.

## 원본

- Figma 파일: <https://www.figma.com/design/Yrun8rClSF4bLDLSsDDjuQ/GONGSAMZ-%C2%B7-8-Customers-Character-Sheets>
- Figma 페이지: `8 Customers · Character Sheets` (`0:1`)
- 추출일: 2026-08-17

| 순번 | 캐릭터 | Figma 이미지 노드 | Unity 파일 | SHA-256 |
|---:|---|---|---|---|
| 1 | 정현 | `3:8` | `01_JeongHyun.png` | `B2616400366D868EE4C2ED843B53951B587343CA7DA210EBE0CB639DAFB72F76` |
| 2 | 하영 | `4:8` | `02_HaYoung.png` | `199EFB0C0A2983C83544E1293C179AC43DC9C5B4305BDDAE35B653C5E3E29463` |
| 3 | 미주 | `5:8` | `03_MiJu.png` | `9EFFAF4F6F532E8F774838916D78625A430BCF39C27928A2802338CC44CD82DF` |
| 4 | 선자 | `14:7` | `04_Sunja.png` | `219F361BAC4DD0B06E36631C544E1544FCB1A848C795D8039C568A67E7BAE374` |
| 5 | 건우 | `14:8` | `05_Geonwoo.png` | `4C5BD85072D4199C74BF9730A28F84AD2039E66A32920935D69C18E776A40AE9` |
| 6 | 태수 | `14:9` | `06_Taesu.png` | `A60E8BFC0F351290213D81120B92F7947D6C90BD35CD2FDB4B16986A82292243` |
| 7 | 나리 | `14:10` | `07_Nari.png` | `2CD0841B4D495F308F9A2F87C41B2D160AEB624E5CA69AD5FE6F30E7DAE7E994` |
| 8 | 준호 | `14:11` | `08_Junho.png` | `1A4981A23840B68747213F6A678203936EA04F2B4B87257C3F433A3C2731F695` |

## Unity 가져오기 규격

- Texture Type: `Sprite (2D and UI)`
- Sprite Mode: `Multiple`
- Pixels Per Unit: `100`
- Filter Mode: `Bilinear`
- Mip Maps: `Off`
- Alpha Is Transparency: `On`
- 칸 1: `(0, 0, 512, 1024)` → `Default`
- 칸 2: `(512, 0, 512, 1024)` → `Joy`
- 칸 3: `(1024, 0, 512, 1024)` → `Disappointed`

## 사용 경로

예: `Resources.LoadAll<Sprite>("Sprites/Customers/Figma8/04_Sunja")`

이번 작업은 이미지와 Unity 가져오기 설정만 추가한다. 실제 손님 생성 데이터, `CustomerType`, 프리팹 및 게임 장면 연결은 별도 구현 대상으로 남긴다.
