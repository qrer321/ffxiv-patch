# Active Reported Issues

사용자가 "아직 안 된다"고 재보고한 항목은 구현만 다시 보지 않고,
먼저 검증 방식 자체가 실패를 잡도록 강화한 뒤 수정한다.

## Current Checklist

- [x] Start screen: `종료`가 `EXIT`로 표시됨
  - 재보고일: 2026-05-09
  - 원인: `Lobby#2002`를 글로벌 target row로 강제한 정책 때문에 시작 화면 종료 버튼이 영어 클라이언트 기준 `EXIT`로 바뀜.
  - 검증 보강: `data-center-rows`가 `Lobby#2002`를 `종료`로 확인하고, `EXIT`가 남으면 실패해야 한다.
  - 처리: 단순히 글로벌 target row를 제거하면 한국 원본이 `나가기`로 들어가므로, 사용자 요구에 맞춰 `Lobby#2002`는 `종료` literal로 고정했다. `data-center-language-slots`도 이 row의 한글을 허용해 모든 언어 슬롯에서 `종료`가 유지되는지 확인한다.
  - 남은 분리 작업: 데이터센터 화면에서 깨졌던 `-로?`는 별도 `뒤로` 버튼 row일 가능성이 있으므로, 다시 보이면 `Lobby#2002`와 혼동하지 않고 실제 row를 추적한다.

- [ ] Data center select: 그룹명은 영어로 나오지만 흐리게 렌더링됨
  - 재보고일: 2026-05-09
  - 검증 보강: 실제 `Title_DataCenter.uld`/`Title_Worldmap.uld` route가 쓰는 font와 texture를 확인하고, 해당 라벨을 clean global 렌더와 픽셀/metrics 비교한다.
  - 수정 방향: 데이터센터 선택 화면의 ASCII 라벨은 TTMP 한글 폰트가 아니라 clean global ASCII glyph/kerning/texture를 사용해야 한다.
  - 2026-05-09 보강: `Elemental`, `Mana`, 주요 서버명 등 핵심 라벨을 문장 픽셀 스냅샷으로 clean global과 직접 비교하도록 verifier를 확장했다. 현재 산출물은 FDT glyph/metrics/kerning/문장 픽셀 기준으로 통과한다.
  - 2026-05-09 추가 보강: glyph box만 비교하면 런타임 스케일링에서 atlas 주변 픽셀이 섞이는 문제를 놓칠 수 있어, 데이터센터 routed ASCII glyph의 2px texture neighborhood를 clean global과 비교하도록 verifier를 확장했다. 제너레이터는 lobby clean ASCII cell을 할당할 때 해당 padding까지 같이 복사한다.
  - 검증 결과: 최신 ja 산출물은 `.tmp\verifier-dc-texture-padding-fix-ja.log`에서 `data-center-title-uld,data-center-worldmap-uld` PASS.
  - 2026-05-09 재보고 후 확인: 최신 산출물은 PASS지만 실제 적용 폴더 `D:\SquareEnix\FINAL FANTASY XIV - A Realm Reborn\game`은 `applied-output-files`에서 lobby FDT/texture가 generated output과 다르고, `data-center-title-uld`에서 `AXIS_12_lobby`/`AXIS_14_lobby` ASCII 2px texture padding이 FAIL한다. 현재 문제는 실제 클라이언트에 최신 `000000` font dat/index가 반영되지 않은 상태로 분리한다.
  - 2026-05-09 재검증 보강: 사용자가 계속 보고한 번짐은 2px 주변 비교로 부족할 수 있으므로 clean global ASCII texture neighborhood 기준을 4px로 올렸다. 이전 산출물은 새 4px verifier에서 실패했고, 새 산출물은 `data-center-title-uld,data-center-worldmap-uld`를 포함한 `.tmp\verifier-reported-font-routes-after-fix2.log`에서 PASS한다.
  - 다음 처리: 클라이언트/런처 종료 후 최신 산출물 또는 release UI로 font patch를 재적용하고, `applied-output-files,data-center-title-uld,data-center-worldmap-uld`를 실제 적용 폴더 기준으로 PASS시킨다.

