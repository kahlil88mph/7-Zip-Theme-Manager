# 7-Zip Theme Manager

Changes the look of [7-Zip](https://www.7-zip.org/): the file type icons Windows shows for archives, and the toolbar buttons in 7-Zip File Manager.

It is a new, open-source program that reads the theme folders used by the original 7-Zip Theme Manager (7zTM). It does not contain code from 7zTM or its remakes.

## Features

- **Fast:** pictures are swapped with Windows' own resource functions in a single pass, with no external tools such as Resource Hacker. A theme applies in well under a second.
- **Works with current 7-Zip:** knows all 27 icon slots of 7-Zip 26, including `sqfs`, `apfs` and `zst`.
- **Forgiving:** a theme can leave out pictures; 7-Zip keeps its current picture for those. Alternative file names such as `squashfs.ico`/`sq.ico` and `zstd.ico` are accepted.
- **Safe:** each file is patched as a copy, checked, then swapped in. The first original of each 7-Zip file is saved, and **Restore saved 7-Zip files** puts it back.
- **Works while 7-Zip is in use:** if Explorer has `7z.dll` loaded, the old file is moved aside and removed later.
- **Asks for administrator rights only when needed:** a portable 7-Zip in a writable folder needs none.
- **Runs on Windows XP SP2 up to Windows 11.** Built for .NET Framework 2.0–3.5 and also runs on 4.x, so Windows 7, 8, 10 and 11 need nothing installed. Windows XP needs .NET Framework 2.0 or later.

## Using it

1. Put `7zThemeManager.exe` and `7zThemeManager.exe.config` in the folder that holds your `FileType` and `ToolBar` theme folders (or choose that folder in the program).
2. Start it. It finds an installed 7-Zip on its own; for a portable 7-Zip, choose its folder.
3. Pick a theme and click **Apply**. Restart 7-Zip File Manager to see new toolbar buttons. Windows may need a sign-out, or an Explorer restart, before it shows new file type icons everywhere.

### Command line

```
7zThemeManager.exe [--7zip <folder>] [--themes <folder>] [--filetype <theme>] [--toolbar <theme>] [--restore]
```

`<theme>` is the theme's folder name or the name in its `theme.ini`.

## Theme format

Themes are folders, compatible with 7zTM:

```
FileType\<theme>\theme.ini, preview.jpg, 7z.ico, zip.ico, ...
ToolBar\<theme>\theme.ini, preview.jpg, 48x36\Add.bmp ... Info.bmp, 24x24\Add.bmp ... Info.bmp
```

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

Themes are not part of this repository; each belongs to its own author and licence.

## Building

Run `build.cmd`. It uses the C# compiler that comes with .NET Framework 3.5 (on Windows 10/11, turn on ".NET Framework 3.5" in Windows Features) and writes `bin\7zThemeManager.exe`.

## Licence

GNU General Public License v3.0 or later. See [LICENSE](LICENSE).
