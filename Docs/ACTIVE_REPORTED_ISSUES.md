# Active Reported Issues

## 2026-05-13 Lobby Route-Scoped Hangul Reinjection Attempt

- Clean lobby FDT/ASCII metrics are preserved, and only required Hangul glyph cells are injected into selected lobby fonts (`AXIS_*_lobby`, data-center popup/menu fonts).
- Existing clean lobby glyph cells are reserved before allocation to avoid contaminating English/number glyphs.
- 2026-05-15 correction: existing 1024 lobby atlas allocation was not enough for all required Hangul without risking clean glyph contamination.
- 2026-05-15 rejection: the follow-up implementation that expanded affected clean lobby textures to fit TTMP source coordinates made live lobby output worse. Source-cell grafting plus texture expansion is rejected and must not be used as a success path.
- Current direction: 로비 한글은 `FDT + font_lobby1..N.tex`가 함께 맞는 complete multi-texture lobby font set으로 해결한다. 기존 texture resize나 partial source-cell 복사로 overflow를 수습하지 않는다.
- 2026-05-13 검증 정정: 이전 focused PASS는 verifier가 generated output dat와 fallback/baseline dat를 잘못 섞어 읽어서 missing glyph를 놓친 false PASS였다. `CompositeArchive`가 sibling `orig.*.index`를 baseline으로 사용하고, generated font dat에 같은 offset의 patched entry가 있으면 primary dat를 읽도록 수정했다.
- 이 검증기 수정 뒤 기존 산출물은 `Jupiter_45_lobby.fdt`의 `템/치/뒤`, `Jupiter_20_lobby.fdt`의 character-select glyph 누락을 제대로 실패시켰다.
- Latest fresh ja output `.tmp\lobby-route-scoped-ja-r4` passes `clean-ascii-font-routes,numeric-glyphs,data-center-title-uld,data-center-worldmap-uld,start-system-settings-uld,start-main-menu-phrase-layouts,character-select-lobby-phrase-layouts,lobby-render-snapshots,hangul-source-preservation,configuration-sharing,bozja-entrance,occult-crescent-support-jobs,reported-ingame-hangul-phrases,action-detail-scale-layouts` with `Verifier failures: 0`.
- Rejected ja output `.tmp\lobby-hangul-expanded-r7-ja` passed `lobby-hangul-source-cells,clean-ascii-font-routes,data-center-title-glyphs`, but the verifier target was wrong because it blessed the source-cell/texture-expansion route that failed live. Do not use this output or check as a fix signal.
- 2026-05-15 multi-texture attempt: `.tmp\lobby-multitex-allocated-ja-r2` keeps clean lobby texture dimensions, allocates required TTMP Hangul alpha into clean `font_lobby3..6.tex`, and passes `lobby-multitexture-font-set` plus `lobby-hangul-visibility`. `lobby-render-snapshots` passes the `_lobby.fdt` routes but still fails non-lobby `AXIS_12/14.fdt` high-scale candidates, so the reported 로비 150%+ issue remains open.
- 2026-05-15 follow-up: `.tmp\lobby-multitex-kerning-ja-r2` adds only start-screen system-settings kerning pairs collected from the known high-resolution/result-message phrase lists. It keeps clean lobby ASCII metric/kerning as the baseline and allows only the `AXIS_14/KrnAXIS_140` `0/%` pair as a generated start-screen exception. Focused verification passed: `lobby-multitexture-font-set,lobby-hangul-visibility,lobby-render-snapshots,clean-ascii-font-routes,reported-ingame-hangul-phrases,action-detail-scale-layouts,hangul-source-preservation`. This is not user-confirmed fixed.
- 2026-05-15 supplemental high-scale follow-up: `.tmp\lobby-supplemental-full-ja-r1` also patches clean high-scale lobby FDTs that are not present in the TTMP payload list (`AXIS_36_lobby`, `Jupiter_46_lobby`, `Jupiter_90_lobby`, `Meidinger_40_lobby`, `MiedingerMid_36_lobby`, `TrumpGothic_68_lobby`). Hangul alpha is copied with atlas-neighborhood padding, glyph coordinates point inside the padded cell, and source advance/offset is preserved instead of normalized. Full font/text verification passed, plus in-game regression checks for reported Hangul phrases and ActionDetail scale layout. This is still not user-confirmed fixed.
- 2026-05-15 verifier baseline split: route verification now accepts separate clean baseline paths for text/font/ui (`--global-text`, `--global-font`, `--global-ui`) so ULD checks do not mix a clean font snapshot with an unrelated UI dat set. `.tmp\lobby-kerning-axis12-split-ja-r1` passes split-baseline `start-system-settings-uld`, lobby layout, clean ASCII/numeric, and reported in-game regression checks. AXIS_12 lobby system-settings kerning uses 2px because 1px still overlapped; AXIS_14/18 lobby stay at 1px.
- `lobby-clean-payloads`는 clean reset 시점의 guard다. 현재 설계는 route-scoped Hangul glyph를 selected lobby FDT/TEX에 의도적으로 추가하므로, 이 check를 현재 성공 기준으로 쓰지 않는다. 대신 clean ASCII/number preservation, ULD route checks, render snapshots, Hangul source preservation, reported in-game regression checks를 함께 사용한다.
- This is not user-confirmed fixed yet.

