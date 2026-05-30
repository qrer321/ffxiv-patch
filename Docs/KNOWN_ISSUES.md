# 현재 미해결 항목과 추가 작업 목록

## Future Feature: Base-Language Patch Profiles

- Requested: 2026-05-22.
- This is a future feature candidate only. Do not change current patch behavior until the scope is explicitly approved.
- Candidate `common-base` profile: preserve common/system boilerplate text in the selected base client language while keeping the normal full Korean patch for the rest.
- Candidate `story-only` profile: preserve non-story UI/system/combat/gameplay text in the selected base client language and patch only story, quest, and dialogue-related text to Korean.
- Implementation must be policy-driven by sheet/row/column groups, not by one-off phrase hardcoding.
- Required verifier before enabling:
  - preserved rows match local clean/global base language data for the selected target language;
  - story-only output leaves non-story sheets in base language and applies Korean only to approved story/dialogue/quest scopes;
  - SeString control codes, icons, links, and macro tokens are byte-preserved where text is preserved;
  - `ja` and `en` base-client outputs are checked separately.
- Do not implement or expose this profile as a UI/CLI option until the policy scope and verifier are ready.

## 2026-05-22 Low-Scale Party Number Marker Contamination

- Report: in-game UI scale 120% or below can show polluted pixels around party-list/chat party number markers. The reported visible marker can be `1` or another party number.
- Scope: this is the shared marker PUA route, not a one-off ASCII digit fix. Seed glyphs `U+E031`, `U+E037`, `U+E0B1`-`U+E0B8`, and `U+E0E1`-`U+E0E8` are always guarded across `AXIS_*` and `KrnAXIS_*`. In addition, each route now auto-collects clean/source PUA glyphs that already exist in the patched target FDT. `U+E0B1`-`U+E0B8` covers the reported zone/instance circled-number marker class and must be preserved as its own clean glyphs, not remapped into the self-marker shape.
- Implementation rule: use isolated clean PUA cells with 8px base/mip texture neighborhoods. Do not reuse dirty target cells, and do not let protected Hangul texture restores overwrite these clean PUA patches. Do not grow this by manually adding one reported glyph at a time unless it is a required seed; prefer route-local auto collection.
- Verification rule: run `party-list-self-marker` after any shared font/texture work. It must compare glyph pixels plus base and mip texture neighborhoods against local restore-baseline/orig clean sources for the collected PUA route set. Current code-level passing outputs: `.tmp\auto-pua-protect-ja-r1` and `.tmp\auto-pua-regression-ja-r1`.
- Live status: user confirmed the low-scale party-list/chat number marker and zone/instance circled-number contamination are fixed in-client on 2026-05-23. Keep `party-list-self-marker` as a required shared-font regression guard.

## 2026-05-17 Shared Font Correction

- Damage fly text is user-confirmed fixed for the current full patch. Treat `Jupiter_45.fdt`, `Jupiter_90.fdt`, and their `font3.tex` damage-number base/mip neighborhoods as protected.
- The latest Dalamud/third-party overlay break was caused by full `--include-font` shared game-font edits, not by a confirmed safe `font-only` path. The bad apply touched broad `000000/common/font` FDTs and atlases; clean ASCII/damage repair then overwrote TTMP Hangul-owned shared atlas cells, especially `font3.tex` cells used by `TrumpGothic_68.fdt`.
- The stale `.tmp\font-only-dalamud-axis-ja-r4` direction is rejected. `font-only` is back to strict `KrnAXIS_120/140/180/360.fdt` plus `font_krn_1.tex`; do not add AXIS/Jupiter/TrumpGothic/Miedinger/Meidinger/font1/font2/font3 to font-only without a route-specific verifier proving safety.
- Current code-level generated check `.tmp\thirdparty-safe-ja-r7` passes `third-party-game-font-safety` (`fonts=14`, `glyphs=159889`) plus combat damage, in-game clean ASCII, TTMP Hangul neighborhood, reported Hangul phrase, and ActionDetail checks. The user confirmed the live Dalamud/third-party Korean font break is fixed on 2026-05-17.

## 2026-05-17 Quest Say Anonymization Disabled

- Quest say anonymization does not cover every relevant sheet yet, so it is disabled before it can create partial or inconsistent quest text patches.
- The implementation remains in `QuestChatPhraseAnonymizer`, but `QuestChatPhraseAnonymizationFeature.Enabled` is false. The CLI flag is accepted as no-op with a warning, and the UI no longer appends `--anonymize-quest-chat-phrases` automatically for full text patches.
- Do not re-enable this feature until a sheet/row coverage verifier proves all required say quest prompt/input sheets are covered.

## 2026-05-17 Large UI Label Font Scope

