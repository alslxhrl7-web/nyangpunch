# 홍보 이미지 도구

itch.io 대표이미지(630×500), 배너(960×240), 게임 칸 배경(450×800)을 게임 그림 코드로 그립니다.

1. `node promo-server.js` (포트 8774)
2. 브라우저에서 http://localhost:8774 → "완료"가 뜨면 `images/`에 PNG 저장
3. 스크린샷 3장(`images/screenshot_*.png`)은 유니티 플레이 모드에서 1080×1920 렌더 텍스처로 찍은 것

그림을 바꾸려면 `promo.html`의 cover/banner/embedBg 함수를 고치세요. 게임 칸 배경은 itch가 원본 크기로 보여 주므로 1배(450×800)로 저장해야 합니다.
- 게임 칸 배경: itch.io가 Run game 버튼을 가운데(세로 약 375~425px)에 그리므로, 그 줄은 비워 둘 것(promo-varco.html의 shift 값).
