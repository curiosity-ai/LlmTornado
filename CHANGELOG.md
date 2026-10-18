# Changelog

## Unreleased

- Add text and image input to Codex app-server and direct OAuth threads, including local images and Base64 data URLs.
- Preserve image content across direct OAuth follow-up turns, function calls, and restored history.
- Read local images asynchronously with cancellation support in direct OAuth turns and `CodexOAuthHistoryItem.UserMessageAsync`.

## 3.8.72 - 2026-10-07

- Add OpenRouter alpha Decisions support for Luna Decisions, Jev 1.13, and the other registered native Decision models.
- Add provider routing, ZDR restrictions, trace metadata, generation identifiers, and request costs for OpenRouter Decisions.
- Include all output modalities in OpenRouter model queries, including models that support both text and decisions.
- Add explicit OpenRouter live tests for Luna Decisions and Jev 1.13, plus local regression tests.
- Change `DecisionScoreAnswer.Legend` from `Dictionary<string, string>` to `Dictionary<string, object>` to preserve structured descriptions.
  Consumers that require string values must handle object and array values.
