# Runtime Contract Kernel

## Scope

この成果物は、`brave-crest` ブランチ上の `plans/rakuspa-kanda-event-api*.md` と `AGENTS.md` を入力として、RAKU SPA 1010 神田コラボイベント確認 API の selected runtime contracts を定義する。

対象 contract は、ユーザー指定および change-risk-triage の selected runtime contracts に従い、次の 3 件に限定する。

- `RC-001`: Easy Auth protected HTTP access
- `RC-002`: Public source collection
- `RC-003`: Azure OpenAI structured extraction and validation

この pass では production code、test code、Plan、triage、implementation-contract artifact は変更しない。runtime contract identification と participant / boundary mapping を記録し、後続の `test-design-kernel` と `verification-kernel` が再探索なしに参照できる handoff を作る。

## Runtime Contract Kernel

| Contract ID | Scenario | Producer | Consumer | Message / API / Event | Required fields | Error / timeout behavior | Production implementation address | Verification hook |
| --- | --- | --- | --- | --- | --- | --- | --- | --- |
| RC-001 | モバイル/ブラウザから RAKU SPA 1010 神田イベント API を呼び出す | Mobile/browser client + App Service Authentication / Authorization | `RakuSpaKandaEventsFunction` | `GET /api/rakuspa/kanda/events?date=YYYY-MM-DD`; deployed environment では Easy Auth / Microsoft Entra が request を gate する。Function trigger は `AuthorizationLevel.Anonymous` | `date` query parameter; authenticated Easy Auth session; platform-provided request identity; correlation ID; `Application:TimeZone` for omitted date | 未認証 request は deployed environment で Function event data へ到達しない。具体的な 302 redirect / 401 は Easy Auth unauthenticated action の設定に依存し、`NeedsHumanDecision`。invalid date は 400。unexpected server/config/source/AOAI failure は 500。Function key fallback は禁止 | `src/ToolWebGetSimpleFunctions.Functions/Functions/RakuSpaKandaEventsFunction.cs`; `src/ToolWebGetSimpleFunctions.Functions/Program.cs`; Azure App Service Authentication / Authorization configuration; Entra tenant/users/groups/app roles configuration | TP-RC001-A: deployed unauthenticated request does not return event data; TP-RC001-B: authenticated request with valid date returns 200 JSON; TP-RC001-C: omitted date uses configured timezone; TP-RC001-D: route is `rakuspa/kanda/events` and Function authLevel is `Anonymous`; to be detailed by test-design-kernel |
| RC-002 | Function 内の source collector が公式/候補ページから text-only extraction input を構築する | `RakuSpaSourceCollector` + named source `HttpClient` + `AngleSharpHtmlTextExtractor` | `ExtractionInput` consumer used by `AzureOpenAIRestExtractionClient` | HTTP GET to `SourceCollection:OfficialNewsUrl`; deterministic article/campaign/PR TIMES link following only when linked from official/candidate pages; AngleSharp DOM extraction; regex/string scanning for date-like and venue-like strings | `SourceCollection:OfficialNewsUrl`; `SourceCollection:FacilityName`; `SourceCollection:FacilityAliases`; candidate article URL/title/body; campaign page URL/title/body; image `alt` text; detected date-like strings; detected venue-like strings; fetched URL list; candidate count; correlation ID | Official news fetch failure is fatal. Required candidate/campaign fetch failure is fatal. General web search is not used. Partial source success must not silently lower confidence. Timeout uses `SourceCollection:HttpTimeoutSeconds`; page size capped by `SourceCollection:MaxPageBytes`; all caught exceptions log `Exception.ToString()` | `src/ToolWebGetSimpleFunctions.Functions/Services/RakuSpaSourceCollector.cs`; `src/ToolWebGetSimpleFunctions.Functions/Services/AngleSharpHtmlTextExtractor.cs`; `src/ToolWebGetSimpleFunctions.Functions/Options/SourceCollectionOptions.cs`; DI registration in `Program.cs` | TP-RC002-A: fixture official news HTML produces candidate articles; TP-RC002-B: linked campaign page visible text and image alt are included; TP-RC002-C: fatal fetch failure returns explicit error and logs exception; TP-RC002-D: no general web search / Playwright / image binary path exists; to be detailed by test-design-kernel |
| RC-003 | text-only candidate input を AOAI Structured Outputs へ渡し、C# validator/selector が active/next event を決定する | `AzureOpenAIRestExtractionClient` | `EventCandidateValidator` + `EventSelectionService` + API response builder | `POST {AzureOpenAI:Endpoint}/openai/deployments/{AzureOpenAI:DeploymentName}/chat/completions?api-version={AzureOpenAI:ApiVersion}` with `response_format.type = json_schema`, `strict = true`; C# deterministic validation and selection after model response | AOAI endpoint; deployment name; API version; API key from app settings; `ExtractionInput`; JSON schema name `rakuspa_event_extraction_result`; candidate fields: `title`, `startDate`, `endDate`, `venueConfirmed`, `venueEvidence`, `eventType`, `sourceUrls`, `evidenceSnippets`, `confidence`, `warnings`; search date; source URL traceability data | AOAI failure, malformed Structured Outputs response, schema mismatch, validation infrastructure error are explicit errors, not fallback. Candidate-level invalid date/source/venue/eventType is rejected. No valid candidate is successful 200 with null events and warning/sourceSummary. JSON mode substitute is prohibited. Timeout behavior is via named AOAI HttpClient / request timeout and remains to be concretized by test-design-kernel | `src/ToolWebGetSimpleFunctions.Functions/Services/AzureOpenAIRestExtractionClient.cs`; `src/ToolWebGetSimpleFunctions.Functions/Services/EventCandidateValidator.cs`; `src/ToolWebGetSimpleFunctions.Functions/Services/EventSelectionService.cs`; `src/ToolWebGetSimpleFunctions.Functions/Options/AzureOpenAIOptions.cs`; `src/ToolWebGetSimpleFunctions.Functions/Models/Extraction/*.cs`; `src/ToolWebGetSimpleFunctions.Functions/Models/Api/*.cs`; DI registration in `Program.cs` | TP-RC003-A: AOAI request contains `response_format.type = json_schema` and strict schema; TP-RC003-B: JSON mode is absent; TP-RC003-C: invalid candidate is rejected; TP-RC003-D: active/next selection handles active, next, none, overlap, same-start cases; TP-RC003-E: start+2 inclusive busy phase; to be detailed by test-design-kernel |

