# Test Design Kernel

## Scope

この成果物は、`suusanex/tool_web_get_simple_functions` の `brave-crest` ブランチにある `AGENTS.md` と `plans/rakuspa-kanda-event-api*.md` を入力として、RAKU SPA 1010 神田コラボイベント確認 API の test design を定義する。

主要入力は `plans/rakuspa-kanda-event-api-runtime-contract-kernel.md` である。caller が対象として明示した contract は次の 3 件であり、この pass ではそれ以外の runtime contract へ scope を広げない。

- `RC-001`: Easy Auth protected HTTP access
- `RC-002`: Public source collection
- `RC-003`: Azure OpenAI structured extraction and validation

この pass では production code、test code、Plan、runtime contract、coverage document は作成または変更しない。test point mapping、stub/fake/in-memory usage identification、production binding verification requirement を記録して停止する。

## Test Design Kernel

| Test Point ID | Runtime Contract ID | What to verify | Stub / fake allowed? | Production binding required? | Expected observation | Status |
| --- | --- | --- | --- | --- | --- | --- |
| TP-RC001-A | RC-001 | デプロイ環境で未認証 request が event data を受け取れないことを確認する | No | Yes | 未認証で `GET /api/rakuspa/kanda/events` を呼び出すと、Easy Auth 設定に従って 302 sign-in redirect または 401/403 が返り、`facility` / `activeEvent` / `nextEvent` を含む event response body は返らない | ManualOnly |
| TP-RC001-B | RC-001 | 認証済み許可ユーザーが valid date で API を呼び出すと stable JSON response を受け取れることを確認する | Yes | Yes | authenticated caller が `GET /api/rakuspa/kanda/events?date=YYYY-MM-DD` を呼ぶと HTTP 200 が返り、`facility`、`searchDate`、`generatedAt`、`activeEvent`、`nextEvent`、`warnings`、`sourceSummary` が存在する | Done |
| TP-RC001-C | RC-001 | date omitted 時に configured timezone の current date が `searchDate` に使われることを確認する | Yes | Yes | fake `IClock` と `Application:TimeZone=Asia/Tokyo` を使う test で、date query を省略した response の `searchDate` が Asia/Tokyo の日付になる | Done |
| TP-RC001-D | RC-001 | Function route と authLevel が runtime contract と一致することを確認する | No | Yes | production function entrypoint が route `rakuspa/kanda/events` を公開し、HTTP GET のみを受け付け、Function trigger authLevel が `Anonymous` であることを確認できる | Done |
| TP-RC001-E | RC-001 | invalid date query が deterministic 400 response になることを確認する | Yes | Yes | `date` が `YYYY-MM-DD` ではない request は HTTP 400 になり、source collection や AOAI call は実行されない | Done |
| TP-RC002-A | RC-002 | fixture official news HTML から collaboration candidate article が抽出されることを確認する | Yes | Yes | fake HTTP response の official news HTML に `コラボ` / `開催決定` などの signal を含む article link がある場合、`ExtractionInput` に candidate article URL/title/text が含まれる | Done |
| TP-RC002-B | RC-002 | candidate article から linked campaign page を辿り、visible text と image alt を text-only input に含めることを確認する | Yes | Yes | fake article HTML に campaign page link がある場合、fake campaign page の visible text、link URL、`img alt` が `ExtractionInput` に含まれ、raw image bytes は含まれない | Done |
| TP-RC002-C | RC-002 | required source fetch failure が explicit error になり、silent fallback しないことを確認する | Yes | Yes | fake HTTP handler が official news または required linked page fetch failure を返すと、request は explicit error path に入り、lower-confidence partial answer は返らない | Done |
| TP-RC002-D | RC-002 | source collection が selected scope 外の手段を使わないことを確認する | Yes | Yes | source collection test または static inspection で、general web search、Playwright、image binary / OCR / vision input の production path が存在しないことを確認できる | Done |
| TP-RC002-E | RC-002 | source collection の timeout/page-size/config limits が request flow に反映されることを確認する | Yes | Yes | configured `HttpTimeoutSeconds`、`MaxPageBytes`、`MaxLinkedPagesPerRequest`、`MaxCandidateArticles` を超える fixture input で explicit error または bounded truncation/warning contract が観測できる。bounded truncation を採用する場合も silent fallback ではない | PartiallyDone |
| TP-RC003-A | RC-003 | AOAI request が Structured Outputs schema request になっていることを確認する | Yes | Yes | fake AOAI endpoint / HTTP handler が受け取る request JSON に `response_format.type = json_schema`、`json_schema.strict = true`、schema name `rakuspa_event_extraction_result`、required fields、`additionalProperties: false` が含まれる | Done |
| TP-RC003-B | RC-003 | JSON mode または free-form prompt-only JSON が production request に使われないことを確認する | Yes | Yes | AOAI request builder test または static inspection で `response_format.type = json_object` が使われず、schema-less extraction path が存在しないことを確認できる | Done |
| TP-RC003-C | RC-003 | LLM candidate validation が invalid/untraceable values を reject することを確認する | Yes | Yes | fake AOAI response で invalid date order、missing source URL、unconfirmed venue、unsupported eventType、input に trace できない URL/title/date/venue を返すと、candidate が final selection から除外され warning/error が記録される | Done |
| TP-RC003-D | RC-003 | active/next event selection が deterministic に動作することを確認する | Yes | Yes | validated candidate fixture に対して、active、next、none、overlap、same-start-date cases の response が deterministic ordering と warning rule に従う | Done |
| TP-RC003-E | RC-003 | busy phase classification の start+2 inclusive rule を確認する | Yes | Yes | start date、start+1、start+2 は `initial_peak`、event period 内の start+3 以降は `after_initial_peak`、event 前は `before_event`、event null は `no_event` になる | Done |
| TP-RC003-F | RC-003 | AOAI failure / malformed Structured Outputs response が explicit error になり fallback しないことを確認する | Yes | Yes | fake AOAI endpoint が HTTP error、timeout、malformed JSON、schema mismatch を返すと explicit error path になり、raw model text や stale/partial answer は返らない | Done |
| TP-CROSS-LOGGING | RC-001 / RC-002 / RC-003 | selected contracts の catch path で `Exception.ToString()` が trace log に出力され、secret/token が log に出ないことを確認する | Yes | Yes | fake logger で exception path を観測すると exception `ToString()` 相当の detail が記録され、AOAI API key、Authorization、Cookie、Easy Auth token、full `X-MS-CLIENT-PRINCIPAL`、raw oversized prompt は記録されない | Done |

