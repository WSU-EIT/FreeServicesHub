# 110 — Popular Website Pattern Analysis

> **Document ID:** 110  
> **Category:** Analysis  
> **Purpose:** Analyze 15 popular websites for distinct UI/UX patterns, cross-reference against our 16 example pages, and propose 5 new pages.

---

## Part 1: Site-by-Site Pattern Extraction

### Jira (atlassian.com/jira)
| Pattern | Description | Have It? |
|---------|-------------|----------|
| Sprint board with columns | Drag cards between status columns | ✅ KanbanBoard |
| Issue detail sidebar | Click row → slide-in panel with all fields | ❌ |
| Activity/Comment thread | Timestamped comments with user avatars, edit, reply | ❌ |
| Inline status dropdown | Click a badge → dropdown to change status without opening the record | ❌ |
| Filter bar with saved filters | Save filter combinations by name, reload them later | ❌ |
| Breadcrumb hierarchy | Project → Board → Sprint → Issue | ✅ GitBrowser |
| Linked items | "Blocked by", "Related to" relationships between records | ❌ |
| Tabs with counts | "Comments (12)", "History (8)", "All (20)" | ❌ |

### Azure DevOps (dev.azure.com)
| Pattern | Description | Have It? |
|---------|-------------|----------|
| Pipeline stage visualization | Connected boxes showing Build → Test → Deploy stages | ❌ |
| Work item form | Rich form with collapsible field groups, related items | ✅ EditSampleItem (basic) |
| Pull request conversation | Inline code comments, threaded discussion | ❌ |
| Dashboard widgets | Configurable tile-based dashboard, drag to rearrange | ❌ |
| Query builder | Visual filter builder: Field + Operator + Value rows | ❌ |
| Wiki with markdown | Markdown editing with live preview, table of contents | ❌ |
| Branch visualization | Timeline with merge points and labels | ❌ |

### GitHub (github.com)
| Pattern | Description | Have It? |
|---------|-------------|----------|
| Repository file browser | Click through folders, view files with syntax highlighting | ✅ GitBrowser |
| Contribution heatmap | Calendar grid of green squares showing daily activity | ❌ |
| Markdown rendering with preview | Type Markdown → see rendered HTML side-by-side | ❌ |
| Issue/PR list with label pills | Colored label badges on rows with filter-by-label | ✅ SampleItems (basic) |
| Tab navigation with counts | Issues (42) ⋅ Pull Requests (3) ⋅ Actions (OK) | ❌ |
| Code search with regex | Search through code files with highlighting | ❌ |
| Reactions (emoji picker) | 👍 👎 😄 ❤️ reaction buttons on comments | ❌ |
| Keyboard shortcuts panel | "?" opens a modal listing all keyboard shortcuts | ❌ |

### Google (google.com)
| Pattern | Description | Have It? |
|---------|-------------|----------|
| Search autocomplete/typeahead | Dropdown suggestions as you type, keyboard navigation | ❌ |
| Search results page | Title, URL, snippet, metadata per result | ❌ |
| "People also ask" accordion | Expandable question/answer sections that load dynamically | ✅ BootstrapShowcase (static accordion) |
| Knowledge panel sidebar | Structured info card alongside results | ❌ |
| Infinite scroll (Images) | Load more content automatically when reaching the bottom | ❌ |
| Pagination with page numbers | ← 1 2 3 ... 10 → | ✅ SampleItems |

### Bing (bing.com)
| Pattern | Description | Have It? |
|---------|-------------|----------|
| Image grid with lightbox | Masonry grid of images, click to enlarge with prev/next | ❌ |
| Sidebar knowledge card | Structured data card with image, facts, links | ❌ |
| Tabbed result types | All, Images, Videos, News, Maps | ✅ BootstrapShowcase |
| Related searches | Suggestion pills at the bottom | ❌ |

### Yahoo (yahoo.com)
| Pattern | Description | Have It? |
|---------|-------------|----------|
| News feed with mixed content | Articles, videos, ads interleaved in a scrolling feed | ❌ |
| Trending topics sidebar | Live-updating ranked list of popular topics | ❌ |
| Inline stock ticker | Horizontal scrolling bar with real-time values | ❌ |
| Weather widget | Current conditions + forecast in a compact card | ❌ |

