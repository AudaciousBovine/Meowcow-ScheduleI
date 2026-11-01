# Meowcow Mod Suite — Module Registry

**Version:** 0.1
**Last updated:** 2026-10-31
**Game:** Schedule I (IL2CPP, MelonLoader 0.7.3)
**Repo:** Meowcow-ScheduleI (monorepo, one project per mod)

---

## How to use this document

This file is the single source of truth for module boundaries. It travels between ChatGPT (idea generation) and Claude (auditing and consistency).

- **Owns / Doesn't own** lines are binding. A new feature goes in the module that owns it. If no module owns it, add it to *Proposed changes* rather than editing a module directly.
- **Settled decisions** should not be undone without a note in *Proposed changes*.
- Bump the version and add a changelog line whenever this file changes.
- All modules are planned concepts unless marked otherwise. Scope may evolve.

---

## Suite-wide rules

**Design philosophy:** modular and configurable. Every module works alone and gets better with others installed.

**Naming**
- Display name: `Meowcow` + an everyday phrase (e.g. Nickel and Dime)
- Code namespace: `Meowcow.<Name>` (e.g. `Meowcow.NickelAndDime`)
- IDs registered by modules are namespaced to avoid clashes

**Dependencies**
- Every module depends on Meowcow Core
- Tie-ins are soft: a module asks Core which modules are installed and enables features accordingly, optionally requiring a minimum version
- Tie-ins are set up at game start, never per frame
- Frameworks expose public APIs; suite modules use the same APIs any outside modder would
- No shared contracts project; event bus deferred until actually needed

**Runtime behaviour**
- Wake on demand: on launch only Core runs; modules wake when relevant
- Modules stay dormant during the tutorial and only run in real saves
- Module progress data is pulled by Core when the game saves
- Unsaved data is dropped on quit, like vanilla

**Settings**
- JSON settings, edited through Meowcow Settings; hand-editing only supported with the game closed
- Settings save the moment they change (no Save button)
- Multiplayer: host's settings win, except personal non-gameplay ones (themes, UI preferences)

---

## Build tiers *(proposed)*

| Tier | Purpose | Modules |
|---|---|---|
| 0 | Infrastructure | Core |
| 1 | Foundation frameworks many others depend on | State of Things, Stuff, Getting Personal, Identity Crisis |
| 2 | Domain frameworks | Mirror, Paper Trail, Nickel and Dime, Property Audit, Digital Dependency, Life Is Hard |
| 3 | Systems built on frameworks | Everything else |
| 4 | Content | Makeover, From Scratch, plus small content mods |

---

## Modules

### 1. Meowcow Core
Shared infrastructure, saves, settings, APIs, and mod management. Also a standalone UI overhaul.
- **Owns:** module registry (SuiteModules), lifecycle interface, wake-on-demand, Meowcow Settings, Improved Settings Menu, keybinds, save system (playthroughs, snapshots, Classic/Modern/Hardcore modes), solo/co-op play mode, UI element registry, menu reworks
- **Doesn't own:** visual themes or menu element positions (Eye Candy), gameplay content
- **Status:** in progress on rebuild branch

### 2. Meowcow Mirror
Character customization framework, the base other modders build on.
- **Owns:** custom menu overriding character creation, barber, and tattoo shops; layout system and player overrides; contexts; item types (Texture, Decal, Model, Part); catalog loading; hair lengths; cost panel; history ("I want this again") list; availability hooks for other mods
- **Doesn't own:** customization content (Makeover), clothing (Personal Protection), hair growth (Vanity and Grunge)
- **Settled:** pure framework, adds no content; hair lengths are bald, shaved, short, medium, long (bald never grows)

### 3. Meowcow Life Is Hard
Customizable difficulty, gameplay modifiers, and in-game time scaling.
- **Owns:** difficulty multipliers, presets, the slider list other modules register into
- **Doesn't own:** the systems being scaled
- **Settled:** works both ways (easier too); can change mid-playthrough with timers rescaling; themed preset names

### 4. Meowcow Digital Dependency
Overhauled phone, apps, notes, task lists, map, and navigation.
- **Owns:** phone modes (full, docked), phone hotbar slot, app store, home screen, reworked apps (Messages, Map), notifications, phone models
- **Doesn't own:** phone plans and data billing (Nickel and Dime), equipment menu (see open questions)

