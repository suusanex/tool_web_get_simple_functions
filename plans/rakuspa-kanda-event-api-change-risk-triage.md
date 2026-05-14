# Change Risk Triage

## Recommended profile

`contract-kernel`

ただし、implementation-realization risk が `Present` / `Unclear` であるため、immediate next agent は `runtime-contract-kernel.agent.md` ではなく、`implementation-contract-kernel.agent.md` とする。

## Reasoning

この変更は、新規作成の .NET isolated worker Azure Functions アプリケーションとして、RAKU SPA 1010 神田のコラボイベント状況を認証付き HTTP API で返すものである。既存の改修前ソースコードは存在しないため、既存実装との互換性リスクはない。一方で、実行時には以下の cross-boundary interaction が発生する。

- モバイル/ブラウザクライアント → Easy Auth / Microsoft Entra → Azure Functions HTTP endpoint
- Azure Functions → 外部公開 Web サイト（RAKU SPA 公式ニュース、公式キャンペーンページ、補足プレスリリース）
- Azure Functions → Azure OpenAI Structured Outputs
- LLM 抽出結果 → 決定論的検証 → activeEvent / nextEvent 選択

Plan は、Easy Auth、Azure Functions HTTP trigger、Azure OpenAI Structured Outputs、C# による決定論的ソース収集と検証を明確に要求している。これらは runtime contract mismatch や production wiring mismatch が起きやすい境界である。

ただし、対象施設は RAKU SPA 1010 神田 1 件に限定され、Durable Functions、通知、複数施設、LLM 画像解析、Playwright、永続化設計はスコープ外である。そのため、`standard-slice` や `full-coverage` ではなく、選択した high-risk runtime contracts に限定して深く扱う `contract-kernel` が minimum sufficient response である。

一方、Plan には Azure OpenAI SDK/API surface、Structured Outputs の C# 実装方法、Easy Auth と Function authorization level の組み合わせ、HTML 抽出ライブラリ、DI/startup/configuration wiring などの未確定事項が残っている。そのため、implementation-realization branch を先に通し、API surface と production address を確定してから runtime contract kernel に進む必要がある。

## High-risk boundaries

| Boundary | Producer | Consumer | Mechanism | Risk type |
| --- | --- | --- | --- | --- |
| Easy Auth protected HTTP access | Mobile/browser client | Azure Functions HTTP endpoint | HTTPS request, App Service Authentication / Authorization, Entra sign-in/session/header injection | Authentication/authorization mismatch, unauthenticated data leakage, browser/mobile auth flow mismatch |
| Function trigger authorization level | Azure Functions host / Easy Auth platform | Event lookup function entrypoint | Function HTTP trigger authLevel plus Easy Auth unauthenticated-request behavior | Double auth requirement, accidental anonymous exposure, key-in-URL reliance |
| Public source collection | Source collection component | RAKU SPA official news / campaign pages and optional PR TIMES pages | HTTP GET, HTML parsing, link extraction, visible text / alt text extraction | Source shape drift, missing fields, failed fetch, misleading partial candidate construction |
| Text candidate to LLM extraction | Candidate input builder | Azure OpenAI extraction component | Structured DTO / prompt / JSON Schema request | Schema mismatch, token bloat, missing evidence, unsupported date or venue inference |
| Azure OpenAI Structured Outputs response | Azure OpenAI service | Event validator / selector | Structured output schema, SDK/API response parsing | SDK/API mismatch, non-conforming output, hallucinated event fields, unsupported schema features |
| Post-LLM validation and selection | Event validator | API response builder | Deterministic validation, active/next event selection, busyPhase classification | Invalid date acceptance, unconfirmed venue acceptance, overlapping event ambiguity |
| Production dependency wiring | Function app startup / DI / configuration | Function entrypoint and services | DI registrations, options binding, HttpClient registration, AOAI client registration, timezone config | Stub-only success, missing production implementation, wrong config names, local-only behavior |
| Diagnostics and error behavior | Source collectors / AOAI component / validator | Logging and HTTP response surface | Exception handling, trace logging, warning/error response | Silent fallback, swallowed exception, secret/token logging, insufficient correlation |

