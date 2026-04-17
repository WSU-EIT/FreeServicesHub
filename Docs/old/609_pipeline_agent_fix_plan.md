# Pipeline Agent Registration Fix — Complete Plan & Research Dump

## Date: April 15, 2026

---

## 1. What You Asked

- Fix deprecated pipeline task warnings (`NodeTool@0` → `UseNode@1`, `NuGetToolInstaller@0` → `@1`)
- Fix the `--NonInteractive` parsing crash in the installer (`System.FormatException`)
- Research what identity the agent service runs under, how it compares to Azure DevOps / GitHub runners
- Figure out why the agent installs and starts successfully but shows **"NO AGENTS REGISTERED"** on the dashboard
- Make the full agent registration flow work end-to-end through the pipeline

---

## 2. What Was Done

### 2.1 Deprecated Task Fix (DONE)
- Updated `Examples/FreeCICD/Templates/build-template.yml`:
  - `NuGetToolInstaller@0` → `NuGetToolInstaller@1`
  - `NodeTool@0` → `UseNode@1` (with updated input names: `versionSource`/`versionSpec` → `version`)
- **NOTE:** The actual warnings come from `Templates/build-template.yml@TemplateRepo` (the `ReleasePipelines` repo). The same changes need to be applied there.

### 2.2 NonInteractive Fix (DONE)
- Changed all `--NonInteractive` → `--NonInteractive=true` in FreeServicesHub.yml (both DEV and PROD stages)
- Root cause: .NET's `AddCommandLine(args)` in the installer treats bare `--NonInteractive` as a key and consumes the next arg (`--Service:InstallPath=...`) as its value, then fails parsing a path string as Boolean

### 2.3 Service Identity Research (DONE)
- Wrote `Docs/608_agent_service_identity.md`
- Summary: Both the Azure DevOps pipeline agent and the FreeServicesHub Agent service run as `NT AUTHORITY\SYSTEM` (Local System). The installer uses `sc.exe create` without `obj=`, which defaults to LocalSystem. Full admin privileges, can create/start/stop services. This is correct and working.

### 2.4 Registration Flow Fix (PARTIALLY DONE — see Section 3)
- **Root cause identified**: The pipeline was generating a random API key and injecting it directly into `Agent.ApiClientToken` in appsettings.json. But the server validates tokens by SHA-256 hashing them and looking them up in the `ApiClientTokens` database table. Since the random key was never registered through the server's `RegisterAgent` endpoint, its hash doesn't exist in the DB → agent gets 401 on every request.
- **Fix started**: Changed PreBuildStage from generating random keys to requesting real RegistrationKeys from the web app API
- **DEV stage partially updated**: Variable reference and appsettings injection updated
- **PROD stage NOT yet updated**: Still has old `ApiKey` references

---

## 3. What's Left To Do

### 3.1 PROD Stage — Update Variable References
The PROD stage still references the old job/variable names. Need to change:

```yaml
# CURRENT (BROKEN):
  - stage: DeployAgentPRODStage
    ...
    variables:
      - group: ${{ variables.CI_PROD_VariableGroup }}
      - name: AgentApiKey
        value: $[ stageDependencies.PreBuildStage.GenerateApiKeys.outputs['apikeys.ApiKey_PROD'] ]

# SHOULD BE:
  - stage: DeployAgentPRODStage
    ...
    variables:
      - group: ${{ variables.CI_PROD_VariableGroup }}
      - name: AgentRegKey
        value: $[ stageDependencies.PreBuildStage.GenerateRegistrationKeys.outputs['regkeys.RegKey_PROD'] ]
```

### 3.2 PROD Stage — Update appsettings.json Injection
The PROD "Deploy Agent & Installer to Services/PROD" step still injects `ApiClientToken`. Need to change to inject `RegistrationKey` instead (same pattern as the DEV fix already applied):

