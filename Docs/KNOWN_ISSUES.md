# 현재 미해결 항목과 추가 작업 목록

이 문서는 현재 확인된 문제와 다음 작업자가 이어서 처리해야 할 항목을 정리합니다. 아래 항목은 “구현 완료”가 아니라 “작업 필요” 상태입니다.

## 최근 보고 후 처리됨

### ESC 시스템 메뉴의 설정 공유 제목이 베이스 클라이언트 언어로 표시됨

상태: 처리됨

보고 내용:

- ESC를 눌렀을 때 나오는 시스템 메뉴/팝업에서 `コンテンツシェア` 또는 `コンフィグシェア` 계열 제목이 번역되지 않음
- 설정 공유 창 내부 내용은 이미 번역되어 있음

처리 방향:

- 기존 보정은 `Addon#17300/17301` 창 제목만 대상으로 삼았음
- ESC 시스템 메뉴 항목은 `MainCommand#99`를 참조하며, 한국 서버의 `MainCommand#99` title/description이 비어 있었음
- `MainCommand#99` title과 description을 정책 literal로 보정
- `configuration-sharing` verifier가 `MainCommand#99`와 `Addon#17300/17301`을 함께 검사하도록 확장

## 우선순위 높음

### 0. 2026-05-09 재보고: 시작 화면 데이터센터/시스템 설정 실사용 경로 미검증

상태: 작업 필요

최근 verifier는 통과했지만 실제 클라이언트에서 아래 문제가 계속 재현되었습니다. 이는 기존 검증이 실제 시작 화면 UI route를 충분히 잡지 못했다는 뜻으로 취급합니다.

재보고 항목:

- 데이터 센터 선택 화면의 group label은 영어로 나오지만 흐리게 보임
- 데이터 센터 선택 화면의 서버명/데이터센터명은 영어로 나오지만 문자 간격이 서로 침범함
- 데이터 센터 선택 화면을 나가는 버튼이 `-로?` / `-료?`처럼 깨져 보임
- 시작 화면 시스템 설정에서 UI 배율을 150/200/300%로 키우면 한글이 `=`로 깨지고 문자 간격이 침범함
- `데이터 센터 Mana에 입장합니다` 계열 팝업 문구가 베이스 클라이언트 언어로 나옴
- 인게임 `즉시 발동` 같은 짧은 한글 UI 문구가 다른 인게임 UI 폰트보다 상대적으로 크게 보일 가능성이 있음. 단, 현재 보고는 깨짐/잘못된 폰트 문제가 아니라 시각적 크기 확인 요청임
- 스토리 종료 후 컷씬/이벤트 이미지 설명문이 베이스 클라이언트 언어로 표시됨. 폰트 패치가 아니라 지역 이동 타이틀처럼 실제 표시 리소스를 한국 클라이언트 리소스로 교체해야 할 가능성이 높음. 단, 아직 scene 리소스로 단정하지 않고 `EventImage` 등 sheet 기반 이미지 가능성부터 확인해야 함

필요 작업:

- `Title_DataCenter.uld`, `Title_Worldmap.uld`, 시작 화면 시스템 설정 ULD가 실제로 참조하는 font id와 FDT route를 확정
- 데이터센터 화면의 버튼/팝업 문구가 어느 sheet/row/column에서 오는지 찾아서 EN/JA 언어 슬롯별로 확인
- 데이터센터 화면 서버명과 DC명은 단어 단위가 아니라 실제 리스트 문자열 기준으로 layout 검증
- 흐림 문제는 clean global ASCII glyph의 픽셀/metrics뿐 아니라 실제 texture cell alpha 차이까지 비교
- 시작 화면 시스템 설정은 인게임 font route와 분리해서, 해당 route의 한글 glyph fallback/spacing만 검증
- `즉시 발동` 계열 문구는 TTMP 원본과 patched glyph size/metrics가 같은지 확인. TTMP 원본과 같으면 패치 깨짐이 아니라 UI route의 원래 크기 문제로 분리
- story 완료 설명문이 `EventImage`, `CutScreenImage`, `ScreenImage`, `DynamicEventScreenImage`, `LoadingImage`, event/cutscene texture/ULD bundle 중 어디에서 오는지 추적
- 설명문이 이미지형 리소스라면 global target 리소스와 Korean source 리소스의 hash를 비교하고, 한국 서버 리소스가 존재하는 경우 output UI index에 Korean packed texture가 매핑됐는지 verifier에 추가

검증 기준:

- 재보고된 항목은 먼저 verifier에서 실패해야 함
- verifier가 pass인데 실제 클라이언트에서 실패하면 해당 verifier는 불완전한 것으로 보고 수정
- 데이터센터 시작 화면의 영어/숫자/특수문자는 clean global 대비 흐림, 간격 침범, fallback 동일 픽셀이 없어야 함
- 시작 화면 시스템 설정의 한글은 `=`/`-` fallback과 같으면 실패
- 인게임 한글 TTMP source preservation은 계속 유지해야 함
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

상태: 작업 필요

150% 이상 UI, 특히 시스템 설정 창에서 space 또는 문자 간격이 좁아 glyph가 겹쳐 보이는 현상이 있습니다. 4K lobby/in-game FDT의 advance 값, offset, glyph width를 잘못 가져오거나 normalize가 부족한 가능성이 있습니다.

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

## 작업 시 주의 사항

- 인게임 폰트가 정상인 상태에서 로비 문제를 고치기 위해 인게임 폰트를 광범위하게 건드리지 말 것
- 로비 문제를 고치기 위해 `TrumpGothic` 또는 `Jupiter` 전체 한글 glyph를 대량 리맵하지 말 것
- generator 로그에서 일반 로비 폰트의 artifact-prone remap이 수백 개 단위로 나오면 잘못된 방향일 가능성이 큼
- 한 글자 문제는 해당 글자 codepoint와 FDT를 특정한 뒤 좁게 수리할 것
- “한글로 나오게 만들기”보다 “베이스 클라이언트 원문 유지가 안전한 글로벌 전용 UI인지” 먼저 판단할 것
- 커밋/푸시/릴리즈 배포는 사용자가 명시적으로 요청했을 때만 진행할 것