- The `즉시 발동`/`초` report is a symptom of a shared large UI label font route, not a request to patch only those words. Do not fix only user-reported phrases/chars and mark the issue done.
- The verifier and generator must cover the font route plus sheet-derived UI label Hangul that can use it. Include representative labels such as `임무 찾기`, `퀘스트`, and `시스템 설정`, but also collect broader UI label coverage from the relevant sheets so unreported labels do not regress elsewhere.
- Coverage fixes must be route/sheet scoped, not symptom-string scoped. A build is not considered fixed just because the exact phrase the user reported renders correctly; the same font route must be checked for other UI locations before reporting progress.
- Coverage collection must use the patched target output text plus clean/global fallback for untouched entries, not raw Korean source text alone. Raw Korean source can contain unused rows/chars; adding those to a shared font route can contaminate unrelated UI/third-party routes even when the reported phrase itself renders.
- Any fix in this area must keep damage fly text and confirmed third-party shared-font routes protected.
- 2026-05-18 r16 strict lobby capacity guard: added `lobby-full-coverage-capacity` as a failing gate for the concern that safe phrase coverage will keep missing unreported lobby text. Current `.tmp\lobby-large-label-expanded-ja-r2` intentionally fails this strict check with `target_fonts=18`, `required=16200`, `missing_target=1386`, `source_covered=1386`, and `aggregate_allocation_failures=1081`. This means broad lobby coverage does not fit the current `font_lobby` pages; do not expand phrase/sheet coverage further until a texture-page split or a complete lobby font set is implemented. The existing focused regression set still passes on the same output.
- 2026-05-18 r17 page-split sizing: added `lobby-page-split-capacity` to simulate full lobby coverage with extra blank `font_lobby` pages. Current `.tmp\lobby-large-label-expanded-ja-r2` has 6 existing lobby texture pages and 1081 allocation failures with no extra page, but reaches 0 allocation failures with one additional synthetic 1024x1024 page. This proves the next blocker is not coverage trimming; it is safely adding/referencing a real extra lobby texture page such as `font_lobby7.tex`.
- 2026-05-19 r18 live crash rejection: the `font_lobby7.tex` implementation is rejected because the user reported a UI-scale-change crash after applying it, while generated-vs-applied verification matched. Added lobby texture pages and lobby FDT `image_index >= 24` must be treated as runtime-unsafe. The next safe release must use only clean lobby pages `font_lobby1.tex` through `font_lobby6.tex`, even if that means the full-sheet lobby coverage problem remains open.
- 2026-05-20 r19 six-page coverage correction: full lobby coverage now fits the clean six lobby texture pages by reducing allocator waste, not by adding a page. The allocator uses dense cursor-backed placement for fragmented space and the large-label visual-scale route shares identical glyph cells across compatible targets. `.tmp\lobby-dense-sixpage-ja-r3` passes full coverage, visible glyph, six-page texture-route, lobby scale, third-party font, combat fly-text, and in-game clean ASCII checks. Keep live status pending until the user confirms the client result.
- 2026-05-20 r20 lobby large-label no-shrink: the user confirmed the UI-scale crash is gone, then reported that 로비 `시스템 설정` and character-select labels such as `종족 및 성별` still look small. The previous verifier allowed `TrumpGothic_23_lobby/34_lobby` Hangul at about `0.88x` of the `AXIS_18_lobby` source route. `.tmp\lobby-large-label-noshrink-ja-r3` passed code-level checks but is not sufficient for the live report.
- 2026-05-20 r21 lobby large-label subset scale: do not scale all 907 `TrumpGothic_23_lobby/34_lobby` glyphs; that starves high-scale lobby fonts. The current safe attempt keeps 907-glyph coverage unscaled and applies `1.08x` only to the large-label subset. `.tmp\lobby-large-label-min108-subset-ja-r1` passes `lobby-large-label-scale-layouts,lobby-scale-font-sources,lobby-full-coverage-capacity,pvp-profile-font-routes,third-party-game-font-safety,combat-flytext-damage-glyphs,action-detail-scale-layouts` from local restore-baseline. Await live confirmation.
- 2026-05-20 PvP profile follow-up: the `PvPCharacter/PvPAction/PvPTeam` AXIS-to-Jupiter ULD route remap made the live PvP profile/Crystalline Conflict font thin and small. That direction is rejected and removed. Keep PvP ULD font/render state byte-for-byte clean unless a future route-scoped verifier and user confirmation prove otherwise.
- 2026-05-23 r22 lobby scale-source correction: route survey showed start-screen system settings uses `TrumpGothic_23_lobby`, while the character-select large labels can route through `TrumpGothic_34_lobby`. `TrumpGothic_34_lobby` now uses `TrumpGothic_34.fdt` as its visual source and only the character-label subset; do not feed system-settings text through this route. The allocator bottom-row off-by-one was corrected. Fresh output `.tmp\lobby-runtime-scale-source-ja-r5` passes `lobby-full-coverage-capacity,lobby-multitexture-font-set,lobby-coverage-glyphs,lobby-large-label-scale-layouts,lobby-scale-font-sources,start-system-settings-uld,pvp-profile-font-routes` and the in-game regression set `combat-flytext-damage-glyphs,action-detail-scale-layouts,party-list-self-marker,ingame-clean-ascii-glyphs,third-party-game-font-safety`.
- 2026-05-23 r22 caveat: the generated r5 output still reports 4 high-scale lobby fallback allocations per high-scale lobby font. These are not missing glyphs and broad coverage passes, but this is a degraded crash-safe six-page fallback, not a user-confirmed live visual fix. Do not report the lobby large-label issue as fixed until the user confirms the client result. Do not reintroduce `font_lobby7.tex` or lobby `image_index >= 24`.
- 2026-05-23 r23 non-4K UI-resolution boot crash guard: user reported boot crash when the UI resolution setting is not 4K. Treat this separately from UI scale. The current code-level mitigation keeps newly allocated non-high-scale lobby glyph cells at least 1px away from the bottom texture edge, while high-scale lobby supplemental fonts keep the old bottom-edge allowance to avoid small fallback glyphs. Supplemental high-scale lobby FDTs are generated before regular lobby payload processing so their large source glyphs do not fall back to `AXIS_18_lobby`. Fresh output `.tmp\lobby-uires-safe-ja-r6` passes `lobby-texture-cell-margin,lobby-multitexture-font-set,lobby-coverage-glyphs,lobby-scale-font-sources`, focused lobby/PvP/in-game regression checks, and `lobby-full-coverage-capacity,start-system-settings-uld`. This is still pending live confirmation; do not call the crash fixed until the user confirms. The clean-page-set mismatch in `lobby-multitexture-font-set` remains a WARN, not a release blocker, because enforcing it breaks full coverage.
- 2026-05-23 r24 non-4K UI-resolution boot crash rejection/correction: the user still reproduced the crash and provided a call stack through `AtkFontAnalyzerRender` / `AtkTextNodeRenderer.Draw`. The previous WARN-only clean-page verifier was wrong. `lobby-multitexture-font-set` now hard-fails when a patched lobby FDT references a lobby texture page that the clean same-name FDT never referenced. Allocation-cache reuse and high-scale fallback reuse must also stay inside the target FDT clean page set. Fresh output `.tmp\lobby-system-priority-ja-r1` passes `lobby-multitexture-font-set,lobby-texture-cell-margin` and confirmed in-game regressions, but still fails high-scale lobby coverage checks. Do not reintroduce `font_lobby7.tex`, lobby `image_index >= 24`, clean-unused page reuse, or partial high-scale glyph edits.
- 2026-05-23 r24 high-scale lobby coverage status: high-scale lobby FDTs are now transactional. If the complete requested high-scale glyph set does not fit inside the clean page set, the FDT/atlas/cache/texture edits are discarded instead of leaving partial large glyphs that can mix with fallback and show only part of a word at the high-scale size. This prevents the previous `Jupiter_46_lobby -> font_lobby6` clean-page violation, but leaves the 150%+ lobby/character-select visual issue open. The next approach must be route-level or clean-page-compatible; adding pages or accepting partial coverage is rejected.
- 2026-05-23 r25 route-scoped crash guard: current implementation reclaims clean Japanese/CJK lobby cells only inside each target FDT's own clean page set, skips `font_lobby7.tex`, and rejects bottom-edge placement for all lobby glyph allocations including high-scale routes. The active scale-source verifier is intentionally limited to ULD-observed/reported lobby routes (`AXIS_12_lobby`, `AXIS_14_lobby`, `AXIS_18_lobby`, `AXIS_36_lobby`, `TrumpGothic_23_lobby`, `TrumpGothic_34_lobby`) instead of patching unobserved high-scale fonts that previously caused partial/cross-page failures. Fresh output `.tmp\lobby-route-scoped-ja-r1` passes `lobby-multitexture-font-set,lobby-texture-cell-margin,lobby-render-snapshots,lobby-scale-font-sources` plus `combat-flytext-damage-glyphs,third-party-game-font-safety,party-list-self-marker,action-detail-scale-layouts,pvp-profile-font-routes`. This is still generated-output verification only; keep the non-4K boot crash and 150%+ lobby visual status open until live confirmation.
- 2026-05-23 r26 PvP profile overflow correction: preserving TTMP source scale for `Jupiter_16.fdt` and `Jupiter_20.fdt` made live PvP profile labels too large and able to overflow their ULD areas. The current route keeps PvP ULD font/render bytes clean, but applies a route-scoped visible-glyph crop/downscale only to those two Jupiter fonts. The verifier now fails the old source-preserved output `.tmp\lobby-route-scoped-ja-r1` (`Jupiter_16/20` ratios about `1.58..1.93`) and passes fresh `.tmp\pvp-profile-cropped-ja-r4` with `pvp-profile-font-routes,combat-flytext-damage-glyphs,third-party-game-font-safety`. Additional regression on the same output passes `action-detail-scale-layouts,party-list-self-marker,pvp-profile-font-routes,combat-flytext-damage-glyphs,third-party-game-font-safety`. This is code-level verification only; do not call the live PvP profile visual-size item closed until user confirmation.
- 2026-05-23 r27 non-4K UI-resolution boot crash reanalysis: the live call stack goes through `AtkFontAnalyzerRender` / `AtkTextNodeRenderer.Draw`, so the verifier now treats lobby FDT kerning as runtime-sensitive, not just glyph texture routing. Synthetic Hangul kerning in `_lobby.fdt` is rejected because Hangul has no Shift-JIS fallback and the generated kerning entries carried `0000` fallback columns. New `lobby-runtime-font-safety` fails `.tmp\pvp-profile-cropped-ja-r4` on those entries and passes fresh `.tmp\lobby-runtime-kerning-safe-ja-r1` after `ApplyStartScreenSystemSettingsKerning` skips lobby FDT paths. The same output passes `lobby-multitexture-font-set,lobby-texture-cell-margin,lobby-scale-font-sources,combat-flytext-damage-glyphs,third-party-game-font-safety,party-list-self-marker,action-detail-scale-layouts,pvp-profile-font-routes`. This is still code-level verification; do not mark the boot crash fixed until live confirmation.
- 2026-05-26 r31 verifier correction: the rejected release output passed broad FDT bounds but failed `start-system-settings-uld` on `common/font/AXIS_18.fdt` terminal punctuation spacing (`다.` minGap=-1). Do not fix this by changing AXIS glyph metrics or texture cells; that fails `third-party-game-font-safety` and can rebreak external overlay text. Current route keeps glyphs clean and adds only the route-derived terminal-punctuation kerning for `AXIS_18.fdt`, while Hangul-Hangul UTF-8-only kerning remains disallowed. Fresh output `.tmp\hd-terminal-kerning-ja-r2` passes `start-system-settings-uld,font-runtime-glyph-bounds,lobby-runtime-font-safety,third-party-game-font-safety,lobby-runtime-scale-font-routes,lobby-large-label-scale-layouts,party-list-self-marker,combat-flytext-damage-glyphs,pvp-profile-font-routes`. This is code-level verification only; keep the live HD/non-4K crash open until user confirmation.
- 2026-05-28 r32 crash verifier correction: the rejected `20260527-234448` live-crash output failed only after checking packed texture locators, not raw FDT/texture bounds. Patched font textures had `blockCount=1` while runtime-safe font textures need 16KB texture blocks (`font1/2/3/font_krn_1` 2098 blocks; lobby font textures 132 blocks). The generator now repacks patched font textures with one SqPack texture locator per 16KB block, and `font-runtime-glyph-bounds` rejects one-block font texture repacks. Fresh `.tmp\hd-texture-block-layout-ja-r1` passes `font-runtime-glyph-bounds,lobby-runtime-font-safety,lobby-multitexture-font-set`; broader visual font-size/gap issues remain separate.
- 2026-05-29 r33 HD analyzer page2 guard: the rejected `20260528-003930` output is now rejected by `lobby-runtime-font-safety` because `AXIS_12_lobby` patched Hangul uses `font_lobby3.tex` (`image_index >= 8`), matching the later crash log's `R10=2` null analyzer buffer. Fresh `.tmp\hd-startup-page2-safe-ja-r7` keeps `AXIS_12_lobby` patched Hangul on `font_lobby1..2` and passes the focused lobby crash/coverage group plus `ingame-critical`. Live confirmation is still required before closing the HD/non-4K boot crash.
- 2026-05-29 r33 release verification: commit `1847c23` produced `Release\Public\FFXIVKoreanPatch.exe` SHA256 `93E628E63E5803B1993B4F60FDEA46C09E94251CF9C60B40E42BFB6B32462BCC`. Release-generator output `.tmp\release-hd-crash-focus-ja` passes the focused lobby crash/coverage group, `font-runtime-glyph-bounds`, and `ingame-critical`. `Scripts\verify-hd-crash-release.ps1` is the reproducible gate for this issue; `-SkipGenerate -RunInGameCritical` passes focused output, `ingame-critical`, known-bad `20260528-003930` rejection, and `Scripts\check-hd-crash-logs.ps1`. `%APPDATA%\XIVLauncher` has no appcrash log newer than this release, and the latest existing `20260528-004301` log remains the page2 evidence, not a post-release rejection.
- 2026-05-29 r33 full gate verification: `Scripts\verify-hd-crash-release.ps1` without `-SkipGenerate` regenerated `.tmp\release-hd-crash-gate-ja` and passed the focused crash/coverage gate, known-bad rejection, and crash-log check. Keep `.tmp\applied-route-verification-logs\20260529-224528-*.log` as the current full-generation evidence.
- 2026-05-29 r33 current release rebuild: after commit `7fcf572`, `Scripts\build-release.ps1` rebuilt `Release\Public\FFXIVKoreanPatch.exe` at `2026-05-29 22:51:37`; SHA256 remains `93E628E63E5803B1993B4F60FDEA46C09E94251CF9C60B40E42BFB6B32462BCC`. A fresh full gate generated `.tmp\release-hd-crash-head-ja` and passed focused crash/coverage checks, known-bad rejection, and crash-log check; a follow-up `-SkipGenerate -RunInGameCritical` gate on the same output also passes. Logs are `.tmp\applied-route-verification-logs\20260529-225238-*.log`.
- 2026-05-29 r34 post-release rejection/correction: the user reproduced the HD crash again after the r33 release. New log `dalamud_appcrash_20260529_231833_830_30492.log` still points to `AtkFontAnalyzerRenderer` with `RAX=FFF`, `RCX=0`, `R10=2`, `R11=0`. Strengthened `lobby-runtime-font-safety` now rejects the r33 output `.tmp\release-hd-crash-head-ja` because `AXIS_14_lobby` patched Hangul still used `font_lobby3.tex`; the previous check only caught `AXIS_12_lobby`. Current r34 code treats the startup/runtime-limited small lobby font group (`AXIS_12/14/18_lobby`, `MiedingerMid_12/14/18_lobby`) as page2-unsafe and keeps patched Hangul on `font_lobby1..2`. Fresh `.tmp\hd-startup-page2-small-font-safe-ja-r1` passes the focused HD crash/coverage checks and `ingame-critical`, but this still needs live confirmation.
- 2026-05-29 r34 release build: `Release\Public\FFXIVKoreanPatch.exe` rebuilt at `2026-05-29 23:36:15`, SHA256 `3EDA5DEA56CE9097841BAE88543778B506DB2A5E678B135C781B2B981CF4BBF2`. The release gate passed with `-BuildRelease -SkipGenerate -RunInGameCritical -FailOnAnyNewCrash`; the latest matching crash log is older than this build.
- 2026-05-29 r34 full gate: current HEAD `da4db5c` regenerated `.tmp\release-hd-crash-r34-ja` through `Scripts\verify-hd-crash-release.ps1` without `-SkipGenerate`; focused HD crash/coverage, `ingame-critical`, known-bad rejection, and crash-log checks all pass. Logs are `.tmp\applied-route-verification-logs\20260529-234613-*.log`.
- 2026-05-29 r34 gate hardening: `Scripts\verify-hd-crash-release.ps1` validates multiple known-bad outputs. Defaults now include the historical `20260528-003930` output and the post-release r33 `.tmp\release-hd-crash-head-ja` output when available; both must fail on `unsafe lobby texture route.*font_lobby3.tex`.
- 2026-05-30 r34 crash-log caveat: the user reported another HD/non-4K crash after the `2026-05-29 23:36:15` release. No new `dalamud_appcrash_*.log` was produced after that release; the latest appcrash stack is still `dalamud_appcrash_20260529_231833_830_30492.log` from before the release. The new evidence is `dalamud.crashhandler.log` at `2026-05-30 00:29:51` with `Failed to read exception information; error: 0x6d` and target-process termination. Treat this as an open live rejection, but do not infer a new unsafe font page from it without a stack or a verifier that fails the current applied output.
- 2026-05-30 verifier correction: `Scripts\check-hd-crash-logs.ps1` now reads the latest `dalamud.crashhandler.log` session, not only `dalamud_appcrash_*.log`, so the `00:29:51` crashhandler-only termination fails the release gate. `Scripts\verify-hd-crash-release.ps1` also has optional `-AppliedGame` support to compare the installed game files against the generated output before trusting a gate run.
- 2026-05-30 applied-state result: installed `D:\SquareEnix\FINAL FANTASY XIV - A Realm Reborn\game` matches `%LOCALAPPDATA%\FFXIVKoreanPatch\generated-release\ja\2026.05.01.0000.0000\20260530-002104` for generated font/UI outputs. That generated output passed the r34 focused lobby runtime checks and `font-runtime-glyph-bounds` before the all-lobby page2 guard below, so the next change first had to add a verifier that fails this exact applied output before changing font placement.
- 2026-05-30 page2 verifier/fix correction: the exact applied output `20260530-002104` is now rejected because `TrumpGothic_23_lobby.fdt` patched Hangul used `font_lobby3.tex` (`image_index=8..11`). The generator now excludes `font_lobby3` from patched `_lobby.fdt` Hangul candidates while still allowing `font_lobby4..6` for coverage; do not collapse every lobby route to `font_lobby1..2` without new evidence. Fresh `.tmp\hd-lobby-no-page3-ja-r1` has no patched `font_lobby3` cells, passes focused HD checks, rejects known-bad outputs including `20260530-002104`, and passes `ingame-critical`. This remains code-level evidence, not live closure.
- 2026-05-30 r35 start-screen HD spacing correction: the broad non-lobby `AXIS_12/14` Hangul `OffsetX` advance edit is rejected because it made `action-detail-scale-layouts` fail source-preservation phrases such as `전적`, `경쟁의 날개`, and `전략적 대화`. The kept route leaves original Hangul glyph entries intact and rewrites only the known start-screen system-settings Addon rows (`Addon` 4000-4200, 8683-8722) to PUA aliases. Alias glyphs reuse the source glyph texture cell, and only the alias entry gets runtime spacing compensation (`AXIS_12/14` +2, `AXIS_18/KrnAXIS_180` +1). The new `start-screen-glyph-variants` verifier fails if PUA aliases appear outside approved Addon rows or if approved rows keep the risky phrase unaliased.
- 2026-05-30 r35 verification: fresh `.tmp\start-variant-ja-r3` passes `start-screen-glyph-variants,lobby-render-snapshots,font-runtime-glyph-bounds,third-party-game-font-safety,action-detail-scale-layouts,pvp-profile-font-routes`; the broader focused set also passes `lobby-coverage-glyphs,lobby-runtime-font-safety,lobby-multitexture-font-set,lobby-runtime-scale-font-routes,lobby-hangul-visibility,party-list-self-marker,combat-flytext-damage-glyphs`. `Scripts\verify-hd-crash-release.ps1 -SkipGenerate` passes on the same output, rejects known-bad outputs, and finds no newer crash log than the release. This is generated-output verification only; do not report the live lobby HD issue as closed until user confirmation.
- 2026-05-30 r35 source-preservation follow-up: full `hangul-source-preservation` now models the `PvpProfileVisualScaleGlyphs` sheet/Addon-derived codepoint set as intentional only on its target font routes, instead of blanket-skipping a whole Jupiter font. `.tmp\start-variant-ja-r3` passes `hangul-source-preservation,third-party-game-font-safety,ingame-ttmp-texture-neighborhoods,action-detail-scale-layouts,pvp-profile-font-routes,combat-flytext-damage-glyphs,party-list-self-marker,start-screen-glyph-variants,lobby-render-snapshots` with `skipped_intentional=607`.
- 2026-05-30 PvP 100% correction: `pvp-profile-font-routes` now fails the prior `.tmp\start-variant-ja-r3` output because 100% `Jupiter_16.fdt` PvP labels such as `크리스탈라인 컨플릭트` preserve oversized TTMP source scale (`Hangul/digit=1.729`, outside `1.160..1.420`). `PvpProfileVisualScaleGlyphs` targets both `Jupiter_16.fdt` and `Jupiter_20.fdt`; fresh `.tmp\pvp-jupiter16-ja-r1` passes `pvp-profile-font-routes` with `Jupiter_16` `크리스탈라인 컨플릭트` at `1.333`, and also passes `hangul-source-preservation,third-party-game-font-safety,ingame-ttmp-texture-neighborhoods,combat-flytext-damage-glyphs,party-list-self-marker,action-detail-scale-layouts`. This is generated-output verification only; keep the live PvP profile report open until user confirmation.
- 2026-05-30 r36 lobby high-scale ASCII correction: do not limit high-scale lobby clean ASCII injection to only high-resolution UI option/result-message phrases. The 4K lobby verifier renders `LobbyScaledHangulPhrases.All`, so generator ASCII coverage must derive from the same phrase set; otherwise mixed Hangul/ASCII phrases such as `데이터 센터 Mana에 접속 중입니다.` can miss `M` in `Jupiter_90_lobby.fdt`/`Meidinger_40_lobby.fdt`. Fresh `.tmp\lobby-highscale-ascii-all-ja-r1` passes the focused lobby route set and `reported-ingame-hangul-phrases,pvp-profile-font-routes,font-runtime-glyph-bounds,4k-lobby-phrase-layouts`.
- 2026-05-30 r36 gate hardening: `Scripts\verify-hd-crash-release.ps1` now also rejects the prior PvP 100%-oversized output `.tmp\start-variant-ja-r3` through `pvp-profile-font-routes` (`Jupiter_16.fdt PvP phrase ... outside`). This keeps the PvP route regression from being treated as fixed merely because the broader release gate passes. A short `-SkipGenerate -SkipCrashLogCheck` gate on `.tmp\lobby-highscale-ascii-all-ja-r1` rejected the three lobby crash known-bad outputs and this PvP known-bad output.
- 2026-05-30 r36 applied full gate evidence: after commit `74b2ad3`, `Scripts\verify-hd-crash-release.ps1 -SkipGenerate -AppliedGame ... -RunFullFontObjective -FailOnAnyNewCrash` passed on `.tmp\lobby-highscale-ascii-all-ja-r1`. `applied-output-files` and `applied-lobby-routes` matched the installed `D:\SquareEnix\FINAL FANTASY XIV - A Realm Reborn\game`; all objective chunks passed; the three lobby crash known-bad outputs and `.tmp\start-variant-ja-r3` PvP known-bad output were rejected; no `AtkFontAnalyzerRenderer` appcrash log is newer than the 2026-05-30 20:55:56 release exe.
- 2026-05-30 r36 PvP verifier noise correction: `pvp-profile-font-routes` now reports non-target reference-scale comparisons as `INFO` instead of `WARN`. The hard checks remain on route-preserved ULD font/render bytes and `Jupiter_16.fdt`/`Jupiter_20.fdt` numeric visual scale. Current `.tmp\lobby-highscale-ascii-all-ja-r1` passes `action-detail-scale-layouts,pvp-profile-font-routes` with no WARN/FAIL, while `.tmp\start-variant-ja-r3` still fails on the expected `Jupiter_16.fdt` PvP oversized route. A short release gate still rejects all known-bad outputs.
- 2026-05-30 applied-state recheck: `Scripts\verify-hd-crash-release.ps1 -SkipGenerate -AppliedGame 'D:\SquareEnix\FINAL FANTASY XIV - A Realm Reborn\game'` fails because the installed game files do not match `.tmp\start-variant-ja-r3`. The applied `TrumpGothic_23_lobby.fdt` still routes patched Hangul through unsafe `font_lobby3.tex` (`image_index=8..11`). Treat crash/font reports from that applied state as stale or mixed-install evidence until `applied-output-files` and `applied-lobby-routes` pass for the current generated output.
- 2026-05-23 live confirmation: in-game ActionDetail `즉시 발동` / `초` output is acceptable enough in client. The remaining numeric value position/alignment observation is a separate follow-up; compare clean/global before changing this route again.
- 2026-05-23 live confirmation: story completion image/card description appears in Korean. `つづく` stays Japanese and is acceptable for now.
- 2026-05-23 live confirmation: Occult Crescent appears to render correctly. Keep `MkdSupportJob` main/support column verification as a regression guard.
- 2026-05-17 verifier correction: `.tmp\large-ui-visual-scale-ja-r10` is rejected by the strengthened verifier. The previous PASS was a false positive because it validated height only and missed width/advance. The fixed path is `.tmp\large-ui-biaxial-scale-ja-r1`, where non-lobby `TrumpGothic_23/34/68.fdt` large UI Hangul alpha, width, and advance are scaled together. This historical caution is superseded for in-game `즉시 발동` / `초` by the 2026-05-23 live confirmation above; lobby/PvP large-label issues remain separate until separately confirmed.
- 2026-05-17 r11 verifier guard: before reporting progress in this area, run at least `action-detail-scale-layouts,lobby-scale-font-sources,third-party-game-font-safety,combat-flytext-damage-glyphs` from local origin/restore baseline. The expected focused result for the current code is `RESULT: PASS`, with `TrumpGothic_68.fdt` high-scale Hangul coverage `898/898`, no `TrumpGothic_68_lobby` contamination, and clean `Jupiter_45/Jupiter_90` damage-number routes still passing.
- 2026-05-17 r11 correction: do not exclude Korean combat phrases such as `극대화`/`직격` from large UI Hangul coverage. The user-confirmed fly-text fix protects clean damage digits and punctuation in `Jupiter_45/Jupiter_90`, not Korean phrase glyphs in `TrumpGothic_23/34/68`. Excluding shared Hangul like `대`/`화` caused mixed high-scale/unscaled glyphs in labels such as `전략적 대화`.
- 2026-05-17 r11 generated output: `.tmp\large-ui-combat-exclusion-ja-r2` passes `action-detail-scale-layouts,combat-flytext-damage-glyphs,third-party-game-font-safety`. A separate check on the same output passes `lobby-large-label-scale-layouts,start-system-settings-uld,lobby-scale-font-sources`. This is still code-level verification only; do not close the live large-label/PvP visual issue until user confirmation.
- 2026-05-17 r12 PvP route guard: `pvp-profile-font-routes` follows `PvPCharacter.uld`, `PvPAction.uld`, and `PvPTeam.uld` routes and checks reported PvP labels such as `PvP 프로필`, `전적`, `크리스탈라인 컨플릭트`, `PvP 기술`, and `전략적 대화` across the routed fonts. On `.tmp\large-ui-combat-exclusion-ja-r2`, it passes with `ulds=3`, `fonts=9`, `phrases=63`. This is a route/regression guard only; it does not mean the live PvP visual-size issue is closed.
- 2026-05-18 r13 PvP visual-scale guard: older output `.tmp\large-ui-combat-exclusion-ja-r2` is rejected by the strengthened `pvp-profile-font-routes` check because `Jupiter_16/20/23.fdt` Korean labels measured about 1.2-1.5x the digit visual height. Fresh output `.tmp\pvp-profile-visual-scale-ja-r3` scales only `Jupiter_16/20/23.fdt` PvP-profile Hangul cells collected from the PvP sheets/Addons; it deliberately keeps `MiedingerMid_12/14.fdt` source-preserved so `third-party-game-font-safety` continues to pass. Focused result: `pvp-profile-font-routes,combat-flytext-damage-glyphs,third-party-game-font-safety` -> `RESULT: PASS`, verifier failures `0`. This is code-level verification only; do not mark the live PvP profile visual-size issue closed until user confirmation.
- 2026-05-18 r14 lobby large-label correction: the lobby `시스템 설정`/character-select large-label scale issue was not caught because `lobby-large-label-scale-layouts` compared patched Korean glyphs against the same Korean source glyphs. The verifier now compares `TrumpGothic_23_lobby/34_lobby` Korean large labels against clean CJK reference height and keeps source-route upper bounds for width/advance. The generator applies a narrow visual-scale repair only to `TrumpGothic_23_lobby.fdt` and `TrumpGothic_34_lobby.fdt`; `TrumpGothic_68_lobby.fdt` stays on the normal high-scale `AXIS_36` shared-source route to avoid atlas exhaustion. Fresh output `.tmp\lobby-large-label-visual-ja-r5` passes `lobby-large-label-scale-layouts,lobby-scale-font-sources,start-system-settings-uld,third-party-game-font-safety,combat-flytext-damage-glyphs` with `Verifier failures: 0`. This is code-level verification only; keep the live lobby UI-scale issue open until user confirmation.
- 2026-05-18 r15 lobby large-label scope note: do not expand `TrumpGothic_23_lobby/34_lobby` visual-scale coverage to broad Addon row ranges yet. A trial adding Addon `4000-4200` and `8683-8722` raised large-label coverage from 200 to 327 glyphs and starved later high-scale lobby fonts, causing missing `U+D734` in `AXIS_36_lobby`, `Jupiter_46_lobby`, `Jupiter_90_lobby`, `Meidinger_40_lobby`, `MiedingerMid_36_lobby`, and `TrumpGothic_68_lobby`. The kept change only adds start-main-menu representative phrases to the large-label candidate set; `.tmp\lobby-large-label-expanded-ja-r2` passes `lobby-large-label-scale-layouts,lobby-scale-font-sources,start-system-settings-uld,start-main-menu-phrase-layouts,third-party-game-font-safety,combat-flytext-damage-glyphs` with `Verifier failures: 0`.

