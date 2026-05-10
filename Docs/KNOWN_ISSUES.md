# 현재 미해결 항목과 추가 작업 목록

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

상태: 코드 검증 통과, 실제 일본어 클라이언트 확인 대기

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

재보고 항목:

- 시작 화면의 `종료`가 `EXIT`로 표시됨. 처리됨
- 데이터 센터 선택 화면을 나가는 버튼이 `-로?` / `-료?`처럼 깨져 보임. `Lobby#2009/#2052` 뒤로 row와 관련 ULD font phrase 검증을 추가해 처리됨
- 데이터 센터 선택 화면의 group label은 영어로 나오지만 흐리게 보임. 2px texture-neighborhood verifier와 ja 산출물 기준 처리됨
- 데이터 센터 선택 화면의 서버명/데이터센터명은 영어로 나오지만 문자 간격이 서로 침범함. clean glyph/metrics/kerning/phrase pixel/padding verifier 기준 처리됨
- 시작 화면 시스템 설정에서 UI 배율을 150/200/300%로 키우면 한글이 `=`로 깨지고 문자 간격이 침범함. `=` fallback은 처리됐고, 간격/번짐은 배율별 대응 로비 폰트 source 검증까지 추가됨
- `데이터 센터 Mana에 입장합니다` 계열 팝업 문구는 처리됨. 재발 시 sheet/row/column 추적 verifier를 먼저 보강
- 인게임 `즉시 발동`/`초`가 UI 배율별 상대 크기를 제대로 따라가지 않는 문제가 추가 보고됨. 100%/150%에서는 주변 문자보다 커 보이고, 200%/300%에서는 작게 보임. 기존 TTMP 원본 대비 문장 pixel/layout/metrics 검증은 patched output과 원본 동일성만 확인하므로 이 상대 크기 문제를 잡지 못함
- 스토리 종료 후 컷씬/이벤트 이미지 설명문이 베이스 클라이언트 언어로 표시됨. 폰트 패치가 아니라 지역 이동 타이틀처럼 실제 표시 리소스를 한국 클라이언트 리소스로 교체해야 할 가능성이 높음. 단, 아직 scene 리소스로 단정하지 않고 `EventImage` 등 sheet 기반 이미지 가능성부터 확인해야 함

필요 작업:

- `Title_DataCenter.uld`, `Title_Worldmap.uld`, 시작 화면 시스템 설정 ULD가 실제로 참조하는 font id와 FDT route를 확정
- 데이터센터 화면에서 재발하는 버튼/문구가 있으면 어느 sheet/row/column에서 오는지 찾아서 EN/JA 언어 슬롯별로 확인
- 데이터센터 화면 서버명과 DC명은 단어 단위가 아니라 실제 리스트 문자열 기준으로 layout 검증. 현재 `DataCenterWorldmapLabels` 전체 기준으로 수행
- 흐림 문제는 clean global ASCII glyph의 픽셀/metrics뿐 아니라 실제 texture cell alpha 차이까지 비교. 현재 non-space ASCII glyph 주변 2px padding 기준으로 수행
- 흐림 문제는 clean global ASCII glyph의 픽셀/metrics뿐 아니라 실제 texture cell alpha 차이까지 비교. 현재 non-space ASCII glyph 주변 4px padding 기준으로 수행
- 시작 화면 시스템 설정은 인게임 font route와 분리해서, 해당 route의 한글 glyph fallback/spacing과 배율별 대응 로비 폰트 source glyph 일치 여부를 함께 검증
- `즉시 발동`/`초` 계열 문구는 TTMP 원본과 patched glyph size/metrics가 같은지 확인하는 기존 `reported-ingame-hangul-phrases`만으로 닫지 않는다. action detail/help UI의 실제 ULD/font route를 찾고, 100/150/200/300%에서 한글 라벨과 시간 단위 glyph bounds/advance가 주변 숫자/라벨과 같은 비율로 스케일되는지 별도 verifier를 추가한다
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

상태: 검증 보강 및 코드 수정됨, 실제 클라이언트 재적용 확인 필요

150% 이상 UI, 특히 시스템 설정 창에서 space 또는 문자 간격이 좁아 glyph가 겹쳐 보이는 현상이 있습니다. 4K lobby/in-game FDT의 advance 값, offset, glyph width를 잘못 가져오거나 normalize가 부족한 가능성이 있습니다.

2026-05-09 처리:

- `LobbyScaledHangulPhrases.HighResolutionUiScaleOptions`에 실제 고해상도 UI 옵션 문장을 추가
- `system-settings-mixed-scale-layouts` verifier 추가
- 4K lobby 파생 font에서 Hangul/ASCII를 같은 source에서 무조건 복사하던 방식을 분리
- Hangul advance는 clean CJK median 또는 glyph width 기반 safety advance를 기준으로 보정
- clean target에 없는 ASCII는 파생 source pair의 clean lobby font에서 4px padding과 함께 보충
- `.tmp\mixed-scale-spacing-fix-ja9` 기준 `.tmp\verifier-reported-font-routes-after-fix2.log` PASS

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
- Latest ja output passes `.tmp\verifier-ja-regression-with-reported-phrases.log` for data-center rows/language slots, start-screen system settings, configuration sharing, Bozja, Occult Crescent support jobs, clean ASCII font routes, 4K lobby phrase/layout, Hangul source preservation, reported in-game Hangul phrases, and lobby Hangul visibility.
- `reported-ingame-hangul-phrases` compares full phrase pixels/layout/metrics against TTMP source for `탐사대 호위대원`, `즉시 발동`, `시전 시간`, `재사용 대기 시간`, and `발동 조건`. This keeps the `호` regression covered without adding a single-character protection rule.
- ULD route checks now also compare text node header bytes and text extra render-state bytes, not only font id/font size. `.tmp\verifier-uld-render-state.log` passed for data center and start-screen system settings candidates.
- `applied-output-files` verifier check compares critical generated font/UI packed files against an installed game folder when `--applied-game <game-dir>` is supplied. Use this before asking for live client confirmation when generated output passes but the installed client still differs.
- `build-release.ps1` now verifies that the release exe embeds the freshly built `FFXIVPatchGenerator.exe` by SHA-256. Generated-output PASS is not enough if the user is applying from a stale `Release\Public\FFXIVKoreanPatch.exe`.
- Release UI reapply guard now allows applying over an already patched client when clean base indexes are available from current index, installed `orig.*` indexes, or any same-version local `restore-baseline` language folder. This prevents stale installed font/UI dat files from surviving after generated output already passes verification.

## 작업 시 주의 사항

- 인게임 폰트가 정상인 상태에서 로비 문제를 고치기 위해 인게임 폰트를 광범위하게 건드리지 말 것
- 로비 문제를 고치기 위해 `TrumpGothic` 또는 `Jupiter` 전체 한글 glyph를 대량 리맵하지 말 것
- generator 로그에서 일반 로비 폰트의 artifact-prone remap이 수백 개 단위로 나오면 잘못된 방향일 가능성이 큼
- 한 글자 문제는 해당 글자 codepoint와 FDT를 특정한 뒤 좁게 수리할 것
- “한글로 나오게 만들기”보다 “베이스 클라이언트 원문 유지가 안전한 글로벌 전용 UI인지” 먼저 판단할 것
- 커밋/푸시/릴리즈 배포는 사용자가 명시적으로 요청했을 때만 진행할 것
