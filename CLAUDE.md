# 냥펀치 (Unity) 작업 규칙

- 모든 설명은 한국어로 한다.
- **밸런스 수치를 바꾸면 `Assets/NyangPunch/Scripts/Defs.cs`의 `BalanceVersion`을 1 올린다.** 에디터(와 개발 빌드)에서는 버전이 다른 저장이 자동으로 새로 시작되므로, 테스트는 항상 새 밸런스로 처음부터 진행된다.
- **배포 빌드에서는 밸런스가 바뀌어도 플레이어 저장을 지우지 않는다(사용자 지시).** `Game.Load`가 예전 저장을 `SaveKey + ".bak<버전>"`에 보관한 뒤 그대로 이어 가고, 범위를 벗어난 값만 바로잡는다. 저장 구조를 바꿀 때는 예전 저장이 읽히는지 확인한다.
- 에디터에서는 메뉴 **NyangPunch > 테스트: 플레이마다 새로 시작**이 기본으로 켜져 있어, Play를 누를 때마다 저장이 지워진다. 이어하기를 시험할 때만 끈다.
- 자동 봇 테스트는 `SaveData.Fresh()`로 만든 새 저장에서 돌린다(기존 저장을 재사용하지 않는다).
- 그림은 `Tools/sprite-export`로 HTML 프로토타입에서 다시 뽑는다. 글꼴은 Jua(SIL OFL, `Resources/Fonts/Jua-OFL.txt`).

## 효과음
- `Sfx.cs`는 `Resources/Sfx/<키>.wav`가 있으면 그 녹음을 쓰고, 없으면 코드로 만든 삐 소리(합성음)를 쓴다. 키가 둘 다 없으면 대체 키로 넘어간다(`clear`→`level`, `bump_pot`→`bump`, `squeak`→`pop`).
- 녹음은 바르코 사운드 결과를 `Tools/sound/cut.html`로 자른 것이다(앞뒤 무음 제거, 첫 소리 덩어리만, 음량 맞춤, 44.1kHz 모노 WAV). `node Tools/sound/serve.js`를 띄우고 `http://localhost:8770/Tools/sound/cut.html`을 열면 다시 만들어진다.
- 후보 비교는 `Tools/sound/index.html`, 영상으로 만든 소리 보고는 `Tools/sound/video-report.html`. 상황별 플레이 영상은 에디터 전용 `DevRecorder.Record("<장면>")`로 찍는다.
- 적 처치 소리는 종류별: 쥐·엘리트 쥐 `squeak`, 파리 `kill_fly`, 오이 `kill_cucumber`, 보스 `kill_boss`(`Puzzle.KillSound`). 화분에 부딪힐 때만 `bump_pot`, 층 클리어는 `clear`, `pop`은 목욕통이 파리를 내보낼 때와 대체용.
- 소리 방향은 **카툰풍**(사용자 지시). 사실적인 폴리 소리는 게임 그림과 안 맞는다고 했다. 비교 페이지에는 고른 소리와 새 후보만 보여 준다(지난 후보를 쌓아 두지 않는다).
- **"안 돼요" 오류음은 없앴다(사용자 지시).** 안 되는 행동은 소리 대신 행동으로 막는다: 쓸 수 없는 버튼은 꺼지고 이유를 적는다(택배·강화·털뭉치·하악질), 벽·가구 쪽 이동은 고양이가 살짝 부딪혔다 돌아온다(턴 소모 없음), 털뭉치 대각선 탭은 가까운 축으로 던진다.
- 규칙: 적이 화분(obs 2)에 부딪히면 화분이 깨져 사라진다(`Puzzle.BreakPot`, 밀려서 부딪힐 때와 로봇청소기 돌진 모두). 쿠션(obs 1)과 벽은 그대로 남는다.

## 웹GL 빌드 (itch.io)
- 6000.6.8f1에는 WebGL 모듈이 없고, 6000.6.7f1의 WebGL 모듈은 불완전(`BuildTools/prejs` 없음)해서 빌드가 실패한다. **6000.6.0f1로 빌드한다.**
- 원본 프로젝트는 6.8로 열려 있으므로 `Builds/webgl-project`에 사본(Assets·Packages·ProjectSettings)을 만들고, 사본의 manifest에서 MCP·claude-hand 패키지를 빼고, `ProjectVersion.txt`를 6000.6.0f1로 바꿔서 빌드한다.
- 명령: `"C:\Program Files\Unity\Hub\Editor\6000.6.0f1\Editor\Unity.exe" -batchmode -quit -projectPath <사본> -buildTarget WebGL -executeMethod NyangPunch.EditorTools.WebGLBuild.Build -logFile Builds\webgl-build.log`
- `WebGLBuild`가 Brotli+압축해제 대체(itch.io 헤더 불필요), 코드 제거 높음, TMP 셰이더 포함, 창 채우기 index.html(`Editor/WebGLTemplate`)까지 처리한다. 결과 약 6MB.
- 빌드 사본은 원본과 Assets가 같아야 한다. 원본에서 바꾼 파일(새 파일이면 .meta도)을 사본에 복사한 뒤 빌드한다. `diff -rq Assets Builds/webgl-project/Assets | grep -v '\.meta'`로 확인.
- **zip 파일은 만들지 않는다(사용자 지시).** 업로드는 butler로 빌드 폴더를 그대로 올린다:
  `"%LOCALAPPDATA%\itch-butler\butler.exe" push "<사본>\Builds\WebGL" na-min-jea/nyangpunch:html5 --userversion <날짜-내용>`
  butler는 `%LOCALAPPDATA%\itch-butler`에 설치되어 있고 로그인도 되어 있다. 같은 `html5` 채널에 올리면 itch.io 설정은 그대로 유지된다.
- itch.io 설정: `html5` 업로드가 "played in the browser", Embed in page 450×800, Portrait, Mobile friendly, Fullscreen button. 예전 `nyangpunch-webgl.zip`과 옛 HTML 버전(index.html)은 숨김 처리만 해 둠.
- 웹 저장: itch.io는 업로드마다 주소가 바뀌고 Unity 웹 `PlayerPrefs`는 주소별로 저장된다. 그래서 `SaveStore`가 브라우저 localStorage(`na-min-jea/nyangpunch:` + SaveKey)에도 저장하고 그걸 먼저 읽는다. 처음 실행할 때 localStorage가 비어 있으면 `/idbfs/*/PlayerPrefs`에서 예전 빌드의 가장 최근 저장을 가져온다.

## 배포 전 기준 (사용자 지시)
- 게임 규칙·소리를 바꾼 뒤 배포하기 전에는 **자동 플레이 테스트를 1층부터 10층 보스까지 10번** 돌린다: 플레이 모드에서 `NyangPunch.DevSelfTest.Run(10, 10)`. 매 판 새 저장으로 시작하고, 봇 하트를 계속 채워서 난이도가 아니라 버그(층이 안 끝남, 판 상태 깨짐, 예외, 재생할 것 없는 소리)를 찾는다.
- **보스 테스트와 문제 찾기는 Opus 5.5 · 높음(high)으로 돌리는 하위 작업자에게 맡긴다.**
- 문제가 없으면 그대로 배포(웹 빌드 → butler)까지 진행하고, 문제가 있으면 그 부분을 고친 뒤 다시 10번 돌려 통과한 다음 배포한다.