사용자가 "아직 안 된다"고 재보고한 항목은 구현만 다시 보지 않고,
먼저 검증 방식 자체가 실패를 잡도록 강화한 뒤 수정한다.

## Current Checklist

- [ ] 예정 조사: 기존에 참고했던 GitHub 저장소 중 로비/시작 화면용 sheet, row, column, route map을 미리 확보한 저장소가 있는지 확인
  - 추가일: 2026-05-13
  - 지금 바로 실행하지 않는다. 후속 조사 항목으로만 유지한다.
  - 목적: 로비 한글 커버리지를 broad EXD 수집이나 단일 glyph 수동 추가에 의존하지 않도록, upstream 또는 관련 저장소에 이미 정리된 route-scoped sheet 데이터가 있는지 확인한다.
  - 제약: 로비 폰트 재조합 금지 원칙은 유지한다. 발견한 sheet map은 넓은 glyph atlas injection이 아니라 경로 기반 커버리지와 verifier 케이스에만 사용한다.

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
  - 2026-05-09 재보고 후 확인: 실제 적용 폴더는 여전히 최신 산출물과 달랐다. `ffxiv_dx11.exe`/`XIVLauncher.exe` 실행 중인 상태에서 `.tmp\verifier-applied-after-user-report.log`가 `applied-output-files` 및 lobby ASCII padding FAIL을 보고했다. 또한 `Release\Public\FFXIVKoreanPatch.exe`가 오래된 embedded generator를 포함하고 있었으므로 release build script에 embedded generator SHA-256 검증을 추가했다.
  - 2026-05-10 검증 보강: FDT `OffsetX`를 draw 시작 위치가 아니라 advance 조정값으로 해석하도록 verifier 렌더링을 수정했다. 이전 설치 산출물은 새 기준에서 `DATA CENTER SELECT`, `Elemental`, `Tonberry` 등 데이터센터 라벨 visual gap FAIL을 재현한다.
  - 2026-05-10 재처리 정정: safe spacing 보정은 100% 로비에서 글자 간격을 과하게 벌렸고, 비로비 인게임 FDT를 clean reference로 쓰는 방식도 로비 렌더러의 실제 기본 폰트와 달라 번짐/간격 판단을 흐렸다. `_lobby.fdt` ASCII/숫자/기호는 이제 동일 이름의 clean lobby FDT를 기준으로 복원한다. 예: `AXIS_12_lobby.fdt` -> clean `AXIS_12_lobby.fdt`.
  - 2026-05-10 검증 결과: `.tmp\lobby-axis-hangul-advance-ja`는 `data-center-title-uld,data-center-worldmap-uld,clean-ascii-font-routes,high-scale-ascii-phrase-layouts`와 전체 broad verifier PASS. 로비 ASCII는 clean lobby FDT의 glyph/kerning/texture 기준과 일치해야 하며, 비로비 인게임 metric으로 강제하지 않는다.
  - 2026-05-10 재보고 후 검증 정정: 로비 번짐/간격 검증은 실제 UI 배율에서 치환되는 큰 로비 폰트가 동급 소스 glyph를 쓰는지도 확인해야 한다. `lobby-scale-font-sources` verifier를 추가해 `AXIS_12/14/18_lobby`는 TTMP의 같은 로비 폰트와, `AXIS_36_lobby` 등 파생 고배율 로비 폰트는 대응되는 TTMP 고배율 source와 glyph 크기/픽셀이 일치하는지 검사한다.
  - 다음 처리: 클라이언트/런처 종료 후 최신 `Release\Public\FFXIVKoreanPatch.exe` 또는 산출물로 font patch를 재적용하고, `applied-output-files,data-center-title-uld,data-center-worldmap-uld`를 실제 적용 폴더 기준으로 PASS시킨다.

