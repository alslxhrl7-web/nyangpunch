# 작업 메모 (2026-11-13, 하트·도감·테마·생동감 작업)

이 파일은 작업이 끊겨도 이어 갈 수 있게 남기는 진행 메모다. 배포(웹 빌드·butler)는 사용자 확인 후.

## 설계 결정
- 하트: 매 층 시작 시 MaxHearts로 가득(ClaimFloor). 보스 +1 회복 제거, 대신 보스 보너스 츄르 +50%. 부활은 판마다 1번 그대로.
- 난이도 보정: 일반층 적 수 증가, 보스 체력 +1, 보스층 소환 +1. BalanceVersion 2 -> 3.
- 고양이 도감: 치즈(시작부터, 털뭉치 관통 1칸), 고등어(박스 집=4단계, 하악질 범위 2칸), 턱시도(5단계, 츄르 +20%), 삼색(6단계, 층마다 첫 피격 무효), 까망(7단계, 털뭉치·하악질 대기 -1턴).
  저장: SaveData.cats(만난 품종 비트), SaveData.pick(출전), RunData.cat, RunData.shield, RunData.tiles, SaveData.seenRules.
- 방 테마: 1~3 거실(규칙 없음), 4~5 부엌(미끄러운 타일, 5층 로봇청소기), 6~8 욕실(물웅덩이), 9~10 마당(풀숲, 10층 목욕통). 11층 이상은 반복.
  미끄러운 타일: 고양이 이동·적 밀기 시 한 칸 더. 물웅덩이: 적이 밟으면 1턴 멈춤, 고양이가 밟으면 첨벙(웅덩이 사라지고 스킬 대기 +1턴, 하트 손해 없음). 풀숲: 고양이가 서 있으면 붙어 있지 않은 일반 적은 쫓아오지 못함.
- 그림: Tools/sprite-export/draw.js + index.html에 추가 → node server.js + 헤드리스 브라우저로 추출.

## 진행 상황 (2026-11-13 중단 시점, 컴파일 오류 0, 씬 저장함)
### 끝난 것 (코드만, 화면 쪽은 아직)
- Defs.cs: BalanceVersion 3. FloorChuru 보스 x1.5 보너스(BossBonus), FloorEnemies/FloorInitial/BossAdds, 보스 체력 청소기 7·목욕통 8.
  CatDef(5품종·기술·만나는 단계, CatDef.Spr), ThemeDef(4테마·타일·규칙 설명, ForFloor), Tile 상수.
  저장 필드 추가: RunData.cat/shield/tiles, SaveData.cats/pick/seenRules. SaveData.YarnCd/HissCd(까망 -1), ChuruMul(턱시도 1.2), HasCat, RunCat.
- Puzzle.cs: 층마다 하트 가득(ClaimFloor), 보스 +1 회복 제거. 테마 타일 생성(GenFloor), 미끄러운 타일(고양이 이동·적 밀기 한 칸 더),
  물웅덩이(적 Soak=1턴 멈춤, 고양이 Splash=웅덩이 사라지고 스킬 대기 1턴 유지), 풀숲(Hidden: 붙지 않은 일반 적은 헤맴),
  일반 적 stun 처리, 치즈 관통 털뭉치, 고등어 하악질 범위 2, 삼색 층마다 첫 피격 무효, 턱시도 츄르 x1.2. 부활은 판마다 1번 그대로.
- Game.cs Load: 예전 저장(밸런스 2)에 cats/tiles 채움(이미 만든 상자 단계의 고양이는 조용히 등록), pick·run.cat 범위 보정, RunLooksValid에 tiles 검사.
- GameOverlays.cs: 보스층 클리어 문구를 '보너스 츄르 +50%'로.
- 구 저장 견본: 프로젝트 루트 Captures_oldsave_v2.json (밸런스 2, 3층 진행 중, 하트 2). JsonUtility로 새 코드에서 읽힘 확인(cats=1, floor 3, hearts 2 유지).
  아직 실제 Game.Load(배포 모드 경로) + 화면에서 이어하기 확인은 안 함. 나중에 Captures/로 옮길 것.

### 진행 중이던 것 (시작 전 상태로 멈춤)
- 그림 추출: Tools/sprite-export/draw.js·index.html·html-prototype.html에 아직 아무것도 안 넣음.
  계획: drawCat에 팔레트 인자(pal) 추가 + CATS 팔레트(mackerel/tuxedo/calico/black), 'cheer' 표정; catHead에 mood('open','sleep','yawn');
  drawTail(kind), drawTile(1~3)+tile_grass_front, drawBg(living/kitchen/bath/yard, 240x427, scale 1.5), win_frame, prop_sun/moon/star/cloud, fx_star/fx_hit/fx_confetti.
  스프라이트 이름: cat_<품종>_<idle|blink|hurt|ko|walk1|walk2|cheer>(치즈는 cat_*, cat_cheer 추가), head_<품종>_<open|sleep|yawn>, tail_<품종>, bg_<테마>, tile_slick/puddle/grass/grass_front.
  (주의: 기존 drawBox는 time 0, seed 0이라 상자 속 고양이가 눈 감은 모습으로 뽑혀 있음.)
  추출: node(C:\Users\mbc\AppData\Local\Programs\nodejs) server.js + 헤드리스 Edge/Chrome로 http://localhost:8772 열기 → NyangPunch > Reimport Sprites.
  bg_*는 SpriteImport에서 압축 설정 고려(웹 빌드 크기).

### 남은 것
- 화면: 퍼즐에 테마 배경·셀 색·타일 그림, 품종별 고양이 그림(CatDef.Spr), 풀숲 숨음 표시, 새 규칙 첫 등장 안내(seenRules), 층 시작 배너에 테마 이름.
- 도감 화면(만난/실루엣, 기술, 첫 만남 연출: NoteTier에서 cats 비트 등록), 출전 고양이 선택(ShowRunIntro·다시 시작 시, S.pick).
- 생동감: 은신처 창(낮·밤, 해/달/별/구름) 줄, 상자 고양이 대기 동작(head 겹치기, 꼬리, Zz), 가끔 나와서 걷기; 퍼즐 히트스톱·흔들림·파티클, 층 클리어 연출, 출전 고양이 등장 연출.
- DevSelfTest: 출전 고양이 순환(run마다 다른 pick, cats 전부 열기), 새 규칙 층 포함 확인. Run(10,10) 10회 통과.
- 구 저장 이어하기 실제 확인(배포 모드 경로), 스크린샷 Captures/(은신처, 도감, 출전 선택, 테마 3종, 새 규칙 장면).
- 배포는 하지 않음(사용자 확인 후).
