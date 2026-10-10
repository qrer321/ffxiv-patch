# 기능 전수 조사

이 문서는 현재 코드 기준으로 UI, 제너레이터, 빌드/배포 스크립트에 구현된 기능을 정리합니다.

프로젝트 전체 구조와 작업 목적은 [PROJECT_OVERVIEW.md](PROJECT_OVERVIEW.md)를 참고하세요.
현재 안 되는 내용과 추가 작업 목록은 [KNOWN_ISSUES.md](KNOWN_ISSUES.md)에 별도로 정리합니다.

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
- 경로 확인 패널에서 경로 자동 탐색 및 리셋
- 테스트 빌드 전용 폰트 프로필 선택

### 언어 선택

- 베이스 클라이언트 언어 선택
  - 일본어 클라이언트 `ja`
  - 영어 클라이언트 `en`
- 언어 변경 시 사전 점검 상태를 다시 필요 상태로 변경
- 선택한 언어 슬롯에 맞는 `*_ja.exd` 또는 `*_en.exd` release 생성
- 베이스 언어 자동 저장 및 재시작 시 복원

### 텍스트 구성

- `전체 한글`, `직접 설정` 두 구성 제공
- 8개 텍스트 범위와 UI 이미지를 항상 표시하고, 각각 `한국어`/선택한 베이스 언어(`일본어` 또는 `영어`)를 명시적인 라디오 버튼으로 선택
- 스토리만 한국어로 보려면 `스토리·퀘스트 텍스트`만 `한국어`, 나머지 7개 텍스트 범위와 UI 이미지는 베이스 언어로 선택
- 개별 결과를 바꾸면 `직접 설정`으로 전환
- 메인 동작 버튼은 `선택 구성 패치` 하나를 유지하고 설명/요약만 선택 결과에 맞게 갱신
- 텍스트 구성 패치는 선택 결과와 관계없이 한글 폰트를 항상 포함
- 기본값은 기존 동작을 유지하는 `전체 한글`
- 이전 UI의 `story` 저장값은 Story만 한국어인 9개 결과의 `custom`으로 마이그레이션
- 이전 6개 `preserveBase...` 키는 하나라도 `true`이면 `custom`, 모두 `false`이면 `full`로 읽음
- `말하기 퀘스트 입력 문구`를 `한국어`/베이스 언어(기본값)로 선택. 텍스트 범위와 별개의 설정이며 `전체 한글`/`직접 설정` 전환과 관계없이 유지. 스토리·퀘스트 텍스트가 베이스 언어면 비활성화. `sayQuestPhrases=base|ko`로 저장하고 제너레이터에 `--say-quest-phrases`로 전달
- `targetLanguage`, `textProfile`, 9개 적용 결과와 별도의 `custom.*` 초안을 저장. 전체 한글로 전환해도 직접 설정을 보존
- 설정 파일은 원자적으로 교체하며 저장 실패는 하단에 계속 표시. 읽기 불가/잘못된 설정은 초기화 오류로 안내
- 본문 스크롤과 하단 고정 적용 영역을 분리. 현재 설치 상태와 다음 선택을 구분하고 좁은 화면에서는 텍스트 범위를 한 열로 표시
- 릴리즈의 `패치 제거`를 하단 고정 주 동작 행에 붉은 테두리 버튼으로 표시. 좁은 창에서도 패치 버튼과 나란히 유지하며, 기존 실행 확인과 작업 중 잠금은 유지

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
- 사전 점검 결과를 본문의 펼침 영역에 표시
- 실패/주의/전체 항목 수 표시
- 사전 점검 로그 저장
- 사전 점검 실패 시 실제 패치 버튼 잠금

### 패치 적용

- 선택한 텍스트·UI 구성 패치
  - 하나 이상의 텍스트 범위가 한국어인 경우에만 텍스트 패키지 생성·적용
  - 한글 폰트 패치 항상 생성
  - UI 이미지 결과가 한국어이면 지역화 텍스처 생성
  - 이미지가 원문이어도 기타 게임 텍스트가 한국어이면 기존 UI 글자 표시용 ULD 보정을 별도로 생성
  - 두 UI 작업 중 하나라도 필요하면 `060000` 패키지 적용. `--skip-ui-texture-fix`는 이미지만 제외
  - 생성된 release 파일 적용
