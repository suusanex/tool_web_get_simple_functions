# Implementation Contract Kernel

## Scope

この成果物は、`rakuspa-kanda-event-api.md` と `rakuspa-kanda-event-api-change-risk-triage.md` を入力として、RAKU SPA 1010 神田コラボイベント確認 API の実装前契約を定義する。

対象は、新規作成の .NET isolated worker Azure Functions アプリケーションである。改修前のソースコードは存在しない前提とする。

この pass では、production code と tests は作成しない。Plan が要求する実装経路、依存関係、API surface、configuration、DI/startup wiring、production address、未解決 implementation-realization item を明示して停止する。

Selected runtime contracts from change-risk-triage:

- `RC-001`: Easy Auth protected HTTP access
- `RC-002`: Public source collection
- `RC-003`: Azure OpenAI structured extraction and validation

Out of scope for this pass:

- LLM 画像分析
- Playwright / ブラウザ自動化
- OCR
- 複数施設対応
- Native Android app
- Push / LINE / Teams / email notification
- Durable Functions
- Timer trigger / cache warm-up / storage persistence
- Azure リソース作成手順の詳細化
- production code / test code の作成

## Plan-named implementation requirements

| Requirement | Expected by Plan | Evidence found | Status |
| --- | --- | --- | --- |
| .NET isolated worker Azure Functions | 1つの認証付き HTTP endpoint を持つ .NET isolated worker Azure Functions app | Microsoft Learn は isolated worker model が .NET version を Functions runtime から独立して target でき、Project structure として `.csproj`、`Program.cs`、function code、`host.json`、`local.settings.json` を示す | Confirmed |
| HTTP-triggered API | Mobile/browser から呼べる serverless HTTP API | Microsoft Learn は HTTP trigger を HTTP request で function を呼び出す serverless API / webhook 用途として説明している | Confirmed |
| Easy Auth / Microsoft Entra sign-in | 未認証 caller に event data を返さず、Microsoft Entra sign-in で保護する | Microsoft Learn は App Service / Azure Functions の built-in authentication / authorization、いわゆる Easy Auth を説明している | Confirmed |
| Function trigger authLevel | Easy Auth と組み合わせる Function trigger authorization level を決める | Plan と triage は未確定としている。implementation contract では `Anonymous` を選択し、authentication enforcement は Easy Auth 側に置く | Confirmed |
| Application timezone | date omitted 時に server-side current date を使用する。想定 default は Asia/Tokyo | Plan は timezone を configuration 化する要求を持つ。implementation contract では `Application:TimeZone = Asia/Tokyo` を既定値とする | Confirmed |
| RAKU SPA 1010 神田 fixed facility | RAKU SPA 1010 神田のみを対象にし、aliases を認識する | Plan に対象施設と alias list が定義済み | Confirmed |
| Public source collection | RAKU SPA 1010 神田 official news を primary source とし、article/campaign page を辿る | Plan に source collection requirement が定義済み。HTML extraction library は本 contract で AngleSharp を選択する | Confirmed |
| Supplemental PR TIMES source | 公式ページから発見可能、または決定論的検索/候補抽出で発見可能な場合に補足使用 | Search API や外部検索 provider は Plan scope にないため、本 pass では official/campaign page 内リンクまたは候補 page 内明示 URL のみを辿る | Confirmed |
| Text-only LLM input | HTML visible text、links、image alt、date-like strings、venue-like strings を LLM に渡す。raw image は送信しない | Plan に明記済み。implementation contract では `ExtractionInputDto` に集約する | Confirmed |
| Azure OpenAI Structured Outputs | Plain JSON mode ではなく schema-constrained Structured Outputs を優先 | Azure OpenAI REST API reference は `response_format` に `{ "type": "json_schema", "json_schema": {...} }` を指定すると Structured Outputs になると説明している | Confirmed |
| JSON mode substitute | JSON mode は valid JSON を保証するが schema match を保証しない | Microsoft Learn は schema guarantees が必要なら Structured Outputs を使うよう説明している | RejectedSubstitute |
| Deterministic post-LLM validation | invalid dates、missing source URL、unconfirmed venue、unsupported values を reject/downgrade する | Plan に validation requirements が定義済み。implementation contract では `EventCandidateValidator` を production binding に含める | Confirmed |
| Active/next event selection | search date に対する `activeEvent` と `nextEvent` を deterministic に選択する | Plan に selection rule が定義済み。implementation contract では `EventSelectionService` を production binding に含める | Confirmed |
| Busy phase classification | start date から 3 日間を `initial_peak` とする | Plan に classification requirement と accepted enum が定義済み | Confirmed |
| Stable JSON response | `facility`、`searchDate`、`generatedAt`、`activeEvent`、`nextEvent`、`warnings`、`sourceSummary` を常に返す | Plan に response contract が定義済み。implementation contract では response DTO を固定する | Confirmed |
| Failure handling | 原則フォールバックせず、処理失敗を error/exception として返す | `Instructions.txt` に明示あり。source fetch / AOAI extraction / validation failure で silent fallback を禁止する | Confirmed |
| Exception logging | すべての例外で `Exception.ToString()` を trace log に出力する | `Instructions.txt` に明示あり。catch-and-wrap / catch-and-return の場合も logging requirement とする | Confirmed |
| UnitTest / CI IntegrationTest isolation | 実 OS 環境を変更せず、外部環境は stub/fake/mock を使う | `Instructions.txt` に明示あり。Easy Auth、external Web、AOAI は production と substitute の境界を明示する | Confirmed |
| Reflection prohibition | 原則 reflection を使用しない | `Instructions.txt` に明示あり。Structured Outputs schema は hand-written schema または BCL-supported generation を使う場合も reflection usage を明示する | Confirmed |
| OSS/BCL/framework reuse before custom implementation | BCL、framework API、NuGet OSS の採用を優先検討する | `Instructions.txt` に明示あり。HTML parser は AngleSharp を採用し、独自 HTML parser は禁止する | Confirmed |

