# 세션 인수인계용 제약 사항

이 문서는 FFXIV 한글 패치 UI/제너레이터 작업을 다른 세션으로 넘길 때 반드시 유지해야 하는 제약과 검증 기준을 정리한 것입니다.

## 프로젝트 경로

- 메인 작업 경로: `E:\codex\ffxiv-patch-clone`
- 실제 게임 테스트 대상 글로벌 클라이언트: `D:\SquareEnix\FINAL FANTASY XIV - A Realm Reborn\game`
- 한국 서버 클라이언트: `E:\FINAL FANTASY XIV - KOREA\game`
- 제너레이터: `FFXIVPatchGenerator`
- UI: `FFXIVPatchUI`
- 검증기: `Tools\PatchRouteVerifier`

## 절대 지켜야 할 안전 제약

- 원본 글로벌/한국 서버 클라이언트 파일에는 직접 쓰지 않는다.
- 산출물은 항상 별도 output 폴더 아래에 만든다.
- output이 원본 게임 폴더 내부면 중단해야 한다.
- 실제 적용 전에는 clean/orig index를 기준으로 산출물을 만들어야 한다.
- `0a0000`, `000000`, `060000` 모두 `index`와 `index2`를 같이 다뤄야 한다.
- 패치 제거/복구는 백업된 원본 index/index2/dat 파일 기준으로 동작해야 한다.
- 백업 파일이 없을 때는 Yes/No 진행 팝업을 띄우지 말고, 먼저 “복구 가능한 백업 없음”을 알려야 한다.
- 테스트 빌드의 자동 패치/디버그 경로 기능은 릴리즈 빌드에 노출되면 안 된다.
- 릴리즈 배포 파일은 최종적으로 `FFXIVKoreanPatch.exe` 하나만 올리는 구조가 목표다.

## 패치 생성 제약

- 글로벌 클라이언트의 `root.exl` 기준으로 sheet를 순회한다.
- 글로벌 EXH 구조를 기준으로 대상 언어 EXD를 재생성한다.
- 문자열 컬럼만 한국 서버 EXD의 SeString 바이트로 교체한다.
- EXD Default variant 중심으로 처리한다.
- Subrows variant sheet는 현재 기본 스킵 대상이다.
- 글로벌/한국 서버 버전이 다르면 row-id fallback이 위험하므로 패치를 막는 방향을 유지한다.
- 이미 글로벌 index가 dat1을 가리키고 clean/orig index가 없으면 배포용 생성은 막아야 한다.
- `--allow-patched-global`은 실험용이며 릴리즈 기본 흐름에서 권장하지 않는다.

## 폰트 패치 제약

- 기본 폰트 소스는 TTMP 패키지(`TTMPD.mpd`, `TTMPL.mpl`)를 사용한다.
- 한국 서버 폰트 직접 복사는 실험용 fallback이며 릴리즈 기본값으로 쓰면 안 된다.
- TTMP의 FDT와 texture는 가능한 한 한 세트로 유지한다.
- 인게임 폰트가 정상인 상태라면 로비 폰트 수정이 인게임 폰트를 건드리면 안 된다.
- 로비 한글 글리프는 “보인다/폴백이 아니다”만으로 검증하면 부족하다. TTMP 원본 렌더와 픽셀 비교가 필요하다.
- 로비용 `Jupiter_*_lobby`, `TrumpGothic_*_lobby`의 한글 glyph를 광범위하게 AXIS 셀로 리맵하면 로비 글자가 망가진다.
- `ReplaceDirtyLobbyHangulGlyphsFromAxis`는 실제 오염이 관측된 소수 글자만 대상으로 유지한다.
- 현재 좁은 수리 대상:
  - `U+B9AD` 릭
  - `U+C815` 정
  - `U+BCC0` 변
  - `U+D558` 하
  - `U+B9BC` 림
- 위 목록을 넓히려면 반드시 glyph dump 실패 또는 실제 픽셀 비교 실패 근거가 있어야 한다.
- 파티 리스트 본인 번호는 `U+E0E1`~`U+E0E8` 경로를 보호해야 한다.
- `U+E0B1`~`U+E0B8`을 그대로 본인 번호처럼 덮으면 모양이 달라질 수 있다.
- 본인 번호는 clean global의 박스형 번호 glyph를 기준으로 연결해야 한다.
- ASCII/일본어/영어 로비 폰트는 데이터 센터 화면에서 clean global 모양과 metrics를 유지해야 한다.
- TrumpGothic 계열을 넓게 수정하면 영어/일본어 UI 폰트가 깨질 수 있으므로 기본 릴리즈 프로필에서는 광범위 수정 금지.

## 4K/고배율 UI 제약

- 150%, 200%, 300% UI 스케일에서 로비 한글이 깨지면 안 된다.
- 4K 로비 파생 폰트는 clean global lobby FDT를 base로 두고, 필요한 한글 glyph만 한국어 가능한 source에서 추가하는 방향을 유지한다.
- 4K 로비에서 한글 glyph의 음수 advance adjustment를 그대로 가져오면 글자 간섭이 생길 수 있으므로 normalize가 필요하다.
- 4K 수정 후에는 기본 배율 로비 폰트도 같이 망가지지 않았는지 검증해야 한다.

