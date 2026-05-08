# Active Reported Issues

사용자가 "아직 안 된다"고 재보고한 항목은 구현만 다시 보지 않고,
먼저 검증 방식 자체가 실패를 잡도록 강화한 뒤 수정한다.

## Current Checklist

- [ ] Data center select: 그룹 선택 후 확인/취소 단계가 `==`로 보이는 문제
  - 검증 보강: `Lobby` 808~811을 EN/JA 및 모든 글로벌 언어 슬롯에서 확인한다.
  - 수정 방향: 시작 화면 데이터센터 전용 row는 베이스 글로벌 문구를 유지한다.

- [ ] Data center select: 영어 그룹명 및 `Information` 계열 문자 간격 겹침
  - 검증 보강: 실제 시작 화면 ULD가 참조하는 FDT의 ASCII metrics를 clean global과 비교한다.
  - 수정 방향: 시작 화면 ASCII glyph/kerning은 clean global과 일치시킨다.

- [ ] System settings: FHD 150% / QHD 200% / UHD 300%에서 글자 깨짐 및 간격 겹침
  - 검증 보강: `시스템 설정 150%`, `시스템 설정 200%`, `시스템 설정 300%`,
    `FHD 150% QHD 200% UHD 300%` 문장 단위 레이아웃 검사를 수행한다.
  - 수정 방향: 한글 glyph의 음수 next offset은 일반 규칙으로 정규화하고,
    ASCII 복구는 기존 atlas cell을 오염시키지 않도록 빈 cell로 재배치한다.

- [ ] In-game: `탐사대 호위대원`의 `호` glyph 깨짐
  - 검증 보강: `탐사대 호위대원` 문장을 glyph visibility/fallback/layout 검사에 포함한다.
  - 수정 방향: 특정 글자만 하드코딩하지 않고, Hangul glyph source/atlas 충돌을 일반 규칙으로 처리한다.

- [ ] Occult Crescent: 메인은 `PHANTOM KNIGHT` / `Ph. Knight`, 서브는 `서포트 나이트`
  - 검증 보강: `MkdSupportJob` playable row 전체의 메인 full/short column과 support 설명 column을 확인한다.
  - 수정 방향: 특정 Knight만 예외 처리하지 않고, column 역할 기준으로 매핑한다.

## Verification Rule

- 재보고된 항목은 먼저 verifier가 실패하도록 만든다.
- verifier가 pass인데 사용자가 다시 실패를 보고하면 해당 검증이 불완전한 것으로 간주한다.
- 특정 row/codepoint만 임시로 덮지 않고, 가능한 경우 sheet column 역할, ULD route,
  FDT glyph category, atlas 충돌 회피 같은 일반 규칙으로 수정한다.
