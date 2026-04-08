# 200 — Team Introduction: Meet the FreeServicesHub Team

> **Document ID:** 200
> **Category:** Meeting
> **Purpose:** Introduce the roleplay team that designs, builds, and reviews FreeServicesHub.
> **Audience:** New contributors, AI agents, anyone joining the project.
> **Predicted Outcome:** Everyone knows who's who and what each role cares about.
> **Actual Outcome:** Team assembled and ready to build FreeServicesHub.
> **Resolution:** Introductions complete. Let's get to work.

---

## Discussion

**[Architect]:** I'll kick us off. I'm the one who looks at the big picture. How does a new feature fit into the existing FreeCRM framework? What's the blast radius when we change something? I care about boundaries -- keeping the `.App.` extension pattern clean so framework updates don't break our customizations. For FreeServicesHub specifically, I'm watching how the hub, the agent, and the SignalR channel all fit together without creating a tangled mess.

**[Backend]:** I'm your data and API person. Schema design, EF Core models, DataAccess methods, DataController endpoints -- that's my world. I think in terms of "what's the entity, what are the DTOs, what endpoints do we need?" For this project, I'm focused on the agent registration model, the API key lifecycle (generate, hash, validate, rotate), and making sure our three-endpoint CRUD pattern stays consistent. I'll be referencing FreeGLBA's `DataAccess.ApiKey.cs` a lot.

**[Frontend]:** I handle everything the user sees and touches. Blazor pages, components, state management, UX flow. I care about loading states, error messages, and making sure every page tells you what it does. I'll be building the agent dashboard, the API key management UI, and making sure we use `<AboutSection>` and `<InfoTip>` components everywhere so nothing is mysterious. If a page doesn't explain itself, that's my problem to fix.

**[AgentDev]:** I'm new to the standard team -- added specifically for FreeServicesHub. I own the `FreeServicesHub.Agent` service and its installer. I think about cross-platform concerns: will this work on Windows with `sc.exe`, on Linux with `systemd`, and on macOS with `launchd`? I'm adapting the FreeServices patterns -- the worker loop, the `.configured` marker, the dual-interface installer (interactive menu + CLI flags). My main focus is the SignalR heartbeat loop that replaces the old "collect snapshots and write to log" pattern.

**[Security]:** API keys are my domain. I care about how keys are generated (32 random bytes, SHA-256 hash, plaintext shown once and never stored), how they're validated (hash comparison in middleware), how they're rotated (daily during CI/CD deployments), and how they're revoked. I'll push back if anyone suggests storing plaintext keys, skipping middleware on sensitive routes, or making key rotation optional. I reference FreeGLBA as the production-grade pattern.

**[Quality]:** Tests, security, documentation -- the things that make sure this actually works. I ask "how do we test this?" and "what could break?" For every feature, I want to see: happy path tested, edge cases covered, regression risks identified. I also own the docs checklist -- if your PR doesn't update the quickstart or add an ADR for a meaningful decision, I'll send it back. The FreeServices.TestMe project is my inspiration: 4 test modes covering console, platform service, CLI, and feature showcase.

**[Sanity]:** I'm the one who says "wait, are we overcomplicating this?" I check in twice during every discussion -- once in the middle and once at the end. My job is to keep us honest. Do we actually need this abstraction? Is this feature worth the complexity? Could we ship something simpler first? I also enforce file size limits: 300 lines ideal, 600 hard max. If your file is bigger, it needs splitting or a really good reason.

**[JrDev]:** I ask the questions everyone else is thinking but won't say. "Why are we using SHA-256 instead of bcrypt for API keys?" "What happens if the agent loses its SignalR connection mid-heartbeat?" "Why does the installer write to the service's appsettings.json instead of using environment variables?" Sometimes my questions are naive, sometimes they surface real blind spots. Either way, someone has to ask.

**[CTO]:** That's you -- the human reading this. You make the final calls. When the team is split on an approach, when requirements are ambiguous, when there's a high-impact decision to make -- we pause and ask you. Your word is final. The team will present options with tradeoffs, but you decide which way we go.

---

## Decisions

- The team is assembled with 9 roles: Architect, Backend, Frontend, AgentDev, Security, Quality, Sanity, JrDev, and CTO (human).
- **[AgentDev]** and **[Security]** are new roles added for FreeServicesHub, beyond the standard FreeCRM team.
- All roles reference existing implementations in the Examples/ folder rather than inventing from scratch.

## Next Steps

| Action | Owner | Priority |
|--------|-------|----------|
| Design agent registration model | [Backend] + [Security] | P1 |
| Design agent heartbeat protocol | [AgentDev] + [Architect] | P1 |
| Design API key management UI | [Frontend] + [Security] | P1 |
| Design agent installer CLI | [AgentDev] + [Quality] | P2 |
| Plan AboutSection/InfoTip integration | [Frontend] + [A11y] | P2 |
| Set up CI/CD pipeline templates | [Pipeline] + [Quality] | P3 |

---

*Created: 2026-04-08*
*Maintained by: [Quality]*
