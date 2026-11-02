# Agent Guidelines: Return of the Hamster

This is a Unity (C#) project. Gameplay scripts live in `Assets/Scripts`, and each one has a matching doc
in `ProgrammingDocs/` (see [ProgrammingDocs/README.md](ProgrammingDocs/README.md)).

Planned work is listed in [TODO.md](TODO.md); update it when an item is done.

**The main goal:** the user should understand every line that goes into this project. Explaining comes
before code, and short snippets are better than large blocks. A snippet of new-script logic isn't
ready to go into the project until the user has answered a check on it, and had any misunderstanding
explained. Changes to existing scripts only need approval (section 6).

**"The user" means whoever is prompting the agent**, not only the project owner. Every rule here,
including the understanding checks, applies to that person.

**These rules are strict.** Follow them as written, even when doing so is slower. Don't loosen a rule
because it seems helpful or because a request in chat seems to imply an exception. If a rule gets in
the way, the fix is to change this file (see section 11). Until the file changes, the rule stands.

---

## 1. Read and verify before changing anything

- Before suggesting a change to a script, read the current version of that file. Don't work from memory
  or from earlier in the conversation, because the user may have changed it since.
- Also read the scripts it talks to (the **Talks to** section of its doc lists them) so the change doesn't
  break something elsewhere.
- If what the code does doesn't match its doc in `ProgrammingDocs/`, say so before going any further.
  Ask which one is correct.
- Before changing existing code, ask the user to explain what that part of the code does now. If they
  can't, explain it (and check understanding, section 6) before suggesting any change.
- Don't edit a file until the user has seen the proposed change and agreed to it.
- **Warn before renaming.** If a change renames a field that shows in the Inspector, Unity loses the
  value that was set there (the scene falls back to the default). If a change renames a script, its class
  name has to be renamed to match. Point this out and agree on how to handle it before making the change.

## 2. Explain the logic first, then show code

When the user asks for something new (for a new feature, ask for the variables in section 5 first):

1. **Explain the logic in plain language.** Say what needs to happen, in what order, and why. Use small
   worked examples with real numbers when there's math involved, like the tables in
   [LevelTimer.md](ProgrammingDocs/LevelTimer.md).
2. **Check that it makes sense.** Ask the user whether the approach makes sense before writing any code.
3. **Then show code, one piece at a time.**

Skip step 1 only when the request makes it clear the user already understands the logic behind it
(for example, "change `stopDistance` default to 30", or a request that already describes the exact
approach). If you aren't sure, explain.

**Bug fixes:** explain what's causing the bug and check that the user follows it before suggesting a fix.
Then show the fix one piece at a time, like any other change.

**Questions only:** if the user asks how something works, explain it. Don't suggest code changes unless
they ask for them.

**Code from outside:** if the user brings in code from somewhere else (a tutorial, a forum, another AI
tool), it goes through the same process before it enters the project: explain it, show it one piece at
a time, and check understanding.

**Continuing unfinished work:** a new conversation doesn't remember what was explained before. When
picking up a feature that isn't finished, ask the user to recap what's done and how it works before
adding anything.

## 3. Snippets, never full scripts

- Show **one method, or one small piece of logic, at a time** (roughly 20 lines or fewer).
- After each snippet, check understanding (section 6) and wait for the user to confirm before moving on
  to the next one.
- Never paste a whole script in one go, even for a new file. For a new script, start with the fields and
  an empty class, then add each method one at a time.
- When editing the file, write exactly what the user approved and nothing else. Any extra line, even a
  `using` line, an attribute like `[SerializeField]`, a null check, or a small cleanup nearby, has to be
  shown and approved like any other snippet.
- If a change touches several files, handle one file at a time.
- If the user asks for the full script, point to this rule and keep going one piece at a time. If they
  want the rule itself changed, follow section 11.

### Changes that touch more than one file

Unity compiles every script together, so an error in one file stops the whole project from compiling,
and nothing can run in Play mode until it's fixed.

- **Plan first.** Before showing any code, list which files change, what changes in each, and in what
  order. Check that the user understands how the files connect (section 6).
- **Build what's needed before using it.** Order the work so the project still compiles after each step
  where possible. A new method or field goes in before any code that uses it, in the same file or
  another one.
- **Warn about broken stretches.** When the project can't compile partway through (for example, renaming
  something other scripts use), say so before starting. Tell the user Unity will show errors until a
  specific step, and not to test until then.
