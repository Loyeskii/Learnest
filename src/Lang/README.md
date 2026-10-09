# Language packs (`src/Lang`)

Learnest ships two language packs as plain JSON files:

| File | Code | Name | Notes |
| --- | --- | --- | --- |
| `en.json` | `en` | English | Reference pack. Its key set and key order are the contract. |
| `zh-CN.json` | `zh-CN` | 简体中文 | Simplified Chinese. |

Both files are compiled into `Learnest.exe` as embedded resources (`<EmbeddedResource Include="Lang\*.json" />`
in `Learnest.csproj`), so the UI always has labels even if every file next to the executable is deleted.
Additional packs can be dropped in or imported at runtime without rebuilding the app.

The user-facing, bilingual version of this guide is [docs/language-packs.md](../../docs/language-packs.md).
If you only want to translate Learnest, read that document instead of this one.

## File format

UTF-8, **no BOM**, no comments, no trailing commas. CRLF and LF both work. The parser is `Core.JsonValue`,
which accepts strict JSON only.

```json
{
  "code": "zh-CN",
  "name": "简体中文",
  "englishName": "Simplified Chinese",
  "author": "Learnest",
  "version": "1.0.0",
  "rtl": false,
  "strings": {
    "app.tagline": "A gateway to everyday management",
    "nav.dashboard": "总览"
  }
}
```

Top-level fields:

| Field | Required | Meaning |
| --- | --- | --- |
| `code` | yes | Language tag, e.g. `fr`, `pt-BR`, `zh-TW`. This is the identity of the pack and the file name it is imported as. Missing → `und`. |
| `name` | yes | Native name shown in Settings → Language, e.g. `Français`. Missing → the code. |
| `englishName` | recommended | English name shown after the native name, e.g. `French`. |
| `author` | recommended | Shown in metadata; use your own name for community packs. |
| `version` | recommended | Free-form string, defaults to `1.0.0`. Bump it when you revise the pack. |
| `rtl` | optional | `true` for right-to-left scripts. Defaults to `false`. |
| `strings` | yes | Flat object: key → value, both strings. No nesting, arrays or `null`. |

Rules for `strings`:

- Values must be plain strings. A missing key or an empty string is treated as "not translated" and falls back.
- Keep the `{0}`, `{1}`, `{2}` placeholders. You may move them anywhere in the sentence, but do not renumber,
  remove or duplicate them — `Loc.T(key, args)` calls `string.Format` on the value.
- No literal newlines inside values; use spaces.
- Escape `"` as `\"` and `\` as `\\`.
- Do not invent or rename keys. The key list in `en.json` is the interface.

## Authoring a new pack

1. Copy `src/Lang/en.json` to `src/Lang/<code>.json` (for example `fr.json`).
2. Set `code`, `name`, `englishName`, `author`, `version` and `rtl`.
3. Translate every value in `strings` **without changing any key, adding keys, or reordering them**.
4. Keep the product identity: `app.name` stays `Learnest`, and `app.tagline` keeps the English tagline
   `A gateway to everyday management` in every language.
5. Save as UTF-8 **without BOM** and verify the file parses:

   ```powershell
   python -c "import json,io; json.load(io.open(r'src\Lang\fr.json', encoding='utf-8'))"
   ```

   Then confirm the key set matches the reference pack exactly (order included):

   ```powershell
   python -c "import json,io; en=json.load(io.open(r'src\Lang\en.json',encoding='utf-8'))['strings']; x=json.load(io.open(r'src\Lang\fr.json',encoding='utf-8'))['strings']; print(len(en), len(x), list(en)==list(x))"
   ```

   The two lengths must match and the last value must print `True`.

### Making a pack built in

A file added to `src/Lang` is already embedded by the `Lang\*.json` glob, but only packs listed in
`LanguagePack.BuiltInPacks()` (`src/Services/LanguagePack.cs`) are loaded before the language folders are
scanned. To make a new pack a first-class built-in, add it there next to `Learnest.Lang.en.json` and
`Learnest.Lang.zh-CN.json`. Otherwise just ship the `.json` next to the executable in a `Lang` folder, or let
users import it — no code change is required.

## Where external packs are found

At startup and after every rescan, `Loc.ScanExternalPacks` looks for `*.json` in this order:

1. `<folder containing Learnest.exe>\Lang`
2. `<folder containing Learnest.exe>`
3. `<workspace root>\languages` (only when a workspace is open)
4. `<workspace root>`

Later folders win: a pack found later replaces an earlier pack with the same `code`. Any file with fewer than
20 entries in `strings` is ignored, so unrelated JSON files in those folders are harmless. A pack whose `code`
matches a built-in `code` replaces it for the session and keeps its built-in flag.

## Importing a pack in the app

Settings → Language → **Import language pack** (`Loc.Import`) copies the chosen file to:

- `<workspace root>\languages\<code>.json` when a workspace is open, otherwise
- `<folder containing Learnest.exe>\Lang\<code>.json`.

The target name is derived from the pack's `code` through `AppPaths.SafeFileName`, so a code containing
characters that are invalid in file names is stored with `_` substitutes. An import fails when the file does not
parse, has no `strings` object, or holds fewer than 20 entries. Use Settings → Language → **Reload languages**
(`Loc.Rescan`) to re-read the folders after copying a file in by hand.

## Lookup and fallback order

`Loc.T(key)` resolves in this order:

1. the selected pack,
2. a pack whose code shares the same primary subtag (so `zh-Hans`, `zh-TW` and `zh-SG` can fall back to `zh-CN`),
3. the `en` pack,
4. the key itself, so an untranslated label is always visible and easy to report.

"Sensitive" languages are matched case-insensitively; when no preference is stored the pack follows the Windows
UI language, falling back to English.

## Maintaining the key list

When you add a UI string:

1. add the key to `en.json` **and** to every other pack in `src/Lang`, in the same position,
2. translate it in `zh-CN.json` as well, even if the value temporarily keeps the English wording,
3. never remove or reorder existing keys — external packs would silently fall back to English.

## License

Language packs contributed to this repository are covered by the project licence. Keep the `author` field
accurate so translators get credit.
