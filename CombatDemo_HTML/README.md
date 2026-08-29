# CombatDemo HTML v0.4

Unity Production 구현과 분리된 Vanilla HTML/CSS/JavaScript 플레이테스트 도구입니다.

## 실행

`index.html`을 브라우저에서 직접 열면 됩니다. 외부 CDN, 빌드, 설치 과정이 없습니다.

## 조작

- `Q`: 공격 / Resolution Popup에서 직접 전투
- `W`: 방어 / Resolution Popup에서 자동 전투
- `E`: 회복 / Resolution Popup에서 협상
- `R`: Resolution Popup에서 도주
- `F1`: 디버그 Drawer

화면 좌측 전투 알림에서 Encounter를 선택하거나 `＋`로 새 Encounter를 만들 수 있습니다. 
장비는 직접 전투 중 잠기며, 새 전투 전 선택할 수 있습니다. 
디버그 Drawer에서는 Timing View, Pattern, Speed, 주요 밸런스 수치와 Force Control을 변경할 수 있습니다.

## 자동 검사

Node.js가 설치되어 있다면 `node tests/core.test.js`를 실행합니다. Soft-RPS 9개 조합, Timing Zone, Pressure, Condition, Universal Equipment, 결정론 난수와 Queue 규칙을 검사합니다.

## 의도적으로 남긴 TBD

협상 비용, 도주 확률, Auto Combat Stat Weight, Queue Capacity는 Baseline의 Production 확정값이 아닙니다. `js/config.js`의 최소 Prototype 기본값으로만 제공됩니다.
