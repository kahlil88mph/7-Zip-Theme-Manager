# 7-Zip Theme Manager

Changes the look of [7-Zip](https://www.7-zip.org/): the file type icons Windows shows for archives, the toolbar buttons in 7-Zip File Manager, and the icons of 7-Zip's own programs.

It is a new, open-source program that reads the theme folders used by the original 7-Zip Theme Manager (7zTM) and its 3.5 remake. It does not contain code from 7zTM or its remakes; the program icon is the palette icon of the original 7zTM. It starts at version 4.0, so it sorts after the original 7zTM (up to 2.x) and the 3.5 remake.

## Features

- **Fast:** pictures are swapped with Windows' own resource functions in a single pass, with no external tools such as Resource Hacker. A theme applies in well under a second.
- **Works with current 7-Zip:** reads which file type icons the selected 7-Zip's `7z.dll` actually has, and shows under the theme list which of them a theme is missing and which theme icons that 7-Zip doesn't use. Nothing is ever written to `theme.ini`.
- **Three kinds of theme:** file types, toolbar buttons, and program icons (7-Zip File Manager, 7zG and the self-extracting archive modules).
- **Forgiving:** a theme can leave out pictures; 7-Zip keeps its current picture for those. Alternative file names such as `squashfs.ico`/`sq.ico` and `zstd.ico` are accepted.
- **Safe:** each file is patched as a copy, checked, then swapped in. The first original of each 7-Zip file is saved, and **Restore saved 7-Zip files** puts it back.
- **Works while 7-Zip is in use:** if Explorer has `7z.dll` loaded, the old file is moved aside and removed later.
- **Portable:** keeps its themes next to the program, and asks for administrator rights only when the 7-Zip folder needs them.
- **Runs on Windows XP SP2 up to Windows 11.** Built for .NET Framework 2.0–3.5 and also runs on 4.x, so Windows 7, 8, 10 and 11 need nothing installed. Windows XP needs .NET Framework 2.0 or later.

## Using it

1. Put `7zTM.exe` and `7zTM.exe.config` in the folder that holds your `FileType`, `ToolBar` and `Icons` theme folders. The program always reads themes from its own folder.
2. Start it. The top line shows whether 7-Zip was detected and which version. For a portable 7-Zip, click the folder button and choose its folder.
3. Pick a tab and a theme, and click **Apply**. Restart 7-Zip File Manager to see new toolbar buttons. Windows may need a sign-out, or an Explorer restart, before it shows new file type or program icons everywhere.

### Command line

```
7zTM.exe [--7zip <folder>] [--filetype <theme>] [--toolbar <theme>] [--icons <theme>] [--restore]
```

`<theme>` is the theme's folder name or the name in its `theme.ini`.

## Theme format

Themes are folders, compatible with 7zTM:

```
FileType\<theme>\theme.ini, preview.jpg, 7z.ico, zip.ico, ...
ToolBar\<theme>\theme.ini, preview.jpg, 48x36\Add.bmp ... Info.bmp, 24x24\Add.bmp ... Info.bmp
Icons\<theme>\theme.ini, preview.jpg, 7zFM.ico, 7zG.ico, 7zSFX.ico, 7zConSFX.ico, 7zSsfx.ico, 7zS2sfx.ico
```

The preview is 430x280 and may be `preview.png` or `preview.jpg`. When a theme has both, `preview.png` is shown; keep `preview.jpg` beside it if the theme should also show a preview in the original 7zTM, which reads only JPEG.

`theme.ini`:

```ini
[Theme]
name=My Theme
author=Your Name
licence=CC BY 4.0
website=https://example.com
```

File type icons, in 7-Zip's order: `7z zip bz2 rar arj z lha cab iso 001 rpm deb cpio tar gz wim lzh dmg hfs xar vhd fat ntfs xz sqfs apfs zst`.

Toolbar buttons: `Add Extract Test Copy Move Delete Info`, as 48x36 and 24x24 bitmaps.

Program icons replace the main icon of:

| Icon | 7-Zip file |
|---|---|
| `7zFM.ico` | `7zFM.exe` (7-Zip File Manager) |
| `7zG.ico` | `7zG.exe` (7-Zip progress and dialog windows) |
| `7zSFX.ico` | `7z.sfx` (self-extracting archives, window version) |
| `7zConSFX.ico` | `7zCon.sfx` (self-extracting archives, console version) |
| `7zSsfx.ico`, `7zS2sfx.ico` | `7zS.sfx`, `7zS2.sfx` (installer modules from the "7-Zip Extra" package, if present) |

Themes are not part of this repository; each belongs to its own author and licence.

## Building

Run `build.cmd`. It uses the C# compiler that comes with .NET Framework 3.5 (on Windows 10/11, turn on ".NET Framework 3.5" in Windows Features) and writes `bin\7zTM.exe`.

## Licence

GNU General Public License v3.0 or later. See [LICENSE](LICENSE).
