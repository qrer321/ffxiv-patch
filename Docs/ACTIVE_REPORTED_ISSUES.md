# Active Reported Issues

사용자가 "아직 안 된다"고 재보고한 항목은 구현만 다시 보지 않고,
먼저 검증 방식 자체가 실패를 잡도록 강화한 뒤 수정한다.

## Current Checklist

- [ ] Data center select: 그룹명은 영어로 나오지만 흐리게 렌더링됨
  - 재보고일: 2026-05-09
  - 검증 보강: 실제 `Title_DataCenter.uld`/`Title_Worldmap.uld` route가 쓰는 font와 texture를 확인하고, 해당 라벨을 clean global 렌더와 픽셀/metrics 비교한다.
  - 수정 방향: 데이터센터 선택 화면의 ASCII 라벨은 TTMP 한글 폰트가 아니라 clean global ASCII glyph/kerning/texture를 사용해야 한다.

- [ ] Data center select: 서버명은 영어로 나오지만 문자 간격이 서로 침범함
  - 재보고일: 2026-05-09
  - 검증 보강: `Elemental`, `Gaia`, `Mana`, 실제 월드명 전체를 데이터센터 화면 route font에서 문장 단위 layout으로 검사한다.
  - 수정 방향: 월드/데이터센터 ASCII 라벨의 advance/kerning을 clean global과 일치시키고, TTMP texture cell에 덮어쓴 ASCII가 흐리거나 좁게 나오지 않게 한다.

- [ ] Data center select: 화면을 나가는 버튼이 `-로?` / `-료?`처럼 잘못 표시됨
  - 재보고일: 2026-05-09
  - 검증 보강: 데이터센터 선택 화면 종료/뒤로가기/취소 버튼 row를 찾아 base client 언어 슬롯별로 확인하고, 실제 버튼 문자열 glyph를 fallback `-`/`=`와 비교한다.
  - 수정 방향: 시작 화면 전용 버튼은 한국어가 아니라 base client 원문을 유지하거나, 한글을 유지할 경우 해당 route font가 한글을 정상 렌더링해야 한다.

- [ ] Start screen system settings: UI 배율을 키우면 한글이 `=`로 깨지고 문자 간격이 침범함
  - 재보고일: 2026-05-09
  - 검증 보강: 시작 화면 시스템 설정이 실제로 쓰는 ULD/font route를 찾고, 150/200/300%에서 해당 route의 한글 대표 문구를 glyph fallback/layout 검사에 넣는다.
  - 수정 방향: 인게임 한글 폰트를 오염시키지 않고, 시작 화면 시스템 설정 전용 font route에 필요한 한글 glyph와 spacing만 보정한다.

- [ ] Data center select popup: `데이터 센터 Mana에 입장합니다` 계열 팝업 문구가 base client 언어로 나옴
  - 재보고일: 2026-05-09
  - 검증 보강: 팝업 문구의 sheet/row/column과 lookup 구조를 찾아 EN/JA 각각에서 값이 의도한 언어인지 확인한다.
  - 수정 방향: 글로벌 전용 lookup row가 아닌 실제 번역 가능한 row라면 한국어로 유지하고, lookup 구조가 글로벌 전용이면 SeString 구조를 깨지 않는 방식으로 literal만 병합한다.

- [x] ESC system menu: `コンテンツシェア`/`コンフィグシェア` 계열 설정 공유 메뉴 제목이 base client 언어로 남음
  - 보고일: 2026-05-09
  - 원인: 창 내부 Addon row는 번역됐지만 ESC 시스템 메뉴 항목은 `MainCommand#99`를 참조하며, 한국 서버의 해당 row title/description이 비어 있어 기존 Addon-only 보정이 닿지 않았음.
  - 검증 보강: `configuration-sharing` verifier가 `Addon#17300/17301`뿐 아니라 `MainCommand#99`의 title/description column을 검사하고, 일본어/영어 공유 제목이 남으면 실패하도록 한다.
  - 수정 방향: `MainCommand#99` title과 description을 설정 공유 정책 literal로 보정한다.

