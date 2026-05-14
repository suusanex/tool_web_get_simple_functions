# Verification Kernel Result

## Scope

この成果物は、`suusanex/tool_web_get_simple_functions` の `brave-crest` ブランチにある `AGENTS.md` と `plans/rakuspa-kanda-event-api*.md` を入力として、RAKU SPA 1010 神田コラボイベント確認 API の selected runtime contracts と selected test points を検証した結果である。

主入力は `plans/rakuspa-kanda-event-api-test-design-kernel.md` と `plans/rakuspa-kanda-event-api-runtime-contract-kernel.md` である。対象は次の 3 contract と、それに紐づく test point のみとする。

- `RC-001`: Easy Auth protected HTTP access
- `RC-002`: Public source collection
- `RC-003`: Azure OpenAI structured extraction and validation

この pass では production code、test code、Plan documents、coverage documents は変更しない。テスト実行は行っていない。production binding / wiring / entrypoint の現物確認、test artifact の有無、contract mismatch / gap の分類のみを行う。

## Runtime contract verification

| Contract ID | Field / behavior | Expected (from Runtime Contract Kernel) | Implementation contract decision | Production evidence | Covered by Test Point ID(s) | Status | Notes |
| --- | --- | --- | --- | --- | --- | --- | --- |
| RC-001 | HTTP route and method | `GET /api/rakuspa/kanda/events?date=YYYY-MM-DD` | Function name `RakuSpaKandaEvents`; route `rakuspa/kanda/events`; HTTP GET only | `src/ToolWebGetSimpleFunctions.Functions/Functions/RakuSpaKandaEventsFunction.cs`: `[Function("RakuSpaKandaEvents")]` and `[HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "rakuspa/kanda/events")]` | TP-RC001-B, TP-RC001-D | Done | production entrypoint は存在する |
| RC-001 | Function trigger auth level | Function trigger is `AuthorizationLevel.Anonymous`; Easy Auth enforces auth at platform level | `AuthorizationLevel.Anonymous` selected; Function key primary auth prohibited | `RakuSpaKandaEventsFunction.cs` uses `AuthorizationLevel.Anonymous` | TP-RC001-A, TP-RC001-D | PartiallyDone | code side は contract と一致。Easy Auth platform config は repo 内では確認できない |
| RC-001 | Unauthenticated request behavior | Deployed unauthenticated request must not return event data; exact 302/401/403 depends on Easy Auth config | Easy Auth / Entra configuration is deployment/manual config contract | not found in repository source or plans as concrete Azure resource/IaC config | TP-RC001-A | ManualOnly | real Azure Function App の Authentication setting と Entra allowed users/groups の確認が必要 |
| RC-001 | Date query parsing | valid `date=YYYY-MM-DD` is accepted; invalid date returns 400 and should not reach source/AOAI | Function parses query date before lookup service call | `RakuSpaKandaEventsFunction.cs`: `ResolveSearchDate`, `DateOnly.TryParseExact`, `FormatException` catch returns `BadRequest` | TP-RC001-B, TP-RC001-E | Done | invalid date path は source/AOAI call 前に処理される |
| RC-001 | Omitted date uses configured timezone | date omitted uses configured `Application:TimeZone`, default `Asia/Tokyo` | `ApplicationOptions.TimeZone = Asia/Tokyo`; options binding in `Program.cs` | `ApplicationOptions.cs` defines default `Asia/Tokyo`; `RakuSpaKandaEventsFunction.cs` uses `TimeZoneInfo.FindSystemTimeZoneById`; `Program.cs` binds `ApplicationOptions` | TP-RC001-C | Done | invalid timezone handlingは 500 になるが、明示 validation は未実装 |
| RC-001 | 500 error behavior and exception logging | unexpected server/config/source/AOAI failure returns 500; all caught exceptions log `Exception.ToString()` | no silent fallback; caught exceptions log `Exception.ToString()` | `RakuSpaKandaEventsFunction.cs` logs `Unhandled error: {ExceptionText}` with `ex.ToString()` and returns InternalServerError | TP-CROSS-LOGGING | Done | source/AOAI lower layers also log; duplicate logging はあり得るが contract violation ではない |
| RC-002 | Official news URL / facility aliases | source collector uses configured official news URL, facility name, aliases | `SourceCollectionOptions` with official URL and aliases | `SourceCollectionOptions.cs` defines `OfficialNewsUrl`, `FacilityName`, `FacilityAliases`; `Program.cs` binds options | TP-RC002-A | Done | options default exists |
| RC-002 | Named source HttpClient and timeout | source fetch uses named source `HttpClient`; timeout uses `SourceCollection:HttpTimeoutSeconds` | `IHttpClientFactory` and named client `source` | `Program.cs` registers `AddHttpClient("source")` and sets timeout from `SourceCollectionOptions.HttpTimeoutSeconds`; `RakuSpaSourceCollector.cs` uses `CreateClient("source")` | TP-RC002-C, TP-RC002-E | Done | timeout test not present, but production wiring exists |
| RC-002 | HTML extraction adapter | use AngleSharp for visible text, links, image alt extraction; no raw image binary | `IHtmlTextExtractor` / `AngleSharpHtmlTextExtractor` selected | `AngleSharpHtmlTextExtractor.cs` uses `HtmlParser`, `TextContent`, `a[href]`, `img[alt]`; `.csproj` references AngleSharp 1.3.0 | TP-RC002-B, TP-RC002-D | Done | raw image bytes are not fetched by extractor |
| RC-002 | Candidate article and campaign link following | deterministic article/campaign/PR TIMES link following only when linked from official/candidate pages | `RakuSpaSourceCollector` responsibility | `RakuSpaSourceCollector.cs` fetches official page, filters candidate links, fetches article pages, checks signals, then follows campaign-like links containing `rakuspa.com`, `prtimes.jp`, or `campaign` | TP-RC002-A, TP-RC002-B, TP-RC002-D | Done | candidate link detection uses link URL rather than official page title/body; may be brittle but not direct contract mismatch in this pass |
| RC-002 | Text-only extraction input fields | include visible text, links, image alt, date-like strings, venue-like strings; no raw images | `ExtractionInput` carries documents and detected strings | `RakuSpaSourceCollector.cs` builds `ExtractionInput` with `Documents`, `DetectedDateLikeStrings`, `DetectedVenueLikeStrings`; `AngleSharpHtmlTextExtractor.cs` fills `ImageAltTexts` in `SourceDocument` | TP-RC002-B | Done | DateLikeRegex only matches numeric yyyy/mm/dd or yyyy-mm-dd; Japanese date notation coverage is not verified |
| RC-002 | Page size and count limits | `MaxPageBytes`, `MaxLinkedPagesPerRequest`, `MaxCandidateArticles` must bound source collection | explicit error or bounded non-silent behavior | `RakuSpaSourceCollector.cs` throws `InvalidOperationException` when bytes exceed `MaxPageBytes`; uses `.Take` for candidate and linked page limits | TP-RC002-E | PartiallyDone | max count truncation has no warning; test-design already marked exact over-limit behavior as PartiallyDone |
| RC-002 | Fatal source fetch error handling | official news / required candidate / campaign fetch failure is fatal, not fallback | no silent fallback; `Exception.ToString()` log | `RakuSpaSourceCollector.cs` uses `EnsureSuccessStatusCode`, catches, logs `Source collection failed: {ExceptionText}`, then rethrows | TP-RC002-C, TP-CROSS-LOGGING | Done | explicit error propagates to Function 500 via lookup/function catch |
| RC-002 | Prohibited substitutions | no general web search, Playwright, OCR, image binary / vision path | AngleSharp + deterministic link following only | `.csproj` package refs contain AngleSharp and Functions packages only; inspected source collector/extractor have no Playwright/OCR/vision path | TP-RC002-D | Done | repository-wide exhaustive dependency audit was not performed |
| RC-003 | AOAI REST endpoint / options | use AOAI REST Chat Completions endpoint with configured endpoint/deployment/api-version/api-key | REST API selected; SDK deferred; API key from app settings | `AzureOpenAIOptions.cs` defines endpoint/deployment/api-version/api-key; `AzureOpenAIRestExtractionClient.cs` builds `/openai/deployments/{deployment}/chat/completions?api-version=...` and sets `api-key` header | TP-RC003-A | Done | actual AOAI deployment not verified in this pass |
| RC-003 | Structured Outputs request | request must contain `response_format.type = json_schema`, `strict = true`, schema name `rakuspa_event_extraction_result` | REST `response_format.type = json_schema`; JSON mode rejected | `AzureOpenAIRestExtractionClient.cs` builds `response_format = { type = "json_schema", json_schema = { name = "rakuspa_event_extraction_result", strict = true, schema = BuildSchema() } }` | TP-RC003-A, TP-RC003-B | Done | schema path exists |
| RC-003 | Required candidate field `venueEvidence` | runtime contract lists candidate fields including `venueEvidence`; implementation-contract output DTO shape includes `venueEvidence` | output DTO shape in implementation-contract includes `venueEvidence` | not found. `ExtractedEventCandidate.cs` has no `VenueEvidence`; `AzureOpenAIRestExtractionClient.BuildSchema()` does not include `venueEvidence` in required/properties | TP-RC003-A, TP-RC003-C | NotImplementedOrMismatch | contract mismatch. Either contract must be revised or production DTO/schema must add `venueEvidence` |
| RC-003 | JSON mode / prompt-only JSON prohibition | no `response_format.type = json_object`; no schema-less extraction path | JSON mode is rejected substitute | inspected `AzureOpenAIRestExtractionClient.cs` uses `json_schema`; no `json_object` found in inspected AOAI client | TP-RC003-B | Done | repository-wide string search was limited by connector search behavior; inspected AOAI client is the production path |
| RC-003 | Malformed AOAI response / failure behavior | AOAI HTTP failure, empty/malformed response, schema mismatch are explicit errors, no fallback | no fallback; AOAI client logs Exception.ToString and rethrows | `AzureOpenAIRestExtractionClient.cs` uses `EnsureSuccessStatusCode`, parses `choices[0].message.content`, throws on empty/parse failure, catches/logs/rethrows | TP-RC003-F, TP-CROSS-LOGGING | Done | no retry policy observed, but retry is not required by selected contract |
| RC-003 | Candidate validation: date/source/venue/eventType/evidence | invalid date order, missing/untraceable source URL, unconfirmed venue, unsupported event type, empty evidence are rejected | `EventCandidateValidator` is production binding | `EventCandidateValidator.cs` rejects empty title, invalid date order, unconfirmed venue, untraceable sourceUrls, non-collaboration eventType, empty evidence snippets | TP-RC003-C | PartiallyDone | validates major fields, but not full title/date/venue/evidence traceability to input source text |
| RC-003 | Candidate validation: values traceable to candidate input | date/title/venue values must be traceable to source text/source URL/detected strings/aliases | implementation-contract requires traceability checks for date/title/venue/source/evidence | `EventCandidateValidator.cs` verifies only sourceUrls against input document URLs; it does not verify title/startDate/endDate/venue/evidence snippets against input documents, detected date strings, detected venue strings, or aliases | TP-RC003-C | NotImplementedOrMismatch | contract mismatch with deterministic post-LLM validation requirement |
| RC-003 | Production flow calls validator before selector | LLM output must not be returned directly; C# validation and selection after extraction | `RakuSpaEventLookupService` orchestrates source -> extraction -> validator -> selector | `RakuSpaEventLookupService.cs` calls `_validator.Validate(input, extraction.Candidates)` then `_selector.Select(searchDate, validCandidates)` | TP-RC003-C, TP-RC003-D | Done | correct flow order exists |
| RC-003 | Active / next event deterministic selection | active and next selected deterministically; overlap/same-start warnings | selector production implementation | `EventSelectionService.cs` sorts by start, end desc, title; warns for multiple active and same-start next candidates | TP-RC003-D | Done | no test artifact found, but production code exists |
| RC-003 | Busy phase classification | start, start+1, start+2 => `initial_peak`; inside after start+2 => `after_initial_peak`; before => `before_event`; event null => `no_event` | selector maps busy phase | `EventSelectionService.cs` returns `before_event`, `initial_peak`, `after_initial_peak`, `unknown`; null event is represented by null `EventDto`, not `EventDto.busyPhase = no_event` | TP-RC003-E | PartiallyDone | Plan allowed `no_event`, but response has null event when no event exists. This may be acceptable if null event is the no-event representation; confirm in downstream gap review if needed |
| RC-003 | No valid candidate result | no valid candidate is not exception; returns null active/next with warning/sourceSummary | lookup service adds no matching warning | `RakuSpaEventLookupService.cs` adds warning when `activeEvent` and `nextEvent` are null and returns `EventLookupResponse` | TP-RC003-D | Done | source/AOAI success path only |
| RC-003 | Stable response DTO | response includes `facility`, `searchDate`, `generatedAt`, `activeEvent`, `nextEvent`, `warnings`, `sourceSummary` | API response DTO stable | `RakuSpaEventLookupService.cs` populates these fields in `EventLookupResponse`; function writes JSON | TP-RC001-B, TP-RC003-D | Done | DTO source file was not separately inspected, but construction evidence exists |