## 데이터 센터 화면 제약

- `DATA CENTER SELECT`, `INFORMATION`, 리전/데이터센터 그룹명은 베이스 글로벌 클라이언트 언어 또는 clean global 값을 따라야 한다.
- 데이터센터 화면의 ULD 폰트 슬롯은 clean global 값을 유지해야 한다.
- ULD 폰트 슬롯을 강제로 바꾸면 `AXIS_20_lobby`처럼 TTMP에 없는 경로를 타며 `=`, `--` 폴백이 나올 수 있다.
- `WorldRegionGroup`, `WorldPhysicalDC`, `WorldDCGroupType` 같은 데이터센터 그룹 row는 한국어로 무리하게 치환하지 말고 clean global/영어 기준을 우선한다.
- 데이터센터 그룹명이 `--`, `==`로 보이는 문제는 EXD 값과 FDT glyph route를 동시에 검증해야 한다.

## UI 텍스처 패치 제약

- `060000` UI 패치는 index/index2/dat4를 같이 생성한다.
- 파티 리스트 본인 번호 관련 UI texture는 한국 서버 리소스와 글로벌 리소스의 glyph 의미가 다를 수 있다.
- 지역명/로딩/입장 이미지 계열은 EXD 텍스트가 아니라 texture일 수 있으므로 `ScreenImage`, `CutScreenImage`, `TerritoryType`, `Map` 계열도 별도 검증해야 한다.
- UI texture 패치가 폰트 texture cell을 덮어쓰지 않는지 확인해야 한다.

## 검증 제약

- 사용자가 게임을 켜서 확인하기 전에 로컬 검증기로 최대한 잡아야 한다.
- 기존 “보임/폴백 아님” 검증은 부족했다. 실제 렌더 픽셀 비교를 우선한다.
- 현재 verifier는 TTMP source preservation 검증을 지원한다.
- 검증 스크립트:

```powershell
.\Scripts\verify-patch-routes.ps1 `
  -Output "<generated-output>" `
  -Global "D:\SquareEnix\FINAL FANTASY XIV - A Realm Reborn\game" `
  -TargetLanguage ja `
  -FontPackDir ".\FFXIVPatchGenerator\bin\Release" `
  -NoGlyphDump