- [ ] Data center select: 서버명은 영어로 나오지만 문자 간격이 서로 침범함
  - 재보고일: 2026-05-09
  - 검증 보강: `Elemental`, `Gaia`, `Mana`, 실제 월드명 전체를 데이터센터 화면 route font에서 문장 단위 layout으로 검사한다.
  - 수정 방향: 월드/데이터센터 ASCII 라벨의 advance/kerning을 clean global과 일치시키고, TTMP texture cell에 덮어쓴 ASCII가 흐리거나 좁게 나오지 않게 한다.
  - 2026-05-09 보강: 데이터센터 ULD route별 핵심 라벨 문장 픽셀 비교를 추가했다. 현재 산출물은 clean global 대비 라벨 문장 폭과 픽셀이 일치한다.
  - 2026-05-09 추가 보강: `DataCenterWorldmapLabels`의 non-space ASCII glyph 전체에 대해 clean global 대비 2px texture padding까지 비교한다. padding 복사는 lobby FDT에만 제한해 일반 인게임/대사 폰트의 atlas 오염을 막는다.
  - 검증 결과: `.tmp\verifier-ja-clean-ascii-after-padding-scope.log` PASS, `.tmp\verifier-ja-regression-after-padding.log` PASS.
  - 2026-05-09 재보고 후 확인: 실제 적용 폴더 기준 verifier에서 lobby ASCII texture padding이 실패하므로, 서버명 간격/번짐 문제도 산출물 수정 미적용 상태로 추적한다.
  - 2026-05-09 재검증 보강: 서버/데이터센터 ASCII도 4px texture neighborhood로 올려 검증한다. 새 산출물은 데이터센터 전체 라벨 metrics/pixels/padding route 검증을 PASS한다.
  - 2026-05-10 검증 보강: clean과 픽셀만 같으면 통과하던 기존 phrase 검증을 인접 glyph alpha bounds 기반 최소 visual gap 검사로 교체했다. 이전 설치 산출물은 서버명/데이터센터명에서 negative advance 및 negative kerning으로 FAIL한다.
  - 2026-05-10 재처리/검증 정정: 로비 전용 advance/kerning 강제 보정을 제거하고, `_lobby.fdt` ASCII/숫자/기호를 동일 이름의 clean lobby FDT metric/kerning/texture 기준으로 복원했다. `.tmp\lobby-axis-hangul-advance-ja`는 데이터센터 route, high-scale ASCII phrase, clean ASCII route, broad verifier를 PASS한다.

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
  - 2026-05-09 재보고 후 확인: 생성 산출물 PASS만으로 닫으면 안 된다. 실제 적용 폴더는 최신 font output과 다르고, 기존 `Release\Public` exe도 오래된 embedded generator를 포함하고 있었다. `Scripts\build-release.ps1`가 배포 exe의 embedded `FFXIVPatchGenerator.exe` SHA-256을 최신 빌드 산출물과 비교하도록 보강했다.
  - 2026-05-10 검증 보강: `system-settings-mixed-scale-layouts`가 인접 glyph pair의 실제 alpha bounds로 visual gap을 측정하고, 실패 시 문자쌍/kerning/glyph metrics를 출력한다. 이전 설치 산출물은 `150%(FHD)`, `200%(WQHD)`, `300%(4K)`에서 FAIL한다.
  - 2026-05-10 재처리 정정: 4K lobby 파생 폰트와 lobby ASCII route의 safe advance/negative kerning 정규화를 제거했다. ASCII/숫자/기호는 동일 이름의 clean lobby FDT를 기준으로 복원하고, 해당 reference에 없는 문장 기호만 fallback source에서 보충한다. 이 변경은 `_lobby.fdt` 경로에만 제한해 인게임/대사 폰트 metrics 오염을 막는다.
  - 2026-05-10 원인 추가: 한글 `=` fallback은 해결됐지만, `AXIS_12_lobby`/`AXIS_14_lobby`/`AXIS_18_lobby`의 TTMP 한글 glyph가 `OffsetX=-3` 계열로 들어와 advance가 glyph width보다 좁아지는 경우가 있었다. 로비 고배율 문장에서는 이 값이 인접 한글 alpha bounds를 침범시키므로, 픽셀은 그대로 두고 위 세 로비 AXIS 폰트의 한글에 한해 `source advance < glyph width`일 때만 `OffsetX=0`으로 정규화한다.
  - 2026-05-10 검증 결과: `.tmp\lobby-axis-hangul-advance-ja`는 `start-system-settings-uld,clean-ascii-font-routes,system-settings-mixed-scale-layouts` PASS, `data-center-title-uld,data-center-worldmap-uld,system-settings-scaled-phrase-layouts,high-scale-ascii-phrase-layouts,lobby-hangul-visibility` PASS, 전체 broad verifier PASS. `hangul-source-preservation`은 대표 문구 5,072개 TTMP 한글 glyph 렌더를 비교하고, 별도 전수 스캔으로 로비 AXIS 한글 table 34,437개 중 33,779개의 advance-only 정규화가 규칙과 맞는지 확인한다.
  - 2026-05-10 재보고 후 검증 정정: `일부 설정은 적용을 눌러야 반영됩니다.`가 결과 메시지 목록에서 빠져 있어 고배율 파생 로비 폰트 glyph 수집과 문장 검증에서 누락됐다. 해당 문구를 `StartScreenSystemSettingsResultMessages`에 추가했고, `start-system-settings-uld`가 `150%(FHD): ...` 같은 고해상도 UI 옵션 문구도 `AXIS_12/14/18_lobby` route에서 검사하도록 확장했다.
  - 2026-05-10 재조합 검증 추가: `lobby-scale-font-sources`는 로비 배율별 대응 폰트가 100% glyph를 억지 확대하지 않았는지 잡기 위해, scale-sensitive Hangul 260개를 대상으로 target glyph width/height와 렌더 alpha가 대응 TTMP source와 동일한지 검사한다. 새 산출물 `.tmp\lobby-scale-source-ja`는 `start-system-settings-uld,lobby-scale-font-sources,system-settings-mixed-scale-layouts` PASS.
  - 다음 처리: font patch 재적용 후 `applied-output-files,start-system-settings-uld,lobby-scale-font-sources,4k-lobby-phrase-layouts,lobby-hangul-visibility`를 실제 적용 폴더 기준으로 PASS시킨다. 그래도 재현되면 시작화면 시스템 설정의 실제 runtime scale substitution map을 더 세분화해 verifier에 추가한다.

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

