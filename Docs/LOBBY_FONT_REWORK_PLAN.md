# 로비 폰트 재작업 계획

이 문서는 로비 폰트/로비 UI 한글 작업을 다시 시작하기 위한 기준 문서입니다. 로비 관련 코드를 수정하기 전에는 반드시 이 문서를 먼저 확인해야 합니다.

## 현재 결정

- 기존 로비 관련 구현은 재사용하지 않고, 참고용 코드로만 둔다.
- 기존 HEAD 기준 로비 구현 diff와 주요 파일 사본은 `.tmp/lobby-rework-reference/20260514-020259` 아래에 보관했다.
- 실제 코드 경로는 clean lobby baseline인 `47ec879 Preserve clean lobby font baseline` 기준으로 되돌렸다.
- 인게임 폰트는 별도 폰트/아틀라스를 사용하므로, 로비 재작업 중 인게임 폰트 수리 로직을 건드리지 않는다.
- 로비 작업은 `common/font/*_lobby.fdt`와 `common/font/font_lobby*.tex` 중심으로 제한한다.
- 데이터 센터 선택 화면은 현재 패치 대상에서 제외한다. route 조사는 회귀 감시용으로만 유지한다.
- 2026-05-15 사용자 확인 결과, TTMP 한글 source cell을 clean lobby texture에 복사하고 texture를 확장하는 방식은 실제 로비에서 더 나쁜 결과를 만들었다. 이 접근은 폐기한다.
- 로비 한글은 기존 아틀라스에 억지로 끼워 넣는 문제가 아니라, `FDT + font_lobby1..N.tex`가 서로 일치하는 완전한 로비 폰트 세트 문제로 다룬다.

## 오픈 리포지토리 조사 결론

- `Soreepeong/FFXIV-FontChanger`는 `font_lobby`가 최대 6개 texture file을 가질 수 있다고 설명한다.
- `Soreepeong/xivres`의 font packer는 glyph를 plane 단위로 배치하고 4 plane마다 새 texture stream을 만든다. 기준은 단일 atlas 확장이 아니라 page/plane 분리다.
- `GpointChen/FFXIVChnTextPatch-GP`는 `common/font`의 `.fdt`와 `.tex`를 완성된 리소스로 교체한다.
- `Souma-Sumire/FFXIVChnTextPatch-Souma` 리소스는 `font_lobby1.tex`, `font_lobby2.tex`, `font_lobby3.tex`와 lobby FDT 세트를 함께 제공한다.
- 결론: atlas 용량이 부족하면 기존 texture를 키우거나 source 좌표를 그대로 밀어 넣지 않는다. 필요한 glyph set을 새 lobby texture page까지 포함한 일관된 폰트 세트로 생성하거나 가져와야 한다.

## 다시 잡아야 하는 문제

- 시작 화면 시스템 설정에서 150%, 200%, 300% UI 배율일 때 특정 한글 문자가 다른 문자보다 작게 보인다.
- 150% 이상 캐릭터 선택 화면에서 대부분의 한글이 `-` 또는 `=`로 나오거나, 글자가 작게 나온다.
- 100%, 150%, 200%, 300% 배율 모두에서 한글이 정상 크기와 정상 glyph로 나와야 한다.
- 시스템 설정/캐릭터 선택/로비 오류 팝업을 한 화면군으로 묶지 말고, 실제 ULD/font route별로 분리해서 처리해야 한다.

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
- 기존 `font_lobby1.tex`/`font_lobby2.tex`를 4096보다 크게 확장하거나, source cell 좌표가 clean atlas 밖이라는 이유로 texture 자체를 키우지 않는다.
- TTMP source glyph entry만 가져오고 대응되는 texture page 세트를 갖추지 않은 상태로 FDT를 수정하지 않는다.
- `font_lobbyN.tex`가 실제 index에 없는데 FDT `image_index`만 그 page를 가리키게 만들지 않는다.

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
- 모든 patched lobby FDT glyph의 `image_index / 4`가 실제 존재하는 `font_lobbyN.tex`로 해석되어야 한다.
- 모든 patched lobby FDT glyph cell은 참조 texture의 width/height 안에 있어야 한다.
- clean에 존재하던 lobby texture는 패치 후 width/height가 커지면 실패로 처리한다. overflow는 texture resize가 아니라 page 수/폰트 세트 문제로 해결한다.
- source-cell 복사 방식 검증은 폐기하고, multi-texture font set route 검증을 사용한다.
- 2026-05-15 추가된 `lobby-multitexture-font-set` verifier는 거부된 `.tmp\lobby-hangul-expanded-r7-ja` 산출물을 `font_lobby3/4.tex` 참조 불일치로 실패시키고, `.tmp\lobby-clean-ja`는 통과시킨다.
- 인게임 폰트 관련 verifier가 회귀하지 않아야 한다.

## 다음 작업 순서

1. clean lobby baseline 상태에서 로비 화면별 ULD/font route 표를 만든다.
2. `Lobby/Error/Addon/ClassJob/Race/Tribe/GuardianDeity`의 실제 row/column 사용처를 덤프한다.
3. 화면별 필요한 glyph set을 만든다.
4. target font별 atlas capacity report를 만든다.
5. 기존 source-cell graft/texture expansion 경로를 코드에서 비활성화하고 verifier에서 금지한다.
6. complete lobby font package를 만들거나 소비하는 경로를 설계한다. 이때 FDT와 `font_lobby*.tex`의 page 수를 같은 단위로 다룬다.
7. capacity가 부족한 font는 새 texture page 또는 font별 분리 atlas 전략을 검토한 뒤 구현한다.
8. verifier가 100/150/200/300 배율을 모두 통과한 뒤에만 릴리즈 빌드를 만든다.
