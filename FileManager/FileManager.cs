﻿using System;
using TUI;
using System.Xml.Serialization;
using System.IO;

namespace FileManager {
    abstract class Window : Frame {  // ------------------------------------------------------------- Root of the Window Hierarchy
        private Window Next;                       // List of all windows
        private static Window First = null, Last = null;
        protected bool visible;                    // If this window is visible
        protected bool canFocus;                   // Iff this window can have keyboard focus
        public static Window FocusWindow = null;   // Window that has keyboard focus
        public static string errorFile;
        protected int OfX;
        private static bool PlacementRequired = true;
        private static ConsoleBackend console_backend;
        private static BufferBackend buffer_backend;

        public static void logError(string errMsg) {
            File.AppendAllText(errorFile, $"{DateTime.Now} Error code: {errMsg}\n");
        }
        public static void InitBackends() {
            console_backend = new ConsoleBackend(true);
            buffer_backend = new BufferBackend(console_backend);
            console_backend.AttachBufferBackend(buffer_backend);
        }
        public Window() : base(5, 5, 5, 5) {
            if (Last == null) First = this; else Last.Next = this;
            Next = null; Last = this; OfX = 0;
            visible = true; canFocus = false;
        }
        protected abstract void Draw();   // Abstract function - draws the window
        protected abstract void Place();  // Abstract function - positions the window on screen
        public void Redraw() {
            if (visible) Draw();
        }
        public static void RedrawAllWindows(bool force) {
            Window Cur;
            PlacementRequired |= console_backend.UpdateSize();
            if (PlacementRequired) {
                for (Cur = First; Cur != null; Cur = Cur.Next) Cur.Place(); PlacementRequired = false;
            } else if (!force) return;
            for (Cur = First; Cur != null; Cur = Cur.Next) Cur.Redraw();
            buffer_backend.ForceRedraw(); FocusWindow.SetupCursor();
        }
        public void Show() {
            visible = true; RedrawAllWindows(true);
        }
        public void Hide() {
            visible = false; RedrawAllWindows(true);
        }
        public virtual void PostCommand() { }
        public virtual string Input(ConsoleKeyInfo cmdKey) { return null; }
        protected bool isFocused() {
            return (FocusWindow == this);
        }
        public virtual void SetupCursor() { backend.HideCursor(); }
        public static void FocusNextWindow() {
            if (FocusWindow != null) FocusWindow = FocusWindow.Next;
            do {
                if (FocusWindow == null) FocusWindow = First;
                if (FocusWindow.canFocus) break;
                FocusWindow = FocusWindow.Next;
            } while (true);
            FocusWindow.SetupCursor();
        }

        protected void SetCursorPosition(int CX, int CY) { CurrX = CX + OfX; CurrY = CY; }

        protected void WriteLine(string str, int colors) { Write(str, colors); CurrX = OfX; CurrY++; }
        protected void WriteLineCentered(string str, int colors) { CurrX = (w - str.Length) / 2; WriteLine(str, colors); }
        protected void WriteLimited(string str, int RightOffset, bool end, int colors) {
            int MaxW = w - RightOffset - CurrX;
            if (str.Length <= MaxW) Write(str, colors);
            else if (end) Write(str.Substring(str.Length - MaxW), colors);
            else Write(str.Substring(0, MaxW), colors);
        }
        protected void WriteSpacesTo(int RightOffset, int colors) {
            int MaxW = w - RightOffset - CurrX;
            for (int i = 0; i < MaxW; i++) Write(' ', colors);
        }

        protected void WriteLine(string str) { WriteLine(str, DefaultColors); }
        protected void WriteLineCentered(string str) { WriteLineCentered(str, DefaultColors); }
        protected void WriteLimited(string str, int RightOffset, bool end) { WriteLimited(str, RightOffset, end, DefaultColors); }
        protected void WriteSpacesTo(int RightOffset) { WriteSpacesTo(RightOffset, DefaultColors); }
    }

    abstract class FramedWindow : Window {  // ---------------------------------------------------- Window that has a frame
        public FramedWindow() : base() { DefaultColors = 0x1F; }
        private void DrawEdgedLine(string Left, string Interior, string Right, int Size) {
            Write(Left);
            for (int i = 0; i < Size; i++) Write(Interior);
            WriteLine(Right);
        }
        protected abstract void DrawInterior();
        protected override void Draw() {
            OfX = 0;
            SetCursorPosition(0, 0);
            DrawEdgedLine("\u2554", "\u2550", "\u2557", w - 2);
            for (int i = 0; i < h - 2; i++) DrawEdgedLine("\u2551", " ", "\u2551", w - 2);
            DrawEdgedLine("\u255A", "\u2550", "\u255D", w - 2);
            OfX = 1; SetCursorPosition(0, 1); DrawInterior();
        }
    }