## Selected runtime contracts to cover

| Contract ID | Boundary | What is at risk | Why selected | Triage status | Next action |
| --- | --- | --- | --- | --- | --- |
| RC-001 | Easy Auth protected HTTP access: mobile/browser client → Easy Auth platform → Azure Functions HTTP endpoint | 未認証呼び出しへのデータ漏洩、Easy Auth と Function authLevel の不整合、モバイルブラウザでのサインイン/401/302挙動の不一致 | ユーザー希望が Easy Auth であり、Plan の FR-001 / AC-001 / AC-002 / AC-017 の中核。ここがずれると API 全体の安全性とモバイル利用性が崩れる | Deferred | `implementation-contract-kernel` で Function trigger authLevel、Easy Auth unauthenticated action、許可ユーザー/グループ、モバイルブラウザ呼び出し方式の実装前契約を確定する。その後 `runtime-contract-kernel` で participant/boundary mapping と test point を作る |
| RC-002 | Public source collection: Source collector → RAKU SPA/PR TIMES public pages → Candidate input builder | 公式ページ構造変更、取得失敗、日付/会場/リンク欠落、誤った補足ソース混入、失敗時の暗黙フォールバック | Plan の FR-003 / FR-004 / AC-006 / AC-007 の中核。外部 Web は新規実装で最も壊れやすく、Instructions の「失敗時はフォールバックせずエラー/例外を返す」方針とも接続する | Deferred | `implementation-contract-kernel` で source URL、取得範囲、HTML/text extraction library、失敗時の例外/エラー契約、候補 DTO を確定する。その後 `runtime-contract-kernel` で source page → candidate input の contract を定義する |
| RC-003 | Azure OpenAI structured extraction and validation: Candidate input builder → AOAI Structured Outputs → Event validator/selector | C# SDK/API surface 不一致、JSON Schema 制約不備、モデル出力のハルシネーション、未確認会場/無効日付の受け入れ、schema と validator の不一致 | Plan の FR-005 / FR-006 / FR-007 / FR-008 / AC-009 / AC-010 / AC-011〜AC-015 の中核。LLM 出力を未検証で返さないという Plan の安全線を守る必要がある | Deferred | `implementation-contract-kernel` で Structured Outputs の SDK/API surface、schema subset、response model、validation rule を確定する。その後 `runtime-contract-kernel` で AOAI request/response と validator/selector の contract を作る |

## Candidate runtime contracts not selected

| Contract ID | Boundary | Why not selected | Candidate status | Suggested next action |
| --- | --- | --- | --- | --- |
| RC-CAND-004 | Function entrypoint → API response JSON → mobile/browser client | JSON response contract は重要だが、RC-001 と RC-003 の結果を受ける downstream surface であり、今回の kernel では認証・ソース・AOAI/検証を優先する | Deferred | `test-design-kernel` で response DTO と代表ケースの test point に含める |
| RC-CAND-005 | Timer trigger / cache warm-up → storage/cache → HTTP function | Plan ではキャッシュとスケジュール更新は必須スコープ外または Deferred。今回の新規作成では live request を優先する | OutOfScopeForThisPass | キャッシュ導入時に別 Plan または追加 triage を行う |
| RC-CAND-006 | Durable Functions orchestration → activities/state | Plan の非目標で明示的に除外されている | OutOfScopeForThisPass | Durable Functions が必要になった時点で別 Plan を作る |
| RC-CAND-007 | Vision model / OCR / Playwright → calendar image extraction | LLM 画像解析、OCR、Playwright は今回の非目標 | OutOfScopeForThisPass | 画像しか情報が取れない実例が出た場合に別 Plan を作る |
| RC-CAND-008 | Native Android app → API | Android ネイティブアプリは非目標。今回はブラウザまたは HTTP shortcut からの直接 API 呼び出しを想定する | OutOfScopeForThisPass | ネイティブアプリ化時に UI/auth flow の別 triage を行う |
| RC-CAND-009 | Managed identity / keyless AOAI access → AOAI resource | Plan では優先候補だが MVP ではアプリ設定も許容として Deferred。選択は環境判断が必要 | NeedsHumanDecision | `implementation-contract-kernel` で key-based / managed identity のどちらを採用するかを明示する |
| RC-CAND-010 | Application Insights / logging sink → diagnostics analysis | 診断は重要だが、今回の最小 runtime contract では各 selected contract の guardrail として扱う | Deferred | `test-design-kernel` と `verification-kernel` で exception logging と secret redaction を確認する |