이 문서는 현재 확인된 문제와 다음 작업자가 이어서 처리해야 할 항목을 정리합니다. 아래 항목은 “구현 완료”가 아니라 “작업 필요” 상태입니다.

## 최근 보고 후 처리됨

### ESC 시스템 메뉴의 설정 공유 제목이 베이스 클라이언트 언어로 표시됨

상태: 처리됨

보고 내용:

- ESC를 눌렀을 때 나오는 시스템 메뉴/팝업에서 `コンテンツシェア` 또는 `コンフィグシェア` 계열 제목이 번역되지 않음
- 설정 공유 창 내부 내용은 이미 번역되어 있음

처리 방향:

- 기존 보정은 `Addon#17300/17301` 창 제목을 모두 `설정 공유`로 고정해, 실제로는 비어 있어야 하는 subtitle row를 오염시켰음
- ESC 시스템 메뉴 항목은 `MainCommand#99`를 참조하며, 한국 서버의 `MainCommand#99` title/description이 비어 있었음
- `MainCommand#99` title과 description을 정책 literal로 보정
- `Addon#17300/17315/17330/17360/17380` main title은 한국어로 고정하고, `Addon#17301/17316/17331/17361/17381` subtitle은 빈 문자열로 고정
- `ColumnRemap.Literal("")`이 secondary language patch에서 무시되던 패처 버그를 수정
- `configuration-sharing` verifier가 `ja/en/de/fr` 전체 슬롯에서 main/subtitle pair를 검사하고, main과 subtitle이 같은 텍스트이거나 서로 다른 언어로 섞이면 실패하도록 확장

