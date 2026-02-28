# AGENTS.md

This file is the authoritative instruction set for Codex in this repository.
Read and follow it before making any code changes.

---

## 1. Architecture Overview

### 1.1 Architectural style
- Use pragmatic Clean Architecture with strict layer boundaries.
- Current projects:
  - `Ai-Business` -> Domain + Application logic/contracts
  - `AI-Infrastructure` -> Persistence + external adapters + tenant runtime services
  - `AI- Whatsapp` -> API host/controllers/composition root
  - `tests/UnitTests` -> unit tests

### 1.2 Layer responsibilities
- Domain (`Ai-Business/Entities`)
  - Entities, invariants, domain behavior.
  - No framework-specific dependencies.
- Application (`Ai-Business/Commands`, `Ai-Business/Clients`, `Ai-Business/Interfaces`, `Ai-Business/Common`)
  - Use-cases, MediatR handlers, contracts, validation behaviors.
  - Depends on domain abstractions, never concrete infrastructure.
- Infrastructure (`AI-Infrastructure`)
  - EF Core `DbContext`, repository implementations, tenant accessor, external API clients.
  - Implements application interfaces from Business.
- API (`AI- Whatsapp`)
  - HTTP transport, controllers, DI, middleware configuration, auth wiring.
  - No business rules in controllers.

### 1.3 Dependency direction rules (mandatory)
- Allowed:
  - `AI- Whatsapp` -> `Ai-Business`, `AI-Infrastructure`
  - `AI-Infrastructure` -> `Ai-Business`
  - `Ai-Business` -> no dependency on API/Infrastructure
- Forbidden:
  - `Ai-Business` referencing `AI- Whatsapp` or `AI-Infrastructure`
  - `AI-Infrastructure` referencing `AI- Whatsapp`
  - Controllers directly depending on EF `DbContext`
  - Controllers containing business decision logic

### 1.4 Strictly forbidden patterns
- Business logic in controllers.
- Bypassing repository/handler abstractions for domain operations.
- Querying tenant data without tenant constraints.
- Hardcoding secrets/tokens in source code.
- Cross-layer shortcuts "just for now".

---

## 2. Coding Rules

### 2.1 Entity design rules
- Entities must enforce invariants through factory methods and behavior methods.
- Avoid public setters for mutable business fields.
- Keep constructors private/protected for EF Core compatibility.
- Every tenant-scoped entity must:
  - implement `ITenantEntity`
  - expose `TenantId` (currently nullable by design)
- For aggregate behavior, prefer methods over external mutation.

### 2.2 Repository usage rules
- Use `IRepository<T>` for generic CRUD and shared read patterns.
- Use specific repositories (`IMessageRepository`, `IConversationThreadRepository`, etc.) only for domain-specific queries.
- Do not duplicate query logic across repositories.
- Repository implementations must stay in Infrastructure only.

### 2.3 UnitOfWork rules
- Current pattern uses `SaveChangesAsync` via repository methods and `PlatformDbContext`.
- If adding a formal `IUnitOfWork`, keep it in Business interface + Infrastructure implementation.
- Do not mix multiple transaction patterns in the same code path.
- A single use-case should define one logical commit boundary.

### 2.4 EF Core conventions
- Entity configuration belongs in `PlatformDbContext` (or extracted configurations when needed).
- All money/decimal fields must define precision explicitly.
- Indexes must be added for query-critical fields.
- Use async EF APIs only.
- Avoid client-evaluated LINQ in production paths.

### 2.5 Multi-tenancy handling rules
- Tenant scoping is mandatory in all tenant-scoped entities and queries.
- Global query filter is the baseline; do not bypass it without explicit reason.
- Tenant resolution comes from `ICurrentTenantAccessor`.
- Webhook flows must set tenant context before downstream processing.
- When introducing new tenant entities, add `TenantId` + filter + index.

### 2.6 Soft delete handling rules
- Current model does not fully implement soft delete.
- If introducing soft delete:
  - add `IsDeleted` + `DeletedAt` + optional `DeletedBy`
  - include query filters to exclude deleted by default
  - avoid physical delete unless explicitly required
- Never introduce partial soft delete behavior on only some critical aggregates without documenting it.

### 2.7 Validation approach
- Use FluentValidation for request/command/query validation.
- Keep validation rules close to request models.
- Use MediatR validation pipeline behavior for automatic enforcement.
- Controllers should not duplicate business validation rules.

