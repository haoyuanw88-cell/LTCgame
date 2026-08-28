# Long-Term Care Game Analytics Backend

Encore.go backend for collecting Unity gameplay data from the long-term care
assistance game project. The analytics service stores completed game sessions,
scores them consistently on the backend, and exposes summary/trend endpoints for
long-term comparison.

The current game catalog is based on the team's Canva deck for the long-term
care assistance game.

## Analytics API

### List Supported Unity Games

```bash
curl http://localhost:4000/v1/games
```

Stable `game_id` values:

- `memory_cards`
- `body_whack_a_mole`
- `stroop_color`
- `trail_making`
- `pipe_puzzle`
- `life_quiz`
- `supermarket`
- `virtual_pet`

### Submit One Completed Unity Session

```bash
curl -X POST http://localhost:4000/v1/game-sessions \
  -H "Content-Type: application/json" \
  -d '{
    "session_id": "unity-device-001-2026-08-02T15:30:00Z",
    "player_id": "player-1",
    "game_id": "supermarket",
    "domain": "memory",
    "difficulty": 2,
    "correct_count": 8,
    "wrong_count": 1,
    "omitted_count": 1,
    "average_reaction_ms": 1600,
    "duration_seconds": 95,
    "played_at": "2026-08-02T07:30:00Z",
    "timezone_offset_minutes": 480,
    "metrics": {
      "shopping_list_count": 3,
      "wrong_item_count": 1,
      "aisle_visits": 4
    }
  }'
```

`session_id` is an idempotency key. Unity can retry the same request after an
offline failure and the backend will return the already stored session.

Supported `domain` values:

- `attention`
- `memory`
- `spatial_reasoning`
- `processing_speed`
- `executive_function`
- `motor_coordination`
- `wellbeing`

Each `domain` must match the submitted `game_id` primary or secondary domains
from `/v1/games`.

### Player Summary And Trends

```bash
curl "http://localhost:4000/v1/players/player-1/cognitive-summary?range_days=30&timezone_offset_minutes=480"
curl "http://localhost:4000/v1/players/player-1/cognitive-trends?range_days=30&domain=memory&timezone_offset_minutes=480"
```

`range_days` accepts `7`, `30`, or `90`. If omitted, it defaults to `30`.

## Database

Local development uses PostgreSQL managed by Encore. Docker Desktop must be
running before starting the backend so Encore can provision the local database
container and apply migrations automatically.

## Local Development

Install Encore before running the app locally. Docker Desktop is required for the
local PostgreSQL database.

Run from the project root:

```bash
encore run
```

Open the local dashboard while `encore run` is running:

```text
http://localhost:9400/
```

Run tests:

```bash
encore test ./...
```

## Deployment

Deploy to Encore Cloud with:

```bash
git add -A .
git commit -m "Update analytics backend"
git push encore
```
