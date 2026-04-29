# FFXIV Korean Patch Tool

파이널판타지14 한국 서버 클라이언트의 한글 텍스트/폰트 리소스를 이용해 글로벌 서버 클라이언트의 지정 언어 슬롯에 적용할 패치 파일을 생성하고, UI에서 설치/제거까지 제어하기 위한 WinForms 기반 도구입니다.

현재 목표는 **한국 서버 한글 클라이언트 → 글로벌 서버 일본어/영어 클라이언트 한글화 패치**를 한 폴더 안에서 생성, 테스트, 적용, 복구할 수 있게 만드는 것입니다.

## 구성

```text
.
├─ FFXIVPatchUI/          WinForms UI, 설치/제거/테스트/경로 탐색 담당
├─ FFXIVPatchGenerator/          패치 release 파일을 생성하는 콘솔 빌더
├─ Scripts/               릴리즈/테스트 빌드 스크립트
├─ Docs/                  릴리즈 빌드 메모
├─ Release/               빌드 결과물 출력 폴더, Git에는 올리지 않음
├─ FfxivKoreanPatch.sln   UI와 FFXIVPatchGenerator를 묶은 루트 솔루션
└─ README.md
```

## 주요 프로젝트

- `FFXIVPatchUI\Main`
  - 실제 사용자가 실행하는 WinForms UI입니다.
  - 글로벌 서버 클라이언트와 한국 서버 클라이언트 경로를 자동 탐색하거나 수동 지정합니다.
  - 패치 릴리즈 생성 진행도를 UI에서 표시합니다.
  - 테스트 빌드에서는 실제 글로벌 클라이언트에 쓰지 않고 `debug-apply` 폴더에만 적용합니다.
  - 릴리즈 빌드에서는 백업, 설치, 제거, 복구 흐름을 제공합니다.

- `FFXIVPatchGenerator`
  - UI에서 호출되는 패치 생성기입니다.
  - 글로벌/한섭 `sqpack\ffxiv`의 index/dat 파일을 읽습니다.
  - 글로벌 클라이언트의 `root.exl`과 EXH 구조를 기준으로 대상 언어 EXD를 재생성합니다.
  - 한섭 `*_ko.exd`의 SeString 바이트를 글로벌 `*_ja.exd` 또는 `*_en.exd`에 반영합니다.
  - 텍스트 패치 파일과 폰트 패치 파일을 생성합니다.

## 참고한 코드

이 프로젝트는 기존 `ffxiv-patch-main` WinForms 패처 UI를 기반으로 UI/설치 흐름을 유지하면서, 별도 콘솔형 패치 생성기인 `FFXIVPatchGenerator`를 추가해 한 폴더에서 동작하도록 정리한 것입니다.

추가로 한글 패치 제작자에게 공유받은 기존 패치 제너레이터 소스도 참고했습니다.

참고한 주요 내용:

- EXH/EXD 구조 파싱 방식
- `root.exl` 기준 sheet 순회 방식
- `TEXT_...` 형태 string key 기반 row 매핑
- string key가 없는 일부 sheet에서 row id 기반 fallback을 허용하는 목록
- 폰트 리소스 대상 목록
- `0a0000.win32.index/dat1`, `000000.win32.index/dat1`, `orig.*.index` 형태의 산출물 구성

단, 기존 제너레이터의 하드코딩된 설치 경로와 `distrib` 폴더 직접 삭제 방식은 사용하지 않았습니다. 이 프로젝트는 원본 게임 폴더에 바로 생성물을 쓰지 않고, UI가 관리하는 출력 폴더 아래에 먼저 release 파일을 생성합니다.

## 생성되는 결과물

릴리즈 생성 시 언어와 버전별 폴더 아래에 다음 파일이 만들어집니다.

텍스트 패치:

```text
0a0000.win32.dat1
0a0000.win32.index
orig.0a0000.win32.index
ffxivgame.ver
manifest.json
```

폰트 패치를 포함한 경우 추가 생성:

```text
000000.win32.dat1
000000.win32.index
orig.000000.win32.index
```

UI 실행 중 생성/관리되는 주요 폴더:

```text
generated-release\   자동 생성된 패치 release 파일
restore-baseline\    복구용 clean/original index 보관
backups\             실제 적용 전 백업
logs\                작업 로그
```

테스트 빌드에서는 실제 글로벌 클라이언트에 적용하지 않고 생성된 release 폴더 아래의 `debug-apply\game\sqpack\ffxiv`에만 파일을 복사합니다.

## 빌드

릴리즈 빌드:

```powershell
.\Scripts\build-release.ps1
```

결과물:

```text
Release\Public\
├─ FFXIVKoreanPatch.exe
├─ FFXIVKoreanPatch.exe.config
├─ FFXIVKoreanPatchUpdater.exe
├─ FFXIVKoreanPatchUpdater.exe.config
└─ FFXIVPatchGenerator.exe
```

테스트 빌드:

```powershell
.\Scripts\build-test.ps1
```

결과물:

```text
Release\Test\
├─ FFXIVKoreanPatch.Test.exe
├─ FFXIVKoreanPatch.Test.exe.config
└─ FFXIVPatchGenerator.exe
```

## GitHub 업로드 기준

소스 코드만 저장소에 올립니다.

커밋 대상:

- `FFXIVPatchUI\`
- `FFXIVPatchGenerator\`
- `Scripts\`
- `Docs\`
- `FfxivKoreanPatch.sln`
- `README.md`
- `.gitignore`
- `.gitattributes`

커밋하지 않는 대상:

- `Release\`
- `bin\`
- `obj\`
- `generated-release\`
- `restore-baseline\`
- `backups\`
- `logs\`
- `*.exe`, `*.dll`, `*.pdb`, `*.zip`

실행 바이너리는 GitHub 소스 커밋이 아니라 GitHub Releases에 `Release\Public` 안의 파일만 업로드하는 방식으로 배포합니다.

## 안전장치

- 원본 글로벌/한섭 게임 폴더에는 패치 생성물을 직접 쓰지 않습니다.
- 패치 생성물은 UI가 관리하는 출력 폴더 아래에 먼저 생성됩니다.
- 출력 폴더가 원본 게임 폴더 내부면 중단합니다.
- 글로벌 index가 이미 dat1을 가리키는데 복구용 `orig.*.index`가 없으면 기본적으로 중단합니다.
- 테스트 빌드에서는 실제 글로벌 클라이언트 적용 버튼이 막히고, 테스트용 `debug-apply` 경로만 사용합니다.

## 현재 제한

- EXD Default variant 중심으로 처리합니다.
- Subrows variant sheet는 아직 스킵합니다.
- 실제 배포용 release 생성에는 clean index 또는 복구용 original index 확보가 필요합니다.