    abstract class PopupWindow : FramedWindow {  // ----------------------------------------------- Window with a message that shows on top
        public PopupWindow() : base() {
            DefaultColors = 0x70;
            w = 60; h = 4; visible = false;
        }
        protected override void Place() {
            Move((backend.Width - w)/2, (backend.Height - h - 2) / 2);
        }
        public void ShowModal() {
            Show(); Console.ReadKey(true); Hide();
        }
    }

    abstract class ListWindow : FramedWindow {  // ------------------------------------------------ Window with a scrollable list
        public int Cursor, Start, Num;
        public ListWindow() : base() {
            Cursor = 0; Start = 0; Num = 0; canFocus = true;
        }
        protected abstract void DrawText(int n, int colors);
        private void DrawLine(int n, bool Selected) {
            SetCursorPosition(0, n + 1);
            int colors = Selected ? 0xE0 : DefaultColors;
            DrawText(Start + n, colors);
            WriteSpacesTo(1, colors);
        }
        protected override void DrawInterior() {
            for (int i = 0; i < h - 2; i++) {
                if ((Start + i) >= Num) break;
                DrawLine(i, ((Start + i) == Cursor) && isFocused());
            }
        }
        public override string Input(ConsoleKeyInfo cmdKey) {
            switch (cmdKey.Key) {
                case ConsoleKey.UpArrow:
                    if (Cursor == 0) break;
                    DrawLine(Cursor - Start, false); Cursor--;
                    if (Cursor < Start) { Start = Cursor; DrawInterior(); } else DrawLine(Cursor - Start, true);
                    backend.DoneUpdate();
                    break;
                case ConsoleKey.DownArrow:
                    if (Cursor == (Num - 1)) break;
                    DrawLine(Cursor - Start, false); Cursor++;
                    if (Cursor - h + 3 > Start) { Start = Cursor - h + 3; DrawInterior(); } else DrawLine(Cursor - Start, true);
                    backend.DoneUpdate();
                    break;
            }
            return null;
        }
    }

    class FileListWindow : ListWindow {  // ------------------------------------------------------- One of file manager panels
        public string[] files;
        public string[] directories;
        bool right;
        int FL;
        public string currFolder;
        public FileListWindow OtherPanel;
        public bool RootFolder;
        public FileListWindow(bool _right, string Folder) : base() {
            currFolder = Folder;
            if (currFolder == "") currFolder = Directory.GetCurrentDirectory();
            try { ListDirectories(); } catch { currFolder = Directory.GetCurrentDirectory(); ListDirectories(); logError("ListDirectory global error."); }
            right = _right;
        }
        protected override void Place() {
            if (right) { x = backend.Width / 2; w = backend.Width - x; } else { x = 0; w = backend.Width / 2; }
            y = 0; h = backend.Height - 10;
        }
        public void ListDirectories() {
            RootFolder = (currFolder.Substring(1) == @":\");
            try { files = Directory.GetFiles(currFolder); } catch { files = new string[0]; logError("Scaninng files in " + currFolder + "."); }
            try { directories = Directory.GetDirectories(currFolder); } catch { directories = new string[0]; logError("Scaninng directories in " + currFolder + "."); }
            Num = files.Length + directories.Length + (RootFolder ? 0 : 1);
            FL = currFolder.Length; if (currFolder[FL - 1] != '\\') FL++;
            if (Cursor >= Num) Cursor = Num - 1;
        }
        public string GetCurrentFile() {
            if ((Cursor == 0) && !RootFolder) return "..";
            int n = Cursor - (RootFolder ? 0 : 1);
            if (n < directories.Length) return directories[n];
            return files[n - directories.Length];
        }
        protected override void DrawText(int n, int colors) {
            if (!RootFolder) {
                if (n == 0) { Write("..", colors); WriteSpacesTo(10, colors); Write(" <FOLDER>", colors); return; }
                n--;
            }
            if (n < directories.Length) {
                WriteLimited(directories[n].Substring(FL), 10, false, colors);
                WriteSpacesTo(10, colors); Write(" <FOLDER>", colors);
            } else WriteLimited(files[n - directories.Length].Substring(FL), 1, false, colors);
        }
        protected override void DrawInterior() {
            base.DrawInterior();
            SetCursorPosition(2, 0); Write(" ");
            WriteLimited($"{currFolder} ", 3, true, 0x12);
            SetCursorPosition(2, h - 1); Write(" ", 0x12);
            WriteLimited($"{directories.Length} folders, {files.Length} files. ", 3, true, 0x12);
        }
        public override string Input(ConsoleKeyInfo cmdKey) {
            base.Input(cmdKey);
            switch (cmdKey.Key) {
                case ConsoleKey.Enter: return "cd '" + GetCurrentFile() + "'";
                case ConsoleKey.F3: if (RootFolder || (Cursor > 0)) return "tp '" + GetCurrentFile() + "'"; break;
                case ConsoleKey.F4: return "chdrv";
                case ConsoleKey.F5: if (RootFolder || (Cursor > 0)) return "cp '" + GetCurrentFile() + "' '" + OtherPanel.currFolder + "'"; break;
                case ConsoleKey.F6: if (RootFolder || (Cursor > 0)) return "mv '" + GetCurrentFile() + "' '" + OtherPanel.currFolder + "'"; break;
                case ConsoleKey.F7: return "md";
                case ConsoleKey.F8: if (RootFolder || (Cursor > 0)) return "rm '" + GetCurrentFile() + "'"; break;
            }
            return null;
        }
    }

