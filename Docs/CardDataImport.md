# 카드 시트 연동

## 최초 설정 (Windows Unity 에디터)

1. Google Cloud 프로젝트에서 Google Sheets API를 활성화한다.
2. Google Auth Platform의 동의 화면을 설정한다. 외부 앱 테스트 모드라면 로그인할 계정을 테스트 사용자에 추가한다.
3. OAuth 클라이언트를 **데스크톱 앱** 유형으로 만들고 JSON을 다운로드한다. 원본 JSON은 Unity 프로젝트 밖에 보관한다.
4. Unity 메뉴 `Tools > Duel Hero > 시트 데이터 갱신`을 연다.
5. `데스크톱 OAuth 클라이언트 JSON 가져오기`에서 파일을 선택한다.
6. `Google 로그인`을 누르고 브라우저에서 시트를 읽을 수 있는 계정으로 승인한다.
7. Unity로 돌아와 `시트 데이터 갱신`을 누른다.

[Google 데스크톱 OAuth 공식 문서](https://developers.google.com/identity/protocols/oauth2/native-app). PKCE와 임의 포트 loopback 콜백을 사용한다. 권한은 spreadsheets.readonly이며 특정 파일 하나만으로 제한되는 권한은 아니다.

## 이후 갱신

시트를 수정한 뒤 Unity 창에서 **시트 데이터 갱신**을 누른다. 접근 토큰은 자동 갱신하며 인증 만료·해제 시 다시 로그인한다. Play 모드에서는 갱신할 수 없다.

원본 시트 ID: `1pspfXZtUhTmSc9ZTL1iTzQDeBgvfbb_wqwCNs83EFQE`

`Card Data`, `Card Effect Data`, `Keyword Data`를 [values.batchGet](https://developers.google.com/workspace/sheets/api/reference/rest/v4/spreadsheets.values/batchGet)으로 읽는다. ID 없는 빈 체크박스 행은 제외한다.

`Keyword Data`는 `id / name / description / parameters / textColor / category / stackRule / countUnit` 구조다. 기절·출혈의 상태 정의도 이 탭에서 가져온다. 일반 카드 키워드는 상태 분류와 중첩·단위 열을 비운다. 상태 ID와 키워드 ID는 동일하게 사용한다. 처리 규칙과 적용 시점은 설명 셀의 메모에 기록하며 실행 데이터로 가져오지 않는다. `parameters`는 필요한 값의 의미를 적는 기존 설명용 필드다.

`Card Effect Data`의 `effectType=keywordParameter` 행은 카드별 키워드 수치다. `id / cardId / keywordId / value`를 작성하고 실행 순서·범위·대상 등의 나머지 항목은 비운다. `keywordId`는 `Keyword Data.id`를 참조하며 해당 카드의 `keywordIds`에도 지정되어 있어야 한다. 같은 카드·키워드는 한 행만 허용하고 수치는 0 이상의 정수다. 가져올 때 `CardDefinition.keywordParameters`로 분리하므로 실행 효과 배열·범위·`{effect1...}` 번호에 포함되지 않는다. 실행 방향을 바꾼 카드에도 그대로 보존된다. `KeywordParameter(id, defaultValue)`로 수치를 조회하며, 행이 없으면 기본값(기본 0)을 반환한다. 수치 저장 기능만으로 키워드의 실제 행동이 구현되지는 않는다.

모든 데이터를 다운로드·검증한 뒤 아래 항목을 갱신한다.

- `Assets/Data/CardSheets/Cards.csv`, `Effects.csv`, `Keywords.csv`: 마지막으로 검증된 시트 사본. 수동 가져오기와 복구에도 사용한다.
- `Assets/Data/CardDatabase.asset`: 게임이 사용하는 로컬 데이터.
- 열린 씬의 `DeckListUI`: 덱 미리보기. 변경된 미리보기를 보존하려면 씬을 저장한다.

다운로드·검증 실패와 취소 시 기존 데이터를 유지한다. 저장 오류 시 CSV와 DB를 복원한다. 카드 ID 중복, 참조 누락, 잘못된 수치·좌표·설명 치환 항목을 거부한다. CSV와 DB 에셋을 함께 버전 관리한다.

## 인증 저장

설정과 토큰은 `%LOCALAPPDATA%/DuelHero/SheetSync/<프로젝트 식별값>`에 Windows 사용자 DPAPI로 암호화해 저장한다. Assets와 Git에 저장하지 않는다. 원본 JSON도 프로젝트 밖에 둔다. `저장된 로그인 지우기`는 로컬 토큰만 삭제한다. Google의 앱 권한 해제는 Google 계정에서 진행한다.

인증·갱신 스크립트는 Editor 폴더에 있어 게임 빌드에 포함되지 않는다. 게임은 로컬 카드 DB를 사용하며 로그인이나 네트워크가 필요 없다.

## 수동 CSV 가져오기

세 탭을 CSV로 다운로드하여 `Cards.csv`, `Effects.csv`, `Keywords.csv` 이름으로 같은 폴더에 저장한다. `Tools > Duel Hero > Cards > Import downloaded CSV folder`에서 선택한다. 저장된 CSV 재적용은 `Reimport saved sheet data`를 사용한다.

## 스크립트 역할

- `CardSheetImporter`: 기존 파싱·검증·DB 생성 기능을 재사용한다. 새 동기화 창도 이 임포터를 호출한다.
- `CardDefinition`, `CardDatabase`: 고정 정보. 시작 덱은 isStarterCard 또는 isBasicAction 카드다.
- `CardInstance`: 보유 카드 예약·재사용 상태. 기본 행동은 재사용 제한이 없고 일반 기술은 N 사용 시 N+1 불가, N+2 가능이다.
- `DeckListUI`: 타입별 표시, 상세 설명, 줄바꿈, 스크롤.
- `GoogleSheetSyncClient`, `GoogleSheetSyncWindow`: 추가된 읽기 전용 인증·조회와 갱신 버튼.

자연어 키워드 규칙은 자동 실행하지 않는다. 예외 동작은 별도 구현이 필요하다. 현재 카드 클릭은 상세 설명까지이며 예약·에너지·공격 판정 연결은 다음 단계다. 카드 목록의 초기 상태는 기존 이동 큐를 나타내지 않는다.

## 카드·유닛·전투 설정 분리 갱신

갱신 창의 범위에서 전체 / 카드 / 유닛(플레이어·적) / 전투 설정을 선택한다.
로그인·조회는 GoogleSheetSyncClient를 공유하고, 각 데이터의 검증은 별도 임포터가 담당한다.
전체 갱신은 모든 조회와 검증을 완료한 뒤 저장하며, 저장 실패 시 기존 CSV와 DB 복원을 시도한다.
부분 갱신은 선택한 데이터만 변경한다.

- Player Data / Enemy Data → Assets/Data/UnitSheets/Players.csv, Enemies.csv → UnitDatabase.asset
- Game Config (`group=BattleConfig`) → Assets/Data/BattleConfigSheets/BattleConfig.csv → BattleConfig.asset
- 카드 데이터 경로와 수동 CSV 메뉴는 기존과 동일하다.

유닛 ID는 Player/Enemy 각 목록 안에서 중복을 허용하지 않는다. 체력/에너지는 음이 아닌 정수이며 최대 체력은 양수, 시작값은 최대값 이하여야 한다.
BattleConfig의 energyRecoveryPerTurn은 빈 값이면 hasEnergyRecoveryPerTurn=false로 저장한다. 숫자 0과 미설정을 구분한다.

설정 원본은 `Game Config` 한 탭이며 열은 `group / key / value / description`이다. group에는 `BattleConfig`, `RunConfig`, `BattleRewardConfig`, `ShopConfig`를 사용한다. 다운로드 시 그룹별 CSV로 분리하여 기존 임포터와 설정 에셋을 재사용한다. 전체 갱신에서도 설정 탭은 한 번만 조회하고, 부분 갱신은 선택한 그룹만 저장한다.
유닛 및 전투 설정 에셋은 데이터 저장까지만 연결한다. 씬 캐릭터 초기화나 턴 회복에 자동 적용하지 않는다.

## 방향 좌표 규칙

Effects.csv와 Card Effect Data에서 fixedDirection 열을 제거했다.
이동은 rangeOffsets에 상 (0,1), 하 (0,-1), 좌 (-1,0), 우 (1,0) 중 단위 좌표 하나를 넣고 value에 이동 칸 수를 넣는다.
임포터는 이동의 대각선·영벡터·복수 좌표를 거부한다. 공격은 현재 우측 기준 범위를 그대로 저장한다.
direction_select 키워드 처리는 아직 구현하지 않았으며, 추후 우측 기본 범위를 선택 방향으로 회전한다.
