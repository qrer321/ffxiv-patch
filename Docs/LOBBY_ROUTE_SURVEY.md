# 로비 route/sheet 조사 결과

이 문서는 로비 폰트 재작업 전에 확인한 실제 ULD/font route와 로비 관련 시트 위치를 기록한다. 로비 glyph coverage를 넓힐 때는 이 문서를 기준으로 target font와 row 범위를 먼저 결정한다.

## 조사 산출물

- 기준 커밋: `1359277 Reset lobby font rework baseline`
- 기준 산출물: `.tmp/lobby-rework-baseline-ja`
- 실행 검증: `PatchRouteVerifier --checks lobby-route-survey`
- 결과: `RESULT: PASS`
- 리포트 위치: `.tmp/lobby-rework-baseline-ja/patch-route-glyph-dumps`
- 주의: 현재 `D:` 글로벌 클라이언트 index가 패치 적용 상태라, 산출물은 `-AllowPatchedGlobal`로 만든 조사용이다. 릴리즈 판단에는 clean/base index가 필요하다.

## ULD Font Route 요약

`lobby-uld-font-summary.tsv` 기준 현재 조사된 routed font:

- `data-center-select`: `AXIS_12_lobby`, `AXIS_14_lobby`, `Jupiter_16_lobby`, `Jupiter_20_lobby`, `MiedingerMid_14_lobby`, `TrumpGothic_23_lobby`, `TrumpGothic_34_lobby`
- `start-system-settings`: `AXIS_12`, `AXIS_14`, `AXIS_18`, `TrumpGothic_23` 및 같은 ULD의 `_lobby` 후보
- `start-main-menu`: `MiedingerMid_18_lobby`
- `character-select`: `AXIS_12_lobby`, `AXIS_14_lobby`, `AXIS_18_lobby`, `MiedingerMid_12_lobby`, `MiedingerMid_14_lobby`, `TrumpGothic_23_lobby`, `TrumpGothic_34_lobby`

중요: `start-system-settings`는 non-lobby와 lobby 후보가 모두 남아 있다. 실제 제목/로비 컨텍스트에서 어느 suffix를 타는지 확정하기 전에는 전역 `AXIS_*`를 수정하지 않는다.
중요: `data-center-select`는 현재 패치 대상에서 제외한다. 이 route는 조사/회귀 감시용으로만 유지하고 glyph coverage나 atlas allocation 후보에 넣지 않는다.

## Sheet Coverage 요약

`lobby-sheet-hangul-summary.tsv` 기준 한글 row/unique codepoint 수:

- `Addon`: 13013 rows, 841 codepoints
- `Lobby`: 917 rows, 645 codepoints
- `Error`: 62 rows, 211 codepoints
- `ClassJob`: 44 rows, 77 codepoints
- `Race`: 8 rows, 20 codepoints
- `Tribe`: 16 rows, 42 codepoints
- `GuardianDeity`: 12 rows, 214 codepoints

전체 시트 주입은 금지한다. 위 숫자는 커버리지 상한을 보기 위한 조사값이며, 실제 주입 범위는 ULD route와 화면별 row 목록으로 줄여야 한다.

## 보고된 누락 문구 위치

- 시작 메뉴: `Lobby#2003`/`Addon#4000` 시스템 설정, `Lobby#2059`/`Addon#2744` 설치 정보, `Lobby#2009`/`Lobby#2052` 뒤로
- 캐릭터 선택 기본: `Lobby#101` 종족 및 성별, `Lobby#2019` 직업, `Lobby#1223` 캐릭터 정보 불러오기
- 캐릭터 선택 메뉴: `Lobby#23` 이름 변경, `Lobby#24` 집사 이름 변경, `Lobby#841`/`Lobby#842` 캐릭터 설정 데이터 백업
- 서버 이동/방문: `Lobby#1100`/`Lobby#1104` 쾌적한 서버로 이동, `Lobby#1150` 다른 데이터 센터 방문
- 백업/설정: `Lobby#849`/`Lobby#850` 백업 대상, `Lobby#921` 각종 HUD 위치와 크기, `Lobby#975` 설정 데이터 복사
- 직업명: `ClassJob#20` 몽크, `ClassJob#37` 건브레이커, `ClassJob#42` 픽토맨서
- 생일/월: `Lobby#41`~`Lobby#53` 그림자/별빛 월 문자열
- 수호성: `Lobby#160` 니메이아 외 로비 row, `GuardianDeity` 전체는 설명문이 길어 glyph coverage 범위로 바로 쓰지 않는다.
- 로그인 오류: `Error#13206` 순차적으로 로그인 처리

## 다음 조사/구현 규칙

- `CharaMakeName`은 계속 제외한다.
- `ClassJob`, `Race`, `Tribe`, `GuardianDeity`는 캐릭터 선택에 보이는 이름/짧은 라벨 row부터 처리하고, 설명문 전체 glyph를 바로 넣지 않는다.
- `Lobby#458` 같은 긴 종족 설명문은 atlas capacity 검토 전까지 glyph source로 쓰지 않는다.
- `Addon` 전체 13013 rows는 로비 대상이 아니다. 시작 메뉴/설정 공유 등 확인된 row만 별도 후보로 관리한다.
- 다음 구현 전 필요한 리포트: target font별 required glyph count와 atlas free capacity.