## Stub-to-Production Binding

| Test Point ID | Stub / fake / in-memory used in test | Implementation contract decision | Production interface | Production concrete implementation | Production wiring / entrypoint | Status | Remaining work |
| --- | --- | --- | --- | --- | --- | --- | --- |
| none | No test artifacts were found in this pass; substitute usage could not be confirmed from tests | Test design expects fake/stub/mock for many CI-safe test points | not applicable in this pass | not applicable in this pass | not applicable in this pass | NotImplementedOrMismatch | Create tests first, then re-run verification to bind each substitute to production interface + concrete implementation + wiring |

## Test observations

| Test Point ID | Runtime Contract ID | Test artifact / Manual-only reason | Substitute used? | Expected observation | Actual observation / status | Notes |
| --- | --- | --- | --- | --- | --- | --- |
| TP-RC001-A | RC-001 | Manual-only: real Easy Auth / Entra environment required | No | unauthenticated request returns 302 sign-in redirect or 401/403 and no event response body | manual-only; not run in this pass | repo has no deployed Easy Auth evidence |
| TP-RC001-B | RC-001 | missing | to be determined | authenticated caller receives HTTP 200 JSON with stable response fields | missing test; production endpoint and response construction partially inspected | create function/API response shape test |
| TP-RC001-C | RC-001 | missing | to be determined | omitted date uses fake `IClock` and `Asia/Tokyo` timezone to set `searchDate` | missing test; production code exists | create date defaulting test |
| TP-RC001-D | RC-001 | missing | No | route `rakuspa/kanda/events`, GET only, authLevel `Anonymous` | missing test; production metadata inspected and matches | create metadata/static test or verification script |
| TP-RC001-E | RC-001 | missing | to be determined | invalid date returns 400 and source/AOAI not called | missing test; production code returns 400 before service call | create invalid date test |
| TP-RC002-A | RC-002 | missing | to be determined | fixture official news HTML produces candidate article URL/title/text in `ExtractionInput` | missing test; production collector exists | create fake HTTP handler fixture test |
| TP-RC002-B | RC-002 | missing | to be determined | linked campaign page visible text and image alt included; raw image bytes not included | missing test; extractor/collector production code exists | create fixture article/campaign test |
| TP-RC002-C | RC-002 | missing | to be determined | required fetch failure uses explicit error path and no lower-confidence answer | missing test; production collector logs/rethrows | create fake HTTP failure test |
| TP-RC002-D | RC-002 | missing | to be determined | no general web search / Playwright / image binary path exists | missing test; inspected csproj/source path shows no such dependency/path | create static dependency/path guard test if desired |
| TP-RC002-E | RC-002 | missing | to be determined | timeout/page-size/count limits produce explicit error or bounded non-silent behavior | missing test; max page error and count bounding exist, but exact warning behavior remains partial | implementation-time clarification still needed |
| TP-RC003-A | RC-003 | missing | to be determined | AOAI request contains `response_format.type=json_schema`, strict schema, required fields, `additionalProperties:false` | missing test; production request builder inspected; mismatch found for missing `venueEvidence` | blocking contract mismatch |
| TP-RC003-B | RC-003 | missing | to be determined | no `json_object` JSON mode or schema-less extraction path | missing test; production AOAI client uses `json_schema` | create request builder/static test |
| TP-RC003-C | RC-003 | missing | to be determined | invalid/untraceable candidate is rejected and warning/error recorded | missing test; validator partially implements rejection; traceability mismatch found | blocking contract mismatch for traceability |
| TP-RC003-D | RC-003 | missing | to be determined | active/next/none/overlap/same-start deterministic selection | missing test; production selector exists | create selector fixture tests |
| TP-RC003-E | RC-003 | missing | to be determined | start/start+1/start+2 initial_peak; start+3 after_initial_peak; before_event; no_event | missing test; production selector implements most cases; no_event represented by null event | create busy phase fixture tests and decide null/no_event representation |
| TP-RC003-F | RC-003 | missing | to be determined | AOAI HTTP error/timeout/malformed/schema mismatch uses explicit error and no fallback | missing test; production AOAI client logs/rethrows on many failures | create fake AOAI error tests |
| TP-CROSS-LOGGING | RC-001 / RC-002 / RC-003 | missing | to be determined | exception paths log `Exception.ToString()` and do not log secrets/tokens/raw oversized prompt | missing test; inspected catch paths include `ex.ToString()` in function/source/AOAI/lookup | create fake logger tests; secret redaction still needs test evidence |

