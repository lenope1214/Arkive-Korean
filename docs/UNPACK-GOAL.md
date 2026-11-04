# 언팩(Unpack) 목표 문서

> 이 문서는 loop가 매 반복마다 읽고 갱신하는 **단일 진실 원천**이다.
> 작업 전에 먼저 읽고, 작업 후에 반드시 "진행 로그"와 체크리스트를 갱신한다.
>
> **상태 (2026-11-04):** 1단계(언팩, G1~G5)와 2단계(포크 접목, P2-1~6) 완료. **3단계(지속 개발, §10): 발굴할 항목이 바닥나 loop 를 멈췄다(2026-11-04, F43). 남은 일은 사용자 결정 4건(P3-8·10·16·17).** 재현은 §7, 포크 판정은 §9. 키는 `work/unpack/keys.local.json` (§5-1).

## 1. 목표 (Goal)

한국 로스트아크 클라이언트(`D:\Smilegate\Games\LOSTARK`)의 패키지를 Arkive가 **읽을 수 있게** 만든다.

최종 완료 조건 (모두 만족해야 DONE):

- [x] G1. `EFGame\ReleasePC\*.upk` 중 대표 샘플이 **전 청크 해제**까지 성공한다 (청크 오류 0). — 샘플 3개(10KB/140MB/271MB), 청크 5,129개 실패 0 (F14)
- [x] G2. 해제한 논리 스트림에서 Name/Export/Import 테이블이 정상 파싱된다. — 3개 모두 헤더 개수와 일치 (F15)
- [x] G3. `EFGame\data*.lpk` 의 인덱스가 읽히고, 압축 엔트리가 해제된다. — 8개 LPK 전부 OK (F16)
- [x] G4. `EFGame\data2.lpk` 의 게임 DB(`EFTable_*.db`)가 SQLite 헤더로 복호화된다. — GameMsg 134MB 포함 (F17)
- [x] G5. 위 결과가 `Arkive.UPKDiagnostic` 보고서(JSON)와 이 문서에 재현 가능한 절차로 남는다. — §7 (키 주입 2가지, 1·2단계 명령 전부, 기대 결과) (P2-6)

> **2단계 목표 (2026-11-04 사용자 지시):** 포크 `lenope1214/Lostark-signle` 에 이미 있는 한국판 지식·도구를 **다 가져와서 쓴다.** 목록과 작업은 §9.

## 2. 범위와 원칙