    class DescriptionWindow : FramedWindow {  // -------------------------------------------------- Description window
        public FileListWindow left, right;
        public DescriptionWindow() : base() { }
        protected override void Place() {
            x = 0; w = backend.Width; y = backend.Height - 10; h = 8;
        }
        public void DrawFileInfo() {
            string fname;
            if (FocusWindow == left) fname = left.GetCurrentFile();
            else if (FocusWindow == right) fname = right.GetCurrentFile();
            else return;
            SetCursorPosition(0, 2);
            WriteLimited(fname, 6, true); WriteSpacesTo(6); WriteLine("");

            FileInfo fileDscrpt = new FileInfo(fname);
            try {
                WriteLimited($"Creation time: {fileDscrpt.CreationTime}", 6, true); WriteSpacesTo(6); WriteLine("");
                WriteLimited($"Attributes: {fileDscrpt.Attributes} bytes", 6, true); WriteSpacesTo(6); WriteLine("");
                if (fileDscrpt.Length < 1024) { WriteLimited($"Size of file: {fileDscrpt.Length} bytes", 6, true); WriteSpacesTo(6); WriteLine(""); ; }
                if ((fileDscrpt.Length > 1024) && (fileDscrpt.Length < 1048576)) { WriteLimited($"Size of file: {fileDscrpt.Length / 1024} Kbytes", 6, true); WriteSpacesTo(6); WriteLine(""); }
                if ((fileDscrpt.Length > 1048576) && (fileDscrpt.Length < 1073741824)) { WriteLimited($"Size of file: {fileDscrpt.Length / 1048576} Mbytes", 6, true); WriteSpacesTo(6); WriteLine(""); }
                if (fileDscrpt.Length > 1073741824) { WriteLimited($"Size of file: {fileDscrpt.Length / 1073741824} Gbytes", 6, true); WriteSpacesTo(6); WriteLine(""); }
            } catch { logError("Generic file error: fileAttr/creation time/length corrupted."); };
        }
        protected override void DrawInterior() {
            OfX = 5; DrawFileInfo();
        }
    }

