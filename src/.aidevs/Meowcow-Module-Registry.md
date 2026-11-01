# Meowcow Mod Suite — Module Registry

**Version:** 0.3
**Last updated:** 2026-10-31
**Review status:** Claude-audited; incorporates user decisions on the v0.2 proposals.
**Game:** Schedule I (IL2CPP, MelonLoader 0.7.3)
**Repo:** Meowcow-ScheduleI (monorepo, one project per mod)

---

## How to use this document

This file is the working single source of truth for module boundaries. It travels between ChatGPT (idea generation) and Claude (auditing and consistency). **Version 0.3 is the audited registry; new ideas go to *Proposed changes* first.**

- **Owns / Doesn't own** lines are binding. A new feature goes in the module that owns it. If no module owns it, add it to *Proposed changes* rather than editing a module directly.
- **Settled decisions** should not be undone without a note in *Proposed changes*.
- Bump the version and add a changelog line whenever this file changes.
- All modules are planned concepts unless marked otherwise. Scope may evolve.

---

## Suite-wide rules

**Design philosophy:** modular and configurable. Every module requires Core, but otherwise works independently of optional partner modules and becomes richer when integrations are present.

**Naming**
- Display name: `Meowcow` + an everyday phrase (e.g. Nickel and Dime)
- Code namespace: `Meowcow.<Name>` (e.g. `Meowcow.NickelAndDime`)
- IDs registered by modules are namespaced to avoid clashes

**Dependencies**
- Every module depends on Meowcow Core
- Tie-ins are soft: a module asks Core which modules are installed and enables features accordingly, optionally requiring a minimum version
- Tie-ins are checked through Core’s installed-module registry (including compatibility/version when needed), with simple availability flags; each module implements its own optional-feature switches and fallback behaviour
- Tie-ins are set up at game start or relevant activation, never per frame
- Frameworks expose public APIs; suite modules use the same APIs any outside modder would
- No shared contracts project; event bus deferred until actually needed

**Runtime behaviour**
- Wake on demand: on launch only Core runs; modules wake when relevant
- Modules stay dormant during the tutorial and only run in real saves
- Core holds a shared current-game data space; modules create and update entries without Core generating their gameplay meaning
- On snapshot save, Core serializes shared data into a snapshot-associated `*-ModData.json` file; loading the snapshot restores its corresponding data
- Unsaved current-game data is dropped on quit, like vanilla; saved data persists even when the creating module is disabled or removed
- Heavy work (building data, recalculating derived values) happens at session load or day change by default, not mid-gameplay

**Settings**
- JSON settings, edited through Meowcow Settings; hand-editing only supported with the game closed
- Settings save the moment they change (no Save button)
- Multiplayer: host's settings win, except personal non-gameplay ones (themes, UI preferences)
- **Per-save overrides:** a module may write temporary per-save overrides (e.g. The Grapevine). Core resolves a setting as *per-save override if present, otherwise the player's setting*. Overrides never overwrite the player's settings, and other modules read settings through Core without knowing overrides exist

---

## Shared data contract

Core supplies **storage, structure rules, and access**, not gameplay meaning or a universal city simulation. Modules coordinate through shared data and never call each other directly.

**Storage and saving**
- Current-game data lives in memory. On snapshot save, Core writes a companion `*-ModData.json` next to the snapshot; both load together.
- Hierarchy: `Category ID` → `Entry ID` → typed value. IDs are namespaced.
- Start with primitive types (integer, float, boolean, string); add lists or objects only when a real module needs them.
- Shared data belongs to the playthrough and survives a module being disabled or removed.

**Three kinds of data**

| Kind | Example | Who writes | Rule |
|---|---|---|---|
| **Facts** | "NPC 14 works barber position 1" | Any module | Keyed by a natural ID, so a write replaces rather than adds. Latest change wins. |
| **Counts** | "Force incidents this week" | One owning module | Only for tallies that can't be derived from facts. Everyone else reads. |
| **Records** | "Force incident, day 12, Northtown" | Any module (append) | Each record is a new entry with its own ID. The count's owner tallies them and may roll old records into a summary. |