- 한글 폰트 패치
  - 폰트 패치만 생성
  - 생성된 font release 파일 적용
- 실제 적용 전 대상 경로, 베이스 언어, 9개 결과와 한글 폰트를 확인하는 취소 우선 확인창
- 초기 점검·사전 점검·생성·적용·제거·백업 복구 중에는 구성 변경 및 창 닫기 차단
- 제너레이터 진행도를 UI progress bar에 표시
- 적용할 패키지와 해당 원본 index, 버전 파일만 `manifest.json`에 기록하고 UI 이미지 결과도 별도로 저장
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
- 이미 패치된 index/index2가 감지되더라도 current/orig/같은 버전의 restore-baseline 중 clean base index를 확보할 수 있으면 최신 전체/폰트 패치를 재적용
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

### CLI 텍스트 구성 프로필

- `full`: 8개 텍스트 범위와 UI 이미지를 모두 한국어로 적용하는 기본값
- `story`: 임무 중 대사·목표만 한국어로 적용하고 나머지 7개 텍스트 범위와 UI 이미지는 베이스 원문 유지
- `custom`: 8개 텍스트 범위와 UI 이미지 결과를 각각 한국어/베이스 원문으로 선택
- 텍스트 8개 범위: 스토리·퀘스트, 전투 NPC·몬스터 이름, 기술 이름, 임무 이름, 아이템 이름, 지역 이름, 상용구, 기타 게임 텍스트
- UI 이미지는 EXD 텍스트 범위가 아닌 별도 `060000` 생성 결과
- 스토리 범위가 이름/상용구 그룹보다 먼저 판정되고, 어느 그룹에도 속하지 않으면 기타 UI·시스템으로 분류
- `story` 프로필은 이미지형 UI를 한국어로 바꾸지 않도록 `060000` UI 텍스처 생성 제외
- `full`은 기존 전체 한글 출력 경로를 그대로 사용
- 제너레이터 `custom`은 8개 텍스트 결과가 모두 있어야 하며 중복/누락/미지원 범위는 오류
- SeString은 선택된 source의 최상위 payload type 순서와 RSV 구조를 보존해 검증
- 검증기에서 스토리/비스토리/mixed page, `InstanceContentTextData` row `999`/`1000` 경계, UI asset 부재, Full 출력 hash 회귀를 독립 검사
- Story 시나리오와 Full 기준선 비교는 `--checks`로 명시적으로 선택할 때만 실행. 일반 검증 기본 실행에 서로 다른 프로필을 섞지 않음
- Story 검증은 한국어 원본과 구별되는 실제 비교 대상이 필요하며, 원본 누락에 따른 베이스 폴백은 한국어 검증 수에 포함하지 않음
- Story 검증의 기대값은 제너레이터와 같이 플레이어 호칭 변환과 RSV 치환을 적용하고, `--say-quest-phrases base`를 지정하면 말하기 퀘스트 문구 처리도 적용. 한국어 원본이 없어 베이스 텍스트로 남은 셀에도 적용하며, 읽을 수 없는 선택적 일본어 참조 page는 제너레이터와 같이 대상 page로 대체

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
  - 익명화한 say quest 문구/row 수
  - 말하기 퀘스트 입력 문구 설정과 베이스 언어로 바꾼 퀘스트·정답 row·안내 row 수, 한국어로 유지한 퀘스트 수
  - mapping 누락 page 수
  - 원본/대상 page 누락 수
  - 미지원 sheet 수
  - 패치한 font 파일 수
  - 패치한 UI 텍스처 파일 수

### 텍스트 패치