- [ ] In-game: `즉시 발동` 등 일부 한글 폰트가 크게 보임
  - 재보고일: 2026-05-09
  - 사용자 정정: 폰트가 깨지거나 잘못된 폰트로 보이는 문제는 아님. 다른 인게임 UI 폰트보다 상대적으로 커 보이는지 확인해 달라는 관찰 항목임.
  - 현재 관찰: 인게임 한글 폰트 계열은 원하는 폰트로 돌아왔음. 다만 일부 문구의 glyph size/advance가 원래 TTMP 기준인지, 패치로 과대화된 것인지 확인 필요.
  - 검증 보강: `즉시 발동`과 비슷한 짧은 UI 문구를 TTMP 원본 대비 pixel/metrics/source preservation으로 비교한다.
  - 수정 방향: TTMP 원본과 다르면 해당 route를 복구한다. TTMP 원본과 같다면 현재 패치의 깨짐/오염 이슈가 아니라 UI route 자체의 기대 폰트 크기 문제로 분리한다.

- [ ] In-game: `탐사대 호위대원`의 `호` glyph 깨짐
  - 검증 보강: `탐사대 호위대원` 문장을 glyph visibility/fallback/layout 검사에 포함한다.
  - 수정 방향: 특정 글자만 하드코딩하지 않고, Hangul glyph source/atlas 충돌을 일반 규칙으로 처리한다.

- [ ] Occult Crescent: 메인은 `PHANTOM KNIGHT` / `Ph. Knight`, 서브는 `서포트 나이트`
  - 검증 보강: `MkdSupportJob` playable row 전체의 메인 full/short column과 support 설명 column을 확인한다.
  - 수정 방향: 특정 Knight만 예외 처리하지 않고, column 역할 기준으로 매핑한다.

- [ ] Story completion card: 스토리 종료 후 컷씬/이벤트 이미지 설명문이 base client 언어로 나옴
  - 보고일: 2026-05-09
  - 사용자 기대: 폰트만 교체해서 한글이 보이게 하는 방식이 아니라, 지역 이동 시 나오는 이미지형 지역명처럼 실제 표시 리소스를 한국 클라이언트 리소스로 바꿔야 함.
  - 현재 판단: 아직 scene 리소스라고 단정하지 않는다. `EventImage`일 가능성도 있으므로 먼저 어떤 sheet/resource가 표시를 담당하는지 판별한다.
  - 후보 경로: `EventImage`, `CutScreenImage`, `ScreenImage`, `DynamicEventScreenImage`, `LoadingImage`, 또는 컷씬/이벤트 전용 texture/ULD/bundle. 이미 `UiPatchGenerator`가 일부 sheet 기반 이미지 리소스를 복사하므로, 누락된 sheet/row/texture path를 찾아 같은 계열로 확장한다.
  - 검증 보강: story 완료 화면에서 표시되는 image ID와 실제 `ui/icon/...`, `ui/loadingimage/...`, event/cutscene texture path를 diagnostics에 남기고, global target 리소스와 Korean source 리소스의 packed bytes/hash가 다르면 Korean source가 output index에 매핑되었는지 확인한다.
  - 수정 방향: 1) 완료 설명문이 텍스트 EXD인지 이미지 texture인지 먼저 판별한다. 2) 이미지라면 global row의 icon ID만 기준으로 삼지 말고 Korean EXD row도 비교해 한국 서버에서 다른 image ID를 쓰는 경우를 추적한다. 3) 언어 폴더형 리소스는 target language 폴더에 `ko` source를 복사하고, same-path 리소스는 동일 path의 Korean packed texture를 복사한다. 4) sheet 기반으로 잡히지 않을 때만 event/cutscene bundle 단위로 global clean과 Korean source를 비교해 안전하게 교체 가능한 최소 파일을 찾는다.
  - 주의 사항: 일반 자막/대사 텍스트와 혼동하지 않는다. story 종료 카드처럼 장면에 박혀 있는 설명문은 폰트 FDT 수리 대상이 아니라 image/event resource localization 대상이다.

- [ ] Follow-up: 기존 한글 폰트와 달라져 보이는 glyph 대응 어색함
  - 2026-05-09 처리: `hangul-source-preservation` verifier 추가, 전역 Hangul offset 정규화 제거.
  - 남은 방향: 새로 보고된 `즉시 발동` 계열 문구가 TTMP 원본과 같은지 별도 대표 문구로 확인한다.

## Verification Rule

- 재보고된 항목은 먼저 verifier가 실패하도록 만든다.
- verifier가 pass인데 사용자가 다시 실패를 보고하면 해당 검증이 불완전한 것으로 간주한다.
- 특정 row/codepoint만 임시로 덮지 않고, 가능한 경우 sheet column 역할, ULD route,
  FDT glyph category, atlas 충돌 회피 같은 일반 규칙으로 수정한다.