## Atlas Capacity 조사

- 실행 검증: `PatchRouteVerifier --checks lobby-atlas-capacity`
- 리포트 위치: `.tmp/lobby-atlas-capacity-smoke`
- 데이터 센터는 glyph coverage와 atlas allocation 후보에서 제외했다.
- 결과: 12 target fonts, 3569 required codepoints, 2795 missing target glyphs, 2795 source-covered glyphs, 1019 aggregate allocation failures.
- source glyph는 모두 TTMP font pack 안에서 확인됐다. 현재 실패는 source 부재가 아니라 기존 `font_lobby1.tex`~`font_lobby7.tex` 용량/배치 실패다.
- 독립 배치 실패가 큰 폰트는 `TrumpGothic_34_lobby` 279 glyphs, `TrumpGothic_23_lobby` 154 glyphs, `AXIS_18_lobby` 56 glyphs다.
- 이 상태에서 기존 아틀라스에 글리프를 복사 주입하는 방식으로 진행하면 텍스처 오염, 번짐, fallback 재발 가능성이 높다. 다음 구현 전에는 새 texture page 추가, screen/font별 분리, 또는 source texture cell 재사용 가능성을 먼저 확인한다.
- source texture 조사 결과, 필요한 로비 글리프 대부분은 TTMP의 `font_lobby1.tex`, 일부는 `font_lobby2.tex`에 있다.
- 현재 기준 산출물의 `font_lobby1.tex`/`font_lobby2.tex`는 clean global과 같고 TTMP와 다르다. 따라서 TTMP FDT glyph entry만 가져오면 잘못된 clean texture cell을 참조한다.
- TTMP `font_lobby1.tex`/`font_lobby2.tex`를 통째로 적용하는 방식은 shared lobby texture를 바꾸므로 데이터 센터/영문 glyph metrics 회귀를 먼저 검증해야 한다.

## TTMP Lobby ASCII Delta 조사

- 실행 검증: `PatchRouteVerifier --checks lobby-ttmp-ascii-delta`
- 리포트 위치: `.tmp/lobby-ttmp-ascii-delta-smoke`
- 결과: 22 fonts, 1267 ASCII glyphs, 940 shape/spacing differences, 223 pixel differences, 5 data-center routed visual-unsafe fonts.
- 데이터 센터 routed font 중 `AXIS_12_lobby`, `AXIS_14_lobby`, `MiedingerMid_14_lobby`, `TrumpGothic_23_lobby`, `TrumpGothic_34_lobby`는 clean 대비 TTMP shape/spacing 차이가 있다.
- `Jupiter_16_lobby`/`Jupiter_20_lobby`는 visual shape/pixel은 clean과 같지만 Shift-JIS 값은 다르다. kerning/lookup 영향은 별도 검증 전까지 안전하다고 보지 않는다.
- 결론: TTMP lobby FDT/TEX를 통째로 적용하는 방식은 데이터 센터를 건드리지 않는다는 조건과 충돌한다. 한글 glyph만 필요 font에 넣되 clean ASCII cell/spacing을 보존하거나, 데이터 센터가 타지 않는 별도 route/page 전략을 찾아야 한다.

## Source Cell Conflict 조사

- 실행 검증: `PatchRouteVerifier --checks lobby-source-cell-conflicts`
- 리포트 위치: `.tmp/lobby-source-cell-conflicts-smoke`
- 결과: 2537 candidate glyph cells, 0 known FDT conflicts, 44 texture alpha conflicts, 0 data-center FDT conflicts, 0 source overlap pairs.
- TTMP 한글 glyph cell 좌표는 현재 clean/patched 로비 FDT에서 참조 중인 glyph cell과 겹치지 않는다.
- source cell끼리도 서로 다른 glyph를 덮는 overlap은 없다.
- 이 결과만으로 source cell 복사 구현을 진행하지 않는다. 2026-05-15 실제 적용 결과, source cell을 clean lobby texture에 복사하고 atlas overflow를 texture 확장으로 수습한 빌드는 로비 150% 이상에서 glyph 깨짐/번짐/크기 이상을 더 악화시켰다.
- source cell 복사 방식은 폐기한다. 다음 구현은 `FDT + font_lobby1..N.tex`가 함께 맞는 complete multi-texture lobby font set을 생성하거나 소비하는 방향으로 진행한다.
- clean texture에 이미 alpha가 있는 미참조 영역 44개는 여전히 회귀 위험이다. 하지만 해결책은 같은 좌표 덮어쓰기나 texture resize가 아니라, 새 texture page와 FDT `image_index`가 함께 유효한지 검증하는 방식이어야 한다.

## Multi-texture Font Set 기준

- 모든 lobby glyph의 `image_index / 4`는 실제 존재하는 `font_lobbyN.tex`로 해석되어야 한다.
- glyph의 `x/y/width/height`는 참조 texture의 bounds 안에 있어야 한다.
- clean에 있던 `font_lobby*.tex`는 패치 후 width/height가 바뀌면 실패로 처리한다.
- atlas가 부족하면 기존 texture를 키우지 않고 page 수, glyph set, 또는 font route를 다시 설계한다.
