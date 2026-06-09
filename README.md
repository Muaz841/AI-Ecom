# EcomAI — AI-Powered E-Commerce Automation Platform

> Multi-tenant backend for AI-driven social commerce: auto-replies on WhatsApp, Instagram & Facebook DMs using Meta Webhooks + Gemini/OpenAI, with an autonomous marketing engine powered by Claude + RAG.

---

## What's Implemented

### ✅ Core Messaging Automation
- **Meta Webhook processing** — WhatsApp, Instagram DM, Facebook DM & comments
- **AI-powered replies** — Gemini (default) or OpenAI, switchable from platform settings
- **Inventory-aware responses** — AI reads product catalog before replying
- **Conversation threading** — per tenant × platform × customer
- **Comment handling** — stored only (no auto-reply), DMs get full AI response

### ✅ Multi-Tenant Architecture
- Host/tenant separation with `IsHost` flag
- Tenant provisioning (create, suspend, activate) from host admin
- Scoped EF global query filters per tenant
- RBAC — roles, permissions, user-role assignment, dynamic JWT claims

### ✅ Meta OAuth Integration
- OAuth connect/callback/disconnect per tenant
- Multi-asset discovery: Facebook Pages, Instagram accounts, WABAs, phone numbers
- Page webhook subscription post-OAuth
- Encrypted token storage at rest (ASP.NET Core Data Protection)
- DB-backed platform Meta config (AppId, AppSecret, GraphVersion, CallbackBaseUrl)

### ✅ Frontend (Angular)
- Auth with host/tenant toggle, JWT session restore
- Tenant management screen (host-only)
- Messaging inbox — real-time capable, split-panel, mobile responsive
- Platform settings screen
- OAuth callback handler
- Webhook tester (dev tool)

### ✅ Autonomous Marketing Engine (Architecture Complete)
- Full spec and DB schema for Claude-powered ad decision engine
- RAG foundation with pgvector for knowledge retrieval
- 3-phase rollout: System Prompt → Knowledge Base → Full RAG Memory
- Skill-file-driven system prompt (no redeployment to change strategy)
- See [`Marketing_Engine_Plan_v3.docx`](./docs/Marketing_Engine_Plan_v3.docx) for complete spec

---

## Tech Stack

| Layer | Technology |
|---|---|
| Runtime | .NET 10 / ASP.NET Core |
| ORM | EF Core 10 (SQL Server) |
| Messaging | MediatR 12 |
| Background Jobs | Hangfire 1.8 |
| Resilience | Polly 8 |
| Logging | Serilog |
| Realtime | SignalR |
| AI Providers | Gemini, OpenAI, Ollama (switchable), Claude |
| Frontend | Angular (SPA) + PrimeNG |
| Auth | JWT + refresh token rotation + RBAC |

---

## Project Structure

```
AI-Whatsapp/
├── Ai-Business/          # Domain entities, commands, interfaces
├── AI-Infrastructure/    # EF Core, repositories, external services, auth
├── AI-Whatsapp/          # API host, controllers, middleware, composition root
├── tests/UnitTests/      # xUnit unit tests
docs/
├── domain_knowledge.docx         # Full implementation documentation
├── Marketing_Engine_Plan_v3.docx # Autonomous marketing engine spec
```

---

## Getting Started

### Prerequisites
- .NET SDK 10.0.103
- SQL Server (or Azure SQL)
- Meta App with WhatsApp/Instagram/Facebook products configured

### Configuration

Copy and fill environment variables / user secrets:

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "<sql-server-connection-string>"
  },
  "JwtSettings": {
    "SecretKey": "<min-32-char-secret>",
    "Issuer": "EcomAI",
    "Audience": "EcomAI",
    "AccessTokenExpiryMinutes": 60,
    "RefreshTokenExpiryDays": 30
  },
  "MetaOAuthSettings": {
    "AppId": "<meta-app-id>",
    "AppSecret": "<meta-app-secret>",
    "GraphVersion": "v21.0",
    "CallbackBaseUrl": "https://yourdomain.com"
  },
  "MetaWebhook": {
    "VerifyToken": "<your-webhook-verify-token>"
  },
  "AIProvider": {
    "ActiveProvider": "Gemini",
    "Gemini": { "ApiKey": "<gemini-api-key>", "Model": "gemini-1.5-pro" },
    "OpenAI": { "ApiKey": "<openai-api-key>", "Model": "gpt-4o" },
    "Ollama": { "BaseUrl": "http://localhost:11434", "Model": "llama3" }
  }
}
```

> **Production:** Store secrets in environment variables or a secrets manager. Never commit to source control.

### Run Migrations

```bash
cd AI-Whatsapp
dotnet ef database update --project AI-Infrastructure --startup-project AI-Whatsapp
```

### Run

```bash
dotnet run --project AI-Whatsapp
```

Swagger available at `https://localhost:{port}/swagger` in development.

---

## AI Provider Switching

The active AI provider is configurable at runtime via Platform Settings (`/host/platform` in the UI or `PUT /api/host/platform/meta`). No redeployment needed.

Supported providers: `Gemini` | `OpenAI` | `Ollama` | `Mock` (zero-cost debug)

---

## Meta Webhook Setup

1. In Meta Developer Console, set webhook URL to: `https://yourdomain.com/api/webhooks/meta`
2. Set verify token to match `MetaWebhook:VerifyToken` in your config
3. Subscribe to: `messages`, `messaging_postbacks`, `feed`, `mention`
4. Connect tenant channels via the Integrations screen in the UI

---

## Marketing Engine (Pending Implementation)

The autonomous marketing engine (Claude + Meta Marketing API + pgvector RAG) is fully architected and documented but not yet implemented in code. The spec covers:

- Daily Hangfire agent job (analyze → decide → execute on Meta Ads)
- pgvector RAG for knowledge retrieval and decision memory
- Safety controls: DryRun mode, spend caps, approval gates, rollback
- Gradual rollout plan (Advisory → Supervised → Partial → Full Autonomy)
- Gemini Veo creative pipeline

See [`docs/Marketing_Engine_Plan_v3.docx`](./docs/Marketing_Engine_Plan_v3.docx) for the complete spec. Contributions welcome.

---

## Pending / Known TODOs

- [ ] Complete Meta outbound methods (template, image, quick-replies, read receipts)
- [ ] Real email provider for password reset (currently logs token)
- [ ] Idempotency for duplicate webhook events (by `ExternalMessageId`)
- [ ] Auth hardening: JWT key to secrets store, CORS restricted to known domains
- [ ] Remove legacy `ClientSecretsRepository` WhatsApp fallback in WebhooksController
- [ ] Connect Messaging inbox to SignalR for live message delivery
- [ ] Angular RBAC management screen (`/admin/rbac`)
- [ ] Angular screens: Content AI, Products, Scheduling (currently placeholders)
- [ ] Implement Marketing Engine services (see spec)
- [ ] Integration tests for webhook signature, tenant resolution, policy enforcement

---

## Contributing

This project is open source. Contributions are welcome across any layer — backend services, Angular screens, test coverage, or the marketing engine implementation.

Please open an issue before large PRs to align on approach.

---