In this agent, `Done` means the test design row is complete for this pass. It does not mean the test has been implemented, executed, or verified.

## Required production binding checks

| Test Point ID | Runtime Contract ID | Substitute used / expected | Production implementation to check | Production wiring / entrypoint to check | Notes |
| --- | --- | --- | --- | --- | --- |
| TP-RC001-A | RC-001 | No automated substitute; real Easy Auth / Entra environment required | Azure App Service Authentication / Authorization configuration; Entra tenant/users/groups/app roles | Deployed Function App authentication settings; unauthenticated action; endpoint does not return event body before auth | `ManualOnly`; exact 302/401/403 behavior is `NeedsHumanDecision` |
| TP-RC001-B | RC-001 | fake `IRakuSpaEventLookupService` or fake downstream services may be used for local/CI API response shape | `RakuSpaKandaEventsFunction.cs`; `IRakuSpaEventLookupService`; API response DTOs | `Program.cs` DI registration; Function endpoint route; Easy Auth config for real environment | CI may validate response shape with substitutes; verification-kernel must confirm production route/wiring |
| TP-RC001-C | RC-001 | fake `IClock`; test configuration for `Application:TimeZone` | `SystemClock`; `ApplicationOptions`; date parsing/defaulting path in `RakuSpaKandaEventsFunction` or lookup service | `Program.cs` options binding and `IClock` registration | Production must use `SystemClock`; tests may inject fake clock |
| TP-RC001-D | RC-001 | No substitute expected | `RakuSpaKandaEventsFunction.cs` | Function attribute/route metadata; host startup; deployed endpoint | Confirms implementation-contract decision: `AuthorizationLevel.Anonymous` behind Easy Auth |
| TP-RC001-E | RC-001 | fake lookup/source/AOAI may be used to assert they are not called | `RakuSpaKandaEventsFunction.cs`; request validation code path | Function route and dependency injection for lookup service | Prevents invalid date from crossing into source/AOAI boundary |
| TP-RC002-A | RC-002 | fake HTTP handler / in-memory fixture HTML | `RakuSpaSourceCollector.cs`; `AngleSharpHtmlTextExtractor.cs`; source DTOs | `Program.cs` named source `HttpClient`; `IHtmlTextExtractor` registration; `SourceCollectionOptions` binding | Fixture tests do not prove production URL/config binding; verification-kernel must check production registration |
| TP-RC002-B | RC-002 | fake HTTP handler / in-memory article and campaign fixtures | `RakuSpaSourceCollector.cs`; `AngleSharpHtmlTextExtractor.cs`; `ExtractionInput` model | named source `HttpClient`; max linked pages config; source collector DI | Must verify raw image bytes are not sent to extraction input |
| TP-RC002-C | RC-002 | fake HTTP failure / timeout response | `RakuSpaSourceCollector.cs`; error handling/logging path | named source `HttpClient`; configured timeout; logging configuration | Ensures AGENTS.md no-fallback and Exception.ToString logging rule are testable |
| TP-RC002-D | RC-002 | static inspection or dependency-level test; no external service | `RakuSpaSourceCollector.cs`; project dependencies | `.csproj` package references; no Playwright / OCR / vision dependency; source collector DI | Guards against prohibited substitutions |
| TP-RC002-E | RC-002 | fake HTTP handler / oversized fixture | `RakuSpaSourceCollector.cs`; `SourceCollectionOptions` | options binding; named source `HttpClient`; size and count limit enforcement | Exact behavior for over-limit input may need implementation-time clarification; current design requires bounded non-silent behavior |
| TP-RC003-A | RC-003 | fake AOAI HTTP handler | `AzureOpenAIRestExtractionClient.cs`; hand-written JSON schema builder; extraction DTOs | named AOAI `HttpClient`; `AzureOpenAIOptions`; DI registration | Confirms REST Structured Outputs rather than SDK/json mode path |
| TP-RC003-B | RC-003 | fake AOAI HTTP handler or static inspection | `AzureOpenAIRestExtractionClient.cs` | `.csproj` dependencies and DI path to AOAI client | Guards against JSON mode / prompt-only JSON substitution |
| TP-RC003-C | RC-003 | fake AOAI response / direct validator fixtures | `EventCandidateValidator.cs`; extraction/source DTOs | DI registration for validator; production lookup flow calls validator before selector | Candidate rejection tests do not prove production flow uses validator; verification-kernel must check call chain |
| TP-RC003-D | RC-003 | in-memory validated candidate fixtures | `EventSelectionService.cs`; API response DTOs | DI registration for selector; production lookup flow calls selector after validation | Covers deterministic ordering and warning behavior |
| TP-RC003-E | RC-003 | in-memory event/date fixtures | `EventSelectionService.cs` or busy phase helper | production lookup flow maps selected events to `EventDto` | Confirms start+2 inclusive rule from Plan |
| TP-RC003-F | RC-003 | fake AOAI HTTP error/malformed response | `AzureOpenAIRestExtractionClient.cs`; extraction parser; error handling/logging path | named AOAI `HttpClient`; options binding; production lookup flow error propagation | Prevents fallback to raw text/stale data |
| TP-CROSS-LOGGING | RC-001 / RC-002 / RC-003 | fake logger / fake failing dependencies | catch paths in `RakuSpaKandaEventsFunction.cs`, `RakuSpaSourceCollector.cs`, `AzureOpenAIRestExtractionClient.cs`, validator/selector infrastructure | `Program.cs` logging configuration; Application Insights or configured logger sink in deployment | Application Insights setup is environment-specific; local fake logger can verify emitted messages but not real sink ingestion |