## Unresolved items

| ID | Type | Why unresolved | Recommended next agent | Target files / addresses |
| --- | --- | --- | --- | --- |
| VK-GAP-001 | contract-mismatch | `venueEvidence` is required by runtime/implementation contract candidate fields, but production DTO and AOAI JSON schema do not include it | `coverage-gap-resolution-slice.agent.md` | `src/ToolWebGetSimpleFunctions.Functions/Models/Extraction/ExtractedEventCandidate.cs`; `src/ToolWebGetSimpleFunctions.Functions/Services/AzureOpenAIRestExtractionClient.cs`; possibly update contract if `venueEvidence` is intentionally removed |
| VK-GAP-002 | contract-mismatch | deterministic validation requires title/date/venue/evidence values to be traceable to input source text/detected strings/aliases, but production validator only checks source URL traceability plus basic candidate validity | `coverage-gap-resolution-slice.agent.md` | `src/ToolWebGetSimpleFunctions.Functions/Services/EventCandidateValidator.cs`; `src/ToolWebGetSimpleFunctions.Functions/Models/Source/ExtractionInput.cs` |
| VK-GAP-003 | missing-test | No test artifacts were found for selected test points; test design exists but tests are not implemented or executed | `coverage-gap-resolution-slice.agent.md` or implementation phase | `tests/ToolWebGetSimpleFunctions.Functions.Tests/` |
| VK-GAP-004 | manual-only | Easy Auth / Entra real deployed behavior, allowed users/groups/app roles, unauthenticated action, and mobile browser sign-in flow cannot be verified from repository code alone | human review / deployment verification | Azure Function App Authentication settings; Entra app/users/groups/app roles |
| VK-GAP-005 | human-decision-needed | Azure Functions hosting plan, AOAI deployment/model/region/credential strategy, exact NuGet update policy, Application Insights setup remain environment decisions | human review | Azure resource configuration; deployment pipeline/IaC if added later |
| VK-GAP-006 | human-decision-needed | Busy phase `no_event` representation is ambiguous: runtime/test design names `no_event`, while API response represents no event as null `activeEvent` / `nextEvent` rather than `EventDto.busyPhase=no_event` | `coverage-gap-triage` or human review | `src/ToolWebGetSimpleFunctions.Functions/Services/EventSelectionService.cs`; `src/ToolWebGetSimpleFunctions.Functions/Models/Api/EventDto.cs`; Plan/runtime contract if representation is intentionally null |
| VK-GAP-007 | manual-only | Live RAKU SPA page shape drift and real AOAI Structured Outputs acceptance were not verified in this pass | manual verification after deployment/config | RAKU SPA official pages; AOAI deployment |

