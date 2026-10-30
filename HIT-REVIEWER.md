# Flix Hit Reviewer

Run on Windows: `dotnet run --project src/Flix.HitReviewer`.

The `/screen` endpoint writes `flix-data/review/hits.jsonl` for non-release decisions, after the audit journal write. Each line is a `HitCase` containing the original structured screening request, the complete decision (including sides and matching evidence), a unique case ID, and comments.

In the GUI select an input hit or investigate JSONL file and the output folder. Enter the reviewer's identity. Each action writes the full hit, disposition, reviewer, UTC time and comments to `release.jsonl`, `reject.jsonl`, or `investigate.jsonl`. a per-queue `processed-*.jsonl` journal tracks completed case IDs so the same queue can be reopened. After processing, the GUI advances to the next hit.

The investigate file can be opened directly as a new queue; embedded cases and existing comments are preserved. Whitelist proposals require a single side, are created inactive and require approval in Flix.ListManager followed by an immutable snapshot rebuild.

Local JSONL files are starter storage and are not transactional across multiple files. Use an access-controlled durable case database, transactional outbox, authentication, dual control, encryption, and tamper-evident audit for production. Release/Reject actions here are case dispositions, **not** payment execution commands.