```yaml
# CURRENT (BROKEN):
                      $apiKey = "$(AgentApiKey)"
                      $configPath = Join-Path $agentFolder "appsettings.json"
                      if (Test-Path $configPath) {
                        $config = Get-Content $configPath -Raw | ConvertFrom-Json
                        $config.Agent.ApiClientToken = $apiKey
                        $config.Agent.AgentName = "PROD-$($env:COMPUTERNAME)"
                        ...

# SHOULD BE:
                      $regKey = "$(AgentRegKey)"
                      $configPath = Join-Path $agentFolder "appsettings.json"
                      if (Test-Path $configPath) {
                        $config = Get-Content $configPath -Raw | ConvertFrom-Json
                        $config.Agent.RegistrationKey = $regKey
                        $config.Agent.ApiClientToken = ""
                        $config.Agent.AgentName = "PROD-$($env:COMPUTERNAME)"
                        $config.Agent.HubUrl = "$(CI_PIPELINE_COMMON_WebAppUrl_PROD)"
                        ...
```

### 3.3 DEV & PROD — Remove Stale `--Security:ApiKey` from Installer Configure Step
Both the DEV and PROD "Install & Start Agent Windows Service" steps pass `--Security:ApiKey=$(AgentApiKey)` to the installer configure command. This is no longer needed (the registration key goes into appsettings.json, not the installer). Change to:

```yaml
# CURRENT:
                      & $installerExe configure --NonInteractive=true `
                        "--Service:InstallPath=$agentFolder" `
                        "--Publish:OutputPath=$agentFolder" `
                        "--Security:ApiKey=$(AgentApiKey)"

# SHOULD BE:
                      & $installerExe configure --NonInteractive=true `
                        "--Service:InstallPath=$agentFolder" `
                        "--Publish:OutputPath=$agentFolder"
```

The installer's `--Security:ApiKey` was writing to the installer's own appsettings, but the agent's appsettings (already configured in the previous step) is what matters. The installer doesn't need the key — it just creates the Windows service.

### 3.4 Pipeline Variable Dependencies — Verify `CI_PIPELINE_COMMON_WebAppUrl_DEV` and `CI_PIPELINE_COMMON_WebAppUrl_PROD` Exist
The new registration key request step uses these variables:
- `$(CI_PIPELINE_COMMON_WebAppUrl_DEV)` — e.g., `https://azuredev.em.wsu.edu/FreeServicesHub`
- `$(CI_PIPELINE_COMMON_WebAppUrl_PROD)` — e.g., `https://prod.em.wsu.edu/FreeServicesHub`
- `$(CI_PIPELINE_COMMON_AdminUser)` — admin username for the web app
- `$(CI_PIPELINE_COMMON_AdminPass)` — admin password for the web app

These need to exist in `Templates/common-variables.yml@TemplateRepo` or in an Azure DevOps variable group. **If they don't exist, you need to create them.**

Likely values based on the existing YAML:
```
CI_PIPELINE_COMMON_WebAppUrl_DEV = https://azuredev.em.wsu.edu/freeserviceshub
CI_PIPELINE_COMMON_WebAppUrl_PROD = https://prod.em.wsu.edu/freeserviceshub
```

For the admin credentials, you may want to use a secret variable group rather than putting them in common-variables.yml.

### 3.5 Timing Issue — Registration Keys Generated BEFORE Web App Deploy
**CRITICAL**: The current pipeline generates registration keys in `PreBuildStage` (before build, before deploy). But the web app needs to be running and have a database to generate keys. This means:

- **If this is the first deploy ever**: The web app isn't running yet → key generation will fail (warning only, not blocking)
- **For subsequent deploys**: The web app from the PREVIOUS deploy is still running → keys come from the old web app's database, which gets carried forward. This should work.

**However**, if the deploy replaces the database or the web app is down during PreBuildStage, the key request will fail.

**Better approach**: Move the registration key generation to AFTER the web app deploy, BEFORE the agent deploy. This requires restructuring the stage dependencies:

