# Test inventory

Existing SDK-style .NET 10 xUnit project: tests/AgentRPA.Tests. Existing suites cover permission decisions, task models, workflow validation, scheduling and sanitization. The current change adds City, BusinessSystem and BusinessFunction construction and state changes in AgentRPA.Domain/Resources/BusinessResources.cs. API integration tests require a host and SQLite fixture not present in the current test project.

Acceptance checklist for this development slice: normalized resource codes and correct parent linkage; invalid resource input rejected; disabled resources marked inactive; permission service rejects invalid or disabled scopes. Wider project acceptance (multi-tenant scopes, per-step authorization, workflows, browser execution) remains outstanding.