## Risk trigger scan

| Risk trigger | Present / Absent / Unclear | Notes |
| --- | --- | --- |
| Cross-process or cross-service sequence | Present | Mobile/browser → Easy Auth → Azure Functions → public websites → AOAI の sequence がある |
| Queue / event / webhook / background worker | Absent | 必須スコープは HTTP-only。Timer/cache は Deferred、Durable Functions は非目標 |
| External API or SDK | Present | Azure Functions HTTP trigger、App Service Authentication / Authorization、Azure OpenAI SDK/API、外部公開 Web ページが関係する |
| Authentication or authorization | Present | Easy Auth / Microsoft Entra サインインで Function endpoint を保護する |
| Durable state / retry / replay / idempotency | Unclear | 必須スコープでは永続状態なし。ただし HTTP/AOAI/source fetch の retry policy と cache 戦略は未確定。Instructions により暗黙フォールバックは不可 |
| Startup wiring / DI / configuration | Present | HttpClient、source collectors、AOAI client、validators、options、timezone、logging の production wiring が必要 |
| Production implementation split from test substitute | Present | 新規作成でも、source fetcher / AOAI extraction / auth headers を test substitute で検証する可能性が高い。production binding との不一致リスクがある |
| Multiple runtime participants coordinating state | Present | Client、Easy Auth platform、Function runtime、external websites、AOAI service、validator/selector が状態・契約を受け渡す |
| Observable behavior spanning more than one component | Present | 最終レスポンスは認証、source collection、LLM extraction、validation、selection の合成結果 |

## Implementation realization risk

| Trigger | Status | Evidence | Required next step |
| --- | --- | --- | --- |
| Plan names a specific external SDK or API | Present | Azure Functions HTTP trigger、App Service Authentication / Authorization、Azure OpenAI Structured Outputs が Plan に登場する | `implementation-contract-kernel` で採用 SDK/API surface、API version、C# 呼び出し形を確定する |
| Plan names a package, release, binary artifact, or local lib folder | Unclear | 具体的な NuGet package は未選定。HTML extraction library と AOAI client package は実装時に選ぶ必要がある | BCL/framework/OSS 優先調査を行い、採用/非採用理由を残す |
| Plan names a namespace, type, method, extension method, provider ID, or config section | Unclear | 具体型名は未指定だが、Easy Auth、AOAI endpoint/deployment、source URLs、timezone、HTTP timeout 等の config contract が必要 | options/config section 名と binding location を確定する |
| Existing code contains a similar but different implementation path | Absent | このソフトは新規作成で、改修前のソースコードはない | 既存コード流用リスクはなし。ただし repository convention は作成時に確認する |
| Implementation requires DI/startup/configuration wiring | Present | Plan は HttpClient、source collectors、AOAI client、validator、options、logging を affected components としている | production DI と Function entrypoint の binding requirement を implementation contract に含める |
| The affected production address is not known from current evidence | Present | 対象リポジトリ URL はあるが、新規作成のため project/file path と entrypoint はまだ存在しない | project layout、Function app project、artifact path を実装前に決める |
| Plan contains remaining work about API surface inspection or dependency confirmation | Present | Structured Outputs SDK/API surface、Easy Auth Function authorization level、HTML extraction library、AOAI deployment/model が未解決 | `implementation-contract-kernel` で解決または明示的に `NeedsHumanDecision` として残す |