```
Current:    PreBuildStage → BuildStage → InfoStage → DeployDEV/PROD → DeployAgentDEV/PROD
                                                                        ↑ (uses keys from PreBuildStage)

Better:     BuildStage → InfoStage → DeployDEV → GenerateRegKeyDEV → DeployAgentDEV
                                     DeployPROD → GenerateRegKeyPROD → DeployAgentPROD
```

**Simplest fix**: Add the key generation as a PowerShell step INSIDE the DeployAgentDEVStage/DeployAgentPRODStage, right before the appsettings injection. That way the web app is guaranteed to be running. Example:

```yaml
- task: PowerShell@2
  displayName: "Request Registration Key from DEV Web App"
  inputs:
    targetType: 'inline'
    script: |
      # ... same Request-RegistrationKey function ...
      $regKey = Request-RegistrationKey -BaseUrl "https://azuredev.em.wsu.edu/freeserviceshub" ...
      Write-Host "##vso[task.setvariable variable=AgentRegKey;issecret=true]$regKey"
```

Then use `$(AgentRegKey)` in the subsequent appsettings injection step.

### 3.6 Idempotent Re-Deploys — Handle Existing Agent
On re-deploy, the agent might already be registered with a valid `ApiClientToken` in its appsettings.json from a previous deploy. The pipeline currently:
1. Extracts fresh artifacts (overwrites appsettings.json with the build artifact version → blank token)
2. Injects new RegistrationKey

This means every deploy burns a new registration key and creates a new agent record in the DB. Over time you'll accumulate stale agents.

**Options**:
1. **Accept it** — simple, works, clean up stale agents manually via the dashboard
2. **Check if agent already registered** — before overwriting appsettings.json, check if the existing one has a valid `ApiClientToken`. If so, preserve it and skip the registration key injection. Example:

```powershell
$existingConfig = $null
if (Test-Path $configPath) {
  $existingConfig = Get-Content $configPath -Raw | ConvertFrom-Json
}

if ($existingConfig -and $existingConfig.Agent.ApiClientToken) {
  Write-Host "Agent already has an API token. Preserving existing registration."
  # Still update AgentName and HubUrl, but keep the token
  $config.Agent.ApiClientToken = $existingConfig.Agent.ApiClientToken
  $config.Agent.RegistrationKey = ""
} else {
  Write-Host "No existing token. Injecting registration key for first-time setup."
  $config.Agent.RegistrationKey = $regKey
  $config.Agent.ApiClientToken = ""
}
```

---

## 4. Complete Research: How The Auth Flow Works

### 4.1 The Registration Flow (Server-Side)

**Endpoint**: `POST /api/Data/RegisterAgent` (AllowAnonymous)

**Request body**:
```json
{
  "RegistrationKey": "plaintext-one-time-key",
  "Hostname": "EM-AZ-DWEB-01",
  "OperatingSystem": "Microsoft Windows ...",
  "Architecture": "X64",
  "AgentVersion": "1.0.0",
  "DotNetVersion": "10.0.0"
}
```

**Server does**:
1. `ValidateRegistrationKey(plaintext)` → SHA-256 hashes it, looks up hash in `RegistrationKeys` table where `Used=false` and `ExpiresAt > now`
2. If valid: Creates `Agent` record in DB (Status=Online)
3. Burns the registration key: sets `Used=true`, `UsedByAgentId=agentId`
4. Calls `GenerateApiClientToken(agentId, tenantId)`:
   - Generates random 32-byte plaintext token
   - SHA-256 hashes it
   - Stores ONLY the hash in `ApiClientTokens` table (Active=true, RevokedAt=null)
   - Returns plaintext once
5. Returns `{ AgentId, ApiClientToken: "plaintext-token" }`
6. Broadcasts `AgentConnected` via SignalR

### 4.2 The Auth Middleware (Every Request)

**File**: `FreeServicesHub.App.ApiKeyMiddleware.cs`

**Runs on**: `/api/agent/*` and `/freeserviceshubHub` (SignalR)