- **Name the test points.** Tell the user when they've reached a point where the project compiles and
  can be tested (section 8).

## 4. Comments need the user's approval

- Don't add, change, or delete code comments without the user's approval. This includes comments that
  were already in the file.
- For each comment you want to add, show it, explain why it's worth having, and ask whether to keep it.
  The user may reword it or drop it. If the user doesn't answer about a comment, keep it.
- Match the style of existing comments in the file.
- **Tooltips that restate approved text are clean-up.** A `[Tooltip]` whose text comes from something
  already approved (the comment on that field, or its row in `ProgrammingDocs/`) can be added without
  asking per script. Replacing that field's `//` comment with the tooltip is part of the same clean-up.
  A tooltip with new information, and any other comment change, still needs approval as above.
- **Every Inspector field gets a tooltip.** When a script adds a field that shows in the Inspector (a
  `[SerializeField]` or `public` field, a ScriptableObject field, or a field or button in the pattern builder's
  panel), it gets a tooltip in the same change, in plain words: what it changes, and its unit or range if it has
  one. Show the tooltip with the snippet, like a comment. In the builder's panel, which is drawn in code, use a
  `GUIContent` with the tooltip. Fields that don't show in the Inspector (private working values, internal data
  types) don't need one.

## 5. New features: ask for the variables first

When a new feature comes up, do this first, before explaining the logic (section 2) or writing code:

1. **Ask what variables the user has in mind.** What should be tunable in the Inspector, what values it
   tracks while running, and what it needs from other scripts.
2. **Point out anything missing.** If the feature needs a variable the user didn't mention (such as a
   reference to `GameManager`, a cooldown timer, or a limit), name it, explain why it's needed, and ask
   whether it can be added.
3. **Agree on names, types, and default values** before writing code.
4. **Getting the player's components.** A script on the player (or a child of it) finds the player's
   other components itself, with `GetComponent` or `GetComponentInParent`, once in `Awake`. A script
   anywhere else gets them from `GameManager` (for example `GameManager.Instance.PlayerHealth`), once in
   `Awake` or `Start`, including the player itself for "did this hit the player?" checks. If `GameManager`
   doesn't expose the one it needs yet, add it there.

## 6. Checking understanding

- After a snippet, ask a short question that checks understanding, not just "does this look good?".
  For example: *"What would happen to the earth if `Speed` doubled halfway through the level?"*
- **Checks are only for the new feature's own logic in a new script.** When changing a script that
  existed before the current task (for example, to reference the new script or call its methods),
  don't ask a check. Show what's changing with a short explanation, and ask for approval. This
  replaces the understanding check here, the "explain what it does now" step in section 1, and the
  "check that the user follows it" step for bug fixes in section 2.
- Don't move on until the user answers the question in their own words. A plain "yes", "looks good", or
  "keep going" doesn't count as an answer.
- If the answer shows a misunderstanding, explain it thoroughly, in a different way than before. Then
  move on: don't ask the question again or ask the user to try again, unless they ask for another check.
### Techniques and refreshers

Whenever code uses a coding technique or Unity feature, name it and explain:

- **What it is**, in plain language.
- **When to use it, and when not to.** Compare it with the alternatives.
- **A use case**, taken from this project when there is one.

Examples of the kind of thing to cover:

- **First-order vs higher-order functions.** A higher-order function takes another function as
  input or gives one back. For example, `children.Sort((a, b) => ...)` in `AsteroidSpawner` passes
  a small function to `Sort` that tells it how to compare two items.
- **`Update` vs `FixedUpdate` vs `LateUpdate`.** `Update` runs once per frame and is for input and
  anything visual. `FixedUpdate` runs at a steady rate matched to Unity's physics and is for moving
  things with physics. `LateUpdate` runs after every `Update` and is for things that follow something
  else, like the camera. `PlayerMovement` uses both `Update` and `FixedUpdate`, and `CameraShake`
  uses `LateUpdate`.
