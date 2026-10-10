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

UI가 release 폴더를 적용할 때는 별도로 `manifest.json`을 생성해 선택한 패키지·원본 index·버전 파일의 크기와 SHA1, `uiAssets` 결과를 기록합니다. 출력 폴더에 남아 있는 미선택 패키지는 포함하지 않습니다.
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
- 데이터 센터 선택/이동 화면에서 사용하는 `Lobby` row `800`~`806`, `WorldRegionGroup` row `1`~`8`, `WorldPhysicalDC` row `1`~`8`, `WorldDCGroupType` row `1`~`32`는 글로벌 클라이언트 전용 로비 UI에 해당하므로 한국 서버 row로 바꾸지 않고 대상 글로벌 언어 row를 유지합니다. `Addon` row `12514`, `12525`도 글로벌 전용 안내/상태 row로 유지하지만, `ワールド間テレポ` 계열 row `12510`, `12511`, `12520`, `12524`, `12537`은 한섭 `서버 텔레포` 번역을 적용합니다.
- 데이터 센터/파티 리스트처럼 로비 또는 공용 UI가 선택 언어 외의 글로벌 언어 슬롯을 참조할 수 있는 row는 `ja/en/de/fr` 슬롯을 함께 보정합니다. 예를 들어 `Addon` row `10952`는 대상 글로벌 언어의 원본 PUA 토큰을 유지합니다.
- `Addon` row `44`, `45`, `49`는 기본 내장 정책으로 글로벌 원본을 유지합니다. 이 row들은 글로벌 클라이언트에서 `h`, `m`, `s`처럼 좁은 영역용 시간 단위로 쓰이며, 한국어 `시간`, `분`, `초`로 바뀌면 핫바/아이콘 타이머 같은 UI에서 텍스트가 영역 밖으로 넘칠 수 있습니다.
- `Addon` row `876`, `2338`, `6166`은 SeString 내부 길이값을 깨지 않도록 글로벌 영어 템플릿을 사용합니다. 버프/남은시간 UI에서 `시간`, `분`이 좁은 영역 밖으로 나가는 문제를 줄이기 위한 예외입니다.
- `Addon` row `10952`는 파티 리스트 본인 표시 glyph가 한글 폰트 적용 후 `=`로 보이는 문제를 피하기 위해 대상 글로벌 언어의 원본 PUA 토큰을 유지합니다. 또한 본인 표시 번호를 1~8로 바꾸는 설정을 고려해 패치되는 각 FDT에는 본인 번호 전용 PUA glyph `U+E0E1`~`U+E0E8`을 같은 FDT의 박스형 번호 glyph `U+E0B1`~`U+E0B8` 좌표로 alias합니다. 인스턴스/legacy circled marker로 쓰이는 `U+E0B1`~`U+E0B8` 자체는 alias 소스 모양을 유지한 채 clean cell/base+mip 보호 대상으로 검증합니다. 추가 PUA는 수동 나열 대신 각 보호 route에서 clean/source와 patched target 양쪽에 존재하는 glyph를 자동 수집해 같은 방식으로 보호합니다.
- `--anonymize-quest-chat-phrases`는 현재 비활성화/no-op입니다. `quest/*` sheet 커버리지가 불완전하므로 기존 익명화 구현은 feature gate 뒤에 보존하고, UI 전체 패치에서는 더 이상 자동으로 켜지지 않습니다.
- `--say-quest-phrases base`를 지정하면 채팅 `말하기`로 문구를 입력하는 퀘스트의 정답 문구 row를 베이스 언어 원문으로 유지하고, 한국어 퀘스트 목표·일지·시스템 안내의 따옴표 속 한국어 문구 뒤에 `(원문)`을 덧붙입니다. 정답 row는 `SAY`/`SAYTODO` 키 또는 일본어 `「Say」モード` 안내의 『』 문구와 같은 `SYSTEM` row로 찾습니다. 추리형 퀘스트는 대사 속 언급에도 원문을 덧붙입니다. 정답 row가 평문이 아니거나 정답 원문 중 하나라도 안내에 표시할 수 없으면 그 퀘스트는 한국어로 유지합니다. 기본값 `ko`는 기존 출력과 같습니다. 회귀 테스트는 `Scripts\test-say-quest-phrases.ps1`로 실행합니다.
- 한국 서버 대사의 풀네임 매크로 `<String(gstr(1))>`는 글로벌 일본어 원문의 `<Split(<String(gstr(1))>, " ", 1|2)>` 호칭(이름/성)으로 바꿉니다. 별도 옵션 없이 항상 적용됩니다. 한국어 대사는 일본어 원문을 따르므로 영어 베이스(`--target-language en`)에서도 일본어 EXD를 기준으로 합니다. 원문 호칭이 한 종류면 모든 이름 참조에, 섞여 있으면 참조 수가 같을 때만 순서대로 적용합니다. 한국어 조사 매크로(`Josa`/`JosaRo`)의 주어는 직전에 출력되는 호칭을 따르며, 조건 분기 때문에 그 호칭을 하나로 정할 수 없거나 `Split` 인자·SeString 파싱이 올바르지 않으면 한국어 원문(풀네임)을 유지합니다. 회귀 테스트는 `Scripts\test-name-forms.ps1`로 실행합니다.
- `--rsv-map <file>`을 지정하면 RSV token JSON map을 읽어 한국 서버 source row의 `_rsv_...` 토큰을 실제 문자열로 치환합니다. 지정하지 않으면 실행 파일 옆 `rsv.json`, 현재 작업 디렉터리 `rsv.json` 순서로 자동 탐색합니다.
- 데이터센터 화면의 한글 proxy glyph 방식은 FDT/텍스처 atlas 불일치 시 읽을 수 없는 글자로 노출될 수 있어 릴리즈 기본값에서 제외했습니다.
- `ExcelVariant.Default` sheet만 처리합니다.
- `ExcelVariant.Subrows` sheet는 아직 스킵하고 `patch-diagnostics.tsv`에 `unsupported-subrows`로 기록합니다.