**Flow**:
1. Extract token from `Authorization: Bearer <token>` header (HTTP) or `?access_token=` query param (SignalR)
2. SHA-256 hash the token
3. Look up: `ApiClientTokens.FirstOrDefault(t => t.TokenHash == hash && t.Active && t.RevokedAt == null)`
4. If found: Set `HttpContext.Items["AgentId"]`, create ClaimsPrincipal, continue
5. If not found: Return 401 `"Token is invalid, revoked, or inactive"`

### 4.3 The Agent's Boot Sequence

**File**: `AgentWorkerService.cs`

```
1. Load appsettings.json
2. Check: has ApiClientToken OR RegistrationKey?
   - Neither → standalone mode (console logging only, no hub)
   - Has ApiClientToken → skip registration, go to step 4
   - Has RegistrationKey only → step 3
3. POST /api/Data/RegisterAgent { RegistrationKey, Hostname, ... }
   - Success → receive ApiClientToken, persist to appsettings.json
   - Failure → fall back to standalone mode
4. Connect to SignalR hub with Bearer token = ApiClientToken
5. Heartbeat loop: collect system snapshot, send via SignalR
```

### 4.4 Registration Key Lifecycle

| Field | Value | Notes |
|-------|-------|-------|
| KeyHash | SHA-256(plaintext) | Only hash stored in DB |
| ExpiresAt | now + 24 hours | Configurable via `RegistrationKeyExpiryHours` |
| Used | false → true | Burned on first use |
| UsedByAgentId | null → agentId | Links to the agent that used it |

### 4.5 API Client Token Lifecycle

| Field | Value | Notes |
|-------|-------|-------|
| TokenHash | SHA-256(plaintext) | Only hash stored in DB |
| Active | true | Set to false on revoke |
| RevokedAt | null | Set to DateTime on revoke |
| AgentId | Guid | Links to the agent |

**Plaintext is returned ONCE** during registration. Agent persists it to appsettings.json. Server never sees plaintext again — only validates by hash.

### 4.6 Admin Management

- **Agent Settings page** (`/AgentSettings`): Shows registered agents, live status, can push settings via SignalR
- **API Key Manager page**: Lists registration keys and API client tokens, can revoke tokens
- **Revoke endpoint**: `POST /api/Data/RevokeApiClientToken/{id}` — marks token `Active=false`

---

## 5. The Installer's Role (Clarification)

The installer (`FreeServicesHub.Agent.Installer.exe`) does these things:
1. `configure` → Copies published files to install path, writes API key to agent's appsettings.json, calls `sc.exe create` to install the Windows service
2. `start` → `sc.exe start`
3. `stop` → `sc.exe stop`
4. `remove` → `sc.exe stop` + `sc.exe delete`
5. `status` → `sc.exe query` + show log tail
6. `destroy` → stop + delete service + delete files

The installer's `--Security:ApiKey` parameter writes to the **agent's** appsettings.json via `WriteApiKeyToServiceConfig()`. But since the pipeline ALREADY writes to that same file in the previous step, the installer's ApiKey write is redundant. The `--Security:ApiKey` can be removed from the pipeline's installer invocation.

---

## 6. Pipeline Variables Needed

### In `Templates/common-variables.yml@TemplateRepo` or a Variable Group:

| Variable | Example Value | Purpose |
|----------|--------------|---------|
| `CI_PIPELINE_COMMON_WebAppUrl_DEV` | `https://azuredev.em.wsu.edu/freeserviceshub` | DEV web app base URL for API calls |
| `CI_PIPELINE_COMMON_WebAppUrl_PROD` | `https://prod.em.wsu.edu/freeserviceshub` | PROD web app base URL for API calls |
| `CI_PIPELINE_COMMON_AdminUser` | (your admin username) | Used to authenticate to GenerateRegistrationKeys API |
| `CI_PIPELINE_COMMON_AdminPass` | (your admin password) | **Should be a secret variable** |