```

- glyph dump가 필요하면 `-NoGlyphDump`를 빼고 `-GlyphDumpDir <dir>`을 지정한다.
- 새 verifier는 TTMP 기준으로 로비 한글 픽셀 비교를 수행해야 한다.
- 정상 새 산출물 예시:
  - `Checks: pass=7500, warn=33, fail=0`
  - `RESULT: PASS`
- 이전 문제 산출물은 같은 verifier에서 로비 한글 TTMP 비교 실패가 다수 발생해야 한다.
- generator 로그에서 일반 로비 폰트의 `Remapped artifact-prone lobby Hangul glyphs`가 `645`처럼 대량이면 잘못된 산출물이다.
- 현재 의도한 정상 범위는 대상 폰트당 약 `5`개다.

## 현재 주의할 점

- 작업 트리에는 여러 파일의 미커밋 변경이 남아 있을 수 있다. 커밋/스쿼시 전에는 `git status`와 `git diff`를 꼭 확인한다.
- 사용자가 명시하지 않으면 커밋/푸시/릴리즈 배포를 하지 않는다.
- 사용자가 “릴리즈 빌드”를 요청하면 `Scripts\build-release.ps1`로 빌드한다.
- 사용자가 “배포”를 요청하면 GitHub Release 업로드 정책에 따라 exe 단일 파일 업로드를 우선한다.
- 테스트 산출물이나 `.tmp` 분석 파일은 커밋하지 않는다.

## 최근 핵심 결론

- 인게임 폰트는 정상이고 로비 폰트만 망가진 상태라면, 인게임 폰트 수리 로직을 건드리지 말고 로비 FDT/texture만 봐야 한다.
- 로비 폰트가 망가진 가장 유력한 원인은 로비 한글 glyph를 너무 넓게 AXIS atlas cell로 리맵한 것이다.
- 해결 방향은 “광범위 리맵 제거 + 실제 오염 글자만 좁게 수리 + TTMP 원본 픽셀 보존 검증”이다.
- 이 기준으로 만든 새 산출물은 verifier에서 PASS했고, 기존 광범위 리맵 산출물은 새 verifier에서 FAIL이 발생하는 것을 확인했다.
- 2026-05-12 정정: “재조합 제거”와 “TTMP lobby payload 적용 제거”는 별개였다. 현재 clean baseline은 TTMP/Korean direct fallback의 `_lobby.fdt`와 `font_lobby*.tex`를 모두 건너뛰고 clean global lobby asset을 유지한다.
- 새 기준 검증은 `lobby-clean-payloads`다. `.tmp\lobby-clean-ja`는 clean lobby payload, clean ASCII route, Hangul source preservation, Configuration Sharing, Bozja, Occult Crescent, ActionDetail checks를 통과했다.
- `lobby-render-snapshots`는 clean baseline에서 여전히 FAIL한다. 이는 의도된 열린 이슈이며, 다음 구현은 clean baseline 위에서 로비/타이틀 route별 최소 Korean glyph/texture injection을 다시 설계해야 한다.
- clean baseline 동안 `data-center-title-uld`, `data-center-worldmap-uld`, `start-system-settings-uld`, `system-settings-mixed-scale-layouts`, `start-main-menu-phrase-layouts`는 기본 smoke check에서 제외한다. 새 injection route가 생기면 다시 기본 묶음에 넣는다.
- clean 클라이언트 자체가 로비 ASCII/숫자/영어에서 정상이라는 사용자 확인이 있으므로, alpha-bound `minGap` 절대값을 clean까지 고칠 대상으로 삼지 않는다. ASCII/숫자/영어 verifier는 patched가 clean보다 나빠졌는지 differential로 판단해야 한다.
- 2026-05-17 최신 사용자 확인: damage fly text는 현재 빌드 기준 고쳐진 것으로 취급한다. 이 영역은 보호 대상이며, Dalamud/font-only/ActionDetail 작업 중 `Jupiter_45/Jupiter_90` damage-number route나 `font3.tex` damage glyph neighborhood를 추가로 건드리지 않는다.
- 다음 우선순위 1: Dalamud/third-party overlay 한글 폰트 깨짐. 이전 `font-only` 변경을 diff/log로 전수조사하고, 실제 overlay가 쓰는 shared font route를 확인하기 전에는 font-only allowlist를 넓히지 않는다. strict `KrnAXIS_*` scope PASS는 overlay fix가 아니라 anti-contamination guard일 뿐이다.
- 다음 우선순위 2: ActionDetail/스킬 상세 폰트의 UI 배율별 상대 크기 이상. 기존 `action-detail-scale-layouts` PASS는 충분하지 않으므로, `다리 쳐내기`, `피의 갈증`, `즉시 발동`, `초`, 숫자 baseline을 100/150/200/300%별로 비교하는 검증을 먼저 실패시키고 수정한다.
- 커버리지 작업은 사용자가 직접 지적한 문구/글자만 찍어서 고치는 방식으로 완료 처리하지 않는다. 동일 font route를 쓰는 다른 UI 위치와 관련 sheet-derived Hangul까지 검증해야 하며, 보고된 문구가 정상이어도 같은 route의 미보고 문구가 깨질 수 있으면 미완료로 둔다.
- shared font coverage는 한국 원본 시트만 기준으로 수집하지 않는다. 반드시 생성된 target output 텍스트와 clean/global fallback을 기준으로 수집해, 실제 target에서 쓰이지 않는 한국 source 글자가 shared FDT/atlas에 들어가 다른 UI나 third-party route를 오염시키지 않게 한다.
- 2026-05-17 clean-context rule: 이전부터 결과가 좋았던 기준은 "가능한 clean/origin과 동일하게 만든다"였다. 다음 세션도 이 원칙을 기본값으로 둔다. glyph route, atlas cell, texture neighborhood, metrics, kerning은 clean/origin 또는 TTMP source와 동일하게 유지하고, 동일 경로가 실패한다는 verifier 증거가 있을 때만 좁게 예외를 둔다.
- 2026-05-17 ActionDetail high-scale note: clean/TTMP `TrumpGothic_68.fdt` Hangul route itself was too small for Korean high-scale labels, so the current exception keeps the target `TrumpGothic_68` line box and scales only Hangul alpha toward the target digit visual height. This is not permission to broadly recombine lobby or shared game fonts.
- Latest large UI verifier: `.tmp\large-ui-visual-scale-ja-r10` is rejected because the old check was height-only and missed width/advance. `.tmp\large-ui-biaxial-scale-ja-r1.focused-verifier-r3.log` passes `action-detail-scale-layouts,reported-ingame-hangul-phrases,third-party-game-font-safety,combat-flytext-damage-glyphs`; `.tmp\large-ui-biaxial-scale-ja-r1.regression-verifier-r2.log` passes the broader lobby/in-game regression set. Still do not mark the live ActionDetail/large UI size issue fixed until user confirmation.
- Rejected lobby path: the `_lobby.fdt` large UI visual-scale/reuse experiment was removed. Do not re-add `_lobby` entries to `LargeUiLabelVisualScaleFonts`, `TryReuseFallbackLobbyHangulAllocation`, or existing-region atlas reservation without a new texture-page design and a failing-then-passing verifier.
- 2026-05-23 party-list/chat/instance low-scale marker guard: `U+E031`, `U+E037`, `U+E0B1`-`U+E0B8`, and `U+E0E1`-`U+E0E8` are required seed glyphs, but the generator/verifier must also auto-collect clean/source PUA glyphs that already exist in each patched protected font route. All collected PUA glyphs must use isolated clean cells with 8px base/mip texture neighborhoods. Do not reuse dirty target cells for these glyphs. `U+E0B1`-`U+E0B8` are the legacy/circled marker class and must not be remapped into the self-marker shape.
- 2026-05-23 lobby crash/scale guard: `font_lobby7.tex`, lobby `image_index >= 24`, clean-unused page reuse, texture resize, and bottom-edge placement are rejected for the non-4K UI-resolution crash path. Current route-scoped output `.tmp\lobby-route-scoped-ja-r1` reclaims only clean Japanese/CJK cells inside the target FDT clean page set, keeps active verifier routes to `AXIS_12/14/18/36_lobby` and `TrumpGothic_23/34_lobby`, and passes focused lobby plus confirmed in-game regressions. This is not live-confirmed; do not mark the boot crash or all 150%+ lobby visual routes fixed until user confirmation.
- 2026-05-23 lobby runtime kerning guard: do not add synthetic Hangul kerning entries to `_lobby.fdt` files. `lobby-runtime-font-safety` rejects UTF-8-only Hangul kerning entries with `0000` Shift-JIS fallback columns, plus invalid kerning offsets and patched Hangul atlas-cell overlaps. Fresh `.tmp\lobby-runtime-kerning-safe-ja-r1` passes this guard; old `.tmp\pvp-profile-cropped-ja-r4` fails it. Keep this check beside `lobby-multitexture-font-set` and `lobby-texture-cell-margin` for boot-crash work.
- 2026-05-23 PvP profile guard: the old source-preserved `Jupiter_16/20` PvP path is rejected because it made live labels too large. Keep PvP ULD font/render bytes clean, and if touching PvP profile size use only the route-scoped visible-pixel crop/downscale for `Jupiter_16.fdt` and `Jupiter_20.fdt`. `pvp-profile-font-routes` must fail old oversized output and pass with `combat-flytext-damage-glyphs`, `third-party-game-font-safety`, `action-detail-scale-layouts`, and `party-list-self-marker` before reporting progress.
- Required regression after shared font, clean ASCII, Hangul texture, party-list, or UI-scale font edits: `party-list-self-marker,ingame-clean-ascii-glyphs,third-party-game-font-safety,combat-flytext-damage-glyphs`. Use local restore-baseline/orig clean sources, not the patched game folder as clean input.
- 2026-05-23 lobby allocation guard: do not reintroduce low-scale lobby `TryGetBySourceCodepoint` allocation reuse. It can share one atlas cell across different glyph dimensions and caused out-of-bounds or overlapped `MiedingerMid_*_lobby` cells. Low-scale lobby sharing is allowed only through exact allocation-key equality; otherwise allocate an isolated cell in existing `font_lobby1.tex` through `font_lobby6.tex`.
- 2026-05-23 direct source-cell guard: whenever a clean-empty lobby source cell is used directly, mark that rectangle occupied in `FontGlyphRepairContext` before any later allocation can reuse it. The verifier group `lobby-runtime-font-safety,lobby-multitexture-font-set,lobby-texture-cell-margin` must stay beside any lobby font work.
- 2026-05-23 start-menu route guard: `ui/uld/Title_Menu.uld` node `0x1D8` routes to `common/font/MiedingerMid_18_lobby.fdt`; `start-main-menu-phrase-layouts` must validate this live route, not broad non-live candidate fonts.
- 2026-05-24 lobby AXIS visual-scale guard: do not add `AXIS_12_lobby`, `AXIS_14_lobby`, `AXIS_18_lobby`, or `AXIS_36_lobby` back to `LobbyLargeLabelVisualScaleFonts`. Those fonts must be checked as exact source-preserved routes in `lobby-scale-font-sources`; allowing them as visual-scaled routes masked the reported lobby system-settings size problem. Fresh `.tmp\lobby-axis-visual-clean-ja-r1` passes runtime/multitexture/margin/source safety plus in-game regressions, but still has open visual failures for `TrumpGothic_23_lobby` advance and AXIS lobby `minGap=-1`.
- 2026-05-24 lobby TrumpGothic advance guard: do not reintroduce the visual-scale `replacementWidth + 2` minimum advance clamp for `TrumpGothic_23_lobby`. It made lobby/character-select Korean labels too widely spaced. Keep the advance source-ratio based and verify with `lobby-large-label-scale-layouts`; `.tmp\lobby-trump23-advance-ja-r1` clears the TrumpGothic advance checks and keeps runtime/multitexture/margin/source plus in-game regressions passing. Remaining AXIS `minGap=-1` failures must be solved without synthetic `_lobby.fdt` Hangul kerning.
- 2026-05-24 lobby AXIS advance guard: for `AXIS_12_lobby`, `AXIS_14_lobby`, and `AXIS_18_lobby`, use route-derived per-left-glyph `OffsetX` advance compensation for reported start-screen/system-settings phrases instead of synthetic Hangul kerning. Do not re-add `_lobby.fdt` Hangul kerning entries, visual-scale routing, page7, image-index expansion, texture resize, bottom-edge placement, or source-codepoint cell reuse. Current output `.tmp\lobby-axis-advance-comp-ja-r1` passes `lobby-large-label-scale-layouts,lobby-render-snapshots,lobby-scale-font-sources,lobby-runtime-font-safety,lobby-multitexture-font-set,lobby-texture-cell-margin,start-main-menu-phrase-layouts` plus the in-game regression set.
- 2026-05-24 4K crash guard: for font-size, low-scale, or boss-spawn crash reports, run `font-runtime-glyph-bounds` with a local restore-baseline `-BaseIndexDirectory`, not an installed game folder as clean input. Pair it with `ingame-font-risk-survey,ingame-clean-ascii-glyphs,numeric-glyphs,party-list-self-marker,combat-flytext-damage-glyphs,third-party-game-font-safety`. The bounds check validates FDT texture routes, glyph rectangles, kerning offsets, and mip raw-data ranges for the broad font set.
- 2026-05-24 MiedingerMid clean ASCII guard: non-lobby `MiedingerMid_10/12/14/18/36.fdt` keeps Korean-capable Hangul from the font pack, but ASCII/number/symbol glyphs must be restored from clean global. `numeric-glyphs` and `ingame-clean-ascii-glyphs` must include these fonts so small-label and low-size mip regressions do not pass silently.
- 2026-05-30 4K lobby coverage update: the previous broad `4k-lobby-font-derivations` caveat is superseded for current `.tmp\lobby-highscale-ascii-all-ja-r1`; the check passes with `static=144`, `addon-derived=136`, `total=280`, including high-scale lobby candidates such as `Jupiter_46_lobby.fdt`. Keep crash-class runtime checks separate, but do not treat the old missing-Hangul caveat as current-output evidence.
- 2026-05-24 lobby high-scale coverage guard: do not restore `new uint[0]` high-scale coverage or system-only `AXIS_12/14/18_lobby` coverage. Those routes caused remaining `-`/`=` fallback glyphs in lobby and character-select.
- 2026-05-24 lobby high-scale capacity guard: naive full 907-glyph coverage in every high-scale lobby font is rejected because it overflows safe atlas capacity. Use `LobbyHangulCoverage.HighScaleRows` plus route-known phrases instead.
- 2026-05-24 lobby high-scale reuse guard: high-scale allocation reuse is allowed only for exact `AXIS_36.fdt` source allocations in existing `font_lobby1..6` pages, with `image_index < 24` and a cell large enough for the target glyph. Do not use `font_lobby7.tex`, image index 24+, texture resize, bottom-edge placement, or source-codepoint reuse to regain coverage.
- 2026-05-27 HD/non-4K startup crash guard: startup/runtime-limited small lobby fonts (`AXIS_12/14/18_lobby`, `MiedingerMid_12/14/18_lobby`) must not place Hangul on `font_lobby4/5/6` (`image_index >= 12`). The crash logs point at `AtkFontAnalyzerRenderer` with later lobby texture slots missing for the startup small-font renderer path. Keep this as a narrow route guard; large-label and high-scale lobby fonts still use six clean lobby pages unless a verifier proves the route is unsafe. If coverage no longer fits, use startup-safe shared clean CJK reclaim cells inside `font_lobby1..3`, not reduced sheet coverage.
- 2026-05-28 HD/non-4K packed texture guard: the later 2026-05-27 crash proved the texture-slot guard was incomplete. Patched font textures must not be repacked as a single texture locator. Keep `PatchPackedFontTexture` aligned with the SqPack 16KB texture-block layout (`blockCount == ceil(textureDataSize / 16000)`, one sub-block per locator), and keep the packed layout checks inside `font-runtime-glyph-bounds`. A bounds-only raw texture pass is not enough because the client can use texture block locators differently when the UI resolution is HD/FHD/WQHD instead of 4K.
- 2026-05-29 HD/non-4K AXIS_12_lobby page2 guard: the 2026-05-28 crash log has `R10=2`/`R11=0` in `AtkFontAnalyzerRenderer`. Treat patched Hangul in `common/font/AXIS_12_lobby.fdt` on `font_lobby3.tex` (`image_index >= 8`) as unsafe for HD startup. Keep the generator's `AXIS_12_lobby` candidates to `font_lobby2,font_lobby1`, and keep `lobby-runtime-font-safety` failing old outputs that still route patched Hangul to `font_lobby3`. This is narrower than a global clean-page restriction and should not be expanded to large-label/high-scale lobby fonts without a failing live log and passing coverage proof.

- 2026-05-29 r33 release baseline: `Release\Public\FFXIVKoreanPatch.exe` from commit `1847c23` has SHA256 `93E628E63E5803B1993B4F60FDEA46C09E94251CF9C60B40E42BFB6B32462BCC`. Its fresh release-generator output `.tmp\release-hd-crash-focus-ja` passes the focused HD crash/coverage checks and `ingame-critical`; the current verifier rejects the old `20260528-003930` output on the expected `AXIS_12_lobby -> font_lobby3.tex` route. `Scripts\check-hd-crash-logs.ps1` reports no `%APPDATA%\XIVLauncher\dalamud_appcrash_*.log` newer than this release and summarizes the latest matching register values. Do not call the live crash closed until a client run confirms it.

- 2026-05-29 HD crash release gate: use `Scripts\verify-hd-crash-release.ps1` before reporting this issue. The short mode can reuse `.tmp\release-hd-crash-focus-ja` with `-SkipGenerate`; add `-RunInGameCritical` for the long in-game regression sweep. Current `-SkipGenerate -RunInGameCritical` passes focused HD crash checks, `ingame-critical`, known-bad `20260528-003930` rejection, and the post-release crash-log check. Full regeneration mode is still available by omitting `-SkipGenerate`.

- 2026-05-29 full-generation gate evidence: full `Scripts\verify-hd-crash-release.ps1` regenerated `.tmp\release-hd-crash-gate-ja` and passed focused HD crash checks, known-bad `20260528-003930` rejection, and the crash-log check. Current full-generation logs are `.tmp\applied-route-verification-logs\20260529-224528-generator.log` and `.tmp\applied-route-verification-logs\20260529-224528-verifier.log`.

- 2026-05-29 current release rebuild evidence: after commit `7fcf572`, `Scripts\build-release.ps1` rebuilt `Release\Public\FFXIVKoreanPatch.exe` at `2026-05-29 22:51:37`; SHA256 remains `93E628E63E5803B1993B4F60FDEA46C09E94251CF9C60B40E42BFB6B32462BCC`. Full `Scripts\verify-hd-crash-release.ps1` regenerated `.tmp\release-hd-crash-head-ja` and passed focused HD crash checks, known-bad rejection, and the post-release crash-log check. A follow-up `-SkipGenerate -RunInGameCritical` gate on `.tmp\release-hd-crash-head-ja` also passes, including `ingame-critical`. Current full-generation logs are `.tmp\applied-route-verification-logs\20260529-225238-generator.log` and `.tmp\applied-route-verification-logs\20260529-225238-verifier.log`.
- 2026-05-29 r34 post-release crash correction: new live log `dalamud_appcrash_20260529_231833_830_30492.log` has the same `AtkFontAnalyzerRenderer`/`R10=2` pattern but `RCX=0`. The r33 verifier was too narrow: it only rejected `AXIS_12_lobby -> font_lobby3.tex`, while the r33 output still had `AXIS_14_lobby` patched Hangul on `font_lobby3.tex`. Keep all startup/runtime-limited small lobby fonts (`AXIS_12/14/18_lobby`, `MiedingerMid_12/14/18_lobby`) off page2 (`image_index >= 8`) for patched Hangul. Fresh `.tmp\hd-startup-page2-small-font-safe-ja-r1` passes the focused HD crash/coverage checks and `ingame-critical`; do not mark the live crash closed until a post-r34 client run has no new matching crash log.
- 2026-05-29 r34 release evidence: `Release\Public\FFXIVKoreanPatch.exe` rebuilt at `2026-05-29 23:36:15`, SHA256 `3EDA5DEA56CE9097841BAE88543778B506DB2A5E678B135C781B2B981CF4BBF2`. The release gate passed with `-BuildRelease -SkipGenerate -RunInGameCritical -FailOnAnyNewCrash`; crash-log check sees `dalamud_appcrash_20260529_231833_830_30492.log` as older than the new build.
- 2026-05-29 r34 full-generation evidence: current HEAD `da4db5c` regenerated `.tmp\release-hd-crash-r34-ja` via `Scripts\verify-hd-crash-release.ps1` without `-SkipGenerate` and with `-RunInGameCritical -FailOnAnyNewCrash`. Focused HD crash/coverage, `ingame-critical`, known-bad rejection, and crash-log checks all pass. Logs are `.tmp\applied-route-verification-logs\20260529-234613-generator.log` and `.tmp\applied-route-verification-logs\20260529-234613-verifier.log`.
- 2026-05-30 crashhandler-only rejection: after the r34 release, no newer `dalamud_appcrash_*.log` exists, but the latest `dalamud.crashhandler.log` session has `2026-05-30 00:29:51 Failed to read exception information; error: 0x6d` and target-process termination. `Scripts\check-hd-crash-logs.ps1` must treat that as a failed post-release crash signal instead of passing just because no appcrash file exists.
- 2026-05-30 applied-state guard: `Scripts\verify-hd-crash-release.ps1` supports `-AppliedGame`. Use it when checking a release already applied to the game folder, so stale `.tmp` outputs are not compared against the installed client by mistake. The installed `D:\SquareEnix\FINAL FANTASY XIV - A Realm Reborn\game` matched `%LOCALAPPDATA%\FFXIVKoreanPatch\generated-release\ja\2026.05.01.0000.0000\20260530-002104`; that output passed the r34 focused HD font checks before the all-lobby page2 guard below. Future crash fixes still need either a new native stack or a verifier that fails the exact applied output before changing font placement.
- 2026-05-30 all-lobby page2 guard: the verifier now treats `font_lobby3.tex` (`image_index=8..11`) as unsafe for patched Hangul in any `_lobby.fdt`, because the repeated native evidence is `AtkFontAnalyzerRenderer` with `R10=2`. Do not over-broaden this to `font_lobby4..6` without a new stack; the fresh passing route keeps those pages available for coverage. Generator candidate order must exclude `font_lobby3` for patched lobby Hangul and `Scripts\verify-hd-crash-release.ps1` must reject the known-bad `20260530-002104` output by default.
- 2026-05-29 r34 known-bad gate rule: `Scripts\verify-hd-crash-release.ps1` checks multiple known-bad outputs and must reject both the historical `20260528-003930` output and the r33 post-release `.tmp\release-hd-crash-head-ja` output when present. Use `-AdditionalFailedOutput` to add future rejected outputs instead of replacing the default regression set.
- 2026-05-24 character-select large-label guard: `TrumpGothic_34_lobby.fdt` character-select coverage is large-label-only (`ClassJob`, `Race`, `Tribe`, `GuardianDeity` labels). Do not require full character-select sheet coverage for that font unless route survey proves it renders those rows.
- 2026-05-24 verifier pass semantics: a focused subset PASS is not a fix signal. Lobby/font completion requires `lobby-critical` or `font-critical`, which expands to the route, coverage, high-scale source, render, runtime, texture, and in-game regression checks. The earlier `.tmp\lobby-highscale-route-coverage-ja-r2` focused PASS is invalid as completion evidence because the broader lobby checks fail on the same output.
- 2026-05-24 lobby-critical r6: `.tmp\lobby-critical-fix-ja-r6` passes `lobby-critical`; the same output also passes `ingame-critical`. This is code-level verification only, not live user confirmation.
- 2026-05-24 high-scale lobby source rule: generator and verifier must collect lobby coverage from the patched output text plus clean/global fallback, not directly from the Korean source client. FDT source lookup must canonicalize glyph entries through UTF-8 first and Shift-JIS fallback second; otherwise source glyphs that verifier can find may be skipped by the generator.
- 2026-05-24 high-scale lobby coverage rule: `LobbyHangulCoverage.HighScaleRows` must stay aligned with the verifier's scale-sensitive collection: route-known phrases, Addon `4000-4200` and `8683-8722`, and full `ClassJob`, `Race`, `Tribe`, `GuardianDeity` sheets. Narrowing these rows can reintroduce `-`/`=` fallback glyphs such as missing U+ACA8.
- 2026-05-24 high-scale lobby ASCII rule: do not inject full ASCII into every high-scale lobby font. That can exhaust safe atlas space, especially `Meidinger_40_lobby.fdt`. Add phrase-derived ASCII only from the same phrase set rendered by `4k-lobby-phrase-layouts`, and add clean numeric-boundary kerning for digit-to-`%` and digit-to-`x` pairs when fallback ASCII glyphs are inserted.
- 2026-05-24 clean ASCII guard: non-lobby `TrumpGothic_34.fdt`, `TrumpGothic_68.fdt`, and `TrumpGothic_184.fdt` must restore clean ASCII glyphs and kerning. `ingame-critical` must be rerun afterward because these fonts overlap shared in-game UI/flytext risk surfaces.
- 2026-05-24 live rejection of `lobby-critical` r6: release `ee2b8cc` still shows oversized/missing-glyph lobby `시스템 설정` at 150%, undersized lobby `시스템 설정` at 200%+, `=`/`-` in character select above 100%, and PvP profile `크리스탈라인 ...` abnormal only at 100%. Treat the r6 `lobby-critical` PASS as insufficient. The next verifier must model live scale-specific ULD font routing and must fail the r6 output before a new fix is trusted.
- 2026-05-25 runtime-scale route guard: keep `lobby-runtime-scale-font-routes` inside `lobby-critical`. It must fail the `20260524-225140` release output before a new lobby scale fix is trusted; that output fails because `TrumpGothic_34_lobby.fdt` lacks `U+D15C` for `시스템 설정`.
- 2026-05-25 `TrumpGothic_34_lobby` guard: do not narrow this font to character-select-only coverage. 150% lobby system-settings can route through it, so it needs `LargeLabels` coverage and visual-scale coverage. `TrumpGothic_23_lobby` keeps `SystemAndCharacter` coverage.
- 2026-05-30 PvP 100% guard update: `Jupiter_16.fdt` is back in `PvpProfileVisualScaleGlyphs` because the strengthened `pvp-profile-font-routes` fails the previous source-preserved output on the reported 100%-only oversized route (`크리스탈라인 컨플릭트` `Hangul/digit=1.729`) and passes fresh `.tmp\pvp-jupiter16-ja-r1` (`1.333`). Keep PvP ULD font/render bytes clean; only the route-scoped `Jupiter_16/20` glyph cells may be visually cropped/downscaled.
- 2026-05-25 font-only apply guard: strict `font-only` is not compatible with leaving full-patch `0a0000/060000` text/UI installed. If the user applies "한글 폰트 패치" over a full patch, restore clean `0a0000.win32.index/index2` and `060000.win32.index/index2` before copying the `000000` font files, include those indexes in the backup, and validate they are clean after apply.
- 2026-05-25 mixed-state verifier guard: do not treat `font-runtime-glyph-bounds` PASS alone as an HD UI crash fix. It can pass on a font-only output even when Korean lobby/character text remains installed from a previous full patch. Always pair it with a check that the applied state is not mixed, or run `lobby-critical` against the actual applied state.
- 2026-05-30 start-screen glyph variant guard: do not fix HD start-screen system-settings spacing by globally editing non-lobby `AXIS_12/14/18` Hangul glyph metrics. That breaks unrelated source-preserved routes. Use `StartScreenGlyphVariants` PUA aliases scoped to approved Addon rows only, and keep `start-screen-glyph-variants,lobby-render-snapshots,third-party-game-font-safety,action-detail-scale-layouts,pvp-profile-font-routes` in the regression set.
- 2026-05-30 start-screen alias verification: if `start-screen-glyph-variants` reports PUA aliases outside `Addon` 4000-4200 or 8683-8722, or if approved rows still contain risky unaliased phrases, the output is invalid. PUA aliases should map visually back to the same Hangul through FDT alias glyphs; verifier/source comparisons must normalize aliases before comparing source text.
- 2026-05-30 source-preservation guard: full `hangul-source-preservation` must keep passing with the PvP visual-scale exception modeled as the route-scoped `PvpProfileVisualScaleGlyphs` codepoint set only. Do not replace it with a blanket Jupiter-font skip; run it with `pvp-profile-font-routes`, `third-party-game-font-safety`, and `ingame-ttmp-texture-neighborhoods` when touching this area.
- 2026-05-30 applied-state guard: if `verify-hd-crash-release.ps1 -AppliedGame` fails because installed game files differ from the current generated output, do not treat that live/applied state as evidence against the current code path. Re-establish an applied state that passes `applied-output-files`/`applied-lobby-routes` first, especially for `TrumpGothic_23_lobby.fdt` and `font_lobby3.tex` routes.
- 2026-05-30 high-scale lobby ASCII guard: `4k-lobby-phrase-layouts` and the generator must use the same phrase-derived ASCII source. Use `LobbyScaledHangulPhrases.All`, not a narrower high-resolution-only subset, so mixed phrases like `데이터 센터 Mana에 접속 중입니다.` do not lose `M` in high-scale lobby fonts.
- 2026-05-30 reported-ingame/PvP verifier guard: `reported-ingame-hangul-phrases` may accept bounded `PvpProfileVisualScaleGlyphs` changes only on `Jupiter_16.fdt`/`Jupiter_20.fdt`. Keep `hangul-source-preservation` strict so this cannot become a blanket Jupiter skip.
- 2026-05-30 release-gate pass semantics: the default `verify-hd-crash-release.ps1` focused checks are no longer only crash-runtime checks. They must include the current repeated live-regression guards: `start-screen-glyph-variants`, `lobby-render-snapshots`, `action-detail-scale-layouts`, `pvp-profile-font-routes`, `combat-flytext-damage-glyphs`, `third-party-game-font-safety`, and `party-list-self-marker`. `lobby-critical` and `ingame-critical` also include the current broad guards, so do not use older narrow PASS logs as completion evidence.
- 2026-05-30 visual evidence guard: when lobby/system-settings, ActionDetail large-label, or PvP profile visual quality is part of the report, run `verify-hd-crash-release.ps1 -DumpVisualSnapshots` or direct `lobby-render-snapshots,action-detail-scale-layouts,pvp-profile-font-routes -GlyphDumpDir ...` checks and inspect the generated PNGs. If checking an installed client, include `-AppliedGame` so the PNGs come from the applied game files. A text-only PASS is weaker evidence for the repeated blur/spacing/scale reports.
- 2026-05-30 current installed-state guard: the real installed `D:\SquareEnix\FINAL FANTASY XIV - A Realm Reborn\game` was observed not matching current `.tmp\lobby-highscale-ascii-all-ja-r1` font files. Before using installed-client evidence, apply the current generated output or otherwise prove `applied-output-files` passes.

- 2026-05-31 start-screen PUA alias guard: do not return to copying the same small `AXIS_12/14/18` glyph entry for `StartScreenGlyphVariants`. High-scale system-settings aliases must use high-scale source cells (`AXIS_12/14 -> AXIS_18`, `AXIS_18 -> AXIS_36`) with route-scoped texture allocation and runtime advance compensation. Keep `start-screen-glyph-variants`, `lobby-render-snapshots`, `lobby-critical`, and `verify-hd-crash-release.ps1 -RunFullFontObjective` in the regression set.
- 2026-05-31 applied-state evidence: `.tmp\start-variant-visual-alias-ja-r3` was applied to `D:\SquareEnix\FINAL FANTASY XIV - A Realm Reborn\game` after confirming no FFXIV/XIVLauncher processes were running. Backup is `.tmp\applied-route-backups\20260531-013525`. The applied gate passed `applied-output-files`, `applied-lobby-routes`, `lobby-runtime-font-safety`, and `font-runtime-glyph-bounds`; if a later client report differs, first rerun `verify-hd-crash-release.ps1 -SkipGenerate -AppliedGame` to rule out installed-state drift.
- 2026-05-31 applied full visual gate evidence: the installed game folder also passed `verify-hd-crash-release.ps1 -SkipGenerate -AppliedGame ... -RunFullFontObjective -DumpVisualSnapshots -FailOnAnyNewCrash`. Use `.tmp\hd-crash-release-gate-logs\20260531-015441-visual-snapshots` as the current PNG evidence set for lobby settings 150/200/300%, character-select large labels, ActionDetail `즉시 발동`/`초`, and PvP profile route checks.