- **원본 게임 폴더는 읽기 전용.** 샘플은 `E:\Arkive-Korean\work\unpack\samples\` 로 복사해서 작업한다 (`work/` 는 .gitignore 대상).
- 대상은 사용자 본인 PC에 설치된 클라이언트와 로컬 분석뿐이다. 추출물/키를 저장소에 커밋하지 않는다.
- 진단 도구는 원본을 수정하지 않는다. 변경은 `src/Arkive.UPKDiagnostic` 및 필요 시 `src/Arkive.Core` 에 최소 diff로.
- 키·복호화 파라미터가 한국판에서 다르면, 기존 Western 프로파일을 덮어쓰지 말고 **새 이름의 프로파일을 추가**한다
  (`ChunkProtectionProviderFactory` 주석 방침).

## 3. 환경

| 항목 | 값 |
|---|---|
| 저장소 | `E:\Arkive-Korean` (브랜치 `main`) |
| 클라이언트 | `D:\Smilegate\Games\LOSTARK` |
| UPK 위치 | `EFGame\ReleasePC\*.upk` (이름 난독화, 약 10KB ~ 400MB) |
| LPK 위치 | `EFGame\{config,data1..4,font,leveldata1,leveldata2}.lpk` |
| 진단 도구 | `dotnet run --no-build --project src/Arkive.UPKDiagnostic -- "<UPK>"` |
| 빌드 | `dotnet build src/Arkive.UPKDiagnostic/Arkive.UPKDiagnostic.csproj` |
| 관련 코드 | `Core/Packages/UpkPackageReader`, `Core/Archives/{LogicalPackageStream, AesEcbPrefixProtectionProvider, ChunkProtectionProviderFactory}`, `Core/Skills/LostArkDataArchive`, `Core/Databases/LostArkDatabaseArchive` |

## 4. 지금까지 확인된 사실 (Findings)

- F1. 한국 UPK 헤더는 서구판 프로파일과 일치: tag `0x9E2A83C1`, 버전 868/16, 압축 플래그 `0x44`. 헤더·청크 테이블 파싱 정상.
- F2. 샘플 `9XUFAXIP8BXBAP1NIEG66EF.upk` (271,578,989 B): Name 1793 / Export 1598 / Import 2 / 압축 청크 1596, **전 청크 `LostArkFlags=0x1`(보호)**.
- F3. 보호 제공자 없이 읽으면 `PROTECTED_CHUNK` (logical offset 0x81). 정상 동작(접근 거부).
- F4. Arkive에는 서구판 프로파일 `CreateWesternGlobal()` (AES-256-ECB, 앞 4096B) 이 있음. **한국판에 그대로 통하는지는 미검증.**
- F5. `data*.lpk` 헤더의 엔트리 수 값은 그럴듯하다(예: data2=803개, data1=35,408개). 서구판 Blowfish 인덱스 키가 한국판에도 맞는지는 미검증.
- F6. **서구판 프로파일(`CreateWesternGlobal`)로는 한국 UPK 청크가 풀리지 않는다.** 샘플 `9XUFAX…upk` 첫 청크(물리 0x7D31): `LZ4 block 1 … could not be decoded. | inner: LZ4 match offset 45123 is invalid.`
- F7. 두 번째 샘플 `FMX8MTU1Q2NB1BHGMEF9MT6.upk` (10,193 B, 청크 1개 9,988→111,424 B, 플래그 0x1)도 동일하게 실패 (`LZ4 match offset 55465 is invalid.`). 파일 크기·청크 수와 무관하게 일관됨.
- F8. 실패 지점이 **복호화 이후 LZ4 단계**라는 점이 중요하다. 청크 헤더(tag·블록 크기 테이블)는 암호화되지 않은 채로 `DecodeCompressedChunk`의 검증을 모두 통과했다
  → 컨테이너/청크 포맷은 서구판과 같고, 보호 대상인 **블록 앞부분의 복호화 결과만 틀리다**. 표본 샘플의 청크 1596개 전부 플래그 1이므로 우회할 비보호 청크도 없다.
- F9. 진단 도구 변경 완료: `--western` 옵션, 첫 청크 전체 해제·크기 검증, 상태 코드 `READ_SUCCESS / DECODE_FAILED / PROTECTED_CHUNK / SHORT_READ / ERROR`. 제공자 사용 시 보고서는 `*.chunk-diagnostic.provider.json`.
- F11. **`data*.lpk` 인덱스도 서구판 키로는 읽히지 않는다 (T5).** 대상 8개 전부(`config, data1~4, font, leveldata1~2`) `INDEX_INVALID: Invalid data archive entry`. 헤더의 엔트리 수는 모두 `count*528+8 <= 파일 크기` 검사를 통과했으므로 컨테이너 레이아웃(4B 개수 + 528B 레코드 표 + 페이로드)은 서구판과 같다고 보이나, 표를 푸는 **Blowfish 인덱스 키가 다르다**(`LostArkDataArchive.Key` 와 불일치).
- F12. 따라서 `data2.lpk` 의 DB 복호화(T6)도 현재 불가능하다: DB 엔트리를 찾는 과정 자체가 같은 인덱스를 거치고, 이후 파일별 AES 키 유도도 한국판에서 같은지 미검증.
- F13. 한국판은 **UPK 청크 보호 키와 LPK 인덱스 키가 모두 서구판과 다르다.** 두 키는 서로 다른 값일 수 있으므로 각각 따로 확보/검증해야 한다.
- F14. **한국판 UPK 보호 키를 포크(`Lostark-signle`)의 `Tools/LpkPipeline/export_upk_texture.py` 에서 확보, 통했다.** 방식은 서구판과 동일(AES-256-ECB, 블록 앞쪽 정수 블록 최대 4096B → LZ4). 즉 F6~F8의 원인은 **키 불일치뿐**이었다. 전 청크 검증: `FMX8…` 1/1, `DKV6…` 3,532/3,532 (601,771,446 B, 3.7s), `9XUF…` 1,596/1,596 (945,197,831 B, 7.1s), 실패 0.
- F15. `AssetCatalogReader` 가 한국 UPK에서 그대로 동작: Name 33/57,761/1,793, Import 6/210/2, Export 6/1,323,343/1,598 — 전부 헤더 개수와 일치. `IsWesternLostArkProfile`(868/16, 0x44) 조건을 한국판도 만족한다.
- F16. **LPK 인덱스 키·DB 기본 키도 포크의 `Tools/LpkPipeline/unpack_lpk.py` `REGIONS["KR"]` 에서 확보, 통했다.** 8개 LPK 전부 인덱스·압축 엔트리 해제 OK: config 2, data1 35,408, data2 803(전부 .db), data3 12,337, data4 60,286, font 27, leveldata1 4,759, leveldata2 1,385. 레이아웃은 서구판과 동일하고 **키만 다르다.**
- F17. `data2.lpk` DB 복호화 OK: `EFTable_GameMsg.db` (134,231,040 B), `EFTable_AchievementChat.db` 둘 다 `SQLite format 3` 헤더. Arkive의 DB 키 유도(`DeriveKey`)는 한국판에서도 동일하게 통했다(단, 이름에 `_` 가 여러 개인 테이블은 `unpack_lpk.py` 의 유도식과 차이 가능 — §9 P2-4에서 전수 검증).
- F18. **키 처리 방식:** 한국판 상수는 Arkive 소스에 넣지 않았다. `work/unpack/keys.local.json`(gitignore)에서만 읽고, Core에는 `LostArkArchiveKeys.Use(...)` 주입 지점만 추가(서구판 기본값은 그대로). 진단 도구는 `--key-file` 로 UPK·LPK 키를 모두 주입한다. 키 값은 출력·보고서·문서에 남기지 않는다.
- F19. 포크는 **로스트아크 DX11 엔진 팀 프로젝트**(Engine/Client/Server/Tools, 체크아웃 약 3.7GB). 한국 설치본(KR) 기준으로 UPK/LPK를 이미 풀어서 맵·이펙트·애니메이션·UI를 복원해 쓰고 있다. 정본 문서: `.md/에셋 추출 파이프라인.md`, `Tools/LpkPipeline/`.
- F20. **P2-1 완료 — 키 프로파일이 Arkive 정식 기능이 됐다.** Core `ArchiveKeyProfile`(키 파일 → UPK 보호 제공자 + LPK 키 활성화, 환경변수 `ARKIVE_KEY_PROFILE`), `ChunkProtectionProviderFactory.CreateDefault()`(활성 프로파일 또는 서구판). `Arkive.Cli` 39곳·`Arkive.App` 1곳·Core 기본값 1곳을 `CreateDefault()` 로 교체했고, CLI 시작과 `App.OnStartup` 에서 환경변수를 적용한다(실패 시 CLI는 오류 출력, App은 세션 로그 후 내장 키로 계속). 진단 도구의 중복 파서는 제거해 `ArchiveKeyProfile` 로 통합.
  검증: 실제 CLI `arkive catalog <한국 UPK>` — 프로파일 없음 → `LZ4 block 1 … could not be decoded`, `ARKIVE_KEY_PROFILE` 지정 → export 6개 정상 출력(보호 방식 칸에는 프로파일 이름만), 없는 경로 → 명확한 오류. 합성 키 스모크 테스트 2개 추가(`PackageArchiveSmokeTests`: 프로파일 적용·해제·LPK 왕복, 잘못된 파일 7종 거부). 서구판 기본 동작은 환경변수가 없으면 그대로.
- F21. **P2-2 완료 — `data2.lpk` DB 803개 전부 복호화·SQLite 열기 성공** (`--lpk --all-db`): 803/803, 실패 0, 테이블 803개(DB마다 1개), 856,958,976 B. Arkive의 `DeriveKey`(마지막 `_` 뒤 이름)가 한국판에서도 전수 통한다 → 포크 `unpack_lpk.py` 의 유도식과의 차이는 실제로는 문제되지 않았고 보정 불필요. 도구는 DB를 하나씩 임시 파일로 풀어 검증 후 지운다(디스크 부담 없음).
- F22. **P2-3 완료 — 한국판 `data2.lpk` 로 `arkive database-build` 성공**: 에셋 참조 148,263개, 엔티티 30,820개, 커스터마이징 2,380개. 에셋 이름 148,176/148,263(99.9%)이 한국어, PC 클래스 이름 36/41이 한국어.
  - 깨진 지점 1 (수정): 한국판 GameMsg는 언어별 표(`GameMsg_English`…)가 없고 **`GameMsg` 단일 표**(컬럼 `KEY, MSG, ANA, CHAT, …`)다. → `Databases/GameMessageTable.Resolve` 추가: `GameMsg_<언어>` 가 있으면 그것, 없고 `GameMsg` 가 있으면 그것. `GameDatabaseMetadataReader`(3곳)와 `ZoneMapSidecar` 존 이름 읽기에 적용. 서구판 동작 변화 없음.
  - 깨진 지점 2 (수정, 서구판도 해당): PC `Name` 은 `Battlemaster`·`DimensionMaster` 같은 PascalCase인데 텍스트 키는 `tip.name.enum_playerclass_battle_master` 처럼 snake_case라 소문자 비교로는 안 맞았다 → 밑줄·대소문자를 무시하고 비교. 남은 5개(`Gunlancer`, `Gunner`, `Gunner_Female`, `Kimaster`, `Mage`)는 키 이름 자체가 달라 영어 키로 남는다(낮은 우선순위).
  - GameMsg 텍스트는 **UTF-8** 확인(바이트 수준). 콘솔에서 깨져 보이는 것은 터미널 인코딩 문제일 뿐이다.
  - 수정 안 함(목록만): `GameLanguageService`(서구판 언어 덮어쓰기 기능 — `GameMsg_English` 전제, 한국판엔 해당 없음), `Tweaks/MarketItemTable`·`CombatPowerTable`(`GameMsg_English` 하드코딩 + 영어 툴팁 텍스트 파싱), `GameFontService.Languages`(영/불/독/서), `TweakCatalog` 안내 문구("needs the English game client"). 모두 게임 텍스트를 영어로 가정하는 서구판 전용 기능이라 언팩 목표 밖.
  - 진단 도구에 `--lpk … --db <이름>` 추가(특정 DB를 `<out>/db` 로 추출해 스키마 확인용).
- F23. **P2-4 완료 — 포크 카탈로그 확정(§9)**: 가져옴 3개(A GFx PlaceObject3 class-name 판정 버그 의심, B 커서 추출, C `.loa` 문자열 덤프), 참고만 8개(대부분 Arkive 에 같은 기능이 이미 있음: MinimapData·무비 덤프·랜드스케이프·사운드 등), 제외 6개(포크 엔진 전용). 포크에 LICENSE 가 없어 형식 지식만 가져오고 코드는 새로 쓴다는 방침을 §9 에 기록.
- F24. **P2-5a 완료 — GFx PlaceObject3 class-name 판정 버그 확인·수정**: 진단 도구 `--gfx <Packages> --key-file F [--max N]` 로 `EFUI_*` 패키지 60개(무비 70개)를 대조. HasImage+HasCharacter 인데 HasClassName 이 없는 배치 399개 중 **리테일식(문자열 없음)만 유효 394, 명세식만 유효 0**, 둘 다 5. → `GfxMovieReader.ReadPlacement` 를 HasClassName(0x08)일 때만 문자열을 읽도록 수정. 이전에는 이런 배치의 character id·인스턴스 이름이 틀려 UI 그림 이름 붙이기(`InterfaceElementNames`)에서 빠졌다. 같은 익스포터를 쓰는 서구판에도 해당되는 수정. 스모크 테스트 `reads PlaceObject3 placements the way the game's Scaleform exporter writes them` 추가. 패키지 이름은 `LostArkPackageName.TryDecodeFileName` 으로 난독화 파일명에서 복원(34,103개 중 EFUI_*).
- F25. **P2-5b 완료 — 마우스 커서 추출** (`Core/Interface/GameCursorReader`, Cli `arkive cursors <Packages|EFUI_CURSOR.upk> [--out DIR] [--json]`): 한국판 `EFUI_CURSOR` 에서 커서 56종, 파일 990개(`.cur` 979 · `.ani` 11)를 `<out>/<preset>/<size>/<이름>.<ext>` 로 추출. **990개 전부 Windows `LoadCursorFromFileW` 로 열림** 확인. `.ico` 로 저장된 것은 export 의 `[size]hotspotx/y` 클릭 지점을 넣어 `.cur` 로 바꾼다(예: `normal` 48px 클릭 지점 9,9). 포크는 구 빌드에서 51종이라 했고 현재는 56종. 포크가 말한 "IntProperty 뒤 8바이트 더" 규칙은 이 패키지에서 필요 없었다(태그 크기대로 넘겨도 끝까지 맞음). 스모크 테스트 `reads the game's mouse cursors as Windows .cur and .ani files`(합성 export, Str/Struct/Bool/Byte-enum/Int 태그 포함).
- F26. **P2-5c 완료 — `.loa` 문자열 덤프** (`Core/GameData/LoaStringReader`, Cli `arkive loa-strings <file.loa | X.lpk --entry TEXT> [--grep TEXT] [--json]`). `int32 길이 + 바이트 + NUL` 스캔, CP949 디코딩. 포크와 달리 **빈 문자열과 CP949 가 아닌 바이트열은 뺀다**: 한국판 data3 에서 빈 문자열 6,427,194개(대부분 int32 1 + 0 바이트인 숫자)와 Latin-1 로만 풀리는 1,748개(예: float 0.3 = `9A 99 99 3E`)가 전부 잡음이었다. 정리 후 data1 35,403파일 1,030,230개 · data3 12,337파일 11,187,080개(대부분 `Action/*.loa` 바이너리의 우연한 일치 — 이 도구는 형식을 모를 때 훑어보는 용도). 실측 결과 **data1·data3 의 `.loa` 에는 한글 문자열(2자 이상)이 하나도 없다** — 한국어 텍스트는 GameMsg(UTF-8)에만 있고 `.loa` 는 키(`que.*`, `tip.name.*`)와 클래스·리소스 이름만 가진다. CP949 디코딩은 포크 설명대로 안전장치로 둔다. 전체 아카이브 `--json` 은 기존 `WriteJson`(문자열 한 번에 생성)이 메모리 부족으로 죽어서 이 명령만 스트림으로 쓴다. 스모크 테스트 `lists the CP949 text in a .loa record file and skips numbers that look like strings`.
- F27. **P3-2 완료 — PC 클래스 이름 41/41 한국어.**
  - 근거: 게임 자체의 `playerclass` 열거형(`data1.lpk` `/../Common/StringData/EFGameMsg_Enums.xml`, `<NODE Type="playerclass" Name="warlord" Index="104" />`). `Index` 가 PC 표의 `PrimaryKey` 이고 `Name` 이 텍스트 키 `tip.name.enum_playerclass_<Name>` 을 정한다. 한국판 PC 41개 모두 이 경로로 키가 존재했다.
  - 남았던 5개: `Gunlancer`→warlord(워로드), `Mage`→magician(마법사), `Kimaster`→force_master(기공사), `Gunner`→hunter(헌터(남)), `Gunner_Female`→hunter_female(헌터(여)).
  - 구현: `Databases/GamePlayerClassEnum` 이 data2.lpk 옆의 data1.lpk 에서 열거형을 읽는다(없거나 못 읽으면 빈 표 → F22 의 글자 비교로 폴백). XML 은 DTD 금지로 읽는다.
  - 진단 도구 `--lpk … --extract <경로 일부>` 추가(일반 엔트리를 `<out>/entries/<lpk>/` 로 추출, 경로 밖으로 나가는 이름은 거부).
  - 스모크: 단일 언어 테스트에 data1.lpk 열거형이 있을 때와 없을 때를 모두 넣었다.
- F28. **P3-3 완료 — `GameMsg_English` 하드코딩 처리.**
  - `Tweaks/MarketItemTable`, `Tweaks/CombatPowerTable`: 붙인(attached) DB 에서 표 이름을 `GameMessageTable.Resolve(connection, language, schema)` 로 찾는다(새 오버로드, 스키마 이름은 영숫자만 허용).
  - 경매장 보석 판정: 영어 `'%Gem'` 에 한국어 `'%보석'` 을 더했다. 한국판 경매 아이템 중 60개가 "N레벨 청명의 보석" 꼴로 맞는다.
  - **실제 한국판 data2 로 실행**(스크래치 .NET 파일 스크립트): 이전에는 `no such table` 로 실패했다.
    - 시세 목록 7,980개(보석 60).
    - 전투력 옵션 rows 4,430 · gemBase 18 · engravings 32 · bracelet 148 · orbs 14 · cores 78.
  - `GameLanguageService`: 한국판처럼 단일 `GameMsg` 표이면 오류 대신 카탈로그에 `SingleLanguage: true` 를 기록한다. 이후 동작:
    - `NeedsSaving()` 은 false — 매번 134MB 를 다시 읽지 않는다.
    - `Languages()` 는 빈 목록.
    - `ApplyAsync` 는 "한 언어뿐이라 바꿀 수 없다"로 거부.
    - 새 `IsSingleLanguage()` 를 쓰는 App 언어 탭은 이 상태를 설명하는 문구를 보여준다. 전에는 "The game's text could not be read: … no English table." 가 방문할 때마다 떴다.
    - `LoadCatalog` 는 단일 언어 카탈로그면 번역 사본 파일이 없어도 유효로 본다.
  - 스모크 2개 추가(단일 언어 시세 목록, 단일 언어 언어 서비스). `DatabaseSmokeTests.WriteDatabaseArchive` 를 다른 테스트가 쓰도록 분리.
  - 참고: data2 에는 같은 이름의 DB 가 하위 폴더에도 있다(`EFTable_Item.db` 가 루트·`DEV/`·`KOR/`). Arkive 빌더는 루트 것을 고른다(기존 테스트 "canonical tables"). 진단 도구 `--db` 는 파일 이름으로 저장해서 **마지막 것이 덮어쓴다** — 스키마를 볼 때만 쓰고 수치 비교에는 쓰지 않는다.