## Verdict

`BLOCKED_BY_CONTRACT_MISMATCH`

RC-003 で、runtime / implementation contract が要求する `venueEvidence` が production DTO と AOAI schema に存在せず、さらに validator が title/date/venue/evidence の入力由来 traceability を検証していない。これは production binding gap より優先される contract mismatch である。加えて selected test points は未実装であり、Easy Auth / AOAI / live source には manual-only 残件がある。

## Handoff Packet

- Profile used: `contract-kernel`
- Source artifacts:
  - `AGENTS.md`
  - `plans/rakuspa-kanda-event-api.md`
  - `plans/rakuspa-kanda-event-api-change-risk-triage.md`
  - `plans/rakuspa-kanda-event-api-implementation-contract-kernel.md`
  - `plans/rakuspa-kanda-event-api-implementation-contract-review-kernel.md`
  - `plans/rakuspa-kanda-event-api-runtime-contract-kernel.md`
  - `plans/rakuspa-kanda-event-api-test-design-kernel.md`
  - `https://github.com/suusanex/coding_agent_plan_and_verify_process/blob/main/.github/agents/verification-kernel.agent.md`
- Selected contracts / IDs:
  - `RC-001`: Easy Auth protected HTTP access
  - `RC-002`: Public source collection
  - `RC-003`: Azure OpenAI structured extraction and validation
