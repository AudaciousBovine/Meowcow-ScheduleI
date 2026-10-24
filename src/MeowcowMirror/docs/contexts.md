# Mirror Contexts

Design reference for how MeowcowMirror opens the character creator in different situations,
what each situation shows, and how changes are paid for. Status: **agreed design, not yet built.**

---

## 1. What a context is

A **context** is a named situation the creator opens in. It decides which options are shown,
where the preview starts from, how changes are committed, and whether they cost anything.

| Id | Opened by | Purpose |
|---|---|---|
| `create` | The vanilla intro | New character: everything, including starting clothing |
| `mirror` | Furniture, SelfieTouchUp, etc. | At-home changes |
| `barber` | Vanilla barbershop (overridden) | Hair, facial hair, eyebrows, hair colour |
| `tattoo` | Vanilla tattoo shop (overridden) | Tattoos, probably piercings |
| `nailsalon` | Content mods | Nails and nail art |
| `surgeon` | Content mods | Face shape, body, weight |
| `dentist` | Content mods | Teeth, gaps, grills |
| `any` | (special) | Option appears in every context |

Mods can register their own contexts. Contexts are just IDs that options declare, so the list can
grow at any time.

## 2. Context definition

| Field | Meaning |
|---|---|
| `id`, `name` | Stable ID and display name (`barber`, "Barbershop") |
| `receipt` | Whether the receipt panel is shown (contexts with costs) |
| `commitLabel` | Text on the commit button: "Done", "Pay for service", "Apply" |

Every registered context starts from the player's **current look**, is **committed by Mirror**,
and can always be **cancelled**.

`create` is the one exception and is internal to Mirror, not something mods can set up: it starts
from the vanilla default preset, commits through vanilla `Done` so the intro continues normally,
and has no Cancel.

## 3. Layout and filtering

- There is **one shared layout** (tabs and options). Contexts never define their own tab order.
- Each option lists the contexts it appears in, or `any`.
- Opening a context **filters** the layout: options not in the context are hidden, and tabs left
  with no visible options are hidden too.
- Clothing is equipment, handled by the vanilla clothing store, so it appears **only in `create`**
  for the starting outfit.

### Proposed defaults for vanilla options (to confirm)

| Option | Contexts |
|---|---|
| Gender, skin colour | `create` |
| Weight | `create`, `surgeon` |
| Hair style, hair colour | `create`, `barber` |
| Facial hair | `create`, `barber`, `mirror` |
| Eyebrows | `create`, `barber`, `mirror` |
| Face (expression) | `create`, `mirror` |
| Facial details | `create`, `surgeon` |
| Eye colour | `create`, `surgeon` |
| Eyelids | `create`, `mirror` (part of the player's expression) |
| Top, bottom, shoes | `create` |

## 4. Opening the creator

- **Intro:** vanilla calls `CharacterCreator.Open()` itself. Mirror catches the creator's `OnOpen`
  and applies the internal `create` context when no other context is pending.
- **Everything else:** a consumer calls `Mirror.Open("barber")`. Mirror sets the pending context,
  opens the creator, seeds the player's current look, and builds the filtered layout.
- **Vanilla shops:** Mirror **always** replaces the vanilla barbershop and tattoo shop UIs with
  the `barber` and `tattoo` contexts. There is no setting to turn this off.

## 5. Committing and the receipt

### Contexts without costs
The main panel's footer has **Cancel** and **Done**, as now (`create` has Done only).

### Contexts with costs
- A **receipt panel** sits on the right edge, in the same style as the main panel.
- One line per changed service, showing what it costs:
  - Money: `Hair style: Mohawk — $30 Cash`
  - Items: `Eyeshadow — 1× Eyeshadow kit`
- Each line has a small **X** on its right. Clicking it **reverts that option** to how it was when
  the session started, and the line disappears.
- A **total** is shown only when at least one line costs money. Item costs have no total; each
  line already shows the item it needs. Anything unaffordable is highlighted.
- The **commit button** ("Pay for service", "Apply") sits at the **bottom of the receipt panel**.
  The main footer keeps only Cancel.
- Nothing is charged until the commit button is pressed.
- If something can't be paid, the button is disabled and shows the reason.

### Change detection
Mirror compares the session's starting look with the current one. Only real changes become lines:
changing something and changing it back costs nothing.

## 6. Costs

### Model
- A **line** is one changed option (or one added/removed item).
- A line has one or more **ways to pay** (alternatives).
- A way to pay is a list of **requirements**.
- A requirement is one of:
  - **Money:** an amount.
  - **Item:** an item ID and quantity, with a mode:
    - `consume`: used up on payment (makeup remover wipes).
    - `require`: must be owned, not used up (a makeup kit).
  - **Custom:** a cost type registered by another mod (e.g. charges on a kit).

### Money
A money requirement **expands automatically** into two ways to pay: cash or card.
Mods never declare that themselves.

### Choosing how to pay
- **Clicking a receipt line cycles** through its ways to pay (Cash → Card → Cash, or
  Eyeshadow kit → Makeup kit). Clicking its **X** reverts the change instead.
- The line shows the method currently selected. Unaffordable methods show in red.
- Lines with only one way to pay don't react to clicks.
- The default is the **first affordable** way, using the player's money preference
  (cash first or card first).

### Example (shape only)
```json
"costs": {
  "barber": {
    "vanilla.hair": {
      "default": [ { "money": 30 } ],
      "values":  { "mohawk": [ { "money": 45 } ] },
      "remove":  [ { "money": 0 } ]
    }
  },
  "mirror": {
    "vag.eyeshadow": {
      "default": [
        { "items": [ { "id": "vag.eyeshadowkit", "qty": 1, "mode": "consume" } ] },
        { "items": [ { "id": "vag.makeupkit",    "qty": 1, "mode": "require" } ] }
      ]
    }
  }
}
```
Each entry in a list is one way to pay.

### Price layering
Later layers override earlier ones:

1. **Vanilla shop prices** (read from the game's shop data)
2. **Mirror defaults**
3. **Content mods**
4. **Player / server overrides**

Prices can be set per context and option, per specific value (Mohawk $45), and for removal
(e.g. tattoo removal).

## 7. Hooks and events

| Hook / event | Purpose |
|---|---|
| **Availability** | Lock a single option with a reason ("Hair not long enough yet") |
| **Before commit** | Veto the commit with a reason |
| **Change diff** | The list of changes, for consumers doing their own pricing or effects |
| **Opened / Cancelled / Applied** | Notifications for other mods (Moodswings, VanityAndGrunge) |

## 8. Settings

| Setting | Default | Meaning |
|---|---|---|
| Full barber catalog in Create | Off | Show every barber hair and facial hair style at character creation |
| Money preference | Cash first | Default payment method for money costs |

Price overrides live in the layered cost files, not in MelonPreferences.

## 9. Needed before the paid contexts work

- **Tattoo rows:** the creator has no tattoo controls. Mirror builds its own, one per body region.
  These are the first non-vanilla rows and will prove out the option API.
- **Wider catalogs:** the barber sells styles the creator never shows, so contexts need to supply
  their own option lists for vanilla selectors.
- **HSV colour picker:** the barber's hue, saturation and brightness picker as a control type.
- **Vanilla shop data:** where shop prices are stored and how the shops take payment.

## 10. Build order

1. Context model, registry and filtering; intro catches `create`; `Mirror.Open(id)`.
2. Barber and tattoo shop override (free at first).
3. Receipt panel, change detection, money costs, cash/card cycling.
4. Item costs, custom cost types, price layering and overrides.
5. Tattoo rows, wider catalogs, HSV picker.