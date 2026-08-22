# WebGL 웹 화면 정책

## 적용 범위

- Unity WebGL 템플릿: `GongsamzWeb`
- 기준 해상도: 1920 × 1080
- 대상 화면 비율: 16:9부터 2:1까지
- 작은 화면 정책: 1366 × 768보다 작아지면 가로 스크롤이나 잘라내기 없이 게임 전체를 비례 축소한다.

## 템플릿 동작

WebGL 템플릿은 iframe 안에서 사용할 수 있도록 HTML 문서 전체를 게임 영역으로 사용한다. 캔버스는 iframe의 너비와 높이를 그대로 채운다.

- 16:9 화면에서는 1920 × 1080 기준 UI가 보인다.
- 더 넓은 2:1 화면에서는 Unity가 늘어난 좌우 영역을 사용할 수 있다.
- 좁거나 낮은 화면에서는 캔버스와 UI가 함께 축소된다.
- Unity 기본 푸터는 만들지 않는다. 우측 하단의 전체 화면 버튼만 제공한다.
- 고해상도 모니터에서는 렌더링 배율을 최대 2배로 제한해 불필요한 성능 저하를 줄인다.

## 외부 웹페이지 연결 규칙

외부 페이지가 헤더를 갖는다면 헤더는 Unity 안이 아니라 iframe 바깥에 둔다. iframe에는 전체 화면 권한을 부여하고, 헤더 높이를 뺀 나머지 공간을 차지하게 한다.

```html
<header>게임 사이트 헤더</header>
<iframe
  class="game-frame"
  src="/game/index.html"
  title="붕어빵 타이쿤"
  allowfullscreen
></iframe>
```

```css
html,
body {
  height: 100%;
  margin: 0;
}

body {
  display: grid;
  grid-template-rows: auto minmax(0, 1fr);
}

.game-frame {
  width: 100%;
  height: 100%;
  border: 0;
}
```

`game-bridge.js`를 사용하는 배포 환경은 기존처럼 웹사이트 루트에 해당 파일을 제공해야 한다.