- Selected test point IDs:
  - `TP-RC001-A`
  - `TP-RC001-B`
  - `TP-RC001-C`
  - `TP-RC001-D`
  - `TP-RC001-E`
  - `TP-RC002-A`
  - `TP-RC002-B`
  - `TP-RC002-C`
  - `TP-RC002-D`
  - `TP-RC002-E`
  - `TP-RC003-A`
  - `TP-RC003-B`
  - `TP-RC003-C`
  - `TP-RC003-D`
  - `TP-RC003-E`
  - `TP-RC003-F`
  - `TP-CROSS-LOGGING`
- Files inspected:
  - `AGENTS.md`
  - `plans/rakuspa-kanda-event-api.md`
  - `plans/rakuspa-kanda-event-api-change-risk-triage.md`
  - `plans/rakuspa-kanda-event-api-implementation-contract-kernel.md`
  - `plans/rakuspa-kanda-event-api-implementation-contract-review-kernel.md`
  - `plans/rakuspa-kanda-event-api-runtime-contract-kernel.md`
  - `plans/rakuspa-kanda-event-api-test-design-kernel.md`
  - `.github/agents/verification-kernel.agent.md` from process repository
  - `src/ToolWebGetSimpleFunctions.Functions/ToolWebGetSimpleFunctions.Functions.csproj`
  - `src/ToolWebGetSimpleFunctions.Functions/Program.cs`
  - `src/ToolWebGetSimpleFunctions.Functions/Functions/RakuSpaKandaEventsFunction.cs`
  - `src/ToolWebGetSimpleFunctions.Functions/Options/ApplicationOptions.cs`
  - `src/ToolWebGetSimpleFunctions.Functions/Options/SourceCollectionOptions.cs`
  - `src/ToolWebGetSimpleFunctions.Functions/Options/AzureOpenAIOptions.cs`
  - `src/ToolWebGetSimpleFunctions.Functions/Services/RakuSpaEventLookupService.cs`
  - `src/ToolWebGetSimpleFunctions.Functions/Services/RakuSpaSourceCollector.cs`
  - `src/ToolWebGetSimpleFunctions.Functions/Services/AngleSharpHtmlTextExtractor.cs`
  - `src/ToolWebGetSimpleFunctions.Functions/Services/AzureOpenAIRestExtractionClient.cs`
  - `src/ToolWebGetSimpleFunctions.Functions/Services/EventCandidateValidator.cs`
  - `src/ToolWebGetSimpleFunctions.Functions/Services/EventSelectionService.cs`
  - `src/ToolWebGetSimpleFunctions.Functions/Models/Extraction/ExtractedEventCandidate.cs`
