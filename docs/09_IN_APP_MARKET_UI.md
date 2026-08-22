# 게임 내 인앱 마켓 UI 구현

## 목표

게임 HUD의 `장인 마켓` 버튼을 누르면 서버 상품 목록을 카드로 보여 주고, 로그인 상태와 보유 수량에 따라 구매 동작을 바꾼다. 화면이 열린 동안 영업 시간은 멈추고 닫으면 이전 상태로 돌아간다.

## 파일별 책임

| 파일 | 책임 | 함께 동작하는 대상 |
| --- | --- | --- |
| `UI_InAppMarket.cs` | 상품/구매 내역 탭, 서버 요청, 로그인, 구매, 페이지네이션, 오류와 닫기 흐름을 관리한다. | `GamePlatformClient`, `UIManager`, 상품 카드 |
| `UI_InAppMarketProductCard.cs` | 상품 하나의 이름·설명·가격·보유 수량·버튼 상태를 표시한다. | `UI_InAppMarket` |
| `InAppMarketData.cs` | 서버 JSON 응답을 Unity가 읽을 수 있는 데이터 형식으로 정의한다. | `JsonUtility` |
| `UI_SafeArea.cs` | 화면 가장자리에서 중요한 버튼이 잘리지 않게 안전 영역을 적용한다. | `Screen.safeArea` |
| `InAppMarketPrefabBuilder.cs` | 팝업 프리팹과 HUD 진입 버튼을 동일한 구조로 다시 생성한다. | Unity Editor, `UI_Game.prefab` |

## 구현 계약

### `UI_InAppMarket`

| 구성원 | 입력·출력 | 동작 및 실패 처리 |
| --- | --- | --- |
| `LoadInitialData()` | 서버 공개 설정·상품·보유품 | 상품 실패 시 빈 화면과 다시 시도를 표시한다. |
| `LoginOrRefresh()` | 로그인 버튼 | 비로그인 상태에서는 HIVE 로그인, 로그인 상태에서는 보유품 새로고침을 수행한다. |
| `Purchase(product)` | 선택 상품 | mock이면 게임 안 구매, HIVE 웹 상점이면 외부 상점을 연다. |
| `RenderProducts()` | 서버 상품 배열·보유품·장착 상태 | 상품마다 ID가 같은 이미지를 표시하고 황금 틀 보유 시 장착·해제 버튼을 제공한다. |
| `LoadPurchaseHistoryPage()` | 로그인 계정·opaque cursor | 최신 구매 상태를 추가 표시하고 `nextCursor`가 있을 때만 더 보기 버튼을 연다. |
| `Close()` | 닫기·Escape | 팝업을 닫고 이전 게임 진행 상태를 복원한다. |

### `UI_InAppMarketProductCard`

| 구성원 | 입력·출력 | 동작 및 실패 처리 |
| --- | --- | --- |
| `SetData(...)` | 상품·보유 수량·로그인·상점 모드 | 표시 문구와 구매 가능 여부를 갱신한다. |
| `RefreshOwned(quantity)` | 보유 수량 | 0이면 `미보유`, 그 외에는 보유 개수를 표시한다. |
| `SetBusy(isBusy)` | 요청 처리 여부 | 중복 클릭을 막고 처리 중 문구를 표시한다. |

## 화면 흐름

```text
UI_Game의 장인 마켓 버튼
→ UI_InAppMarket 팝업
→ GET /api/v1/config/public
→ GET /api/v1/store/catalog
→ 로그인했다면 GET /api/v1/store/me
→ 구매 내역 탭: GET /api/v1/store/purchases?limit=20&cursor=...
→ mock: POST /api/v1/store/mock-purchases
→ hive-web-shop: GameBridge_OpenShop으로 HIVE 웹 상점 열기
→ 황금 틀: PUT /api/v1/store/equipment/mold
```

## Unity 연결

- 생성 프리팹: `Assets/Resources/Prefabs/UI/UI_InAppMarket.prefab`
- HUD 버튼: `Assets/Resources/Prefabs/UI/UI_Game.prefab`의 `inAppMarketButton`
- `UI_Game.cs`가 버튼을 누를 때 `Managers.UI.ShowUI<UI_InAppMarket>(false)`를 호출한다.
- HUD와 하루 종료 상점은 일반 돈과 별도로 서버의 `red-bean-coin` 잔액을 표시한다.
- `Sprites/StoreProducts/<상품 ID>` 이미지를 사용하며 누락 시 기본 코인 이미지를 표시한다.
- 황금 틀 장착 상태는 서버 계정에 저장되고, 모든 조리 틀에 금색 틴트·광택을 적용하며 새로 굽는 붕어빵의 성공/탄 판정을 `4.8초/12초`로 고정한다.
- 팝업은 `1920×1080`, `Scale With Screen Size`, 가로·세로 혼합 배율 `0.3`을 사용한다.
- 프리팹을 다시 만들려면 Unity 메뉴에서 `Tools > GONGSAMZ > Rebuild In-App Market UI`를 실행한다.
- 구매 내역은 `결제 대기/완료/실패/취소/시간 만료` 한글 상태, 비로그인 안내, 빈 목록, 재시도와 더 보기를 제공한다. 계정이 바뀌면 진행 중 요청을 무효화하고 이전 행을 즉시 지운다.
- 웹 헤더와 Unity iframe은 각자의 세션도 함께 폐기한다. Unity WebGL 콜백은 런타임에 실제 수신기를 등록하고, 늦게 도착한 이전 계정 요청은 세션 세대 번호로 무시한다.

## 검증 결과

- Unity 6000.3.22f1 C# 컴파일 통과
- 프리팹 필수 컴포넌트와 HUD 버튼 자동 검사 통과
- `1280×720`, `1920×1080`, `2560×1080` 렌더 검수 통과: `resources/ui-qa/`
- 플랫폼 서버·브리지: 10개 테스트 파일, 53개 테스트 통과
- Unity 6000.3.22f1 WebGL 빌드와 소스/산출물 SHA-256 매니페스트 검증 통과
- 삭제됐던 `UI_Ending.prefab` 복원 및 에디터 필수 프리팹 검사 통과

## 현재 제한

- Unity Editor에서는 WebGL JavaScript 브리지가 없어 HIVE 팝업 로그인을 완료할 수 없다. 로그인 구매 흐름은 WebGL 빌드와 mock 서버에서 확인해야 한다.
- 팥 코인은 보유 수량과 구매 반영만 제공하며 소비 API와 게임 내 사용처는 아직 없다.
- 실제 HIVE 결제는 게임 안에서 직접 처리하지 않고 HIVE 웹 상점을 새 창으로 연다. 결제 후 즉시 조회와 5초 폴링으로 서버 지급 결과를 다시 읽는다.
