# 프로젝트 작업 내용과 기능 정리

이 문서는 현재 프로젝트가 무엇을 하는지, 어떤 기능이 들어갔는지, 각 구성 요소가 어떤 책임을 가지는지 정리합니다.

## 프로젝트 목적

이 프로젝트는 한국 서버 클라이언트의 한글 텍스트, 폰트, UI 리소스를 이용해 글로벌 서버 클라이언트의 일본어/영어 슬롯에 적용할 한글 패치 release 파일을 로컬에서 생성하고, WinForms UI에서 설치/제거/복구까지 제어하는 도구입니다.

기존 원격 release 다운로드형 패처 UI를 기반으로 하되, 현재 구조는 다음 흐름을 목표로 합니다.

1. 사용자가 UI에서 글로벌 서버 클라이언트와 한국 서버 클라이언트 경로를 확인한다.
2. UI가 clean/original index와 게임 버전을 사전 점검한다.
3. UI가 내장된 `FFXIVPatchGenerator`를 실행해 release 파일을 로컬에 생성한다.
4. 생성된 release 파일을 글로벌 클라이언트에 적용한다.
5. 필요하면 `orig.*.index/index2` 또는 로컬 백업으로 원복한다.

## 주요 구성 요소

### `FFXIVPatchUI`

사용자가 실행하는 WinForms 패처입니다.

- 글로벌/한국 서버 클라이언트 경로 자동 탐색
- 경로 수동 지정
- 베이스 클라이언트 언어 선택
- 사전 점검 자동 실행
- 전체 한글 패치 실행
- 한글 폰트 패치 실행
- 한글 패치 제거
- 백업으로 복구
- 작업 결과/오류/로그 표시
- 제너레이터 진행도 표시
- 테스트 빌드 전용 디버그 적용 흐름 제공

릴리즈 빌드에서는 실제 글로벌 클라이언트 적용 버튼이 보이고, 테스트 빌드에서는 실제 적용을 막고 `debug-apply` 경로로만 작업하도록 방어합니다.

### `FFXIVPatchGenerator`

UI에서 내부 실행되는 콘솔형 release 생성기입니다.

- `0a0000` 텍스트 패치 생성
- `000000` 폰트 패치 생성
- `060000` UI 텍스처 패치 생성
- clean/original index 기반 release 생성
- `index`와 `index2` 동시 갱신
- 새 dat 파일 생성
  - `0a0000.win32.dat1`
  - `000000.win32.dat1`
  - `060000.win32.dat4`
- 복구용 `orig.*.index/index2` 출력
- `ffxivgame.ver` 출력
- `patch-diagnostics.tsv` 출력
- UI progress prefix 출력

### `Tools\PatchRouteVerifier`

생성된 release 폴더가 의도한 패치 경로를 타는지 확인하는 검증 도구입니다.

- 데이터센터 관련 EXD row 확인
- 좁은 UI 시간 단위 확인
- 숫자 glyph 확인
- 파티 리스트 본인 번호 glyph 확인
- 로비/대사 문장 glyph dump 생성
- TTMP 원본 폰트와 로비 한글 glyph 픽셀 비교
- 4K/고배율 로비 폰트 파생 경로 검증

현재는 사용자가 게임을 켜기 전에 최대한 문제를 잡기 위한 보조 검증 도구 역할입니다.

## 텍스트 패치 기능

- 글로벌 `root.exl` 기준 sheet 순회
- 글로벌 EXH 구조 기준으로 대상 언어 EXD 재생성
- 한국 서버 `*_ko.exd`에서 문자열 컬럼의 SeString 바이트 추출
- 글로벌 대상 언어 슬롯 `*_ja.exd` 또는 `*_en.exd`에 문자열 반영
- string key 기반 row 매핑
- 일부 allowlist sheet의 row-id fallback
- `patch-policy.json` 기반 row/column 보존과 remap 지원
- `Addon`, `AddonTransient`의 짧은 UI 토큰 보호
- SeString macro/lookup 구조가 다를 때 안전 병합
- 병합이 불가능하면 글로벌 원본 구조 유지
- `quest/*`의 `TEXT_*_SAY_*` 입력 문구 익명화 코드는 보존하되, 시트 커버리지가 불완전하므로 현재 옵션은 비활성화/no-op으로 둠
- `_rsv_` 토큰 통계 수집
- diagnostic CSV 출력 지원