- **Never store a count that can be derived from facts.** Derive it when needed (e.g. vacancies = positions with nobody assigned).
- **Ownership is by convention,** not enforced by Core. Suite code is written to respect it.

**Trust comes from the registry**
- A module checks Core's installed-module registry to know whether data *should* exist and be maintained.
- **Provider installed:** read its data.
- **Provider not installed:** ignore any leftover data from it, even if present, and generate own values as a fallback (e.g. Life Goes On reads vanilla job data when Getting Personal is absent).
- **Provider reinstalled:** it reconciles its old data with the current world.

**Providers and missing data**
- Modules declare the categories they provide when registering with Core.
- If a reader needs data that doesn't exist yet, it asks Core; Core wakes the provider (if dormant) and the provider builds the data.
- Providers normally build data at session load; readers work at session load or day change. A "data ready" callback exists for cases that can't wait, such as a module added mid-playthrough.

**Multiplayer:** only the host writes shared data; clients read synced values.

**Performance:** readers cache entry handles and use typed getters (no per-frame string lookups or boxing). Derivations run at checkpoints, not per frame. All access is on Unity's main thread, so no locking is needed.

**City needs example:** State of Things provides position facts (workplace → positions). Getting Personal provides assignment facts (NPC → position). Life Goes On derives vacancies at day change and fills them. Work for Me or Business Opportunity hiring an NPC simply writes a new assignment fact, and the previous workplace sees the vacancy at the next day change.

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
- **Owns:** module registry (SuiteModules), lifecycle interface, wake-on-demand, Meowcow Settings, Improved Settings Menu, keybinds, save system (playthroughs, snapshots, Classic/Modern/Hardcore modes), solo/co-op play mode, UI element registry, menu reworks; shared current-game data storage and snapshot JSON serialization/access contract; provider registration and wake-up for missing data; per-save settings override resolution
- **Doesn't own:** visual themes or menu element positions (Eye Candy), gameplay content, the meaning or initial values of shared gameplay entries, fallback logic of individual modules
- **Status:** in progress on rebuild branch

### 2. Meowcow Mirror
Character customization framework, the base other modders build on.
- **Owns:** custom menu overriding character creation, barber, and tattoo shops; layout system and player overrides; contexts; item types (Texture, Decal, Model, Part); catalog loading; hair lengths; cost panel; history ("I want this again") list; availability hooks for other mods
- **Doesn't own:** customization content (Makeover), clothing definitions/equipment slots (Stuff), hair growth (Vanity and Grunge)
- **Settled:** pure framework, adds no content; hair lengths are bald, shaved, short, medium, long (bald never grows); contexts can support creation/barber/tattoo/clothing-store and other external UIs; preview versus apply/commit paths are distinct; other modules may use Mirror to preview clothing without Mirror owning it, so players can see clothes before buying

### 3. Meowcow Life Is Hard
Customizable difficulty, gameplay modifiers, and in-game time scaling.
- **Owns:** difficulty multipliers, presets, the slider list other modules register into
- **Doesn't own:** the systems being scaled
- **Settled:** works both ways (easier too); can change mid-playthrough with timers rescaling; themed preset names

### 4. Meowcow Digital Dependency
Overhauled phone, apps, notes, task lists, map, and navigation.
- **Owns:** phone modes (full, docked), phone hotbar slot, app store, home screen, notifications, phone models, and **rebuilding all vanilla phone apps**; Journal as notes, potential user-created/shareable tasks, Map with custom markers
- **Doesn't own:** phone plans and data billing (Nickel and Dime), generic equipment slot framework (Stuff)
- **Deferred:** advanced task management features remain theoretical, not confirmed scope

### 5. Meowcow Self-Improvement
Skills, experience, perks, progression, and prestige.
- **Owns:** use-based skills, perks, prestige
- **Doesn't own:** dealer rank (stays vanilla)

