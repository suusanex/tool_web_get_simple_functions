# Implementation Contract Review Kernel

## Verdict

`READY_FOR_RUNTIME_CONTRACT`

## Blocking issues

なし。

本レビューでは、Plan が要求する実装経路、change-risk-triage が選択した runtime contract、implementation-contract-kernel が確定した実装前契約の間に、runtime-contract-kernel へ進めることを妨げる blocking issue は確認しなかった。

ただし、本 verdict は実装開始可能を意味しない。`runtime-contract-kernel` と `test-design-kernel` は未実行であり、実装前にはそれらの downstream prerequisites を通す必要がある。

## Non-blocking notes

- `net10.0` を選択する契約は明示されているが、Azure Functions hosting plan は `NeedsHumanDecision` のまま残っている。これは runtime-contract 設計へ進むことは妨げないが、実装開始前またはデプロイ設計前に確定が必要である。
- Azure OpenAI は REST Chat Completions + `response_format.type = json_schema` を MVP の API surface として選択しており、SDK surface uncertainty を避ける判断として妥当である。ただし API version、model deployment、region、quota、pricing は環境依存のため未解決のまま扱う。
- Easy Auth と Function trigger の関係は、Function 側を `AuthorizationLevel.Anonymous` とし、認証強制を App Service Authentication / Microsoft Entra 側に置く契約で明示されている。これは Function key を mobile/browser URL に含めないという Plan 意図と整合する。
- Source collection は AngleSharp を採用し、独自 HTML parser や Playwright を禁止している。`AGENTS.md` の BCL/framework/OSS 優先方針と整合する。
- Failure handling は silent fallback を禁止し、fatal source fetch / AOAI failure / malformed Structured Outputs response を explicit error として扱う契約になっている。`AGENTS.md` の失敗時フォールバック禁止方針と整合する。
- `local.settings.json` は production address として列挙されているが、local-only / secrets not committed の扱いが明記されている。実装時には `.gitignore` と secret placeholder の扱いを確認する必要がある。

## Scope checked

この pass では、次を確認した。

- `AGENTS.md` の共通ルールと Token-aware guardrail kernel flow
- `plans/rakuspa-kanda-event-api.md` の Plan 要件
- `plans/rakuspa-kanda-event-api-change-risk-triage.md` の selected runtime contracts と next-agent 指示
- `plans/rakuspa-kanda-event-api-implementation-contract-kernel.md` の implementation path / dependency evidence / prohibited substitutions / required code changes / verification hooks
- `implementation-contract-review-kernel.agent.md` の required verdict / bounded review structure

この pass では、production code、test code、Azure resource / IaC、実際の Azure tenant / AOAI deployment は確認していない。

## Plan / implementation contract conformance review

| Checkpoint | Evidence | Status | Notes |
| --- | --- | --- | --- |
| Required artifacts exist | `plans/rakuspa-kanda-event-api.md`、`plans/rakuspa-kanda-event-api-change-risk-triage.md`、`plans/rakuspa-kanda-event-api-implementation-contract-kernel.md` を確認 | Pass | review の required input は揃っている |
| `AGENTS.md` compliance | ドキュメント日本語、失敗時フォールバック禁止、例外ログ、OS-changing test 禁止、reflection 原則禁止、OSS/BCL/framework 優先が contract に反映されている | Pass | 実装時にもコメント/ログ言語差分を維持する必要あり |
| Plan-required implementation path | Azure Functions + AOAI + Easy Auth + deterministic source collection / validation が contract に明示されている | Pass | Codex runtime、Playwright、画像解析、JSON mode は禁止/非採用として記録済み |
| Selected runtime contracts | RC-001 / RC-002 / RC-003 が implementation contract の scope と verification hooks に接続されている | Pass | runtime-contract-kernel で participant/boundary mapping に進める |
| Dependency / API evidence | Azure Functions isolated worker、HTTP trigger、Easy Auth、AOAI REST `json_schema`、AngleSharp が evidence 付きで選定されている | Pass | exact NuGet versions は未確定だが、runtime-contract 設計を妨げる不足ではない |
| Function auth strategy | `AuthorizationLevel.Anonymous` + Easy Auth enforcement が明示され、Function key primary auth が禁止されている | Pass | 実デプロイでは Easy Auth unauthenticated action と allowed users/groups が必要 |
| AOAI Structured Outputs strategy | REST Chat Completions + `response_format.type = json_schema` が MVP API surface として明示され、JSON mode は RejectedSubstitute | Pass | schema details は runtime-contract/test-design で検証点化する必要あり |
| Source collection strategy | official news page を primary source、candidate/campaign page の deterministic link following、AngleSharp による text extraction が明示されている | Pass | general web search は追加しない方針で scope creep を抑制している |
| Failure contract | source fetch / required linked page fetch / AOAI / malformed response failure を explicit error として扱い、silent fallback を禁止 | Pass | HTTP 500 への変換と Exception.ToString() logging を runtime/test で検証する必要あり |
| Required code changes | project path、Program.cs、Function entrypoint、Options、Models、Services、host.json まで production address が列挙されている | Pass | 新規作成で MissingButRequired とする扱いは妥当 |
| Verification hooks | route/authLevel、options validation、named HttpClient、AOAI request builder、validator、selector、exception logging、secret redaction が列挙されている | Pass | test-design-kernel で stub/fake/mock boundary を具体化する |
| Prohibited substitutions | Codex runtime、JSON mode、free-form prompt parsing、Playwright、画像解析、Function key auth、regex-only parser、silent fallback が禁止されている | Pass | Plan と contract の source-of-truth drift は見当たらない |
| Unresolved items | hosting plan、AOAI deployment、Easy Auth tenant/users、exact versions、Application Insights、cache、managed identity などが明示されている | Pass | 実装前の人手判断として残すべきものと Deferred が分離されている |
| Source-of-truth drift | Plan の非目標と contract の out-of-scope が一致し、runtime path の置換も正当化されている | Pass | drift なし |
| Readiness for implementation | runtime-contract-kernel / test-design-kernel が未実行 | NotReadyForImplementation | verdict は `READY_FOR_RUNTIME_CONTRACT` に留める |