## Plan / implementation contract conformance

| Runtime Contract ID | Plan requirement | Implementation contract decision | Runtime contract address | Conformance |
| --- | --- | --- | --- | --- |
| RC-001 | FR-001 / AC-001 / AC-002 / AC-003 / AC-017: mobile/browser callable authenticated HTTP API; unauthenticated caller must not receive event data; omitted date uses configured timezone | Function trigger is `AuthorizationLevel.Anonymous`; Easy Auth / Microsoft Entra performs platform-level authentication; Function key primary auth is prohibited; local anonymous mode is not production | `RakuSpaKandaEventsFunction.cs`; `Program.cs`; Azure App Service Authentication / Authorization; Entra configuration | Conformant |
| RC-002 | FR-003 / FR-004 / AC-006 / AC-007 / AC-008: official/high-confidence public source collection; article/campaign link following; text-only extraction input; no raw image | Use `RakuSpaSourceCollector`, named HttpClient, AngleSharp extraction, deterministic link following; include visible text, links, image alt, date-like strings, venue-like strings; no general web search, Playwright, image binary | `RakuSpaSourceCollector.cs`; `AngleSharpHtmlTextExtractor.cs`; `SourceCollectionOptions.cs`; source/candidate DTOs | Conformant |
| RC-003 | FR-005 / FR-006 / FR-007 / FR-008 / FR-009 / AC-009〜AC-016: AOAI Structured Outputs; deterministic validation; active/next selection; busy phase classification; stable response JSON | Use AOAI REST Chat Completions with `response_format.type = json_schema`; model returns candidates only; C# validator rejects invalid/untraceable candidates; selector builds active/next; response DTO stable | `AzureOpenAIRestExtractionClient.cs`; `EventCandidateValidator.cs`; `EventSelectionService.cs`; extraction/API models; `Program.cs` DI | Conformant |

## Notes / assumptions

- `AGENTS.md` の bounded flow に従い、この pass は runtime contract と participant / boundary mapping のみを行う。production code と tests は作成しない。
- `RC-001` の Easy Auth exact behavior は `NeedsHumanDecision` である。runtime contract としては「未認証 request が event data に到達しない」ことを要求し、302 redirect か 401 response かは deployment configuration と test-design で扱う。
- `RC-001` では Function trigger を `AuthorizationLevel.Anonymous` とすることを implementation-contract の decision として採用する。Function key を primary mobile auth として追加しない。
- `RC-002` は deterministic link following のみを扱う。一般 web search、Playwright、画像 OCR / vision analysis は selected slice 外である。
- `RC-002` では source fetch の部分成功を「低 confidence fallback」として扱わない。required source が fatal failure した場合は explicit error とする。
- `RC-003` は AOAI REST API surface を前提にする。Azure.AI.OpenAI SDK への切替、managed identity / keyless AOAI access、model/deployment/region 選定はこの pass では扱わない。
- `RC-003` の JSON schema 詳細は implementation-contract の schema contract を引き継ぐ。後続の `test-design-kernel` で hand-written schema の fixture validation を test point 化する必要がある。
- エスカレーション条件は現時点では満たさない。selected contracts は 3 件に限定され、kernel table で participant / boundary を表現できる。詳細 sequence diagram や runtime-evidence pass は、Easy Auth 実デプロイ挙動や AOAI 実レスポンスの証跡が必要になった時点で検討する。
- Production implementation address は implementation-contract-kernel の `MissingButRequired` production addresses を採用している。既存 implementation が存在するとは扱わない。