- 글로벌 `exd/root.exl` 기준 sheet 순회
- 글로벌 EXH 구조 기준 EXD 재생성
- 한국 서버 `*_ko.exd`에서 문자열 컬럼 SeString 바이트 추출
- 글로벌 대상 언어 `*_ja.exd` 또는 `*_en.exd`에 문자열만 반영
- Default variant EXD 처리
- `--text-profile full|story|custom`으로 텍스트 구성 선택
- `--text-scope-outcomes`로 custom의 8개 `ko`/`base` 결과 지정
- sheet별 범위 정책을 한 번 계산하고 베이스 원문 sheet는 한국 source EXD 읽기 전에 건너뜀. 명시적 CSV 진단은 비교 자료를 읽되 원문 결과를 유지
- `InstanceContentTextData`는 row `1000` 이상만 story, 그 아래는 기타 UI·시스템으로 분류
- Subrows variant sheet 스킵
- Subrows variant sheet를 `unsupported-subrows`로 진단
- string key 기반 row 매핑
- 일부 allowlist sheet의 row id fallback
- `row_key_fallback_files` 정책으로 row id fallback 대상 sheet 외부 확장
- `global_target_rows` 정책으로 특정 row를 대상 글로벌 언어 원본으로 유지
- `global_english_rows` 정책으로 특정 row를 글로벌 영어 원본으로 유지
- `Addon`, `AddonTransient` sheet의 짧은 숫자/기호/SeString UI 토큰 보호
- `Addon`, `AddonTransient` SeString macro/lookup 구조가 글로벌/한국 row 사이에서 달라질 때 글로벌 payload/lookup 구조와 한국어 literal을 안전 병합
- 병합이 불가능한 글로벌 전용 UI row는 글로벌 원본 구조를 유지해 깨진 glyph나 `--` 표시 방지
- 데이터 센터 선택/이동 화면의 `Lobby` row `800`~`806`, `WorldRegionGroup` row `1`~`8`, `WorldPhysicalDC` row `1`~`8`, `WorldDCGroupType` row `1`~`32`, `Addon` row `12510`~`12538`은 글로벌 전용 로비 UI라서 대상 글로벌 언어 row 사용. `--target-language ja`면 일본어 원본, `en`이면 영어 원본을 사용
- 데이터센터 화면의 한글 proxy glyph 방식은 FDT/텍스처 atlas 불일치 시 읽을 수 없는 글자로 노출될 수 있어 릴리즈 기본값에서 제외
- `Addon` row `44`, `45`, `49` 기본 보호로 좁은 UI의 `h/m/s` 시간 단위가 `시간/분/초`로 늘어나 영역을 넘치는 문제 완화
- `Addon` row `2338`, `6166`은 글로벌 영어 시간 템플릿을 사용해 버프/남은시간 UI의 `시간/분` overflow 완화
- `Addon` row `10952`는 파티 리스트 본인 표시 glyph가 `=`로 보이는 문제를 피하기 위해 대상 글로벌 언어의 원본 PUA 토큰 유지
- 파티 리스트 번호 표시 설정이 1~8로 바뀌는 경우를 고려해 본인 번호 PUA glyph(`U+E0E1`~`U+E0E8`)를 clean global의 속 빈 네모 번호 모양으로 복원. FDT 엔트리와 glyph 픽셀을 함께 이식해 `U+E0B1`~`U+E0B8` 동그라미 번호와 섞이지 않도록 처리
- 2026-05-23 보강: 저배율/작은 채팅창/인스턴스 표기에서 PUA glyph 주변 픽셀이 섞이지 않도록 `U+E031`, `U+E037`, `U+E0B1`~`U+E0B8`, `U+E0E1`~`U+E0E8`은 필수 seed로 유지하고, 각 보호 font route에서 clean/source와 patched target 양쪽에 존재하는 PUA glyph도 자동 수집해 dirty target cell을 재사용하지 않고 8px base/mip 주변 영역까지 clean PUA cell로 검증한다.
- `--anonymize-quest-chat-phrases`는 현재 비활성화/no-op입니다. `quest/*` sheet 커버리지가 불완전하므로 기존 구현은 feature gate 뒤에 보존하고, UI 전체 패치/테스트 자동 패치는 더 이상 퀘스트 채팅 문구 익명화를 자동 활성화하지 않습니다.
- 플레이어 호칭 복원: 한국 서버 대사의 풀네임 매크로 `<String(gstr(1))>`를 글로벌 일본어 원문의 `<Split(<String(gstr(1))>, " ", 1|2)>`(이름/성) 호칭으로 교체. 별도 옵션 없이 텍스트 패치 생성 시 항상 적용
  - 한국어 대사는 일본어 원문을 따르므로 `--target-language en`이어도 같은 page의 일본어 EXD를 기준으로 삼음. 영어 원문은 의역으로 이름을 생략하는 경우가 많아 기준으로 쓰지 않음
  - 원문의 호칭이 모두 같으면(이름만 또는 성만) 순서와 무관하게 모든 이름 참조에 적용. 섞여 있으면 한국어 이름 참조 수가 같을 때만 순서대로 대응하고, 다르면 풀네임 유지
  - 한국어 조사 매크로 `Josa`/`JosaRo`의 주어는 직전에 출력되는 이름 호칭을 따름. 한국어에 이미 `Split` 호칭이 있으면 그 호칭을 유지하고 조사도 그 호칭을 따름. 조건 분기(`If`/`Switch` 등)에 따라 직전 호칭이 달라져 조사 주어를 하나로 정할 수 없으면 해당 문자열은 풀네임 유지
  - 플레이어 이름 `Split`은 구분자(공백 1자)와 인덱스(1 또는 2)까지 검증하고, 하나라도 맞지 않거나 SeString 파싱에 실패하면 한국어 원문 유지
  - 원문에 이름/성 호칭이 없는 row, 글로벌 fallback row, literal remap은 그대로 유지. 처리/유지 수는 `Name forms applied`/`Name forms kept full`로 출력
  - 회귀 테스트: `Scripts\test-name-forms.ps1`