### 5. Meowcow Self-Improvement
Skills, experience, perks, progression, and prestige.
- **Owns:** use-based skills, perks, prestige
- **Doesn't own:** dealer rank (stays vanilla)

### 6. Meowcow I Have Needs
Hunger, thirst, fatigue, sleep, and home comfort.
- **Owns:** needs bars, including the home comfort bar
- **Doesn't own:** what influences home comfort (Property Audit)

### 7. Meowcow Mood Swings
Moods, emotional states, expressions, and behavioural feedback.
- **Owns:** mood values for players and NPCs, expressions, mood effects on interactions
- **Doesn't own:** NPC personalities (Getting Personal)

### 8. Meowcow Groundwork
Object placement, landscaping, terrain interactions, and world-object persistence.
- **Owns:** placement tools, landscaping, persistence of placed objects
- **Doesn't own:** structural building (Home Improvement), destruction (Property Damage)

### 9. Meowcow Unhealthy Competition
Rival organizations, faction influence, territory, and market competition.
- **Owns:** rivals, factions, territory control, competing markets
- **Doesn't own:** organization traits (State of Things)

### 10. Meowcow Paper Trail
Banking, accounts, loans, financial records, and transfers.
- **Owns:** personal and business accounts, transfers, debt, loans, credit, bankruptcy, offshore accounts, financial records, identity slot per account
- **Doesn't own:** recurring schedules (Nickel and Dime), creating identities (Identity Crisis)
- **Settled:** identity slot defaults to the player's identity; Identity Crisis adds more identities

### 11. Meowcow Getting Personal
NPC personalities, relationships, memories, schedules, and dialogue.
- **Owns:** everything about people
- **Doesn't own:** traits of non-people (State of Things), NPC death and replacement (Life Goes On), moods (Mood Swings)

### 12. Meowcow Eye Candy
Customizable UI themes, styling, and visual presentation.
- **Owns:** colours, fonts, spacing overrides, main menu element positions, anchors, scale, hide toggles, shareable themes
- **Doesn't own:** UI functionality and navigation (Core)

### 13. Meowcow The Grapevine
Dynamic news, rumours, social information, and world events.
- **Owns:** news and rumour generation, world event announcements
- **Doesn't own:** the events themselves (reads from other modules)

### 14. Meowcow Public Safety
Police, patrols, crime, arrests, and legal consequences.
- **Owns:** police behaviour, patrol regions, crime tracking, arrests, legal outcomes, NPC inventory restocks
- **Doesn't own:** locks, alarms, surveillance (Access Denied)
- **First scope:** NPC inventory restocks; cop gun and ammo refill only at Wanted Dead or Alive

### 15. Meowcow Identity Crisis
Player identities, aliases, recognition, reputation, and individual ownership.
- **Owns:** creating identities, aliases, recognition, reputation per identity
- **Doesn't own:** account mechanics (Paper Trail)

### 16. Meowcow Budding Potential
Expanded cultivation, plant varieties, hybridization, and crop quality.
- **Owns:** growing, varieties, hybrids, crop quality
- **Doesn't own:** processing and mixing (Mixed Results)

### 17. Meowcow Property Audit
Property characteristics, infrastructure, inspections, resources, and valuation.
- **Owns:** property attributes, value, condition, infrastructure, inputs to home comfort
- **Doesn't own:** the home comfort bar (I Have Needs), structural changes (Home Improvement), damage (Property Damage)

### 18. Meowcow Stuff
Extensible items, inventories, storage, quality, and item-use effects.
- **Owns:** item framework, inventories, storage, item quality, item-use effects, backpack hotbars
- **Doesn't own:** crafting (Built Different, Mixed Results)

### 19. Meowcow Vanity and Grunge
Cleanliness, grooming, clothing condition, and appearance changes.
- **Owns:** hygiene, laundry, hair growth, makeup fade, grooming, clothing condition
- **Doesn't own:** the customization menu (Mirror)

### 20. Meowcow Nickel and Dime
Recurring money in and out, with an editor and API.
- **Owns:** scheduled bills, utilities, subscriptions, insurance, taxes, interest, investments, payouts
- **Doesn't own:** balances and accounts (Paper Trail)

### 21. Meowcow Work for Me
Employees, professions, wages, dealer management, and logistics.
- **Owns:** hiring, professions, wages, dealers, logistics

