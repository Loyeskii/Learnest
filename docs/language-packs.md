# 语言包 / Language packs

Learnest 的界面文字全部来自**语言包**——一个普通的 JSON 文本文件。内置简体中文和英文，你也可以自己制作或导入其他语言。
Every label in the Learnest interface comes from a **language pack**: an ordinary JSON text file. Simplified Chinese
and English are built in, and you can add or import any other language yourself.

本指南面向使用者；如果你要为 Learnest 开发新的语言包，请看开发者文档 [src/Lang/README.md](../src/Lang/README.md)。
This guide is for users. If you are contributing a new pack to the project, see the developer notes in
[src/Lang/README.md](../src/Lang/README.md).

---

## 简体中文

### 1. 语言包是什么

语言包是一个 `.json` 文件，里面是一份「文字编号 → 译文」的对照表。Learnest 只认识编号（例如 `nav.tasks`），
界面显示哪段文字，完全由当前语言包决定。因此换语言不需要重新安装软件，换一个文件就够了。

内置语言包已编译进程序本体，即使你删掉了程序旁边的所有语言文件，界面也不会变成空白。

### 2. 切换语言

1. 打开 **设置 → 语言**。
2. 在语言列表中选择：
   - **跟随系统**：自动使用 Windows 的显示语言；如果系统语言是 `zh-Hans`、`zh-TW`、`zh-SG` 等，会匹配到简体中文包。
   - 其他选项：直接选择具体的语言包（列表里显示「本地名称（English name）」）。
3. 选择后界面立即刷新，不需要重启。

### 3. 导入语言包

1. 先取得语言包的 `.json` 文件（例如别人分享给你的 `fr.json`）。
2. 打开 **设置 → 语言 → 导入语言包**。
3. 选择该文件并确定。Learnest 会把它复制到语言包文件夹，并立刻出现在语言列表中。
4. 在列表中选中它即可生效，状态栏会提示「已导入语言包：<名称>」。

如果没有打开任何工作区，语言包会放在程序所在目录的 `Lang` 子文件夹里；如果已经打开了工作区，则放在
工作区内的 `languages` 文件夹里，这样语言包会跟着工作区一起备份和迁移。

如果导入失败，通常是文件不是合法 JSON、缺少 `strings` 内容，或者条目太少（少于 20 条会被视为无关文件而忽略）。

### 4. 手动安装与重新加载

你也可以不经过程序，直接把 `.json` 文件复制到下列任意一个文件夹：

| 位置 | 说明 |
| --- | --- |
| 程序目录下的 `Lang` 文件夹 | 推荐；对所有工作区都生效 |
| 程序目录本身 | 也可以，但目录里最好少放其他 `.json` 文件 |
| 工作区内的 `languages` 文件夹 | 只对该工作区生效，随工作区一起搬迁 |
| 工作区根目录 | 同上 |

复制完成后，回到 **设置 → 语言**，点击 **重新加载语言**，程序会重新扫描这些文件夹，新语言包就会出现在列表里。

程序会读取这些文件夹中的 `*.json`。内容少于 20 条的 JSON 会被忽略，所以放错位置的其他 JSON 文件不会造成问题；
但如果两个语言包的 `code` 相同，后扫描到的会覆盖先前的。

### 5. 自己制作一个语言包

1. 在程序目录的 `Lang` 文件夹里找到 `en.json`（英文包），复制一份并改名为目标语言代码，例如 `de.json`。
2. 用记事本、VS Code 等文本编辑器打开，修改开头的几行：

   ```json
   {
     "code": "de",
     "name": "Deutsch",
     "englishName": "German",
     "author": "你的名字",
     "version": "1.0.0",
     "rtl": false,
     "strings": {
       "app.name": "Learnest",
       "app.tagline": "A gateway to everyday management",
       "nav.dashboard": "Übersicht"
     }
   }
   ```

   - `code`：语言代码，决定文件名和匹配规则，务必唯一。
   - `name`：语言的自称，会显示在语言列表里。
   - `englishName`：英文名称。
   - `rtl`：从右到左的文字（如阿拉伯语、希伯来语）填 `true`，其他填 `false`。
3. 逐条翻译 `strings` 里的内容。请注意：

   - **不要改动、删除、增加或调换任何编号（key）**，只改冒号后面的文字。
   - 保留 `{0}`、`{1}` 这类的占位符，位置可以随语序调整，但数量和编号必须一致，否则文字会显示异常。
   - 一条译文要写成一行，不要在文字中间换行。
   - 文字里的双引号要写成 `\"`，反斜杠要写成 `\\`。
   - `app.name` 保持 `Learnest`，`app.tagline` 在各语言中都保留英文原文 `A gateway to everyday management`。
4. **以 UTF-8 编码、不带 BOM 保存**（VS Code 右下角选择「UTF-8」即可；记事本另存为时编码选 UTF-8）。
5. 回到 **设置 → 语言 → 重新加载语言**，选中你的语言包检查效果。

### 6. 常见问题

**界面出现 `nav.tasks` 这样的英文编号？**
说明当前语言包缺少这个编号。补齐该条目，或在语言列表里换回内置语言包。

**某些文字仍是英文？**
该条目在当前语言包里缺失或为空。Learnest 的查找顺序是：当前语言包 → 同一语系的包 → 英文包 → 编号本身。