### 텍스트 구성 프로필

- `full`(기본값): 8개 범위를 모두 한국어 source로 라우팅합니다. 기존 전체 한글 출력 계약입니다.
- `story`: 임무 중 대사·목표 범위만 한국어로 라우팅하고 나머지 7개 범위는 대상 베이스 언어 원문을 유지합니다. 이미지형 UI까지 한국어로 바뀌지 않도록 `060000` UI 텍스처 생성도 제외합니다.
- `custom`: 8개 범위의 결과를 `--text-scope-outcomes`로 모두 지정합니다. 형식은 `story=ko,bnpc=base,actions=ko,duty=base,item=base,place=base,common=base,remainder=base`입니다.

UI 이미지는 아홉 번째 EXD 텍스트 범위가 아닙니다. `custom`에서 UI 이미지를 원문으로 유지하려면 `--skip-ui-texture-fix`를 함께 사용하고, 한국어 이미지를 만들려면 이 플래그를 생략합니다. WPF의 텍스트·UI 구성 작업은 프로필과 관계없이 `--include-font`를 항상 전달하며, `--font-only`는 별도 작업입니다.

`--skip-ui-texture-fix`는 이미지 지역화만 제외합니다. `--include-font`와 한국어 `remainder`를 선택하면 PartyMemberList/ContentsFinder/RaidFinder의 기존 글자 표시용 ULD 보정은 유지합니다. 이 보정만 생성할 때는 한국 UI 텍스처와 글로벌 EXD 입력이 필요하지 않습니다.

8개 텍스트 결과를 모두 `base`로 지정하면 `0a0000` 텍스트 출력은 만들지 않고 요청한 폰트·이미지만 생성합니다. `--include-font`가 없으면 폰트를 강제로 추가하지 않습니다. 이미지 전용 구성과 `--font-only`는 다른 작업이며, 후자는 항상 `000000`만 생성합니다.

범위는 `story`, 전투 NPC·몬스터 이름(`bnpc`), 기술 이름(`actions`), 임무 이름(`duty`), 아이템 이름(`item`), 지역 이름(`place`), 자동 번역 상용구(`common`), 기타 게임 텍스트(`remainder`) 순으로 분류합니다. 스토리 판정이 먼저이며, 이름/상용구 그룹 판정과 어느 쪽에도 해당하지 않는 문자열은 `remainder`입니다.

`story`에는 `quest/*`, `cut_scene/*`, `opening/*`, `custom/*`, leaf 이름에 `Talk`가 포함된 sheet, `Balloon`, `NpcYell`, `TopicSelect`, `Quest`, `CompleteJournal`, `QuestRedoChapterUI*`가 포함됩니다. `InstanceContentTextData`는 row `1000` 이상만 story이고 row `999` 이하는 `remainder`입니다.

마수도감·마수 시련장의 다음 문자열은 모두 `remainder`입니다.

