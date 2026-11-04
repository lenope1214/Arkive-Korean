# 언팩(Unpack) 목표 문서

> 이 문서는 loop가 매 반복마다 읽고 갱신하는 **단일 진실 원천**이다.
> 작업 전에 먼저 읽고, 작업 후에 반드시 "진행 로그"와 체크리스트를 갱신한다.
>
> **마지막 확인:** 2026-11-04 — 키 파일(`work/unpack/keys.local.json`) 없음, 키 대기 중 (§5-1)

## 1. 목표 (Goal)

한국 로스트아크 클라이언트(`D:\Smilegate\Games\LOSTARK`)의 패키지를 Arkive가 **읽을 수 있게** 만든다.

최종 완료 조건 (모두 만족해야 DONE):

- [ ] G1. `EFGame\ReleasePC\*.upk` 중 대표 샘플이 **전 청크 해제**까지 성공한다 (청크 오류 0).
- [ ] G2. 해제한 논리 스트림에서 Name/Export/Import 테이블이 정상 파싱된다.
- [ ] G3. `EFGame\data*.lpk` 의 인덱스가 읽히고, 압축 엔트리가 해제된다.
- [ ] G4. `EFGame\data2.lpk` 의 게임 DB(`EFTable_*.db`)가 SQLite 헤더로 복호화된다.
- [ ] G5. 위 결과가 `Arkive.UPKDiagnostic` 보고서(JSON)와 이 문서에 재현 가능한 절차로 남는다.

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
- F10. 도구 사용 주의: 표준입력이 열려 있으면 경로 프롬프트 대기로 멈출 수 있다. 자동 실행 시 `</dev/null` 로 막는다.

## 5. 다음 작업 (우선순위 순 — loop는 맨 위 미완료 항목을 수행)

> **방침 (2026-11-04 사용자 결정):** 한국판 UPK 보호 키는 **사용자가 직접 찾는다.** loop는 키가 필요 없는 범위(T5, T6)만 진행한다.
> loop는 클라이언트 바이너리에서 키를 추출하거나 추측하려 하지 않는다. 사용자가 키/프로파일을 전달하면 T2부터 재개한다.

1. [x] **T1.** 진단 도구에 `--western` 옵션 추가, 샘플 2개로 첫 보호 청크 해제 시도 → **실패** (F6, F7).
2. [x] **T5.** `data*.lpk` 인덱스 읽기 프로브 (`--lpk` 모드 추가). 결과: 8개 전부 `INDEX_INVALID` (F11). 서구판 인덱스 키 비호환 확인.
3. [⏸] **T6.** `data2.lpk` DB 복호화 프로브 (G4). **인덱스 키 대기** (F12). 키가 생기면: `LostArkDatabaseArchive` 는 `internal` 이라 최소한의 공개 래퍼를 Core에 추가해서 프로브한다.
4. [x] **T7.** 재현 절차 정리(G5): §7 갱신 완료.
5. [⏸] **T2.** (키 대기) 전 청크 순회 검증. 한국판 보호 프로파일이 전달돼야 진행.
6. [⏸] **T4.** (키 대기) 논리 스트림 위에서 Name/Export 테이블 파싱 검증(G2).
7. [~] **T3.** 원인 분류 — (a) 키 불일치가 가장 유력, (c) 포맷 차이는 거의 배제 (F8). 나머지는 사용자가 키를 찾은 뒤 확정.

### 5-1. 키 대기 중 loop의 동작 (매 반복 첫 단계)

남은 작업(T2/T4/T6)은 사용자가 찾는 한국판 키가 필요하다. 키는 채팅이 아니라 **로컬 파일**로 전달받는다.

- 경로: `E:\Arkive-Korean\work\unpack\keys.local.json` (`work/` 는 .gitignore 대상 — 커밋되지 않음)
- 형식 (있는 키만 넣으면 됨):

```json
{
  "upkChunkKeyAscii": "<32자 문자열>",
  "upkChunkKeyHex": "<64 hex, 위와 둘 중 하나>",
  "lpkIndexKeyAscii": "<Blowfish 키 문자열>",
  "lpkIndexKeyHex": "<Blowfish 키 hex, 위와 둘 중 하나>"
}
```

loop 규칙:

1. 파일이 **없으면**: 아무것도 하지 않는다 (이 문서 맨 위 "마지막 확인" 줄의 시각만 갱신, §6 로그는 늘리지 않음). 다음 반복을 길게(1시간) 예약.
2. 파일이 **있고 `upkChunkKey*` 가 있으면**: T2 → T4. 진단 도구는 `--key-file work/unpack/keys.local.json` 로 실행한다 (구현됨).
3. 파일이 **있고 `lpkIndexKey*` 가 있으면**: `LostArkDataArchive`/`LostArkDatabaseArchive` 가 키를 외부에서 주입받도록 최소 변경 후 T5 재실행 → T6.
4. 키 값은 **절대 출력·로그·보고서·커밋에 남기지 않는다.** 보고서에는 "user-supplied profile" 정도만 쓴다.
5. 키로 풀렸는지 판정은 결과(해제 크기 일치, SQLite 헤더 등)로만 한다. 풀리지 않아도 loop가 키를 대신 찾거나 추측하지 않는다 → 결과를 기록하고 사용자에게 보고.

