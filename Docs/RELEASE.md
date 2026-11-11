# 릴리즈 빌드 문서

## 목적

릴리즈 빌드는 실제 사용자에게 배포할 실행 파일 묶음을 생성합니다. 소스 저장소에는 바이너리를 커밋하지 않고, `Release\Public` 폴더의 파일만 GitHub Releases에 업로드하는 것을 기준으로 합니다.

## 빌드 명령

루트 폴더에서 실행합니다.

```powershell
.\Scripts\build-release.ps1
```

스크립트가 수행하는 작업:

- 필수 TTMP 폰트 입력 존재·크기 확인 (`FFXIVPatchGenerator\FontPatchAssets\TTMPD.mpd`, `TTMPL.mpl`)
- `FFXIVPatchGenerator\build.ps1`로 제너레이터 빌드
- `FfxivKoreanPatch.sln` Release 구성 빌드
- UI exe에 `FFXIVPatchGenerator.exe`, TTMP 폰트 패키지, `rsv.json` 내장
- `Release\Public` 폴더 생성
- 배포용 단일 실행 파일 복사
- 내장된 네 payload의 SHA256을 빌드 입력과 대조
- 기존 upstream self-updater 파일이 남아 있으면 제거

## 릴리즈 산출물

```text
Release\Public\
└─ FFXIVKoreanPatch.exe
```

`FFXIVKoreanPatch.exe` 하나에 `FFXIVPatchGenerator.exe`, `TTMPD.mpd`, `TTMPL.mpl`, `rsv.json`이 내장됩니다. 실행 시 `%LocalAppData%\FFXIVKoreanPatch\embedded-tools` 아래로 자동 추출해 사용합니다.

RSV 입력을 고정하려면 빌드 전 `FFXIV_RSV_MAP_PATH`를 별도로 확보한 staged JSON 경로로 지정합니다. 지정하지 않으면 기존 upstream 맵을 내려받습니다. TTMP는 저장소에 커밋하지 않는 필수 입력이므로, 새 빌드 환경에도 검증한 폰트 패키지를 따로 준비해야 합니다.

## 포함하지 않는 파일

현재 구조에서는 다음 파일을 릴리즈에 포함하지 않습니다.

- `FFXIVKoreanPatchUpdater.exe`
- `FFXIVKoreanPatchUpdater.exe.config`
- `SHA1Producer.exe`
- `FFXIVPatchGenerator.exe`
- `TTMPD.mpd`
- `TTMPL.mpl`
- `*.config`
- `*.pdb`
- `bin`, `obj`
- `generated-release`, `restore-baseline`, `backups`, `logs`

업데이트/다운로드 방식은 제거되었고, UI가 로컬 제너레이터를 실행해 필요한 release 파일을 생성합니다.

## 테스트 빌드

테스트용 실행 파일은 별도 명령으로 생성합니다.

```powershell
.\Scripts\build-test.ps1
```

출력:

```text
Release\Test\
└─ FFXIVKoreanPatch.Test.exe
```

테스트 빌드는 실제 글로벌 클라이언트에 적용하지 않고 `debug-apply` 폴더에만 파일을 복사합니다.
테스트 빌드에서는 폰트 프로필을 선택해 특정 폰트군을 제외한 release를 만들 수 있으므로, 파티 리스트 숫자처럼 특정 UI glyph가 깨지는 문제를 분리 검증할 때 사용합니다.

## 배포 전 확인

- 릴리즈 빌드가 경고 0개, 오류 0개로 완료되는지 확인합니다.
- `Release\Public`에 `FFXIVKoreanPatch.exe` 하나만 남아 있는지 확인합니다.
- 실행 후 내장 제너레이터와 TTMP 폰트 패키지가 자동 추출되는지 확인합니다.
- 로컬 내장 도구가 없는 상태와 TTMP가 삭제된 상태에서도 네 payload를 다시 추출하는지 확인합니다.
- 실행 후 사전 점검 창이 자동으로 뜨는지 확인합니다.
- 실제 패치 버튼이 사전 점검 통과 전에는 잠겨 있는지 확인합니다.
- 글로벌/한국 서버 버전이 다르면 릴리즈 빌드에서 실제 패치가 잠기는지 확인합니다.
- 이미 패치된 index 상태라도 clean base index가 current/orig/같은 버전의 restore-baseline에 있으면 전체/폰트 패치를 재적용할 수 있고, clean base가 없으면 버튼/시작 가드가 차단하는지 확인합니다.
- `한글 패치 제거`와 `백업으로 복구`가 index2까지 복구 대상으로 잡는지 확인합니다.
- 게임 업데이트 뒤 `한글 패치 제거`가 패치 엔트리 없는 index를 덮어쓰지 않고, 이전 버전의 orig index를 복원하지 않는지 확인합니다. 이전 버전 index가 설치된 상태에서는 사전 점검이 실패하고 패치 버튼이 잠기는지 확인합니다.
- 패치 완료 결과 창에 EXD/RSV/진단 파일 요약이 표시되는지 확인합니다.
- `전체 한글`과 `직접 설정`, JA/EN, 모든 텍스트 베이스·이미지 전용·기타 텍스트 한국어/이미지 베이스 구성을 구분해 확인합니다.
- Story와 Full 기준선 검사는 `--checks`로 각각 선택합니다. Story 검사에는 `--korea`, Full 비교에는 `--baseline-output`을 명시합니다. Story 경계/SeString 검사에 실제 비교 대상이 없으면 정상 검증으로 취급하지 않습니다.
- 개발 검증에는 설치된 게임 폴더를 읽거나 사용하지 않습니다. 별도 백업/내보내기에서 필요한 파일만 `.sandbox/.tmp/`에 복사한 staged 입력을 사용합니다.
- 합성 아카이브·WPF 검증과 실제 게임 확인을 구분합니다. 아직 확인하지 않은 실제 표시/호환성은 [KNOWN_ISSUES.md](KNOWN_ISSUES.md)에 남깁니다.

