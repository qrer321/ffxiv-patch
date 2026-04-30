# FFXIVPatchGenerator

## 원작자에 대한 감사

이 제너레이터는 FFXIV 한글 패치 원작자인 [korean-patch](https://github.com/korean-patch)의 작업과 공유받은 기존 제너레이터 구현을 참고해 확장했습니다. EXH/EXD 파싱, sheet 순회, 문자열 row 매핑, 폰트 패치 대상 구성의 기반을 만들어주신 원작자에게 감사드립니다.

## 역할

`FFXIVPatchGenerator`는 UI에서 호출되는 콘솔형 패치 생성기입니다. 한국 서버 클라이언트의 한글 텍스트/폰트 리소스를 읽어 글로벌 서버 클라이언트의 일본어 또는 영어 언어 슬롯에 적용할 release 파일을 만듭니다.

이 프로그램은 원본 글로벌/한국 서버 게임 폴더에 쓰지 않습니다. 모든 결과물은 `--output`으로 지정한 폴더 아래에만 생성됩니다.

## 입력

필수 입력:

- 글로벌 서버 클라이언트 `game` 폴더
- 한국 서버 클라이언트 `game` 폴더
- release 출력 폴더

주요 원본 파일:

- `sqpack\ffxiv\0a0000.win32.index`
- `sqpack\ffxiv\0a0000.win32.index2`
- `sqpack\ffxiv\0a0000.win32.dat*`
- `sqpack\ffxiv\000000.win32.index`
- `sqpack\ffxiv\000000.win32.index2`
- `sqpack\ffxiv\000000.win32.dat*`
- `ffxivgame.ver`

## 출력

텍스트 패치 출력:

```text
0a0000.win32.dat1
0a0000.win32.index
0a0000.win32.index2
orig.0a0000.win32.index
orig.0a0000.win32.index2
ffxivgame.ver
patch-diagnostics.tsv
```

폰트 패치 포함 시 추가 출력:

```text
000000.win32.dat1
000000.win32.index
000000.win32.index2
orig.000000.win32.index
orig.000000.win32.index2
```

UI가 release 폴더를 적용할 때는 별도로 `manifest.json`을 생성해 적용 파일의 크기와 SHA1을 기록합니다.
`--diagnostic-csv <sheet>`를 지정하면 `diagnostic-csv\` 폴더에 sheet별 비교 CSV가 추가로 생성됩니다.

## 텍스트 패치 방식

- 글로벌 `exd/root.exl`을 기준으로 sheet 목록을 순회합니다.
- 글로벌 EXH 구조를 기준으로 대상 언어 EXD를 재생성합니다.
- 대상 언어는 기본 `ja`이며 `--target-language en`으로 영어 클라이언트 슬롯도 지정할 수 있습니다.
- 한국 서버 `*_ko.exd`에서 문자열 컬럼의 SeString 바이트만 가져옵니다.
- 문자열 key가 있는 sheet는 `TEXT_...` 형태의 string key로 row를 매핑합니다.
- string key가 안정적이지 않은 일부 sheet는 명시된 allowlist에 한해 row id 기반 fallback을 사용합니다.
- `Addon` sheet에서는 한글이 없는 짧은 숫자/기호/SeString UI 토큰을 치환하지 않고 글로벌 원본 값을 유지합니다. 파티 리스트 번호처럼 별도 glyph 경로를 타는 UI 요소가 한국 서버 토큰으로 바뀌어 깨지는 상황을 줄이기 위한 보호 로직입니다.
- `ExcelVariant.Default` sheet만 처리합니다.
- `ExcelVariant.Subrows` sheet는 아직 스킵하고 `patch-diagnostics.tsv`에 `unsupported-subrows`로 기록합니다.

## 진단과 정책 파일

기본 생성물에는 `patch-diagnostics.tsv`가 포함됩니다. 이 파일에는 sheet/page별 처리 상태, 패치 row 수, string-key/row-id 매칭 수, RSV 잔존 수가 기록됩니다.

추가 진단이 필요하면 `--diagnostic-csv <sheet>`를 사용합니다. 지정한 sheet에 대해 글로벌 문자열, 한국 서버 문자열, 실제 선택된 문자열, 매핑 방식, row/column 정책 적용 여부를 CSV로 확인할 수 있습니다.

선택적으로 `--policy <json>` 또는 실행 파일 옆 `patch-policy.json`으로 외부 보정 정책을 적용할 수 있습니다. 지원하는 항목은 다음과 같습니다.

- `delete_files`: sheet 전체 스킵
- `row_key_fallback_files`: string key가 없는 sheet의 row-id fallback 허용. `*`, `?` wildcard를 사용할 수 있습니다.
- `keep_rows`, `delete_rows`: 특정 row를 글로벌 원본으로 유지
- `keep_columns`, `delete_columns`: 특정 문자열 column을 글로벌 원본으로 유지
- `remap_keys`: 대상 row id가 참조할 한국 서버 source row id 지정
- `remap_columns`: 대상 column이 참조할 한국 서버 source column offset 지정, 또는 `G`/`GLOBAL`/`KEEP`으로 글로벌 원본 유지

정책 파일 예시는 `patch-policy.example.json`을 참고하면 됩니다.

## 폰트 패치 방식

폰트 패치는 `000000` common 패키지의 `common/font` 리소스를 대상으로 합니다.

기본 방식:

- `TTMPD.mpd`
- `TTMPL.mpl`

위 TTMP 패키지를 실행 파일 옆 또는 `FontPatchAssets` 폴더에서 찾아 사용합니다. 이 방식이 기본이며, 글로벌 클라이언트에서 누락 글리프가 나오는 문제를 피하기 위해 권장됩니다.

실험용 fallback:

- `--allow-korean-font-fallback`

TTMP 파일이 없을 때 한국 서버 클라이언트의 폰트 리소스를 직접 복사합니다. 이 방식은 `--`처럼 글리프가 누락될 수 있어 실사용 release에는 권장하지 않습니다.

진단용 폰트 프로필:

- `--font-profile full`
- `--font-profile ui-numeric-safe`
- `--font-profile no-miedingermid`
- `--font-profile no-trumpgothic`
- `--font-profile no-jupiter`
- `--font-profile no-axis`
- `--font-profile fdt-only`
- `--font-profile textures-only`

기본값은 `full`입니다. FDT 파일은 TTMP/KR glyph 좌표와 texture를 유지하되, 숫자/ASCII UI glyph lookup에 필요한 Shift_JIS 문자 코드를 글로벌 원본 또는 ASCII 값으로 복구합니다. 나머지 프로필은 파티 리스트 숫자처럼 특정 UI glyph가 깨질 때 원인이 되는 폰트군을 찾기 위한 진단용입니다. UI에서는 테스트 빌드에서만 선택할 수 있습니다.

## 안전장치

- `--output`이 글로벌/한국 서버 원본 game 폴더 내부면 중단합니다.
- 글로벌/한국 서버 `ffxivgame.ver`가 다르면 중단합니다. `--allow-version-mismatch`는 진단용으로만 사용합니다.
- 기본 index/index2가 이미 `dat1` 엔트리를 포함하면 중단합니다.
- 이미 패치된 index를 기준으로 release를 만들려면 `--allow-patched-global`이 필요하지만, 이 옵션은 실험용입니다.
- 실제 배포용 release는 clean index 또는 UI가 확보한 복구용 original index를 `--base-index`, `--base-index2`, `--base-font-index`, `--base-font-index2`로 지정하는 방식을 권장합니다.
- 생성되는 `orig.*.index/index2`는 패치 제거 시 원본 index 참조로 되돌리기 위한 복구 파일입니다.
- 수정된 index/index2는 파일 세그먼트 Adler32 checksum을 다시 계산해 저장합니다.

## 옵션

```text
--global <dir>                  글로벌 서버 클라이언트 game 폴더
--korea <dir>                   한국 서버 클라이언트 game 폴더
--output <dir>                  release 출력 폴더
--target-language <code>        글로벌 클라이언트 대상 언어 슬롯, 기본 ja
                                UI에서는 ja/en만 선택합니다.
--source-language <code>        원본 언어 슬롯, 기본 ko
                                한국 서버 기반 패치에서는 ko를 사용합니다.
--sheet <name>                  테스트용 단일 sheet 제한
--policy <file>                 JSON 패치 정책 파일
--diagnostic-csv <sheet>        지정 sheet의 row/column 비교 CSV 출력
--base-index <file>             clean 0a0000.win32.index 지정
--base-index2 <file>            clean 0a0000.win32.index2 지정
--include-font                  텍스트와 폰트 패치를 함께 생성
--font-only                     폰트 패치 파일만 생성
--font-pack-dir <dir>           TTMPD.mpd/TTMPL.mpl 위치 지정
--font-profile <name>           진단용 폰트 프로필, 기본 full
--base-font-index <file>        clean 000000.win32.index 지정
--base-font-index2 <file>       clean 000000.win32.index2 지정
--allow-patched-global          이미 dat1을 가리키는 index 사용 허용, 실험용
--allow-korean-font-fallback    TTMP 누락 시 한국 서버 폰트 직접 복사 허용, 실험용
--allow-version-mismatch        글로벌/한국 서버 버전 불일치 허용, 진단용
```

언어 코드는 FFXIV EXD 언어 id가 있는 `ja`, `en`, `de`, `fr`, `chs`, `cht`, `ko`를 인식합니다. 현재 UI에서 노출하는 대상 언어는 일본어 `ja`와 영어 `en`입니다.

## 빌드

```powershell
.\build.ps1
```

기본 출력:

```text
bin\Release\FFXIVPatchGenerator.exe
```

## 실행 예

```powershell
.\bin\Release\FFXIVPatchGenerator.exe `
  --global "D:\SquareEnix\FINAL FANTASY XIV - A Realm Reborn\game" `
  --korea "E:\FINAL FANTASY XIV - KOREA\game" `
  --target-language ja `
  --include-font `
  --output "E:\codex\release-ja"
```

clean index를 명시하는 예:

```powershell
.\bin\Release\FFXIVPatchGenerator.exe `
  --global "D:\SquareEnix\FINAL FANTASY XIV - A Realm Reborn\game" `
  --korea "E:\FINAL FANTASY XIV - KOREA\game" `
  --target-language ja `
  --include-font `
  --base-index "E:\codex\clean\0a0000.win32.index" `
  --base-index2 "E:\codex\clean\0a0000.win32.index2" `
  --base-font-index "E:\codex\clean\000000.win32.index" `
  --base-font-index2 "E:\codex\clean\000000.win32.index2" `
  --output "E:\codex\release-ja"
```

## UI와의 연동

UI는 제너레이터 stdout에서 다음 prefix를 감지해 진행도를 표시합니다.

```text
@@FFXIVPATCHGENERATOR_PROGRESS|<percent>|<message>
```

사용자가 UI에서 전체/폰트 패치를 누르면 UI가 자동으로 출력 폴더를 만들고, 필요한 clean index를 찾아 제너레이터에 전달한 뒤 생성된 release 파일을 적용합니다.
