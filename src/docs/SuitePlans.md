






# ===============================================
# Meowcow Core
# ===============================================

## [ Foundation ]

### CorePathIndex.cs
- Points to file paths so that each mod doesn't have to search for its own directory
- Per Module Settings Path
- SuiteSaveManager Save Extras Path
- SuiteSaveManager Saves Path
- SuiteSaveManager Save Backup Path

### MeowCowLogger.cs
- Creates a Logger that every module registers to have its own tagged Log in MelonLoader
- 4 Levels of Log Alert

## [ PLANS ]

## Life Cycle
- Hooks into parts of the games life cycle for other modules to reference for their actions

## Player Game Mode Thing
- Detects if the game is Solo or Coop and how many players there are
- Gets reference by other mods to know what settings to use if there are differences between solo and coop

## UI Element Registry Thing
- Place that holds references to all UI elements of the vanilla game
- For use when building or editing UIs so each module doesnt have to look it up themselves
- Makes it easy for Themes Module to alter shared UI components

## Suite Settings (Meowcow Options)
- Builds the Meowcow settings menu based off of the settings available per module including its own
- Menu built using UI Element Registry
- Main Tabs along the left
- Sub Tabs along the top

## Main Menu
- One of the first things to run
- Skips Logo if enabled
- Skips Disclaimer if enabled
- Adds Meowcow Options to Menu list
- Adds Meocow Suite tag below game logo
- Renames Continue to Load Game
- Creates a new menu list item called Continue (Internally called ContinueLastSave)
- Menu List should be ordered: Continue, Load Game, New Game, Settings, Meowcow Settings, Credits, Quit
- If possible background last save render shows all players that participated in the game last if it was Coop

## Suite Save Manager
- New Game button Starts a new game in first available slot
- Continue Button Loads most recent Save
- Load Game opens Save Window
- Supports creating new games past the limit of 5

### Save Window
- Overriden from vanilla
- Built from UI Element Registry
- Top Save Most Recent, Padding, Pinned Saves, Padding, Saves in order of most recent to least recent
- Save slots feature, Organization Name, Save Note, Net Worth, Solo or Coop with the user steam avatars shown, Last Played, Date created, Difficulty Mode, Version, Save Slot Name, Pin Button, Import Button, Export Button, Delete Button
- Supports displaying saves over the limit of 5

# ===============================================
# Meowcow Mirror
# ===============================================

## [ PLANS ]
- Override create character menu and build custom menu
- Override Barber Shop and Tattoo Shop with custom menu
- One single menu style for editing character customization
- Menu built from Core UI Registry
- Context system gives context of the conditions the menu was open in
- Context denotes what is displayed and available to customize and if there is a cost
- Features a Cost panel on the side that displays all selected changes with the ability to change cost type if more options are available
- Several camera character preview settings to choose from
- Character preview mouse/keyboard rotation zoom and pan
- Limits set per camera preview mode
- Support for conditional and extra costs based off what character currently has
- Support for more customization to add later
- Support for more fine tuning of customization options
- History tab shows last few changes made for easy reverting
- Support for saving and loading customization presets

# ===============================================
# Meowcow Themes
# ===============================================

## [ PLANS ]
- Ability to alter layout of menus and other UI elements
- Uses UI Registry from Core
- Changing scale, font, color, position, visibility, alignment, anchor, of UI elements
- Save and Load themes in menu
- Themes menu draws overtop other menus and is dragable so it can be opened with the desired UI edit visible