## Dependency and API surface findings

| Dependency / API / symbol | Expected source | Found location | Status | Notes |
| --- | --- | --- | --- | --- |
| Azure Functions runtime | Azure Functions 4.x | Microsoft Learn isolated worker support table | Confirmed | Project target は `net10.0` を選択する。ただし Linux Consumption plan では .NET 10 が使えない caveat があるため hosting plan は NeedsHumanDecision として残す |
| `Microsoft.Azure.Functions.Worker` | NuGet / Azure Functions isolated worker | Microsoft Learn core packages | Confirmed | `net10.0` の場合、doc 上は `2.50.0` 以降が必要 |
| `Microsoft.Azure.Functions.Worker.Sdk` | NuGet / Azure Functions isolated worker | Microsoft Learn core packages | Confirmed | `net10.0` の場合、doc 上は `2.0.5` 以降が必要 |
| `Microsoft.Azure.Functions.Worker.Extensions.Http` | NuGet / Azure Functions HTTP trigger extension | Azure Functions HTTP trigger binding | Confirmed | HTTP trigger function に必要。version は implementation 時点で最新 stable を採用する |
| Function entrypoint | `src/ToolWebGetSimpleFunctions.Functions/Functions/RakuSpaKandaEventsFunction.cs` | 新規作成のため repository source には未存在 | MissingButRequired | この production address を実装時に作成する |
| App startup | `src/ToolWebGetSimpleFunctions.Functions/Program.cs` | 新規作成のため repository source には未存在 | MissingButRequired | DI、Options、HttpClient registration、logging setup の production address |
| Project file | `src/ToolWebGetSimpleFunctions.Functions/ToolWebGetSimpleFunctions.Functions.csproj` | 新規作成のため repository source には未存在 | MissingButRequired | Functions app project の production address |
| `host.json` | `src/ToolWebGetSimpleFunctions.Functions/host.json` | 新規作成のため repository source には未存在 | MissingButRequired | Functions host configuration |
| `local.settings.sample.json` | `src/ToolWebGetSimpleFunctions.Functions/local.settings.sample.json` | local template として repository に配置し、実運用の `local.settings.json` は各開発環境でコピーして作成する | Confirmed | local secrets placeholder。実ファイルの `local.settings.json` は `.gitignore` で commit 対象外 |
| Easy Auth Microsoft provider | Azure App Service / Functions Authentication | Microsoft Learn provider configuration | Confirmed | Azure resource configuration。code artifact ではなく deployment/manual config contract |
| Function trigger `AuthorizationLevel.Anonymous` | Function code attribute | Easy Auth protected endpoint contract | Confirmed | Function key をモバイル URL に含めない。platform 側で unauthenticated request を reject/redirect する |
| Easy Auth unauthenticated action | App Service Authentication setting | Azure Portal / IaC / manual config | NeedsHumanDecision | Contract: deployed environment では unauthenticated request を allow しない。具体的な 401/302 は runtime-contract/test-design で扱う |
| Allowed users/groups/app roles | Microsoft Entra / App Service auth | Azure Portal / Entra configuration | NeedsHumanDecision | 個人利用なら tenant user 限定、組織利用なら group/app role。ここでは implementation code に埋め込まない |
| `X-MS-CLIENT-PRINCIPAL` header | Easy Auth injected request header | Easy Auth platform behavior | ApiSurfaceUnknown | Code は認証判定に直接依存しない。ただし diagnostics と verification hook として presence check を optional にする |
| `System.Net.Http.IHttpClientFactory` | BCL / Microsoft.Extensions.Http | .NET framework API | Confirmed | Source fetch と AOAI REST call で named clients を使う。HttpClient new 直書きは禁止 |
| AngleSharp | NuGet OSS | NuGet: AngleSharp parses HTML5/CSS/XML into DOM and supports query selection | Confirmed | HTML visible text / link / img alt extraction に採用。独自 HTML parser と Playwright は使用しない |
| HtmlAgilityPack | NuGet OSS | NuGet: tolerant real-world malformed HTML parser | RejectedSubstitute | AngleSharp を選択するため採用しない。XPath 中心ではなく CSS selector/DOM traversal を使う方針 |
| Azure OpenAI REST Chat Completions | Azure OpenAI data plane inference API | Microsoft Learn REST reference | Confirmed | `POST {endpoint}/openai/deployments/{deployment-id}/chat/completions?api-version=2024-10-21` を MVP の production API surface とする |
| `response_format.type = json_schema` | Azure OpenAI REST API | Microsoft Learn REST reference | Confirmed | Structured Outputs を REST で直接指定する。SDK surface uncertainty を避ける |
| `response_format.type = json_object` | Azure OpenAI REST API | Microsoft Learn JSON mode | RejectedSubstitute | valid JSON だけで schema match を保証しないため Plan requirement を満たさない |
| Azure.AI.OpenAI SDK | Azure SDK for .NET | Current public docs / samples exist, but exact selected version 未確定 | ApiSurfaceUnknown | MVP では採用しない。後続で SDK に切り替える場合は別 implementation contract update が必要 |
| Azure OpenAI API key | Azure OpenAI REST request header | Microsoft Learn REST reference shows `api-key` request header | Confirmed | MVP credential strategy として app setting から供給する。managed identity は Deferred |
| Managed identity / keyless AOAI access | Azure Identity / RBAC | Plan says preferred if environment supports | NeedsHumanDecision | MVP では Deferred。採用するなら endpoint auth と RBAC の separate contract が必要 |
| System.Text.Json | BCL | .NET BCL | Confirmed | DTO serialization/deserialization、manual JSON Schema document construction に使用する |
| TimeZoneInfo | BCL | .NET BCL | Confirmed | `Application:TimeZone` を `Asia/Tokyo` default で読む。timezone missing/invalid は startup validation error |
| Application Insights | Azure Functions monitoring/logging | Hosting setup dependent | NeedsHumanDecision | Logging sink は hosting/deployment 側で決定。code は ILogger に出す |

