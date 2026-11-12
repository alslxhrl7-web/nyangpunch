# 냥펀치

상자를 합쳐 은신처를 키우고, 한 칸씩 생각하며 거실을 지키는 고양이 게임입니다.

- 플레이: https://na-min-jea.itch.io/nyangpunch (브라우저에서 바로 실행)
- 장르: 방치형 머지 + 턴제 퍼즐
- 엔진: Unity 6 (6000.6), uGUI + TextMeshPro, 화면은 모두 실행 중에 코드로 만듭니다

## 구성

- **박스 은신처(방치 + 머지):** 택배 상자를 받아 같은 상자끼리 합치면 한 단계 큰 상자가 됩니다. 꺼져 있어도 최대 8시간 동안 츄르가 쌓입니다.
- **거실 방어(턴제 퍼즐):** 7×7 칸에서 내가 한 칸 움직이면 적도 한 칸 움직입니다. 적 쪽으로 움직이면 냥펀치로 밀어내고, 벽·가구·다른 적에 부딪히면 더 아픕니다.
- 층을 깨면 선물 상자가 은신처로 오고, 큰 상자를 만들면 거실 방어 스킬이 열립니다.

## 폴더

| 위치 | 내용 |
|---|---|
| `Assets/NyangPunch/Scripts` | 게임 코드. `Puzzle.cs`가 거실 방어 규칙, `Game*.cs`가 화면 |
| `Assets/NyangPunch/Resources` | 그림(스프라이트), 효과음, 글꼴(Jua, SIL OFL) |
| `Assets/NyangPunch/Editor` | 씬 만들기, WebGL 빌드 스크립트 |
| `Tools/sprite-export` | 그림을 코드로 그려 PNG로 뽑는 도구 |
| `Tools/sound` | 효과음 후보 비교 페이지와 자르기 도구 |
| `Tools/promo` | itch.io 홍보 이미지와 개발 일지 |
| `CLAUDE.md` | 작업 규칙(밸런스, 테스트, 빌드, 배포 절차) |

## 만든 방식

이 게임은 AI 도구(Claude)와 대화하며 만들었습니다. 코드와 게임 안 그림(그리는 코드)은 AI가 작성했고, 효과음은 VARCO Sound로 만들어 골랐습니다. 무엇을 만들지, 어떤 장르로 갈지, 규칙과 밸런스는 직접 플레이해 보고 정했습니다. 과정은 `Tools/promo/devlog-01.md`에 적어 두었습니다.

## 열어 보기

Unity 6000.6으로 프로젝트를 열고 메뉴 **NyangPunch > Build Main Scene**을 실행한 뒤 Play를 누릅니다. `Packages/manifest.json`의 `com.claudehand.tools`와 `com.coplaydev.unity-mcp`는 개발 보조 도구라서, 없으면 그 두 줄을 지우고 열면 됩니다.