**导入时提示失败？**
用文本编辑器确认文件是合法 JSON（括号、逗号都正确，最后一项后面不能有多余逗号），并且 `strings` 里至少有
20 条内容。也可以换用 UTF-8 无 BOM 重新保存一次。

**翻译错了想改回来？**
直接编辑 `languages` 或 `Lang` 文件夹里的 `.json` 文件，保存后点「重新加载语言」。语言包与你的待办、考试、
竞赛数据互不影响，可以放心修改。

**升级 Learnest 后语言包会消失吗？**
放在工作区 `languages` 文件夹里的不会；放在程序目录 `Lang` 文件夹里的，请在覆盖安装前先备份。

---

## English

### 1. What a language pack is

A language pack is a `.json` file holding a table of "text id → translation". Learnest only knows the ids
(for example `nav.tasks`); which words appear on screen is decided entirely by the pack that is currently
selected. Changing the language therefore never requires reinstalling the app — swapping one file is enough.

The built-in packs are compiled into the executable, so the interface still has labels even if you delete every
language file sitting next to the program.

### 2. Switching language

1. Open **Settings → Language**.
2. Pick an entry in the list:
   - **Follow system** — uses your Windows display language, and matches Simplified Chinese for `zh-Hans`,
     `zh-TW`, `zh-SG` and similar tags.
   - Any other entry — selects that pack directly (shown as "native name (English name)").
3. The interface refreshes immediately; no restart is needed.

### 3. Importing a pack

1. Get the pack's `.json` file (for example a `fr.json` someone shared with you).
2. Open **Settings → Language → Import language pack**.
3. Choose the file and confirm. Learnest copies it into the language folder and it appears in the list at once.
4. Select it to switch; a message confirms "Language pack imported: <name>".

When no workspace is open the pack is stored in the `Lang` subfolder beside the executable. When a workspace is
open it is stored in that workspace's `languages` folder, so it travels and gets backed up with your data.

An import usually fails because the file is not valid JSON, has no `strings` section, or contains too few
entries (fewer than 20 entries is treated as an unrelated file and ignored).

### 4. Installing by hand and reloading

You can also skip the app and copy the `.json` file into any of these folders:

| Location | Notes |
| --- | --- |
| `Lang` folder beside the executable | Recommended; applies to every workspace |
| The executable's own folder | Also works, but keep other `.json` files out of it |
| `languages` folder inside a workspace | Applies to that workspace only, and travels with it |
| The workspace root | Same as above |

After copying, return to **Settings → Language** and click **Reload languages**. Learnest rescans those folders
and the new pack appears in the list.

The app reads `*.json` from those folders. Files with fewer than 20 entries are ignored, so a stray JSON file in
the wrong place is harmless — but if two packs share the same `code`, the one scanned last wins.

### 5. Making your own pack

1. In the `Lang` folder beside the executable, copy `en.json` (the English pack) and rename it to your target
   language code, for example `de.json`.
2. Open it in Notepad, VS Code or any text editor and change the header:

   ```json
   {
     "code": "de",
     "name": "Deutsch",
     "englishName": "German",
     "author": "Your name",
     "version": "1.0.0",
     "rtl": false,
     "strings": {
       "app.name": "Learnest",
       "app.tagline": "A gateway to everyday management",
       "nav.dashboard": "Übersicht"
     }
   }
   ```

   - `code` — the language tag; it decides the file name and matching, so keep it unique.
   - `name` — the language's own name, shown in the language list.
   - `englishName` — the English name.
   - `rtl` — `true` for right-to-left scripts such as Arabic or Hebrew, `false` otherwise.
3. Translate the entries inside `strings`, one by one. Please note:

   - **Never change, delete, add or reorder the ids (keys).** Only edit the text after the colon.
   - Keep placeholders such as `{0}` and `{1}`. You may move them to fit the grammar, but the count and the
     numbers must stay the same, otherwise the text will render incorrectly.
   - One translation per line — do not break a value across lines.
   - Inside a value, write a double quote as `\"` and a backslash as `\\`.
   - Keep `app.name` as `Learnest`, and keep `app.tagline` as the English tagline
     `A gateway to everyday management` in every language.
4. **Save as UTF-8 without a BOM** (in VS Code pick "UTF-8" in the status bar; in Notepad choose UTF-8 in the
   Save as dialog).
5. Go back to **Settings → Language → Reload languages** and select your pack to check it.

### 6. Troubleshooting

**The interface shows ids such as `nav.tasks`.**
The selected pack is missing that entry. Add it, or switch back to a built-in pack in the language list.

**Some text is still English.**
That entry is missing or empty in the current pack. Learnest looks up in this order: selected pack → a pack of
the same language family → the English pack → the id itself.

**The import reports a failure.**
Open the file in a text editor and check that it is valid JSON (braces and commas correct, no trailing comma
after the last entry) and that `strings` holds at least 20 entries. Re-saving as UTF-8 without BOM often fixes it.

**I made a mistake and want to undo it.**
Edit the `.json` file in the `languages` or `Lang` folder directly, save, then click **Reload languages**.
Language packs are separate from your to-dos, exams and competitions, so editing them cannot damage your data.

**Will a Learnest update remove my pack?**
Packs in a workspace's `languages` folder are safe. If yours lives in the `Lang` folder beside the executable,
back it up before installing an update over the top.