## 우선순위 높음

### 0. 2026-05-09 재보고: 시작 화면 데이터센터/시스템 설정 실사용 경로 미검증

상태: 사용자 확인 완료 (2026-05-16)

최근 verifier는 통과했지만 실제 클라이언트에서 아래 문제가 계속 재현되었습니다. 이는 기존 검증이 실제 시작 화면 UI route를 충분히 잡지 못했다는 뜻으로 취급합니다.

2026-05-09 진행:

- `Lobby#2002`는 `종료` literal로 고정했고, `EXIT`가 남으면 `data-center-rows`에서 실패하도록 했다.
- 데이터센터 화면의 뒤로/확인/취소 계열은 `Lobby#2002`가 아니라 `Lobby#2009`, `Lobby#2047`, `Lobby#2050`, `Lobby#2051`, `Lobby#2052`에도 분산되어 있었다. 이 row들을 로비 액션 literal 정책과 verifier 기대값에 추가했다.
- 시작 화면 시스템 설정은 실제 설정 Addon 라벨을 공유 목록으로 분리해 4K 로비 파생 glyph와 verifier가 같은 기준을 쓰도록 했다. 이전 산출물은 새 verifier에서 실패했고, 새 산출물은 통과했다.
- 데이터센터 ULD route는 기존 glyph/metrics/kerning 비교에 더해 핵심 라벨 문장 픽셀 스냅샷을 clean global과 비교하도록 보강했다. 현재 산출물은 코드 기준으로 통과한다.
- 데이터센터 그룹명 흐림/서버명 간격 문제는 glyph box 비교만으로는 부족했으므로, clean global 대비 2px texture neighborhood까지 비교하도록 verifier를 보강했다. 제너레이터는 lobby clean ASCII cell을 할당할 때 glyph 주변 padding도 같이 복사하며, 이 padding 복사는 lobby FDT에만 제한해 일반 인게임/대사 폰트 atlas 오염을 막는다.
- 재보고 후 2px padding 검증은 부족한 것으로 보고, 데이터센터/시작화면 lobby ASCII texture neighborhood 기준을 4px로 올렸다.
- 시작화면 시스템 설정은 스크린샷에 나온 실제 혼합 문장(`150%(FHD): 1728x972 이상 권장` 등)을 `system-settings-mixed-scale-layouts`로 검증한다. 이 검증은 ASCII/숫자/기호의 clean metrics/texture padding, Hangul advance, 문장 overlap을 함께 확인한다.
- 로비 시작화면 설정 완료 메시지 `설정을 변경했습니다.`, `일부 설정은 적용을 눌러야 반영됩니다.`도 같은 고배율 로비 폰트 검증에 포함한다. 이전 검증은 두 번째 결과 메시지를 놓쳤으므로, 새 산출물은 두 문구 모두 fallback `=`/`-`가 아님을 확인한다.
- 4K lobby 파생 폰트 생성은 Hangul만 한국 TTMP source에서 가져오고, ASCII/숫자/기호는 clean global lobby route를 유지한다. clean target에 없는 ASCII만 기존 파생 source pair의 clean lobby font에서 보충한다.
- 2026-05-10 보강: 기존 verifier는 FDT byte 14를 draw 시작 offset처럼 해석해 ASCII 간격 문제를 놓쳤다. 렌더링 기준을 `drawX = 32`, `advance = width + OffsetX`로 바꾸고, 인접 glyph의 실제 alpha bounds 기준 최소 visual gap을 검사하도록 수정했다.
- 2026-05-10 재처리: 로비용 safe spacing은 100%에서 간격이 과하게 벌어지고 150% 이상에서도 일부 겹침을 남겼다. `_lobby.fdt` ASCII/숫자/기호는 동일 이름의 clean lobby FDT를 reference로 사용하도록 바꿨다. 예: `AXIS_12_lobby.fdt` -> clean `AXIS_12_lobby.fdt`.
- 2026-05-10 검증: 현재 설치된 이전 산출물은 새 verifier에서 `DATA CENTER SELECT`, `Elemental`, `150%(FHD): 1728x972 이상 권장` 등으로 FAIL한다. 새 ja 산출물 `.tmp\lobby-ingame-reference-fallback-ja`는 `data-center-title-uld`, `system-settings-mixed-scale-layouts`, `clean-ascii-font-routes`와 넓은 회귀 묶음에서 PASS한다.
- 2026-05-10 추가 검증: 실제 UI 배율에서 12/14가 아니라 큰 로비 폰트로 치환되는 경로를 잡기 위해 `lobby-scale-font-sources` verifier를 추가했다. 이 검증은 `AXIS_12/14/18_lobby`와 `AXIS_36_lobby` 등 고배율 파생 로비 폰트가 각 배율에 대응되는 TTMP source glyph를 쓰는지, 100% glyph를 억지 확대하지 않는지 width/height 및 alpha pixel 단위로 확인한다.
- 2026-05-10 최신 산출물: `.tmp\lobby-scale-source-ja`는 `start-system-settings-uld,lobby-scale-font-sources,system-settings-mixed-scale-layouts` PASS. `start-system-settings-uld`는 이제 `150%(FHD): ...` 계열 고해상도 UI 옵션 문구도 `AXIS_12/14/18_lobby` route에서 검사한다.
- 2026-05-10 인게임 ActionDetail 배율 보강: `ActionDetail.uld` route를 추적하고 `즉시 발동`, `초`, `120.00초`, `1.50초`를 숫자 baseline과 비교하는 `action-detail-scale-layouts` verifier를 추가했다. `TrumpGothic_68.fdt`의 고배율 Hangul glyph는 `Addon#699-714`와 fallback 문구에서 자동 수집한 codepoint만 route 단위로 보정하며, 기존 glyph atlas cell은 덮어쓰지 않는다. `.tmp\action-detail-scale-ja4`와 `.tmp\action-detail-scale-ja4-apply` 산출물은 ActionDetail/reported-ingame/4K lobby derivation/Hangul preservation 검증을 통과했다. 실제 일본어 클라이언트 폴더 복사는 실행 중인 `ffxiv_dx11`/`XIVLauncher` 잠금으로 차단되어 산출물 검증 상태로 남긴다.
- 2026-05-10 strict 검증 강화 정정: 이전 PASS는 clean/source baseline도 음수 visual gap을 갖는 경우를 통과시켜 사용자가 보고한 로비/데이터센터 증상을 닫지 못했다. `data-center-title-uld,data-center-worldmap-uld,start-system-settings-uld`는 strict visual-gap을 적용한다.
- 2026-05-11 재조합 정정: broad `KrnAXIS_120/140/180/360` lobby Hangul 치환은 `AXIS_12/14/18/36_lobby` glyph의 높이/컴포넌트 수를 바꾸며 pixelation 위험을 만들었으므로 제거했다. `AXIS_*_lobby` Hangul은 TTMP source glyph 크기/pixel을 보존하고, `AXIS_12/14/18_lobby`에서 `source advance < glyph width`인 Hangul만 `OffsetX=0` advance-only 보정을 허용한다.
- 현재 산출물: `.tmp\lobby-ttmp-source-advance-ja`는 `.tmp\verifier-lobby-ttmp-source-advance-focused-r5.log`와 `.tmp\verifier-lobby-ttmp-source-advance-full-r2.log`에서 PASS한다. 다만 데이터센터 영어 서버명 spacing/blur와 로비 150%+ 시스템 설정 spacing/blur는 사용자 OK 전까지 열린 항목으로 둔다.
- 2026-05-11 재검증 보강: 기존 PASS는 `_lobby` 고배율 FDT 자체만 봤고, 실제 로비 시스템 설정의 non-lobby AXIS route를 놓쳤다. `lobby-render-snapshots` verifier를 추가해 대표 문구 PNG와 `lobby-render-report.tsv`를 덤프하고, `AXIS_18.fdt`/`KrnAXIS_180.fdt`/`AXIS_36.fdt`/`KrnAXIS_360.fdt`를 고배율 후보로 비교한다. `AXIS_18.fdt`/`KrnAXIS_180.fdt` advance를 직접 보정한 `.tmp\lobby-axis-advance-ja`는 render snapshot은 PASS했지만 `reported-ingame-hangul-phrases`에서 인게임 문구를 오염시켜 폐기했다. 전역 AXIS/KrnAXIS는 진단 대상으로만 유지하고, 실제 수정은 로비 전용 route나 타이틀 컨텍스트 route 분리로 잡는다. 사용자 OK 전까지 해당 이슈는 열린 상태로 유지한다.
- 2026-05-12 재검증 강화: `lobby-render-snapshots`가 `min_pair`를 TSV/로그에 남기며, 재보고된 런타임 후보 `AXIS_12.fdt` 150%와 `AXIS_14.fdt` 150/200/300%는 더 이상 진단 전용이 아니라 `minGap >= 1` 실패 조건으로 검사한다. r1 산출물은 `했/습`, `다/.`, `0/%` 계열에서 이 검사를 실패했고, r2 산출물 `.tmp\startscreen-route-kerning-ja-r2`는 해당 경로를 모두 통과한다.
- 2026-05-12 코드 조치: 시작화면 시스템 설정 문구 목록에서 필요한 kerning pair를 자동 수집해 `AXIS_12/14/18` 및 대응 `KrnAXIS` route에 최소 보정만 추가한다. 직접 codepoint 보호 배열이나 broad advance 보정은 사용하지 않는다. 같은 FDT가 인게임에서도 쓰일 수 있으므로 `reported-ingame-hangul-phrases`, `action-detail-scale-layouts`, `hangul-source-preservation`를 함께 돌려 오염 여부를 확인한다. `.tmp\verifier-startscreen-route-kerning-regression.log`는 데이터센터, 설정 공유, 보즈야, 오컬트 크레센트, 인게임 보고 문구, ActionDetail, Hangul source preservation, lobby render snapshot 묶음에서 PASS했다. 사용자 OK 전까지 완료로 닫지 않는다.
- 2026-05-12 reset: 위 PASS 이후에도 사용자가 로비 150%+ pixelation/blur/spacing이 전혀 개선되지 않았다고 재보고했다. 따라서 로비 glyph 재조합을 더 얹는 방식은 폐기하고, 기본 생성에서 derived 4K lobby FDT 생성, `AXIS_12/14/18_lobby` Hangul advance 정규화, `AXIS_14_lobby` blank Hangul alias, start-screen kerning 우회를 제거했다.
- 2026-05-12 reset 검증: `lobby-ttmp-payloads` verifier를 추가했다. TTMP에 포함된 lobby FDT/TEX는 TTMP payload와 byte-for-byte 동일해야 하고, TTMP에 없는 high-scale lobby target은 clean global payload와 동일해야 한다. `.tmp\lobby-reset-ja`는 핵심 회귀 묶음에서 PASS했지만, `lobby-render-snapshots`는 의도대로 남은 문제를 FAIL로 보고한다.
- 2026-05-12 reset 후 남은 실패: `Jupiter_20_lobby` world list의 `A/t` overlap, `TrumpGothic_23_lobby` title의 `A/T` minGap -2, `AXIS_12/18_lobby` 설정 완료 문구 한글 minGap -1, `AXIS_36_lobby`의 `U+C124` missing, non-lobby `AXIS_12/14` 150/200/300% 후보의 minGap < 1. 이 항목들은 사용자 OK 전까지 열린 이슈이며, 다음 구현은 route-specific source manifest와 render snapshot 실패 조건을 먼저 세운 뒤 최소 재조합으로 다시 시작한다.
- 2026-05-16 사용자 확인: full-sheet/shared-source 산출물 기반 릴리즈 적용 후 “전체적으로 로비에서 폰트가 다 잘 나온다”고 확인됨. 기준 산출물은 `.tmp\lobby-fullsheet-sharedsource-pad0-ja-r3`, 릴리즈는 `Release\Public\FFXIVKoreanPatch.exe`, embedded generator SHA-256은 `7243DCA02C167EA5D9AEA1F212ECDD63BDD9241E878A541FA50DE4F2C8F36DEC`.
- 2026-05-16 in-game combat fly-text follow-up: clean 비교 기준은 설치된 게임 클라이언트가 아니라 local `.tmp`/origin backup과 explicit `orig.*` base index여야 한다. `.tmp\ingame-combat-flytext-clean-ja-r3`는 local `.tmp\clean-global-full-input-game` + `.tmp\lobby-fullsheet-sharedsource-pad0-ja-r3\orig.*` 기준으로 생성했고, `직격`/`극대`/`극대화`/`크리티컬`/`극대 직격` 계열은 `TrumpGothic_68.fdt`에서도 TTMP source와 일치해야 하도록 verifier를 강화했다. 같은 검증에서 `action-detail-scale-layouts`는 local clean UI dat0 부재 시 ULD route probing을 경고로 낮추고 정적 font probe를 계속 수행한다.
- 2026-05-16 in-game font risk survey: `ingame-font-risk-survey`를 추가했다. local temp/origin 기준으로 인게임 ULD font route, 인게임 관련 sheet Hangul 후보, PUA glyph visibility를 TSV로 남긴다. local origin index는 `%LOCALAPPDATA%\FFXIVKoreanPatch\restore-baseline\ja\2026.05.01.0000.0000\orig.060000.win32.index/index2`에 있다. 단, `%LOCALAPPDATA%`에는 raw `060000.win32.dat0`~`dat3` 복사본이 없으므로 ULD route clean read는 origin index와 원본 dat shard source를 함께 써야 한다. `PatchRouteVerifier`는 이제 `--clean-font-index`/`--clean-ui-index`를 받고, output-local `orig.*`가 없으면 `%LOCALAPPDATA%\FFXIVKoreanPatch\restore-baseline\<target-language>\<latest>`를 자동 탐색한다.
- 2026-05-16 current in-game survey result: 오래된 generated release `20260516-212522`는 `TrumpGothic_68.fdt`의 `극대화`, `극대화!`, `크리티컬`, `극대 직격`이 TTMP 원본보다 커져 strong verifier에서 실패했다. 현재 코드로 `%LOCALAPPDATA%` restore-baseline을 `-BaseIndexDirectory`로 지정해 만든 `.tmp\ingame-combat-current-ja-r1`은 `reported-ingame-hangul-phrases,ingame-ttmp-texture-neighborhoods,action-detail-scale-layouts,protected-hangul-glyphs,numeric-glyphs` PASS. 같은 산출물의 `ingame-font-risk-survey`는 ULD 201 rows, sheet 후보 172266 rows, unique Hangul 1540개, PUA 130 rows를 기록했고 clean-visible PUA regression은 0이다. 추가 위험 지점은 route 변경이 아니라 shared font 사용이다: `TrumpGothic_23.fdt`는 ActionDetail/ActionMenu/ItemDetail/Character/ContentsFinder/ContentsInfo/HudLayout/Teleport에서 쓰이고, `AXIS_12/14/18`은 여러 HUD와 party PUA를 공유한다.
- 2026-05-16 follow-up: applied-output comparison had a false drift when reading installed game folders without sibling `orig.*.index`; `CompositeArchive` now uses explicit clean font/UI baseline indexes for applied comparisons. Current generated/applied sqpack file hashes matched, and the fixed verifier reports font/UI payload matches instead of false `Jupiter_45.fdt`/`font5.tex` drift.
- 2026-05-16 follow-up correction: critical/direct-hit fly text breakage is the damage-number glyphs before the large `!`/`!!`, not Korean phrase text such as `극대 직격!`. Added `combat-flytext-damage-glyphs` to verify auto-discovered non-lobby `TrumpGothic_*.fdt` candidates for digits, number+`!`/`!!` phrases, and all non-Hangul combat glyph 8px neighborhoods against clean source. The first run `.tmp\combat-damage-glyphs-ja-r1` failed because non-Hangul glyph neighborhoods in `font1/font2` were polluted by nearby patches; the generator now reserves 8px around every glyph in those combat fonts. `.tmp\combat-damage-glyphs-ja-r2` passes the focused combat verifier set; user confirmation is still required before closing the live critical/direct-hit blur report.
- 2026-05-17 broader in-game blur guard: the clean-source verifier showed that protecting only TrumpGothic damage digits is too narrow because other non-lobby fonts can have clean glyph interiors but polluted 8px texture neighborhoods. Added `ingame-clean-ascii-glyphs` and changed generation so repaired non-lobby ASCII/number/symbol glyphs copy the clean source with 8px neighborhoods. Clean `Jupiter_45.fdt` ASCII neighborhoods are also restored after shared global texture patches. Fresh font-only output `.tmp\ingame-clean-ascii-broad-ja-r2` passes the broad in-game ASCII verifier plus combat damage, TTMP Hangul neighborhood, reported Hangul phrase, and action-detail scale checks.
- 2026-05-17 live correction: damage fly text is still blurred in the client. The previous combat verifier is now considered insufficient because it focused on `TrumpGothic_*` instead of forcing `Jupiter_45/Jupiter_90` damage-number routes. Keep the issue open until the expanded Jupiter clean-cell protection and 16px neighborhood verifier are validated and the user confirms the live result.
- 2026-05-17 implementation update: damage-number protection is now scoped to clean `Jupiter_45/Jupiter_90` FDTs and their visible glyph cells, with 16px texture-neighborhood restoration after shared `font3.tex` patches. The stronger verifier fails the older `.tmp\full-flytext-check-ja-r1` output and passes fresh `.tmp\full-flytext-jupiter-guard-ja-r2` together with in-game ASCII, TTMP Hangul neighborhood, reported phrase, and ActionDetail scale checks. Do not close the live issue before user confirmation.
- 2026-05-17 live rejection: the Jupiter whole-FDT clean-preservation attempt is not accepted. The user reports that damage fly text is still not original-looking and that a third-party notification overlay using the patched game font path now renders Korean as broken glyphs. Do not preserve shared in-game Jupiter FDTs wholesale for damage text; keep Korean-capable FDT payloads and protect only clean damage-number ASCII/`!` texture neighborhoods from the local origin baseline.
- 2026-05-17 verifier correction: matching rendered pixels is not enough for damage fly text. The verifier must also require `Jupiter_45/Jupiter_90` damage glyph `ImageIndex/X/Y` routes to match clean source, because relocating clean-looking glyphs to new atlas cells can still differ from the original client path.
- 2026-05-17 mip correction: the third-party overlay report is not a direct game-resource patch target, but it exposes that shared in-game font paths must remain Korean-capable while damage-number glyphs are restored from clean. Damage fly-text verification now also compares the clean glyph texture neighborhoods across available mip levels, not only the base texture, because scaled rendering can sample polluted lower mips and appear blurred even when the base glyph cell matches.
- 2026-05-17 font-only scope: `font-only` is now tracked separately from the full text/UI patch. It should only generate/apply `000000` font files for Korean input/readability support, not `0a0000` text or `060000` UI/image resources. It also skips full-patch-only lobby Hangul allocation, supplemental lobby font generation, ActionDetail high-scale Hangul repair, and start-screen system-settings kerning. See `Docs/FONT_ONLY_PATCH_SCOPE.md`; use `font-only-output-scope` before deeper glyph checks.
- 2026-05-17 font-only contamination correction: the first strict scope still allowed `AXIS_*` plus `font1/font2`, but `ingame-clean-ascii-glyphs` failed because those shared textures are also sampled by `Jupiter/TrumpGothic/Miedinger` routes that third-party overlays can use.
- 2026-05-17 font-only/Dalamud correction: the AXIS/font1/font2/font3 expansion trial is rejected. The live third-party break was traced to full `--include-font` shared font atlas contamination, not to a safe `font-only` fix. `font-only` stays strict `KrnAXIS_120/140/180/360.fdt` plus `font_krn_1.tex`; shared game-font safety is verified separately by `third-party-game-font-safety`.
- 2026-05-17 user confirmation: damage fly text is now accepted as fixed for the current build. Treat `Jupiter_45/Jupiter_90` damage-number glyph routes and their `font3.tex` base/mip neighborhoods as protected. Do not touch this area while fixing Dalamud/font-only/ActionDetail issues unless a focused verifier first proves a regression.
- 2026-05-17 user confirmation: Dalamud/third-party overlay Korean font break is fixed in-client. Keep `third-party-game-font-safety` as a required regression guard before widening any shared game-font allowlist or reintroducing broad AXIS/Jupiter/TrumpGothic/Miedinger/Meidinger edits.
- 2026-05-17 new priority: ActionDetail/skill-detail font scale looked wrong at high UI scales. Existing `action-detail-scale-layouts` PASS was insufficient; the verifier had to compare live-like ActionDetail/tooltip phrases at 100/150/200/300% against numeric baseline ratios and fail the bad output before any broad in-game font edit. 2026-05-23 live status: the in-game `즉시 발동` / `초` output is now acceptable, with only numeric value alignment left as a follow-up observation.
- 2026-05-16 credit check: current generated `CreditListText` rows for the reported credit area are text-correct (`Lead Artist`, `Bryan Ding`), and only seven Hangul rows exist in the generated sheet; those seven are also present in both global/Korean source. Treat old credit-screen garbling as not reproduced in current EXD text, while keeping font/payload verification open until the user confirms.