## Manual-only checks

- `MO-RC001-A`: Easy Auth / Microsoft Entra の real deployed environment で、未認証 request が event data を返さないことを確認する。302 redirect / 401 / 403 の exact behavior は deployment configuration に依存するため、人間が chosen setting を確認する。
- `MO-RC001-B`: 許可ユーザー/グループ/app role の設定が意図した利用者に限定されていることを確認する。code 側に allowed users/groups を hardcode しない。
- `MO-RC001-C`: モバイルブラウザまたは HTTP shortcut app から、Easy Auth sign-in 後に endpoint JSON を直接表示できることを確認する。
- `MO-RC002-A`: 実 RAKU SPA 1010 神田 official news page / linked campaign page の live HTML shape が fixture 前提と大きく乖離していないことを、実装後に手動または manual-only integration で確認する。CI では live external Web に依存しない。
- `MO-RC003-A`: 実 Azure OpenAI deployment/model/API version で `response_format.type = json_schema` request が受理されることを確認する。CI では fake AOAI を使用する。
- `MO-CFG-A`: Azure Functions hosting plan、target framework、AOAI deployment/model/region、Application Insights sink、secret configuration が選定済みであることを確認する。

## Notes / assumptions

- この成果物は test design のみであり、tests は実装していない。`Done` は test design row がこの pass で記録済みであることだけを意味する。
- `AGENTS.md` のルールに従い、UnitTest と CI IntegrationTest は実 OS 環境や外部環境を変更しない。Easy Auth、external Web、AOAI は CI では fake/stub/mock を使う。
- stub/fake を使う test point はすべて `Production binding required? = Yes` とした。これは Stub-complete but production-missing を防ぐためである。
- `RC-001` の real Easy Auth behavior は自動 unit test だけでは確認できないため、manual-only checks に分離した。CI では response shape、date parsing、route/authLevel metadata など code-level observable を扱う。
- `RC-002` の live RAKU SPA page 取得は CI では行わない。fixture HTML と fake HTTP handler を使い、live source drift は manual-only または明示的な外部依存テストに限定する。
- `RC-003` の AOAI は CI では呼び出さない。fake AOAI HTTP handler で request schema、error handling、malformed response handling、validator/selector behavior を確認する。
- `TP-RC002-E` は `PartiallyDone` とした。implementation-contract は timeout / page size / max link count config を要求しているが、over-limit input の exact observable behavior は implementation-time に explicit error とするか bounded truncation + warning とするかの細部が未確定である。ただし silent fallback は禁止である。
- `TP-CROSS-LOGGING` は selected contracts をまたぐ横断 guardrail である。新しい runtime contract を追加するものではなく、AGENTS.md と各 selected contract の error handling requirements を検証点として束ねる。
- 現時点で `integration-test-design.agent.md` や full-coverage へのエスカレーションは不要と判断する。selected contracts は 3 件に限定され、unit/CI-safe fixture tests と manual-only checks に分離できる。ただし real deployment の認証挙動や live source drift を広く扱う段階では、別途 integration test design を検討する。