## Handoff Packet

- Profile used: `contract-kernel`
- Source artifacts:
  - `AGENTS.md`
  - `plans/rakuspa-kanda-event-api.md`
  - `plans/rakuspa-kanda-event-api-change-risk-triage.md`
  - `plans/rakuspa-kanda-event-api-implementation-contract-kernel.md`
  - `plans/rakuspa-kanda-event-api-implementation-contract-review-kernel.md`
  - `https://github.com/suusanex/coding_agent_plan_and_verify_process/blob/main/.github/agents/runtime-contract-kernel.agent.md`
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
  - `.github/agents/runtime-contract-kernel.agent.md` from process repository
- Files intentionally not inspected:
  - production source files: 新規作成であり、この pass は implementation-contract の production addresses を source of truth とするため
  - test files: この pass では tests を作成/確認しないため
  - Azure resource / IaC files: Azure resource 作成と deployment automation は runtime contract kernel の scope 外であるため
  - live RAKU SPA pages: selected runtime contract は source collection boundary を定義するものであり、live scraping evidence は test-design / manual verification で扱うため
- Decisions made:
  - runtime contract 対象はユーザー指定どおり `RC-001` / `RC-002` / `RC-003` に限定する
  - `RC-001` は mobile/browser → Easy Auth platform → `RakuSpaKandaEventsFunction` の authentication boundary として定義する
  - `RC-002` は `RakuSpaSourceCollector` / `AngleSharpHtmlTextExtractor` → `ExtractionInput` の source collection boundary として定義する
  - `RC-003` は `AzureOpenAIRestExtractionClient` → `EventCandidateValidator` / `EventSelectionService` の AOAI structured extraction and validation boundary として定義する
  - production implementation addresses は implementation-contract-kernel の `MissingButRequired` paths を採用し、既存実装があるとは扱わない
  - Function key primary auth、JSON mode、Playwright、画像解析、general web search は runtime contract から除外する
- Do not redo unless new evidence appears:
  - selected runtime contract IDs を rename しない
  - `AuthorizationLevel.Anonymous` + Easy Auth enforcement の方針を、Function key primary auth へ置換しない
  - AOAI Structured Outputs を JSON mode / prompt-only JSON に置換しない
  - source collection を general web search / Playwright / image OCR へ広げない
  - `RC-CAND-*` や cache / Timer / Durable Functions / notification / native Android をこの artifact に追加しない
- Remaining work:
  - `NeedsHumanDecision`: Easy Auth unauthenticated action の exact behavior（302 redirect / 401 など）と allowed users/groups/app roles
  - `NeedsHumanDecision`: Azure Functions hosting plan と selected target framework compatibility
  - `NeedsHumanDecision`: AOAI deployment/model/region/credential strategy
  - `NeedsHumanDecision`: exact NuGet versions と Application Insights deployment setup
  - `Deferred`: managed identity / keyless AOAI access
  - `Deferred`: cache / Timer / storage design
  - `NotImplementedOrMismatch`: all production addresses listed in this artifact are not yet implemented because this is a new application
  - `PartiallyDone`: runtime contract identification and participant/boundary mapping are complete for RC-001 / RC-002 / RC-003; test point details remain for `test-design-kernel`
- Recommended next step:
  - `test-design-kernel.agent.md` を実行する
  - inputs: `AGENTS.md`, `plans/rakuspa-kanda-event-api.md`, `plans/rakuspa-kanda-event-api-change-risk-triage.md`, `plans/rakuspa-kanda-event-api-implementation-contract-kernel.md`, `plans/rakuspa-kanda-event-api-implementation-contract-review-kernel.md`, `plans/rakuspa-kanda-event-api-runtime-contract-kernel.md`
  - test-design では、RC-001 / RC-002 / RC-003 に対する fake/stub/mock 境界、CI-safe test point、manual-only real environment validation を明示する
