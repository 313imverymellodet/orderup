# ORDER UP!

A kitchen game that cooks to the music. Chop, cook, plate and serve against the clock, either solo or with up to 4 chefs online. It's a Unity 6 WebGL game that runs on phones and desktop and is hosted on Vercel.

## The hook: KITCHEN BEATS
The kitchen runs on the music (126 bpm).
- **Chopping is tapping.** At a board with a raw ingredient, every tap is a chop stroke. **On the beat** a tap counts double (4 taps finish it); **off the beat** it counts half. Holding the button still chops, but at half the old speed.
- **Serving on the beat** pays a 1.5x tip.
- **Crew beat streak:** every on-beat chop or serve adds to a streak that the whole crew shares. It multiplies every tip by up to 2x (+5% per step, capped at 20). An off-beat chop breaks it.
- **On screen:**
  - A beat ring shrinks onto the action button on every beat and flashes green on a hit.
  - The kitchen lights pulse to the music.
  - The "BEAT xN · TIPS xM" meter sits above the button.
  - Results show on-beat hits and the best streak.
- **Online:** each device judges its own taps against the music it hears and sends a running on-beat count (`b`) with its inputs; the host credits them and broadcasts the streak (`bs`). Older clients simply count as off-beat.
- Analytics: `beats_best_streak`, `beats_on_beat`.

## How to play
- **Tickets** at the top show what to cook. Serve each one before its timer runs out.
- Grab ingredients from the **crates**. Lettuce, tomato and cheese need **chopping**: put them on a board and TAP the action button on the beat. **Patties** go on the stove. Pick them up once cooked, because if you leave them too long they burn and have to be trashed.
- Put the ingredients on a **plate** (from the plate stack) and take the finished dish to the **serving hatch**. Fast service earns tips, and serving several in a row builds a combo bonus.
- Controls: drag on the left side of the screen (or use WASD) to move, press the action button (or SPACE) to act, and use DASH (or SHIFT) to dash.

| Dish | Ingredients |
|---|---|
| Cheeseburger | bread + cooked patty + chopped cheese |
| Deluxe | bread + patty + cheese + lettuce + tomato |
| Salad | chopped lettuce + chopped tomato |
| Sandwich | bread + chopped cheese + chopped lettuce |

Kitchens:
- **BURGER BAR** is one open kitchen with an island.
- **SPLIT SHIFT** has a wall down the middle. Teams pass food across the divider counter, and a gap at the bottom lets a solo chef walk around.

Stars scale with crew size.

## Online co-op (Railway, shared service: `orbyt/server/kitchen.js`)
- **Host-authoritative.** The first chef in a room runs the kitchen simulation. The other chefs send their position and button presses about 15 times a second, and the host broadcasts the full kitchen state 12 times a second. Every item in the game is a single int, so a snapshot is a few small arrays.
- **Lobby** (in `kitchen.js` on the page):
  - Quick Match pairs chefs on the same kitchen and starts automatically.
  - A private room uses a 4-letter code or an invite link (`?kitchen=CODE`).
  - If nobody joins within about 15 s, you cook solo.
- **Leaderboard** (one per kitchen): `/api/kitchen/run`, `/api/kitchen/finish` (checked against the server clock and a points-per-second cap) and `/api/kitchen/board`.

## Layout
```
unity/Assets/Scripts/   Game.cs (rules, orders, net sync, camera), Kitchen.cs (grid, stations, item models),
                        Chef.cs, Bot.cs (autoplay planner), Data.cs (items/recipes/kitchens), UI.cs, Sfx.cs, WebBridge.cs
unity/Assets/WebGLTemplates/OrderUp/   loader page, kitchen.js (lobby + leaderboard), OG card, icons
art/                    OG/icon sources, food + chef renders
```

## Build and deploy
```bash
"C:\Program Files\Unity\Hub\Editor\6000.6.3f1\Editor\Unity.exe" -batchmode -nographics -projectPath unity -executeMethod OrderBuild.WebGL -quit -logFile build.log
npx vercel --prod
```
Dev flags (all require `dev=1`):
- `autodrive=1` lets the bot cook for you without starting a shift. These runs are never submitted.
- `short=1` makes shifts 25 seconds long.
- `fresh=1` resets your save.

The public `bot=1` flag runs a demo.

Assets: Kenney Mini Characters, Food Kit and Furniture Kit (CC0). All audio is synthesized in code.
