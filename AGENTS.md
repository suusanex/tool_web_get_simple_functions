# 共通

1. ドキュメント（コミットログやissueを含む）は日本語で記載する
2. ソースコード上のコメント・XMLコメントは開発者向けのため日本語で記載し、ソースコードやログ出力は英語で記載する
3. 原則として処理失敗時のフォールバックは行わず、処理が失敗したことを示すエラー・例外を返す実装とすること。
4. 全ての例外は、トレースログへException.ToString()の内容を出力すること。そのため、例外を再throwせずに捨てる場合は、その場でトレースログを出力すること。
5. UnitTest と（CIで走る）IntegrationTest は、実OS環境（レジストリ、SetupAPI、サービス、ドライバ、デバイス等）を変更しない。
    1. OS依存処理は必ずインターフェースで抽象化し、テスト時はスタブ／モックを注入する（例: ISetupApiWrapper）。
    2. CIで実行される統合テストもスタブを使用し、管理者権限や実OS変更を要求しない。
6. 原則としてリフレクションを使用しない。もしも実装上リフレクションを使用するべきだと判断する場合は、コードコメントで必要な理由を説明した上で、チャットでもリフレクションを使用した事実と理由を説明すること。
7. 独自実装に入る前に、既存コード、BCL、フレームワーク専用 API、NuGet で取得可能な OSS を調査し、再利用または採用を優先して検討すること。
8. 独自実装を採用する場合は、既存コード・BCL・フレームワーク専用 API・OSS を採用しなかった理由を明記すること。

# Token-aware guardrail kernel flow

次のようにカスタムエージェントを使用した開発フローを使用します。

1. `plan-kernel`
2. `change-risk-triage`
3. `implementation-contract-kernel`（implementation-realization risk がある場合）
4. `implementation-contract-review-kernel` または bounded `implementation-contract-review`（contract が non-trivial の場合）
5. `runtime-contract-kernel`
6. `test-design-kernel`
7. 必要に応じて `implementation-handoff-review`
8. 通常エージェントまたは人間主導で実装
9. `verification-kernel`
10. 未解決がある場合は `coverage-gap-triage`
11. 選択した gap は `coverage-gap-resolution-slice`
12. 必要に応じて `verification-kernel` を再実行

このフローでは、各 agent が 1 回の bounded な実行を行い、未解決項目は成果物に残して停止します。

「直るまで修正し続ける」ことは目的ではありません。

`implementation-handoff-review` は任意の軽量 gate です。

常に必須ではありませんが、実装前に Plan → selected runtime contract → test point → production binding requirement の接続を一度だけ確認したい場合に使います。

## カスタムエージェントが含まれるリポジトリ

リポジトリ: https://github.com/suusanex/coding_agent_plan_and_verify_process/

各エージェントの有るフォルダ: https://github.com/suusanex/coding_agent_plan_and_verify_process/tree/main/.github/agents

エージェントファイルの命名規則: [エージェント名].agent.md

