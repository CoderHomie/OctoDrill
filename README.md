# OctoDrill

### Course Information

**Southern Illinois University Edwardsville**  
**Course:** CS 382 — Game Design, Development, and Technology  
**Authors:** Ryan Bender, Ethan Hoeman, Jacob Stephens, Lorenzzi Zappa

## Project Overview

**OctoDrill** is a **grid-based arcade game** built for the semester. The player controls **Doc** on a discrete grid, stepping on trash tiles to reveal coral underneath. **Sharks** and **urchins** (with **spike** projectiles) increase pressure over time; **whirlpools** and **nets** add environmental risk on the grid. When all clearable trash is gone, a **goal tile** appears; reaching it starts the **next round** with stronger spawns, while **score**, **level**, **lives**, and **high score** (persisted for WebGL) keep the run meaningful.

### Links


| Resource                | URL                                                                                  |
| ----------------------- | ------------------------------------------------------------------------------------ |
| **Play (WebGL)**        | [Play in browser](https://coderhomie.github.io/OctoDrill/)                                     |
| **Source / submission** | *[https://github.com/CoderHomie/OctoDrill](https://github.com/CoderHomie/OctoDrill)* |


---

### Scope and Deliverables

- **Playable loop:** movement, reveal, hazards, enemies, round advancement, lives, game over, restart / menu return  
- **Scenes:** main menu, how-to-play, credits, and core gameplay (`OctoDrill`)  
- **Distribution:** WebGL build intended for browser play   
- **Presentation:** HUD layout, optional full-screen gradient overlay for readability and mood

### Features

 - **Tile-by-tile exploration** - Move on a discrete grid with smooth optional stepping. Reveal the map by clearing trash-covered cells and watch the coral underneath appear.

 - **Round goals and escalating pressure** - When every clearable trash tile is gone, a drill goal spawns somewhere on the grid. Touch it to advance: the grid resets, enemies ramp up, and you keep your run going.

 - **Environmental hazards** - Each new round can mix in **whirlpools** that warp Doc to another safe spot and **nets** that take a life if stepped on. Placement stays fair, so the grid never becomes unwinnable.

 - **Enemy arrive with warning**
     - **Sharks** streak in from the sides on random rows.
     - **Urchins** drift in from an edge, pause, then launch **spike bursts** in multiple directions. Spawns show a brief warning before the threat arrives.

 - **Lives, respawns, and comeback** - Limited lives; lose one, and you respawn at the round’s start with enemies cleared. Run out, and it’s game over. Clearing every third level earns an extra life.

 - **Score and persistence** - Points for clearing trash, a visible level counter, and a **high score** saved locally (including in WebGL) so you can beat your best run.

 - **Menus and presentation** - Main menu, how-to-play, and credits scenes; full-screen blue gradient overlay for underwater mood without hiding the action.

---

## Controls


| Input                      | Action       |
| -------------------------- | ------------ |
| **WASD** or **Arrow keys** | Move (4-way) |
| **Gamepad** left stick     | Move (4-way) |


Uses Unity’s **Input System** (keyboard/gamepad).

---

