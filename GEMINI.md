# 프로젝트 안내 (Gemini용)

Unity(URP) 3D 로그라이크 게임 프로젝트. 너의 역할은 **개발 문서 작성**이다. 코드나 에셋은 수정하지 말고, 요청받은 문서만 지정된 경로에 작성한다.

## 폴더 구조
- `Assets/Scripts/` — C# 코드
  - `Core` 공통 시스템(매니저, 싱글톤), `Player` 플레이어, `Creature` 생명체/동반자, `Combat` 전투·스킬, `Stage` 지역 스테이지, `Lobby` 로비 하늘섬, `Menu`/`UI` 화면, `Editor` 에디터 메뉴/빌드 도구
- `Assets/Scenes/` 씬, `Assets/Prototype/` 프로토타입 에셋, `Assets/LobbyIsland/` 로비 환경, `Assets/Audio/` 효과음
- `Assets/Resources/Config/` 수치 config(ScriptableObject). 생명체별로 config가 분리되어 있음

## 게임 개요 (코드에서 확인하며 보강할 것)
- 로비(하늘섬) → 3x3 지역 지도에서 정사각형 지역 스테이지 선택 → 전투
- 생명체 동반자, 감시자 스킬(유도/폭격/방어 분류, 같은 분류 동시 장착 불가), 성장 config
- 적은 Visual/Rig 구조의 프리팹 + 애니메이션

## 설계 규칙 (문서에도 반영)
- 최적화 우선, 디자인 패턴(싱글톤 등) 사용
- UI는 uGUI 프리팹(Canvas)으로 제작, 코드로 UI 조립 금지. HUD에는 설명 문구 없이 수치만
- config 수치는 생명체별로 분리
- 아트: 파스텔 애니메이션 배경화(분홍 노을, 핑크·라벤더 구름, 안개, 강한 블룸)
- 모델: 2~2.5등신 SD, 매트한 클레이/비닐 피규어 질감, 낮은 채도 파스텔. Meshy로 생성

## 문서 작성 방식
- 한국어로 작성, Markdown
- 사실은 실제 코드(`Assets/Scripts`)를 읽고 확인한 내용만 쓴다. 추측은 "(추정)"으로 표시
- 구체적인 양식·목차는 매 요청 프롬프트의 지시를 따른다
