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
- `sqpack\ffxiv\060000.win32.index`
- `sqpack\ffxiv\060000.win32.index2`
- `sqpack\ffxiv\060000.win32.dat*`
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

UI 텍스처 패치 출력:

```text
060000.win32.dat4
060000.win32.index
060000.win32.index2
orig.060000.win32.index
orig.060000.win32.index2
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
- `Addon`, `AddonTransient` sheet에서는 한글이 없는 짧은 숫자/기호/SeString UI 토큰을 치환하지 않고 글로벌 원본 값을 유지합니다. 파티 리스트 번호처럼 별도 glyph 경로를 타는 UI 요소가 한국 서버 토큰으로 바뀌어 깨지는 상황을 줄이기 위한 보호 로직입니다.
- `Addon`, `AddonTransient`의 SeString macro/lookup 구조가 글로벌 row와 한국 row 사이에서 달라지는 경우에는 글로벌 payload/lookup 구조를 유지하고, 안전하게 분리 가능한 한국어 literal만 병합합니다. 병합이 불가능한 row는 글로벌 원본 구조를 유지해 데이터 센터 이동처럼 한국 클라이언트에 없는 글로벌 전용 lookup이 사라져 `--` 또는 깨진 glyph로 보이는 문제를 막습니다.
- 데이터 센터 선택 화면에서 사용하는 `Lobby` row 중 한국 서버에 값이 있는 `800`, `802`, `803`, `806`은 한국어로 반영합니다. 한국 서버에 없거나 비어 있는 `Lobby` row `801`, `804`, `805`와 `WorldRegionGroup` row `1`~`8`, `WorldPhysicalDC` row `1`~`8`, `WorldDCGroupType` row `1`~`32`, `Addon` row `12510`~`12538`은 대상 글로벌 언어 원문을 유지합니다.
- 데이터 센터/파티 리스트처럼 로비 또는 공용 UI가 선택 언어 외의 글로벌 언어 슬롯을 참조할 수 있는 row는 `ja/en/de/fr` 슬롯을 함께 보정합니다. 예를 들어 `Addon` row `10952`는 모든 글로벌 언어 슬롯에서 ASCII `1`로 고정합니다.
- `Addon` row `44`, `45`, `49`는 기본 내장 정책으로 글로벌 원본을 유지합니다. 이 row들은 글로벌 클라이언트에서 `h`, `m`, `s`처럼 좁은 영역용 시간 단위로 쓰이며, 한국어 `시간`, `분`, `초`로 바뀌면 핫바/아이콘 타이머 같은 UI에서 텍스트가 영역 밖으로 넘칠 수 있습니다.
- `Addon` row `2338`, `6166`은 SeString 내부 길이값을 깨지 않도록 글로벌 영어 템플릿을 사용합니다. 버프/남은시간 UI에서 `시간`, `분`이 좁은 영역 밖으로 나가는 문제를 줄이기 위한 예외입니다.
- `Addon` row `10952`는 파티 리스트 본인 표시 glyph가 한글 폰트 적용 후 `=`로 보이는 문제를 피하기 위해 모든 글로벌 언어 슬롯에서 ASCII `1`로 고정합니다. 또한 본인 표시 번호를 1~8로 바꾸는 설정을 고려해 `AXIS_12`/`AXIS_12_lobby`에는 본인 번호 전용 PUA glyph `U+E0E1`~`U+E0E8`, `U+E0B1`~`U+E0B8`을 ASCII `1`~`8` glyph 좌표로 alias합니다.
- 데이터센터 화면의 한글 proxy glyph 방식은 FDT/텍스처 atlas 불일치 시 읽을 수 없는 글자로 노출될 수 있어 릴리즈 기본값에서 제외했습니다.
- `ExcelVariant.Default` sheet만 처리합니다.
- `ExcelVariant.Subrows` sheet는 아직 스킵하고 `patch-diagnostics.tsv`에 `unsupported-subrows`로 기록합니다.

## 진단과 정책 파일

기본 생성물에는 `patch-diagnostics.tsv`가 포함됩니다. 이 파일에는 sheet/page별 처리 상태, 패치 row 수, string-key/row-id 매칭 수, RSV 잔존 수가 기록됩니다.

추가 진단이 필요하면 `--diagnostic-csv <sheet>`를 사용합니다. 지정한 sheet에 대해 글로벌 문자열, 한국 서버 문자열, 실제 선택된 문자열, 매핑 방식, row/column 정책 적용 여부를 CSV로 확인할 수 있습니다.

선택적으로 `--policy <json>` 또는 실행 파일 옆 `patch-policy.json`으로 외부 보정 정책을 적용할 수 있습니다. 지원하는 항목은 다음과 같습니다.

외부 정책 파일이 없어도 일부 안전 정책은 기본 내장됩니다. 현재는 `Addon` row `44`, `45`, `49`를 글로벌 원본으로 유지하고, row `2338`, `6166`은 글로벌 영어 시간 템플릿을 사용해 좁은 UI 시간 단위가 `1시간`, `32분`처럼 넘치는 상황을 줄입니다. row `10952`는 파티 리스트 본인 표시 glyph 보정용으로 ASCII `1`을 사용합니다. 데이터센터 선택 화면은 한국 서버에 실제 값이 있는 `Lobby` 안내 row만 한국어로 반영하고, 글로벌 전용 lookup row는 대상 글로벌 언어 row를 사용합니다.

- `delete_files`: sheet 전체 스킵
- `row_key_fallback_files`: string key가 없는 sheet의 row-id fallback 허용. `*`, `?` wildcard를 사용할 수 있습니다.
- `keep_rows`, `delete_rows`: 특정 row를 글로벌 원본으로 유지
- `keep_columns`, `delete_columns`: 특정 문자열 column을 글로벌 원본으로 유지
- `global_target_rows`: 특정 row를 대상 글로벌 언어 원본으로 유지. `--target-language ja`면 일본어 row, `en`이면 영어 row를 사용합니다.
- `global_english_rows`: 특정 row를 글로벌 영어 원본으로 유지
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

기본값은 `full`입니다. 폰트 패치는 TTMP 패키지의 FDT와 texture를 한 세트로 유지합니다. 릴리즈 기본값에서는 전역 glyph alias를 추가하지 않지만, 파티 리스트 본인 번호가 `=`로 보이는 문제를 피하기 위해 `AXIS_12`/`AXIS_12_lobby`의 본인 번호 PUA glyph만 `1`~`8`로 좁게 alias합니다. 이 alias는 FDT의 UTF-8 key 저장 방식과 Shift-JIS key를 함께 맞춰, 기존 영문/일문/한글 glyph atlas를 섞지 않도록 제한합니다. 나머지 프로필은 특정 UI glyph가 깨질 때 원인이 되는 폰트군을 찾기 위한 진단용입니다. UI에서는 테스트 빌드에서만 선택할 수 있습니다.

## UI 텍스처 패치 방식

UI 텍스처 패치는 `060000` UI 패키지를 대상으로 하며 새 `060000.win32.dat4`를 만듭니다. 원본 `dat0`에는 쓰지 않고, 수정된 `index/index2`만 새 `dat4`를 참조하게 만듭니다.

현재 포함되는 대상:

- `ui/uld/PartyListTargetBase.tex`: 파티 리스트에서 본인을 표시하는 번호/glyph 텍스처 차이를 보정합니다.
- `ScreenImage` 언어별 이미지: `exd/screenimage.exh`와 `screenimage_*.exd`에서 `Lang` 플래그가 켜진 이미지 ID를 읽고, 글로벌 대상 언어 폴더(`ja` 또는 `en`)의 `ui/icon/...` 파일을 한국 서버 `ko` 이미지로 교체합니다.
- `CutScreenImage` 언어별 이미지: 지역 이동, 던전/컨텐츠 진입, 컷신 전환에서 쓰이는 타이틀 이미지 ID를 읽어 같은 방식으로 한국 서버 `ko` 이미지를 복사합니다.
- `TerritoryType` 언어별 이미지: 필드 지역 진입 시 표시되는 지역 타이틀 이미지 ID를 읽어, 저지/중부 라노시아처럼 `PlaceName` 문자열과 별도로 렌더링되는 이미지형 지역명을 한국 서버 `ko` 이미지로 보정합니다. 지역명 아래에 표시되는 `+2000` 계열 부제 이미지도 함께 복사합니다.
- `Map` 지도 텍스처: `Map.Id`에서 `ui/map/.../*_m.tex` 경로를 계산하고 한국 서버 텍스처와 다를 때 복사합니다. 지도 이미지 자체에 포함된 일본어 지역명/표기까지 보정하기 위한 처리입니다.
- `DynamicEventScreenImage`, `EventImage`, `TradeScreenImage`, `LoadingImage`: 언어 폴더가 없는 이미지형 UI 리소스는 글로벌과 한국 서버의 동일 경로 파일을 비교하고, 실제 바이트가 다른 경우에만 한국 서버 리소스로 교체합니다.
- ULD 텍스트 노드의 폰트 슬롯은 원본 글로벌 클라이언트 값을 유지합니다. 폰트 슬롯을 강제로 바꾸면 `AXIS_20_lobby`처럼 TTMP 패키지에 없는 크기/로비용 폰트 경로를 타면서 데이터 센터 화면의 한글이 `=`로 보일 수 있기 때문입니다.
- 데이터 센터/월드 이동 화면은 한국 서버에 실제 값이 있는 `Lobby` 안내 row만 한국어로 반영하고, `WorldRegionGroup`, `WorldPhysicalDC`, `WorldDCGroupType`, `Addon` 데이터센터 안내 row는 대상 글로벌 언어 원문을 유지합니다. 이 화면의 한글 proxy glyph 방식은 읽을 수 없는 글자로 노출될 수 있어 릴리즈 기본값으로 사용하지 않습니다.

이 처리는 지역/컨텐츠 입장 시 표시되는 타이틀처럼 텍스트가 아니라 이미지로 렌더링되는 요소를 보정하기 위한 처리입니다.

## 안전장치

- `--output`이 글로벌/한국 서버 원본 game 폴더 내부면 중단합니다.
- 글로벌/한국 서버 `ffxivgame.ver`가 다르면 중단합니다. `--allow-version-mismatch`는 진단용으로만 사용합니다.
- 기본 index/index2가 이미 `dat1` 엔트리를 포함하면 중단합니다.
- 이미 패치된 index를 기준으로 release를 만들려면 `--allow-patched-global`이 필요하지만, 이 옵션은 실험용입니다.
- 실제 배포용 release는 clean index 또는 UI가 확보한 복구용 original index를 `--base-index`, `--base-index2`, `--base-font-index`, `--base-font-index2`, `--base-ui-index`, `--base-ui-index2`로 지정하는 방식을 권장합니다.
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
--base-ui-index <file>          clean 060000.win32.index 지정
--base-ui-index2 <file>         clean 060000.win32.index2 지정
--skip-ui-texture-fix           060000 UI 텍스처 패치 생성 제외
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