재보고 항목:

- 시작 화면의 `종료`가 `EXIT`로 표시됨. 처리됨
- 데이터 센터 선택 화면을 나가는 버튼이 `-로?` / `-료?`처럼 깨져 보임. `Lobby#2009/#2052` 뒤로 row와 관련 ULD font phrase 검증을 추가해 처리됨
- 데이터 센터 선택 화면의 group label은 영어로 나오지만 흐리게 보임. 사용자 확인 완료
- 데이터 센터 선택 화면의 서버명/데이터센터명은 영어로 나오지만 문자 간격이 서로 침범함. 사용자 확인 완료
- 시작 화면 시스템 설정에서 UI 배율을 150/200/300%로 키우면 한글이 `=`로 깨지고 문자 간격이 침범함. 사용자 확인 완료
- `데이터 센터 Mana에 입장합니다` 계열 팝업 문구는 처리됨. 재발 시 sheet/row/column 추적 verifier를 먼저 보강
- 인게임 `즉시 발동`/`초`가 UI 배율별 상대 크기를 제대로 따라가지 않는 문제는 `action-detail-scale-layouts` verifier와 `TrumpGothic_68.fdt` route 단위 glyph 보정으로 처리됨. 실제 클라이언트 복사는 파일 잠금 해제 후 재적용 필요
- 스토리 종료 후 컷씬/이벤트 이미지 설명문이 베이스 클라이언트 언어로 표시됨. 폰트 패치가 아니라 지역 이동 타이틀처럼 실제 표시 리소스를 한국 클라이언트 리소스로 교체해야 할 가능성이 높음. 단, 아직 scene 리소스로 단정하지 않고 `EventImage` 등 sheet 기반 이미지 가능성부터 확인해야 함