- F29. **P3-4 완료 — 한국판 Cli 전수 실행(발굴) + UModel 지역 스위치 수정.**
  - 실행 결과:

    | 명령 | 결과 |
    |---|---|
    | `scan` 전체 | 34,103/34,103 파싱, 실패 0. 버전 전부 868/16, 압축 플래그 전부 0x44, 패키지 플래그 6종 |
    | `astrogem-odds` | 정상 |
    | `zone-names` | 정상, 한국어 존 이름(예: "프리힐리아 마을 10년 전") |
    | `interface-names` | 정상 |
    | `language` | 정상(단일 언어 인식). 다만 "Current: English" 로 오해를 부르게 찍어서 고쳤다 |
    | `catalog` | 정상 |
    | `static-mesh-recon` · `material-recon` · `model-viewer-scene` | 정상 |
    | `texture-export-native` | NTX018 → P3-6 |
    | 셰이더 캐시 미발견 | → P3-7 |

  - **가장 큰 발견:** Arkive 는 UModel 을 항상 `-game=lostark -na` 로 실행했다. 동봉된 `umodel_lostark_v7.exe` 는 한국판 패키지를 **`-kr` 일 때만** 읽는다. `-na` 로는 `appDecompress: … Flags=0x44` 로 실패하는데, 포크 문서 §2.3 도 `-kr` 을 쓴다.
    - 키 프로파일에 선택 항목 `umodelRegion`(영문 소문자 2~3자, 대소문자 무시; 명령줄에 들어가므로 검증)을 추가했다. 기본값은 `na`.
    - `ArchiveKeyProfile.UmodelRegion` 을 쓰는 곳: `UmodelBridge`(목록·내보내기·애니메이션), `PackageIndexStore` 백엔드 프로파일 문자열(`lostark-<region>-nameresolve-…`, na 는 이전과 같아서 기존 색인 유지), App 로그 문구.
    - 수정 후 한국판 결과: `static-mesh-recon` 의 STM302 "UModel probe failed" 경고가 사라졌고, `model-viewer-scene` 은 UModel 경로로 동작한다(이전에는 "UModel cannot read this object").
    - 로컬 키 파일에 `"umodelRegion": "kr"` 추가(§5-1).
  - Cli `language` 는 단일 언어일 때 "the game's only language … cannot be changed" 로 출력하고, JSON 은 `SingleLanguage: true` 다.
  - 스모크: 키 프로파일 테스트에 지역 적용·해제, 잘못된 지역 2종(명령줄 주입 시도, 숫자) 거부를 추가했다.
- F30. **P3-6 진단 — NTX018 은 한국판 문제가 아니라 스트리밍 텍스처다.** `fx_d_ocnfoam_01`(512², DXT1, `busecrunchcompression=True`, `texturefilecachename=textures`, `firstresourcememmip=3`)의 밉 구성:
  - 밉 0~2: `BulkDataFlags=0x201`(0x1 = 별도 파일, 0x200 = 압축 bulk). 외부 텍스처 캐시에 있다.
  - 밉 3 이상: 패키지 안(inline).
  - `NativeTextureExportService` 는 모든 밉이 inline 이어야 해서 거부한다(NTX018). 서구판에서도 같은 제약이다.
  - 텍스처 캐시 파일: `ReleasePC/LOD9LS7OEDEP1EBMOHOT5O.upk`(398,190,387 B), 이름을 풀면 `textures`. 확장자는 `.upk` 지만 실제로는 원시 데이터 캐시다.
  - 기록된 오프셋(356,891,320 등)에서 밉 0~2 를 읽으면 셋 다 **Crunch 파일(`Hx` 헤더, 헤더의 크기 필드 = StoredSize)** 이다 → 그대로 읽을 수 있다. 구현은 P3-9.
  - `ReleasePC` 루트에서 이름을 푼 파일들:

    | 이름 | 실제 파일 |
    |---|---|
    | `gameframework` · `gfxui` · `core` · `engine` · `efgame` · `akaudio` | `.u` |
    | `coalesced_int` | `.bin` |
    | `globalshadercache-pc-d3d-sm5` | `.bin` |
    | `extshadercache-dx11` | `.bin` |
    | `textures` | `.upk` |

    1단계 샘플 3개(`DKV6…`, `FMX8…`, `9XUF…`)도 Packages 가 아니라 **ReleasePC 루트**에 있다 → §7 경로를 되돌려 고쳤다(P2-6 의 "고침"이 틀렸다).
  - 원본 게임 폴더 `ReleasePC/Packages/074SCMSRX0EJ8X0T4RE0YRMYRX0RXQDSQX.diagnostic.json`(2,978 B, 2026-11-02 19:49): 이번 작업 전, 진단 도구가 UPK 옆에 보고서를 쓰던 시절에 남은 파일이다(그래서 "샘플은 복사해서 쓴다" 규칙이 생겼다). 게임 동작에는 영향이 없다. 원본 폴더라 지우지 않고 **사용자 판단에 맡긴다**.
- F31. **P3-9 완료 — 스트리밍 텍스처를 보기 경로에서 전체 해상도로.**
  - 새 `Textures/TextureFileCache`(읽기 전용):
    - 외부 밉 판정: inline 위치가 없고 `BulkDataFlags & 0x1`.
    - 캐시 이름: `texturefilecachename`.
    - 캐시 파일 찾기: 패키지 폴더의 **상위 폴더 먼저**, 그다음 같은 폴더. 난독화 이름을 풀거나 `<name>.tfc` 로 찾고, 결과를 캐시한다.
    - `ReadMip`: 범위를 검사한 뒤 `BulkDataOffset` 위치의 `StoredSize` 바이트를 읽는다.
  - `NativeTextureExportService.DecodePreviewAsync`·`DecodeChainAsync`(App 텍스처 미리보기, 존 3D 장면)가 외부 밉을 캐시에서 읽는다.
    - 이전 동작: 미리보기는 `PayloadRelativeOffset!.Value` 에서 예외를 내고 장면 쪽이 삼켜서 텍스처가 빠졌다. 체인은 첫 외부 밉에서 멈춰 상위 레벨이 없었다. 서구판에도 같은 문제가 있었다.
    - 보기 경로는 코덱 플래그 없이 정확히 GPU 크기인 **원시 밉**(스트리밍 텍스처의 작은 꼬리 밉: 32² DXT1 = 512 B, flags 0)도 받는다.
    - LZ4 판정은 별도 파일 비트를 무시한다(`0x81` 도 LZ4).
  - **편집용 내보내기는 그대로 inline 만 받는다.** NTX018 메시지는 "texture file cache 에서 스트리밍됨 — 보기는 되지만 편집용 내보내기는 아직 안 됨"으로 바꿨다(P3-10).
  - **실측**: 원본 `ReleasePC/DKV6KRSCXY3T6D9CJIK3G.upk` 의 텍스처 40개가 전부 스트리밍이었고, **40/40 이 전체 크기로 디코딩**됐다(실패 0). `fx_d_ocnfoam_01` 은 512×512 DXT1, DDS 174,904 B 다. 이전에는 상위 3레벨이 빠졌다.
  - 텍스처 하나에 ~8.7초가 걸린다. 이는 텍스처마다 recon 이 140MB 패키지를 다시 읽기 때문이고 이번 변경과는 무관하다.
  - 스모크: 합성 패키지 + 합성 `testcache.tfc` 로 보기 결과가 inline 과 같은지, 캐시가 없으면 `FileNotFoundException`, 편집용 내보내기 거부를 확인한다.
- F32. **P3-7 — 셰이더 캐시는 한국판에서도 동작한다. 경고는 내 시험 방식 탓이었다.**
  - `ModelViewerSceneService.ShaderIndexAsync` 는 **패키지 폴더의 상위 폴더**(보통 Packages → ReleasePC)에서 최상위 `*.upk` 의 `shadercache` export 를 찾는다. P3-4 에서는 `work/unpack/samples` 의 복사본을 열어서 상위가 `work/unpack` 이었고, 그래서 캐시를 못 찾았다.
  - 원본 `Packages/DSN9FV06NDLR69NCXEOUY.upk` 의 `bg_frame_planes` 로 다시 실행했다.
    - 한국판 셰이더 색인이 33초 만에 만들어졌다: shadercache export 1,599개(`ReleasePC/9XUFAXIP8BXBAP1NIEG66EF.upk`), 머티리얼 셰이더 맵 19,271개, 픽셀 셰이더 28,646개(13.6MB 색인).
    - 서구판 기준으로 쓴 스캐너(`ShaderCacheIndex`, "868/16 Western" 레이아웃)가 한국판에서도 맵을 찾는다.
    - 해당 MIC(`mi.bg_unlit_trn_op`)는 맵이 없다고 나왔다. 색인에는 `BG_Unlit_TRN`, `BG_Unlit_TRN_AutoSorting` 이 있다. 머티리얼별 매칭 문제인지 판단하려면 서구판 기준선이 필요하다(보류).
  - 수정: 실패 메시지에 찾아본 폴더를 넣고, Packages 폴더에서 열라는 안내를 붙였다.
- F33. **P3-8 중단 — 시스템 메모리 부족으로 Claude Code 가 백그라운드 `index-build` 를 종료했다**(명령 자체의 실패가 아니다). 그전까지 패키지 약 472개를 색인했다(`work/unpack/index` 에 473개 파일, 12MB). 남은 UModel·Cli 프로세스는 없다. 2차 실행(2026-11-04, 사용자 지시, `--workers 2`)도 같은 이유로 종료됐다. 그때까지 캐시는 20,727개 파일(486MB)로, 전체 34,103개의 약 61%다. 색인 저장소는 이미 만든 패키지를 다시 쓰므로(`TryLoadFreshAsync`), 다시 실행하면 남은 약 13,400개만 처리한다. 3차 실행(2026-11-05)도 같은 이유로 종료됐고, 그때 캐시는 26,073개 파일(약 76%)이었다. 종료 직후 여유 메모리는 약 19GB여서, 색인 도중 특정 패키지에서 메모리가 순간적으로 치솟는 것으로 보인다(미확인).
  - 색인은 패키지마다 캐시되므로 다시 실행하면 이어서 할 것으로 보인다.
  - **loop 는 스스로 다시 시작하지 않는다**. 사용자가 메모리 여유를 확인하고 지시하면 다시 실행한다. 이때 `--workers 2` 처럼 동시 실행 수를 낮춰 메모리를 아낀다.
  - 앞서 다른 백그라운드 감시도 같은 이유로 종료된 적이 있다(§6 이전 기록).