- `XBMPet`: 마수도감 설명과 공격 관련 문자열.
- `XBMItem`: 시련장 아이템의 단수형·복수형·표시 이름, 효과 설명과 짧은 설명.
- `XBMItemType`: 시련장 아이템 분류 이름.
- `XBMScoreBonus`: 추가/점수 보너스 이름과 달성 조건.

이 네 시트만 기존 row-id 매칭 대상에 포함하며 다른 `XBM*` 시트는 일괄 허용하지 않습니다. UI에서는 **전체 한글** 또는 **직접 설정 → 기타 게임 텍스트 → 한국어**로 적용합니다. `remainder=base`이면 다른 7개 범위가 한국어여도 위 문자열은 선택한 일본어/영어 원문을 유지합니다. 일반 **아이템 이름**이나 UI 이미지·전투 NPC 이름·기술 이름 선택만으로는 바뀌지 않습니다. 기타 게임 텍스트는 마수 시련장 전용 옵션이 아니므로 같은 범위의 메뉴·일반 설명도 함께 선택됩니다.

## 진단과 정책 파일

텍스트 생성 또는 명시적 CSV 진단에는 `patch-diagnostics.tsv`가 포함됩니다. 이 파일에는 sheet/page별 처리 상태, 패치 row 수, string-key/row-id 매칭 수, RSV 잔존 수가 기록됩니다. 모든 텍스트가 Base인 일반 실행은 자산 전용 경로를 사용하므로 이 파일을 생성하지 않습니다.

추가 진단이 필요하면 `--diagnostic-csv <sheet>`를 사용합니다. 지정한 sheet에 대해 글로벌 문자열, 한국 서버 문자열, 실제 선택된 문자열, 매핑 방식, row/column 정책 적용 여부를 CSV로 확인할 수 있습니다.
Base 셀은 remap/RSV 적용 전에 글로벌 원문으로 선택하며 CSV에도 `keep-global`로 기록합니다. 모든 텍스트가 Base여도 명시적 CSV 진단은 생성하지만 `0a0000` 출력은 만들지 않습니다.

생성된 release 폴더는 `Scripts\verify-patch-routes.ps1`로 후검증할 수 있습니다. 이 검증기는 데이터센터 row, 시간 단위, 파티 리스트 본인 번호, 주요 숫자 glyph를 확인하고, 기본적으로 로비/대사 문장 glyph PNG와 `glyph-report.tsv`를 함께 출력합니다. `캐릭터 정보를 변경하기 위해`, `진정한 변혁을 위해서라면` 같은 문장에서 특정 한글 glyph에 잔픽셀이 겹치는지 확인할 때 사용합니다.

선택적으로 `--policy <json>` 또는 실행 파일 옆 `patch-policy.json`으로 외부 보정 정책을 적용할 수 있습니다. 지원하는 항목은 다음과 같습니다.

외부 정책 파일이 없어도 일부 안전 정책은 기본 내장됩니다. 현재는 `Addon` row `44`, `45`, `49`를 글로벌 원본으로 유지하고, row `876`, `2338`, `6166`은 글로벌 영어 시간 템플릿을 사용해 좁은 UI 시간 단위가 `1시간`, `32분`처럼 넘치는 상황을 줄입니다. row `10952`는 파티 리스트 본인 표시 glyph 보정용으로 대상 글로벌 언어의 원본 PUA 토큰을 유지하고, 데이터센터 선택/이동 화면에 해당하는 글로벌 전용 row는 대상 글로벌 언어 row를 사용합니다.

- `delete_files`: sheet 전체 스킵
- `row_key_fallback_files`: string key가 없는 sheet의 row-id fallback 허용. `*`, `?` wildcard를 사용할 수 있습니다.
- `preserve_global_rows`: 특정 row를 글로벌 원본으로 유지
- `preserve_global_columns`: 특정 문자열 column을 글로벌 원본으로 유지
- `keep_rows`, `delete_rows`, `keep_columns`, `delete_columns`: 이전 정책 파일 호환용 alias
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
- `--font-profile no-trumpgothic` (legacy alias of `full`)
- `--font-profile no-jupiter`
- `--font-profile no-axis`
- `--font-profile fdt-only`
- `--font-profile textures-only`