## Handoff Packet

- Profile used: `contract-kernel`
- Source artifacts:
  - `AGENTS.md`
  - `plans/rakuspa-kanda-event-api.md`
  - `plans/rakuspa-kanda-event-api-change-risk-triage.md`
  - `plans/rakuspa-kanda-event-api-implementation-contract-kernel.md`
  - `plans/rakuspa-kanda-event-api-implementation-contract-review-kernel.md`
  - `plans/rakuspa-kanda-event-api-runtime-contract-kernel.md`
  - `https://github.com/suusanex/coding_agent_plan_and_verify_process/blob/main/.github/agents/test-design-kernel.agent.md`
- Selected contracts / IDs:
  - `RC-001`: Easy Auth protected HTTP access
  - `RC-002`: Public source collection
  - `RC-003`: Azure OpenAI structured extraction and validation
- Files inspected:
  - `AGENTS.md`
  - `plans/rakuspa-kanda-event-api.md`
  - `plans/rakuspa-kanda-event-api-change-risk-triage.md`
  - `plans/rakuspa-kanda-event-api-implementation-contract-kernel.md`
  - `plans/rakuspa-kanda-event-api-implementation-contract-review-kernel.md`
  - `plans/rakuspa-kanda-event-api-runtime-contract-kernel.md`
  - `.github/agents/test-design-kernel.agent.md` from process repository