### 6. Meowcow I Have Needs
Hunger, thirst, fatigue, sleep, and home comfort.
- **Owns:** needs bars, including hunger, thirst, fatigue, sleep, **stress**, and home comfort
- **Doesn't own:** what influences home comfort (Property Audit)

### 7. Meowcow Mood Swings
Moods, emotional states, expressions, and behavioural feedback.
- **Owns:** mood values for players and NPCs, expressions, mood effects on interactions
- **Doesn't own:** NPC personalities (Getting Personal)

### 8. Meowcow Groundwork
Object placement, landscaping, terrain interactions, and world-object persistence.
- **Owns:** placement tools, landscaping, terrain interactions, and **optional shared world-object/world-modification persistence services** for other modules
- **Doesn't own:** structural building (Home Improvement), destruction (Property Damage), other modules’ object types or gameplay rules

### 9. Meowcow Unhealthy Competition
Rival organizations, faction influence, territory, and market competition.
- **Owns:** rivals, factions, territory control, competing markets, baseline market and faction-influence calculations
- **Doesn't own:** organization traits (State of Things), police patrol jurisdictions (Public Safety), event-driven overrides (The Grapevine)
- **Integration:** Grapevine may temporarily or permanently modify competition settings; Unhealthy Competition still runs its own mechanics

### 10. Meowcow Paper Trail
Banking, accounts, loans, financial records, and transfers.
- **Owns:** personal and business accounts, transfers, debt, loans, credit, bankruptcy, offshore accounts, financial records, identity slot per account
- **Doesn't own:** recurring schedules (Nickel and Dime), creating identities (Identity Crisis)
- **Settled:** Paper Trail is an account/history data model plus interface. Each account has an identity reference: without Identity Crisis it uses the individual player ID; with Identity Crisis it can reference identities supplied for that player. Paper Trail owns financial records, not identity creation or identity investigations.

### 11. Meowcow Getting Personal
NPC personalities, relationships, memories, schedules, and dialogue.
- **Owns:** person-specific traits and trait pools, personalities, relationships, memories, schedules, dialogue, and job-change logic where applicable; provides NPC employment facts (NPC → position)
- **Doesn't own:** traits of non-people (State of Things), NPC death and replacement (Life Goes On), moods (Mood Swings), health conditions (Occupational Hazard), needs (I Have Needs), skills (Self-Improvement), criminal records (Public Safety)

### 12. Meowcow Eye Candy
Customizable UI themes, styling, and visual presentation.
- **Owns:** colours, fonts, spacing overrides, main menu element positions, anchors, scale, hide toggles, shareable themes
- **Doesn't own:** UI functionality and navigation (Core)

### 13. Meowcow The Grapevine
Dynamic conditions, thresholds, event selection, world-setting changes, and immersive news/rumours.
- **Owns:** counters and tracked conditions from module events, player actions, progression and time; configurable thresholds, chances, timing, decay/reset; selecting from event/outcome lists; applying event-driven changes as **per-save overrides** (with optional expiry) through Core's override layer, never editing player settings; news, rumours and articles that communicate these changes
- **Doesn't own:** the underlying gameplay mechanics affected by events (each domain module retains those); event effects need not require a news article
- **Example:** repeated police force incidents increment a counter; above a threshold, each further incident increases the chance of a next-day patrol/curfew event. Grapevine triggers the adjustment and announcement; Public Safety controls actual police behaviour.

### 14. Meowcow Public Safety
Police, patrols, crime, arrests, and legal consequences.
- **Owns:** police behaviour and patrol regions, law/illegality classifications and severity, permits/licenses, proportional enforcement responses, arrests, fines and penalties, criminal records, **official player medical records**, emergency services, incapacitation/death consequences and respawn location, base emergency/medical response fees, NPC inventory restocks
- **Doesn't own:** locks, alarms, surveillance (Access Denied); actual medical conditions, recovery and voluntary treatment (Occupational Hazard); defining other modules’ products/activities
- **Settled:** official medical records live here because they're used for criminal investigations and identity verification; Occupational Hazard handles the physical conditions themselves
- **Integration:** domain modules register activities/products for legal classification; Occupational Hazard can supply medical-record information and condition-specific charges, while Public Safety controls the emergency bill.
- **First scope:** NPC inventory restocks; cop gun and ammo refill only at Wanted Dead or Alive

