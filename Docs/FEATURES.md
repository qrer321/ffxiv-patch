# 기능 전수 조사

이 문서는 현재 코드 기준으로 UI, 제너레이터, 빌드/배포 스크립트에 구현된 기능을 정리합니다.

## UI 기능

### 경로 관리

- 글로벌 서버 클라이언트 경로 자동 탐색
  - Steam uninstall registry
  - Square Enix uninstall registry
  - 알려진 설치 경로 fallback
- 한국 서버 클라이언트 경로 자동 탐색
  - 알려진 한국 서버 설치 경로 fallback
- 글로벌/한국 서버 클라이언트 경로 수동 지정
- `ffxiv_dx11.exe`를 선택해도 `game` 폴더로 보정
- 현재 설정된 경로를 UI에 표시
- 테스트 빌드 전용 경로 자동 탐색 버튼
- 테스트 빌드 전용 경로 리셋 버튼
- 테스트 빌드 전용 폰트 프로필 선택

### 언어 선택

- 베이스 클라이언트 언어 선택
  - 일본어 클라이언트 `ja`
  - 영어 클라이언트 `en`
- 언어 변경 시 사전 점검 상태를 다시 필요 상태로 변경
- 선택한 언어 슬롯에 맞는 `*_ja.exd` 또는 `*_en.exd` release 생성

### 사전 점검

- 프로그램 시작 시 사전 점검 자동 실행
- 글로벌 클라이언트 필수 파일 확인
- 한국 서버 클라이언트 필수 파일 확인
- `ffxivgame.ver` 확인
- 글로벌/한국 서버 `ffxivgame.ver` 불일치 확인
- TTMP 폰트 패키지 확인
- `0a0000.win32.index/index2` 상태 확인
- `000000.win32.index/index2` 상태 확인
- dat1 참조 여부 확인
- clean/original index 복구 기준 생성 또는 발견
- 릴리즈 빌드에서 한글 채팅용 Scancode Map 레지스트리 확인 및 설치
- 사전 점검 결과를 별도 대화상자로 표시
- 실패/주의/전체 항목 수 표시
- 사전 점검 로그 저장
- 사전 점검 실패 시 실제 패치 버튼 잠금

### 패치 적용

- 전체 한글 패치
  - 텍스트 패치 생성
  - 폰트 패치 생성
  - 생성된 release 파일 적용
- 한글 폰트 패치
  - 폰트 패치만 생성
  - 생성된 font release 파일 적용
- 제너레이터 진행도를 UI progress bar에 표시
- 적용할 release 파일의 `manifest.json` 생성
- 패치 완료 후 프로그램을 종료하지 않음
- 작업 결과를 별도 대화상자로 표시
- 작업 결과 대화상자에 제너레이터 요약 표시
- 작업 로그 저장
- 생성 폴더 열기
- 로그 폴더 열기

### 백업과 복구

- 실제 패치 적용 전 대상 파일 백업
- 패치 제거 전 현재 index/index2 백업
- 백업으로 복구 전 현재 대상 파일 재백업
- 수동 롤백용 sqpack 파일 패키지 생성
- `restore-baseline`에 clean/original index 보관
- `한글 패치 제거`로 `orig.*.index/index2` 또는 clean index 복구
- `백업으로 복구`로 백업 폴더 선택 후 복구
- 복구 대상에 index2 포함
- 백업 가능한 파일이 없으면 복구 확인 팝업 전에 안내
- dat1 파일이 남아도 index가 참조하지 않으면 제거된 상태로 처리

### 안전장치

- 릴리즈 빌드에서는 FFXIV 실행 중 실제 패치 중단
- 테스트 빌드에서는 FFXIV 실행 중이어도 테스트 작업 허용
- 테스트 빌드에서는 실제 글로벌 클라이언트 적용 버튼 차단
- 테스트 빌드는 `debug-apply` 폴더에만 적용
- 테스트 빌드에서는 폰트 프로필로 특정 폰트군 제외 패치를 생성해 UI glyph 깨짐 원인을 분리 가능
- 사전 점검을 통과하지 않으면 실제 전체/폰트 패치 차단
- 글로벌/한국 서버 버전이 다르면 실제 전체/폰트 패치 차단
- 이미 패치된 index/index2가 감지되면 실제 전체/폰트 패치 차단
- 출력 폴더가 원본 게임 폴더 내부면 제너레이터에서 중단
- 원본 글로벌/한국 서버 게임 폴더에는 release 생성물을 직접 쓰지 않음

### 정리 기능

- 오래된 생성 release 파일 정리
- 로그 폴더 열기
- 생성 폴더 열기
- `%LocalAppData%\FFXIVKoreanPatch` 아래에 생성물/백업/로그 분리 보관

### UI 빌드 차이

- 릴리즈 빌드
  - 실제 적용 버튼 표시
  - 경로 자동 탐색/리셋 테스트 버튼 숨김
  - 테스트 자동 패치 버튼 숨김
  - FFXIV 실행 중 실제 패치 차단
- 테스트 빌드
  - 실제 적용 버튼 숨김
  - 경로 자동 탐색/리셋 버튼 표시
  - 폰트 프로필 선택 표시
  - 테스트 자동 패치 버튼 표시
  - 실제 글로벌 클라이언트 대신 `debug-apply` 사용

## 제너레이터 기능

### 공통

- 콘솔 실행 지원
- UI progress prefix 출력
- 글로벌/한국 서버 game 폴더 입력
- release 출력 폴더 입력
- 출력 폴더가 원본 game 폴더 내부인지 검사
- `ffxivgame.ver` 복사
- 작업 통계 출력
  - 스캔한 sheet 수
  - 패치한 EXD page 수
  - 패치한 row 수
  - string-key row 수
  - row-key fallback row 수
  - 보호한 UI 토큰 수
  - RSV 포함 row/string 수
  - mapping 누락 page 수
  - 원본/대상 page 누락 수
  - 미지원 sheet 수
  - 패치한 font 파일 수