### 22. Meowcow Home Improvement
Construction, renovations, and structural property modifications.
- **Owns:** building and renovating structures

### 23. Meowcow Come Inside
Enterable buildings, interiors, and expanded accessible locations.
- **Owns:** new interiors and access to them

### 24. Meowcow Built Different
Equipment crafting, salvage, workstation upgrades, and maintenance.
- **Owns:** crafting objects and equipment, salvage, workstation upgrades

### 25. Meowcow Shady Dealings
Expanded customer sales, negotiations, product requests, and standing deals.
- **Owns:** deal flow, negotiation, requests, standing orders

### 26. Meowcow Finders Keepers
Searchable objects, loot, hidden caches, and discoverable items.
- **Owns:** searchable containers, loot tables, caches

### 27. Meowcow Under the Hood
Vehicle fuel, condition, repairs, upgrades, ownership, and storage.
- **Owns:** everything about vehicles

### 28. Meowcow Occupational Hazard
Injuries, illnesses, allergies, medical conditions, and recovery.
- **Owns:** health conditions and recovery

### 29. Meowcow Business Opportunity
Legitimate businesses, services, rental properties, and property income.
- **Owns:** legit business operation, rentals, property income sources
- **Doesn't own:** scheduling the payments (Nickel and Dime)

### 30. Meowcow Life Goes On
NPC mortality, replacement characters, succession, and persistent social connections.
- **Owns:** NPC death, replacements, succession

### 31. Meowcow Seasons of Change *(working name)*
Seasons, weather, environmental conditions, and ambient soundscapes.
- **Owns:** seasons, weather, ambience

### 32. Meowcow Personal Protection *(working name)*
Expanded combat, weapons, protective equipment, armour, and clothing.
- **Owns:** combat, weapons, armour, clothing

### 33. Meowcow State of Things
Extensible traits and states for organizations, properties, items, and the world.
- **Owns:** traits and states for anything that isn't a person
- **Doesn't own:** people (Getting Personal)

### 34. Meowcow Property Damage
Damageable environments, destructible objects, vandalism, and repairs.
- **Owns:** damage, destruction, vandalism

### 35. Meowcow Access Denied
Locks, keys, permissions, restricted areas, alarms, and surveillance.
- **Owns:** access control and security systems

### 36. Meowcow Mixed Results
Overhauled mixing, cooking, consumable crafting, and production quality.
- **Owns:** crafting products, production quality

### 37. Meowcow From Scratch
New consumable products, foods, drinks, recipes, and categories.
- **Owns:** consumable content (uses Stuff and Mixed Results)

### 38. Meowcow Makeover
Additional hairstyles, cosmetics, and customization options.
- **Owns:** customization content for Mirror
- **Doesn't own:** clothing (Personal Protection)

---

## Content mods

Smaller mods that plug into a framework's API.

| Mod | Purpose | Uses |
|---|---|---|
| Selfie Touch Up | Open customization anywhere via a phone app | Mirror, Digital Dependency |
| Umazon | Online ordering and package delivery | Stuff, Digital Dependency, Paper Trail |

---

## Open questions

1. **Shared trait format:** should Getting Personal and State of Things share one trait structure (possibly in Core) so mods query people and things the same way?
2. **Stress:** owned by I Have Needs or Mood Swings?
3. **Equipment menu:** which module owns equipment slots, such as phones? Personal Protection, Stuff, or its own?
4. **Repairs:** Property Damage, Home Improvement, Built Different (maintenance), and Under the Hood all mention repairs. Each likely owns repairs for its own domain, but worth confirming.
5. **Staffing:** Work for Me (employees) and Business Opportunity (staffing) overlap. Suggest Work for Me owns all employees, with Business Opportunity using it.
6. **Ownership records:** Identity Crisis lists individual ownership. Does it record who owns properties and vehicles, or do Property Audit and Under the Hood?
7. **Mirror and Core:** currently leaning toward Mirror depending on Core, despite being meant as a base for outside modders.
8. **Clothing:** confirm Personal Protection as the owner.

---

## Proposed changes

*Add new ideas or boundary changes here for review before editing modules above.*

---

## Changelog

- **0.1 (2026-10-31):** First version. 38 modules with ownership boundaries, suite rules, proposed tiers, content mods, open questions. Corrected Stuff to exclude crafting and Mirror to exclude clothing.