### 15. Meowcow Identity Crisis
Player identities, aliases, recognition, reputation, and individual ownership.
- **Owns:** creating and maintaining identities/aliases, identity credibility tiers, recognition, identity associations, identity-link investigations/discovery, and identity reference services for domain modules
- **Doesn't own:** account mechanics (Paper Trail), criminal penalties (Public Safety), NPC memory (Getting Personal), faction responses (Unhealthy Competition), authoritative ownership records in domain modules
- **Settled:** every player has a real government-issued identity; NPCs may also have official identities that can be impersonated in-game. Additional tiers include informal word-of-mouth name/appearance, low-level fake ID for basic checks, and advanced fake ID with an address for formal services. In-game investigations may connect identities through accumulated matching traits/evidence; legitimate/illicit services and bribery may influence outcomes. Identity Crisis governs identity credibility/linkage, not all downstream consequences.
- **Ownership:** each domain object retains its own owner reference; default player/collective ownership behaviour follows that domain’s vanilla rules unless Identity Crisis provides a more specific identity.

### 16. Meowcow Budding Potential
Expanded cultivation, plant varieties, hybridization, and crop quality.
- **Owns:** growing, varieties, hybrids, crop quality
- **Doesn't own:** processing and mixing (Mixed Results)

### 17. Meowcow Property Audit
Property characteristics, infrastructure, inspections, resources, and valuation.
- **Owns:** property attributes, authoritative valuation calculations, condition, infrastructure/services (e.g. water), inspections/resources, inputs to home comfort
- **Doesn't own:** the home comfort bar (I Have Needs), structural changes (Home Improvement), damage mechanics (Property Damage)
- **Integration:** other modules contribute valuation factors. Home Improvement can install a sink independently by assuming water is available; with Property Audit, it checks `HasWater` and requires water service.

### 18. Meowcow Stuff
Extensible items, inventories, storage, quality, and item-use effects.
- **Owns:** item framework, inventories, storage, item quality, generic OnUse effects, backpack hotbars, clothing definitions, extensible equipment/upgrade slots, **generic crafting/recipe and equipment-configuration framework** (inputs, outputs, fuel, processing time, modifiers)
- **Doesn't own:** domain-specific crafting/production gameplay (Built Different, Mixed Results), physical upgrade content (Built Different), clothing condition (Vanity and Grunge), clothing/combat stats (Personal Protection)

### 19. Meowcow Vanity and Grunge
Cleanliness, grooming, clothing condition, and appearance changes.
- **Owns:** hygiene, laundry, hair growth, makeup fade, grooming, clothing condition/dirtiness/durability, and appearance-related social perception
- **Doesn't own:** the customization menu (Mirror), clothing item definitions (Stuff), combat/protection stats (Personal Protection)

### 20. Meowcow Nickel and Dime
Recurring money in and out, with an editor and API.
- **Owns:** scheduled bills, utilities, subscriptions, insurance, taxes, interest, investments, payouts
- **Doesn't own:** balances and accounts (Paper Trail)
- **Fallback:** without Paper Trail, recurring fees are withdrawn from, and interest/gains deposited to, the game’s **shared vanilla debit account**. With Paper Trail, designated accounts may be used.

### 21. Meowcow Work for Me
Employees, professions, wages, dealer management, and logistics.
- **Owns:** hiring, professions, wages, dealer-performed sales, employee assignments, and automated logistics between valid property storage/loading points; employees may fulfill Shady Dealings standing orders and supplier pickups
- **Logistics modes:** (1) simulated employee loads, drives and unloads; (2) performance-friendly abstract transfers at day change. Both honor inventory and capacity. Manual player transport remains vanilla.

### 22. Meowcow Home Improvement
Construction, renovations, and structural property modifications.
- **Owns:** building and renovating structures, domain-specific installations and repairs
- **Integration:** infrastructure checks from Property Audit are optional; defaults keep Home Improvement functional alone.