## Suggested next agent

Immediate next agent: `implementation-contract-kernel.agent.md`

Reason:

- Runtime risk は `contract-kernel` で足りる範囲に収まっている。
- ただし implementation-realization risk が `Present` / `Unclear` であり、Azure Functions、Easy Auth、Azure OpenAI Structured Outputs、HTML extraction、DI/config の concrete API surface と production address が未確定である。
- したがって、`runtime-contract-kernel.agent.md` へ直行してはいけない。

Required inputs for `implementation-contract-kernel.agent.md`:

- `plans/rakuspa-kanda-event-api.md`
- この `plans/rakuspa-kanda-event-api-change-risk-triage.md`
- `Instructions.txt`
- 対象リポジトリ: `https://github.com/suusanex/tool_web_get_simple_functions`
- 新規作成であり、改修前ソースコードは存在しないという前提
- 実装候補の技術前提:
  - .NET isolated worker Azure Functions
  - Azure App Service Authentication / Authorization（Easy Auth）
  - Microsoft Entra sign-in
  - Azure OpenAI Structured Outputs
  - C# による deterministic source collection / validation

Minimum required downstream flow:

1. `implementation-contract-kernel.agent.md`
   - Azure Functions project layout / entrypoint / trigger authLevel の contract を決める
   - Easy Auth と Function authorization level の組み合わせを決める
   - AOAI Structured Outputs の C# SDK/API surface と JSON Schema contract を決める
   - HTML extraction library と source collection DTO contract を決める
   - DI/startup/configuration wiring の production address を決める
   - 未決定項目は `NeedsHumanDecision` または `Deferred` として残す
2. `implementation-contract-review-kernel.agent.md` または bounded `implementation-contract-review`
   - Contract が non-trivial な場合のみ実行する
3. `runtime-contract-kernel.agent.md`
   - RC-001、RC-002、RC-003 の runtime contract identification を行う
   - participant/boundary mapping を行う
   - production wiring / entrypoint verification point を明示する
4. `test-design-kernel.agent.md`
   - stub/fake/mock 使用箇所を identification する
   - UnitTest / CI IntegrationTest は実 OS や外部環境を変更しない方針で test point を作る
   - 外部 Web / AOAI / Easy Auth は実環境依存と substitute の境界を明示する
5. 通常エージェントまたは人間主導で実装
6. `verification-kernel.agent.md`
   - production implementation binding
   - production wiring / entrypoint verification
   - exception logging と secret redaction
   - 未解決項目の explicit unresolved status

## Out of scope for this triage

- 実装コードの作成
- テストコードの作成
- 既存 Plan の改訂
- Azure リソース作成手順の詳細化
- Azure Functions hosting plan の選定
- Azure OpenAI model/deployment/region/価格の選定
- Easy Auth の実 tenant、allowed users/groups、app registration の確定
- HTML extraction library の最終選定
- NuGet package version の確定
- キャッシュ、Timer trigger、Durable Functions、通知、複数施設対応の設計
- LLM 画像解析、OCR、Playwright、Chrome 拡張機能自動化の検討
- runtime evidence、sequence diagram、full integration test design の作成
- 対象リポジトリ全体の探索。新規作成であり、現時点では改修前ソースコードが存在しないため、risk classification に必要な repository source inspection は発生しない

## Handoff Packet

