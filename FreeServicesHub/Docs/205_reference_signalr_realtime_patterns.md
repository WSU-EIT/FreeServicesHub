# 205 — Reference: SignalR & Real-Time UI Patterns for Agent Dashboard

> **Document ID:** 205
> **Category:** Reference
> **Purpose:** Visual guide to how SignalR data flows through the system and how our agent dashboard should render it.
> **Audience:** Full team, especially [Frontend] and [AgentDev].
> **Outcome:** Everyone can trace a heartbeat from agent to pixel.

**Written by:** [Sanity] — "If I can draw it, we can build it. If I can't draw it, we're overcomplicating it."

---

## 1. The Heartbeat Journey — One Beat, Start to Finish

```
 AGENT (Server 1)                  HUB (Web App)                    BROWSER (Dashboard)
 ================                  =============                    ===================

 AgentWorkerService                                                 AgentDashboard.razor
 collects snapshot:                                                 subscribed to:
 - CPU: 73%                                                         Model.OnSignalRUpdate
 - MEM: 4.2/8 GB
 - DISK: 82%
        |
        | SignalR .InvokeAsync("AgentHeartbeat", payload)
        | Bearer: Token X (API client token)
        |
        v
                                   freeserviceshubHub
                                   receives heartbeat
                                          |
                                          | validate token
                                          | (hash lookup in DB)
                                          |
                                          v
                                   AgentMonitorService
                                   updates in-memory cache
                                   detects changes
                                          |
                                          | Clients.Group("AgentMonitor")
                                          |   .SignalRUpdate(update)
                                          |
                                          v
                                                                    MainLayout receives
                                                                    ProcessSignalRUpdate()
                                                                          |
                                                                          | type == AgentHeartbeat
                                                                          | falls to default case
                                                                          |
                                                                          v
                                                                    Helpers.ProcessSignalRUpdateApp()
                                                                    updates Model.AgentStatuses
                                                                          |
                                                                          | Model.SignalRUpdate(update)
                                                                          | fires OnSignalRUpdate event
                                                                          |
                                                                          v
                                                                    AgentDashboard.SignalRUpdate()
                                                                    finds card by AgentId
                                                                    updates metrics in-place
                                                                    sets threshold colors
                                                                    adds to _recentlyUpdatedIds
                                                                    await InvokeAsync(StateHasChanged)
                                                                          |
                                                                          v
                                                                    CARD FLASHES BLUE (table-info)
                                                                    3-second timer clears highlight
                                                                    DISK CARD TURNS RED (82% > threshold)
```

That's the whole thing. One heartbeat, one straight line, no branches. If it's more complicated than this, we've messed up.

---

## 2. The Agent Card — What One Looks Like

```
 ┌─────────────────────────────────────────────────────────┐
 │  ┌──────┐                                               │
 │  │ icon │  SERVER-PROD-01              ● Online    [3s]  │
 │  │  🖥  │  Windows 11 / x64            ▲ 0.3s ago       │
 │  └──────┘                                               │
 │─────────────────────────────────────────────────────────│
 │                                                         │
 │   CPU          MEMORY         DISK C:        DISK D:    │
 │  ┌─────┐      ┌─────┐       ┌─────┐       ┌─────┐     │
 │  │ 73% │      │ 52% │       │ 82% │       │ 23% │     │
 │  │█████│      │█████│       │█████│       │██   │     │
 │  │█████│      │███  │       │█████│       │     │     │
 │  │█████│      │     │       │█████│       │     │     │
 │  └─────┘      └─────┘       └─────┘       └─────┘     │
 │  WARNING       WARNING       ERROR         OK           │
 │  (>70%)        (>50%)        (>80%)        (<50%)       │
 │                                                         │
 │  Uptime: 14d 6h 23m    Last Deploy: 2026-04-07 02:00   │
 └─────────────────────────────────────────────────────────┘

 CARD BORDER COLORS:
 ┌──── border-success ────┐  All metrics OK
 ┌──── border-warning ────┐  Any metric in warning
 ┌──── border-danger  ────┐  Any metric in error
 ┌──── border-secondary ──┐  Agent offline / no heartbeat
```

The card border takes the WORST status of any metric. One red metric = red border.

---

## 3. Threshold Logic — Simple, No Exceptions

```
 METRIC VALUE      STATUS        CSS CLASS           ICON
 ============      ======        =========           ====
    0%──────┐
            │      OK            bg-success          fa-check-circle
   50%──────┤  <-- MEMORY/DISK warning threshold
            │      WARNING       bg-warning          fa-exclamation-triangle
   70%──────┤  <-- CPU warning threshold
            │
   80%──────┤  <-- DISK error threshold
            │      ERROR         bg-danger           fa-times-circle
   90%──────┤  <-- CPU/MEMORY error threshold
            │
  100%──────┘
```

