# Classes
The main stuff happens in `MapChunkDatabase`, which derives from `DatabaseWithFixedDS`.
`Chunk` also has a little bit (`mapColors`, `updateFullMap()`, and `GetMapColors()`).

# Bugs in the Game
## `updateFullMap()`
In `updateFullMap()`, `index2` never gets updated in the loop,
meaning the block values retrieved keep repeating near the heightmap values, while the water values actually go down.

# Miscellaneous Notes
## Area covered by the player
The players covers the map in a 9x9 chunk square around themselves.
That is, the chunk the player is current in, plus 4 chunks in either direction, forming a square.
The size of this square is hardcoded in `MapChunkDatabase.Add()`.
Interestingly, when in editor mode, the area covered is 15x15 instead of 9x9.