## Required handoff inputs

- `plans/rakuspa-kanda-event-api.md`
- `plans/rakuspa-kanda-event-api-change-risk-triage.md`
- `plans/rakuspa-kanda-event-api-implementation-contract-kernel.md`
- `plans/rakuspa-kanda-event-api-implementation-contract-review-kernel.md`
- `AGENTS.md`

## Handoff Packet

- Profile used: `contract-kernel`
- Source artifacts:
  - `AGENTS.md`
  - `plans/rakuspa-kanda-event-api.md`
  - `plans/rakuspa-kanda-event-api-change-risk-triage.md`
  - `plans/rakuspa-kanda-event-api-implementation-contract-kernel.md`
  - `https://github.com/suusanex/coding_agent_plan_and_verify_process/blob/main/.github/agents/implementation-contract-review-kernel.agent.md`
- Selected contracts / IDs:
  - `RC-001`: Easy Auth protected HTTP access
  - `RC-002`: Public source collection
  - `RC-003`: Azure OpenAI structured extraction and validation
- Files inspected:
  - `AGENTS.md`
  - `plans/rakuspa-kanda-event-api.md`
  - `plans/rakuspa-kanda-event-api-change-risk-triage.md`
  - `plans/rakuspa-kanda-event-api-implementation-contract-kernel.md`
  - `.github/agents/implementation-contract-review-kernel.agent.md` from process repository
- Files intentionally not inspected:
  - production source files: 新規作成であり、この review は code 実装を行わないため
  - test files: 新規作成であり、この review は test 実装を行わないため
  - Azure resource / IaC files: Azure resource 作成手順と deployment automation はこの pass の scope 外であるため
  - live RAKU SPA pages: implementation contract review の対象は artifact consistency であり、source scraping 実証は runtime/test 以降に回すため
- Decisions made:
  - implementation-contract-kernel は runtime-contract-kernel へ進める品質に達していると判断する
  - `READY_FOR_IMPLEMENTATION` ではなく `READY_FOR_RUNTIME_CONTRACT` とする
  - Function `AuthorizationLevel.Anonymous` + Easy Auth enforcement は Plan-compatible と判断する
  - AOAI REST `response_format.type = json_schema` は Structured Outputs requirement を満たす implementation path と判断する
  - AngleSharp 採用は OSS reuse 方針と一致し、regex-only parser / Playwright substitute より妥当と判断する
  - 残っている `NeedsHumanDecision` / `Deferred` は runtime-contract 進行を妨げる blocking issue ではないと判断する
- Do not redo unless new evidence appears:
  - Codex runtime、JSON mode、Playwright、LLM 画像解析、Function key primary auth への置換レビューは再開しない
  - `runtime-contract-kernel` より前に full implementation review へ拡張しない
  - production code / test code をこの phase で作成しない
  - Plan / change-risk-triage / implementation-contract-kernel をこの phase で直接修正しない
- Remaining work:
  - `runtime-contract-kernel` で RC-001 / RC-002 / RC-003 の participant、boundary、observable behavior、test point を定義する
  - `test-design-kernel` で fake source fetcher、fake AOAI client、Easy Auth substitute boundary、CI-safe integration test 方針を定義する
  - `NeedsHumanDecision`: Azure Functions hosting plan compatible with selected target framework
  - `NeedsHumanDecision`: AOAI deployment/model/region/credential strategy
  - `NeedsHumanDecision`: Easy Auth tenant、allowed users/groups/app roles、unauthenticated action
  - `NeedsHumanDecision`: exact NuGet versions
  - `Deferred`: managed identity / keyless AOAI access
  - `Deferred`: cache / Timer / storage design
- Recommended next step:
  - `runtime-contract-kernel.agent.md` を実行する
  - 対象 contract は `RC-001`、`RC-002`、`RC-003` に限定する
  - その後 `test-design-kernel.agent.md` へ進む