### Rover.com
| Pattern | Description | Have It? |
|---------|-------------|----------|
| Search + Map split view | List on the left, interactive map on the right | ❌ |
| Profile card with star rating | Photo, name, rating stars, review count, price | ❌ |
| Date range picker | Calendar popup for selecting check-in / check-out dates | ❌ |
| Price range slider | Min/Max slider to filter by price | ❌ |
| Photo gallery with thumbnails | Main image + thumbnail strip, click to switch | ❌ |
| Review system | 5-star input, written review, owner response | ❌ |

### Dominos.com
| Pattern | Description | Have It? |
|---------|-------------|----------|
| Product configurator | Build a pizza: size → crust → toppings with live preview | ❌ |
| Shopping cart sidebar | Slide-out panel showing selected items and total | ❌ |
| Order tracker | "Order Placed → Prep → Baking → QC → Delivery" with moving dot | ❌ |
| Store locator | Search by address, list + map of results | ❌ |
| Coupon code input | Text field with "Apply" button, shows discount or error | ❌ |

### Wikipedia (wikipedia.org)
| Pattern | Description | Have It? |
|---------|-------------|----------|
| Table of contents (scrollspy) | Sidebar that highlights current section as you scroll | ❌ |
| Infobox sidebar | Structured key-value data card alongside article content | ❌ |
| Footnotes with back-links | Superscript numbers linking to references, and back | ❌ |
| Edit history / revision comparison | View past versions, diff between any two | ✅ CodePlayground (diff) |
| Collapsible sections | Long sections collapsed by default, click to expand | ✅ BootstrapShowcase |
| Category tags | Clickable tags at the bottom linking to related topics | ❌ |

### Ford.com / Chevy.com
| Pattern | Description | Have It? |
|---------|-------------|----------|
| Product comparison table | Select 2-4 vehicles, side-by-side spec rows | ❌ |
| Image carousel/gallery | Large hero image with thumbnail navigation, arrows | ❌ |
| Build & Price configurator | Step through options with running price total | ❌ |
| Specification tabs | Dimensions ⋅ Engine ⋅ Safety ⋅ Tech in tabs | ✅ BootstrapShowcase |
| Video hero section | Full-width background video with overlay text | ❌ |
| Sticky nav with scroll detection | Nav changes appearance when user scrolls down | ✅ StickyMenuClass |

---

## Part 2: Consolidation — Unique Patterns We're Missing

After de-duplicating and grouping, here are the **distinct patterns** not covered by any of our 16 pages:

| # | Pattern | Seen On | What Makes It Distinct |
|---|---------|---------|----------------------|
| P1 | **Search autocomplete / typeahead** | Google, GitHub, Bing, Rover | A dropdown of suggestions appears as you type, with keyboard navigation (↑↓ Enter Esc). Fundamentally different from our filter inputs which filter an existing list — this queries the server for suggestions on each keystroke (debounced). |
| P2 | **Comment / activity thread** | Jira, GitHub, Azure DevOps | User-authored content with timestamps, avatars, edit/delete, reply. Our SignalR activity log is system-generated events. This is threaded conversation with rich interaction — the foundation of every helpdesk ticket view. |
| P3 | **Side-by-side comparison table** | Ford, Chevy, Amazon, Rover | Select 2-4 items → see their properties in aligned rows. Differences highlighted. Column headers are the items, rows are the properties. Completely different from a normal data table. |
| P4 | **Image gallery / lightbox** | Rover, Ford, Bing, Dominos | Grid of thumbnails → click one → full-size overlay with prev/next arrows, keyboard navigation (← → Esc), counter ("3 of 12"). We have zero image handling demos. |
| P5 | **Pipeline / order tracker** | Dominos, FedEx, Azure DevOps | Horizontal sequence of named stages with a progress indicator showing which stage is current. Different from our wizard stepper (which is an interactive form) — this is a read-only status visualization. |
| P6 | **Notification center / dropdown** | GitHub, Jira, Slack, Teams | Bell icon with unread count badge, click opens dropdown of notifications with mark-as-read, links to related items, "mark all read" button. |
| P7 | **Markdown editor + preview** | GitHub, Azure DevOps Wiki | Split pane: raw Markdown on the left, rendered HTML on the right. Toggle between edit/preview/split modes. We have Monaco for code but never render the output. |
| P8 | **Star rating + reviews** | Rover, Amazon, Yelp | Interactive star rating input (hover → preview stars, click → set rating), plus review text. Aggregate display showing distribution (5★: ████ 67%, 4★: ██ 23%, ...). |
| P9 | **Inline status transition** | Jira, Azure DevOps | Click a status badge on a row → dropdown appears with allowed transitions → select one → status updates in-place without opening the record. Different from Kanban drag-and-drop. |
| P10 | **Tab navigation with live counts** | GitHub, Jira, Azure DevOps | Tabs where the label includes a dynamic count badge: "Issues (42) ⋅ PRs (3) ⋅ Actions ✓". The counts come from the server and update when data changes. |