- F34. **P3-5 완료 — GFx 프로브 전수**(`--gfx --max 100000`).
  - 대상: `EFUI_*` 패키지 552개, 무비 674개.
  - 규칙이 갈리는 PlaceObject3 배치 15,343개 중 **리테일식만 유효 9,394 · 명세식만 유효 72 · 둘 다 154**. 나머지(~5,700)는 둘 다 정의된 캐릭터를 가리키지 않는데, 공유 라이브러리에서 가져온(ImportAssets) 캐릭터로 보인다.
  - → F24 의 수정(HasClassName 일 때만 문자열)이 전수로도 맞다. 명세식 72개는 우연히 맞은 것으로 보이지만 별도 확인하지 않았다.
  - HasClassName 배치는 31,473개다.
  - **실패 1개**: `efui_iconatlas_enc_novb`(`Packages/SZWK4EKEQJ0P50I4TSJE4EJQ37.upk`).
    - `--all` 결과 청크 6개 전부 `LZ4 block 1 … could not be decoded`.
    - UModel `-kr` 도 같은 곳에서 실패한다.
    - 34,103개 중 내용을 풀 수 없는 유일한 패키지다(헤더 `scan` 은 통과). → P3-11.
- F35. **P3-11 종료 — `efui_iconatlas_enc_novb` 는 현재 키로 풀 수 없다.**
  - 스크래치 래퍼 제공자로 같은 한국판 UPK 키의 형식 가설 4가지를 시험했다: 접두 4096B(기존), 블록 전체 AES-ECB, 복호화 없음, 접두 2회. **모두 첫 블록(청크 0 의 #0, 110,049 B)에서 LZ4 실패**다.
  - 오류 문구의 "block 1" 은 1부터 센 번호다. 즉 첫 블록부터 실패하고, F34 의 "블록 0 은 풀린다"는 해석은 틀렸다.
  - → 다른 키 또는 다른 보호 방식이다. §8 에 따라 바이너리에서 키를 찾거나 추측하지 않는다. 34,103개 중 1개라 영향은 아이콘 아틀라스 하나뿐이다. 필요해지면 사용자에게 키를 요청한다.
- F36. **P3-12 — 한국판 글꼴은 font.lpk 최상위 폴더다(발굴 1회차).**
  - 한국판 `font.lpk`(27개)에는 서구판 언어 폴더(English/French/German/Spanish/Russian/Japanese/Chinese/Taiwanese)와 함께 **최상위 `FontMap.xml`** 이 있다.
    - 키는 같다(`$YG760`, `$YG330`, `$YoonGasiIIM`, `$ChronologyFont`, `$eventDamageFont` …).
    - 가리키는 파일은 한국 글꼴이다: `HANYoonGothic760.ttf`/`HANYoonGothic330.TTF`(본문), `YoonGasiIIM.ttf`(제목), `BMKkubulimTTF.ttf`(데미지 숫자).
  - 기존 `GameFontService` 는 서구판 4개 언어 폴더만 바꿔서, 한국판에서는 글꼴을 바꿔도 게임에 나타나지 않았다.
  - 수정:
    - `MainFolder`(최상위)를 언어 폴더처럼 함께 바꾼다. 서구판 클라이언트는 이 파일들을 읽지 않으므로 무해하고, 되돌리기는 원본 그대로다. 최상위 FontMap 에 본문·제목 글꼴이 없으면 건너뛴다.
    - "게임 자체 글꼴" 미리보기 기본값은 키 프로파일 지역이 `kr` 이면 최상위, 아니면 English.
    - 오류 문구에 "main (Korean)" 을 넣었다.
  - **실제 한국판**(읽기 전용, 미리보기 파일만 스크래치에 저장): 본문 HANYoonGothic760(8,484,852 B), 제목 YoonGasiIIM(3,572,912 B), 데미지 BMKkubulim(2,893,776 B).
  - 스모크 1개 추가: 한국식 font.lpk 에서 본문(760·330 둘 다)·제목·데미지 숫자(새 파일 + FontMap 항목)를 바꾸고, 프로파일 지역에 따른 기본 폴더와 원본 복원을 확인한다.
  - 참고: Git Bash 는 `/Binaries/...` 처럼 슬래시로 시작하는 인자를 Windows 경로로 바꾼다. 진단 도구 `--extract` 에는 `Fonts` 처럼 앞 슬래시 없이 준다. `--extract` 는 이제 압축되지 않은 일반 엔트리도 꺼낸다(DB 만 제외).
- F37. **P3-13 — 한국판 오디오 키스트림(발굴 2회차).**
  - 증상: `SoundCatalog.Build` 가 한국판 `WwiseAudioPackage` 에서 **뱅크 0 · 클립 0**. 예외는 조용히 건너뛰어져서 겉으로는 소리가 없을 뿐이었다.
  - 원인: 마커(`3E CE A6 74`)는 서구판과 같지만, 뒤의 XOR 키스트림이 다르다. 서구판 키로 풀면 `AKPK` 가 아니다(모든 파일에서 같은 잘못된 바이트 `5BD73D9F…`).
  - 포크 `Tools/SoundPipeline/README.md`: 한국판은 **435,540 B 마다 반복되는 단일 키스트림**이고(서구판은 854 B 키 × 카운터 255 = 217,770 B 주기), 표를 `wwise_keystream.dat` 로 제공한다. 뱅크 버전은 134 로 Arkive 와 같다.
  - 키 규칙대로 처리했다. 포크 파일을 로컬 `work/unpack/wwise_keystream.kr.dat` 로 복사했고(git 무시, 내용 출력 없음), 키 프로파일에 선택 항목 `wwiseKeystreamFile`(상대 경로는 프로파일 위치 기준, 1B~64MB)을 추가했다. `WwiseAudioCipher.Apply` 는 프로파일 키스트림이 있으면 `data[i] ^= ks[(offset+i) % len]`, 없으면 서구판 공식을 쓴다.
  - **실측**: 뱅크 1,607 · 미지원 0 · 클립 207,931(효과음 150,531, 한국어 음성 59,952).
  - 정리: 느슨한 `.wem` 의 언어(폴더 이름 `Korean`)를 패키지 언어 표기(`korean`)에 맞춰, 한 언어가 둘로 보이지 않게 했다.
  - 스모크: 키 프로파일 테스트에 키스트림 적용(오프셋·순환)과 해제 후 서구판 복귀를 넣었고, 키스트림 파일이 없는 경우의 거부도 추가했다.
- F38. **발굴 3회차 — 스킬 카탈로그 확인, 키 불일치 오류 문구 개선(P3-14).**
  - `SkillCatalog.Load`(App 스킬 탭) 를 한국판으로 실행했다: 액션 파일 1,631개, 스킬 22,548개, NPC 액션 38,765개, 16초. 스킬 이름은 한국어다(예: ARCANA 39개 중 37개).
  - 키 없이 한국판 data2 를 열면 `Invalid LPK entry name length; the protection profile may have changed.` 가 났다. 다른 두 문구도 "Western Lost Ark LPK index", "Select … from a Western/global Steam client" 라고 해서 한국판 사용자를 잘못 안내했다.
  - 수정: `ArchiveKeyProfile.WrongKeysHint` 를 추가해 `LostArkDatabaseArchive`(3곳)와 `LostArkDataArchive`(엔트리 오류)에 붙였다.
    - 프로파일이 있으면: "그 프로파일이 이 클라이언트 지역 것인지 확인".
    - 없으면: "서구판 키를 썼다; 다른 지역(한국판 등)은 ARKIVE_KEY_PROFILE 키 프로파일이 필요".
  - 실제 확인: 프로파일 없이 한국판 `database-build` → 새 문구가 나온다.
- F39. **발굴 4회차 — 한국판 설치 구조 확인, Steam 전용 안내 문구 정리(P3-15).**
  - 한국판 설치 위치는 `D:\Smilegate\Games\LOSTARK`(`Binaries`, `EFGame`, `combinedata_manifest`)이고 Steam 이 아니다.
  - 그대로 동작하는 것:
    - 업데이트 감지(`GameBuildStamp`): Steam 기록이 없으면 `Binaries\Win64\LOSTARK.exe`(57,147,744 B)의 크기·시각을 쓴다.
    - `game-crashes`: `EFGame\Logs` 가 없어서 "덤프 없음"으로 정상 응답한다.
  - 고친 것: 게임 원본 복구를 안내하는 메시지 11곳이 "Verify the game files in Steam" 뿐이었다. data2/data4/font/로그인 영상/모드 적용/App 모드 라이브러리 메시지다.
    - 새 `Arkive.Core.GameFileCheck.Verify`/`VerifyLower` 문구: "Verify the game files (Steam: Verify integrity of game files; the Korean client: its launcher's file check)".
    - 이 문구를 고정한 테스트는 없었다.
- F40. **발굴 5회차 — 로그인 화면(영상).**
  - 한국판 `EFGame\Movies` 에서 `login_*` 화면 15개를 찾았다(`login_cinema_ocean` 등, 각 15~21MB). `CanUse` 는 **0개**다.
  - Arkive 의 `LoginVideoCipher` 는 "공유 48B 기준 키(서구판) XOR 파일별 16B(헤더 필드로 유도)" 모델이다. 한국판 영상은 이 기준 키로 맞지 않는다.
  - 포크(`ipk_to_bk2.py`, `bink_key_solver.py`): 파일마다 키가 다르고, 저장된 키 없이 Bink 구조 제약(`KB2`/`BIK` 마법 값, 파일 크기, 프레임 표 증가·정렬, 상위 바이트 0 등)으로 48B 키를 정확히 푼다.
  - → P3-16, 사용자 확인 대기.
- F41. **발굴 6회차 — data2 의 지역 폴더(`TableData/KOR`, `/DEV`).**
  - data2 의 DB 803개 중 800개는 `TableData/` 에, 2개는 `DEV/` 에, 1개는 `KOR/` 에 있다. 이름이 겹치는 것은 `EFTable_GameLog.db`(루트·DEV)와 `EFTable_Item.db`(루트 138MB · KOR 136MB · DEV 188MB) 둘이다.
  - Item 비교:
    - 루트: 137,275행, 334열.
    - KOR: 136,254행, 332열.
    - KOR 에만 있는 키 2,955개, 루트에만 있는 키 3,976개. 같은 키인데 이름이 다른 것은 0개다.
    - 양쪽 고유 아이템 모두 GameMsg 에 한국어 이름이 있다(2,950/2,955, 3,965/3,976). → 어느 쪽이 살아 있는지 데이터로는 갈리지 않는다.
  - `config.lpk`: `Common/Publish/publish.Kor.xml`(`<Region>KOR</Region>`, `<Language>Korean</Language>`, `<WebLocale>ko</WebLocale>`)과 `EFGame/Config/EFSetup.xml`(`<Publish>publish.KOR.xml</Publish>`). 지역 폴더를 덮어쓰기로 쓸 가능성은 있지만 확인된 것은 아니다.
  - Arkive 빌더는 루트를 고른다(기존 동작).
  - 진단 도구 `--db` 는 하위 폴더 사본을 `KOR_…`, `DEV_…` 이름으로 따로 저장하게 고쳤다(F28 의 덮어쓰기 문제 해결).
  - → P3-17.
- F42. **발굴 7회차 — 새 항목 없음(연속 1회째).**
  - Game Data 편집(`GameDataArchive`): 단일 GameMsg 처리는 P3-3 의 `GameLanguageService` 경로를 따른다. 편집 대상 DB 는 루트 `TableData` 다. `KOR/` 사본 문제는 P3-17 과 같다.
  - 외형(outfit) 편집: 한국판 `data4.lpk`(60,286개)에 `XmlData/LookInfo/Item/` 가 43,798개 있다. 무작위 2,000개를 `OutfitLooks.Read` 로 읽은 결과는 레이아웃 있음 1,469 · 없음 531 · 실패 0 이다. 서구판 기준선이 없어서 비율은 판단하지 않는다.
- F43. **발굴 8회차 — 새 항목 없음(연속 2회째) → §10 멈춤 조건으로 loop 정지.**
  - 인터페이스 트윅: 트윅 25개의 훅 65개 전부, 한국판에서 대상 패키지(`EFUI_*` 552개 중)·무비·클래스·메서드 이름을 찾았다(65/65).
  - 이름 존재 여부는 무비 바이트 안의 문자열로 확인했다(ABC 문자열 풀 근사).
  - 남은 미완료 항목은 전부 사용자 판단·확인이 필요하다: P3-8(색인 재실행), P3-10(스트리밍 텍스처 교체 방식), P3-16(로그인 영상 키 풀이 허용 여부), P3-17(인게임 아이템 확인).
- F10. 도구 사용 주의: 표준입력이 열려 있으면 경로 프롬프트 대기로 멈출 수 있다. 자동 실행 시 `</dev/null` 로 막는다.

## 5. 다음 작업 (우선순위 순 — loop는 맨 위 미완료 항목을 수행)

> **방침 (2026-11-04 사용자 결정):** 키는 포크(`lenope1214/Lostark-signle`)에서 확보해 로컬 파일로 주입한다(F18). 1단계(언팩, T1~T7)는 사실상 끝났고,
> 이제 **포크에 이미 있는 한국판 지식·도구를 다 가져와서 Arkive에 접목**한다 ("있는 걸 안 쓸 이유는 없다"). 단, 이미 Arkive에 있는 기능을 중복 도입하지 않는다.

**1단계 (언팩) — 완료**

- [x] **T1.** `--western` 옵션, 첫 보호 청크 시도 → 실패 (F6, F7).
- [x] **T2.** 전 청크 순회 검증 (`--all`) → 3개 샘플 5,129 청크 실패 0 (F14).
- [x] **T3.** 원인 분류 → **키 불일치 확정** (F14, F16). 포맷·알고리즘은 서구판과 동일.
- [x] **T4.** Name/Import/Export 파싱 (`AssetCatalogReader`) → 개수 일치 (F15).
- [x] **T5.** LPK 인덱스·압축 엔트리 프로브 (`--lpk`) → 8개 OK (F16).
- [x] **T6.** DB 복호화 → GameMsg 등 SQLite 헤더 확인 (F17).
- [x] **T7.** 재현 절차 정리 (§7).

**2단계 (포크 접목) — loop는 맨 위 미완료 항목 하나씩 수행**

1. [x] **P2-1.** (완료, F20) 키 프로파일을 Core 공개 API로 정식화: `ArchiveKeyProfile.LoadFromFile(path)` 한 번 호출로 UPK 보호 제공자 + LPK 키(`LostArkArchiveKeys.Use`)를 모두 구성. 환경변수 `ARKIVE_KEY_PROFILE`(파일 경로)을 지원하고, 진단 도구·`Arkive.Cli`·`Arkive.App` 시작 지점에서 적용한다. 서구판 기본 동작은 변경 없음(환경변수 없으면 그대로). 한국판 상수는 소스에 넣지 않는다(F18).
2. [x] **P2-2.** (완료, F21) DB 전수 검증: `data2.lpk` 803개 `.db` 전부 복호화 → SQLite 헤더 + 열어서 `sqlite_master` 조회. 실패가 있으면 `DeriveKey`(마지막 `_` 뒤 이름)와 포크 `unpack_lpk.py` 의 `database_key`(`EFTable_` 접두 제거한 전체 이름) 차이를 원인으로 확인하고 필요 시 보정(F17).
3. [x] **P2-3.** (완료, F22) 한국판 GameMsg로 Arkive의 게임 DB 빌드 경로 실행 (`GameDatabaseMetadataReader`, `GameLanguageService`, Cli의 DB 빌드). 서구판 가정(언어 컬럼·영어 텍스트·"Western" 예외 문구)에서 깨지는 지점을 목록화하고 최소 수정. GameMsg는 UTF-8, 다른 `.loa` 표는 CP949 (포크 `dump_loa_strings.py` 설명).
4. [x] **P2-4.** (완료, F23) 포크 카탈로그 완성(§9): 항목별로 Arkive 기존 기능과 비교해 `가져옴 / 참고만 / 제외` 판정과 근거 기록. 포크 `.md/에셋 추출 파이프라인.md` 와 `Tools/*/README.md` 를 근거 문서로 읽는다. 엔진/클라/서버 코드는 제외.
5. [x] **P2-5.** (완료, F24~F26) §9에서 `가져옴`으로 판정된 항목을 우선순위대로 접목 — **한 번에 하나**, Core에 최소 구현 + 스모크 테스트(`tests/Arkive.SmokeTests`) 1개 이상. 순서: **5a** GFx PlaceObject3 class-name 판정(§9-A, 먼저 한국판 무비로 재현 확인) → **5b** 커서 추출(§9-B) → **5c** `.loa` 문자열 덤프(§9-C). 하위 항목이 모두 끝나야 P2-5 를 [x] 로 한다.
   - [x] 5a (F24)  - [x] 5b (F25)  - [x] 5c (F26)
6. [x] **P2-6.** (완료) G5 마무리: §7 재현 방법을 현재 도구 옵션(`--key-file`, `--all`, `--lpk`)과 `ARKIVE_KEY_PROFILE` 기준으로 갱신.

### 5-1. 키 처리 규칙 (항상 적용)

- 키 파일: `E:\Arkive-Korean\work\unpack\keys.local.json` (`work/` 는 .gitignore 대상 — 커밋되지 않음). 이미 있다. 형식:

```json
{
  "upkChunkKeyAscii": "<32자>",
  "lpkIndexKeyAscii": "<Blowfish 키 문자열>",
  "lpkDbBaseKeyHex": "<32 hex>",
  "umodelRegion": "kr",
  "wwiseKeystreamFile": "wwise_keystream.kr.dat"
}
```

- 키 값은 **절대 출력·로그·보고서·문서·커밋에 남기지 않는다.** 보고서/로그에는 "user-supplied profile" 정도만 쓴다. 포크에서 값을 옮길 때도 파일에서 파일로(정규식), 화면에 찍지 않는다.
- 한국판 키 상수를 Arkive 소스에 넣지 않는다. 필요하면 로컬 파일/환경변수로만 주입한다.
- 포크 저장소는 외부 자료로 취급: `C:\Users\chlsk\AppData\Local\Temp\claude\...\scratchpad\signle\repo` 에 읽기 전용 클론이 있다(없으면 `git clone --depth 1 https://github.com/lenope1214/Lostark-signle.git`). 포크의 스크립트를 실행하지 않고 읽기만 한다. 포크 코드를 가져올 때는 Arkive 스타일로 다시 쓴다(복사 금지, 라이선스/출처는 §9에 기록).

## 6. 진행 로그 (최신이 위)

| 일시 | 반복 | 한 일 | 결과 | 다음 |
|---|---|---|---|---|
| 2026-11-04 | 28 | 발굴 8회차 → 트윅 훅 65개 대상 존재 확인 | 65/65, 새 항목 없음, 연속 2회 → **loop 정지**, 사용자 결정 4건 보고 (F43). 문서만 변경 | 사용자 결정 대기 |
| 2026-11-04 | 27 | 발굴 7회차 → Game Data 편집 경로·data4 외형 파일 확인 | 새 항목 없음, 연속 1회 (F42). 문서만 변경 | 발굴 8회차 — 또 없으면 §10 멈춤 조건으로 loop 정지·보고 |
| 2026-11-04 | 26 | 발굴 6회차 → data2 의 KOR/DEV 사본 비교, config.lpk 지역 설정 확인, 진단 `--db` 사본 이름 분리, P3-17(인게임 확인) | 루트·KOR Item 이 서로 다름, 판정은 사용자 확인 필요 (F41). 빌드 경고/오류 0, push | 발굴 7회차 |
| 2026-11-04 | 25 | 발굴 5회차 → 한국판 로그인 영상 15개 전부 사용 불가 확인, 포크의 키 풀이 방식 조사 → P3-16(사용자 확인 필요) | (F40). 문서만 변경 | 발굴 6회차 |
| 2026-11-04 | 24 | 발굴 4회차 → 한국판 설치 구조·업데이트 감지·crash 명령 확인, P3-15: Steam 전용 복구 안내 11곳을 `GameFileCheck` 문구로 | (F39). 전체 스모크 통과, 빌드 경고/오류 0, push | 발굴 5회차 |
| 2026-11-04 | 23 | 발굴 3회차 → 스킬 카탈로그 한국판 실행 확인, P3-14: 키 불일치 LPK 오류 문구를 키 출처·프로파일 안내로 | 스킬 22,548 정상, 새 문구 확인 (F38). 전체 스모크 통과, 빌드 경고/오류 0, push | 발굴 4회차 |
| 2026-11-04 | 22 | 발굴 2회차 → P3-13: 한국판 사운드 0개 원인(다른 오디오 키스트림) 확인, 포크 키스트림을 로컬 파일 + 프로파일 `wwiseKeystreamFile` 로 주입, 언어 표기 정리, 스모크 보강 | 사운드 뱅크 1,607 · 클립 207,931 (F37). 전체 스모크 통과, 빌드 경고/오류 0, push | 발굴 3회차 |
| 2026-11-04 | 21 | 발굴 1회차 → P3-12: 한국판 font.lpk 최상위 FontMap 발견, `GameFontService` 가 최상위 폴더도 바꾸고 한국판에서는 기본으로 보여줌, 스모크 1개, 진단 `--extract` 가 비압축 엔트리 지원 | 실제 한국판 자체 글꼴 3종 정상 (F36). 전체 스모크 통과, 빌드 경고/오류 0, push | 발굴 2회차(사운드·한국어 음성 등) |
| 2026-11-04 | 20 | P3-11: enc 패키지에 같은 키로 형식 가설 4가지(접두/전체/없음/2회) 시험 | 전부 첫 블록 실패 → 다른 키 필요, 종료 (F35). 문서만 변경 | 발굴 1회차 (P3-8·P3-10 은 사용자 대기) |
| 2026-11-04 | 19 | P3-5: GFx 프로브를 EFUI 전체(552 패키지·674 무비)로 실행, 유일한 해제 실패 패키지 발견 → 백로그 P3-11 | 리테일식 9,394 / 명세식 72 (F34). 문서만 변경 | P3-11 (enc 패키지, 낮음) 또는 발굴 |
| 2026-11-04 | 18 | P3-7: 셰이더 캐시 탐색 경로 확인, 원본 Packages 패키지로 재실행 → 한국판 셰이더 색인 생성, 실패 메시지 개선 | 셰이더 맵 19,271 · 픽셀 셰이더 28,646 (F32). 전체 스모크 통과, 빌드 경고/오류 0, push | P3-8 (index-build 전체) |
| 2026-11-04 | 17 | P3-9: `TextureFileCache` + 보기 경로 외부 밉·원시 밉 지원, NTX018 문구, 스모크 1개, 백로그 P3-10(편집용, 보류) | 한국판 스트리밍 텍스처 40/40 전체 크기 디코딩 (F31). 전체 스모크 통과, 빌드 경고/오류 0, push | P3-7 (셰이더 캐시) |
| 2026-11-04 | 16 | P3-6: NTX018 원인 진단 — 스트리밍 텍스처(외부 캐시 밉), 캐시 파일 `textures` 위치·형식 확인, ReleasePC 루트 파일 이름 해독, §7 샘플 경로 정정, 백로그 P3-9 추가 | 외부 밉 3개 모두 정상 Crunch 로 읽힘 (F30). 문서만 변경 | P3-9 (스트리밍 텍스처 내보내기) |
| 2026-11-04 | 15 | P3-4: 한국판으로 Cli 읽기 명령 전수 실행, `scan` 34,103개 전체, UModel `-na`/`-kr` 비교 → 키 프로파일 `umodelRegion` 도입, Cli `language` 문구, 스모크 보강, 백로그 P3-6~8 추가 | UModel 이 한국판을 읽게 됨, 전 패키지 헤더 실패 0 (F29). 전체 스모크 통과, 빌드 경고/오류 0, push | P3-6 (텍스처 NTX018) |
| 2026-11-04 | 14 | P3-3: 시세·전투력 표를 `GameMessageTable.Resolve` 로, 한국어 보석 판정, 언어 서비스 단일 언어 상태 + App 문구, 스모크 2개 | 실제 한국판으로 시세 7,980개·전투력 옵션 전부 채워짐 (F28). 전체 스모크 통과, 빌드 경고/오류 0, push | P3-4 (Cli 전수 실행 발굴) |
| 2026-11-04 | 13 | P3-2: `playerclass` 열거형으로 클래스 이름 연결(`GamePlayerClassEnum`), 진단 도구 `--extract`, 스모크 보강 | 한국판 PC 41/41 한국어 (F27). 전체 스모크 통과, 빌드 경고/오류 0, push | P3-3 (`GameMsg_English` 하드코딩) |
| 2026-11-04 | 12 | P3-1: 원격 문서 커밋 위로 rebase 후 push(`14a2fe6`), 3단계 백로그·loop 규칙(§10) 작성 | push 성공, 빌드 경고/오류 0 | P3-2 |
| 2026-11-04 | 11 | P2-6: §7 재현 방법을 현재 옵션(`--key-file`·`--all`·`--lpk`·`--all-db`·`--db`·`--gfx`)과 `ARKIVE_KEY_PROFILE`, Cli `catalog`·`database-build`·`cursors`·`loa-strings` 기준으로 다시 씀. UPK 경로(`ReleasePC/Packages`)·DB 샘플 경로(`lpk/db`) 오기 수정 | 진단 기본 모드·Cli `catalog` 를 문서 명령 그대로 재실행해 확인. **G1~G5, P2-1~6 전부 완료 → loop 종료** | — |
| 2026-11-04 | 10 | P2-5c: `LoaStringReader` + Cli `loa-strings`(JSON 스트림) + 스모크 테스트 | data1·data3 전 `.loa` 덤프, 잡음 규칙 보정, `.loa` 에 한글 없음 확인 (F26). 전체 스모크 통과, 빌드 경고/오류 0. **P2-5 완료** | P2-6 (§7 재현 방법 갱신) |
| 2026-11-04 | 9 | P2-5b: `GameCursorReader` + Cli `cursors` + 스모크 테스트 | 한국판 커서 56종 990개 추출, Windows 로 전부 로드 (F25). 전체 스모크 통과, 빌드 경고/오류 0 | P2-5c (`.loa` 문자열 덤프) |
| 2026-11-04 | 8 | P2-5a: 진단 도구 `--gfx` 추가, 한국판 UI 무비 70개로 PlaceObject3 규칙 대조 → `GfxMovieReader` 수정, 스모크 테스트 1개 | 리테일식 394 / 명세식 0 (F24). 전체 스모크 통과, 빌드 경고/오류 0 | P2-5b (커서 추출) |
| 2026-11-04 | 7 | P2-4: 포크 스크립트·README 와 Arkive Core 대조, §9 판정 확정 | 가져옴 3 / 참고만 8 / 제외 6 (F23). 문서만 변경 | P2-5a (GFx PlaceObject3 재현 확인) |
| 2026-11-04 | 6 | P2-3: 한국판 `database-build` 실행 → `GameMsg` 단일 표 대응(`GameMessageTable`), PC 클래스 이름 매칭 보정, 스모크 테스트 1개 추가, 진단 도구 `--db` | 빌드 성공(참조 148,263·엔티티 30,820), 이름 99.9% 한국어 (F22). 전체 스모크 통과, 빌드 경고/오류 0 | P2-4 (포크 카탈로그 확정) |
| 2026-11-04 | 5 | P2-2: `--all-db` 추가, `data2.lpk` DB 803개 전수 복호화+SQLite 열기 | 803/803 성공, 실패 0 (F21). 빌드 경고/오류 0 | P2-3 (한국판 GameMsg 로 게임 DB 빌드 경로) |
| 2026-11-04 | 4 | P2-1: `ArchiveKeyProfile`·`CreateDefault()` 추가, Cli/App/Core 호출부 41곳 교체, 시작 지점 적용, 진단 도구 파서 통합, 스모크 테스트 2개 추가 | 실제 CLI `catalog` 로 프로파일 유/무/잘못된 경로 검증, 전체 스모크 스위트 통과, 빌드 경고/오류 0 (F20) | P2-2 (DB 803개 전수 검증) |
| 2026-11-04 | 3 | 포크 `Lostark-signle` 분석: 한국판 UPK/LPK 키 확보(→ 로컬 키 파일), `LostArkArchiveKeys` 주입 지점·`AllChunksVerifier`·`--all`·`--key-file`(UPK+LPK)·DB 프로브 추가. T2~T6 완료 | UPK 3개 5,129 청크 실패 0, 카탈로그 개수 일치, LPK 8개 OK, GameMsg 134MB 복호화 OK (F14~F19). 빌드 경고/오류 0 | 2단계 P2-1 |
| 2026-11-04 | 2 | 사용자 방침 반영(키는 사용자가 찾음). T5: `LpkProbe`/`--lpk` 추가, 8개 LPK 프로브. T7: §7 갱신 | 8개 전부 `INDEX_INVALID` — LPK 인덱스 키도 서구판과 다름 (F11~F13). 빌드 경고/오류 0 | **키 필요 없는 작업 소진 → loop 종료(§8).** 사용자가 UPK 보호 키·LPK 인덱스 키를 전달하면 T2/T4/T6 재개 |
| 2026-11-04 | 1 | T1 수행: `--western` 옵션·전체 청크 해제 검증 추가, 샘플 2개(271MB 다청크 / 10KB 단일청크) 실행 | 둘 다 `DECODE_FAILED` — 서구판 키로 한국 UPK 불가 (F6~F8). 빌드 경고/오류 0 | **loop 중단(§8: 한국판 키/프로파일 확보는 사용자 판단 필요)** |
| 2026-11-04 | 0 | 문서 생성, 헤더/청크 진단을 `Program.cs` 에 연결, 샘플 UPK 1개로 첫 진단 실행 | F1~F5 확인, 빌드 경고/오류 0 | T1 |

## 7. 재현 방법

**준비.** 키 파일 `work/unpack/keys.local.json`(형식 §5-1, git 에 올리지 않음)이 있어야 한국판이 풀린다. 없으면 서구판 키만 쓰이고 한국 UPK 는 `LZ4 block … could not be decoded`, LPK 는 `INDEX_INVALID` 로 끝난다(F6, F11).
키를 넣는 방법은 두 가지다:

- **진단 도구:** `--key-file <json>` 옵션.
- **Arkive 본체(Cli·App):** 환경변수 `ARKIVE_KEY_PROFILE=<json 경로>` (F20). 지정하면 Cli 는 stderr 에 `Key profile: User-supplied profile (…)` 를 찍고, 없으면 서구판 그대로.

자동 실행에서는 항상 `</dev/null` 로 표준입력을 막는다(F10). 원본 게임 폴더는 읽기만 하고 모든 출력은 `work/unpack/` 아래로 간다.

```bash
cd /e/Arkive-Korean
dotnet build Arkive.slnx -c Release                       # 경고/오류 0 이어야 함
K=work/unpack/keys.local.json
D=/d/Smilegate/Games/LOSTARK/EFGame                       # 원본 (읽기 전용)
R="D:/Smilegate/Games/LOSTARK/EFGame/ReleasePC"           # 엔진 .u, 셰이더 캐시 .bin, 텍스처 캐시 등 루트 UPK 몇 개 (F30)
P="$R/Packages"                                           # UPK 34,103개, 파일명은 난독화
DIAG="dotnet run -c Release --no-build --project src/Arkive.UPKDiagnostic --"
CLI="dotnet run -c Release --no-build --project src/Arkive.Cli --"

# ── 1단계: 언팩 검증 (G1~G4) ─────────────────────────────────────────────
# UPK 샘플은 복사해서 쓴다 (보고서가 UPK 옆에 저장됨)
mkdir -p work/unpack/samples && cp "$R/<NAME>.upk" work/unpack/samples/   # 1단계 샘플 3개는 ReleasePC 루트에 있다 ($P 의 패키지도 같은 방식)
$DIAG --key-file $K work/unpack/samples/<NAME>.upk </dev/null          # 헤더·청크 표 + 첫 청크 → *.chunk-diagnostic.*.json
$DIAG --western      work/unpack/samples/<NAME>.upk </dev/null          # 서구판 키로 시험 (한국판은 실패가 정상)
$DIAG --all --key-file $K work/unpack/samples/*.upk </dev/null         # G1·G2: 전 청크 해제 + Name/Import/Export 개수 대조 → work/unpack/verify/*.all-chunks.json

# G3·G4: LPK 인덱스 + 압축 샘플 해제 + DB 복호화 → work/unpack/lpk/*.probe.json, DB 샘플 work/unpack/lpk/db/
$DIAG --lpk --key-file $K $D/*.lpk </dev/null
$DIAG --lpk --key-file $K --all-db $D/data2.lpk </dev/null              # DB 803개 전수 복호화 + SQLite 열기 (F21)
$DIAG --lpk --key-file $K --db EFTable_PC.db $D/data2.lpk </dev/null    # 특정 DB 를 lpk/db/ 로 추가 추출 (여러 번 가능)

# Arkive 본체로 같은 확인: 카탈로그 (프로파일 없으면 LZ4 오류, 있으면 export 목록)
ARKIVE_KEY_PROFILE="$PWD/$K" $CLI catalog work/unpack/samples/<NAME>.upk </dev/null

# ── 2단계: 포크 접목 결과 ────────────────────────────────────────────────
export ARKIVE_KEY_PROFILE="E:\Arkive-Korean\work\unpack\keys.local.json"
$CLI database-build $D/data2.lpk --out work/unpack/gamedb/arkive-asset-metadata.json </dev/null   # 한국판 GameMsg 단일 표 (F22)
$DIAG --gfx "$P" --key-file $K --max 60 </dev/null                      # EFUI_* 무비의 PlaceObject3 규칙 대조 → work/unpack/gfx/ (F24)
$CLI cursors "$P" --out work/unpack/cursors </dev/null                   # 커서 56종 990개 .cur/.ani (F25)
$CLI loa-strings $D/data1.lpk --entry NPCFunction/10008 </dev/null      # .loa 문자열 (F26); 파일 하나도 가능, --grep, --json(스트림)

# 회귀 확인
dotnet run -c Release --no-build --project tests/Arkive.SmokeTests </dev/null    # "All Arkive smoke tests passed."
```

**상태 코드**
- UPK: `READ_SUCCESS / DECODE_FAILED / PROTECTED_CHUNK / SHORT_READ / ERROR`
- LPK: `OK / INDEX_INVALID / INDEX_OK_DECODE_FAILED / ERROR`

**기대 결과 (2026-11-04, 한국판)**
- UPK: 샘플 3개, 청크 5,129개 실패 0, 카탈로그 개수 일치 (F14, F15).
- LPK: 8개 전부 OK (F16). DB: 803/803 (F21).
- `database-build`: 참조 148,263개 · 엔티티 30,820개 (F22).
- GFx: 리테일식 394 / 명세식 0 (F24).
- 커서: 990/990 Windows 로드 (F25).
- 패치 후에는 개수가 바뀔 수 있다. 실패가 나오면 먼저 키(새 빌드에서 바뀌었는지)를 의심한다.

## 8. 중단/에스컬레이션 규칙

loop는 다음 중 하나에 해당하면 **멈추고 사용자에게 보고**한다 (wakeup 재예약 금지):

- G5 및 P2-1 ~ P2-6 이 전부 완료되었다 (§9 에서 `가져옴` 판정 항목 중 남은 것은 사용자에게 목록으로 보고).
- 같은 태스크가 3회 연속 실패하고 새로운 가설이 없다.
- 클라이언트 바이너리에서 키를 추출/추측해야 하는 상황. (키는 포크에서 확보한 것을 §5-1 방식으로만 쓴다. 다른 지역·버전의 새 키가 필요하면 사용자에게 요청.)
- 접목하려는 포크 항목의 출처/라이선스가 불분명하거나, 포크의 사유(팀) 자료를 그대로 복사해야 하는 상황 (사용자 판단 필요).
- 원본 게임 파일을 수정해야만 진행되는 상황.
- 빌드가 복구 불가능하게 깨졌다.

## 9. 포크 카탈로그 (`lenope1214/Lostark-signle`) — P2-4 에서 완성

포크는 로스트아크 DX11 엔진 팀 프로젝트(F19)로 한국 설치본(KR)을 기준으로 UPK/LPK를 풀어 쓴다. **P2-4 에서 확정(2026-11-04)** — 근거: 포크 `Tools/LpkPipeline/*.py` 머리 설명·함수 목록, `Tools/*/README.md`, `.md/에셋 추출 파이프라인.md` 목차, Arkive `src/Arkive.Core` 대조.

**출처/라이선스:** 포크에는 LICENSE·README 가 없다. 사용자가 직접 fork 한 저장소이고 "필요한 건 다 가져와"라고 지시했으므로(2026-11-04) 진행하되, 가져오는 것은 **파일 형식에 대한 사실(레이아웃·플래그 의미)** 뿐이고 코드는 Arkive 스타일로 새로 쓴다. 포크 코드·팀 문서·리소스를 복사해야 하는 상황이 오면 §8 에 따라 멈춘다.

| # | 항목 | 포크 위치 | Arkive 현황 | 판정 | 근거 / P2-5 할 일 |
|---|---|---|---|---|---|
| — | 한국판 UPK 보호 키 | `LpkPipeline/export_upk_texture.py` | `AesEcbPrefixProtectionProvider` | **가져옴(완료)** | 로컬 키 파일로만 주입 (F14, F18, F20) |
| — | 한국판 LPK 인덱스 키·DB 기본 키 | `LpkPipeline/unpack_lpk.py` | `LostArkDataArchive`/`LostArkDatabaseArchive` | **가져옴(완료)** | `LostArkArchiveKeys` (F16, F20, F21) |
| A | **GFx PlaceObject3 class-name 판정** | `gfx_scene_extract.py` 머리 설명 | `GfxMovieReader.ReadPlacement` 는 SWF 명세대로 `HasClassName(0x08) \|\| (HasImage(0x10) && HasCharacter)` 이면 문자열을 읽는다 | **가져옴 (1순위)** | 포크 실측: 리테일 Scaleform 익스포터는 HasImage+HasCharacter 여도 class-name 을 **쓰지 않는다**(0x08 일 때만) → 명세대로 읽으면 character id·행렬이 쓰레기가 된다. 먼저 한국판 UI 무비 하나로 재현을 확인하고, 맞으면 조건을 고친다(기존 기능의 버그 수정, 서구판에도 해당 가능). |
| B | **마우스 커서 추출** | `extract_cursors.py` | 없음 | **가져옴 (2순위)** | `EFUI_CURSOR` 의 `efcursordata` export 속성 `cursordata[_<size>][preset<N>]` 에 완성된 `.ani`(RIFF ACON)/`.cur`/`.ico` 바이트가 그대로 있다. `.ico` 는 `<size>hotspotx/y` 로 `.cur` 로 바꿔 쓴다. Core 에 읽기 전용 추출기 + 스모크 테스트(합성 export). |
| C | **`.loa` 문자열 덤프** | `dump_loa_strings.py` | `.loa` 는 `SkillActionReader`(Action)·`ZoneMapSidecar`(MinimapData) 전용 파서만 있음 | **가져옴 (3순위)** | 형식: float 1.0 + int32 몇 개 + `int32 길이 + NUL 종료 바이트` 레코드. 문자열은 **CP949**(GameMsg 만 UTF-8, F22). 진단 도구 또는 Cli 서브커맨드로 범용 문자열 목록화. |
| — | MinimapData.loa 파서 | `parse_minimap_data.py` | `ZoneMapSidecar` 가 이미 읽음 | 참고만 | 중복. 포크가 밝힌 꼬리 필드(층 이름 `tip.name.floor_*`, 타일 축 방향)는 필요할 때 참고. |
| — | UPK 안 무비(EFSwfMovie) 덤프 | `dump_upk_movie.py` | `GfxMovieFile`·`InterfaceMovieCandidateService` | 참고만 | 중복. "export 데이터 앞의 짧은 헤더 뒤 첫 `GFX/FWS/CFX/CWS` 시그니처부터 무비" 규칙만 대조. |
| — | GFx 트리·심볼 덤프·커버리지 감사 | `gfx_native_parse/tree.py`, `gfx_dump_symbol.py`, `audit_gfx_extraction_coverage.py` | `GfxMovieReader` | 참고만 | HUD 재구성용 도구. A 외에 Arkive 에 필요한 형식 지식은 없음. |
| — | 컷신 Matinee 전 배열 파싱 | `extract_scene_matinee.py` | 컷신 기능 없음 | 참고만 | 560줄, 엔진 런타임 문서용 좌표 변환 포함. Arkive 모드 제작에서 쓸 곳이 생기면 재검토. |
| — | 랜드스케이프·레벨 배치 추출 | `LandscapeExtractor/`, `LevelPlacementExtractor/` | `ZoneMapScene`(지형·배치 장면) | 참고만 | 기능 중복, 포크는 자기 엔진용 JSON 산출. |
| — | Wwise 사운드 | `SoundPipeline/` | `Audio/SoundCatalog`, `WemAudio` 등 | 참고만 | 중복. |
| — | UModel 한국판 사용 절차 | `.md/에셋 추출 파이프라인.md` §2.3~2.4 | `tools/external/umodel_lostark_v7.exe` | 참고만 | `-game=lostark -kr -nameresolve` 로 논리명↔난독화 UPK. |
| — | 게임 DB 연결 체인 (NPC→ZoneBase→LevelFileName) | 같은 문서 §2.2 | `GameDatabasePages` 등 | 참고만 | 포크 문서는 DB 792개(구 빌드), 현재 803개(F21) — 패치마다 수가 바뀐다. |
| — | LPK 풀기 / UPK 복호화+LZ4 코드 | `unpack_lpk.py`, `export_upk_texture.py` | Core 동일 구현 | 제외 | 레이아웃 동일, 키만 다름 |
| — | 폰트 스프라이트 굽기 | `FontPipeline/` | `GameFontService`(폰트 교체) | 제외 | 포크 엔진 전용 SpriteFont |
| — | HUD/UI 재구성 스크립트 (`build_*_ui.py` 등 ~40개) | `LpkPipeline/build_*.py` | 무관 | 제외 | 포크 엔진의 화면 재현용 |
| — | 모델 변환·쿠킹, 맵 퍼블리시 | `ModelAssetConverter/`, `ActorXAssetCooker/`, `MapPipeline/` | 무관 | 제외 | `.wmodel` 등 포크 엔진 형식 |
| — | 엔진/클라이언트/서버 C++ 코드, 팀 문서, 3.7GB 리소스 | `Engine/`, `Client/`, `Server/`, `Release/`, `.md/TEAM` | 무관 | **제외** | |

판정 기준: (1) Arkive 에 같은 기능이 이미 있으면 중복 도입하지 않고 한국판에서 동작만 검증, (2) 없으면서 모드 제작에 쓸모가 있으면 `가져옴`, (3) 문서·절차는 `참고만`, (4) 게임 엔진/팀 고유 자산은 `제외`.

## 10. 3단계 — 지속 개발 (2026-11-04 사용자 지시: "멈추라고 할 때까지 새 개발 거리를 찾아 진행")

**목표:** Arkive 를 한국판 클라이언트에서 실제로 쓸 수 있게 만든다. 서구판 동작은 깨지지 않게 유지한다.

**loop 규칙**
- 매 반복마다 아래 백로그의 맨 위 미완료 항목 **하나**를 수행한다.
- 백로그가 비면 **발굴** 작업을 한다. 근거(명령 출력·코드 위치)가 있는 새 항목을 1~3개 백로그에 추가하고, 그중 첫 항목을 진행한다.
- 발굴할 곳:
  - 한국판으로 Cli 의 읽기 전용 명령을 하나씩 실행해 보고 깨지는 곳
  - 코드의 `GameMsg_English`·"Western"·영어 텍스트 가정
  - 스모크 테스트가 없는 Core 경로
  - §9 의 `참고만` 항목 재검토
- 완료 조건은 1·2단계와 같다.
  - 빌드 경고/오류 0.
  - 전체 스모크 통과(새 동작에는 스모크 테스트를 추가).
  - 키 누출 검사 CLEAN.
  - 커밋 후 **push**(사용자 승인, 2026-11-04). push 전에 `origin/main` 을 fetch 하고, 앞서 있으면 rebase 한다. force push 는 하지 않는다.
- 멈춤:
  - 사용자가 멈추라고 할 때.
  - §8 의 중단 규칙에 해당할 때.
  - 발굴을 2회 연속 해도 근거 있는 새 항목이 없을 때. 이때는 사용자에게 보고한다.

**백로그 (맨 위부터)**

1. [x] **P3-1.** 로컬 커밋 11개 push. 원격의 문서 커밋 1개(`fc12c80`) 위로 rebase 한 뒤 push 했다(`14a2fe6`).
2. [x] **P3-2.** (완료, F27) 영어로 남은 PC 클래스 이름 5개(`Gunlancer`, `Gunner`, `Gunner_Female`, `Kimaster`, `Mage`)를 한국어 이름에 연결한다(F22). DB 의 다른 컬럼이나 키에서 근거를 찾고, 추측으로 매핑하지 않는다.
3. [x] **P3-3.** (완료, F28) 단일 GameMsg 표 클라이언트(한국판)에서 `GameMsg_English` 를 하드코딩한 곳을 처리한다.
   - `Tweaks/MarketItemTable`, `Tweaks/CombatPowerTable`: 표 이름을 `GameMessageTable.Resolve` 로 찾는다. 영어 텍스트 파싱에 의존하는 부분은 동작 여부를 판정하고 안내 문구를 둔다.
   - `GameLanguageService`: 한국판에서는 "영어 표가 없다" 오류 대신 기능을 쓸 수 없다는 명확한 상태를 돌려준다.
4. [x] **P3-4.** (완료, F29) 발굴: 한국판으로 Cli 의 읽기 전용 명령(`inspect`, `catalog`, `interface-names`, `zone-names`, 스킬·사운드·폰트 관련 등)을 전부 한 번씩 실행해 깨지는 곳의 목록을 만든다. 결과는 이 백로그에 항목으로 추가한다.
5. [x] **P3-6.** (완료 — 원인 진단, F30. 구현은 P3-9) 한국판 텍스처 `texture-export-native` 가 `NTX018: Mip 0 is not a bounded, hash-verified inline payload.` 로 실패한다(`DKV6KRSCXY3T6D9CJIK3G.upk` `fx_d_ocnfoam_01`). 밉이 외부 텍스처 캐시(TFC)에 있는 정상 케이스인지, 한국판 형식 차이인지 가른다. 같은 텍스처를 UModel(`-kr`)로 내보내 비교하고, 다른 패키지(UI 등)의 텍스처로도 시험한다.
6. [x] **P3-9.** (완료 — 보기 경로, F31. 편집용 내보내기는 P3-10) 스트리밍 텍스처 내보내기: 큰 밉이 외부 텍스처 캐시에 있는 텍스처(`BulkDataFlags & 0x1`)를 `texture-export-native` 가 내보낼 수 있게 한다.
   - 캐시 파일 이름은 텍스처의 `texturefilecachename`(예: `textures`)이고, Packages 의 상위 `ReleasePC` 에서 난독화 이름을 풀어 찾는다(`LOD9LS7OEDEP1EBMOHOT5O.upk` → textures).
   - 밉 바이트는 `BulkDataOffset` 위치의 원시 바이트(`StoredSize` 만큼)이다(F30).
   - 외부 밉도 내부 밉과 같은 해시 검증(SHA256 기록)을 거친다.
   - 범위는 **내보내기만**이다. 교체(쓰기)는 캐시 파일을 건드려야 하므로 별도 판단 — 원본 게임 폴더 읽기 전용 원칙.
   - 스모크: 합성 패키지 + 합성 캐시 파일.
7. [ ] **P3-10.** (보류 — 설계 판단 필요) 스트리밍 텍스처의 **편집용** 내보내기·교체.
   - 교체 파이프라인(`TextureExportAssemblyService` 등)은 inline 밉(플래그 0x200 Crunch / 0x80 LZ4)만 다시 쓴다.
   - 스트리밍 텍스처를 교체하려면 둘 중 하나가 필요하다: 게임의 텍스처 캐시 파일을 고치기(원본 쓰기 — 지금 원칙과 충돌), 또는 교체 패키지 안에 모든 밉을 inline 으로 넣기(엔진이 받아들이는지 실측 필요).
   - 사용자 판단 전에는 진행하지 않는다. 발굴 단계에서 다른 항목이 없으면 사용자에게 묻는다.
8. [x] **P3-7.** (완료, F32 — 한국판 문제 아님, 메시지만 개선) `model-viewer-scene` 이 "No game shader cache was found next to the Packages folder" 라고 한다. 그런데 샘플 `9XUFAXIP8BXBAP1NIEG66EF.upk` 는 export 1,598개가 전부 `shadercache` 다. Arkive 가 셰이더 캐시를 찾는 경로·이름 규칙을 확인하고, 한국판 배치에 맞춘다.
7. [ ] **P3-8.** (중단됨 — 사용자 지시 대기, F33) `index-build` 를 한국판 Packages(34,103개)에 돌린다. 오래 걸리므로 백그라운드로 실행하고, 실패 패키지 수와 원인을 분류한다. 이 색인이 App 의 에셋 라이브러리의 기반이다.
8. [x] **P3-5.** (완료, F34) GFx 프로브를 `EFUI_*` 전체로 넓혀 PlaceObject3 외의 리테일 변종이 있는지 확인한다(F24 는 60개 패키지 표본).
9. [x] **P3-11.** (종료 — 같은 키로는 불가, F35. 다른 키가 필요하면 사용자에게) (낮음) 한국판에서 유일하게 풀리지 않는 패키지 `Packages/SZWK4EKEQJ0P50I4TSJE4EJQ37.upk`(`efui_iconatlas_enc_novb`, 654,341 B)를 조사한다.
   - 청크 6개 모두 블록 0 은 풀리고 블록 1 에서 LZ4 실패한다(F34). UModel `-kr` 도 `LZ4_decompress_safe returned -7` 로 실패한다.
   - 가설:
     - 이름의 `enc` 대로 다른 보호 방식이다(예: 접두 4096B 가 아니라 블록 전체 AES, 또는 다른 키).
     - 의도적으로 잠근 콘텐츠다.
   - 키를 바이너리에서 추측·추출하지 않는다(§8). 같은 키로 "블록 전체 복호화" 같은 형식 가설만 시험하고, 안 되면 목록에만 남긴다.
10. [x] **P3-12.** (발굴 1회차, 완료, F36) 글꼴 바꾸기가 한국판에서 효과가 없던 문제. 한국판은 `font.lpk` 의 언어 폴더가 아니라 **최상위 `/Binaries/Fonts/FontMap.xml`** 을 읽는다.
11. [x] **P3-13.** (발굴 2회차, 완료, F37) 한국판 사운드가 하나도 읽히지 않던 문제(오디오 XOR 키스트림이 서구판과 다름) → 키 프로파일 `wwiseKeystreamFile` 로 주입.
12. [x] **P3-14.** (발굴 3회차, 완료, F38) 키가 맞지 않을 때의 LPK 오류 문구가 "서구판 클라이언트를 고르라" 고 안내하던 문제 → 쓰인 키와 `ARKIVE_KEY_PROFILE` 안내로 교체. 같은 회차에 스킬 카탈로그가 한국판에서 동작함을 확인했다.
13. [x] **P3-15.** (발굴 4회차, 완료, F39) "Steam 에서 게임 파일을 검사하라" 고만 안내하던 사용자 메시지 11곳을 Steam·한국판 런처 둘 다 말하도록 바꿨다(`GameFileCheck`).
14. [ ] **P3-16.** (사용자 확인 필요, F40) 한국판 로그인 영상 15개를 Arkive 가 하나도 못 쓴다(`LoginVideoCipher.FindKey` = null). 포크 `Tools/MoviePipeline/bink_key_solver.py` 는 영상마다 48B XOR 키를 Bink 헤더·프레임 표 구조로 **풀어낸다**(저장된 키 없음).
   - 이것을 Arkive 로 옮기면 "게임 데이터에서 키를 복원"하는 일이 된다. Arkive 의 서구판 경로도 이미 헤더 필드로 파일별 16B 를 유도하지만, 고정 기준 키가 있다.
   - §8(키 추출·추측 금지, 대상은 클라이언트 바이너리)에 걸리는지 사용자 판단을 받은 뒤 진행한다.
15. [ ] **P3-17.** (사용자 인게임 확인 필요, F41) 한국판 data2 에 `EFTable_Item.db` 가 루트·`KOR/`·`DEV/` 세 벌 있고 루트와 `KOR/` 의 내용이 다르다. Arkive 는 루트를 쓴다.
   - 게임이 어느 것을 읽는지는 데이터만으로는 판정되지 않는다. 사용자가 게임에서 아래 아이템이 있는지 확인하면 정해진다.
     - `KOR/` 에만 있는 것: 곡물 파우더, 향신 파우더, 생활 포인트 물약.
     - 루트에만 있는 것: 차원술사 : 차원의 틈, 칭호 : 찰나의 지배자.
   - `KOR/` 쪽이면 빌더·시세 표·게임 데이터 편집이 지역 폴더를 우선하도록 바꾼다.