- 말하기 퀘스트 입력 문구: `--say-quest-phrases base`이면 `quest/*`에서 말하기 퀘스트의 정답 문구 row를 베이스 언어 원문으로 유지하고, 한국어 안내에 원문을 덧붙임. 기본값 `ko`는 기존 출력과 바이트 단위로 같음
  - 정답 문구 row: 키 종류가 `SAY`/`SAYTODO*`이거나, `SYSTEM`이면서 일본어 텍스트가 `「Say」モード` 안내의 『』 문구(끝 문장부호 무시)와 같은 row. 화자 키(`..._RYNE_000_145` 등)는 같은 문장이어도 대사로 취급. 영어 베이스도 일본어 EXD로 찾음
  - 안내 표시: `SEQ`/`TODO`/`SYSTEM` row에서 따옴표로 감싼 한국어 정답 문구 뒤에 `(원문)`을 삽입. 매크로 안은 건드리지 않음. 한 한국어 문구가 단계마다 다른 원문에 대응하면(영어 `I come in peace.`/`Greetings and salutations!`) 같은 row의 베이스 언어 텍스트에 나온 원문을 사용하고, 정할 수 없으면 그 따옴표는 그대로 둠
  - 추리형 퀘스트(안내에 『』 정답이 없음): 대사를 포함한 모든 row의 따옴표 언급에 원문을 덧붙이고, 따옴표가 없으면 정답 단어 자체에 덧붙임
  - 안전 규칙: 정답 row가 매크로를 포함하거나 비어 있거나 80자를 넘으면, 안내형 퀘스트에서 정답 원문 하나라도(한 한국어 문구가 여러 원문에 대응하면 각 원문마다) 안내나 대사 어디에도 표시할 수 없으면, 추리형 퀘스트에서 표시할 곳이 하나도 없으면 해당 퀘스트 전체를 한국어로 유지
  - 정답 판정용 row 목록은 제너레이터와 검증기가 같은 코드(`SayQuestPhraseLocalizer.BuildRows`)로 만들며, 키와 베이스 텍스트는 clean 대상 page에서 읽음
  - 회귀 테스트: `Scripts\test-say-quest-phrases.ps1`