## Selected implementation approach

### 1. Repository and project layout

新規作成のため、対象 repository `https://github.com/suusanex/tool_web_get_simple_functions` に次の structure を作成する契約とする。

```text
src/
  ToolWebGetSimpleFunctions.Functions/
    ToolWebGetSimpleFunctions.Functions.csproj
    Program.cs
    host.json
    local.settings.sample.json       # committed template without secrets
    local.settings.json              # local only / gitignored
    Functions/
      RakuSpaKandaEventsFunction.cs
    Options/
      ApplicationOptions.cs
      SourceCollectionOptions.cs
      AzureOpenAIOptions.cs
    Models/
      Api/
        EventLookupResponse.cs
        EventDto.cs
        SourceSummaryDto.cs
      Source/
        SourceDocument.cs
        CandidateArticle.cs
        ExtractionInput.cs
      Extraction/
        ExtractedEventCandidate.cs
        ExtractionResult.cs
    Services/
      IRakuSpaEventLookupService.cs
      RakuSpaEventLookupService.cs
      ISourceCollector.cs
      RakuSpaSourceCollector.cs
      IHtmlTextExtractor.cs
      AngleSharpHtmlTextExtractor.cs
      IAzureOpenAIExtractionClient.cs
      AzureOpenAIRestExtractionClient.cs
      IEventCandidateValidator.cs
      EventCandidateValidator.cs
      IEventSelectionService.cs
      EventSelectionService.cs
      IClock.cs
      SystemClock.cs
```

