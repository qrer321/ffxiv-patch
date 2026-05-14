# 로비 폰트 재작업 계획

이 문서는 로비 폰트/로비 UI 한글 작업을 다시 시작하기 위한 기준 문서입니다. 로비 관련 코드를 수정하기 전에는 반드시 이 문서를 먼저 확인해야 합니다.

## 현재 결정

- 기존 로비 관련 구현은 재사용하지 않고, 참고용 코드로만 둔다.
- 기존 HEAD 기준 로비 구현 diff와 주요 파일 사본은 `.tmp/lobby-rework-reference/20260514-020259` 아래에 보관했다.
- 실제 코드 경로는 clean lobby baseline인 `47ec879 Preserve clean lobby font baseline` 기준으로 되돌렸다.
- 인게임 폰트는 별도 폰트/아틀라스를 사용하므로, 로비 재작업 중 인게임 폰트 수리 로직을 건드리지 않는다.
- 로비 작업은 `common/font/*_lobby.fdt`와 `common/font/font_lobby*.tex` 중심으로 제한한다.

## 다시 잡아야 하는 문제

- 시작 화면 시스템 설정에서 150%, 200%, 300% UI 배율일 때 특정 한글 문자가 다른 문자보다 작게 보인다.
- 150% 이상 캐릭터 선택 화면에서 대부분의 한글이 `-` 또는 `=`로 나오거나, 글자가 작게 나온다.
- 100%, 150%, 200%, 300% 배율 모두에서 한글이 정상 크기와 정상 glyph로 나와야 한다.
- 데이터 센터/시스템 설정/캐릭터 선택/로비 오류 팝업을 한 화면군으로 묶지 말고, 실제 ULD/font route별로 분리해서 처리해야 한다.

## 커버 대상 시트

로비 텍스트 후보는 아래 시트를 기준으로 다시 조사한다. 전체 row를 바로 주입하지 말고, row/column 단위로 실제 사용 범위를 먼저 산출한다.

- `Lobby`
- `Error`
- `Addon`
- `ClassJob`
- `Race`
- `Tribe`
- `GuardianDeity`

추가 후보:

- 캐릭터 생일/월/수호성 표시에 실제로 쓰이는 별도 시트가 있으면 포함한다.
- `CharaMakeName`은 글로벌 캐릭터 이름에 한글이 들어가지 않으므로 로비 glyph coverage 대상에서 제외한다.

## 구현 전 조사

1. 로비 화면별 ULD를 먼저 확정한다.
   - 시작 화면 메인 메뉴
   - 시작 화면 시스템 설정
   - 데이터 센터 선택
   - 캐릭터 선택
   - 캐릭터 선택 상세 팝업/오류/백업/서버 이동
2. 각 ULD의 font id/size가 실제 어떤 `*_lobby.fdt`로 매핑되는지 배율별로 기록한다.
3. 각 화면에서 참조하는 EXD sheet, row, string column을 산출한다.
4. 각 target font별 필요한 한글 codepoint 수를 별도로 계산한다.
5. 각 target font별 기존 `font_lobby*.tex` 빈 공간과 필요한 glyph 셀 크기를 비교한다.
6. 아틀라스가 부족하면 시트 범위를 줄이는 방식만 반복하지 말고, texture page 추가/분리 가능성을 먼저 검토한다.

## 아틀라스 설계 질문

- 하나의 기존 lobby atlas에 모든 배율/화면 glyph를 넣어야 하는가?
- FDT glyph entry의 image index가 새 `font_lobby*.tex` page를 참조하도록 확장 가능한가?
- 기존 `font_lobby1.tex`~`font_lobby7.tex` 외 새 texture path를 index에 추가할 수 있는가?
- 배율별로 같은 glyph를 공유해야 하는가, 아니면 `AXIS_12/14/18/36_lobby`처럼 target font별로 별도 셀을 써야 하는가?
- 큰 배율 glyph가 작은 배율 source에서 오면 안 된다. 150% 이상은 대응되는 큰 source glyph를 사용해야 한다.

## 금지할 접근

- `Lobby`, `Error`, `Addon` 전체 row를 한 번에 커버하지 않는다.
- `Jupiter_*_lobby`, `TrumpGothic_*_lobby`에 AXIS glyph를 무차별 리맵하지 않는다.
- 100%에서 정상인 clean ASCII/영어/숫자 metrics를 바꾸지 않는다.
- `=`/`-`가 사라졌다는 이유만으로 완료 처리하지 않는다.
- 사용자가 클라이언트를 직접 열어 확인해야 한다는 식으로 넘기지 않는다.

## 검증 조건

생성 패치가 아래 조건을 만족하기 전에는 수정 완료로 판단하지 않는다.

- 작업 중 반복 검증은 선택 verifier만 사용한다. 기본 약한 검증은 `lobby-route-survey`만 사용하고, 필요한 route/source check는 clean base 조건이 맞을 때 명시적으로 추가한다.
- 전체 verifier와 긴 render/layout 검증은 로비 주입 방식이 안정된 뒤와 릴리즈 빌드 직전에만 실행한다.
- generator 로그에 로비 font별 required/changed/allocation-failures가 출력된다.
- 모든 로비 target font에서 `allocation-failures=0`이어야 한다.
- 100%, 150%, 200%, 300% 대응 render snapshot을 생성한다.
- 한글 glyph가 fallback `-`/`=`가 아닌지 확인한다.
- 150% 이상에서 glyph width/height가 작은 source glyph를 억지 확대하거나 축소한 값이 아닌지 확인한다.
- clean global 대비 데이터 센터 영어/숫자/특수문자 metrics와 texture neighborhood가 악화되지 않아야 한다.
- 인게임 폰트 관련 verifier가 회귀하지 않아야 한다.

## 다음 작업 순서

1. clean lobby baseline 상태에서 로비 화면별 ULD/font route 표를 만든다.
2. `Lobby/Error/Addon/ClassJob/Race/Tribe/GuardianDeity`의 실제 row/column 사용처를 덤프한다.
3. 화면별 필요한 glyph set을 만든다.
4. target font별 atlas capacity report를 만든다.
5. capacity가 충분한 font부터 최소 glyph injection을 구현한다.
6. capacity가 부족한 font는 새 texture page 또는 font별 분리 atlas 전략을 검토한 뒤 구현한다.
7. verifier가 100/150/200/300 배율을 모두 통과한 뒤에만 릴리즈 빌드를 만든다.