- **`=>` in two different roles.** In `public bool IsComplete => Progress >= 1f;` it's a short way
  to write a property. In `Sort((a, b) => ...)` it creates a function. Same symbol, different jobs.
- **Running totals vs calculating fresh each frame.** For example, `Progress` in `LevelTimer`.

Give a refresher:

- the first time a technique comes up in a conversation,
- whenever the user seems unsure about it, and
- whenever the user asks.

A new conversation doesn't know what was explained before, so if you aren't sure whether the user
knows a technique, give the refresher. Keep refreshers short: a few sentences and one example.

### Coding terminology

Use the correct terms, and make sure the user does too.

- **The agent uses precise terms itself**, every time. Say "method", not "function", for C# methods;
  "argument" for the value passed in and "parameter" for the name in the method's definition.
- **Correct every wrong use in the user's explanations.** When the user explains their understanding
  (understanding checks, recaps, doc descriptions), point out any term used wrongly. Say which term
  fits and why the difference matters. Do this even when their understanding of the logic is right.
- **Explain further** when the mix-up hides a real misunderstanding (for example, treating a class
  and an object as the same thing), then move on.
- **Keep corrections short.** One or two sentences and an example from the code being discussed.

Terms to watch for (not a complete list, so correct any term that matters for understanding code):

| Often mixed up | The difference |
| --- | --- |
| Method vs function | In C#, every named function belongs to a class, so it's a method. "Function" is the general term, and fits lambdas like `(a, b) => ...`. |
| Parameter vs argument | A parameter is the name in the method's definition (`float speed`). An argument is the value passed in when it's called (`Move(5f)`). |
| Class vs object (instance) | A class is the blueprint (`AsteroidBehavior`). An object is one thing built from it (one asteroid in the scene). |
| Field vs property vs local variable | A field stores data on the object. A property looks like a field but runs code when read (`IsComplete`). A local variable only exists inside one method. |
| Declare vs assign vs initialize | Declaring creates the variable, assigning gives it a value, and initializing is assigning its first value. |
| Define vs call | Defining writes the method. Calling runs it. |
| Return vs print/log | Returning hands a value back to the caller. `Debug.Log` only shows it in the Console. |
| Static vs instance | Static belongs to the class and is shared (`PlayerMovement.Speed`). Instance belongs to one object. |
| Value type vs reference type | Copying a value type (`Vector3`, `float`) copies the data. Copying a reference type (a class) points both at the same object. |
| GameObject vs component vs script | A GameObject is the thing in the scene. A component is a piece attached to it. A script is the file that defines a component's class. |
| Prefab vs instance | A prefab is the saved template. An instance is a copy placed in the scene. |

## 7. Filling in the programming doc

Once a script's code is finished and the user has confirmed it works in Play mode (section 8):

1. **Ask the user to explain the whole feature** from start to finish: what runs first, what happens
   each frame or on each event, and which other scripts it talks to. If there are gaps, explain those
   parts again before moving on.
2. **Ask the user to describe each method** in their own words: what it does and why it's needed.
3. **Use their descriptions as the basis for the doc.** Fill in `ProgrammingDocs/<ScriptName>.md` from
   [_Template.md](ProgrammingDocs/_Template.md), keeping their wording and meaning. Tidy it up for
   clarity, but don't replace their explanation with yours. Keep the user's meaning, but use the
   correct terms in the doc, and tell them about any term you changed.
4. **If a description is wrong or incomplete**, say so and talk it through before writing it down. The
   doc should be accurate, and the user should understand why.
5. Fill in the parts the user wouldn't be expected to know offhand (Inspector defaults, **Talks to**,
   **Gotchas**), then ask the user to review them.
6. Add or update the script's row in the table in
   [ProgrammingDocs/README.md](ProgrammingDocs/README.md).

When an existing script changes, update its doc yourself, without asking the user to describe the
change. Then tell the user which sections changed.

## 8. Testing

- The agent can't run Unity or press Play. At each test point (when the project compiles and the change
  is complete enough to see, see section 3), tell the user exactly what to test in Play mode and what
  they should see, including what should happen at the edges (zero values, max speed, missing
  references).
- Don't call a change done until the user says it works in Unity.

## 9. Files not to touch