- Files intentionally not inspected:
  - unrelated production files outside selected contracts: selected scope 外へ広げないため
  - Azure resource / IaC files: selected source artifacts and inspected pathsから存在確認できず、Easy Auth/AOAI real environment は ManualOnly / NeedsHumanDecision として扱うため
  - live RAKU SPA pages: verification-kernel は repository state と selected test points の検証が主目的であり、live source drift は manual-only として扱うため
  - real AOAI deployment: credentials/environment が未提供であり、CI-safe policy に反するため
- Decisions made:
  - Tests were not executed in this pass.
  - Test artifacts for selected test points were treated as missing because repository search did not find corresponding RAKU SPA test files and no test file path was provided by Test Design Kernel.
  - Production entrypoint, DI wiring, source collector, AOAI client, validator, selector, and key options exist and partially satisfy RC-001 / RC-002 / RC-003.
  - RC-003 has blocking contract mismatch for missing `venueEvidence` and insufficient deterministic traceability validation.
  - Easy Auth exact behavior is ManualOnly / NeedsHumanDecision, not automatically passable from code inspection.
  - No `Bound` status was assigned because no substitute-using test artifact exists and production binding cannot be matched to a concrete test substitute.
- Do not redo unless new evidence appears:
  - Do not mark this verification as pass while `venueEvidence` contract mismatch remains unresolved.
  - Do not treat fake/stub test design rows as implemented tests.
  - Do not treat code-side `AuthorizationLevel.Anonymous` as proof that Easy Auth is configured in Azure.
  - Do not treat source URL traceability alone as full deterministic traceability for title/date/venue/evidence.
  - Do not replace Structured Outputs with JSON mode or prompt-only JSON to bypass schema mismatch.
- Remaining work:
  - `contract-mismatch`: add or explicitly remove/revise `venueEvidence` across contract, DTO, schema, and validator.
  - `contract-mismatch`: implement deterministic traceability checks for title/date/venue/evidence or revise contract if intentionally out of scope.
  - `missing-test`: create test artifacts for all selected non-manual test points under `tests/ToolWebGetSimpleFunctions.Functions.Tests/` or chosen repository convention.
  - `manual-only`: verify Easy Auth / Entra deployment behavior, allowed users/groups/app roles, mobile sign-in flow.
  - `manual-only`: verify real AOAI deployment/model/API version accepts selected Structured Outputs request.
  - `manual-only`: verify live RAKU SPA source page shape after implementation.
  - `human-decision-needed`: decide hosting plan, deployment config, AOAI deployment/model/region, Application Insights setup, exact `no_event` representation.
- Recommended next step:
  - Run `coverage-gap-triage.agent.md` with this verification artifact and target gaps `VK-GAP-001`, `VK-GAP-002`, and `VK-GAP-003`.
  - For an immediate bounded fix slice, prioritize `VK-GAP-001` and `VK-GAP-002` before writing broad tests, because current verdict is blocked by contract mismatch.
