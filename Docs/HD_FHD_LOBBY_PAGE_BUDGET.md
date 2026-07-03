# HD/FHD 로비 폰트 텍스처 페이지 예산

2026-06-12 분석으로 확정한 비-4K UI 해상도 크래시의 동작 모델과 안전 규칙을 기록한다.
로비 폰트/atlas 코드를 바꾸기 전에 이 문서를 먼저 읽는다.

## 런타임 모델

- 클라이언트는 UI 해상도 설정에 따라 로비 폰트 세트와 `font_lobby*.tex` 로드 수가 달라진다.
  - HD/FHD: 소형 로비 폰트 세트만 로드하고, 텍스처는 `font_lobby1/2`(페이지 0,1)만 로드한다.
  - QHD/4K: 대형 로비 폰트 세트(`AXIS_36_lobby` 등)까지 로드하고, 텍스처 6장을 전부 로드한다.
- 로드되지 않은 페이지를 참조하는 glyph가 렌더되면 `Component::GUI::AtkFontAnalyzerRenderer`에서
  access violation이 난다. 크래시 덤프 레지스터 `R10`이 범위를 벗어난 페이지 인덱스다.
  (수집된 덤프는 전부 `R10=2`, `RAX=R15=0xFFF`.)

## clean FDT별 페이지 사용 (2026.05 클라이언트 기준)

- 페이지 {0}: `AXIS_12/14_lobby`, `Jupiter_16/20/23_lobby`, `Meidinger_16/20_lobby`,
  `MiedingerMid_10/12/14/18_lobby`
- 페이지 {1}: `AXIS_18_lobby`, `Jupiter_45_lobby`, `TrumpGothic_184/23/34_lobby`
- 페이지 {2}: `Jupiter_46/90_lobby`, `Meidinger_40_lobby`, `MiedingerMid_36_lobby`,
  `TrumpGothic_68_lobby`
- 페이지 {2,3,4,5}: `AXIS_36_lobby`

HD/FHD에서 로드되는 폰트들의 clean 페이지 합집합이 정확히 {0,1}이고, 상위 페이지는
4K 세트 폰트만 참조한다.

## 안전 규칙 (생성기와 verifier가 동일하게 강제)

- clean FDT가 페이지 0/1 안에 들어가는 폰트(HD-runtime 폰트):
  패치 glyph route는 페이지 0/1만 허용한다.
- clean FDT가 상위 페이지를 참조하는 폰트(QHD/4K 세트 폰트):
  해당 폰트가 사용 가능한 시점에는 6페이지가 모두 로드되어 있으므로 페이지 0~5를 허용한다.
- `font_lobby7.tex` 등 합성 페이지 추가와 `image_index >= 24`는 계속 금지한다.
- 용량이 부족하면 페이지를 넓히지 말고 clean kana/CJK 셀 reclaim
  (`AddLobbyCleanCellReclaimPools`)과 allocation cache 공유로 해결한다.

구현 위치:

- `FontPatchGenerator.IsLobbyPageAllowedForPatchedHangul` — 최종 게이트.
  fresh allocation, allocation cache 재사용, high-scale fallback 재사용, placement cell
  경로 모두 이 게이트를 통과해야 FDT entry가 쓰인다.
- `FontPatchGenerator.BuildLobbyHangulAllocationCandidates` — 폰트별 할당 후보 텍스처.
- `PatchRouteVerifier`의 `lobby-multitexture-font-set` —
  `IsPatchedGlyphOutsideRuntimePageBudget`이 clean baseline 대비 route가 바뀐 모든
  glyph(한글뿐 아니라 ASCII/PUA 포함)에 예산을 검증한다.

## 크래시 증거와의 대조

크래시가 확인된 산출물은 전부 HD-runtime 폰트의 패치 한글이 페이지 2+에 있었다.

- `20260528-003930`: `AXIS_12_lobby` → page 2 (시작 화면 크래시)
- r33 `.tmp\release-hd-crash-head-ja`: `AXIS_14/18_lobby` → page 2
- r34 `.tmp\release-hd-crash-r34-ja`: `TrumpGothic_23_lobby` → page 2,3 (캐릭터 선택/설정 진입 크래시)
- `20260531-214231`: `TrumpGothic_23_lobby` → page 2
- retry(6/3) 빌드: `AXIS_12/18_lobby` 등 → page 3,4,5 (font_lobby3만 피한 것으로는 부족)
- 6/8 main 빌드: `AXIS_12_lobby` 등 → page 2~5