    class CommandLine : Window {  // -------------------------------------------------------------- Command line and buttons
        public string CurPath;
        public string Command;
        public string[] cmdHistory;
        int HistorySize, CurHistory;
        int CursorX = 0;
        public CommandLine() : base() {
            Command = "";
            cmdHistory = new string[100]; // history for 100 last commands 
            HistorySize = 0; canFocus = true;
            CurHistory = 0;
        }
        protected override void Place() {
            x = 0; y = backend.Height - 2; w = backend.Width; h = 2;
        }
        private void ShowButton(int Num, string cmd, bool Reserved) {
            SetCursorPosition(10 * (Num - 1), 1);
            WriteExt($"F{Num}: {cmd,-5}", 0, 0, Reserved ? 0x33 : 0x30);
        }
        public override void SetupCursor() { backend.ShowCursor(x+CursorX, y); }
        protected override void Draw() {
            DefaultColors = 0x0F;
            SetCursorPosition(0, 1);
            WriteSpacesTo(1);
            ShowButton(1, "Help", false);
            ShowButton(2, "About", false);
            ShowButton(3, "Type", false);
            ShowButton(4, "chDrv", false);
            ShowButton(5, "Copy", false);
            ShowButton(6, "Move", false);
            ShowButton(7, "MkDr", false);
            ShowButton(8, "Del ", false);
            ShowButton(9, "Setup", false);
            ShowButton(10, "Exit", false);
            DefaultColors = (isFocused()) ? 0x0F : 0x07;
            SetCursorPosition(0, 0);
            WriteLimited(CurPath, 10, true);
            Write(" :> ");
            WriteLimited(Command, 0, true);
            CursorX = CurrX;
            WriteSpacesTo(0);
        }
        public override void PostCommand() { Command = ""; }
        public override string Input(ConsoleKeyInfo cmdKey) {
            if (cmdKey.KeyChar >= ' ') Command += cmdKey.KeyChar;
            switch (cmdKey.Key) {
                case ConsoleKey.UpArrow:
                    if (CurHistory > 0) CurHistory--;
                    if (CurHistory < HistorySize) Command = cmdHistory[CurHistory];
                    break;
                case ConsoleKey.DownArrow:
                    if (CurHistory < HistorySize - 1) CurHistory++;
                    if (CurHistory < HistorySize) Command = cmdHistory[CurHistory];
                    break;
                case ConsoleKey.F5: Command = "cp "; break;
                case ConsoleKey.Backspace: if (Command.Length > 0) Command = Command.Substring(0, Command.Length - 1); break;
                case ConsoleKey.Enter:
                    if (HistorySize < 100) cmdHistory[HistorySize++] = Command;
                    else {
                        for (int i = 0; i < 99; i++) cmdHistory[i] = cmdHistory[i + 1];
                        cmdHistory[99] = Command;
                    }
                    CurHistory = HistorySize - 1;
                    return Command;
            }
            Redraw(); backend.DoneUpdate(); SetupCursor(); return null;
        }
    }

    class ChangeDrive : PopupWindow {  // --------------------------------------------------------- Drive selection window
        DriveInfo[] drvs;
        int Cursor = 0;
        public ChangeDrive() : base() {
            drvs = DriveInfo.GetDrives();
            h = drvs.Length + 2; Cursor = 0;
        }
        private string GetSize(Int64 x) { return $"{x / (102410241024)} Gb"; }
        private void DrawLine(int n, bool Selected) {
            SetCursorPosition(0, n + 1);
            DefaultColors = Selected ? 0x0E : 0x70; 
            Write(drvs[n].Name); WriteSpacesTo(54);
            Write($"{drvs[n].DriveType}"); WriteSpacesTo(45);
            if ((drvs[n].DriveType != DriveType.Network) && drvs[n].IsReady) {
                Write(drvs[n].VolumeLabel); WriteSpacesTo(35);
                Write(drvs[n].DriveFormat); WriteSpacesTo(28);
                Write(GetSize(drvs[n].TotalFreeSpace)); Write(" / ");
                Write(GetSize(drvs[n].TotalSize));
            }
            WriteSpacesTo(1); DefaultColors = 0x70;
        }
        protected override void DrawInterior() {
            for (int i = 0; i < drvs.Length; i++) DrawLine(i, i == Cursor);
        }
        public string Choose() {
            Show();
            do {
                switch (Console.ReadKey(true).Key) {
                    case ConsoleKey.UpArrow:
                        if (Cursor > 0) { DrawLine(Cursor, false); Cursor--; DrawLine(Cursor, true); }
                        backend.DoneUpdate();
                        break;
                    case ConsoleKey.DownArrow:
                        if (Cursor < drvs.Length - 1) { DrawLine(Cursor, false); Cursor++; DrawLine(Cursor, true); }
                        backend.DoneUpdate();
                        break;
                    case ConsoleKey.Enter:
                        Hide(); return drvs[Cursor].Name;
                }
            } while (true);
        }
    }

    class Help1 : PopupWindow {
        public Help1() : base() {
            h = 20;
        }
        protected override void DrawInterior() {
            WriteLineCentered("Command List:");
            WriteLine("ls : List directories and files.");
            WriteLine("   type 'ls' to list all.");
            WriteLine("   use 'ls -p Number' to start from page <Number>.");
            WriteLine("   use 'ls -f' to list only files.");
            WriteLine("   use 'ls -d' to list only directories.");
            WriteLine("md : Make new directory.");
            WriteLine("   type 'md new_directory'.");
            WriteLine("cd : Change current directory.");
            WriteLine("   type 'cd new_directory'");
            WriteLine("rm : Remove directory or file.");
            WriteLine("   type 'rm file_name' or 'rm directory_name'.");
            WriteLine("mv : Move directory or file.");
            WriteLine("   type 'mv file_name new_location'.");
            WriteLine("cp : Copy directory or file.");
            WriteLine("   type 'cp file_name new_location'.");
            WriteLine("tp : Print file on screen.");
            WriteLine("   type 'tp file_name'.");
        }
    }