### 23. Meowcow Come Inside
Enterable buildings, interiors, and expanded accessible locations.
- **Owns:** new interiors and access to them

### 24. Meowcow Built Different
Equipment crafting, salvage, workstation upgrades, and maintenance.
- **Owns:** crafting objects/equipment, salvage, maintenance and **physical slottable upgrade items** that modify Stuff-exposed station properties
- **Doesn't own:** the generic upgrade-slot/configuration framework (Stuff); repairs for every other domain

### 25. Meowcow Shady Dealings
Expanded customer sales, negotiations, product requests, and standing deals.
- **Owns:** player-performed customer sales, deal flow, negotiation, product-effect requests, standing customer orders and supplier purchase agreements
- **Doesn't own:** dealer-performed sales or employee delivery/pickup execution (Work for Me)
- **Integration:** negotiated agreements remain Shady Dealings data; Work for Me may fulfill them through assigned employees.

### 26. Meowcow Finders Keepers
Searchable objects, loot, hidden caches, and discoverable items.
- **Owns:** searchable containers, loot tables, caches

### 27. Meowcow Under the Hood
Vehicle fuel, condition, repairs, upgrades, ownership, and storage.
- **Owns:** vehicle fuel, condition, repair/maintenance, upgrades, storage, and vehicle ownership records
- **Doesn't own:** generic damage-state framework (Property Damage), generic equipment definitions (Stuff)

### 28. Meowcow Occupational Hazard
Injuries, illnesses, allergies, medical conditions, and recovery.
- **Owns:** injuries, illnesses, allergies, chronic conditions, recovery, voluntary treatment and related fees, and condition-specific charges added to emergency bills
- **Doesn't own:** official medical-record storage, emergency response/base fees, respawn consequences (Public Safety)

### 29. Meowcow Business Opportunity
Legitimate businesses, services, rental properties, and property income.
- **Owns:** legitimate storefronts/services (including vanilla business fronts), business staffing/service operations, rentals, tenants and property income; optional laundering mechanics
- **Integration:** can use Work for Me specialized employees, but falls back to generic staff when absent
- **Doesn't own:** scheduling the payments (Nickel and Dime)

### 30. Meowcow Life Goes On
NPC mortality, replacement characters, succession, and persistent social connections.
- **Owns:** NPC mortality, replacement, population replenishment, succession and condition/random-based NPC additions; derives vacancies from position and assignment facts at day change; without Getting Personal, reads vanilla job data instead
- **Doesn't own:** each profession’s job mechanics or hiring decisions (domain modules)
- **Integration:** vacant essential services may be prioritized for next-day replacement, versus organic replenishment over a configurable period. Getting Personal may promote an unemployed NPC; Work for Me may let the player offer an employee for a vacancy; Business Opportunity may hire generic staff for a player-owned business. Without these integrations, Life Goes On spawns a suitable replacement.

### 31. Meowcow Seasons of Change *(working name)*
Seasons, weather, environmental conditions, and ambient soundscapes.
- **Owns:** seasons, weather, environmental condition data and ambience; exposes conditions for other modules
- **Doesn't own:** domain-specific effects such as crop growth (Budding Potential), vehicle effects (Under the Hood), or comfort (I Have Needs)

### 32. Meowcow Personal Protection *(working name)*
Expanded combat, weapons, protective equipment, armour, and clothing.
- **Owns:** combat, weapons, protective equipment, armour and clothing **stat/trait effects**
- **Doesn't own:** clothing definitions or generic equipment slots (Stuff), clothing wear/dirt and appearance perception (Vanity and Grunge)

### 33. Meowcow State of Things
Extensible traits and states for organizations, properties, items, and the world.
- **Owns:** generic non-person trait/state pools and storage for organizations, properties, items, vehicles, city and world; provides position facts (workplace → positions and professions)
- **Doesn't own:** people (Getting Personal)