- [ ] Data center select: 서버명은 영어로 나오지만 문자 간격이 서로 침범함
  - 재보고일: 2026-05-09
  - 검증 보강: `Elemental`, `Gaia`, `Mana`, 실제 월드명 전체를 데이터센터 화면 route font에서 문장 단위 layout으로 검사한다.
  - 수정 방향: 월드/데이터센터 ASCII 라벨의 advance/kerning을 clean global과 일치시키고, TTMP texture cell에 덮어쓴 ASCII가 흐리거나 좁게 나오지 않게 한다.
  - 2026-05-09 보강: 데이터센터 ULD route별 핵심 라벨 문장 픽셀 비교를 추가했다. 현재 산출물은 clean global 대비 라벨 문장 폭과 픽셀이 일치한다.
  - 2026-05-09 추가 보강: `DataCenterWorldmapLabels`의 non-space ASCII glyph 전체에 대해 clean global 대비 2px texture padding까지 비교한다. padding 복사는 lobby FDT에만 제한해 일반 인게임/대사 폰트의 atlas 오염을 막는다.
  - 검증 결과: `.tmp\verifier-ja-clean-ascii-after-padding-scope.log` PASS, `.tmp\verifier-ja-regression-after-padding.log` PASS.
  - 2026-05-09 재보고 후 확인: 실제 적용 폴더 기준 verifier에서 lobby ASCII texture padding이 실패하므로, 서버명 간격/번짐 문제도 산출물 수정 미적용 상태로 추적한다.
  - 2026-05-09 재검증 보강: 서버/데이터센터 ASCII도 4px texture neighborhood로 올려 검증한다. 새 산출물은 데이터센터 전체 라벨 metrics/pixels/padding route 검증을 PASS한다.

- [x] Data center select: 화면을 나가는 버튼이 `-로?` / `-료?`처럼 잘못 표시됨
  - 재보고일: 2026-05-09
  - 원인: 시작 화면의 `종료`는 `Lobby#2002`지만 데이터센터 화면의 `뒤로` 계열은 `Lobby#2009`, `Lobby#2052` 등 별도 row였다. 기존 검증은 `Lobby#2002`만 확인해 `-로?` 재발을 놓칠 수 있었다.
  - 검증 보강: `data-center-rows`/`data-center-language-slots`가 `Lobby#2009`, `Lobby#2047`, `Lobby#2050`, `Lobby#2051`, `Lobby#2052`를 각 언어 슬롯에서 확인한다. `data-center-title-uld`/`data-center-worldmap-uld`는 `뒤로`, `이전 단계로 되돌아가기` 문구의 glyph fallback/overlap도 검사한다.
  - 처리: 로비 액션 row를 정책 테이블로 묶고 `종료`/`뒤로`/`확인`/`취소`를 literal remap으로 고정했다. 이 remap row는 secondary language safety patch에도 들어가므로 `ja/en/de/fr` 슬롯이 서로 다른 언어로 갈라지지 않는다.