---

## Part 3: The 5 Proposals

### Proposal 1: Search & Autocomplete Demo
**Pattern from:** Google, GitHub, Bing  
**Why this one:** The most ubiquitous interaction on the web. Every search bar, address field, and user picker uses it. We have search inputs that filter lists, but nothing that queries the server for suggestions as you type and shows a dropdown.

**What it would show:**
- Text input with debounced keyup (500ms) calling an API that returns matching SampleItems
- Dropdown panel below the input showing up to 8 suggestions
- Each suggestion shows icon + name + category + status badge
- Keyboard navigation: ↑ ↓ to move highlight, Enter to select, Esc to dismiss
- Click a suggestion → navigates to EditSampleItem
- "Recent searches" section stored in localStorage
- Clear button (×) inside the input
- "No results" empty state when nothing matches
- InfoTips explaining debounce, dropdown positioning, keyboard event handling

**Helpdesk value:** Every helpdesk needs a quick-search bar for finding tickets by number, title, or contact name.

### Proposal 2: Comment Thread Demo
**Pattern from:** Jira, GitHub, Azure DevOps  
**Why this one:** Every helpdesk ticket needs a comment section. Our SignalR demo shows system-generated activity, but user-authored threaded conversation is a completely different pattern — it involves text input, timestamps, edit/delete, and optionally reply nesting.

**What it would show:**
- List of comments on a SampleItem, newest at bottom (chat style)
- Each comment: user avatar (initials circle), display name, relative timestamp ("2 min ago"), message text
- "Add a comment" text area at the bottom with Send button
- Edit (pencil icon) → textarea replaces the message text, Save/Cancel
- Delete with two-click confirmation pattern
- SignalR: comments appear in real time across tabs
- Timestamp formatting using relative time ("just now", "5 min ago", "2 hours ago", "yesterday")
- Empty state: "No comments yet. Start the conversation!"
- New CommentThread data object + in-memory service (same as ApiKeyDemoService pattern)

**Helpdesk value:** Literally the core of a helpdesk ticket — the internal discussion thread.

### Proposal 3: Comparison Table Demo
**Pattern from:** Ford, Chevy, Amazon  
**Why this one:** Side-by-side comparison is a fundamentally different table layout from anything we have. Instead of rows=items and columns=properties (normal table), it's rows=properties and columns=items. It's used on every product page, SaaS pricing page, and spec sheet.

**What it would show:**
- Pick 2-4 SampleItems from a multi-select list (checkboxes) → click "Compare"
- Comparison table: columns are the selected items, rows are properties (Name, Category, Status, Priority, Amount, Enabled, DueDate, Added)
- Rows where values differ get a subtle highlight
- "Best" value in each row gets a green badge (highest amount, highest priority, etc.)
- Pin/unpin columns for scrolling on mobile
- "Remove" button (×) at the top of each column
- "Add another" button to select more items
- Export comparison as CSV or copy as Markdown table
- Responsive: on mobile, swipe horizontally between columns

**Helpdesk value:** Comparing tickets, comparing SLA tiers, comparing service plans.

