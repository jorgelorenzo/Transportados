---
name: transportados-deploy-public
description: Deploy latest Transportados API and Web containers to Transportados public environments using environment-mapped targets, dry-run support, health checks, and production safety gates.
---

# Transportados Deploy Public

Deploy Transportados public routes through a config-driven target map and guarded execution script. Staging uses `-staging` hosts, paths, and container names; production keeps the existing unsuffixed names.

## Trigger

- Primary trigger command: `$transportados-deploy-public`

## Required Inputs

- `environment`: optional; target environment key. Currently supported: `staging` and `prod`.
- `components`: optional; one or more of `backend`, `frontend`, `landing`.
- `sshKeyPath`: local private key path for VM SSH.

## Current SSH Key Convention

- Use `C:\Users\Rodrigo\.ssh\id_ed25519` for staging unless the user provides a different key.
- Do not commit private keys, PATs, SQL passwords, JWT keys, or local override files.

## Source Of Truth

- Environment targets are defined in `references/public-environments.json`.
- Local secret/operator overrides can be stored in `references/public-environments.local.json`; this path is gitignored.
- Deployment execution is `scripts/deploy-public.ps1`.
- Component bootstrap uses repository scripts:
  - `ops/scripts/deploy-api-to-vm.ps1`
  - `ops/scripts/deploy-blazor-to-vm.ps1`

## Defaults

- If `environment` is omitted, deploy `staging`.
- If `components` is omitted, deploy `backend,frontend`.
- Public route deployment always requires `-ApproveProduction`.
- Backend deploy requires a JWT key, SQL SA password, email broker internal shared secret, and email broker transport values. Provide them with local environment variables (`TRANSPORTADOS_JWT_KEY`, `TRANSPORTADOS_SQL_SA_PASSWORD`, `TRANSPORTADOS_EMAIL_BROKER_INTERNAL_API_SHARED_SECRET`, `TRANSPORTADOS_EMAIL_BROKER_TRANSPORT_*`) or with local-only config overrides.

## Workflow

1. Confirm the requested components and verify images have already been built and pushed to GHCR.
2. Run a dry run.
```powershell
powershell -ExecutionPolicy Bypass -File ".codex/skills/transportados-deploy-public/scripts/deploy-public.ps1" -Environment "staging" -SshKeyPath "C:\Users\Rodrigo\.ssh\id_ed25519" -DryRun
```
3. Execute staging public deployment.
```powershell
powershell -ExecutionPolicy Bypass -File ".codex/skills/transportados-deploy-public/scripts/deploy-public.ps1" -Environment "staging" -SshKeyPath "C:\Users\Rodrigo\.ssh\id_ed25519" -ApproveProduction
```
4. Verify outcome.
- `https://transportados-api-staging.transportados.com/api/health` returns HTTP 200.
- `https://transportados-app-staging.transportados.com` returns HTTP 200.
- The frontend container has `ApiSettings__BaseUrl=https://transportados-api-staging.transportados.com/`.
- Running image IDs changed for configured services unless `-AllowUnchangedImage` was intentionally used.

## Safety Rules

- Never deploy public Transportados routes without `-ApproveProduction`.
- Never run destructive database resets unless explicitly requested.
- Do not target the old Azure production VM (`20.85.231.141`) for Transportados deployments; public Transportados deployments now target staging VM `10.0.0.125`.
- Abort if config is missing required environment or component entries.
- Abort on first failure; do not continue to the next component.
- Abort if the running image is unchanged after deploy unless intentionally overridden with `-AllowUnchangedImage`.
- Keep secrets and operator-specific arguments in `references/public-environments.local.json` or local environment variables.

## Public Routes

Staging:

- `transportados-app-staging.transportados.com -> transportados-web-staging:8080`
- `transportados-api-staging.transportados.com -> transportados-api-staging:8080`

Production:

- `transportados-app.transportados.com -> transportados-web:8080`
- `transportados-api.transportados.com -> transportados-api:8080`
