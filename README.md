# PlanningPoker

A Slack app for running planning poker sessions.

## Deployment

Hosted on Azure App Service at **https://planning--poker.azurewebsites.net**

Deploys are **manual**. The deployment source points at this repo's `master` branch, but with manual
integration — so merging a PR does not ship it. A merged change is live only once someone runs:

```bash
az webapp deployment source sync -n planning--poker -g <resource-group>
```

Azure then pulls `master` and builds it in place with Kudu. `.deployment` names the project to build,
since the test project sits alongside the web app in the repo root:

```ini
[config]
project = PlanningPoker.csproj
```

To see what is actually running:

```bash
az webapp log deployment list -n planning--poker -g <resource-group> \
  --query "[?active].{id:id, message:message}" -o json
```

`id` is the deployed commit. In the full listing, `status: 4` means success and `3` means failure.

### Target framework

The app targets **net9.0**, not net10.0, because the Kudu build cannot handle net10.0. net9.0 goes out
of support on 2026-11-10, so moving up needs a build that runs somewhere other than Kudu — see #5.

Keep `PlanningPoker.csproj` and `PlanningPoker.Tests/PlanningPoker.Tests.csproj` on the same target
framework: a test project cannot reference an app project built for a newer one.

### Configuration

Two things are supplied to the App Service and are deliberately not in the repo:

- `.planningpokerconfig` — one `teamId:token` line per Slack workspace (gitignored)
- `PlanningPoker:PollSecret` — shared secret for the signed machine-to-machine endpoint

Neither should ever be committed.

## Endpoints

| Method | Path | Purpose |
| --- | --- | --- |
| GET | `/PlanningPoker/Hello` | Health check — returns `Hello from Planning Poker App!` |
| POST | `/PlanningPoker/Poker` | Slack slash command handler |
| POST | `/PlanningPoker/PokerInteract` | Slack interactivity (button) payload handler |

Quick check:

```bash
curl https://planning--poker.azurewebsites.net/PlanningPoker/Hello
```