- Profile used: triage-only
- Recommended profile: `contract-kernel`
- Immediate next agent: `implementation-contract-kernel.agent.md`
- Source artifacts:
  - `Instructions.txt`
  - `rakuspa-kanda-event-api.md`
  - `リポジトリの場所.txt`
  - `https://github.com/suusanex/coding_agent_plan_and_verify_process/blob/main/.github/agents/change-risk-triage.agent.md`
  - `https://learn.microsoft.com/en-us/azure/azure-functions/functions-bindings-http-webhook-trigger`
  - `https://learn.microsoft.com/en-us/azure/app-service/overview-authentication-authorization`
  - `https://learn.microsoft.com/en-us/azure/foundry/openai/how-to/structured-outputs`
- Selected contracts / IDs:
  - `RC-001`: Easy Auth protected HTTP access
  - `RC-002`: Public source collection
  - `RC-003`: Azure OpenAI structured extraction and validation
- Files inspected:
  - `/mnt/data/Instructions.txt`
  - `/mnt/data/rakuspa-kanda-event-api.md`
  - `/mnt/data/リポジトリの場所.txt`
  - `change-risk-triage.agent.md` via public GitHub raw URL
- Files intentionally not inspected:
  - 対象リポジトリの source files: 新規作成であり、改修前のソースコードが存在しないため
  - Azure resource / IaC files: 新規作成であり、この triage では deployment automation を設計しないため
  - Test files: 新規作成であり、既存テストは存在しない前提のため
- Decisions made:
  - Runtime risk は narrow だが meaningful であるため `contract-kernel` を推奨する
  - Implementation-realization risk があるため、immediate next agent は `implementation-contract-kernel.agent.md` とする
  - Selected runtime contracts は認証、外部ソース収集、AOAI structured extraction/validation の 3 件に限定する
  - キャッシュ、Timer trigger、Durable Functions、画像解析、Playwright、ネイティブ Android は今回の triage では扱わない
  - 失敗時の暗黙フォールバックは Instructions に反するため、source fetch / AOAI extraction / validation failure は error/exception contract として扱うべき
  - 例外は `Exception.ToString()` を trace log に出力する requirement として downstream に渡す
- Implementation realization risk summary:
  - Status: `Present`
  - Main causes:
    - Azure Functions / Easy Auth / AOAI Structured Outputs の具体 API surface が未確定
    - Project layout / Function entrypoint / DI registration / options binding が未作成
    - HTML extraction library が未選定
    - Function authLevel と Easy Auth action の組み合わせが未決定
    - AOAI credential strategy が key-based か managed identity か未決定
  - Required branch: `implementation-contract-kernel.agent.md`
- Do not redo unless new evidence appears:
  - この pass では `runtime-contract-kernel.agent.md` へ直行しない
  - この pass では `standard-slice` / `full-coverage` は過剰と判断する
  - Selected runtime contracts は RC-001、RC-002、RC-003 の 3 件で開始する
  - 新規作成であり、既存 source code mismatch は `Absent` と扱う
  - 画像解析、Playwright、Durable Functions、複数施設、通知は `OutOfScopeForThisPass`
- Remaining work:
  - `NeedsHumanDecision`: .NET target version と Azure Functions hosting plan
  - `NeedsHumanDecision`: Easy Auth の tenant、allowed users/groups、app registration approach
  - `NeedsHumanDecision`: Function trigger authLevel と Easy Auth unauthenticated action の組み合わせ
  - `NeedsHumanDecision`: Azure OpenAI deployment/model/region/credential strategy
  - `NeedsHumanDecision`: repository project layout と artifact path
  - `Unclear`: Structured Outputs を使う C# SDK/API surface と API version
  - `Unclear`: HTML extraction library と source parsing approach
  - `Deferred`: cache / scheduled refresh / storage design
  - `Deferred`: managed identity / keyless AOAI access。環境確認後に採否判断
- Recommended next step:
  - `implementation-contract-kernel.agent.md` を実行する
  - Required downstream guardrails:
    - Runtime contract identification
    - Runtime participant and boundary mapping
    - Test point mapping
    - Stub / fake / mock / in-memory usage identification
    - Production implementation binding
    - Production wiring / entrypoint verification
    - Explicit unresolved status for anything not completed