### Already Existing (used by deploy):
| Variable | Already In | Used By |
|----------|-----------|---------|
| `CI_PIPELINE_COMMON_ApplicationFolder_DEV` | common-variables.yml | Deploy steps (derive Services folder) |
| `CI_PIPELINE_COMMON_ApplicationFolder_PROD` | common-variables.yml | Deploy steps |

---

## 7. Summary of All Files Changed

| File | Change | Status |
|------|--------|--------|
| `Examples/FreeCICD/Templates/build-template.yml` | `NuGetToolInstaller@0`→`@1`, `NodeTool@0`→`UseNode@1` | ✅ Done |
| `FreeServicesHub/FreeServicesHub/FreeServicesHub.yml` | `--NonInteractive` → `--NonInteractive=true` (DEV+PROD) | ✅ Done |
| `FreeServicesHub/FreeServicesHub/FreeServicesHub.yml` | PreBuildStage: Replace `GenerateApiKeys` with `GenerateRegistrationKeys` | ✅ Done |
| `FreeServicesHub/FreeServicesHub/FreeServicesHub.yml` | DEV stage: `AgentApiKey` → `AgentRegKey`, inject RegistrationKey | ✅ Done |
| `FreeServicesHub/FreeServicesHub/FreeServicesHub.yml` | PROD stage: Same changes as DEV | ❌ Not done |
| `FreeServicesHub/FreeServicesHub/FreeServicesHub.yml` | Remove `--Security:ApiKey` from installer configure (DEV+PROD) | ❌ Not done |
| `Docs/608_agent_service_identity.md` | Service identity research doc | ✅ Done |
| `Docs/609_pipeline_agent_fix_plan.md` | This plan document | ✅ Done |
| `ReleasePipelines` repo (external) | Same `NodeTool@0`/`NuGetToolInstaller@0` fixes | ❌ Not done (different repo) |

---

## 8. Quick Reference: What the Final PROD Stage Should Look Like

```yaml
  - stage: DeployAgentPRODStage
    displayName: "Deploy Agent to PROD"
    dependsOn:
      - DeployPRODStage
      - PreBuildStage
    variables:
      - group: ${{ variables.CI_PROD_VariableGroup }}
      - name: AgentRegKey
        value: $[ stageDependencies.PreBuildStage.GenerateRegistrationKeys.outputs['regkeys.RegKey_PROD'] ]
    jobs:
      - deployment: DeployAgentPROD
        # ... (same structure) ...
        strategy:
          runOnce:
            deploy:
              steps:
                # ... downloads ...

                - task: PowerShell@2
                  displayName: "Deploy Agent & Installer to Services/PROD"
                  inputs:
                    targetType: 'inline'
                    script: |
                      # ... same extract logic ...

                      # Inject registration key (NOT ApiClientToken)
                      $regKey = "$(AgentRegKey)"
                      $configPath = Join-Path $agentFolder "appsettings.json"
                      if (Test-Path $configPath) {
                        $config = Get-Content $configPath -Raw | ConvertFrom-Json
                        $config.Agent.RegistrationKey = $regKey
                        $config.Agent.ApiClientToken = ""
                        $config.Agent.AgentName = "PROD-$($env:COMPUTERNAME)"
                        $config.Agent.HubUrl = "$(CI_PIPELINE_COMMON_WebAppUrl_PROD)"
                        $config | ConvertTo-Json -Depth 10 | Set-Content $configPath -Encoding UTF8
                      }

                - task: PowerShell@2
                  displayName: "Install & Start Agent Windows Service (PROD)"
                  inputs:
                    targetType: 'inline'
                    script: |
                      # ... same service management logic ...

                      # Configure WITHOUT --Security:ApiKey
                      & $installerExe configure --NonInteractive=true `
                        "--Service:InstallPath=$agentFolder" `
                        "--Publish:OutputPath=$agentFolder"

                      # ... start, verify ...
```
