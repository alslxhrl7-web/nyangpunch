# 스프라이트 추출 도구

HTML 프로토타입(`html-prototype.html`)의 캔버스 그리기 코드로 유니티용 PNG를 다시 뽑는 도구입니다.

1. `node server.js` 실행 (포트 8772, 환경변수 PORT로 변경 가능)
2. 브라우저에서 http://localhost:8772 열기 → "완료: N개"가 뜨면 끝
3. PNG와 `sprite_meta.json`이 `Assets/NyangPunch/Resources/Sprites/`에 저장됨
4. 유니티 메뉴 **NyangPunch > Reimport Sprites**

그림을 고치려면 `html-prototype.html`의 그리기 함수를 고친 뒤, 그 부분을 `draw.js`에 반영하고 다시 추출하세요.
`index.html`의 목록에 이름·크기·원점(ox, oy)을 추가하면 새 스프라이트도 뽑힙니다.
