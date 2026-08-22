# GitHub 이슈 작업 문서

이 폴더는 GitHub 이슈를 실제 구현 가능한 작업 단위로 풀어 쓴 문서를 보관한다. 완료 후 작성하는 `docs/codex/` 작업 기록과 달리, 여기의 문서는 **작업 전 범위 결정, 구현 순서, 검증 기준**을 정의하는 데 사용한다.

## 문서 목록

| 이슈 | 작업 문서 | 상태 |
|---|---|---|
| [#1 기존 핵심 오류 수정 및 기본 플레이 안정화](https://github.com/GONGSAMZ/OpenAI-Game-Builders-SEOUL/issues/1) | [issue-001-core-gameplay-stabilization/](./issue-001-core-gameplay-stabilization/) | 작업 준비 |

## 작성 규칙

1. 이슈마다 `issue-NNN-짧은-영문-이름/` 폴더를 만든다.
2. 폴더의 `README.md`에는 전체 범위, 우선순위, 구현 순서와 완료 기준을 적는다.
3. 서로 독립적으로 재현·수정·검증할 수 있는 문제는 `problems/` 아래의 별도 문서로 나눈다.
4. 아직 실행으로 확인하지 못한 내용은 “확정 버그”라고 부르지 않고 상태를 명시한다.
5. 구현 뒤에는 실제 재현 결과, 변경 파일, 테스트 결과와 PR 링크를 갱신한다.