**Decision tree per metric:**

```
 metric.Value >= metric.ErrorThreshold?
     YES ──> bg-danger
     NO  ──> metric.Value >= metric.WarningThreshold?
                 YES ──> bg-warning text-dark
                 NO  ──> bg-success
```

Three lines of code. Not four. Not two. Three.

```csharp
string GetMetricClass(double value, double warn, double error) =>
    value >= error ? "bg-danger" :
    value >= warn  ? "bg-warning text-dark" :
                     "bg-success";
```

---

## 4. The Dashboard Grid — Card Layout

```
 ┌──────────────────────────────────────────────────────────────────┐
 │  AGENT DASHBOARD              [Card View] [Table View]  [Filter]│
 │                                                                  │
 │  ┌─ AboutSection ──────────────────────────────────────────────┐ │
 │  │ What Is This?          │ What You'll See        │ Actions   │ │
 │  │ Real-time monitor for  │ Agent cards update     │ Click to  │ │
 │  │ all service agents     │ every 30s with CPU,    │ see logs, │ │
 │  │ reporting to this hub. │ memory, disk metrics.  │ revoke.   │ │
 │  └─────────────────────────────────────────────────────────────┘ │
 │                                                                  │
 │  SUMMARY: 3 Online  0 Warning  1 Error  1 Offline               │
 │  ┌──────┐ ┌──────┐ ┌──────┐ ┌──────┐                           │
 │  │  3   │ │  0   │ │  1   │ │  1   │                           │
 │  │Online│ │ Warn │ │Error │ │ Off  │                           │
 │  │ grn  │ │ yel  │ │ red  │ │ gray │                           │
 │  └──────┘ └──────┘ └──────┘ └──────┘                           │
 │                                                                  │
 │  row-cols-1 row-cols-md-2 row-cols-lg-3 row-cols-xl-4 g-3      │
 │  ┌──────────────┐ ┌──────────────┐ ┌──────────────┐            │
 │  │ PROD-01      │ │ PROD-02      │ │ PROD-03      │            │
 │  │ border-danger │ │ border-succes│ │ border-succes│            │
 │  │ CPU:73% WARN │ │ CPU:12% OK   │ │ CPU:45% OK   │            │
 │  │ MEM:52% WARN │ │ MEM:31% OK   │ │ MEM:38% OK   │            │
 │  │ DSK:82% ERR  │ │ DSK:44% OK   │ │ DSK:29% OK   │            │
 │  └──────────────┘ └──────────────┘ └──────────────┘            │
 │  ┌──────────────┐ ┌──────────────┐                              │
 │  │ DEV-01       │ │ STAGING-01   │                              │
 │  │ border-succes│ │ border-second│                              │
 │  │ CPU:8%  OK   │ │   OFFLINE    │                              │
 │  │ MEM:22% OK   │ │ Last seen:   │                              │
 │  │ DSK:15% OK   │ │ 14 min ago   │                              │
 │  └──────────────┘ └──────────────┘                              │
 └──────────────────────────────────────────────────────────────────┘
```

---

## 5. The Activity Feed — What Scrolls In

```
 ┌─ ACTIVITY FEED ─────────────────────────────────────────┐
 │                                                          │
 │  ● 02:00:15  PROD-01    Heartbeat received       [OK]   │
 │  ● 02:00:15  PROD-02    Heartbeat received       [OK]   │
 │  ● 02:00:14  PROD-03    Heartbeat received       [OK]   │
 │  ▲ 02:00:14  PROD-01    Disk C: crossed 80%    [WARN]   │
 │  ● 02:00:12  DEV-01     Heartbeat received       [OK]   │
 │  ✕ 01:59:45  STAGING-01 Connection lost        [ERROR]   │
 │  ★ 01:58:00  PROD-02    Registered (new token)  [INFO]   │
 │  ★ 01:57:55  PROD-01    Registered (new token)  [INFO]   │
 │  ↻ 01:57:50  ---        Deployment started       [SYS]   │
 │                                                          │
 │  Icons:                                                  │
 │  ●  = normal heartbeat     text-success                  │
 │  ▲  = threshold crossed    text-warning                  │
 │  ✕  = error/disconnect     text-danger                   │
 │  ★  = registration event   text-info                     │
 │  ↻  = system event         text-primary                  │
 └──────────────────────────────────────────────────────────┘
```

New entries insert at top via `_activityLog.Insert(0, entry)`. Cap at 100 entries. This is the SignalRDemo pattern exactly.

---

## 6. Agent Detail Page — What You See When You Click a Card