필요 작업:

- `Title_DataCenter.uld`, `Title_Worldmap.uld`, 시작 화면 시스템 설정 ULD가 실제로 참조하는 font id와 FDT route를 확정
- 데이터센터 화면에서 재발하는 버튼/문구가 있으면 어느 sheet/row/column에서 오는지 찾아서 EN/JA 언어 슬롯별로 확인
- 데이터센터 화면 서버명과 DC명은 단어 단위가 아니라 실제 리스트 문자열 기준으로 layout 검증. 현재 `DataCenterWorldmapLabels` 전체 기준으로 수행
- 흐림 문제는 clean global ASCII glyph의 픽셀/metrics뿐 아니라 실제 texture cell alpha 차이까지 비교. 현재 non-space ASCII glyph 주변 2px padding 기준으로 수행
- 흐림 문제는 clean global ASCII glyph의 픽셀/metrics뿐 아니라 실제 texture cell alpha 차이까지 비교. 현재 non-space ASCII glyph 주변 4px padding 기준으로 수행
- 시작 화면 시스템 설정은 인게임 font route와 분리해서, 해당 route의 한글 glyph fallback/spacing과 배율별 대응 로비 폰트 source glyph 일치 여부를 함께 검증
- `즉시 발동`/`초` 계열 문구는 `action-detail-scale-layouts`로 실제 ULD/font route, 숫자 대비 시각 높이, `TrumpGothic_34 -> TrumpGothic_68` 상대 스케일을 검증한다
- story 완료 설명문이 `EventImage`, `CutScreenImage`, `ScreenImage`, `DynamicEventScreenImage`, `LoadingImage`, event/cutscene texture/ULD bundle 중 어디에서 오는지 추적
- 설명문이 이미지형 리소스라면 global target 리소스와 Korean source 리소스의 hash를 비교하고, 한국 서버 리소스가 존재하는 경우 output UI index에 Korean packed texture가 매핑됐는지 verifier에 추가

검증 기준:

- 재보고된 항목은 먼저 verifier에서 실패해야 함
- verifier가 pass인데 실제 클라이언트에서 실패하면 해당 verifier는 불완전한 것으로 보고 수정
- 데이터센터 시작 화면의 영어/숫자/특수문자는 clean global 대비 흐림, 간격 침범, fallback 동일 픽셀이 없어야 함
- 시작 화면 시스템 설정의 한글은 `=`/`-` fallback과 같으면 실패
- 인게임 한글 TTMP source preservation은 계속 유지해야 함. `탐사대 호위대원` 같은 보고 문구는 특정 글자 보호 배열이 아니라 문구 목록에서 Hangul codepoint를 자동 수집해 검증
- story 완료 설명문은 EXD 문자열만 한국어인데 실제 화면이 base client 언어로 남으면 실패. 이미지/event/cutscene 리소스까지 Korean source로 교체되었는지 확인해야 함

### 1. 150% / 200% / 300% UI 대응 부족

상태: 작업 필요

현재 대부분의 작업과 검증이 100% UI 기준으로 진행되었습니다. 150%, 200%, 300% UI 스케일에서 사용하는 lobby/in-game font route가 100%와 다를 수 있고, 일부 FDT는 다른 크기 또는 4K lobby font를 탑니다.

필요 작업:

- 150/200/300%에서 실제로 참조되는 FDT 목록 확정
- 시스템 설정, 로비, 캐릭터 선택, 데이터센터 선택, 파티 리스트, 컨텐츠 입장 UI별 font route 확인
- `PatchRouteVerifier`에 UI scale별 대표 문장/숫자/glyph 검증 추가
- 100%에서 정상인 glyph가 고배율에서 다른 texture cell을 타는지 비교

검증 기준:

- 150%, 200%, 300%에서 한글이 `=`, `--`, 빈칸으로 나오지 않아야 함
- 150%, 200%, 300%에서 문자 간격이 과하게 좁아 서로 겹치지 않아야 함
- 100%에서 정상인 기존 표시가 깨지면 실패로 처리

### 2. 100% UI에서도 문자 사이에 다른 문자가 끼거나 지저분하게 보임

상태: 작업 필요

100% UI에서도 일부 한글 glyph 주변에 다른 문자 조각이 보이거나, glyph cell 경계 밖 잔픽셀이 같이 렌더링되는 문제가 있습니다. 이전에 `변`, `호`, `혼` 등에서 비슷한 증상이 관측되었습니다.

필요 작업:

- 잔픽셀이 보이는 글자의 codepoint와 사용 FDT 확인
- TTMP 원본 glyph와 patched glyph 픽셀 비교
- FDT 좌표/폭/높이/offset/advance가 잘못된 것인지, texture atlas cell 자체가 오염된 것인지 분리
- 문제가 있는 글자만 좁게 수리

검증 기준:

- 특정 글자 주변에 다른 글자 조각이 보이지 않아야 함
- 수리 대상 외 한글 glyph는 TTMP 원본과 픽셀 차이가 없어야 함
- 광범위한 한글 리맵 금지

### 3. 150% 이상 UI에서 문자 간격이 좁아 폰트가 겹쳐 보임

상태: 비로비 큰 UI 라벨은 코드 산출물 verifier PASS, 로비/실사용 사용자 OK 대기

150% 이상 UI, 특히 시스템 설정 창에서 space 또는 문자 간격이 좁아 glyph가 겹쳐 보이는 현상이 있습니다. 4K lobby/in-game FDT의 advance 값, offset, glyph width를 잘못 가져오거나 normalize가 부족한 가능성이 있습니다.

2026-05-09 처리:

- `LobbyScaledHangulPhrases.HighResolutionUiScaleOptions`에 실제 고해상도 UI 옵션 문장을 추가
- `system-settings-mixed-scale-layouts` verifier 추가
- 4K lobby 파생 font에서 Hangul/ASCII를 같은 source에서 무조건 복사하던 방식을 분리
- Hangul advance는 clean CJK median 또는 glyph width 기반 safety advance를 기준으로 보정
- clean target에 없는 ASCII는 파생 source pair의 clean lobby font에서 4px padding과 함께 보충
- `.tmp\mixed-scale-spacing-fix-ja9` 기준 `.tmp\verifier-reported-font-routes-after-fix2.log` PASS

2026-05-11 정정:

- broad `KrnAXIS` lobby Hangul 치환은 제거하고 TTMP source glyph 크기/pixel 보존으로 되돌렸다.
- `AXIS_12/14/18_lobby`는 glyph pixel을 바꾸지 않고 Hangul advance만 필요한 경우 `OffsetX=0`으로 보정한다.
- `lobby-scale-font-sources`, `start-system-settings-uld`, `system-settings-mixed-scale-layouts`, `start-main-menu-phrase-layouts`, `hangul-source-preservation` 기준으로 `.tmp\lobby-ttmp-source-advance-ja` PASS.

2026-05-17 r10 정정:

- 로비 `_lobby.fdt`에 큰 UI visual-scale glyph를 얹는 방식은 폐기했다. atlas 용량 부족과 다른 로비 폰트 route 오염을 만들었으므로, 현재 코드는 비로비 `TrumpGothic_23/34/68.fdt`에만 큰 UI 라벨 visual-scale 보정을 적용한다.
- `.tmp\large-ui-visual-scale-ja-r10`의 PASS 기록은 폐기한다. height-only 검증이라 width/advance가 0.56 수준으로 남는 false positive였다. `.tmp\large-ui-biaxial-scale-ja-r1`은 `TrumpGothic_34 -> TrumpGothic_68` `시스템 설정`의 height/width/advance relative가 `1.072/1.110/1.104`로 통과하며, broad regression도 PASS한다. 이는 코드 검증 결과이며, 사용자 확인 전까지 live ActionDetail/large-label 이슈를 닫지 않는다.

필요 작업:

- 시스템 설정 창에서 쓰이는 FDT 확인
- space glyph와 한글 glyph의 advance 계산 검증
- `OffsetX`/next-character adjustment 처리 방식 재검토
- 4K 파생 FDT에서 negative advance adjustment를 안전하게 처리
- 문장 단위 layout verifier를 시스템 설정 대표 문구에도 추가

검증 기준:

- 시스템 설정 창 대표 문구에서 glyph overlap이 없어야 함
- space 이후 문자가 이전 glyph와 겹치지 않아야 함
- 100/150/200/300% 모두 같은 기준으로 통과해야 함

### 4. 데이터 센터 선택 창 group 표시 문제

상태: 작업 필요

`DATA CENTER SELECT` 화면에서 group이 한글로 나오고 있습니다. 한글로 유지할 경우 폰트가 깔끔하게 나와야 하고, 그렇지 않으면 베이스 클라이언트 언어 그대로 나와야 합니다.

현재 기대 방향:

- 베이스 클라이언트가 일본어면 일본어/글로벌 원본 기준
- 베이스 클라이언트가 영어면 영어/글로벌 원본 기준
- 한글로 표시할 경우에는 `=`, `--`, 겹침, 더러운 glyph 없이 깨끗해야 함

필요 작업:

- group label이 참조하는 sheet/row/column 확정
- `WorldRegionGroup`, `WorldPhysicalDC`, `WorldDCGroupType`, `Lobby`, 관련 `Addon` row의 실제 사용 경로 재검증
- EXD 값이 한글로 들어간 것인지, ULD/lookup이 다른 언어 슬롯을 참조하는지 확인
- 폰트 문제가 아니라 데이터 row 문제인지 분리
- verifier가 실제 group 표시 문자열을 기준으로 실패하게 개선

검증 기준:

- group label이 `--`, `==`, 깨진 문자로 나오면 실패
- 베이스 클라이언트 언어와 섞인 상태가 의도된 정책과 다르면 실패
- 한글로 나오는 정책을 선택할 경우 font pixel/spacing까지 통과해야 함

### 5. 보즈야 입장 시도 시 내용 미번역

상태: 작업 필요

보즈야 입장 시도 시 일부 안내/내용이 번역되지 않고 베이스 클라이언트 언어로 나옵니다.

필요 작업:

- 해당 UI/대사/안내가 어떤 sheet에서 오는지 추적
- `Content*`, `Addon`, `Event*`, `Quest*`, `custom/*` 중 어느 경로인지 확인
- 한국 서버 row가 존재하지만 매핑이 누락된 것인지, 한국 서버에 없는 글로벌 전용 row인지 분리
- 글로벌 전용 row라면 원문 유지가 맞는지, 수동 정책으로 번역 가능한지 판단

검증 기준:

- 한국 서버에 대응 번역이 있는 row라면 한글로 나와야 함
- 한국 서버에 대응 데이터가 없는 글로벌 전용 row라면 베이스 클라이언트 원문 유지가 맞음
- 미번역 row는 diagnostics에 원인과 sheet/row가 남아야 함

### 6. 크레센트 아일랜드/초승달 섬 내부 파티원 크레센트 레벨이 `=`로 나옴

상태: 작업 필요

크레센트 아일랜드 내부에서는 HUD 파티 리스트가 나오지만, 파티원 번호 문제가 아니라 모든 파티원 레벨 옆의 크레센트 레벨 표시가 `=`로 나옵니다. 이전 파티 리스트 본인 번호 `U+E0E1` 문제와 유사하게, 특수 PUA glyph 또는 전용 UI glyph route가 폰트 패치 후 누락된 것으로 추정됩니다.

필요 작업:

- 크레센트 레벨 표시가 사용하는 glyph codepoint 확인
- 해당 glyph가 `Addon` row, ULD text, texture, 또는 PUA font glyph인지 추적
- clean global FDT에서 정상 glyph를 찾고 patched FDT에서 `=` fallback으로 가는 원인 확인
- 파티 리스트 본인 번호처럼 alias가 필요한지, texture 보정이 필요한지 분리
- 파티 리스트 본인 번호 수리와 충돌하지 않도록 별도 보호 규칙 추가

검증 기준:

- 크레센트 레벨 glyph가 `=` fallback과 픽셀 동일하면 실패
- 모든 관련 FDT에서 해당 glyph가 visible이고 clean global 또는 의도한 source와 일치해야 함
- 일반 파티 리스트 번호와 본인 번호 표시가 다시 깨지면 실패

### 7. 스토리 종료 컷씬/이벤트 이미지 설명문이 베이스 클라이언트 언어로 표시됨

상태: 작업 필요

각 스토리 구간이 끝났을 때 컷씬 또는 이벤트 화면 안에 표시되는 설명문/타이틀 카드가 한국어가 아니라 베이스 클라이언트 언어로 표시됩니다. 이 항목은 폰트 FDT나 일반 EXD 문자열만 고쳐서는 해결되지 않을 가능성이 높습니다. 다만 아직 scene 리소스라고 단정하지 않습니다. `EventImage` 같은 sheet 기반 이미지일 수 있으므로, 지역 이동 시 표시되는 이미지형 지역명처럼 실제 표시 리소스가 어디에서 오는지 먼저 판별한 뒤 한국 클라이언트 리소스로 교체해야 합니다.

패치 가능 방향:

- 먼저 해당 설명문이 실제 텍스트 렌더링인지, 이미지형 texture인지 판별
- 현재 `UiPatchGenerator`가 처리하는 `EventImage`, `CutScreenImage`, `ScreenImage`, `DynamicEventScreenImage`, `TradeScreenImage`, `LoadingImage`, `TerritoryType` 후보 중 어느 sheet/row/icon ID를 참조하는지 diagnostics 추가
- global EXD row의 image ID만 그대로 쓰는 방식으로 부족하면, Korean EXD의 같은 row 또는 대응 row가 다른 image ID를 쓰는지 비교
- 언어 폴더형 리소스는 `ui/icon/<folder>/<target-language>/<id>.tex`에 Korean `ko` 리소스를 복사
- 언어 폴더가 없는 리소스는 현재 `EventImage`/`LoadingImage`처럼 동일 path의 global/Korean packed bytes를 비교하고 다를 때만 Korean source를 복사
- sheet 기반으로 잡히지 않으면 event/cutscene bundle, ULD, texture path를 hash 비교 대상으로 확장하고, 안전하게 Korean source로 교체 가능한 최소 파일만 패치

검증 기준:

- story 완료 설명문이 base client 언어로 남으면 실패
- 관련 image ID/path, global hash, Korean hash, output hash가 diagnostics에 남아야 함
- Korean source가 없는 글로벌 전용 리소스면 “원문 유지”로 진단에 남기고 임의 번역/하드코딩하지 않음
- 일반 자막/대사 텍스트와 혼동하지 않고, 이미지형 타이틀/설명문만 image/event resource localization으로 처리

## 검증 체계 개선 필요

상태: 계속 작업 필요

기존 검증은 여러 번 “PASS지만 실제 게임에서 실패”한 사례가 있었습니다. 앞으로는 단순히 glyph가 보이는지만 보지 말고, 실제 source와 픽셀/metrics/layout을 비교해야 합니다.

필요 작업:

- 문제 UI별 실제 FDT route를 먼저 확정
- 대표 문구/숫자/PUA glyph를 verifier에 추가
- TTMP 원본, clean global, patched output을 각각 비교
- `visible > 0`만으로 성공 처리하지 않기
- fallback `=`, `-`, `--`와 픽셀 동일한지 확인
- layout overlap, advance, glyph bbox를 문장 단위로 검증
- generated output과 실제 applied game folder를 모두 검증하는 모드 유지

### 2026-05-09 verifier note

- Data center routed phrase rendering now applies FDT kerning when measuring layout and phrase pixels.
- Data center routed ASCII pixel comparison now covers every `DataCenterWorldmapLabels` entry, including DC groups and world names, not only the previous critical subset.
- Data center routed ASCII texture-neighborhood comparison now covers the 2px padding around every non-space ASCII glyph used by DC groups/world names. Latest ja output passes `.tmp\verifier-dc-texture-padding-fix-ja.log`.
- Re-reported lobby blur/spacing must be checked against the installed game folder, not only generated output. On 2026-05-09 the latest generated ja output passed `data-center-title-uld`, but `--applied-game "D:\SquareEnix\FINAL FANTASY XIV - A Realm Reborn\game"` failed `applied-output-files` for lobby FDT/texture files and failed `data-center-title-uld` texture padding for `AXIS_12_lobby`/`AXIS_14_lobby`.
- 4K lobby font glyph coverage is no longer limited to the fixed representative phrase list. The generator/verifier derive additional start-screen system-settings coverage from Korean Addon rows `4000-4200` and `8683-8722`; `.tmp\lobby-dynamic-glyphs-ja` generated 327 required codepoints and `.tmp\verifier-lobby-dynamic-core.log` passed the focused lobby/start-screen route checks.
- 2026-05-10 lobby blur/overlap follow-up: the earlier generated output still allowed atlas-neighborhood pollution around direct lobby Hangul glyphs and allowed clean lobby ASCII routes with negative visual gaps. The generator now preserves 4px texture neighborhoods for scaled lobby Hangul, reserves lobby glyph neighborhoods during atlas allocation, and clamps lobby ASCII advance/kerning so rendered adjacent glyphs keep at least a 1px visual gap. The verifier now checks texture neighborhoods and strict lobby visual gaps; `.tmp\lobby-spacing-floor1-ja` passes the full verifier in `.tmp\verifier-lobby-spacing-floor1-full.log`.
- 2026-05-10 follow-up correction: the 1px lobby spacing clamp was removed because it over-widened 100% lobby text and did not match the real high-scale lobby source route. The generator now preserves TTMP/source advance and kerning for lobby Hangul, ASCII, numbers, and symbols. Derived 4K lobby fonts route phrase-required ASCII/numeric glyphs through the same paired source font as Hangul, so `Jupiter_90_lobby.fdt` no longer mixes oversized clean `Jupiter_90` digits with `Jupiter_46` Hangul.
- 2026-05-10 verification result: `.tmp\lobby-derived-source-ascii-ja` passes the full verifier in `.tmp\verifier-lobby-derived-source-ascii-full-r4.log`. The verifier now includes start-menu phrase layout, high-scale system-setting phrase layout, mixed Hangul/ASCII source layout comparison, derived 4K lobby source metrics, numeric glyph source routes, and exact Hangul TTMP source preservation.
- 2026-05-10 applied-folder result: `Scripts\generate-and-verify-patch-routes.ps1` generates a fresh patch output and runs verifier checks with `-AppliedGame`. Current smoke run against `D:\SquareEnix\FINAL FANTASY XIV - A Realm Reborn\game` fails `applied-output-files` for 22 lobby FDT files and 3 lobby font texture files, confirming the installed folder has not been updated to the latest lobby font output.
- 2026-05-10 applied-folder closed: `Scripts\generate-and-verify-patch-routes.ps1 -ApplyToAppliedGame` now applies the generated files after backing up the previous target files. Latest ja run applied `.tmp\applied-route-ja-apply` to `D:\SquareEnix\FINAL FANTASY XIV - A Realm Reborn\game`, stored backup files under `.tmp\applied-route-backups\20260510-193548`, and full verifier passed with `--applied-game` in `.tmp\verifier-applied-route-ja-full.log`.
- Latest ja output passes `.tmp\verifier-ja-regression-with-reported-phrases.log` for data-center rows/language slots, start-screen system settings, configuration sharing, Bozja, Occult Crescent support jobs, clean ASCII font routes, 4K lobby phrase/layout, Hangul source preservation, reported in-game Hangul phrases, and lobby Hangul visibility.
- `reported-ingame-hangul-phrases` compares full phrase pixels/layout/metrics against TTMP source for `탐사대 호위대원`, `즉시 발동`, `시전 시간`, `재사용 대기 시간`, and `발동 조건`. This keeps the `호` regression covered without adding a single-character protection rule.
- ULD route checks now also compare text node header bytes and text extra render-state bytes, not only font id/font size. `.tmp\verifier-uld-render-state.log` passed for data center and start-screen system settings candidates.
- `applied-output-files` verifier check compares critical generated font/UI packed files against an installed game folder when `--applied-game <game-dir>` is supplied. Use this before asking for live client confirmation when generated output passes but the installed client still differs.
- `build-release.ps1` now verifies that the release exe embeds the freshly built `FFXIVPatchGenerator.exe` by SHA-256. Generated-output PASS is not enough if the user is applying from a stale `Release\Public\FFXIVKoreanPatch.exe`.
- Release UI reapply guard now allows applying over an already patched client when clean base indexes are available from current index, installed `orig.*` indexes, or any same-version local `restore-baseline` language folder. This prevents stale installed font/UI dat files from surviving after generated output already passes verification.
- 2026-05-11 lobby high-scale source correction: do not mark the lobby 150%+ blur/pixelation issue as complete until the user explicitly OKs it. The broad `KrnAXIS` lobby route was removed because it changed `AXIS_12/14/18/36_lobby` Hangul glyph dimensions/component profiles. The generator now preserves TTMP lobby glyph pixels/sizes and only applies advance-only `OffsetX=0` normalization for `AXIS_12/14/18_lobby` Hangul whose source advance is narrower than glyph width. `.tmp\lobby-ttmp-source-advance-ja` passes focused and broad verifiers in `.tmp\verifier-lobby-ttmp-source-advance-focused-r5.log` and `.tmp\verifier-lobby-ttmp-source-advance-full-r2.log`; the issue remains user-confirmation pending.
- 2026-05-11 verifier correction: `lobby-render-snapshots` now renders representative lobby/system-setting phrases to PNG/TSV and covers non-lobby high-scale candidates (`AXIS_18.fdt`, `KrnAXIS_180.fdt`, `AXIS_36.fdt`, `KrnAXIS_360.fdt`) in addition to `_lobby` fonts. The attempted `.tmp\lobby-axis-advance-ja` global AXIS/KrnAXIS advance fix passed render snapshots but failed `reported-ingame-hangul-phrases`, so it was rejected as in-game font pollution. Non-lobby/global candidates are diagnostic-only until the title/lobby route is isolated. Keep the user-reported lobby high-scale issue open until explicit user OK.
- 2026-05-12 verifier correction: the reported low-route candidates are now enforced, not diagnostic-only. `lobby-render-snapshots` fails `AXIS_12.fdt` at 150% and `AXIS_14.fdt` at 150/200/300% when `minGap < 1`, and records the offending `min_pair`. `.tmp\startscreen-route-kerning-ja-r2` passes the focused check and the broader regression log `.tmp\verifier-startscreen-route-kerning-regression.log`; do not mark the issue complete until explicit user OK.
- 2026-05-12 clean lobby baseline correction: the previous reset only removed lobby glyph recomposition; TTMP `_lobby.fdt` and `font_lobby*.tex` payloads were still being written. The generator now skips lobby FDT/TEX payloads from both TTMP and direct Korean font fallback, leaving clean global lobby font assets in place. New default verifier check `lobby-clean-payloads` requires lobby FDT/TEX to match clean global; `.tmp\lobby-clean-ja` passes `lobby-clean-payloads,clean-ascii-font-routes,hangul-source-preservation,configuration-sharing,bozja-entrance,occult-crescent-support-jobs,action-detail-scale-layouts`.
- 2026-05-12 remaining lobby snapshot failures on the clean baseline: `lobby-render-snapshots` now clearly separates the next work. Clean lobby fonts miss Korean glyphs in `AXIS_12/14/18/36_lobby` (`설정을 변경했습니다.`, `150%(FHD)...`), while `Jupiter_20_lobby` and `TrumpGothic_23_lobby` still show clean-source English spacing failures. Non-lobby `AXIS_12/14` high-scale system-setting candidates still fail `minGap >= 1`. Do not call the lobby font issue fixed until a new minimal route-specific Korean glyph plan passes snapshots and the user confirms.
- 2026-05-12 default verifier correction: because the clean baseline intentionally lacks lobby Korean glyph injection and also exposes clean-source data-center visual-gap failures, `data-center-title-uld`, `data-center-worldmap-uld`, `start-system-settings-uld`, `system-settings-mixed-scale-layouts`, and `start-main-menu-phrase-layouts` are not part of the default generate-and-verify smoke check for now. They remain covered by the failing `lobby-render-snapshots` open issue until the new minimal injection route exists.
- 2026-05-12 clean-client observation: clean lobby fonts do not visibly blur/overlap in the live client, so absolute alpha-bound `minGap` failures in verifier are not a valid fix target by themselves. Verifier now treats lobby ASCII/number spacing as differential: patched output may have negative alpha bounds only when it is no worse than clean global for the same phrase/font route. `.tmp\verify-clean-render-relative.log` now marks `Jupiter_20_lobby` and `TrumpGothic_23_lobby` data-center English cases as clean-baseline OK; remaining failures are Korean glyph missing/high-scale Korean layout only.
- 2026-05-12 lobby Hangul reinjection attempt: generator now keeps clean lobby FDT/ASCII metrics and injects only required Hangul glyph cells into selected lobby fonts (`AXIS_*_lobby`, data-center popup/menu fonts). Existing lobby glyph cells are reserved before allocation, so Korean cells are written into newly allocated atlas regions instead of overwriting clean English/number glyphs. The verifier was updated to validate this route by checking clean ASCII preservation, data-center ULD routes, start-screen system settings routes, and render snapshots. Latest ja output `.tmp\lobby-hangul-injection-ja-r4` passes that focused set with `Verifier failures: 0`. This is still pending live-client confirmation and must not be treated as user-confirmed fixed yet.