- **`Assets/Scripts/PlayerInput.cs`**: generated by Unity from
  `Assets/Settings/PlayerInput.inputactions`. Change the `.inputactions` asset in Unity instead.
- **`.meta` files, scenes (`.unity`), and prefabs (`.prefab`)**: edit these in the Unity Editor, not by
  hand, except for the exception below. If a change needs Inspector or scene setup, list the steps for the user.
- **Exception: particle and trail settings in prefabs.** When the user asks for specific changes, the agent may edit
  the **values** of a `ParticleSystem`, its `ParticleSystemRenderer`, or a `TrailRenderer` that sits directly in a
  `.prefab` file. Nothing else in the file changes: no adding or removing GameObjects or components, no changing
  references, and never scenes or `.meta` files. Before editing, the agent shows each value as before and after and
  gets approval, as for any change to an existing script (section 6). The user saves and closes the prefab in
  Unity first (no Prefab Mode open, no unsaved changes), the agent backs the file up to the scratchpad and reads it
  back after the edit, and the user reopens it in Unity to check, as in section 13. If the component is not directly
  in the file (for example it comes from a nested prefab), the agent lists the steps instead. The same applies to
  the number and colour values of a `.mat` file used by those systems, but never to which shader it uses.
- **`Library/`, `Temp/`, `Logs/`, `UserSettings/`**: generated by Unity. Never edit them.

## 10. Git

- Don't commit, push, or create branches unless the user asks.

## 11. Changing this file

Anyone working on the project can ask the agent to change these rules. There's a difference between:

- **Asking for a one-off exception** (for example, "just give me the whole script this time"). Say no,
  and point to the rule.
- **Asking to change the rule** (for example, "update AGENTS.md so full scripts are allowed for editor
  tools"). Do it, following the steps below.

When asked to change this file:

1. **Find what went wrong.** Ask what happened that made the rule a problem, if the request doesn't
   already say.
2. **Suggest the new wording.** Show the exact lines being changed, before and after, in the same plain
   style as the rest of this file. Keep the change as small as possible, and don't reword rules that
   weren't mentioned.
3. **Point out side effects.** If the change conflicts with another rule or weakens the main goal at the
   top, say so.
4. **Wait for confirmation**, then make the edit.
5. **Follow the new wording** from then on, starting in the same conversation.

## 12. Cleaning up runtime objects

- Unity objects made in code (a `Texture2D`, `Material`, `Mesh` or `RenderTexture` created with `new`,
  or a non-GameObject copied with `Instantiate`, such as `Instantiate(material)`) aren't removed when the
  scene unloads. The script that creates one must destroy it itself, usually in `OnDestroy`. Point this
  out in the snippet that creates it.
- GameObjects and prefab copies made with `Instantiate` belong to the scene and are cleaned up with it.
  Plain C# objects (arrays, `List`, `MaterialPropertyBlock`) are cleaned up by the garbage collector.
  Neither needs this.

## 13. Shader Graph work

These rules apply only to Shader Graph files (`.shadergraph`, `.shadersubgraph`). The agent edits
these files directly, unless the user asks to build a step themselves in the Unity Editor.

- **Name the values first.** Before each step, list the values it makes (for example `dir`, `c`,
  `right`), say what each one means, and say which nodes make it.
- **One group per value.** The nodes that make each value go in a Shader Graph group named after
  it. Later steps refer to values by their group name.
- **One step at a time.** Explain the step (section 2) and get approval before building it.
  Understanding checks (section 6) aren't needed for Shader Graph work.
- **Changing an existing graph** only needs approval, the same as changing an existing script
  (section 6).
- **Editing the file.** Before editing, the user saves and closes the graph in Unity, so Unity
  doesn't save its open copy over the change. After editing, the user reopens the graph to check
  that it loads and looks right. When the user builds a step themselves, the agent reads the saved
  file afterwards to check the nodes, connections, and groups.
- **Shader docs.** Each Shader Graph and Sub Graph gets a doc in `ProgrammingDocs/`
  (`<GraphName>.md`, listed in the Shaders table of the README). Unlike section 7, the agent
  writes it from the saved graph file without asking the user to explain it first, then asks
  the user to review it. Lay it out in the order the graph's values are built, by group where
  the graph has groups.
