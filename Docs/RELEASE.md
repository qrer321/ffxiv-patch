# 릴리즈 빌드 문서

## 목적

릴리즈 빌드는 실제 사용자에게 배포할 실행 파일 묶음을 생성합니다. 소스 저장소에는 바이너리를 커밋하지 않고, `Release\Public` 폴더의 파일만 GitHub Releases에 업로드하는 것을 기준으로 합니다.

## 빌드 명령

루트 폴더에서 실행합니다.

```powershell
.\Scripts\build-release.ps1
```

스크립트가 수행하는 작업:

- `FFXIVPatchGenerator\build.ps1`로 제너레이터 빌드
- `FfxivKoreanPatch.sln` Release 구성 빌드
- UI 출력 폴더에 `FFXIVPatchGenerator.exe` 복사
- `Release\Public` 폴더 생성
- 배포에 필요한 런타임 파일만 복사
- 기존 upstream self-updater 파일이 남아 있으면 제거

## 릴리즈 산출물

```text
Release\Public\
├─ FFXIVKoreanPatch.exe
├─ FFXIVKoreanPatch.exe.config
├─ FFXIVPatchGenerator.exe
├─ TTMPD.mpd
└─ TTMPL.mpl
```

`TTMPD.mpd`와 `TTMPL.mpl`은 로컬에 파일이 있을 때만 포함됩니다. 안정적인 폰트 패치를 위해 실제 배포용 릴리즈에는 두 파일이 포함되어야 합니다.

## 포함하지 않는 파일

현재 구조에서는 다음 파일을 릴리즈에 포함하지 않습니다.

- `FFXIVKoreanPatchUpdater.exe`
- `FFXIVKoreanPatchUpdater.exe.config`
- `SHA1Producer.exe`
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
├─ FFXIVKoreanPatch.Test.exe
├─ FFXIVKoreanPatch.Test.exe.config
├─ FFXIVPatchGenerator.exe
├─ TTMPD.mpd
└─ TTMPL.mpl
```

테스트 빌드는 실제 글로벌 클라이언트에 적용하지 않고 `debug-apply` 폴더에만 파일을 복사합니다.
테스트 빌드에서는 폰트 프로필을 선택해 특정 폰트군을 제외한 release를 만들 수 있으므로, 파티 리스트 숫자처럼 특정 UI glyph가 깨지는 문제를 분리 검증할 때 사용합니다.

## 배포 전 확인

- 릴리즈 빌드가 경고 0개, 오류 0개로 완료되는지 확인합니다.
- `Release\Public`에 updater 파일이 남아 있지 않은지 확인합니다.
- `TTMPD.mpd`, `TTMPL.mpl` 포함 여부를 확인합니다.
- 실행 후 사전 점검 창이 자동으로 뜨는지 확인합니다.
- 실제 패치 버튼이 사전 점검 통과 전에는 잠겨 있는지 확인합니다.
- 글로벌/한국 서버 버전이 다르면 릴리즈 빌드에서 실제 패치가 잠기는지 확인합니다.
- 이미 패치된 index 상태에서는 전체/폰트 패치 버튼이 잠기는지 확인합니다.
- `한글 패치 제거`와 `백업으로 복구`가 index2까지 복구 대상으로 잡는지 확인합니다.
- 패치 완료 결과 창에 EXD/RSV/진단 파일 요약이 표시되는지 확인합니다.

## GitHub Release 준비

바이너리 배포 준비는 다음 스크립트로 수행합니다.

```powershell
.\Scripts\publish-release.ps1 -TagName v2026.04.30
```

기본 실행은 준비 전용입니다. 다음 작업만 수행하고 GitHub에는 아무것도 만들지 않습니다.

- 릴리즈 빌드 실행
- `Release\Public` 필수 파일 검증
- `Release\GitHub\<tag>\FFXIVKoreanPatch-<tag>.zip` 생성
- SHA256 파일 생성
- `release-notes.md` 생성

작업 트리가 깨끗하지 않으면 기본적으로 중단합니다. 로컬 산출물 형태만 확인하려면 다음처럼 실행할 수 있습니다.

```powershell
.\Scripts\publish-release.ps1 -TagName local-check -SkipBuild -AllowDirty -Force
```

## GitHub Release 배포

실제 배포는 사용자가 명시적으로 `-Publish`를 붙였을 때만 진행합니다.

```powershell
.\Scripts\publish-release.ps1 -TagName v2026.04.30 -Publish
```

배포 시 수행하는 작업:

- GitHub CLI 로그인 상태 확인
- 현재 브랜치가 `main`인지 확인
- 로컬 HEAD가 `origin/main`과 같은지 확인
- 같은 태그가 로컬/원격에 없는지 확인
- annotated git tag 생성
- 태그 push
- GitHub Release 생성
- zip과 SHA256 파일 업로드

초안 릴리즈로 만들려면 `-Draft`, 프리릴리즈로 표시하려면 `-Prerelease`를 함께 사용합니다.

```powershell
.\Scripts\publish-release.ps1 -TagName v2026.04.30-beta.1 -Publish -Draft -Prerelease
```