- [ ] Start screen system settings: UI 배율을 키우면 한글이 `=`로 깨지고 문자 간격이 침범함
  - 재보고일: 2026-05-09
  - 검증 보강: 시작 화면 시스템 설정이 실제로 쓰는 ULD/font route를 찾고, 150/200/300%에서 해당 route의 한글 대표 문구를 glyph fallback/layout 검사에 넣는다.
  - 처리: `그래픽 설정`, `UI 해상도 설정`, `고해상도 UI 크기 설정` 등 실제 시스템 설정 Addon 라벨을 `LobbyScaledHangulPhrases`로 분리하고, 제너레이터와 verifier가 같은 목록을 사용하도록 변경했다. 4K 로비 파생 폰트에 필요한 glyph cell을 추가하고, `start-system-settings-uld`/`4k-lobby-phrase-layouts`에서 150/200/300% 대표 라벨을 검사한다.
  - 검증 결과: 이전 산출물은 새 verifier에서 누락 glyph로 실패했고, 새 산출물은 4K lobby phrase/layout 및 TTMP source preservation 기준으로 통과했다.
  - 2026-05-09 재보고 후 확인: 실제 적용 폴더의 lobby FDT/texture가 최신 산출물과 다르다. `start-system-settings-uld` 자체는 기존 대표 문구 layout 기준으로 PASS하지만, 고배율 로비 폰트가 실제 적용 산출물과 일치하지 않는 상태에서는 클라이언트 증상을 닫지 않는다.
  - 2026-05-09 검증/생성 보강: 4K 로비 파생 폰트의 필요 glyph를 고정 대표 문구만으로 만들지 않고, 한국 Addon의 시스템 설정/고해상도 UI 관련 row 범위(`4000-4200`, `8683-8722`)에서 Hangul/ASCII codepoint를 수집해 생성한다. verifier도 같은 row 범위를 patched text에서 읽어 4K 로비 폰트 누락 glyph를 검사한다.
  - 검증 결과: `.tmp\lobby-dynamic-glyphs-ja`는 `4k-lobby-font-derivations,data-center-title-uld,data-center-worldmap-uld,start-system-settings-uld` PASS. 생성 로그 기준 4K 로비 required codepoint는 static 123 + Addon-derived 204 = 327개.
  - 2026-05-09 재보고 후 원인 보강: 기존 검증은 glyph 존재 여부와 대표 한글 문구만 봐서, `150%(FHD): 1728x972 이상 권장`처럼 한글/ASCII/숫자/기호가 섞인 실제 문장 layout 오염을 놓쳤다. 새 `system-settings-mixed-scale-layouts` verifier는 스크린샷 문장을 그대로 사용해 ASCII metrics, 4px texture padding, Hangul advance, phrase overlap을 함께 검사한다.
  - 처리: 4K lobby 파생 폰트는 Hangul만 한국 TTMP source에서 가져오고, ASCII/숫자/기호는 clean global lobby route를 유지한다. clean target에 일부 ASCII가 없는 4K lobby font는 기존 파생 source pair의 clean lobby font에서만 보충하며, 이 보충 glyph도 4px padding을 같이 복사한다. Hangul advance는 clean CJK median 또는 glyph width 기반 safety advance보다 좁아지지 않게 보정한다.
  - 검증 결과: 이전 산출물은 `system-settings-mixed-scale-layouts`에서 ASCII texture padding/overlap/missing glyph로 FAIL했고, 새 산출물 `.tmp\mixed-scale-spacing-fix-ja9`는 `.tmp\verifier-reported-font-routes-after-fix2.log`에서 `data-center-title-uld,data-center-worldmap-uld,start-system-settings-uld,system-settings-mixed-scale-layouts,high-scale-ascii-phrase-layouts,clean-ascii-font-routes,4k-lobby-font-derivations,4k-lobby-phrase-layouts` PASS.
  - 다음 처리: font patch 재적용 후 `applied-output-files,start-system-settings-uld,4k-lobby-phrase-layouts,lobby-hangul-visibility`를 실제 적용 폴더 기준으로 PASS시킨다. 그래도 재현되면 시작화면 시스템 설정의 실제 scale별 font substitution을 별도 verifier로 추가한다.

- [x] Data center select popup: `데이터 센터 Mana에 입장합니다` 계열 팝업 문구가 base client 언어로 나옴 - 처리됨
  - 재보고일: 2026-05-09
  - 검증 보강: 팝업 문구의 sheet/row/column과 lookup 구조를 찾아 EN/JA 각각에서 값이 의도한 언어인지 확인한다.
  - 수정 방향: 글로벌 전용 lookup row가 아닌 실제 번역 가능한 row라면 한국어로 유지하고, lookup 구조가 글로벌 전용이면 SeString 구조를 깨지 않는 방식으로 literal만 병합한다.
  - 상태 정정: 사용자 확인 기준으로 이미 수정된 항목이므로 재작업 대상에서 제외한다. 재발하면 이 항목의 verifier를 먼저 실패하도록 보강한다.