### 텍스트 패치

- 글로벌 `exd/root.exl` 기준 sheet 순회
- 글로벌 EXH 구조 기준 EXD 재생성
- 한국 서버 `*_ko.exd`에서 문자열 컬럼 SeString 바이트 추출
- 글로벌 대상 언어 `*_ja.exd` 또는 `*_en.exd`에 문자열만 반영
- Default variant EXD 처리
- Subrows variant sheet 스킵
- Subrows variant sheet를 `unsupported-subrows`로 진단
- string key 기반 row 매핑
- 일부 allowlist sheet의 row id fallback
- `row_key_fallback_files` 정책으로 row id fallback 대상 sheet 외부 확장
- `Addon` sheet의 짧은 숫자/기호/SeString UI 토큰 보호
- `patch-policy.json` 기반 sheet/row/column 보존과 row/column remap
- `patch-diagnostics.tsv` 생성
- `--diagnostic-csv` 지정 sheet의 row/column 비교 CSV 생성
- `_rsv_` 토큰이 남은 row/string 수 집계
- unsafe sheet 스킵
- 새 `0a0000.win32.dat1` 생성
- 수정된 `0a0000.win32.index` 생성
- 수정된 `0a0000.win32.index2` 생성
- 수정된 index/index2 파일 세그먼트 Adler32 checksum 갱신
- 복구용 `orig.0a0000.win32.index` 생성
- 복구용 `orig.0a0000.win32.index2` 생성

### 폰트 패치

- `--include-font`으로 텍스트+폰트 동시 생성
- `--font-only`로 폰트만 생성
- TTMP 패키지 우선 사용
  - `TTMPD.mpd`
  - `TTMPL.mpl`
- `--font-pack-dir`로 TTMP 위치 지정
- `--font-profile`로 진단용 폰트 프로필 선택
  - `full`
  - `ui-numeric-safe`
  - `no-miedingermid`
  - `no-trumpgothic`
  - `no-jupiter`
  - `no-axis`
  - `fdt-only`
  - `textures-only`
- TTMP 누락 시 기본 실패
- `--allow-korean-font-fallback`으로 한국 서버 폰트 직접 복사 허용
- 새 `000000.win32.dat1` 생성
- 수정된 `000000.win32.index` 생성
- 수정된 `000000.win32.index2` 생성
- 복구용 `orig.000000.win32.index` 생성
- 복구용 `orig.000000.win32.index2` 생성

### clean index 처리

- `--base-index`
- `--base-index2`
- `--base-font-index`
- `--base-font-index2`
- 지정하지 않으면 설치된 글로벌 index 사용
- base index 옆의 matching index2 자동 탐색
- dat1 엔트리가 있는 index는 기본 중단
- `--allow-patched-global`로 실험용 진행 허용

### CLI 옵션

- `--global`: 글로벌 서버 클라이언트 game 폴더
- `--korea`: 한국 서버 클라이언트 game 폴더
- `--output`: release 출력 폴더
- `--target-language`: 대상 글로벌 언어 슬롯, 기본 `ja`, UI 노출 대상은 `ja`/`en`
- `--source-language`: 원본 언어 슬롯, 기본 `ko`, 한국 서버 기반 패치에서는 `ko` 사용
- `--sheet`: 테스트용 단일 sheet 제한
- `--include-font`: 텍스트와 폰트 동시 생성
- `--font-only`: 폰트만 생성
- `--font-pack-dir`: TTMP 패키지 위치 지정
- `--font-profile`: 진단용 폰트 프로필 선택, 기본 `ui-numeric-safe`
- `--base-index`, `--base-index2`: 텍스트 패치용 clean index 지정
- `--base-font-index`, `--base-font-index2`: 폰트 패치용 clean index 지정
- `--allow-patched-global`: 이미 패치된 index 사용 허용, 실험용
- `--allow-korean-font-fallback`: TTMP 없이 한국 서버 폰트 직접 복사, 실험용
- `--policy`: JSON 패치 정책 파일
- `--diagnostic-csv`: 지정 sheet의 row/column 비교 CSV 출력
- `--allow-version-mismatch`: 글로벌/한국 서버 버전 불일치 허용, 진단용

## 빌드와 배포 기능

### 릴리즈 빌드

- `Scripts\build-release.ps1`
- Release 구성 빌드
- `Release\Public` 생성
- 배포용 파일 복사
- 오래된 updater 파일 제거

### 테스트 빌드

- `Scripts\build-test.ps1`
- Debug + `TEST_BUILD` 구성 빌드
- `Release\Test` 생성
- 테스트 실행 파일명을 `FFXIVKoreanPatch.Test.exe`로 변경

### GitHub 배포 보조

- `Scripts\publish-github.ps1`
- 소스 커밋과 바이너리 릴리즈를 분리하는 구조
- `Release` 폴더는 Git 커밋 대상에서 제외

## 현재 제한

- EXD Default variant 중심으로 처리합니다.
- Subrows variant sheet는 아직 패치하지 않고 진단 파일에만 기록합니다.
- 폰트 패치 실사용은 TTMP 패키지 포함을 전제로 합니다.
- clean index 또는 복구용 original index가 없으면 이미 패치된 글로벌 index를 배포용 base로 쓰지 않습니다.
- `--allow-patched-global`, `--allow-korean-font-fallback`, `--allow-version-mismatch`는 실험/진단용입니다.
