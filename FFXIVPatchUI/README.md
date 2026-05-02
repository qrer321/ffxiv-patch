# FFXIVPatchUI

## 원작자에 대한 감사

이 UI는 FFXIV 한글 패치 원작자인 [korean-patch](https://github.com/korean-patch)의 기존 WinForms 패처 UI 흐름을 참고해 확장했습니다. 설치/제거 UX와 패치 적용 구조의 기반을 만들어주신 원작자에게 감사드립니다.

## 역할

`FFXIVPatchUI`는 사용자가 실행하는 WinForms 패처입니다. 글로벌 서버 클라이언트와 한국 서버 클라이언트 경로를 찾고, `FFXIVPatchGenerator`를 내부에서 실행해 release 파일을 생성한 뒤 패치 적용/제거/복구를 제어합니다.

이 UI는 기존 원격 release 다운로드 방식이 아니라 로컬 생성 방식을 사용합니다.

## 화면 기능

- 글로벌 서버 클라이언트 경로 표시 및 수동 변경
- 한국 서버 클라이언트 경로 표시 및 수동 변경
- 베이스 클라이언트 언어 선택
  - 일본어 클라이언트 `ja`
  - 영어 클라이언트 `en`
- 사전 점검
- 전체 한글 패치
- 한글 폰트 패치
- 한글 패치 제거
- 백업으로 복구
- 생성 폴더 열기
- 로그 폴더 열기
- 오래된 파일 정리
- 테스트 빌드 전용 경로 자동 탐색/리셋
- 테스트 빌드 전용 폰트 프로필 선택
- 테스트 빌드 전용 자동 패치

## 릴리즈 빌드 동작

릴리즈 빌드에서는 실제 글로벌 서버 클라이언트를 대상으로 동작합니다.

- 프로그램 시작 시 자동으로 사전 점검을 실행합니다.
- 사전 점검 실패 시 전체/폰트 패치 버튼을 잠급니다.
- index/index2가 이미 `dat1`을 가리키는 패치 적용 상태면 전체/폰트 패치 버튼을 잠급니다.
- 글로벌/한국 서버 `ffxivgame.ver`가 다르면 실제 패치를 잠급니다.
- 한글 채팅 입력을 위해 필요한 Scancode Map 레지스트리를 확인하고, 없으면 설치를 안내합니다.
- 패치 적용 전 대상 파일을 백업합니다.
- 패치 제거는 `orig.*.index/index2` 또는 백업된 clean index를 이용해 원래 index 참조로 되돌립니다.
- 글로벌 클라이언트 실행 중에는 실제 패치를 막습니다.

## 테스트 빌드 동작

테스트 빌드는 실제 글로벌 서버 클라이언트에 쓰지 않습니다.

- 실제 적용 버튼은 숨김 또는 비활성화됩니다.
- `테스트 자동 패치`는 생성된 release 파일을 `debug-apply` 폴더에만 복사합니다.
- FFXIV가 실행 중이어도 테스트 작업은 계속 진행됩니다.
- 경로 자동 탐색/리셋 버튼이 표시됩니다.
- 폰트 프로필을 바꿔 `MiedingerMid`, `TrumpGothic`, `Jupiter`, `AXIS` 계열을 제외한 테스트 패치를 만들 수 있습니다.

## 관리 폴더

사용자별 영구 데이터는 `%LocalAppData%\FFXIVKoreanPatch` 아래에 저장됩니다.

```text
%LocalAppData%\FFXIVKoreanPatch\
├─ generated-release\   자동 생성된 패치 release 파일
├─ restore-baseline\    패치 전 복구용 clean/original index 보관
├─ backups\             실제 적용/복구 전 백업
└─ logs\                작업 로그
```

실행 파일 옆 `Release\Public` 또는 `Release\Test` 폴더는 배포 산출물 위치입니다. 배포 산출물은 단일 실행 파일이며, 내장된 제너레이터와 TTMP 폰트 패키지는 `%LocalAppData%\FFXIVKoreanPatch\embedded-tools` 아래로 자동 추출됩니다. 백업/복구 기준 폴더는 이 배포 산출물 폴더와 분리되어 있어 빌드 산출물을 다시 만들어도 삭제되지 않습니다.

## 패치 적용 흐름

1. 글로벌/한국 서버 클라이언트 경로를 자동 탐색하거나 사용자가 지정합니다.
2. 대상 언어 슬롯을 선택합니다.
3. 사전 점검에서 필수 파일, TTMP 폰트 패키지, index 상태, 복구 기준을 확인합니다.
4. 실행 파일 옆에 `patch-policy.json`이 있으면 제너레이터에 전달합니다.
5. UI가 `FFXIVPatchGenerator.exe`를 실행합니다.
6. 제너레이터 진행도는 UI progress bar에 표시됩니다.
7. 생성된 release 파일의 `manifest.json`을 기록합니다.
8. 적용 대상 파일을 먼저 백업합니다.
9. index/index2/dat1/dat4 파일을 글로벌 클라이언트 sqpack 폴더에 복사합니다.
10. 작업 결과, 제너레이터 요약, 로그 경로를 결과 창에 표시합니다.

`patch-policy.json`은 특정 sheet/row/column 보정이 필요할 때만 사용합니다. 파일이 없으면 기본 내장 정책으로 동작합니다.

폰트 패치를 포함하는 작업은 `060000` UI 텍스처 패치도 함께 생성합니다. 이 패치는 파티 리스트 본인 번호 텍스처와 `ScreenImage`, `CutScreenImage`, `TerritoryType`, `Map` 등 이미지형 UI 리소스를 교체해, 지역 이동이나 던전 진입 시 표시되는 이미지형 타이틀/부제와 지도 이미지 안의 표기가 한국어 리소스를 사용하도록 보정합니다. 폰트 패치 쪽에서는 파티 리스트 본인 번호 PUA glyph(`U+E0E1`~`U+E0E8`)를 clean global의 속 빈 네모 번호 모양으로 복원하고, 해당 픽셀만 TTMP 폰트 atlas의 빈 영역에 이식합니다. 데이터 센터 선택 화면은 글로벌 전용 로비 UI이므로 대상 글로벌 언어 row를 사용하고, `Title_DataCenter.uld`의 `TrumpGothic` 제목 노드는 `Jupiter` 폰트 슬롯으로 보정합니다. 텍스트 패치 쪽에서는 파티 리스트 본인 표시 glyph, 데이터센터 로비 row, 버프/남은시간의 좁은 시간 단위를 내장 예외로 보정하며, 로비/공용 UI가 선택 언어 외 슬롯을 읽는 경우를 고려해 일부 안전 row는 `ja/en/de/fr` 슬롯을 함께 보정합니다. 또한 전체 텍스트 패치 생성 시 `quest/*`의 `TEXT_*_SAY_*` 입력 문구를 백틱 문자 `` ` `` 로 익명화해, 일반 채팅으로 한국어 주문/문구를 직접 입력해야 하는 퀘스트의 입력 문제를 줄입니다.

## 패치 제거와 복구

`한글 패치 제거`는 패치 적용 시 생성된 `orig.*.index/index2` 또는 로컬 복구 기준을 찾아 index를 원래 상태로 되돌립니다. `dat1`/`dat4` 파일이 남아 있어도 index가 참조하지 않으면 게임은 해당 패치 데이터를 사용하지 않습니다.

`백업으로 복구`는 적용/복구 전에 만들어둔 백업 폴더를 선택해 직접 복원합니다. 복구 전에도 현재 대상 파일을 다시 백업해 수동 롤백 여지를 남깁니다.

## 빌드

루트에서 실행합니다.

```powershell
.\Scripts\build-release.ps1
.\Scripts\build-test.ps1
```

출력:

```text
Release\Public\FFXIVKoreanPatch.exe
Release\Test\FFXIVKoreanPatch.Test.exe
```

두 빌드 모두 같은 UI 소스를 사용하지만 `TEST_BUILD` 조건부 컴파일로 실제 적용 가능 여부와 테스트 전용 버튼 노출이 달라집니다.