## 배포 작업 목록 관리

[RELEASE_CHANGES.md](RELEASE_CHANGES.md)가 다음 배포에 포함할 실제 작업 목록입니다. 사용자 기능, 수정 문제, 사용법 변화, 생성·배포 내부 변경, 검증 범위와 남은 제한을 정리합니다.

- 배포 준비와 실제 게시 모두 이 파일을 기본으로 읽습니다. 다른 버전의 목록은 `-ChangesFile <path>`로 지정할 수 있습니다. 상대 경로는 저장소 루트 기준입니다.
- 태그·커밋·실행 파일 이름·SHA256은 자동 생성하고, 변경 사항 본문은 작성한 목록을 그대로 넣습니다. 커밋 제목만으로 본문을 대체하지 않습니다.
- 파일이 없거나 비어 있으면 빌드·staging 전에 중단합니다.
- 다음 배포 전에는 이전 배포 항목을 그대로 재사용하지 말고 포함 커밋/변경 범위와 대조해 갱신한 뒤 소스와 함께 커밋합니다.
- 생성된 `Release\GitHub\<tag>\release-notes.md`를 직접 수정하지 않습니다. 재준비·게시 시 다시 생성되므로 원본 작업 목록을 수정해야 합니다.

## GitHub Release 준비

바이너리 배포 준비는 다음 스크립트로 수행합니다.

```powershell
.\Scripts\publish-release.ps1 -TagName v2026.04.30
```

기본 실행은 준비 전용입니다. 다음 작업만 수행하고 GitHub에는 아무것도 만들지 않습니다.

- 릴리즈 빌드 실행
- `Release\Public` 단일 exe 검증
- `Release\GitHub\<tag>\FFXIVKoreanPatch.exe` 복사
- exe SHA256 파일 생성
- 작업 목록을 포함한 `release-notes.md` 생성

작업 트리가 깨끗하지 않으면 기본적으로 중단합니다. 로컬 산출물 형태만 확인하려면 다음처럼 실행할 수 있습니다.

```powershell
.\Scripts\publish-release.ps1 -TagName local-check -SkipBuild -AllowDirty -Force
```

`-AllowDirty`는 준비 전용입니다. 이때 노트에 미커밋 작업물이 포함됐음을 표시하며, 기록된 HEAD가 실행 파일의 전체 소스를 대표한다고 표시하지 않습니다. 실제 게시에는 사용할 수 없습니다.

미커밋 feature 변경과 새 `.cs`·문서 파일을 빠뜨리지 않고 커밋한 후 main으로 통합해야 합니다. 브랜치 이름만 병합해도 아직 커밋하지 않은 변경은 들어가지 않습니다. `.sandbox/`, 빌드 산출물, TTMP 입력은 커밋 대상이 아닙니다.

## GitHub Release 배포

실제 배포는 사용자가 명시적으로 `-Publish`를 붙였을 때만 진행합니다.

```powershell
.\Scripts\publish-release.ps1 -TagName v2026.04.30 -Publish -Force
```

이미 준비한 태그 디렉터리를 다시 만들 때만 `-Force`를 사용합니다. 작업 목록을 다시 읽고 빌드하므로, main 통합·푸시 후 최신 커밋에서 실행합니다. 로컬 준비에 사용한 `local-check` 같은 이름 대신 실제 배포 태그를 지정합니다.

배포 시 수행하는 작업:

- GitHub CLI 로그인 상태 확인
- 현재 브랜치가 `main`인지 확인
- 로컬 HEAD가 `origin/main`과 같은지 확인
- 같은 태그가 로컬/원격에 없는지 확인
- annotated git tag 생성
- 태그 push
- GitHub Release 생성
- GitHub에는 `FFXIVKoreanPatch.exe` 하나만 업로드. SHA256 파일과 원본 릴리즈 노트는 로컬 staging에 보관하며 해시는 게시 본문에도 기록

초안 릴리즈로 만들려면 `-Draft`, 프리릴리즈로 표시하려면 `-Prerelease`를 함께 사용합니다.

```powershell
.\Scripts\publish-release.ps1 -TagName v2026.04.30-beta.1 -Publish -Draft -Prerelease
```
