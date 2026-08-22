# HIVE 계정 식별·표시 가이드

## 결론

- 계정 데이터의 서버 저장 키는 HIVE가 인증한 불변 식별자를 사용한다.
- 이메일 주소는 계정 키로 사용하지 않는다.
- 웹 화면에는 원본 `idp_index:idp_user_id`를 그대로 노출하지 않고 `HIVE 계정 · 끝 6자리`로 표시한다.

## 현재 Web Login v2 동작

HIVE Web Login v2 검증 응답은 `idp_index`와 `idp_user_id`를 제공하지만 이메일 주소는 제공하지 않는다. 서버는 PlayerID가 있으면 PlayerID를, 없으면 `idp_index:idp_user_id`를 내부 `subject`로 사용한다. 이 값으로 저장, 인벤토리, 장비, 구매 내역을 계정별로 격리한다.

이메일을 임의로 입력받아 로그인 이메일처럼 표시하면 계정 소유를 검증할 수 없고, 이메일 변경 시 저장 데이터가 끊길 수 있다. 따라서 현재 웹 빌드에서는 이메일을 계정 식별자로 취급하지 않는다.

## 향후 확장

HIVE SDK 또는 별도 인증 API가 검증된 `playerName`이나 이메일을 제공하면 다음 규칙으로 확장한다.

1. 기존 `subject`와 DynamoDB 키는 변경하지 않는다.
2. 검증된 값은 별도의 `accountLabel` 표시 필드로만 사용한다.
3. 이메일을 표시할 경우 기본값은 마스킹하고, 원문 표시에는 명시적인 사용자 동의를 받는다.
4. HIVE 응답에서 얻지 못한 이메일을 코드나 환경변수에 계정별로 하드코딩하지 않는다.

관련 공식 문서: <https://developers.withhive.com/api/hive-server-api/web-login/verify-token-v2/>