- Files intentionally not inspected:
  - production source files: 新規作成であり、test design は implementation-contract/runtime-contract の production addresses を source of truth とするため
  - existing test files: 新規作成であり、この pass では test implementation や suite exploration を行わないため
  - Azure resource / IaC files: deployment automation と real resource configuration は ManualOnly / NeedsHumanDecision として扱うため
  - live RAKU SPA pages: CI-safe test design では fixture を使い、live source drift は manual-only check として分離するため
- Decisions made:
  - test design 対象は `RC-001` / `RC-002` / `RC-003` のみとする
  - CI-safe tests は fake/stub/mock/in-memory fixture を使い、real Easy Auth / external Web / AOAI calls は manual-only に分離する
  - stub/fake を使う test point はすべて production binding verification を必須にする
  - Function key primary auth、JSON mode、general web search、Playwright、image binary / OCR / vision path は prohibited substitution guard として test point に含める
  - Exception.ToString logging と secret redaction は selected contracts をまたぐ cross-cutting test point とする
  - `Bound` は付けない。production binding verification は verification-kernel へ引き継ぐ
- Do not redo unless new evidence appears:
  - selected contract IDs を増やさない
  - CI tests で real Easy Auth / real RAKU SPA live page / real AOAI deployment を必須にしない
  - stub/fake-only success を production binding success と扱わない
  - JSON mode、Function key primary auth、Playwright、image analysis を substitute として再導入しない
  - `TP-RC002-E` の exact over-limit behavior は実装時に明示するまで `PartiallyDone` として扱う
- Remaining work:
  - `NotImplementedOrMismatch`: production code は未実装であり、すべての test point は未実装
  - `NotImplementedOrMismatch`: production binding / DI / entrypoint verification は未実行
  - `ManualOnly`: Easy Auth real deployment behavior、allowed users/groups、mobile browser sign-in flow
  - `ManualOnly`: real AOAI deployment/model/API version の Structured Outputs acceptance
  - `ManualOnly`: live RAKU SPA source shape drift confirmation
  - `NeedsHumanDecision`: Azure Functions hosting plan、AOAI deployment/model/region/credential strategy、Easy Auth unauthenticated action、exact NuGet versions、Application Insights setup
  - `PartiallyDone`: timeout/page-size/max-count over-limit behavior の exact observable contract
  - `Deferred`: managed identity / keyless AOAI access、cache / Timer / storage design
- Recommended next step:
  - `verification-kernel.agent.md` を実行する
  - inputs: `AGENTS.md`, `plans/rakuspa-kanda-event-api.md`, `plans/rakuspa-kanda-event-api-change-risk-triage.md`, `plans/rakuspa-kanda-event-api-implementation-contract-kernel.md`, `plans/rakuspa-kanda-event-api-implementation-contract-review-kernel.md`, `plans/rakuspa-kanda-event-api-runtime-contract-kernel.md`, `plans/rakuspa-kanda-event-api-test-design-kernel.md`
  - verification では production implementation address、DI wiring、Function endpoint metadata、AOAI request builder、source collector adapter、validator/selector call chain、Exception.ToString logging、secret redaction を確認する