반면 4K 세트 폰트(Jupiter_46/90 등)의 한글이 페이지 4/5에 있던 산출물에서 QHD/4K
크래시는 보고되지 않았다. 과거의 "font_lobby3(page2) 전역 금지"는 이 규칙의 부분
관측이었고, page2가 위험했던 것이 아니라 HD-runtime 폰트가 페이지 2+로 나간 것이
위험했던 것이다.

## 폰트 텍스처 packed 레이아웃 (2026-06-12 실측 확정)

clean 클라이언트의 type-4 폰트 텍스처 패킹은 **LOD locator 1개 + 16000바이트 서브블록
N개** 구조다 (`font_lobby2.tex` 실측: blockCount(0x14)=1, locator={offset=80(텍스처 헤더
크기), size=실제 저장 총 바이트, rawLen=텍스처 데이터 크기, blockIndex=0,
subBlockCount=132}, 이어서 uint16 서브블록 저장 크기 테이블).

- 헤더 0x10(UsedNumberOfBlocks) = ceil((텍스처헤더 + 저장총량)/128). `font_lobby1/2`,
  `font1`에서 공식 일치 확인.
- 과거 5/28 수정("서브블록당 locator 1개, blockCount=132")은 이 포맷을 오독한 것이다.
  그 레이아웃은 크래시는 안 나지만 비-4K 로더의 LOD/블록 읽기가 어긋나 **폰트 픽셀이
  통째로 깨진다** (2026-06-12 사용자 실기기 보고: HD/FHD 크래시 해소 후 폰트 전면 깨짐).
- 원래 main 레이아웃은 구조는 맞았지만 locator size 필드에 저장총량 대신
  rawSize+서브블록헤더 합을 기록한 것이 결함이었다.
- 현재 `PackTextureFile`은 clean 구조를 그대로 재현한다. 검증은
  `.tmp\hd-fhd-analysis\DumpPackedTex.exe`로 clean과 산출물의 locator 필드를 직접 비교한다.

## 2026-06-12 코드 레벨 검증 결과

기준 산출물 `.tmp\hd-fhd-clean-page-budget-ja-r2` (브랜치 `crash/hd-fhd-clean-page-routes`):

- 14개 대상 로비 폰트 전부 907/907 한글 할당, allocation failure 0, fallback 강등 0.
- `AnalyzeFdtPages` 페이지 예산 위반 0: HD 폰트는 {0,1}만, 4K 폰트는 {2,5}만 사용.
- `verify-hd-fhd-lobby-crash-gate.ps1`: positive PASS + known-bad 4종
  (r33, r34, retry 빌드, 2026-06-08 main 빌드) 전부 새 verifier에서 거부 확인.
- 회귀 PASS: `lobby-multitexture-font-set, lobby-source-cell-conflicts,
  lobby-render-snapshots, lobby-coverage-glyphs, lobby-hangul-visibility,
  lobby-large-label-scale-layouts, lobby-scale-font-sources, start-system-settings-uld,
  start-main-menu-phrase-layouts, data-center-rows, hangul-source-preservation,
  reported-ingame-hangul-phrases, ingame-ttmp-texture-neighborhoods,
  action-detail-scale-layouts, pvp-profile-font-routes, protected-hangul-glyphs,
  party-list-self-marker, ingame-clean-ascii-glyphs, third-party-game-font-safety,
  combat-flytext-damage-glyphs`.
- 다음 4개 검사는 r2와 2026-06-08 main 산출물이 동일하게 실패한다. 이 브랜치의
  회귀가 아니라 별도 추적 중인 기존 이슈다:
  `data-center-title-uld`(clean 자체의 strict ASCII minGap),
  `4k-lobby-font-derivations`(Jupiter_46_lobby 파생 소스 높이 50 vs 70),
  `4k-lobby-phrase-layouts`(Jupiter_90_lobby 고배율 ASCII 미주입 — fixes 브랜치 전용 기능),
  `numeric-glyphs`(MiedingerMid_10 TTMP 라인높이 17 vs clean 14, 픽셀 동일).
- 라이브 상태: 사용자 클라이언트 확인 전까지 크래시 해결로 단정하지 않는다.
  확인 항목 — HD/FHD에서 ① 시작 화면 진입, ② 시스템 설정 진입(배율 100~300% 변경 포함),
  ③ 캐릭터 선택 진입, ④ QHD/4K에서 동일 항목 회귀 없음.

## 진단 도구

`.tmp\hd-fhd-analysis\AnalyzeFdtPages.exe` (소스 동봉, csc로 빌드):