- [x] In-game: `즉시 발동`/`초`가 UI 배율별 상대 크기를 제대로 따라가지 않음
  - 재보고일: 2026-05-10
  - 증상: 100%/150%에서는 `즉시 발동`과 `초`가 주변 문자보다 크게 보이고, 200%/300%에서는 주변 문자보다 작게 보임. 깨짐이 아니라 같은 UI 안에서 상대 크기가 어긋나는 문제다.
  - 검증 공백: 기존 `reported-ingame-hangul-phrases`는 patched glyph가 TTMP 원본과 같은지만 확인하므로, UI 배율별 font route와 주변 숫자/라벨 대비 상대 크기 차이를 잡지 못한다.
  - 검증 보강: `ActionDetail.uld`의 실제 font route를 수집하고, `즉시 발동`, `초`, `120.00초`, `1.50초`를 숫자 baseline과 함께 렌더링해 한글/숫자 시각 높이 비율 및 `TrumpGothic_34 -> TrumpGothic_68` 스케일 비율을 검사하는 `action-detail-scale-layouts` verifier를 추가했다.
  - 처리: `TrumpGothic_68.fdt`의 action-detail 고배율 Hangul glyph를 `Addon#699-714`와 fallback 문구에서 자동 수집한 codepoint 기준으로 보정한다. 특정 글자 보호 배열이 아니라 route/font category 단위 수리이며, atlas cell은 새로 할당해 기존 glyph cell을 덮어쓰지 않는다.
  - 검증 결과: `.tmp\action-detail-scale-ja4` 및 `.tmp\action-detail-scale-ja4-apply` 산출물이 `action-detail-scale-layouts` PASS. `reported-ingame-hangul-phrases`, `4k-lobby-font-derivations`, `hangul-source-preservation`도 새 기준으로 PASS.
  - 적용 상태: 실제 `D:\SquareEnix\FINAL FANTASY XIV - A Realm Reborn\game` 복사는 실행 중인 `ffxiv_dx11`/`XIVLauncher` 파일 잠금으로 차단됨. 생성 산출물 자체는 검증 완료.

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
  - 남은 방향: 새로 보고된 `즉시 발동`/`초` 배율별 상대 크기 문제는 TTMP 원본 동일성 검증이 아니라 UI route별 scale-ratio 검증으로 확인한다.