## 폰트 패치 기능

- TTMP 패키지(`TTMPD.mpd`, `TTMPL.mpl`) 기반 폰트 패치
- TTMP의 FDT와 texture atlas 조합 유지
- 한국 서버 폰트 직접 복사는 실험용 fallback으로만 허용
- 파티 리스트 본인 번호 PUA glyph 보정
- 인게임 숫자/큰 숫자 glyph 경로 보정
- 로비 한글 glyph의 특정 오염 셀 수리
- 150/200/300% 로비 UI에서 쓰이는 4K lobby font 파생 처리
- 폰트 프로필 기반 진단 빌드 지원

폰트 쪽은 깨짐 영향 범위가 넓으므로, 인게임 폰트와 로비 폰트의 수정 범위를 분리해서 관리해야 합니다.

## UI 텍스처 패치 기능

- `060000` UI 패키지 release 생성
- 파티 리스트 본인 번호 관련 UI texture 보정
- `ScreenImage` 언어별 이미지 보정
- `CutScreenImage` 타이틀 이미지 보정
- `TerritoryType` 지역 타이틀/부제 이미지 보정
- `Map` 지도 texture 보정
- `DynamicEventScreenImage`, `EventImage`, `TradeScreenImage`, `LoadingImage` 일부 보정

텍스트가 아니라 이미지로 렌더링되는 지역명, 컨텐츠 진입 이미지, 지도 표기 등을 보정하기 위한 기능입니다.

## 백업과 복구 기능

- 실제 적용 전 대상 파일 백업
- 패치 제거 전 현재 index/index2 백업
- `restore-baseline`에 clean/original index 보관
- `orig.*.index/index2` 기반 패치 제거
- 사용자가 직접 sqpack 폴더에 붙여넣을 수 있는 수동 롤백 여지 유지
- 백업 가능한 파일이 없으면 작업 확인 팝업 전에 안내
- `dat1`/`dat4` 파일이 남아 있어도 index가 참조하지 않으면 제거된 상태로 처리

## 빌드와 배포 기능

- `Scripts\build-release.ps1`
  - 릴리즈 빌드 생성
  - `Release\Public\FFXIVKoreanPatch.exe` 출력
- `Scripts\build-test.ps1`
  - 테스트 빌드 생성
  - 실제 글로벌 클라이언트 적용 방어
- `Scripts\verify-patch-routes.ps1`
  - release 산출물 후검증
- `Scripts\publish-release.ps1`
  - GitHub Release용 exe asset, SHA256, 릴리즈 노트 생성
  - `-Publish` 지정 시에만 실제 배포

릴리즈 배포는 zip이 아니라 `FFXIVKoreanPatch.exe` 단일 파일 업로드를 목표로 합니다.

## 현재 안정화된 것으로 보는 영역

- 전체 한글 패치 release 생성 흐름
- clean/original index 기반 생성과 복구 기준 관리
- `index2` 포함 적용/제거/복구 흐름
- 파티 리스트 본인 번호 PUA glyph 기본 경로 보정
- 인게임 기본 폰트의 주요 한글 표시
- 인게임 큰 숫자 glyph 일부 보정
- 로비 한글 glyph 대량 리맵 방지
- TTMP 원본 대비 로비 한글 glyph 픽셀 비교 검증

## 별도 문서

- [FEATURES.md](FEATURES.md): 구현 기능 전수 조사
- [KNOWN_ISSUES.md](KNOWN_ISSUES.md): 현재 안 되는 내용과 작업 예정 항목
- [SESSION_HANDOFF_CONSTRAINTS.md](SESSION_HANDOFF_CONSTRAINTS.md): 다음 세션 인수인계용 제약 사항
- [RELEASE.md](RELEASE.md): 릴리즈 빌드/배포 관련 메모