Test project path is not created in this pass, but downstream test-design should assume the following target path unless repository convention changes:

```text
tests/
  ToolWebGetSimpleFunctions.Functions.Tests/
```

### 2. Runtime endpoint contract

Production endpoint:

```text
GET /api/rakuspa/kanda/events?date=YYYY-MM-DD
```

Function name:

```text
RakuSpaKandaEvents
```

Function trigger:

```text
HTTP GET only
AuthorizationLevel.Anonymous
```

Authentication contract:

- Function trigger は `Anonymous` とする。
- Deployed environment では Easy Auth / Microsoft Entra を必須化し、未認証 request を event function まで到達させない。
- Function key を mobile/browser URL に含める設計は禁止する。
- Function code は application-level authorization policy を独自実装しない。allowed users/groups/app roles は Easy Auth / Entra configuration 側の contract として扱う。
- Local development では Easy Auth が存在しないため anonymous access になり得る。この状態を production とみなしてはならない。

### 3. Configuration contract

Configuration sections:

```text
Application:TimeZone = Asia/Tokyo
SourceCollection:OfficialNewsUrl = https://rakuspa.com/kanda/news/
SourceCollection:FacilityName = RAKU SPA 1010 神田
SourceCollection:FacilityAliases = RAKU SPA 1010 神田;らくスパ 1010 神田;らくスパ1010神田;RAKU SPA 1010 KANDA
SourceCollection:HttpTimeoutSeconds = 20
SourceCollection:MaxPageBytes = 1048576
SourceCollection:MaxLinkedPagesPerRequest = 8
SourceCollection:MaxCandidateArticles = 20
AzureOpenAI:Endpoint = https://<resource>.openai.azure.com
AzureOpenAI:DeploymentName = <deployment-name>
AzureOpenAI:ApiVersion = 2024-10-21
AzureOpenAI:ApiKey = <secret>
AzureOpenAI:MaxInputCharacters = 80000
AzureOpenAI:Temperature = 0
AzureOpenAI:MaxTokens = 2000
```

Startup validation:

- missing required config は startup or request-time configuration error とする。
- invalid timezone は error とする。
- missing AOAI endpoint/deployment/api key は AOAI extraction error とする。
- default fallback to another model / another endpoint / unauthenticated source is prohibited.

### 4. Source collection contract

`RakuSpaSourceCollector` responsibilities:

1. Fetch `SourceCollection:OfficialNewsUrl`.
2. Parse visible text and links with AngleSharp.
3. Identify candidate articles by title/body signals:
   - `コラボ`
   - `開催決定`
   - `スペシャルイベント`
   - `×極楽湯`
   - `×RAKU SPA`
4. Follow article links found in official news page.
5. Follow campaign/special page links found inside candidate articles.
6. Include image `alt` text but never image binaries.
7. Include deterministic `detectedDateLikeStrings` and `detectedVenueLikeStrings` using regex/string scanning.
8. Build one `ExtractionInput` object for AOAI.

Supplemental PR TIMES contract:

- Do not use general web search in this pass.
- Follow PR TIMES or press release URLs only when they are present in official/campaign/candidate pages.
- If no supplemental PR TIMES URL is linked, proceed with official/campaign source set only.
- This is not a fallback. It is deterministic link-following within source collection scope.

Failure contract:

- Official news page fetch failure is fatal for the request.
- Candidate article fetch failure is fatal when the article was selected as candidate and is required to build input.
- Campaign page fetch failure is fatal when linked from a selected candidate and required to confirm event details.
- Partial source success must not silently produce a lower-confidence answer unless explicitly represented as a warning from validation over complete required source set.
- All caught exceptions must be logged with `Exception.ToString()`.

### 5. Azure OpenAI extraction contract

Production API surface:

```text
POST {AzureOpenAI:Endpoint}/openai/deployments/{AzureOpenAI:DeploymentName}/chat/completions?api-version={AzureOpenAI:ApiVersion}
Header: api-key: {AzureOpenAI:ApiKey}
Content-Type: application/json
```

Request must include:

```json
{
  "temperature": 0,
  "response_format": {
    "type": "json_schema",
    "json_schema": {
      "name": "rakuspa_event_extraction_result",
      "strict": true,
      "schema": { }
    }
  },
  "messages": [ ]
}
```

Schema contract:

- root type is `object`
- root is not `anyOf`
- all fields are listed in `required`
- every object has `additionalProperties: false`
- schema depth must remain within Azure OpenAI Structured Outputs limits
- optional semantic values use nullable unions only if supported by the chosen API/schema subset; otherwise use explicit enum/string sentinels such as `unknown`