- [x] ESC system menu: `コンテンツシェア`/`コンフィグシェア` 계열 설정 공유 메뉴 제목이 base client 언어로 남음
  - 보고일: 2026-05-09
  - 원인: ESC 시스템 메뉴 항목은 `MainCommand#99`를 참조하며, 한국 서버의 해당 row title/description이 비어 있어 기존 Addon-only 보정이 닿지 않았음. 추가로 `Addon#17301` 같은 subtitle row는 한섭 기준 빈 문자열이어야 하는데 `설정 공유`로 강제되어 main/subtitle pair가 오염됐음.
  - 검증 보강: `configuration-sharing` verifier가 `ja/en/de/fr` 전체 슬롯에서 `MainCommand#99`와 설정 공유 Addon title/subtitle pair를 검사한다. `コンフィグシェア`, `コンテンツシェア`, `Configuration Sharing`, `CONFIG SHARE`가 남거나 main/subtitle이 같은 텍스트이면 실패한다.
  - 처리: `MainCommand#99` title/description과 설정 공유 main title rows(`17300/17315/17330/17360/17380`)는 한국어 literal로 고정하고, subtitle rows(`17301/17316/17331/17361/17381`)는 빈 문자열 literal로 고정했다. `ColumnRemap.Literal("")`이 secondary language patch에서 무시되던 패처 조건도 수정했다.

- [x] In-game: `즉시 발동` 등 일부 한글 폰트가 크게 보임
  - 재보고일: 2026-05-09
  - 사용자 정정: 폰트가 깨지거나 잘못된 폰트로 보이는 문제는 아님. 다른 인게임 UI 폰트보다 상대적으로 커 보이는지 확인해 달라는 관찰 항목임.
  - 현재 관찰: 인게임 한글 폰트 계열은 원하는 폰트로 돌아왔음. 다만 일부 문구의 glyph size/advance가 원래 TTMP 기준인지, 패치로 과대화된 것인지 확인 필요.
  - 검증 보강: `즉시 발동`과 비슷한 짧은 UI 문구를 TTMP 원본 대비 pixel/metrics/source preservation으로 비교한다.
  - 처리: `reported-ingame-hangul-phrases` verifier를 추가해 `즉시 발동`, `시전 시간`, `재사용 대기 시간`, `발동 조건`을 모든 TTMP-covered in-game font route에서 문장 단위 pixel/layout/metrics로 비교한다.
  - 검증 결과: `.tmp\verifier-ja-reported-ingame-phrases.log`와 `.tmp\verifier-ja-regression-with-reported-phrases.log` PASS. patched output이 TTMP 원본과 같으므로 패치가 glyph를 키운 문제는 아닌 것으로 분리한다.

- [x] In-game: `탐사대 호위대원`의 `호` glyph 깨짐
  - 검증 보강: `탐사대 호위대원` 문장을 glyph visibility/fallback/layout 검사에 포함한다.
  - 처리: `호` 같은 특정 글자를 보호 배열에 넣지 않고, 보고 문구 목록에서 Hangul codepoint를 자동 수집해 `hangul-source-preservation`으로 TTMP 원본 대비 glyph source를 검증한다. 추가로 `reported-ingame-hangul-phrases`가 문장 전체 pixel/layout/metrics를 TTMP 원본과 비교한다.
  - 검증 결과: `.tmp\verifier-ja-reported-ingame-phrases.log`와 `.tmp\verifier-ja-regression-with-reported-phrases.log` PASS.

- [ ] Occult Crescent: 메인은 `PHANTOM KNIGHT` / `Ph. Knight`, 서브는 `서포트 나이트`
  - 검증 보강: `MkdSupportJob` playable row 전체의 메인 full/short column과 support 설명 column을 확인한다.
  - 수정 방향: 특정 Knight만 예외 처리하지 않고, column 역할 기준으로 매핑한다.