### Proposal 4: Image Gallery / Lightbox Demo  
**Pattern from:** Rover, Ford, Bing, Instagram  
**Why this one:** We have zero image handling demos. File upload exists but only for text files (.txt, .csv, .json, .xml). Image gallery is one of the most common web patterns — every profile page, product page, and social feed uses it. It teaches completely different concepts: image loading, thumbnails, overlay/modal, keyboard navigation, and responsive image sizing.

**What it would show:**
- Grid of sample image cards (use placeholder images or FontAwesome-based colored tiles)
- Click thumbnail → opens a full-screen lightbox overlay
- Lightbox: large image centered, dark backdrop, close (×) button, prev/next arrows
- Keyboard: ← → to navigate, Esc to close
- Counter: "3 of 12"
- Thumbnail strip at the bottom of the lightbox
- Upload image button (extend UploadFile to accept image types)
- Grid view toggle: 2/3/4 columns
- Lazy loading placeholder (skeleton) for images not yet loaded
- Touch swipe support on mobile (optional)

**Helpdesk value:** Ticket attachments (screenshots, photos of hardware issues) need a gallery viewer.

### Proposal 5: Pipeline / Status Tracker Demo  
**Pattern from:** Dominos order tracker, FedEx shipment, Azure DevOps Pipelines  
**Why this one:** A linear progress visualization showing named stages with a moving indicator. This is NOT the same as our wizard (which is an interactive multi-step form) — it's a read-only status display that shows where something is in a process. Every order system, CI/CD pipeline, and approval workflow uses this pattern.

**What it would show:**
- Horizontal pipeline: connected circles/boxes with labels (e.g., "Submitted → Review → Approved → In Progress → Done")
- Current stage highlighted, completed stages checked, future stages greyed
- Click a stage → panel below shows details (timestamp, who, notes)
- Multiple sample pipelines to switch between (Order tracking, Bug lifecycle, Deployment, Approval chain)
- Animated transition when status advances
- Vertical timeline variant (for mobile or detailed view)
- SampleItem status mapped to pipeline stages
- Live update via button: "Advance to next stage" → animation plays, stage fills in
- Error state: stage turns red with error icon when something fails mid-pipeline
- Branch/fork: one pipeline that splits into two paths (parallel stages)

**Helpdesk value:** Ticket lifecycle visualization, approval workflows, escalation chains.

---

## Part 4: How These Fill the Gap Matrix

| Proposal | Primary Patterns Covered | Gaps Closed from 108 |
|----------|--------------------------|---------------------|
| **Search & Autocomplete** | Typeahead, debounce+API, keyboard nav, localStorage, dropdown positioning | G7 Auto-Complete, E7 keyboard shortcuts |
| **Comment Thread** | User content, relative timestamps, inline edit, threaded UI, SignalR comments | G1 partially (user interaction), new pattern entirely |
| **Comparison Table** | Transposed table, multi-select, diff highlighting, responsive horizontal scroll | New pattern entirely |
| **Image Gallery** | Image grid, lightbox overlay, keyboard nav, lazy loading, responsive images | G15 Lazy Loading, zero image coverage |
| **Pipeline Tracker** | Stage visualization, animated transitions, timeline, branch/fork, error states | Pipeline visualization, not wizard |

---

## Part 5: Implementation Priority

| Order | Page | Effort | Helpdesk Direct Value |
|-------|------|--------|----------------------|
| 1 | **Comment Thread** | Medium (new data model + service) | 🔴 Critical — this IS the helpdesk ticket view |
| 2 | **Search & Autocomplete** | Medium (new API endpoint + JS interop for keyboard) | 🔴 High — quick-find for tickets |
| 3 | **Pipeline Tracker** | Small-Medium (CSS + animation, no new data model) | 🟡 Medium — ticket lifecycle display |
| 4 | **Comparison Table** | Medium (new UI pattern, uses existing data) | 🟡 Medium — SLA/plan comparison |
| 5 | **Image Gallery** | Medium (image handling, lightbox overlay) | 🟢 Low — attachment viewer, nice-to-have |

---

*Created: 2025-06-28*  
*Status: PENDING APPROVAL*  
*Next step: Pick which to build first.*