Output DTO shape:

```text
ExtractionResult
  candidates: ExtractedEventCandidate[]
  warnings: string[]

ExtractedEventCandidate
  title: string
  startDate: string
  endDate: string
  venueConfirmed: boolean
  venueEvidence: string
  eventType: string
  sourceUrls: string[]
  evidenceSnippets: string[]
  confidence: high | medium | low
  warnings: string[]
```

The model must not return `activeEvent` or `nextEvent` directly. Selection is deterministic C# logic after validation.

### 6. Validation and selection contract

`EventCandidateValidator` rejects candidates when:

- `title` is empty
- `startDate` or `endDate` is not `YYYY-MM-DD`
- `startDate > endDate`
- `venueConfirmed` is false
- `sourceUrls` is empty
- `sourceUrls` contains URL not present in `ExtractionInput`
- `eventType` is not a collaboration/special campaign equivalent
- `evidenceSnippets` are empty
- date/title/venue values cannot be traced to source text, source URL, detected date strings, detected venue strings, or accepted facility aliases

`EventSelectionService` rules:

- `activeEvent`: confirmed candidate where `startDate <= searchDate <= endDate`; if multiple, choose deterministic ordering by earliest `startDate`, then latest `endDate`, then lexicographic `title`, and emit warning.
- `nextEvent`: confirmed candidate where `startDate > searchDate`, ordered by earliest `startDate`, then latest `endDate`, then lexicographic `title`; if multiple same start date, emit warning.
- No valid candidate is not an exception. It returns `activeEvent = null`, `nextEvent = null`, and warning/sourceSummary.
- Validation failure for malformed model output is an extraction/validation error, not a fallback to raw model text.

`BusyPhase` rules:

- event null: `no_event`
- search date before start: `before_event`
- search date between start and start+2 inclusive: `initial_peak`
- search date inside event after start+2: `after_initial_peak`
- otherwise: `unknown`

### 7. API response contract

Response DTO:

```text
EventLookupResponse
  facility: string
  searchDate: string
  generatedAt: string
  activeEvent: EventDto | null
  nextEvent: EventDto | null
  warnings: string[]
  sourceSummary: SourceSummaryDto

EventDto
  title: string
  startDate: string
  endDate: string
  initialPeakDates: string[]
  busyPhase: initial_peak | after_initial_peak | before_event | no_event | unknown
  confidence: high | medium | low
  sourceUrls: string[]

SourceSummaryDto
  officialNewsUrl: string
  fetchedUrls: string[]
  candidateArticleCount: integer
  extractionCandidateCount: integer
  validatedCandidateCount: integer
```

HTTP status contract:

- 200: request processed successfully, even if no matching event exists.
- 400: invalid query parameter, such as invalid date format.
- 401/302: Easy Auth platform behavior for unauthenticated request in deployed environment.
- 500: unexpected server error, configuration error, source collection fatal error, AOAI fatal error, malformed Structured Outputs response, or validation infrastructure error.

### 8. Error and logging contract

- Logs must be English.
- Documentation and issue/commit text must be Japanese.
- All caught exceptions must log `Exception.ToString()` to trace logs.
- Logs must include correlation ID, search date, phase, fetched URL count, candidate count, AOAI call success/failure, validation warning count, selected active/next status.
- Logs must not include `AzureOpenAI:ApiKey`, Easy Auth token, Cookie, Authorization header, full `X-MS-CLIENT-PRINCIPAL` payload, or raw AOAI prompt when it may contain excessive source text.

## Required code changes

Because this is a new application, all production addresses are missing and must be created by implementation.

