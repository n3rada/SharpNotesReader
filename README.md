# SharpNotesReader
`SharpNotesReader` is a tool designed to **extract unsaved notes from [Windows 11's Notepad](https://apps.microsoft.com/detail/9msmlrh6lzf3)** (`Notepad.exe`) session files. This feature allows quick-typed notes to persist even after the application is closed and reopened later. This tool lets you retrieve and read those unsaved files, providing insight into the hidden "`TabState`" files Notepad uses.

Many users rely on Notepad for tasks like quickly jotting down passwords or short notes:

![notes](./images/notes.png)

With a bit of trickery and a few lines of code, you can look into the stored note data located at `%localAppData%\Packages\Microsoft.WindowsNotepad_8wekyb3d8bbwe\LocalState\TabState` and extract the content:

![example](./images/example.png)

## Build Instruction
Open the solution (`.sln`) file with `Visual Studio`, and build the solution (`F7`). Executable will be located at `SharpNotesReader\bin\Release\SharpNotesReader.exe`. 

