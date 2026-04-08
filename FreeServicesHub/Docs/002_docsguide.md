# 002 — Docs Guide: Writing and Organizing Documentation

> **Document ID:** 002
> **Category:** Guide
> **Purpose:** How we name, number, format, and maintain docs.
> **Audience:** Anyone writing docs.
> **Outcome:** Consistent, discoverable, maintainable documentation.

**Scope:** This documentation covers FreeServicesHub patterns and conventions. For FreeCRM base patterns, see `Examples/FreeTools/FreeExamples/Docs/`.

---

## Principles

1. **Numbered chronologically** — Each doc gets the next available number
2. **Categorized by name** — Filename tells you the type
3. **Self-contained** — Each doc stands alone
4. **Versioned with code** — Docs live in Git
5. **Docs as part of done** — PRs include doc updates

---

## Folder Structure

```
FreeServicesHub/
  Docs/
    000_quickstart.md
    001_roleplay.md
    002_docsguide.md
    003_templates.md
    004_feature_xyz.md
    ...
  Docs/archive/
```

---

## Naming Convention

```
{NUM}_{CATEGORY}_{TOPIC}.md
```

| Part | Rule |
|------|------|
| **NUM** | 3-digit, next available (000, 001, 002...) |
| **CATEGORY** | Doc type (see below) |
| **TOPIC** | Main subject, underscores for spaces |

### Get Next Number

```bash
ls Docs/*.md | sort -r | head -1
```

---

## Categories

| Category | Use For | Example |
|----------|---------|---------|
| `quickstart` | Getting started | `000_quickstart.md` |
| `guide` | How-to, standards | `001_roleplay.md` |
| `templates` | Reusable templates | `003_templates.md` |
| `feature` | Feature designs | `004_feature_agents.md` |
| `meeting` | Discussion notes | `005_meeting_apikeys.md` |
| `decision` | ADRs | `006_decision_signalr.md` |
| `runbook` | Ops procedures | `007_runbook_deploy.md` |
| `postmortem` | Incident analysis | `008_postmortem_outage.md` |
| `reference` | Technical details | `009_reference_arch.md` |

---

## Header Format

### Reference / Guide Docs (Single Outcome)

```markdown
# {NUM} — {Title}

> **Document ID:** {NUM}
> **Category:** {Category}
> **Purpose:** {One line}
> **Audience:** {Who reads this}
> **Outcome:** {Status + brief description}
```

### Meeting / Planning Docs (Predicted + Actual + Resolution)

```markdown
# {NUM} — {Title}

> **Document ID:** {NUM}
> **Category:** {Category}
> **Purpose:** {One line}
> **Predicted Outcome:** {What we expected}
> **Actual Outcome:** {What happened — update when done}
> **Resolution:** {Action taken — PR link, decision, etc.}
```

---

## Document Footer

Every doc ends with:

```markdown
*Created: YYYY-MM-DD*
*Maintained by: [Role]*
```

---

## File Size Limits

| Threshold | Lines | Action |
|-----------|-------|--------|
| Target | <=300 | Ideal |
| Soft max | 500 | Consider splitting |
| Hard max | 600 | Must split or justify |

---

## Docs as Part of Done

**Every PR that changes behavior should update docs.**

### PR Checklist

- [ ] Quickstart still works (or updated)
- [ ] New config keys documented
- [ ] New behavior has an example
- [ ] ADR added for meaningful decisions
- [ ] Runbook updated if ops changed

---

## Quick Reference

### Create a Doc

1. Find next number: `ls Docs/*.md | sort -r | head -1`
2. Create: `{NUM}_{category}_{topic}.md`
3. Add header + content + footer
4. Commit: `git commit -m "docs: add {NUM} {topic}"`

### Archive a Doc

1. Move to `Docs/archive/`
2. Keep the number (no renumbering)
3. Update cross-references

### Remember

- Lower numbers = older/foundational
- Higher numbers = newer/recent
- Gaps are fine (don't renumber)

---

*Created: 2026-04-08*
*Maintained by: [Quality]*