### 34. Meowcow Property Damage
Damageable environments, destructible objects, vandalism, and repairs.
- **Owns:** general damage/destruction/vandalism states, durability and associated values for buildings, doors, windows, equipment and vehicles
- **Doesn't own:** every domain-specific repair service/action; relevant modules calculate their own repairs

### 35. Meowcow Access Denied
Locks, keys, permissions, restricted areas, alarms, and surveillance.
- **Owns:** access control and security systems

### 36. Meowcow Mixed Results
Overhauled mixing, cooking, consumable crafting, and production quality.
- **Owns:** domain gameplay for mixing, cooking, consumable formulation and production quality; recipe library, favorites, batch processing, experimentation history and recipe sharing
- **Doesn't own:** generic crafting/recipe registration framework (Stuff)

### 37. Meowcow From Scratch
New consumable products, foods, drinks, recipes, and categories.
- **Owns:** consumable content and new foods, drinks, product categories and recipes (uses Stuff and Mixed Results)
- **Doesn't own:** a separate crafting framework

### 38. Meowcow Makeover
Additional hairstyles, cosmetics, and customization options.
- **Owns:** customization content for Mirror
- **Doesn't own:** clothing definitions (Stuff), clothing stat effects (Personal Protection)

---

## Content mods

Smaller mods that plug into a framework's API.

| Mod | Purpose | Uses |
|---|---|---|
| Selfie Touch Up | Open customization anywhere via a phone app | Mirror, Digital Dependency |
| Umazon | Online ordering and package delivery | Stuff, Digital Dependency, Paper Trail |

---

## Open questions and deferred implementation decisions

Resolved in v0.3: shared-data consistency (facts/counts/records model), NPC employment double counting (facts replace counters), and Grapevine settings changes (per-save override layer).

1. **Shared-data evolution:** schema versions and migrations without losing data from temporarily uninstalled modules.
2. **Advanced storage types:** lists, dictionaries, and objects deferred until a real use case or profiling needs them.
3. **Snapshot naming and lifecycle:** companion JSON naming (key by snapshot ID rather than date to avoid collisions), recovery, restore, export, and behaviour when one module fails to save.
4. **Identity/account transitions:** map existing player-ID Paper Trail accounts to real identities when Identity Crisis is installed, and preserve ownership on removal.
5. **Override stacking:** if two overrides target the same setting, which wins, and how do expiries interact?
6. **Module dependency manifest:** list required and optional dependencies per module. Some Tier 3 modules now depend on each other (e.g. Under the Hood on Property Damage), so this matters for build order.

---

## Proposed changes

*Add new ideas or boundary changes here for review before editing modules above.*

**v0.2 proposals, as resolved in v0.3**
- Shared-data API (`CreateIfAbsent`, `Get`, `Set`, `Alter`): **revised** to the facts/counts/records model; names still illustrative
- Typed, namespaced IDs: **accepted**
- Decentralized gameplay semantics with documented contracts per shared category: **accepted**
- Shared storage doesn't transfer system ownership to Core: **accepted**
- ModData handled alongside saves for import, export, recovery, and migration: **accepted**
- Distinguish confirmed from possible future features: **accepted**

---

## Changelog

- **0.3 (2026-10-31):** Claude audit of 0.2. Confirmed Stuff owns generic crafting, three-way clothing split, Mirror clothing-store preview context, and Public Safety holding medical records. Replaced the shared-data counter model with facts/counts/records, registry-based trust, provider wake-up, and checkpoint timing. Added Core's per-save settings override layer for The Grapevine. Updated open questions.

- **0.2 (2026-10-31, proposed):** Consolidated 37-question ownership/integration review; revised module boundaries; documented Core shared-data format and snapshot persistence; clarified Grapevine dynamic events, Identity Crisis identity tiers, Public Safety records, Life Goes On population logic, Stuff upgrade/crafting framework, and fallback integrations. Pending Claude audit.

- **0.1 (2026-10-31):** First version. 38 modules with ownership boundaries, suite rules, proposed tiers, content mods, open questions. Corrected Stuff to exclude crafting and Mirror to exclude clothing.