| Area | Required file / address | Required change | Status |
| --- | --- | --- | --- |
| Functions project | `src/ToolWebGetSimpleFunctions.Functions/ToolWebGetSimpleFunctions.Functions.csproj` | Create .NET isolated worker Functions project targeting `net10.0`; reference Functions worker packages, HTTP extension, AngleSharp, and required Microsoft.Extensions packages | MissingButRequired |
| Startup / DI | `src/ToolWebGetSimpleFunctions.Functions/Program.cs` | Configure Functions worker, Options binding/validation, named HttpClients, services, logging | MissingButRequired |
| HTTP function | `src/ToolWebGetSimpleFunctions.Functions/Functions/RakuSpaKandaEventsFunction.cs` | Create GET endpoint route `rakuspa/kanda/events`; parse `date`; call lookup service; return JSON response; no business logic in function body | MissingButRequired |
| Options | `Options/*.cs` | Define strongly typed options for Application, SourceCollection, AzureOpenAI | MissingButRequired |
| Source models | `Models/Source/*.cs` | Define `SourceDocument`, `CandidateArticle`, `ExtractionInput` | MissingButRequired |
| Extraction models | `Models/Extraction/*.cs` | Define `ExtractionResult`, `ExtractedEventCandidate`; align with JSON schema | MissingButRequired |
| API models | `Models/Api/*.cs` | Define stable response DTOs | MissingButRequired |
| Source collection | `Services/RakuSpaSourceCollector.cs` | Implement deterministic HTTP fetch/link follow/text extraction flow | MissingButRequired |
| HTML extraction | `Services/AngleSharpHtmlTextExtractor.cs` | Implement visible text/link/img alt extraction using AngleSharp | MissingButRequired |
| AOAI REST client | `Services/AzureOpenAIRestExtractionClient.cs` | Implement REST chat completions call with `response_format.type = json_schema` | MissingButRequired |
| Validation | `Services/EventCandidateValidator.cs` | Implement deterministic validation and traceability checks | MissingButRequired |
| Selection | `Services/EventSelectionService.cs` | Implement active/next selection and busy phase classification | MissingButRequired |
| Clock abstraction | `Services/IClock.cs`, `Services/SystemClock.cs` | Provide testable current time/date source | MissingButRequired |
| Host config | `host.json` | Minimal Functions host config | MissingButRequired |
| Local config template | `local.settings.sample.json` | Committed template without secrets. Copy to local.settings.json for local execution. | Confirmed |
| Plans artifact | `plans/rakuspa-kanda-event-api-implementation-contract-kernel.md` | Store this contract artifact in repository if implementation agent has repository write access | MissingButRequired |

## Prohibited substitutions

| Similar existing path | Why it is not sufficient | Allowed reuse, if any |
| --- | --- | --- |
| Codex CLI / Codex as runtime extractor | Plan selected Azure Functions + Azure OpenAI runtime. Codex runtime would not provide mobile API / Easy Auth integration and would violate selected path | None for runtime. Codex may assist implementation only |
| JSON mode instead of Structured Outputs | JSON mode does not guarantee schema match; Plan requires structured schema output | None |
| Free-form prompt parsing without schema | Fails Plan FR-005/AC-009 and weakens deterministic validation | None |
| Playwright / browser automation | Explicitly out of scope | None |
| LLM image analysis / OCR | Explicitly out of scope | None |
| General web search for PR TIMES discovery | Not in selected implementation scope; may be nondeterministic and expands dependency surface | Only deterministic link following from official/candidate pages |
| Function key authentication as primary mobile auth | Leaks key through mobile URL/bookmark and duplicates Easy Auth responsibility | None for production. Local dev may use host tooling only |
| Function `AuthorizationLevel.Function` combined with Easy Auth | Creates double-auth and mobile usability risk; conflicts with Easy Auth-first contract | None unless future Plan explicitly changes auth strategy |
| Custom HTML parser built from regex only | Instructions require reuse/BCL/framework/OSS investigation; HTML parsing by regex is brittle | Regex is allowed only for date/venue-like string detection after DOM extraction |
| Silent fallback to stale/partial data | Instructions prohibit fallback on processing failure | None |
| Swallowing exceptions without trace logging | Instructions require `Exception.ToString()` trace logging | None |
| Production code depending directly on test fake/stub | Would cause production binding mismatch | Fakes allowed only in test project |
| Reflection-based schema generation without explicit justification | Instructions prohibit reflection by default | Hand-written JSON Schema is preferred. If BCL schema generation is later used and involves reflection, document reason in code comments and chat/report |

## Verification hooks

Downstream `runtime-contract-kernel` and `test-design-kernel` should use these hooks.