    class Help2 : PopupWindow {
        public Help2() : base() {
            h = 20;
        }
        protected override void DrawInterior() {
            WriteLineCentered("Buttons Help:");
            WriteLine("F1 : Help window.");
            WriteLine("F2 : About window.");
            WriteLine("F3 : Type file on screen.");
            WriteLine("F4 : Change Drive.");
            WriteLine("F5 : Copy file or directory.");
            WriteLine("F6 : Move file or directory.");
            WriteLine("F7 : Make new directory.");
            WriteLine("F8 : Delete file or directory.");
            WriteLine("F9 : Change program settings.");
            WriteLine("F10 : Exit from program.");

        }
    }

    class About : PopupWindow {
        private static string version = "2.0 alpha (text ui)";
        public About() : base() {
            DefaultColors = 0x6E;
            h = 7;
        }
        protected override void DrawInterior() {
            WriteLineCentered("About:");
            WriteLine($"File Manager. JFM v{version}.");
            WriteLine("Copyright (C) 2021, Jacken Quake.");
            WriteLine("");
            WriteLine("powered by Visual Studio 2019 (С) Microsoft Corporation.");
        }
    }

    class ErrorWindow : PopupWindow {
        public ErrorWindow() : base() {
            DefaultColors = 0x4F;
        }
        protected override void DrawInterior() {
            WriteLine("Command is incorrect.");
            WriteLine("Type 'help' or '/?' to list command.");
        }
    }

    class GenericErrorWindow : PopupWindow {
        public string msg;
        public GenericErrorWindow() : base() {
            DefaultColors = 0x4F;
        }
        protected override void DrawInterior() {
            WriteLine(msg);
            WriteLine("Press any key to continue...");
        }
    }

    //[Serializable]
    public class Config {
        public int PageSize;
        public string StartFolder1, StartFolder2, cmdLineFolder;
        public void Default() {
            StartFolder1 = "";
            StartFolder2 = @"C:\";
            cmdLineFolder = Directory.GetCurrentDirectory();
            PageSize = 25;
        }
        public void Input() {
            Console.Clear();
            Console.WriteLine("Setup: ");
            Console.Write("Please enter start folder for Left Window :> "); StartFolder1 = Console.ReadLine();
            Console.Write("Please enter start folder for Right Window :> "); StartFolder2 = Console.ReadLine();
            do {
                Console.Write("Please enter page size (max number of lines per page) :> "); var str = Console.ReadLine();
                try {
                    PageSize = Int32.Parse(str);
                    if (PageSize <= 0) Console.WriteLine("Number must be positive.");
                } catch { Console.WriteLine("Please enter the number..."); PageSize = 0; Window.logError("Entering number failure."); }
            } while (PageSize <= 0);
        }
    }

