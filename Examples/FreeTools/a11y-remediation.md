# Accessibility (A11y) Remediation Guide

> **Generated from:** FreeTools.AccessibilityScanner run on 2026-03-04
> **Scope:** FreeExamples (all 120 pages)
> **Tools:** axe-core 4.10 + htmlcheck
> **Standard:** WCAG 2.1 Level AA

---

## Summary

| Metric | Value |
|--------|------:|
| Pages scanned | 120 |
| Pages passed (HTTP 200) | 120 |
| Unique violation types | 4 |
| Total violation instances | 722 |
| 🔴 Critical | 0 |
| 🟠 Serious | 480 |
| 🟡 Moderate | 242 |
| 🔵 Minor | 0 |

### Key Finding

All 722 violations come from **3 rules repeated on every page** because they originate in the shared layout (`MainLayout.razor` + `NavigationMenu.razor`), plus **1 rule on 2 transient pages** (login/logout). Zero violations originate from any of the 75 example pages.

---

## Violation Types

| # | Rule ID | Severity | WCAG | Instances | Pages | Source |
|--:|---------|:--------:|:----:|----------:|------:|--------|
| 1 | [`link-name`](#1-link-name) | 🟠 Serious | 4.1.2, 2.4.4 | 480 | 120 | `NavigationMenu.razor` |
| 2 | [`skip-link`](#2-skip-link) | 🟡 Moderate | 2.4.1 | 120 | 120 | `MainLayout.razor` |
| 3 | [`landmark-one-main`](#3-landmark-one-main) | 🟡 Moderate | 1.3.1 | 120 | 120 | `MainLayout.razor` |
| 4 | [`page-has-heading-one`](#4-page-has-heading-one) | 🟡 Moderate | 1.3.1 | 2 | 2 | `ProcessLogin`, `Logout` |

---

## Detailed Violations

### 1. `link-name`

| Field | Value |
|-------|-------|
| Severity | 🟠 **Serious** |
| WCAG | [4.1.2 Name, Role, Value](https://www.w3.org/WAI/WCAG21/Understanding/name-role-value), [2.4.4 Link Purpose](https://www.w3.org/WAI/WCAG21/Understanding/link-purpose-in-context) |
| Confidence | 🟢 High (both axe and htmlcheck agree) |
| Instances per page | 4 (2 from axe, 2 from htmlcheck — same 2 elements) |
| Total instances | 480 (4 × 120 pages) |
| Learn more | [dequeuniversity.com/rules/axe/4.10/link-name](https://dequeuniversity.com/rules/axe/4.10/link-name) |

**What it means:** Links must have discernible text so screen readers can announce what the link does. An `<a>` tag that contains only an icon (no text, no `aria-label`) is invisible to assistive technology.

#### Instance A — Theme Dropdown Toggle

| Field | Value |
|-------|-------|
| File | `FreeExamples.Client/Shared/NavigationMenu.razor` |
| Line | 161 |
| Selector | `#themeDropdown` |

**Current code:**
```razor
<a class="nav-link dropdown-toggle" href="#" id="themeDropdown"
   role="button" data-bs-toggle="dropdown" aria-expanded="false">
    @switch (Model.Theme) {
        case "dark":
            <Icon Name="ThemeDark" />
            break;
        case "light":
            <Icon Name="ThemeLight" />
            break;
        case "auto":
            <Icon Name="ThemeAuto" />
            break;
    }
</a>
```

**Problem:** The `<a>` contains only an `<Icon>` component (renders an `<i>` tag). No text content, no `aria-label`, no `<span class="visually-hidden">`. Screen readers announce this as "link" with no description.

#### Instance B — User Menu Offcanvas Toggle

| Field | Value |
|-------|-------|
| File | `FreeExamples.Client/Shared/NavigationMenu.razor` |
| Line | 202 |
| Selector | `a[data-bs-toggle="offcanvas"]` |

**Current code:**
```razor
<a class="nav-link" data-bs-toggle="offcanvas"
   href="#offcanvasUserMenu" role="button"
   aria-controls="offcanvasUserMenu">
    @if (Model.LoggedIn) {
        @if (!String.IsNullOrWhiteSpace(Helpers.UserAvatarUrl)) {
            <img class="user-menu-icon" src="@Helpers.UserAvatarUrl" />
        } else {
            <Icon Name="User" />
        }
    } else {
        <Icon Name="Info" />
    }
</a>
```

**Problem:** Same as Instance A — the `<a>` contains only an icon or image. The `<img>` also lacks an `alt` attribute. `aria-controls` tells the screen reader what panel opens, but not what the link itself is for.

---

### 2. `skip-link`

| Field | Value |
|-------|-------|
| Severity | 🟡 **Moderate** |
| WCAG | [2.4.1 Bypass Blocks](https://www.w3.org/WAI/WCAG21/Understanding/bypass-blocks) |
| Confidence | 🟡 Medium (htmlcheck only; axe did not flag) |
| Instances per page | 1 |
| Total instances | 120 |
| Learn more | [webaim.org/techniques/skipnav](https://webaim.org/techniques/skipnav/) |

**What it means:** Keyboard users must be able to skip the navigation and jump to the main content. Without a skip link, a keyboard user has to tab through every nav item on every page load.

| Field | Value |
|-------|-------|
| File | `FreeExamples.Client/Layout/MainLayout.razor` |
| Line | 33 (top of `#page-area`) |

**Current code (top of page):**
```razor
<div id="page-area" class="@PageAreaClass">
    <OffcanvasPopoutMenu />
    @if (!Loading) {
        ...
        <NavigationMenu Loading="Loading" />
```

**Problem:** No `<a href="#main-content" class="visually-hidden-focusable">Skip to content</a>` exists at the top of the page. The first focusable element is inside the navigation.

---

### 3. `landmark-one-main`

| Field | Value |
|-------|-------|
| Severity | 🟡 **Moderate** |
| WCAG | [1.3.1 Info and Relationships](https://www.w3.org/WAI/WCAG21/Understanding/info-and-relationships) |
| Confidence | 🟡 Medium (htmlcheck only; axe did not flag) |
| Instances per page | 1 |
| Total instances | 120 |
| Learn more | [dequeuniversity.com/rules/axe/4.10/landmark-one-main](https://dequeuniversity.com/rules/axe/4.10/landmark-one-main) |

**What it means:** Every page should have exactly one `<main>` landmark (or `role="main"`). Screen readers use landmarks to let users jump directly to content areas.

| Field | Value |
|-------|-------|
| File | `FreeExamples.Client/Layout/MainLayout.razor` |
| Line | 67 |

**Current code:**
```razor
<div class="@(useAppLayoutMinimal ? "" : "container-fluid page-view")">
    ...
    <div class="pb-3">@Body</div>
</div>
```

**Problem:** The content wrapper is a plain `<div>`. It should be a `<main>` element (or have `role="main"`).

---

### 4. `page-has-heading-one`

| Field | Value |
|-------|-------|
| Severity | 🟡 **Moderate** |
| WCAG | [1.3.1 Info and Relationships](https://www.w3.org/WAI/WCAG21/Understanding/info-and-relationships) |
| Confidence | 🟡 Medium (htmlcheck only) |
| Instances | 2 |
| Pages | `/tenant1/ProcessLogin`, `/tenant1/Logout` |

**What it means:** Pages that contain headings (`<h2>`, `<h3>`, etc.) but no `<h1>` have a broken heading hierarchy. Screen reader users rely on heading levels for page structure.

| Field | Value |
|-------|-------|
| File | Framework login/logout pages (not in FreeExamples app code) |

**Problem:** The ProcessLogin and Logout pages have headings but no `<h1>`. These are transient redirect pages, so this is low-impact.

---

## Remediation Options

### Fix 1: `link-name` — Add `aria-label` to icon-only links

**Option A: `aria-label` attribute (Recommended — minimal change)**

```razor
@* BEFORE *@
<a class="nav-link dropdown-toggle" href="#" id="themeDropdown"
   role="button" data-bs-toggle="dropdown" aria-expanded="false">
    <Icon Name="ThemeDark" />
</a>

@* AFTER *@
<a class="nav-link dropdown-toggle" href="#" id="themeDropdown"
   role="button" data-bs-toggle="dropdown" aria-expanded="false"
   aria-label="@Helpers.Text("Theme")">
    <Icon Name="ThemeDark" />
</a>
```

```razor
@* BEFORE *@
<a class="nav-link" data-bs-toggle="offcanvas"
   href="#offcanvasUserMenu" role="button"
   aria-controls="offcanvasUserMenu">
    <Icon Name="User" />
</a>

@* AFTER *@
<a class="nav-link" data-bs-toggle="offcanvas"
   href="#offcanvasUserMenu" role="button"
   aria-controls="offcanvasUserMenu"
   aria-label="@Helpers.Text("UserMenu")">
    <Icon Name="User" />
</a>
```

**Option B: `visually-hidden` span (Bootstrap pattern)**

```razor
<a class="nav-link dropdown-toggle" href="#" id="themeDropdown"
   role="button" data-bs-toggle="dropdown" aria-expanded="false">
    <Icon Name="ThemeDark" />
    <span class="visually-hidden">Theme</span>
</a>
```

**Option C: `title` attribute (Least preferred — tooltip only)**

```razor
<a class="nav-link dropdown-toggle" href="#" id="themeDropdown"
   role="button" data-bs-toggle="dropdown" aria-expanded="false"
   title="Theme">
    <Icon Name="ThemeDark" />
</a>
```

> **Recommendation:** Option A. It's the most direct fix, uses existing `Helpers.Text()` for localization, and doesn't change the visual appearance. The `title` attribute is already on the parent `<li>` but isn't inherited by the `<a>` for accessibility purposes.

---

### Fix 2: `skip-link` — Add a skip-to-content link

Add a visually-hidden-focusable link as the first child of `#page-area` in `MainLayout.razor`:

```razor
@* BEFORE (MainLayout.razor, line ~33) *@
<div id="page-area" class="@PageAreaClass">
    <OffcanvasPopoutMenu />

@* AFTER *@
<div id="page-area" class="@PageAreaClass">
    <a href="#main-content" class="visually-hidden-focusable skip-link">
        Skip to main content
    </a>
    <OffcanvasPopoutMenu />
```

Then add the matching `id` to the content area (combined with Fix 3):

```razor
@* Line ~67: also add the id target *@
<main id="main-content" class="@(useAppLayoutMinimal ? "" : "container-fluid page-view")">
```

Optional CSS for positioning (Bootstrap's `visually-hidden-focusable` handles most of this):

```css
.skip-link:focus {
    position: fixed;
    top: 0;
    left: 0;
    z-index: 9999;
    padding: 0.5rem 1rem;
    background: var(--bs-primary);
    color: white;
    text-decoration: none;
    font-weight: 600;
}
```

> **How it works:** The link is invisible until a keyboard user presses Tab. It appears at the top of the screen. Pressing Enter jumps focus to `#main-content`, bypassing all navigation.

---

### Fix 3: `landmark-one-main` — Change content `<div>` to `<main>`

Change the content wrapper from `<div>` to `<main>` in `MainLayout.razor`:

```razor
@* BEFORE (MainLayout.razor, line ~67) *@
<div class="@(useAppLayoutMinimal ? "" : "container-fluid page-view")">
    ...
    <div class="pb-3">@Body</div>
</div>

@* AFTER *@
<main id="main-content" class="@(useAppLayoutMinimal ? "" : "container-fluid page-view")">
    ...
    <div class="pb-3">@Body</div>
</main>
```

> **Note:** This fix combines with Fix 2 — the `id="main-content"` serves as both the skip-link target and the landmark. Changing `<div>` to `<main>` is a zero-visual-impact change.

---

### Fix 4: `page-has-heading-one` — Low priority (transient pages)

The ProcessLogin and Logout pages are transient redirect screens. If desired, add an `<h1>` to these pages:

```razor
@* In the login/logout page template *@
<h1 class="visually-hidden">Logging in</h1>
```

> **Priority:** Low. These pages display for less than a second during login redirects. The heading hierarchy issue has minimal real-world impact.

---

## Implementation Priority

| Priority | Fix | Impact | Effort | Fixes |
|:--------:|-----|--------|--------|------:|
| **P1** | [Fix 1: `link-name`](#fix-1-link-name--add-aria-label-to-icon-only-links) | 🟠 Serious × 480 | ~5 min (2 lines) | 480 |
| **P2** | [Fix 3: `landmark-one-main`](#fix-3-landmark-one-main--change-content-div-to-main) | 🟡 Moderate × 120 | ~1 min (1 tag change) | 120 |
| **P3** | [Fix 2: `skip-link`](#fix-2-skip-link--add-a-skip-to-content-link) | 🟡 Moderate × 120 | ~5 min (3 lines + CSS) | 120 |
| **P4** | [Fix 4: `page-has-heading-one`](#fix-4-page-has-heading-one--low-priority-transient-pages) | 🟡 Moderate × 2 | ~1 min | 2 |
| | | **Total** | **~12 min** | **722** |

---

## Files to Modify

| File | Fixes Applied |
|------|---------------|
| `FreeExamples.Client/Shared/NavigationMenu.razor` (line 161, 202) | Fix 1: Add `aria-label` to both icon-only links |
| `FreeExamples.Client/Layout/MainLayout.razor` (line 33, 67) | Fix 2: Add skip link; Fix 3: `<div>` → `<main>` |

> **Note:** Because `NavigationMenu.razor` and `MainLayout.razor` are FreeCRM framework files, these fixes should also be applied upstream in `FreeCRM-main` so all 30+ derived projects benefit.

---

## Appendix: Rule Reference

### axe-core Rules Detected

| Rule | Severity | WCAG | Description |
|------|:--------:|:----:|-------------|
| `link-name` | 🟠 Serious | 4.1.2, 2.4.4 | Links must have discernible text |

### htmlcheck Rules Detected

| Rule | Maps to | Severity | WCAG | Description |
|------|---------|:--------:|:----:|-------------|
| `link-empty` | `link-name` | 🟠 Serious | 4.1.2 | Link has no text content or accessible name |
| `skip-link-missing` | `skip-link` | 🟡 Moderate | 2.4.1 | No skip-to-content link found |
| `landmark-main` | `landmark-one-main` | 🟡 Moderate | 1.3.1 | Page has no `<main>` landmark |
| `page-has-heading-one` | `page-has-heading-one` | 🟡 Moderate | 1.3.1 | Page has headings but no `<h1>` |

### Confidence Scoring

The scanner runs multiple tools (axe + htmlcheck) and cross-references results:

| Confidence | Meaning | Our Violations |
|:----------:|---------|:--------------:|
| 🟢 High | Both tools agree | `link-name` |
| 🟡 Medium | One tool flags, other doesn't | `skip-link`, `landmark-one-main`, `page-has-heading-one` |
| 🔴 Conflict | Tools disagree | None |

---

*Generated from AccessibilityScanner (FreeTools) v1.0 scan data. Automated scanning catches ~30-60% of WCAG issues. Manual keyboard and screen reader testing is still required for full compliance.*