- [ ] Story completion card: 스토리 종료 후 컷씬/이벤트 이미지 설명문이 base client 언어로 나옴
  - 보고일: 2026-05-09
  - 사용자 기대: 폰트만 교체해서 한글이 보이게 하는 방식이 아니라, 지역 이동 시 나오는 이미지형 지역명처럼 실제 표시 리소스를 한국 클라이언트 리소스로 바꿔야 함.
  - 현재 판단: 아직 scene 리소스라고 단정하지 않는다. `EventImage`일 가능성도 있으므로 먼저 어떤 sheet/resource가 표시를 담당하는지 판별한다.
  - 후보 경로: `EventImage`, `CutScreenImage`, `ScreenImage`, `DynamicEventScreenImage`, `LoadingImage`, 또는 컷씬/이벤트 전용 texture/ULD/bundle. 이미 `UiPatchGenerator`가 일부 sheet 기반 이미지 리소스를 복사하므로, 누락된 sheet/row/texture path를 찾아 같은 계열로 확장한다.
  - 검증 보강: story 완료 화면에서 표시되는 image ID와 실제 `ui/icon/...`, `ui/loadingimage/...`, event/cutscene texture path를 diagnostics에 남기고, global target 리소스와 Korean source 리소스의 packed bytes/hash가 다르면 Korean source가 output index에 매핑되었는지 확인한다.
  - 수정 방향: 1) 완료 설명문이 텍스트 EXD인지 이미지 texture인지 먼저 판별한다. 2) 이미지라면 global row의 icon ID만 기준으로 삼지 말고 Korean EXD row도 비교해 한국 서버에서 다른 image ID를 쓰는 경우를 추적한다. 3) 언어 폴더형 리소스는 target language 폴더에 `ko` source를 복사하고, same-path 리소스는 동일 path의 Korean packed texture를 복사한다. 4) sheet 기반으로 잡히지 않을 때만 event/cutscene bundle 단위로 global clean과 Korean source를 비교해 안전하게 교체 가능한 최소 파일을 찾는다.
  - 주의 사항: 일반 자막/대사 텍스트와 혼동하지 않는다. story 종료 카드처럼 장면에 박혀 있는 설명문은 폰트 FDT 수리 대상이 아니라 image/event resource localization 대상이다.

- [ ] Follow-up: 기존 한글 폰트와 달라져 보이는 glyph 대응 어색함
  - 2026-05-09 처리: `hangul-source-preservation` verifier 추가, 전역 Hangul offset 정규화 제거.
  - 남은 방향: 새로 보고된 `즉시 발동` 계열 문구가 TTMP 원본과 같은지 별도 대표 문구로 확인한다.

## Verification Rule

- 재보고된 항목은 먼저 verifier가 실패하도록 만든다.
- verifier가 pass인데 사용자가 다시 실패를 보고하면 해당 검증이 불완전한 것으로 간주한다.
- 특정 row/codepoint만 임시로 덮지 않고, 가능한 경우 sheet column 역할, ULD route,
  FDT glyph category, atlas 충돌 회피 같은 일반 규칙으로 수정한다.

## 2026-05-09 verifier note

- Data center phrase layout and pixel rendering checks now apply FDT kerning before each glyph/space advance.
- Data center ASCII pixel checks now run against every `DataCenterWorldmapLabels` entry, including DC group names and server/world names.
- Data center routed ASCII texture-neighborhood checks compare the 4px padding around every non-space ASCII glyph used by DC groups/world names. Latest ja output passes `.tmp\verifier-reported-font-routes-after-fix2.log`.
- Latest ja output passes `data-center-title-uld`, but the installed `D:` game folder currently fails `applied-output-files` for lobby FDT/texture files and fails `data-center-title-uld` texture padding for `AXIS_12_lobby`/`AXIS_14_lobby`. Do not treat generated-output PASS as client PASS until the applied folder also passes.
- 4K lobby font generation now derives additional system-settings Hangul/ASCII coverage from Korean Addon rows `4000-4200` and `8683-8722`, instead of relying only on the fixed representative phrase list. `.tmp\verifier-lobby-dynamic-core.log` passes the focused lobby/start-screen checks.
- `system-settings-mixed-scale-layouts` now verifies the exact high-resolution UI option strings shown in the start-screen system settings, including `150%(FHD): 1728x972 이상 권장`, with ASCII metrics/texture padding and Hangul advance checks.
- Current output passes `.tmp\verifier-ja-regression-with-reported-phrases.log` for data-center rows/language slots, start-screen system settings, configuration sharing, Bozja, Occult Crescent support jobs, clean ASCII font routes, 4K lobby phrase/layout, Hangul source preservation, reported in-game Hangul phrases, and lobby Hangul visibility.
- ULD font route checks now also assert text node header and text extra render-state bytes match clean global. Current output passes `.tmp\verifier-uld-render-state.log`.
- `applied-output-files` compares generated output packed bytes against `--applied-game`; use it to rule out an installed-folder mismatch before live client retesting.
- Release UI reapply now permits an already patched client when a clean base index is available from the current index, installed `orig.*`, or any same-version local `restore-baseline` language folder; this addresses the case where generated output passes but the installed English client still has older lobby font/UI packed files.
