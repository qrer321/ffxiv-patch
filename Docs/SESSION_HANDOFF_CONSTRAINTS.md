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