### 2.8 Mapping strategy
- Use explicit mapping (manual mapping methods/DTO constructors) unless a mapper is approved globally.
- Keep mapping inside handler/controller-specific boundaries.
- Never expose sensitive fields (e.g., `MetaAccessToken`) in response DTOs.

---

## 3. Task Execution Rules for Codex

### 3.1 Before coding
- Read this `AGENTS.md`.
- Review relevant files in all impacted layers.
- Check for existing abstractions and conventions first.

### 3.2 Implementation behavior
- Extend existing patterns; do not introduce new architecture styles ad hoc.
- Follow SOLID principles.
- Avoid duplicate logic and duplicate DTOs.
- Keep naming consistent with existing repository conventions.
- Keep methods focused and cohesive.

### 3.3 Mandatory constraints
- Never bypass global tenant query filters intentionally.
- Never place domain logic inside controllers.
- Never introduce cross-layer coupling.
- Never hardcode secrets or tokens.
- Never silently change external behavior without updating tests/docs.

### 3.4 Change scope discipline
- Make minimal incremental changes.
- Prefer additive refactors over wide rewrites.
- If refactor is required, preserve behavior compatibility.

---

## 4. Testing Rules

### 4.1 Test location
- Unit tests belong in `tests/UnitTests`.
- Add integration tests in a dedicated test project when introducing persistence behavior that unit tests cannot verify.

### 4.2 Naming convention
- Test class: `<ClassOrHandlerName>Tests`
- Test method: `MethodName_Condition_ExpectedBehavior`
- Keep tests deterministic and independent.

### 4.3 Coverage expectations
- New behavior requires tests for:
  - success path
  - validation/error path
  - boundary/edge path (tenant mismatch, missing data, external failure)
- Update existing tests whenever behavior changes.

### 4.4 Required updates
- Do not ship behavior changes without corresponding test updates.
- If unable to run tests due environment issues, explicitly note what could not be verified.

---

## 5. Code Generation Guidelines

- Check similar files before generating new files.
- Match repository formatting and style.
- Keep methods small and readable.
- Add XML comments for public APIs where intent is non-obvious.
- Avoid speculative abstractions and unused extension points.
- Prefer explicit, maintainable code over generic overengineering.

---

## 6. Modification Rules

- Preserve backward compatibility unless change is explicitly breaking and approved.
- Do not remove code without checking all references.
- If behavior is unclear, inspect call sites before editing.
- Prefer incremental migrations over large schema rewrites.
- When adding entities/fields:
  - update EF model
  - add migration guidance
  - update docs

---

## 7. Performance and Safety Constraints

### 7.1 Performance
- Avoid N+1 queries; use includes and proper query composition.
- Prefer `IQueryable` composition until final materialization.
- Add indexes for frequent filters and ordering fields.
- Avoid loading large payloads unnecessarily.

### 7.2 Async and threading
- Use async APIs consistently end-to-end.
- Do not use `.Result`, `.Wait()`, or sync-over-async.
- Pass cancellation tokens through handlers/repositories/external services.

### 7.3 Transactions and consistency
- Respect transaction boundaries at use-case level.
- Persist related conversation/message updates coherently.
- Ensure logs do not break core business flow; logging failures should not crash primary processing.

### 7.4 Security and compliance
- Keep secrets in user-secrets/env/secret manager, never source control.
- Do not expose sensitive tokens in DTOs or logs.
- Enforce webhook signature validation on inbound events.
- Ensure tenant isolation in all query paths.

---

## 8. Repository-specific Guardrails

- Current backend is conversation-storage ready:
  - `ConversationThread` + `Message` timeline model exists.
  - `ConversationsController` provides backend read APIs.
- UI is intentionally deferred; do not block backend completion on frontend dependencies.
- Any new channel integration (Instagram/Facebook comments/DMs) must:
  - map to existing conversation/message storage model
  - persist inbound + outbound messages
  - preserve tenant scope
  - add logs in `AppLogs`.

---

## 9. Documentation Rules

- Keep `project_implementation_documentation.md` and `domainknowledge.md` aligned with actual code changes.
- Mark status as `Done`, `Partially Implemented`, or `Pending` accurately.
- Do not add implementation chatter; keep docs technical and durable.

---

## 10. Pre-merge Checklist (Codex must self-check)

- Layer boundaries respected.
- Tenant handling preserved.
- No controller business logic added.
- No secret exposure introduced.
- Validation and tests updated.
- Performance risks (N+1, premature materialization) reviewed.
- Documentation updated for behavior/schema/API changes.