| Hook | Purpose | Expected verification point |
| --- | --- | --- |
| Function route and authLevel | Confirm API route and Easy Auth boundary | Function has route `rakuspa/kanda/events` and `AuthorizationLevel.Anonymous`; deployment auth requires Microsoft Entra |
| Options validation | Confirm production configuration is wired | Missing `Application:TimeZone`, source URL, AOAI endpoint/deployment/api key produces explicit error |
| Named HttpClient registration | Avoid port exhaustion and local-only clients | `Program.cs` registers named/source/AOAI HttpClients; services do not `new HttpClient()` per call |
| AngleSharp extraction adapter | Confirm OSS parser boundary | Source collector depends on `IHtmlTextExtractor`, not direct parsing in function entrypoint |
| Source collector DTO | Confirm deterministic source candidate construction | Given fixture HTML, collector emits expected `ExtractionInput` with visible text, links, alt text, date-like strings, venue-like strings |
| AOAI REST request builder | Confirm Structured Outputs contract | Request JSON contains `response_format.type = json_schema`, strict schema, all required fields, `additionalProperties: false` |
| JSON mode rejection | Guard against nearest-neighbor substitution | No production request uses `response_format.type = json_object` |
| Extraction parser | Confirm malformed model response is not accepted | Non-schema / missing field / malformed candidate causes explicit error or rejected candidate according to validation rule |
| Candidate validator | Confirm LLM output is not trusted directly | Invalid date order, missing source URL, unconfirmed venue, unsupported event type are rejected |
| Event selector | Confirm deterministic active/next result | Fixtures cover active, next, none, overlap, same-start-date cases |
| Busy phase classifier | Confirm start+2 inclusive behavior | Start date, start+1, start+2 => `initial_peak`; start+3 inside event => `after_initial_peak` |
| Exception logging | Confirm Instructions compliance | Every catch path logs `Exception.ToString()` before converting to response/error |
| Secret redaction | Confirm no sensitive logs | Logs never include API key, Authorization, Cookie, Easy Auth token, full client principal payload |
| Test substitute boundary | Confirm CI does not call real external systems | Unit/CI integration tests use fake source fetcher and fake AOAI client; real Web/AOAI tests are manual-only if added later |

## Unresolved implementation-realization items

| Item | Status | Reason / required decision |
| --- | --- | --- |
| Azure Functions hosting plan | NeedsHumanDecision | `net10.0` is selected for code, but hosting plan must support it. Linux Consumption is not valid for .NET 10 per current doc; use Flex Consumption for Linux or decide another supported host |
| Azure OpenAI deployment/model/region | NeedsHumanDecision | Plan does not name deployment/model/region. Must be configured outside code |
| Easy Auth tenant | NeedsHumanDecision | Workforce tenant / external tenant selection is environment-specific |
| Easy Auth allowed users/groups/app roles | NeedsHumanDecision | User/group access policy is deployment-specific and must not be hardcoded |
| Easy Auth unauthenticated request behavior exact mode | NeedsHumanDecision | Contract requires unauthenticated requests not reach event data. Whether browser receives redirect or 401 depends on Easy Auth configuration and runtime contract/test design |
| Managed identity / keyless AOAI access | Deferred | Preferred for production, but MVP contract uses API key in app settings to avoid expanding RBAC and SDK surface in this pass |
| Azure.AI.OpenAI SDK adoption | Deferred | REST API is selected for MVP because Structured Outputs REST surface is confirmed. SDK adoption can be revisited later |
| HTML selector details for RAKU SPA pages | ApiSurfaceUnknown | Actual DOM selectors are not committed in this contract. Implementation must inspect live/fixture HTML while keeping extraction resilient |
| Exact NuGet versions | NeedsHumanDecision | Use latest stable compatible versions during implementation; no package lock exists yet |
| Application Insights setup | NeedsHumanDecision | Code uses `ILogger`; sink and workspace/resource configuration are deployment decisions |
| Cache / storage / scheduled refresh | OutOfScopeForThisPass | Plan leaves cache deferred; HTTP live request is selected |
| Repository CI design | Deferred | Test design phase should define CI without real external calls |

## Handoff Packet

- Profile used: `contract-kernel`
- Source artifacts:
  - `/mnt/data/Instructions.txt`
  - `/mnt/data/rakuspa-kanda-event-api.md`
  - `/mnt/data/rakuspa-kanda-event-api-change-risk-triage.md`
  - `/mnt/data/リポジトリの場所.txt`
  - `https://raw.githubusercontent.com/suusanex/coding_agent_plan_and_verify_process/main/.github/agents/implementation-contract-kernel.agent.md`
  - `https://github.com/suusanex/tool_web_get_simple_functions`
  - `https://learn.microsoft.com/en-us/azure/azure-functions/dotnet-isolated-process-guide`
  - `https://learn.microsoft.com/en-us/azure/azure-functions/functions-bindings-http-webhook-trigger`
  - `https://learn.microsoft.com/en-us/azure/app-service/overview-authentication-authorization`
  - `https://learn.microsoft.com/en-us/azure/app-service/configure-authentication-provider-aad`
  - `https://learn.microsoft.com/en-us/azure/foundry/openai/how-to/structured-outputs`
  - `https://learn.microsoft.com/en-us/azure/foundry/openai/how-to/json-mode`
  - `https://learn.microsoft.com/en-us/azure/foundry/openai/reference`
  - `https://www.nuget.org/packages/AngleSharp`