- `patch-policy.json` 기반 sheet/row/column 보존과 row/column remap
- 텍스트 생성 또는 명시적 CSV 진단 시 `patch-diagnostics.tsv` 생성. 모든 텍스트가 Base이면 일반 실행은 자산 전용 경로 사용
- `--diagnostic-csv` 지정 sheet의 row/column 비교 CSV 생성. Base 셀은 remap/RSV보다 먼저 원문으로 선택
- `--rsv-map`으로 RSV token JSON map을 읽어 한국어 source row의 `_rsv_...` 토큰을 실제 문자열로 치환
- `_rsv_` 토큰이 남은 row/string 수 집계
- `Scripts\verify-patch-routes.ps1`로 release 폴더 후검증
  - 데이터센터 row, 좁은 시간 단위, 숫자 glyph, 파티 리스트 본인 번호 glyph 확인
  - 로비/대사 문장 한글 glyph를 PNG와 `glyph-report.tsv`로 덤프해 특정 글자의 atlas 잔픽셀/겹침 여부 확인
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
- TTMP의 FDT와 texture atlas를 한 세트로 유지
- FDT glyph 좌표/문자 코드와 다른 클라이언트의 font atlas를 섞지 않음
- 릴리즈 기본값에서는 TTMP의 FDT/texture 조합을 한 세트로 유지. 예외적으로 파티 리스트 본인 번호 및 인스턴스 번호 계열 PUA glyph는 필수 seed와 route-local 자동 수집 결과를 clean global 모양으로 복원하며, TTMP atlas의 빈 영역을 찾아 해당 glyph 픽셀과 FDT 엔트리를 함께 보정
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

### UI 이미지와 글자 표시 보정

- 텍스트 구성 작업에서 지역화 이미지 또는 한국어 UI 글자 표시용 ULD 보정이 필요할 때 `060000` 패키지 생성
- 새 `060000.win32.dat4` 생성
- 수정된 `060000.win32.index` 생성
- 수정된 `060000.win32.index2` 생성
- 복구용 `orig.060000.win32.index` 생성
- 복구용 `orig.060000.win32.index2` 생성
- `ui/uld/PartyListTargetBase.tex`를 한국 서버 텍스처로 교체해 파티 리스트 본인 번호/glyph 표시 차이 보정
- `ScreenImage` sheet의 `Lang` 플래그가 켜진 이미지 ID를 읽어 글로벌 대상 언어 폴더(`ja`/`en`)의 `ui/icon/...` 텍스처를 한국 서버 `ko` 텍스처로 교체
- `CutScreenImage` sheet의 타이틀 이미지 ID를 읽어 지역 이동, 던전/컨텐츠 진입, 컷신 전환에 쓰이는 언어별 이미지 보정
- `TerritoryType` sheet의 지역 타이틀 이미지 ID와 `+2000` 부제 이미지 ID를 읽어 필드 지역 진입 시 표시되는 이미지형 지역명 보정
- `Map` sheet의 `ui/map/.../*_m.tex` 지도 텍스처를 한국 서버 텍스처와 비교해, 지도 이미지에 포함된 지역명 표기 보정
- `DynamicEventScreenImage`, `EventImage`, `TradeScreenImage`, `LoadingImage`는 언어 폴더가 없는 동일 경로 리소스를 비교하고 실제 파일이 다른 경우에만 한국 서버 리소스로 교체
- 지역/컨텐츠 입장 시 표시되는 타이틀 이미지처럼 텍스트가 아니라 이미지로 렌더링되는 UI 요소 보정
- 데이터 센터 선택 화면의 `Title_DataCenter.uld`와 `Title_Worldmap.uld`는 clean global 폰트 슬롯을 그대로 유지하고, 데이터센터 라벨에 쓰이는 ASCII glyph/texture를 clean route와 비교한다. `_lobby.fdt` ASCII/숫자/기호는 동일 이름의 비로비 인게임 FDT를 기준으로 glyph/metrics/kerning을 맞춘다
- TTMP 패키지가 제공하는 원래 폰트군 조합을 사용해 렌더링하며, `AXIS_20_lobby`처럼 패키지에 없는 크기/로비용 폰트 경로로 잘못 라우팅되는 것을 방지
- `Lobby`, `WorldRegionGroup`, `WorldPhysicalDC`, `WorldDCGroupType`, `Addon` 12510번대 서버/데이터센터 이동 안내 row는 대상 글로벌 언어 row를 사용해 읽을 수 없는 proxy glyph 노출을 방지
- `--base-ui-index`, `--base-ui-index2`로 clean `060000` index/index2 지정
- `story`, UI 이미지 `원문`, `--font-only`에서는 UI 텍스처 패치 생성 제외
- `--skip-ui-texture-fix`는 지역화 이미지만 제외. 한국어 Remainder의 PartyMemberList/ContentsFinder/RaidFinder ULD 보정은 유지하며 이 보정만 할 때는 한국 UI/글로벌 EXD 입력을 읽지 않음

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
- `--font-profile`: 진단용 폰트 프로필 선택, 기본 `full`
- `--text-profile`: `full`, `story`, `custom`; 기본 `full`
- `--text-scope-outcomes`: `custom`의 `story,bnpc,actions,duty,item,place,common,remainder` 결과를 `ko`/`base`로 모두 지정
- `--base-index`, `--base-index2`: 텍스트 패치용 clean index 지정
- `--base-font-index`, `--base-font-index2`: 폰트 패치용 clean index 지정
- `--allow-patched-global`: 이미 패치된 index 사용 허용, 실험용
- `--allow-korean-font-fallback`: TTMP 없이 한국 서버 폰트 직접 복사, 실험용
- `--policy`: JSON 패치 정책 파일
- `--say-quest-phrases ko|base`: 말하기 퀘스트 입력 문구, 기본 `ko`
- `--rsv-map`: RSV token map JSON 파일. 지정하지 않으면 실행 파일 옆 `rsv.json`, 현재 작업 디렉터리 `rsv.json` 순서로 자동 탐색
- `--diagnostic-csv`: 지정 sheet의 row/column 비교 CSV 출력
- `--allow-version-mismatch`: 글로벌/한국 서버 버전 불일치 허용, 진단용

