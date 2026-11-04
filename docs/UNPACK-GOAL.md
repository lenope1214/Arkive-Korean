# 언팩(Unpack) 목표 문서

> 이 문서는 loop가 매 반복마다 읽고 갱신하는 **단일 진실 원천**이다.
> 작업 전에 먼저 읽고, 작업 후에 반드시 "진행 로그"와 체크리스트를 갱신한다.
>
> **상태 (2026-11-04): 완료.** 1단계(언팩, G1~G5)와 2단계(포크 접목, P2-1~6)가 모두 끝났다. 재현은 §7, 포크에서 가져온 것과 남긴 것은 §9. 키는 `work/unpack/keys.local.json` (§5-1).

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
  "lpkDbBaseKeyHex": "<32 hex>"
}
```

- 키 값은 **절대 출력·로그·보고서·문서·커밋에 남기지 않는다.** 보고서/로그에는 "user-supplied profile" 정도만 쓴다. 포크에서 값을 옮길 때도 파일에서 파일로(정규식), 화면에 찍지 않는다.
- 한국판 키 상수를 Arkive 소스에 넣지 않는다. 필요하면 로컬 파일/환경변수로만 주입한다.
- 포크 저장소는 외부 자료로 취급: `C:\Users\chlsk\AppData\Local\Temp\claude\...\scratchpad\signle\repo` 에 읽기 전용 클론이 있다(없으면 `git clone --depth 1 https://github.com/lenope1214/Lostark-signle.git`). 포크의 스크립트를 실행하지 않고 읽기만 한다. 포크 코드를 가져올 때는 Arkive 스타일로 다시 쓴다(복사 금지, 라이선스/출처는 §9에 기록).

## 6. 진행 로그 (최신이 위)

| 일시 | 반복 | 한 일 | 결과 | 다음 |
|---|---|---|---|---|
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
P="D:/Smilegate/Games/LOSTARK/EFGame/ReleasePC/Packages"  # UPK 34,103개, 파일명은 난독화
DIAG="dotnet run -c Release --no-build --project src/Arkive.UPKDiagnostic --"
CLI="dotnet run -c Release --no-build --project src/Arkive.Cli --"

# ── 1단계: 언팩 검증 (G1~G4) ─────────────────────────────────────────────
# UPK 샘플은 복사해서 쓴다 (보고서가 UPK 옆에 저장됨)
mkdir -p work/unpack/samples && cp "$P/<NAME>.upk" work/unpack/samples/
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