- 2026-05-12 lobby follow-up: user confirmed data center and system settings are OK, but reported start lobby menu glyph fallback (`게임 시작` etc. shown as `-`) and character-select fallback (`로스가-`, `--`, `-자`, `지고= 거리`). Generator now splits lobby Hangul coverage into general lobby, start-main-menu high-scale fonts, and reported character-select phrases, and reuses already-patched lobby glyph cells when rebuilding over a patched client so atlas capacity is not consumed repeatedly. Verifier default now includes `start-main-menu-phrase-layouts` and `character-select-lobby-phrase-layouts`; `.tmp\lobby-main-char-ja-r5` passes focused ja verification with `Verifier failures: 0`. This is still pending live-client confirmation and must not be treated as user-confirmed fixed yet.

- 2026-05-13 lobby follow-up correction: the previous verifier checked representative start-menu fonts but did not follow the live title menu ULD route. The live route is `ui/uld/Title_Menu.uld` node `0x1D8` -> `common/font/MiedingerMid_18_lobby.fdt`, so `게임 시작`/`데이터 센터`/`동영상 및 타이틀`/`설정`/`라이선스`/`종료` could still fall back to `-` even when `Jupiter_*_lobby` checks passed. `MiedingerMid_18_lobby.fdt` is now included in start-main-menu Hangul coverage, and `start-main-menu-phrase-layouts` follows ULD routes before passing. Character-select verification now also follows the actual `CharaSelect_DKT_Select.uld`, `CharaSelect_DKT_YesNo.uld`, `CharaSelect_Info.uld`, and `CharaSelect_WorldServer.uld` routes instead of only checking a static font list. Focused ja route verification `.tmp\lobby-main-menu-route-ja-r3` passes with `Verifier failures: 0`. This is still pending live-client confirmation and must not be treated as user-confirmed fixed yet.

- 2026-05-13 verifier false-pass correction: the previous focused route PASS was invalid because `CompositeArchive` could read generated font entries from fallback/base dat when the patched output reused the same index offset. The verifier now resolves sibling `orig.*.index` as baseline and reads primary generated font dat when the offset exists there. This made the old output fail for missing `Jupiter_45_lobby` start-menu glyphs and `Jupiter_20_lobby` character-select glyphs, then the route-scoped coverage update passed on a fresh output.

- 2026-05-13 current ja result: fresh `.tmp\lobby-route-scoped-ja-r4` passes `clean-ascii-font-routes,numeric-glyphs,data-center-title-uld,data-center-worldmap-uld,start-system-settings-uld,start-main-menu-phrase-layouts,character-select-lobby-phrase-layouts,lobby-render-snapshots,hangul-source-preservation,configuration-sharing,bozja-entrance,occult-crescent-support-jobs,reported-ingame-hangul-phrases,action-detail-scale-layouts` with `Verifier failures: 0`. `lobby-clean-payloads` is no longer the pass/fail check for this design because route-scoped Hangul injection intentionally modifies selected lobby FDT/TEX; keep the live lobby 150%+ issue open until the user explicitly confirms it.

- 2026-05-15 lobby atlas correction rejected: allocating required Hangul into the existing lobby atlas and then expanding affected clean lobby textures to fit TTMP source coordinates failed live-client review and made the 로비 150%+ glyph/blur/spacing issue worse. `lobby-hangul-source-cells` passing was a false success criterion for the wrong architecture. Do not use source-cell grafting or texture expansion as the next fix path.
- 2026-05-15 new lobby font direction: treat overflow as a complete font-set/page-count problem. A valid 로비 fix must keep `*_lobby.fdt` glyph `image_index` values consistent with actual `font_lobby1..N.tex` pages, keep glyph rectangles inside texture bounds, and reject any patched lobby texture width/height expansion. Follow `Docs/LOBBY_FONT_REWORK_PLAN.md` before editing 로비 font code.
- 2026-05-15 multi-texture + kerning follow-up: `.tmp\lobby-multitex-kerning-ja-r2` keeps clean lobby ASCII metric/kerning as baseline, allocates TTMP Hangul alpha into valid `font_lobby3..6.tex` pages, and adds only start-screen system-settings kerning pairs collected from the high-resolution/result-message phrase lists. `clean-ascii-font-routes` allows the generated `AXIS_14/KrnAXIS_140` `0/%` pair as a start-screen exception, while other ASCII glyph/kerning routes must still match clean. Focused verifiers pass, but the 로비 150%+ issue remains open until user confirmation.
- 2026-05-15 supplemental high-scale lobby follow-up: `.tmp\lobby-supplemental-full-ja-r1` writes supplemental clean-lobby FDT payloads for derived high-scale routes that were not present in the TTMP payload list. The generator now resolves the matching high-scale TTMP source FDT before copying Hangul glyph alpha, copies a padded alpha region to avoid atlas-neighborhood blur, and keeps source advance/offset values instead of widening lobby Hangul. Full font/text verification and reported in-game regression verification pass, but the live 로비 150%+ issue stays open until the user explicitly OKs it.
- 2026-05-15 split-baseline ULD verification: verifier CLI/scripts now support separate clean baseline game paths for text/font/ui. This prevents `start-system-settings-uld` and data-center ULD checks from reading clean font indexes against a different global dat set. `.tmp\lobby-kerning-axis12-split-ja-r1` passes split-baseline `start-system-settings-uld`, lobby layout snapshots, clean ASCII/numeric preservation, and reported in-game regression checks. The issue remains open until user confirmation.

## 작업 시 주의 사항

- 인게임 폰트가 정상인 상태에서 로비 문제를 고치기 위해 인게임 폰트를 광범위하게 건드리지 말 것
- 로비 문제를 고치기 위해 `TrumpGothic` 또는 `Jupiter` 전체 한글 glyph를 대량 리맵하지 말 것
- generator 로그에서 일반 로비 폰트의 artifact-prone remap이 수백 개 단위로 나오면 잘못된 방향일 가능성이 큼
- 한 글자 문제는 해당 글자 codepoint와 FDT를 특정한 뒤 좁게 수리할 것
- “한글로 나오게 만들기”보다 “베이스 클라이언트 원문 유지가 안전한 글로벌 전용 UI인지” 먼저 판단할 것
- 커밋/푸시/릴리즈 배포는 사용자가 명시적으로 요청했을 때만 진행할 것
- 2026-05-16 route-scoped multi-texture lobby result: `.tmp\lobby-routed-multitex-ja-r8` passes focused lobby checks and the strong regression set `lobby-render-snapshots,clean-ascii-font-routes,lobby-multitexture-font-set,reported-ingame-hangul-phrases,ingame-ttmp-texture-neighborhoods,action-detail-scale-layouts,protected-hangul-glyphs`. The generator prioritizes high-scale system-setting/result-message Hangul, compares data-center ASCII spacing against clean baseline instead of an absolute alpha gap, and reserves 2px around global in-game Hangul cells before ASCII allocation. Keep the live lobby issue open until the user explicitly OKs it.
- 2026-05-16 full-sheet lobby result: route-scoped coverage can miss unreported lobby strings, so the accepted candidate is `.tmp\lobby-fullsheet-sharedsource-pad0-ja-r3`. It covers full `Lobby/Error/Addon/ClassJob/Race/Tribe/GuardianDeity` Hangul coverage, excludes `CharaMakeName`, shares canonical Hangul source glyphs across compatible lobby fonts to avoid duplicate atlas use, and keeps lobby texture dimensions unchanged. Focused lobby checks and strong in-game/configuration regression checks passed, and the user confirmed the lobby fonts render correctly overall after the release build.
- 2026-05-23 lobby allocator correction: source-codepoint fallback reuse is rejected for low-scale lobby Hangul because the same codepoint may have different target dimensions or visual-scale state. Use exact allocation-key reuse only, or allocate a new isolated cell. Direct clean-empty source-cell writes must mark atlas occupancy immediately. `.tmp\lobby-safe-realloc-ja-r2` passes focused lobby safety/source checks and the confirmed in-game regression group, with `font_lobby7.tex` and `image_index >= 24` still forbidden.
- 2026-05-25 font-only mixed-state issue: applying "한글 폰트 패치" after a full patch can leave Korean `0a0000/060000` text/UI installed while replacing only `000000` fonts with the minimal font-only payload. This can reintroduce lobby/character-select `=`/`-` fallback and may be related to the HD UI resolution crash path. Font-only apply must restore clean text/UI indexes first and must not be verified with `font-runtime-glyph-bounds` alone.
# 2026-05-30 verifier guard

- For the current HD/4K lobby and in-game font objective, a focused crash PASS is not enough.
- Run `Scripts\verify-hd-crash-release.ps1` with `-RunFullFontObjective` before treating a release as code-level ready.
- The new mode deliberately splits heavy checks into logged chunks to avoid timeout/noise hiding failures while still covering lobby scale routes, 4K lobby phrases, in-game critical fonts, reported in-game phrases, source preservation, and TTMP texture neighborhoods.
- Mixed Korean/ASCII phrase overlap checks must not fail solely on ASCII pairs that already overlap in clean fonts. They may pass only when the patched mixed phrase is no worse than the clean ASCII-only baseline for the same font.
- When `-AppliedGame` is used, `applied-output-files` must pass before running applied route/runtime checks. If the installed game folder differs from the generated output, later route failures are stale/mixed-state evidence only and must not be used to reject the current generator.
- Use `Scripts\apply-generated-output.ps1` when the exact verified `.tmp` output must be applied to the game folder. Do not regenerate in that path unless the task is explicitly to create a new output; otherwise the applied-state comparison no longer proves that the already-verified output is installed.
- A release rebuild that produces the same SHA256 still needs the release timestamp recorded if crash-log gating is part of the evidence. The crash-log check compares logs against `Release\Public\FFXIVKoreanPatch.exe` last-write time, not just commit hash.