## 빌드와 배포 기능

### 릴리즈 빌드

- `Scripts\build-release.ps1`
- Release 구성 빌드
- `Release\Public` 생성
- `FFXIVKoreanPatch.exe` 단일 배포 파일 복사
- UI exe에 내장된 제너레이터/TTMP 폰트 패키지 사용
- 오래된 updater 파일 제거

### 테스트 빌드

- `Scripts\build-test.ps1`
- Debug + `TEST_BUILD` 구성 빌드
- `Release\Test` 생성
- 테스트 단일 실행 파일명을 `FFXIVKoreanPatch.Test.exe`로 변경

### GitHub 배포 보조

- `Scripts\publish-github.ps1`
- 소스 커밋과 바이너리 릴리즈를 분리하는 구조
- `Release` 폴더는 Git 커밋 대상에서 제외
- `Scripts\publish-release.ps1`
- GitHub Release용 exe asset, SHA256, 릴리즈 노트 생성
- GitHub Release에는 `FFXIVKoreanPatch.exe` 단일 실행 파일을 직접 업로드
- `-Publish`를 명시했을 때만 태그 생성, 태그 push, GitHub Release 생성
- 배포 시 로컬 HEAD와 `origin/main` 일치 여부 확인

## 현재 제한

- EXD Default variant 중심으로 처리합니다.
- Subrows variant sheet는 아직 패치하지 않고 진단 파일에만 기록합니다.
- 폰트 패치 실사용은 TTMP 패키지 포함을 전제로 합니다.
- clean index 또는 복구용 original index가 없으면 이미 패치된 글로벌 index를 배포용 base로 쓰지 않습니다.
- `--allow-patched-global`, `--allow-korean-font-fallback`, `--allow-version-mismatch`는 실험/진단용입니다.
- 150%/200%/300% UI 스케일, 데이터 센터 group 표시, 보즈야 입장 안내, 크레센트 레벨 glyph 등은 아직 추가 작업이 필요합니다. 상세 내용은 [KNOWN_ISSUES.md](KNOWN_ISSUES.md)를 참고하세요.