## 6. 진행 로그 (최신이 위)

| 일시 | 반복 | 한 일 | 결과 | 다음 |
|---|---|---|---|---|
| 2026-11-04 | 2 | 사용자 방침 반영(키는 사용자가 찾음). T5: `LpkProbe`/`--lpk` 추가, 8개 LPK 프로브. T7: §7 갱신 | 8개 전부 `INDEX_INVALID` — LPK 인덱스 키도 서구판과 다름 (F11~F13). 빌드 경고/오류 0 | **키 필요 없는 작업 소진 → loop 종료(§8).** 사용자가 UPK 보호 키·LPK 인덱스 키를 전달하면 T2/T4/T6 재개 |
| 2026-11-04 | 1 | T1 수행: `--western` 옵션·전체 청크 해제 검증 추가, 샘플 2개(271MB 다청크 / 10KB 단일청크) 실행 | 둘 다 `DECODE_FAILED` — 서구판 키로 한국 UPK 불가 (F6~F8). 빌드 경고/오류 0 | **loop 중단(§8: 한국판 키/프로파일 확보는 사용자 판단 필요)** |
| 2026-11-04 | 0 | 문서 생성, 헤더/청크 진단을 `Program.cs` 에 연결, 샘플 UPK 1개로 첫 진단 실행 | F1~F5 확인, 빌드 경고/오류 0 | T1 |

## 7. 재현 방법

```bash
cd /e/Arkive-Korean
dotnet build src/Arkive.UPKDiagnostic/Arkive.UPKDiagnostic.csproj

# UPK: 헤더·청크 테이블 분석 + 첫 청크 읽기 (샘플은 반드시 복사해서 사용 — 보고서가 UPK 옆에 저장됨)
mkdir -p work/unpack/samples
cp "/d/Smilegate/Games/LOSTARK/EFGame/ReleasePC/<NAME>.upk" work/unpack/samples/
dotnet run --no-build --project src/Arkive.UPKDiagnostic -- "E:\Arkive-Korean\work\unpack\samples\<NAME>.upk" </dev/null
dotnet run --no-build --project src/Arkive.UPKDiagnostic -- --western "E:\Arkive-Korean\work\unpack\samples\<NAME>.upk" </dev/null   # 서구판 프로파일 시험

# LPK: 인덱스 읽기 + 압축 엔트리 해제 프로브 (원본 읽기 전용, 보고서는 work/unpack/lpk/*.probe.json)
D=/d/Smilegate/Games/LOSTARK/EFGame
dotnet run --no-build --project src/Arkive.UPKDiagnostic -- --lpk "$D/data1.lpk" "$D/data2.lpk" </dev/null
```

상태 코드: UPK = `READ_SUCCESS / DECODE_FAILED / PROTECTED_CHUNK / SHORT_READ / ERROR`, LPK = `OK / INDEX_INVALID / INDEX_OK_DECODE_FAILED / ERROR`.

### 재개 조건 (사용자가 키를 찾은 뒤)

- UPK 보호 키(AES-256-ECB 앞 4096B 프로파일 가정)를 받으면: `ChunkProtectionProviderFactory` 에 **새 이름의 한국판 프로파일**을 추가하고(서구판 덮어쓰기 금지), 진단 도구에 선택 옵션을 붙여 T2(전 청크 검증) → T4(Name/Export 파싱)를 수행한다.
- LPK 인덱스 키(Blowfish)를 받으면: `LostArkDataArchive`/`LostArkDatabaseArchive` 의 하드코딩된 키를 프로파일화하고 T5 재실행 → T6(DB 복호화) 수행.
- 키 자체는 저장소에 커밋하지 않고 로컬 설정/환경변수로 주입한다.

## 8. 중단/에스컬레이션 규칙

loop는 다음 중 하나에 해당하면 **멈추고 사용자에게 보고**한다 (wakeup 재예약 금지):

- G1~G5 전부 완료.
- 같은 태스크가 3회 연속 실패하고 새로운 가설이 없다.
- 키/비밀값을 새로 알아내야 하거나 외부에서 가져와야 하는 상황 (사용자 판단 필요). 한국판 UPK 키는 사용자가 찾는 중이므로 loop가 직접 찾지 않는다.
- (키 대기 상태 자체는 중단 사유가 아니다. §5-1 대로 키 파일이 생길 때까지 길게 폴링한다. 단, 사용자가 중단을 요청하면 즉시 종료.)
- 원본 게임 파일을 수정해야만 진행되는 상황.
- 빌드가 복구 불가능하게 깨졌다.