```
 ┌──────────────────────────────────────────────────────────────────┐
 │  < Back to Dashboard                                             │
 │                                                                  │
 │  SERVER: PROD-01                    Status: ● Online             │
 │  OS: Windows 11 Pro / x64          Uptime: 14d 6h 23m          │
 │  .NET: 10.0.3                      Agent Version: 1.0.0         │
 │  Last Heartbeat: 3 seconds ago     Token: ****a7f2 [Revoke]    │
 │                                                                  │
 │  ┌─────────────────────────────────────────────────────────────┐ │
 │  │              CPU USAGE (last 24 hours)                      │ │
 │  │  100%|                                                      │ │
 │  │   90%|──────────────────────────── ERROR THRESHOLD ─────── │ │
 │  │   70%|──────────────────────────── WARNING THRESHOLD ───── │ │
 │  │      |          ╱╲    ╱╲                                    │ │
 │  │      |    ╱╲  ╱╱  ╲╱╱  ╲╱╲                                │ │
 │  │      |╱╲╱╱  ╲╱            ╲╱╲  ╱╲╱╲                       │ │
 │  │   0% |────────────────────────────────────────────────────  │ │
 │  │      2AM    6AM    10AM    2PM    6PM    10PM    2AM        │ │
 │  └─────────────────────────────────────────────────────────────┘ │
 │                                                                  │
 │  ┌───────────────┐ ┌───────────────┐ ┌───────────────┐         │
 │  │ MEMORY        │ │ DISK C:       │ │ DISK D:       │         │
 │  │ [chart]       │ │ [chart]       │ │ [chart]       │         │
 │  │ 4.2 / 8 GB   │ │ 164 / 200 GB  │ │ 115 / 500 GB  │         │
 │  │ 52% WARNING   │ │ 82% ERROR     │ │ 23% OK        │         │
 │  └───────────────┘ └───────────────┘ └───────────────┘         │
 │                                                                  │
 │  ┌─ RECENT LOGS ───────────────────────────────────────────────┐ │
 │  │ 02:00:15  [INFO]  Heartbeat sent successfully               │ │
 │  │ 02:00:14  [WARN]  Disk C: usage at 82%                     │ │
 │  │ 01:30:15  [INFO]  Heartbeat sent successfully               │ │
 │  │ 01:00:15  [INFO]  Heartbeat sent successfully               │ │
 │  └─────────────────────────────────────────────────────────────┘ │
 └──────────────────────────────────────────────────────────────────┘
```

Charts are Highcharts Column type with 24 data points (hourly). Threshold lines are just visual reference — Highcharts `plotLines` on yAxis.

---

## 7. The Subscribe/Unsubscribe Dance — Never Skip Steps

```
 PAGE LOADS                          PAGE UNLOADS
 ==========                          ============

 OnInitialized() {                   Dispose() {
   |                                   |
   |-- Model.View = _pageName          |-- Model.OnChange -= handler
   |                                   |-- Model.Subscribers_OnChange
   |-- if (!Subscribers                |       .Remove(_pageName)
   |      .Contains(_pageName))        |
   |     Subscribers.Add(_pageName)    |-- Model.OnSignalRUpdate -= handler
   |                                   |-- Model.Subscribers_OnSignalRUpdate
   |-- Model.OnChange += handler       |       .Remove(_pageName)
   |                                   |
   |-- Model.OnSignalRUpdate           }
   |     += SignalRUpdate              
   }                                  

 HANDLER FIRES                       
 =============                       

 SignalRUpdate(update) {             
   |                                 
   |-- if (update.Type != mine)      
   |     return                       SKIP wrong type
   |                                 
   |-- if (Model.View != _pageName)  
   |     return                       SKIP wrong page
   |                                 
   |-- if (update.UserId             
   |     == Model.User.UserId)       
   |     return                       SKIP own updates
   |                                 
   |-- deserialize payload           
   |-- update local list in-place    
   |-- add to _recentlyUpdatedIds   
   |-- await InvokeAsync(            
   |     StateHasChanged)             MUST use InvokeAsync
   }                                   (non-UI thread)
```

Four guard checks. Every page. No exceptions. Miss one and you get phantom updates on the wrong page or infinite render loops.

---

## 8. Sanity's Complexity Checklist

Before building anything from this doc, ask:

```
 [ ] Can I draw it in ASCII art?
     NO  --> You don't understand it yet. Stop. Think more.
     YES --> Proceed.

 [ ] Does the data flow have more than one branch?
     YES --> You're overcomplicating it. Heartbeat goes one direction.
     NO  --> Good.

 [ ] Does the threshold logic need more than 3 lines?
     YES --> You're adding edge cases that don't exist yet.
     NO  --> Ship it.

 [ ] Does the card need more than 5 metrics?
     YES --> That's a detail page, not a card. Split it.
     NO  --> Good.

 [ ] Can a new developer read the SignalR handler and know
     what it does in under 30 seconds?
     YES --> Merge it.
     NO  --> Simplify.
```

---

*Created: 2026-04-08*
*Maintained by: [Sanity]*