## 2026-05-10 lobby high-scale font follow-up

- 사용자 지시: 사용자가 OK하기 전까지 시작화면/로비 150% 이상 폰트 문제는 `수정 완료`로 판단하지 않는다. 코드상 조치와 verifier 산출물은 남기되, 상태는 열린 이슈로 유지한다.
- 재검증 정정: upstream `ffxiv-patch-generator`는 TTMP payload를 재패킹할 뿐 FDT glyph cell을 재조합하지 않는다. 우리 쪽에서 추가한 broad `KrnAXIS` lobby route가 `AXIS_12/14/18/36_lobby` Hangul의 높이/컴포넌트 수를 바꾸며, 사용자가 보고한 150%+ pixelation 증상과 같은 방향의 오염을 만들었다.
- 코드상 조치: broad `KrnAXIS_120/140/180/360` lobby Hangul 치환을 제거하고, `AXIS_*_lobby` Hangul glyph의 TTMP source 크기/pixel을 복원했다. 겹침 방지는 `AXIS_12_lobby`, `AXIS_14_lobby`, `AXIS_18_lobby`에서 source advance가 glyph width보다 좁은 Hangul에 한해 `OffsetX=0`으로 바꾸는 advance-only 보정만 허용한다. `AXIS_36_lobby`는 다시 TTMP `AXIS_36.fdt` 기반 파생 route를 탄다.
- 검증 보강: `lobby-scale-font-sources`는 TTMP source 대비 width/height/pixel alpha를 직접 비교하고, 이전 KrnAXIS 산출물 `.tmp\lobby-spacing-normalized-ja-v3`를 FAIL시킨다. `korean-lobby-font-sources`는 KrnAXIS route가 비활성화되어야 한다는 guard로 축소했다. 시작 메뉴/시스템 설정 phrase verifier는 pixel/size 보존과 advance-only 보정 폭을 분리해 계산한다.
- 현재 산출물 기준: `.tmp\lobby-ttmp-source-advance-ja`는 focused verifier `.tmp\verifier-lobby-ttmp-source-advance-focused-r5.log` 및 broad verifier `.tmp\verifier-lobby-ttmp-source-advance-full-r2.log`에서 PASS한다.
- 열린 이슈: 실제 시작화면/로비 150%+ 번짐/간격은 사용자 OK 전까지 열린 항목으로 유지한다. generated-output PASS만으로 완료 처리하지 않고, 적용 폴더 기준 검증과 사용자 확인을 별도 단계로 둔다.
- 2026-05-11 재검증 보강: upstream `korean-patch/ffxiv-patch-generator`는 TTMP payload를 그대로 얹는 흐름인데, 우리 verifier는 그 뒤의 재조합 결과만 `_lobby` 중심으로 확인하고 있었다. `lobby-render-snapshots`는 `설정을 변경했습니다.`, `150%(FHD): 1728x972 이상 권장` 등을 PNG/TSV로 덤프하고, `_lobby`뿐 아니라 실제 150% 후보인 non-lobby `AXIS_18.fdt`/`KrnAXIS_180.fdt`와 200/300% 후보 `AXIS_36.fdt`/`KrnAXIS_360.fdt`를 비교한다. `AXIS_18.fdt`/`KrnAXIS_180.fdt` advance를 직접 보정한 `.tmp\lobby-axis-advance-ja`는 render snapshot은 통과했지만 `reported-ingame-hangul-phrases`에서 인게임 문구를 오염시켜 폐기했다. 전역 AXIS/KrnAXIS는 진단 대상으로만 남기고, 실제 수정은 로비 전용 route나 타이틀 컨텍스트 route를 분리하는 방향으로 유지한다. 사용자 OK 전까지 완료로 닫지 않는다.
- 2026-05-12 재검증 강화: `lobby-render-snapshots`는 `min_pair`를 기록하고, 재보고된 실제 후보 `AXIS_12.fdt` 150%와 `AXIS_14.fdt` 150/200/300%를 `minGap >= 1` 실패 조건으로 승격했다. 이 기준에서 r1은 `했/습`, `다/.`, `0/%` 계열로 실패했고, r2 산출물 `.tmp\startscreen-route-kerning-ja-r2`는 통과했다.
- 2026-05-12 코드 조치: 보정 pair는 시작화면 시스템 설정 문구 목록에서 자동 수집한다. 직접 glyph 보호 배열이나 broad advance 보정은 피하고, `AXIS_12/14/18` 및 대응 `KrnAXIS`의 필요한 kerning pair만 최소 보정한다. 같은 FDT가 인게임에서도 쓰일 수 있으므로 `.tmp\verifier-startscreen-route-kerning-regression.log`에서 데이터센터, 설정 공유, 보즈야, 오컬트 크레센트, 인게임 보고 문구, ActionDetail, Hangul source preservation, lobby render snapshot 회귀 묶음을 함께 통과시켰다. 사용자 OK 전까지 완료로 닫지 않는다.
- 2026-05-12 reset: 사용자가 재보고한 로비 150%+ pixelation/blur/spacing 문제가 전혀 개선되지 않았으므로, 실패했던 로비 glyph 재조합 계층을 기본 생성에서 제거했다. 제거 대상은 derived 4K lobby FDT 생성, `AXIS_12/14/18_lobby` Hangul advance 정규화, `AXIS_14_lobby` blank Hangul alias, start-screen system-settings kerning 우회다.
- 2026-05-12 reset 검증: 새 `lobby-ttmp-payloads` verifier는 TTMP에 포함된 lobby FDT/TEX가 TTMP payload와 byte-for-byte 동일하고, TTMP에 없는 high-scale lobby target은 clean global payload와 동일해야 통과한다. `.tmp\lobby-reset-ja`는 `lobby-ttmp-payloads,hangul-source-preservation,configuration-sharing,bozja-entrance,occult-crescent-support-jobs,reported-ingame-hangul-phrases,action-detail-scale-layouts` PASS.
- 2026-05-12 남은 실패: reset 산출물은 `lobby-render-snapshots`에서 여전히 FAIL한다. 대표 실패는 `Jupiter_20_lobby` data-center world list `A/t` overlap, `TrumpGothic_23_lobby` title `A/T` minGap -2, `AXIS_12/18_lobby` 설정 완료 문구 한글 minGap -1, `AXIS_36_lobby`의 `U+C124` missing, 그리고 non-lobby `AXIS_12/14` 150/200/300% 후보 minGap < 1이다. 따라서 이 이슈는 해결로 판단하지 않는다.
- 다음 구현 방향: 재조합을 되살리더라도 TTMP lobby payload 전체를 임의로 섞지 않는다. 먼저 실제 로비/타이틀 컨텍스트에서 쓰는 FDT와 source를 route 단위로 확정하고, source selection manifest와 render snapshot 실패 조건을 같이 추가한 뒤 최소 glyph/texture만 생성한다. 인게임 공용 FDT는 오염시키지 않는다.
- 2026-05-12 clean lobby baseline correction: 위 reset은 로비 glyph 재조합만 제거했고 TTMP `_lobby.fdt`/`font_lobby*.tex` 적용은 남아 있었다. 이번 기준은 TTMP 및 Korean direct fallback 양쪽에서 lobby FDT/TEX payload를 건너뛰고 clean global lobby asset을 그대로 보존한다. 새 기본 검증 `lobby-clean-payloads`는 lobby FDT/TEX가 clean global과 일치해야 통과한다. `.tmp\lobby-clean-ja`는 `lobby-clean-payloads,clean-ascii-font-routes,hangul-source-preservation,configuration-sharing,bozja-entrance,occult-crescent-support-jobs,action-detail-scale-layouts` PASS.
- 2026-05-12 clean baseline render status: `lobby-render-snapshots`는 아직 FAIL이어야 한다. clean lobby fonts에는 `설정을 변경했습니다.`/`150%(FHD)...`용 Korean glyph가 빠지고, `Jupiter_20_lobby`/`TrumpGothic_23_lobby`는 clean-source English spacing issue를 그대로 드러낸다. non-lobby `AXIS_12/14` high-scale candidates도 `minGap >= 1`을 통과하지 못한다. 다음 구현은 clean baseline 위에서 route-specific 최소 Korean glyph/texture injection을 다시 설계해야 하며, 사용자가 OK하기 전까지 완료로 판단하지 않는다.
- 2026-05-12 default verifier correction: clean baseline은 lobby Korean glyph injection이 빠져 있고 clean-source data-center visual-gap 실패도 그대로 드러내므로 `data-center-title-uld`, `data-center-worldmap-uld`, `start-system-settings-uld`, `system-settings-mixed-scale-layouts`, `start-main-menu-phrase-layouts`는 현재 기본 smoke check에서 제외한다. 해당 실패는 `lobby-render-snapshots`의 열린 이슈로 유지하고, 새 route-specific injection 구현 뒤 기본 묶음에 다시 넣는다.
- 2026-05-12 clean-client observation: clean 클라이언트에서는 로비 글자 침범/번짐/깨짐이 재현되지 않는다는 사용자 확인에 따라, verifier의 절대 `minGap` 기준은 clean 자체를 고칠 대상으로 보지 않는다. lobby ASCII/숫자/영어는 patched가 clean global보다 나빠졌는지로만 판단한다. `.tmp\verify-clean-render-relative.log` 기준으로 데이터센터 영어 `Jupiter_20_lobby`/`TrumpGothic_23_lobby`는 clean-baseline OK가 되었고, 남은 실패는 Korean glyph missing 및 Korean/high-scale layout뿐이다.
- 2026-05-13 verifier false-pass correction: generated output을 읽는 `CompositeArchive`가 patched font dat의 same-offset entry를 fallback/base dat로 잘못 읽는 경우가 있어, `start-main-menu-phrase-layouts`/`character-select-lobby-phrase-layouts`가 실제 산출물의 missing glyph를 놓쳤다. 이제 sibling `orig.*.index`를 baseline으로 두고, generated font dat에 offset이 존재하면 primary dat를 우선 읽는다. 이 수정으로 기존 r3 산출물에서 `Jupiter_45_lobby`/`Jupiter_20_lobby` 누락 glyph가 재현됐고, route-scoped coverage 수정 뒤 `.tmp\lobby-route-scoped-ja-r4`가 fresh generate-and-verify 전체 묶음을 PASS했다.
- 2026-05-13 current verifier set: 현재 로비 한글 재주입 설계는 selected lobby FDT/TEX를 수정하므로 `lobby-clean-payloads`는 의도적으로 제외한다. 대신 `clean-ascii-font-routes`, `numeric-glyphs`, 데이터센터/시스템설정 ULD route, start main menu/character-select phrase layouts, `lobby-render-snapshots`, `hangul-source-preservation`, 설정 공유/보즈야/오컬트/인게임 보고 문구/ActionDetail 회귀 검증을 한 번에 돌린다. 사용자 OK 전까지 실제 로비 150%+ 이슈는 열린 상태로 둔다.

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
- Release build now verifies that `Release\Public\FFXIVKoreanPatch.exe` embeds the same `FFXIVPatchGenerator.exe` SHA-256 as the freshly built generator. This prevents stale release executables from regenerating old font output after code fixes.
- 4K lobby font generation now derives additional system-settings Hangul/ASCII coverage from Korean Addon rows `4000-4200` and `8683-8722`, instead of relying only on the fixed representative phrase list. `.tmp\verifier-lobby-dynamic-core.log` passes the focused lobby/start-screen checks.
- `system-settings-mixed-scale-layouts` now verifies the exact high-resolution UI option strings shown in the start-screen system settings, including `150%(FHD): 1728x972 이상 권장`, with ASCII metrics/texture padding and Hangul advance checks.
- Lobby start-screen setting result messages now include `설정을 변경했습니다.`. The old output failed with missing `U+D588` in all six high-scale lobby fonts; `.tmp\lobby-result-message-fix-ja` passes `.tmp\verifier-lobby-result-message-regression.log`.
- Current output passes `.tmp\verifier-ja-regression-with-reported-phrases.log` for data-center rows/language slots, start-screen system settings, configuration sharing, Bozja, Occult Crescent support jobs, clean ASCII font routes, 4K lobby phrase/layout, Hangul source preservation, reported in-game Hangul phrases, and lobby Hangul visibility.
- ULD font route checks now also assert text node header and text extra render-state bytes match clean global. Current output passes `.tmp\verifier-uld-render-state.log`.
- `applied-output-files` compares generated output packed bytes against `--applied-game`; use it to rule out an installed-folder mismatch before live client retesting.
- Release UI reapply now permits an already patched client when a clean base index is available from the current index, installed `orig.*`, or any same-version local `restore-baseline` language folder; this addresses the case where generated output passes but the installed English client still has older lobby font/UI packed files.
- 2026-05-10 lobby blur/overlap follow-up: previous checks compared glyph interiors and clean-baseline spacing, so they missed atlas-neighborhood pollution and clean lobby fonts whose own ASCII spacing rendered with negative visual gaps. Generator now copies derived lobby Hangul with 4px texture padding, reserves 4px neighborhoods around existing lobby glyph cells before ASCII allocation, and normalizes lobby ASCII advance/kerning to a minimum 1px visual gap. `lobby-scale-font-sources`, data-center ULD checks, start-screen system-settings checks, and `system-settings-mixed-scale-layouts` now fail on texture-neighborhood differences or visual gap below 1px. Current ja output `.tmp\lobby-spacing-floor1-ja` passes the full verifier (`.tmp\verifier-lobby-spacing-floor1-full.log`).
- 2026-05-10 lobby font route correction: the 1px visual-gap normalization above was too broad and made the 100% lobby text look unnaturally wide while still failing some high-scale lobby cases. The generator now preserves TTMP/source FDT advance and kerning instead of widening lobby glyphs. Derived 4K lobby targets such as `Jupiter_90_lobby.fdt` and `Meidinger_40_lobby.fdt` now source both Hangul and phrase-required ASCII/numeric glyphs from their paired source fonts, avoiding mixed 90px/46px glyph sizes inside one phrase.
- 2026-05-10 verifier correction: `start-main-menu-phrase-layouts`, `system-settings-mixed-scale-layouts`, `system-settings-scaled-phrase-layouts`, `lobby-scale-font-sources`, `clean-ascii-font-routes`, `numeric-glyphs`, `4k-lobby-font-derivations`, and `hangul-source-preservation` now compare the generated lobby output against the same TTMP/clean source route used by the generator. Full ja verification passes for `.tmp\lobby-derived-source-ascii-ja` in `.tmp\verifier-lobby-derived-source-ascii-full-r4.log`.
- 2026-05-10 applied-folder verification: `Scripts\generate-and-verify-patch-routes.ps1` now builds a fresh output and immediately runs route verification, including `applied-output-files` when `-AppliedGame` is supplied. Current ja smoke run against `D:\SquareEnix\FINAL FANTASY XIV - A Realm Reborn\game` fails 25 applied lobby font/texture files, so that installed folder is still carrying older lobby font output than the latest generated patch.
- 2026-05-10 applied-folder closed: `Scripts\generate-and-verify-patch-routes.ps1 -ApplyToAppliedGame` generated `.tmp\applied-route-ja-apply`, backed up the previous target files to `.tmp\applied-route-backups\20260510-193548`, copied the generated patch files into `D:\SquareEnix\FINAL FANTASY XIV - A Realm Reborn\game\sqpack\ffxiv`, and passed focused route verification with `Verifier failures: 0`. Full verifier with `--applied-game` also passes in `.tmp\verifier-applied-route-ja-full.log`.