- Selected contracts / IDs:
  - `RC-001`: Easy Auth protected HTTP access
  - `RC-002`: Public source collection
  - `RC-003`: Azure OpenAI structured extraction and validation
- Files inspected:
  - `/mnt/data/Instructions.txt`
  - `/mnt/data/rakuspa-kanda-event-api.md`
  - `/mnt/data/rakuspa-kanda-event-api-change-risk-triage.md`
  - `/mnt/data/リポジトリの場所.txt`
  - public GitHub repository landing page for `suusanex/tool_web_get_simple_functions`
  - `implementation-contract-kernel.agent.md` via public GitHub raw URL
- Files intentionally not inspected:
  - Target repository source files beyond the landing file list, because this is a new application and no prior source code exists for this feature.
  - Existing test files, because the user stated the software is new and pre-change source code does not exist.
  - Azure resource / IaC files, because deployment automation and resource creation are outside this pass.
- Decisions made:
  - Use .NET isolated worker Azure Functions.
  - Use `net10.0` for the new Functions project, with hosting plan caveat left as `NeedsHumanDecision`.
  - Use route `GET /api/rakuspa/kanda/events?date=YYYY-MM-DD`.
  - Use Function trigger `AuthorizationLevel.Anonymous` and require Easy Auth / Microsoft Entra at platform level in deployed environment.
  - Do not use Function key as primary mobile/browser auth.
  - Use AngleSharp as the HTML parser.
  - Use deterministic link following only; do not add general web search for PR TIMES discovery in this pass.
  - Use Azure OpenAI REST Chat Completions with `api-version=2024-10-21` and `response_format.type = json_schema` for MVP.
  - Do not use JSON mode as substitute for Structured Outputs.
  - Use hand-written JSON Schema for Structured Outputs to avoid reflection unless a later implementation explicitly justifies schema generation.
  - Use API key from app settings for MVP AOAI authentication; managed identity is deferred.
  - Keep all source fetching, candidate validation, active/next selection, and busy phase classification deterministic in C#.
  - Treat official source fetch failure, required linked page fetch failure, AOAI failure, malformed Structured Outputs response, and validation infrastructure failure as explicit errors, not fallback opportunities.
  - Preserve Instructions requirements for Japanese docs, English logs/source log messages, no silent fallback, `Exception.ToString()` trace logging, no OS-changing tests, no reflection by default, and OSS/BCL/framework reuse before custom implementation.
- Do not redo unless new evidence appears:
  - Do not replace Azure Functions + AOAI with Codex runtime.
  - Do not replace Structured Outputs with JSON mode or prompt-only JSON.
  - Do not add LLM image analysis, OCR, Playwright, Durable Functions, Timer/cache, notifications, or multi-facility support in this pass.
  - Do not use Function key auth as the primary mobile auth path.
  - Do not build a custom regex-only HTML parser.
  - Do not silently continue with partial sources after fatal source collection failure.
- Remaining work:
  - `NeedsHumanDecision`: choose Azure Functions hosting plan compatible with selected `net10.0` target.
  - `NeedsHumanDecision`: choose AOAI deployment/model/region and set app settings.
  - `NeedsHumanDecision`: choose Easy Auth tenant, allowed users/groups/app roles, and unauthenticated behavior.
  - `NeedsHumanDecision`: confirm whether `net10.0` is acceptable for this repository or whether implementation should downgrade to `net8.0` for hosting simplicity.
  - `NeedsHumanDecision`: exact NuGet versions.
  - `NeedsHumanDecision`: Application Insights / logging sink deployment setup.
  - `Deferred`: managed identity / keyless AOAI access.
  - `Deferred`: cache / Timer / storage design.
  - `ApiSurfaceUnknown`: exact RAKU SPA DOM selectors and fixture shape.
- Recommended next step:
  - Because this contract selects concrete non-trivial paths for auth, AOAI REST Structured Outputs, source collection, and production wiring, run `implementation-contract-review-kernel.agent.md` or bounded `implementation-contract-review` before `runtime-contract-kernel.agent.md`.
  - After review, run `runtime-contract-kernel.agent.md` for `RC-001`, `RC-002`, and `RC-003`.