    class FileManager {
        static int pageLines = 0;
        static int pageSize = 25;
        static string command, configFile;
        static int cmdPtr;
        enum ListMode { Files, Directories, All };
        enum ProcessMode { Copy, Move, Delete };
        private static ProcessMode pMode;
        private static string pSource, pDest;
        private static CommandLine cmdLine;
        private static FileListWindow leftPanel, rightPanel;
        private static GenericErrorWindow ErrWin;
        public static void logError(string errMsg) {
            Window.logError(errMsg);
        }
        public static void ShowError(string errMsg) {
            ErrWin.msg = errMsg;
            ErrWin.ShowModal();
        }
        private static string getWord() {
            char finChar;
            int startPtr;
            while ((cmdPtr < command.Length) && (command[cmdPtr] <= ' ')) cmdPtr++;
            if (cmdPtr >= command.Length) return null;
            if (command[cmdPtr] == '\'') { finChar = '\''; cmdPtr++; } else if (command[cmdPtr] == '"') { finChar = '"'; cmdPtr++; } else finChar = ' ';
            if (cmdPtr == command.Length) return null;
            startPtr = cmdPtr; cmdPtr = command.IndexOf(finChar, cmdPtr);
            if (cmdPtr < 0) cmdPtr = command.Length + 1; else cmdPtr++;
            return command.Substring(startPtr, cmdPtr - startPtr - 1);
        }
        private static bool PageWriteLine(string str) {
            if (pageLines < 0) { pageLines++; return false; }
            Console.WriteLine(str);
            pageLines++; if (pageLines < pageSize) return false;
            Console.WriteLine();
            Console.Write("Press ESC to exit or any other key for next page...");
            if (Console.ReadKey(true).Key == ConsoleKey.Escape) return true;
            pageLines = 0; Console.Clear(); return false;
        }
        static void ListDirectories() {
            ListMode Lmode = ListMode.All;
            Console.Clear();
            string Folder = cmdLine.CurPath;
            //pageSize = Window.winH - 3;
            pageLines = 0;
            do {
                string word = getWord();
                if (word == null) break;
                if (word[0] != '-') Folder = word;
                else switch (word) {
                        case "-p":
                            word = getWord();
                            if (word == null) break;
                            try { pageLines = -pageSize * Int32.Parse(word); } catch { logError("Type conversion error." + word + " is not an integer."); };
                            break;
                        case "-f": Lmode = ListMode.Files; break;
                        case "-d": Lmode = ListMode.Directories; break;
                    }
            } while (true);
            string[] files = Directory.GetFiles(Folder);
            string[] directories = Directory.GetDirectories(Folder);
            if (PageWriteLine("")) return;
            if (PageWriteLine($"Listing of {Folder} :")) return;
            if (PageWriteLine("")) return;
            int FL = Folder.Length; if (Folder[FL - 1] != '\\') FL++;
            //for (int i = 0; i < directories.Length; i++) Console.WriteLine($"{directories[i].Substring(FL),-50}<FOLDER>");
            char[] AttrsPacked = new char[10];
            if (Lmode != ListMode.Files) foreach (string directory in directories) {
                    DirectoryInfo DirInfo = new DirectoryInfo(directory);
                    string AttrsFull = $"{DirInfo.Attributes}";
                    int ptr = 0;
                    for (int num = 0; num < 10; num++) {
                        while ((ptr < AttrsFull.Length) && (AttrsFull[ptr] == ' ')) ptr++;
                        if (ptr == AttrsFull.Length) { AttrsPacked[num] = ' '; continue; }
                        AttrsPacked[num] = AttrsFull[ptr]; ptr = AttrsFull.IndexOf(',', ptr);
                        if (ptr < 0) ptr = AttrsFull.Length; else ptr++;
                    }
                    if (PageWriteLine($"{DirInfo.FullName.Substring(FL),-30} {DirInfo.CreationTime,-20} {new string(AttrsPacked),10}        <FOLDER>")) return;
                }
            //for (int i = 0; i < files.Length; i++) Console.WriteLine(files[i].Substring(FL));
            if (Lmode != ListMode.Directories) foreach (string file in files) {
                    FileInfo FlInfo = new FileInfo(file);
                    string FLength = "";
                    if (FlInfo.Length < 1024) FLength = $"{FlInfo.Length} bytes";
                    if ((FlInfo.Length > 1024) && (FlInfo.Length < 1048576)) FLength = $"{FlInfo.Length / 1024} Kbytes";
                    if ((FlInfo.Length > 1048576) && (FlInfo.Length < 1073741824)) FLength = $"{FlInfo.Length / 1048576} Mbytes";
                    if (FlInfo.Length > 1073741824) FLength = $"{FlInfo.Length / 1073741824} Gbytes";
                    string AttrsFull = $"{FlInfo.Attributes}";
                    int ptr = 0;
                    for (int num = 0; num < 10; num++) {
                        while ((ptr < AttrsFull.Length) && (AttrsFull[ptr] == ' ')) ptr++;
                        if (ptr == AttrsFull.Length) { AttrsPacked[num] = ' '; continue; }
                        AttrsPacked[num] = AttrsFull[ptr]; ptr = AttrsFull.IndexOf(',', ptr);
                        if (ptr < 0) ptr = AttrsFull.Length; else ptr++;
                    }
                    if (PageWriteLine($"{FlInfo.FullName.Substring(FL),-30} {FlInfo.CreationTime,-20} {new string(AttrsPacked),10} {FLength,15}")) return;
                }
            if (PageWriteLine("")) return;
            if (PageWriteLine($"{directories.Length} folders.")) return;
            if (PageWriteLine($"{files.Length} files.")) return;
            if (PageWriteLine("")) return;
            Console.Write("Press any key to return..."); Console.ReadKey(true);
        }
        private static void TypeFile(string myFile) {
            Console.Clear();
            string[] tpSTR;
            pageLines = 0;
            try { tpSTR = File.ReadAllLines(myFile); } catch { ShowError($"Incorrect file name <{myFile}>. Press any key to return..."); logError("Incorrect file name: " + myFile + "."); return; }
            foreach (string STR in tpSTR) if (PageWriteLine(STR)) return;
            Console.Write("Press any key to return..."); Console.ReadKey(true);
        }
        private static void ProcessFile(string Name) {
            try {
                var fiSource = new FileInfo(pSource + Name);
                if (pMode == ProcessMode.Delete) fiSource.Delete();
                else {
                    if (pDest[pDest.Length - 1] != '\\') Name = pDest;
                    else Name = pDest + Name;
                    //var fiDest = new FileInfo(Name);
                    if (pMode == ProcessMode.Copy) fiSource.CopyTo(Name);
                    else fiSource.MoveTo(Name);
                }
            } catch {
                logError("File operation error.");
                ShowError("File operation error. Press any key to return...");
                return;
            }
        }
        private static void ProcessRecursively(string Folder) {
            if (pMode != ProcessMode.Delete)
                try { Directory.CreateDirectory(pDest + Folder.Substring(pSource.Length)); } catch {
                    logError("Directory creation error.");
                    ShowError("Cannot create directory. Press any key to return...");
                    return;
                }
            string[] fd;
            fd = Directory.GetDirectories(Folder);
            foreach (string cur in fd)
                ProcessRecursively(cur);
            fd = Directory.GetFiles(Folder);
            foreach (string cur in fd)
                ProcessFile(cur.Substring(pSource.Length));
            try { if (pMode != ProcessMode.Copy) Directory.Delete(Folder); } catch {
                logError("Deleting directory error.");
                ShowError("Cannot delete directory. Press any key to return...");
                return;
            }
        }
        private static void Process() {
            pSource = getWord(); if (pSource == null) return;
            if (pMode != ProcessMode.Delete) {
                pDest = getWord();
                if (pDest == null) pDest = cmdLine.CurPath;
            }
            FileInfo flInfo = new FileInfo(pSource);
            if ((flInfo.Attributes & FileAttributes.Directory) == FileAttributes.Directory) {
                if (pSource[pSource.Length - 1] != '\\') pSource = pSource + '\\';
                if (pMode != ProcessMode.Delete) {
                    if (pDest[pDest.Length - 1] != '\\') pDest = pDest + '\\';
                    pDest = pDest + flInfo.Name;
                    Directory.CreateDirectory(pDest);
                    pDest = pDest + "\\";
                }
                ProcessRecursively(pSource);
            } else {
                pSource = flInfo.DirectoryName;
                if (pSource[pSource.Length - 1] != '\\') pSource = pSource + '\\';
                if (pMode != ProcessMode.Delete) {
                    FileAttributes attr = File.GetAttributes(pDest);
                    if ((attr & FileAttributes.Directory) == FileAttributes.Directory)
                        if (pDest[pDest.Length - 1] != '\\') pDest = pDest + '\\';
                }
                ProcessFile(flInfo.Name);
            }
        }
        private static string GetFocusFolder() {
            if (Window.FocusWindow == leftPanel) return leftPanel.currFolder;
            if (Window.FocusWindow == rightPanel) return rightPanel.currFolder;
            if (Window.FocusWindow == cmdLine) return cmdLine.CurPath;
            return null;
        }
        private static void SetFocusFolder(string DirName) {
            try { Directory.SetCurrentDirectory(DirName); } catch { Console.Clear(); ShowError($"Incorrect directory name <{DirName}>. Press any key to return..."); logError("Incorrect directory name: " + DirName + "."); };
            if (Window.FocusWindow == leftPanel) {
                leftPanel.currFolder = Directory.GetCurrentDirectory();
                leftPanel.Cursor = 0; leftPanel.Start = 0;
            }
            if (Window.FocusWindow == rightPanel) {
                rightPanel.currFolder = Directory.GetCurrentDirectory();
                rightPanel.Cursor = 0; rightPanel.Start = 0;
            }
            if (Window.FocusWindow == cmdLine) cmdLine.CurPath = Directory.GetCurrentDirectory();
        }
        private static void MakeDirectory(string DirName) {
            if (DirName == null) {
                ResetConsole();
                Console.Write("Press enter new directory name :> ");
                DirName = Console.ReadLine();
            }
            try { Directory.CreateDirectory(DirName); } catch {
                ShowError($"Cannot create {DirName} directory. Press any key to return...");
                logError("Directory creation error: " + DirName + ".");
            }
        }
        private static Config LoadConfig() {
            /*
            string json = File.ReadAllText("config.ini");
            return JsonSerializer.Deserialize<Config>(json);
            */
            string xml = File.ReadAllText(configFile);
            StringReader stringReader = new StringReader(xml);
            XmlSerializer serializer = new XmlSerializer(typeof(Config));
            Config tmp = (Config)serializer.Deserialize(stringReader);
            return tmp;
        }
        private static void SaveConfig(Config config) {
            /*
            string json = JsonSerializer.Serialize<Config>(config);
            File.WriteAllText("config.ini", json);
            */
            config.cmdLineFolder = cmdLine.CurPath;
            StringWriter stringWriter = new StringWriter();
            XmlSerializer serializer = new XmlSerializer(typeof(Config));
            serializer.Serialize(stringWriter, config);
            string xml = stringWriter.ToString();
            if (File.Exists(configFile)) File.Delete(configFile);
            File.WriteAllText(configFile, xml);
        }
        static void ResetConsole() {
            Console.ForegroundColor = ConsoleColor.White;
            Console.BackgroundColor = ConsoleColor.Black;
            Console.Clear();
            Console.CursorVisible = true;
        }
        static void Main(string[] args) {
            Window.InitBackends();
            configFile = Directory.GetCurrentDirectory() + "\\config.ini";
            Window.errorFile = Directory.GetCurrentDirectory() + "\\error.log";
            Config config;
            try { config = LoadConfig(); } catch {
                config = new Config();
                config.Default();
                logError("Config reading error.");
            }
            pageSize = config.PageSize;
            var descWnd = new DescriptionWindow();
            leftPanel = new FileListWindow(false, config.StartFolder1);
            rightPanel = new FileListWindow(true, config.StartFolder2);
            leftPanel.OtherPanel = rightPanel;
            rightPanel.OtherPanel = leftPanel;
            cmdLine = new CommandLine();
            descWnd.left = leftPanel; descWnd.right = rightPanel;
            //cmdLine.CurPath = Directory.GetCurrentDirectory();
            cmdLine.CurPath = config.cmdLineFolder;
            var drvWnd = new ChangeDrive();
            var HelpWnd1 = new Help1();
            var HelpWnd2 = new Help2();
            var AboutWnd = new About();
            var errWnd = new ErrorWindow();
            ErrWin = new GenericErrorWindow();
            //Window.FocusNextWindow();
            Window.FocusWindow = cmdLine;
            bool NeedRedraw = true;
            ConsoleKeyInfo cmdKey;
            do {
                Window.RedrawAllWindows(NeedRedraw); NeedRedraw = false;
                descWnd.DrawFileInfo();
                cmdKey = Console.ReadKey(true);
                switch (cmdKey.Key) {
                    case ConsoleKey.Tab:
                        Window.FocusNextWindow();
                        NeedRedraw = true;
                        Directory.SetCurrentDirectory(GetFocusFolder());
                        continue;
                    case ConsoleKey.F1: HelpWnd1.ShowModal(); HelpWnd2.ShowModal(); continue;
                    case ConsoleKey.F2: AboutWnd.ShowModal(); continue;
                    case ConsoleKey.F9: ResetConsole(); config.Input(); NeedRedraw = true; pageSize = config.PageSize; continue;
                    case ConsoleKey.F10: ResetConsole(); SaveConfig(config); return;
                }
                command = Window.FocusWindow.Input(cmdKey);
                if (command == null) continue;
                cmdPtr = 0;
                //string DirName;
                switch (getWord()) {
                    case "ls": ResetConsole(); ListDirectories(); break;  // ------------------------------------- List with parameters
                    case "help":  // -------------------------------------------------------------- Help
                    case "/?": HelpWnd1.ShowModal(); HelpWnd2.ShowModal(); break;  // ------------ Help
                    case "chdrv": SetFocusFolder(drvWnd.Choose()); break;  // --------------------- Change Drive
                    case "cd": SetFocusFolder(getWord()); break;  // ------------------------------ Change Directory
                    case "md": MakeDirectory(getWord()); break;  // ------------------------------- Make Directory
                    case "rm": pMode = ProcessMode.Delete; Process(); break;  // ------------------ Delete Directory
                    case "mv": pMode = ProcessMode.Move; Process(); break;  // -------------------- Move Directory
                    case "cp": pMode = ProcessMode.Copy; Process(); break;  // -------------------- Copy file/Directory
                    case "tp": ResetConsole(); TypeFile(getWord()); break; // ------------------------------------- Type file
                    case "exit": ResetConsole(); SaveConfig(config); return;
                    default: errWnd.ShowModal(); break;
                }
                leftPanel.ListDirectories();
                rightPanel.ListDirectories();
                Window.FocusWindow.PostCommand(); NeedRedraw = true;
            } while (true);
        }
    }
}