기본값은 `full`입니다. 폰트 패치는 TTMP 패키지의 FDT와 texture를 한 세트로 유지합니다. `no-trumpgothic`은 이전 기본값 이름을 받기 위한 legacy alias이며 현재는 `full`과 동일하게 처리됩니다. 릴리즈 기본값에서는 광범위한 한글 proxy glyph 병합을 추가하지 않지만, 파티 리스트 본인 번호가 `=`로 보이는 문제를 피하기 위해 본인 번호 PUA glyph만 같은 FDT의 박스형 번호 glyph로 좁게 alias합니다. 이 alias는 FDT의 UTF-8 key 저장 방식과 Shift-JIS key를 함께 맞춰, 기존 영문/일문/한글 glyph atlas를 섞지 않도록 제한합니다. 나머지 프로필은 특정 UI glyph가 깨질 때 원인이 되는 폰트군을 찾기 위한 진단용입니다. UI에서는 테스트 빌드에서만 선택할 수 있습니다.

비로비 `TrumpGothic_23/34/68.fdt`에는 TTMP의 직접 한글/자모를 유지합니다. 일반 폰트 포함 생성은 정적 문구·선택 시트·Addon 범위·패치 출력에서 수집한 한글에 기존 시각 크기 보정을 적용합니다. 현재 목표는 숫자 대비0.88로, 직전0.98보다 약10.2% 작습니다. 기준선 정렬을 유지하면서 실제 글리프 크기를 추가 축소합니다. 폰트 전용 생성은 기존처럼 이 보정을 건너뜁니다. AXIS/KrnAXIS 본문, 영문·숫자·기호 및 로비 경로는 이번 크기 변경 대상이 아닙니다. 원본 마수도감·결과창 ULD는 변경하지 않으며, 같은 Trump 글꼴을 쓰는 인벤토리·시스템 설정 등의 제목 한글에도 축소가 적용됩니다. 100/150/200/300/400%의 기존 티어를 오프라인 검사하지만 실제 게임의 동적 배치·클리핑 확인을 대신하지는 않습니다.

크기 보정 시 source 한글의 평균 가시 하단과 target 숫자의 평균 가시 하단을 함께 측정하여 세로 원점을 맞춥니다. 축소 전 `OffsetY`를 그대로 재사용하지 않으며, source34→target68에서도 target 기준선을 사용합니다. 영문·느낌표의 메트릭이나 텍스처는 옮기지 않습니다. 검사기는 큰 글리프에 맞춰 캔버스를 확장하고 FDT의 실제 공백 전진 폭을 사용하여, 검사기 자체의 잘림·간격 오류를 방지합니다.

소지품의 일본어 부제를 유지하는 창별 간격 보정은 위의 공통 폰트 축소와 별개입니다. 일반 폰트 포함 생성에서 기타 UI가 한국어일 때 `Inventory`·`InventoryLarge`·`InventoryExpansion`의 부제 X만13→25로 옮기며, 원본 제목/부제 노드·폰트·텍스트 바인딩 규격을 검증합니다. `InventoryEvent`와 마수도감·결과창 ULD는 변경하지 않습니다. 랭크업 Addon17888은 기존 행별 텍스트 정책으로 `업 !` 간격을 적용하며 Base·명시적 보존 정책과 발바닥/랭크 행17889는 유지합니다. 실제 게임에서의 소지품 최종 배치는 별도 확인이 필요합니다.

## UI 텍스처 패치 방식

UI 텍스처 패치는 `060000` UI 패키지를 대상으로 하며 새 `060000.win32.dat4`를 만듭니다. 원본 `dat0`에는 쓰지 않고, 수정된 `index/index2`만 새 `dat4`를 참조하게 만듭니다.

현재 포함되는 대상:

- `ui/uld/PartyListTargetBase.tex`: 파티 리스트에서 본인을 표시하는 번호/glyph 텍스처 차이를 보정합니다.
- `ui/uld/PartyMemberList.uld`: 플레이어 간 교류 창의 파티 보너스 역할명 노드만 `TrumpGothic 19`에서 `AXIS 12`로 변경합니다. 컴포넌트/노드 구조, 위치, 크기, 정렬, 간격, 인스턴스 수가 예상값과 모두 일치할 때만 두 폰트 바이트를 수정하며, 구조가 달라지면 생성을 중단합니다.
- `ui/uld/ContentsFinder.uld`, `ui/uld/RaidFinder.uld`: 일반/고난도 임무 찾기 역할 탭의 `역할` 노드와 동적 `공격 역할`/`방어 역할`/`회복 역할` 노드만 `TrumpGothic 23`에서 `AXIS 12`로 변경합니다. widget/node ID, 위치, 크기, TextId, 정렬, 플래그, 간격이 예상값과 모두 일치할 때만 각 파일의 네 폰트 바이트를 수정하며, 구조가 달라지면 생성을 중단합니다. 같은 탭의 직업명 노드는 원래 `Jupiter 23`과 180x34 영역을 유지합니다.
- `ScreenImage` 언어별 이미지: `exd/screenimage.exh`와 `screenimage_*.exd`에서 `Lang` 플래그가 켜진 이미지 ID를 읽고, 글로벌 대상 언어 폴더(`ja` 또는 `en`)의 `ui/icon/...` 파일을 한국 서버 `ko` 이미지로 교체합니다.
- `CutScreenImage` 언어별 이미지: 지역 이동, 던전/컨텐츠 진입, 컷신 전환에서 쓰이는 타이틀 이미지 ID를 읽어 같은 방식으로 한국 서버 `ko` 이미지를 복사합니다.
- `TerritoryType` 언어별 이미지: 필드 지역 진입 시 표시되는 지역 타이틀 이미지 ID를 읽어, 저지/중부 라노시아처럼 `PlaceName` 문자열과 별도로 렌더링되는 이미지형 지역명을 한국 서버 `ko` 이미지로 보정합니다. 지역명 아래에 표시되는 `+2000` 계열 부제 이미지도 함께 복사합니다.
- `Map` 지도 텍스처: `Map.Id`에서 `ui/map/.../*_m.tex` 경로를 계산하고 한국 서버 텍스처와 다를 때 복사합니다. 지도 이미지 자체에 포함된 일본어 지역명/표기까지 보정하기 위한 처리입니다.
- `DynamicEventScreenImage`, `EventImage`, `TradeScreenImage`, `LoadingImage`: 언어 폴더가 없는 이미지형 UI 리소스는 글로벌과 한국 서버의 동일 경로 파일을 비교하고, 실제 바이트가 다른 경우에만 한국 서버 리소스로 교체합니다.
- ULD 텍스트 노드의 폰트 슬롯은 위 `PartyMemberList.uld`와 `ContentsFinder.uld`의 구조 검증된 노드를 제외하고 원본 글로벌 클라이언트 값을 유지합니다. 폰트 슬롯을 광범위하게 바꾸면 `AXIS_20_lobby`처럼 TTMP 패키지에 없는 크기/로비용 폰트 경로를 타면서 데이터 센터 화면의 한글이 `=`로 보일 수 있기 때문입니다.
- 데이터 센터/월드 이동 화면은 `Lobby`, `WorldRegionGroup`, `WorldPhysicalDC`, `WorldDCGroupType`, `Addon` 데이터센터 안내 row를 대상 글로벌 언어 row로 유지합니다. 이 화면의 한글 proxy glyph 방식은 읽을 수 없는 글자로 노출될 수 있어 릴리즈 기본값으로 사용하지 않습니다.

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
--rsv-map <file>                RSV token map JSON 파일
--anonymize-quest-chat-phrases  현재 비활성화/no-op, quest say sheet 커버리지 완료 전까지 적용하지 않음
--say-quest-phrases ko|base     말하기 퀘스트 입력 문구, 기본 ko
                                base: 정답 문구를 베이스 언어로 유지하고 안내에 "한국어(원문)" 표시
--diagnostic-csv <sheet>        지정 sheet의 row/column 비교 CSV 출력
--text-profile <name>           텍스트 구성: full(기본), story, custom
--text-scope-outcomes <csv>     custom의 8개 범위를 ko/base로 모두 지정
                                story,bnpc,actions,duty,item,place,common,remainder
                                순서와 무관하며 중복/누락/미지원 키는 오류
                                아래 preserve-base 옵션은 이전 CLI 호환용
--preserve-base-bnpc-names     BNpcName 이름을 베이스 클라이언트 언어로 유지
--preserve-base-action-names   기술 이름을 베이스 클라이언트 언어로 유지
--preserve-base-common-phrases 상용구를 베이스 클라이언트 언어로 유지
--preserve-base-duty-names     ContentFinderCondition 임무명/짧은 임무명을 베이스 클라이언트 언어로 유지
--preserve-base-item-names     Item 단수형/복수형/표시 이름을 베이스 클라이언트 언어로 유지
--preserve-base-place-names    PlaceName 지역명 변형을 베이스 클라이언트 언어로 유지
--preserve-base-language-groups <csv>
                                위 원문 유지 그룹을 CSV로 지정
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
--skip-ui-texture-fix           지역화 UI 이미지 제외; 한국어 remainder의 ULD 글자 표시 보정은 유지
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
