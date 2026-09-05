# PlanningPoker

A Slack app for running planning poker sessions.

## Deployment

Hosted on Azure App Service at **https://planning--poker.azurewebsites.net**

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