```powershell
.\AnalyzeFdtPages.exe --clean-index <orig.000000.win32.index> --clean-dats <게임 sqpack\ffxiv> `
  --out label=<산출물 폴더> [--out ...] --report fdt-pages.tsv
```

clean/산출물별로 폰트당 사용 페이지, 패치 한글 페이지, 예산 위반 수를 덤프한다.

## 2026-06-12 2차 수정 (실기기 폰트 깨짐 보고 반영)

- reclaim 정책: 가나는 절대 회수하지 않고, 한자는 "생성 산출물의 로비 시트 최종
  텍스트(베이스 언어 유지 행 포함)에 등장하지 않는 글자"만 회수한다. 가시 CJK는
  `CollectLobbyVisibleCjkCodepoints`가 대상 언어(ja/en) 기준으로 수집하므로 영어
  베이스 클라이언트에서도 동일하게 동작한다(EN 텍스트엔 CJK가 없어 회수 풀이 커짐).
- TrumpGothic_23/34_lobby 커버리지 = 비주얼 스케일 부분집합(LargeLabels[∪Character])
  으로 일치시켜 한 라벨 안에 큰/작은 글자가 섞이는 문제를 제거. 비주얼 소스에 없는
  글자는 베이스 소스를 동일 배율로 스케일링한다.
- 생성 mip은 max 필터 대신 박스 평균 필터를 사용한다(HD/FHD mip 샘플링에서 글리프
  주변 흐릿한 사각 테두리 완화).
- `lobby-coverage-glyphs`는 디스플레이 라벨 폰트(TG_23/34_lobby)에 화면 전체
  커버리지를 요구하지 않는다(라벨 검증은 lobby-large-label-scale-layouts 담당).
- 기준 산출물 `.tmp\hd-fhd-clean-page-budget-ja-r5`: 할당 실패 0, 예산 위반 0,
  reclaim 희생 글리프 전부 비가시 한자, crash gate PASS + known-bad 거부,
  회귀 16종 PASS.

## 2026-06-13 3차 수정 (실기기 흐릿한 사각 테두리 / 150% 제목 과대 보고 반영)

### 가드 링 모델 (테두리의 근본 원인)

- bilinear 샘플링은 글리프 쿼드 가장자리에서 최대 0.5텍셀 바깥을 읽는다.
  glyph rect 바로 바깥 1px 링에 잉크가 있으면 그 픽셀이 최대 50% 가중치로
  섞여 **글리프 쿼드 윤곽을 따라 흐릿한 사각 테두리**가 보인다
  (캐릭터 선택 라벨 보고의 정체).
- clean 아틀라스는 CJK 셀 rect끼리 0~1px로 맞닿아 있고, **가드 마진은 rect
  내부**에 있다(잉크가 모서리에서 1~2px 안쪽). 측정: `MinRectGap.exe` —
  clean AXIS_12/14/18_lobby 한자 6357셀 전부 rect 간격 0.
- 따라서 패치 글리프도 같은 불변식을 지켜야 한다:
  **렌더되는 rect 바깥 1px 링은 항상 잉크 0**.

### 생성기 강제 사항

- 프레시 할당(`FontGlyphRepairContext.TryAllocate`, 로비 텍스처 한정):
  (w+2)x(h+2) 외곽 블록을 할당하고 내부 (X+1,Y+1)에 glyph를 써서 링을
  블록 안에 포함시킨다.
- reclaim 셀: 셀 전체를 클리어(옛 한자 AA가 rect 모서리까지 닿는 경우가
  있음)하고 잉크는 `PadGlyphAlpha`로 1px 안쪽에 쓴다. best-fit은 셀이
  glyph보다 2px 이상 커야 하고, FDT rect는 셀 원점+1로 이동한다.
- 풀(비가시 한자) 셀 rect는 등록 시 allocator에 선점유 표시한다 —
  프레시 셀의 링이 풀 셀의 빈 마진에 들어갔다가 나중에 그 셀이 reclaim되며
  잉크가 차는 간섭을 차단.
- 확장 reclaim 블록은 ① 자신이 덮는 풀 셀 외 영역에 점유(잉크/할당 마크)가
  없어야 하고(`HasOccupiedAreaOutsideRects`) — r7에서 확장 블록이 먼저
  할당된 프레시 셀 위를 덮어 글리프가 파괴되는 사례를 실측으로 확인 —
  ② 사용 시 블록 전체를 allocator에 점유 표시하며 ③ 보호 셀(가나·가시
  한자·ASCII 등 클린이 계속 렌더하는 모든 셀, 비대상 폰트 포함)과 1px
  링까지 겹치면 거부한다.

### verifier / 도구

- `lobby-multitexture-font-set`에 가드 링 검사 추가: 패치된 로비 한글
  glyph rect 바깥 1px 링에 잉크가 있으면 FAIL.
- `.tmp\hd-fhd-analysis\RingAudit.exe` — 산출물 전체 한글 글리프 링 전수
  감사(독립 도구). r5는 위반 2,024건(TG_23/34 207/207 전부 포함),
  수정 후 0건이어야 한다.
- `.tmp\hd-fhd-analysis\LocateCell.exe` — 특정 텍스처 좌표를 참조하는
  글리프 역추적(중첩 사고 분석용).

### 150%+ 로비 제목 크기 (TG_34_lobby)

- 150%+ 배율에서 제목/라벨은 TG_34_lobby를 네이티브로 렌더하지만 베이스
  언어(JP/EN)는 TG_34_lobby에 가나가 없어 AXIS_18_lobby의 ~1.5배 스케일
  (~21.6px 유효)로 표시된다. 비주얼 스케일 1.00은 한글을 베이스 대비
  약 1.5배로 만들어 제목이 옆 보조 문자를 침범했다(3차 실기기 보고).
- `LobbyLargeLabelVisualScaleSpec`의 TG_34_lobby 비율을 0.85(최소 0.80)로
  내리고, verifier `lobby-large-label-scale-layouts`에
  `start-system-config-title-34` 케이스를 추가, TG_34 픽셀 높이 상한을
  30.5px로 강화해 이전 과대 상태(30.6~35px)를 거부한다.

### PvP 프로필 폰트 (fixes 브랜치 포팅)

- Jupiter_16/20 PvP 라벨(전적/크리스탈라인 컨플릭트 등)은 TTMP 소스 크기
  보존 시 ULD 영역보다 커 보인다. fixes 브랜치의 visible-pixel crop 후
  다운스케일 구현을 포팅(`PvpProfileVisualScaleGlyphs.TargetFontPaths`
  활성화, HangulToDigitRatio 1.03, 보정 1.0). verifier
  `pvp-profile-font-routes`는 대상 폰트에 대해 digit-height 비율
  1.16..1.42 창을 강제(소스 보존 검사 대체).
- 다운스케일은 의도된 소스 변경이므로 소스-보존 계열 3개 체크에 fixes
  브랜치의 예외를 함께 포팅했다. 예외 집합은 생성기와 동일한 레시피
  (FallbackPhrases + PvP 시트 + Addon 행 범위 − 전투 플라이텍스트 보존
  문구)로 계산한다: ① `hangul-source-preservation` /
  `ingame-ttmp-texture-neighborhoods`는 집합 내 코드포인트의 Jupiter_16/20
  변경을 intentional로 스킵, ② `reported-ingame-hangul-phrases`는
  `IsExpectedPvpProfileVisualScaleRepair`(글리프 수 동일, overlap ≤ 소스+3,
  폭 비율 0.45..1.05, 픽셀 ≥ 소스의 25%)를 만족하면 수용.

### r9 산출물 검증 결과 (2026-07-03)

- 생성: 전 폰트 완전 할당 907/907(TG_23/34 커버리지 207/207 포함),
  할당 실패 0, 생성기 경고 0.
- `RingAudit.exe`: 로비 한글 11,298 글리프 가드 링 위반 0
  (r5는 2,024건 — TG_23/34 207/207 전부 포함).
- `AnalyzeFdtPages.exe`: HD/FHD 페이지 예산 위반 0, CELL-SHARING 4쌍
  모두 907/907, RECLAIM-OVERWRITE 희생은 전부 비가시 한자.
- 전체 20-체크 verifier 회귀 PASS (`.tmp\r9-regression4.log`): FAIL 0,
  PvP visual-scale 수용 20문구, intentional neighborhood 스킵 719건.
- `start-system-config-title-34` 실측: [시스템 설정] 픽셀 높이 27.6
  (창 24.0..30.5), source-height/width/advance 비율 0.958/0.945/1.033.
- 크래시 게이트(`verify-hd-fhd-lobby-crash-gate.ps1`) PASS: r9 통과 +
  known-bad 4종(release-hd-crash-head-ja, release-hd-crash-r34-ja,
  hd-fhd-current-main-ja, hd-fhd-retry-current-ja) 전부 verifier 거부.
