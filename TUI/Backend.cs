using System;
using System.Collections.Generic;
using System.Text;

// These classes represent backends to show TUI on particular system. They serve two purposes:
// 1. Abstraction. While default implementation uses Console class of .Net Core, other backends are possible
//    - ANSI codes for terminal emulation on *nix systems (or maybe to show TUI over network on remote terminal)
//    - rendering in graphics mode to use TUI under GUI
//    - direct output to video memory in text modes, should .Net ever be implemented for such OS
// 2. Bufferization.
//    Console output is pretty slow, to the point that screen updates are literally visible
//    And it can greatly decrease performance for many overlapping windows, with many updates overwriting each other
//    So a special BufferBackend can be added, saving all updates in memory buffer, which should be very fast
//    And once everything is done, only areas that were changed are copied to screen
//
// Usage note: after any writing DoneUpdate() should be called to copy all buffers and caches to screen
//
// Techincal note about implementaion internal detail:
// initially all buffers are allocated equal to backend dimensions, to save memory if backend is never resized (some backends cannot)
// however, as soon as backend starts growing, buffers are reallocated to double amount required to have fewer reallocate requests

namespace TUI {
    // ---------- Abstract backend, root of the backend hierarchy
    public abstract class Backend {
        // ---------- Default backend. Current implementation assigns this to default C# console
        public static Backend DefaultBackend;

        // ---------- Buffering backend optionally attached to this one
        protected BufferBackend buffer_backend;

        public void AttachBufferBackend(BufferBackend backend) {
            if (buffer_backend != backend) {
                if (buffer_backend != null) throw new InvalidOperationException("TUI: Buffer backend already attached.");
                buffer_backend = backend;
            }
            backend.Resize(w, h);
            if (DefaultBackend == this) DefaultBackend = backend;
        }

        // ---------- Dimensions management
        protected int w, h;                // Width and height of the view area

        public int Width { get => w; }
        public int Height { get => h; }
        public virtual void Resize(int _w, int _h) { w = _w; h = _h; if (buffer_backend != null) buffer_backend.Resize(w, h); }

        // ---------- Utility function to verify coordinates
        bool UseExceptions = false;        // Whether exceptions should be thrown on attempts to access out-of-bounds cells

        protected bool Validate(int x, int y) {
            if ((x < 0) || (x >= w)) if (UseExceptions) throw new ArgumentOutOfRangeException("x", "TUI: coordinates outside output area"); else return false;
            if ((y < 0) || (y >= h)) if (UseExceptions) throw new ArgumentOutOfRangeException("y", "TUI: coordinates outside output area"); else return false;
            return true;
        }

        // ---------- Character output
        public abstract void OutChar(int x, int y, char c, int colors);
        public virtual char ReadChar(int x, int y) { throw new InvalidOperationException("TUI: Read operations are not implemented for this backend"); }
        public virtual int ReadColors(int x, int y) { throw new InvalidOperationException("TUI: Read operations are not implemented for this backend"); }
        public virtual void DoneUpdate() { }

        // ---------- Cursor control
        public int CursorX { get => GetCursorX(); }
        public int CursorY { get => GetCursorY(); }
        public virtual int GetCursorX() { throw new InvalidOperationException("TUI: Cursor is not implemented for this backend"); }
        public virtual int GetCursorY() { throw new InvalidOperationException("TUI: Cursor is not implemented for this backend"); }
        public virtual bool IsCursorVisible() { throw new InvalidOperationException("TUI: Cursor is not implemented for this backend"); }
        public virtual void ShowCursor(int x, int y) { throw new InvalidOperationException("TUI: Cursor is not implemented for this backend"); }
        public virtual void HideCursor() { throw new InvalidOperationException("TUI: Cursor is not implemented for this backend"); }

        // ---------- Event reading
        public abstract int GetEvent();

        // ---------- Constructor
        protected Backend(int _w, int _h) { w = _w; h = _h; UseExceptions = false; buffer_backend = null; }
    }

    // ---------- Bufferization backend, serves two purposes:
    // - can be attached to another backend by "AttachBufferBackend"
    // - can be used as an offscreen buffer for a frame to speed up drawing
    public class BufferBackend : Backend {
        protected Backend backend;    // Underlying backend
        protected int off_x, off_y;   // Offset of this virtual frame on the underlying backend

        // ---------- Buffer, its size and allocation
        protected struct BufferSymbol {
            public char c, old_c;
            public int colors, old_colors;
        };
        protected BufferSymbol [] buffer;
        protected int size;

        public override void Resize(int _w, int _h) {
            base.Resize(_w, _h); if (w*h <= size) return;
            size = ((size > 0) ? 2 : 1) * w * h;  // Resize trick explained in the note above
            buffer = new BufferSymbol[size];
            for (int i = 0; i<size; i++) {
                buffer[i].c = buffer[i].old_c = ' ';
                buffer[i].colors = buffer[i].old_colors = -1;
            }
        }

        public void Move(int _x, int _y, int _w, int _h) { off_x = _x; off_y = _y; Resize(_w, _h); }

        // ---------- Constructors
        public BufferBackend(Backend _backend, int _x, int _y, int _w, int _h) : base(0, 0) { backend = _backend; Move(_x, _y, _w, _h); }
        public BufferBackend(Backend _backend, int _w, int _h) : this(_backend, 0, 0, _w, _h) { }
        public BufferBackend(Backend _backend) : this(_backend, 0, 0) { }

        // ---------- Character output and reading simply work with the buffer
        public override void OutChar(int x, int y, char c, int colors) { if (Validate(x, y)) { buffer[y*w+x].c = c; buffer[y*w+x].colors = colors; } }
        public override char ReadChar(int x, int y) { return Validate(x, y) ? buffer[y*w+x].c : ' '; }
        public override int ReadColors(int x, int y) { return Validate(x, y) ? buffer[y*w+x].colors : -1; }

        // ---------- Methods to copy buffer to the underlying backend
        // DoneUpdate only copies cells that were changed, ForceRedraw forces full redraw
        public override void DoneUpdate() {
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                    if ((buffer[y*w+x].c != buffer[y*w+x].old_c) || (buffer[y*w+x].colors != buffer[y*w+x].old_colors)) {
                        backend.OutChar(off_x+x, off_y+y, buffer[y*w+x].c, buffer[y*w+x].colors);
                        buffer[y*w+x].old_c = buffer[y*w+x].c; buffer[y*w+x].old_colors = buffer[y*w+x].colors;
                    }
            backend.DoneUpdate();
        }

        public void ForceRedraw() {
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++) {
                    backend.OutChar(off_x+x, off_y+y, buffer[y*w+x].c, buffer[y*w+x].colors);
                    buffer[y*w+x].old_c = buffer[y*w+x].c; buffer[y*w+x].old_colors = buffer[y*w+x].colors;
                }
            backend.DoneUpdate();
        }

        // ---------- Cursor control and event processing pass requests to underlying backend
        public override int GetCursorX() { return backend.GetCursorX(); }
        public override int GetCursorY() { return backend.GetCursorY(); }
        public override bool IsCursorVisible() { return backend.IsCursorVisible(); }
        public override void ShowCursor(int x, int y) { if (Validate(x, y)) backend.ShowCursor(off_x+x, off_y+y); }
        public override void HideCursor() { backend.HideCursor(); }
        public override int GetEvent() { return backend.GetEvent(); }
    }

    // ---------- Bufferization backend with virtual cursor implementation
    public class BufferBackendCursor : BufferBackend {
        protected int cursor_x, cursor_y;
        protected bool cursor_visible;

        // Default cursor just swaps background and foreground for cursor cell
        // Descendants can implement more interesting cursors
        protected virtual void ToggleCursor(bool show) {
            int c = buffer[cursor_y * w + cursor_x].colors;
            if (c != -1) { c = (c >> 4) | ((c & 0x0f) << 4); }
            buffer[cursor_y * w + cursor_x].colors = c;
        }

        public override void DoneUpdate() { if (cursor_visible) ToggleCursor(true); base.DoneUpdate(); if (cursor_visible) ToggleCursor(false); }

        public override int GetCursorX() { return cursor_x; }
        public override int GetCursorY() { return cursor_y; }
        public override bool IsCursorVisible() { return cursor_visible; }

        public override void ShowCursor(int x, int y) { cursor_visible = Validate(x, y); cursor_x = x; cursor_y = y; }
        public override void HideCursor() { cursor_visible = false; }

        public override void Resize(int _w, int _h) { base.Resize(_w, _h); cursor_visible &= ((cursor_x < w) && (cursor_y < h)); }

        public BufferBackendCursor(Backend _backend, int _x, int _y, int _w, int _h) : base(_backend, _x, _y, _w, _h) { cursor_visible = false; }
        public BufferBackendCursor(Backend _backend, int _w, int _h) : this(_backend, 0, 0, _w, _h) { }
        public BufferBackendCursor(Backend _backend) : this(_backend, 0, 0) { }
    }

    // ---------- Useful abstraction for console backends, caches sequences of adjacent characters to speed up output
    public abstract class CachingBackend : Backend {
        protected char [] cache;           // Cache buffer
        protected int cache_size,          // Size of cache buffer
                      cache_len,           // Amount of characters currently cached
                      cache_x, cache_y,    // Position of the cached string
                      cache_colors;        // Colors of the cached string

        // ---------- This function actually prints cached symbols
        protected abstract void PrintCache();

        // ---------- Output functions perform caching
        public override void OutChar(int x, int y, char c, int colors) {
            if (!Validate(x, y)) return;
            if (cache_len > 0) {
                if ((cache_x + cache_len == x) && (cache_y == y) && (cache_colors == colors)) { cache[cache_len++] = c; return; }
                PrintCache();
            }
            cache[0] = c; cache_len = 1; cache_x = x; cache_y = y; cache_colors = colors;
        }

        public override void DoneUpdate() { if (cache_len > 0) PrintCache(); cache_len = 0; }

        // ---------- Resizing should also (re)allocate cache buffer
        protected void AllocateCache() {
            cache_len = 0; if (w <= cache_size) return;
            cache_size = ((cache_size > 0) ? 2 : 1) * w;  // Resize trick explained in the note above
            cache = new char[cache_size];
        }

        public override void Resize(int _w, int _h) { base.Resize(_w, _h); AllocateCache(); }

        protected CachingBackend(int _w, int _h) : base(_w, _h) { cache_size = 0; AllocateCache(); }
    }

    // ---------- Backend implementation over standard .Net Core Console
    public class ConsoleBackend : CachingBackend {
        protected int output_x, output_y;   // Current output position
        protected int output_colors;        // Current output colors
        protected int cursor_x, cursor_y;   // Current cursor position
        protected int cursor_visible;       // 0 = invisible, 1 = visible, 2 = visible, but hidden for screen updates

        protected override void PrintCache() {
            if (cache_colors == -1) return;
            if (cache_colors != output_colors) {
                Console.ForegroundColor = (ConsoleColor)(cache_colors & 0x0f);
                Console.BackgroundColor = (ConsoleColor)((cache_colors >> 4) & 0x0f);
                output_colors = cache_colors;
            }
            if (cursor_visible == 1) { cursor_visible = 2; Console.CursorVisible = false; output_y = -1; }
            if ((output_x != cache_x) || (output_y != cache_y)) { Console.SetCursorPosition(cache_x, cache_y); output_y = cache_y; }
            Console.Write(cache, 0, cache_len); output_x = cache_x + cache_len;
        }

        public override void DoneUpdate() {
            base.DoneUpdate();
            if (cursor_visible == 2) { Console.SetCursorPosition(cursor_x, cursor_y); Console.CursorVisible = true; cursor_visible = 1; }
        }

        public override int GetCursorX() { return cursor_x; }
        public override int GetCursorY() { return cursor_y; }
        public override bool IsCursorVisible() { return cursor_visible > 0; }
        public override void ShowCursor(int x, int y) {
            cursor_x = x; cursor_y = y;
            if (cursor_visible == 2) DoneUpdate(); else { Console.SetCursorPosition(cursor_x, cursor_y); Console.CursorVisible = true; cursor_visible = 1; }
        }
        public override void HideCursor() {
            if (cursor_visible == 1) { Console.CursorVisible = false; output_y = -1; }
            cursor_visible = 0;
        }

        public override int GetEvent() { return 0; }

        public bool UpdateSize() {
            if ((w == Console.WindowWidth) && (h == Console.WindowHeight)) return false;
            base.Resize(Console.WindowWidth, Console.WindowHeight);
            Console.SetBufferSize(w, h); output_y = -1; return true;
        }

        public override void Resize(int _w, int _h) { Console.SetWindowSize(_w, _h); UpdateSize(); }

        public ConsoleBackend(bool SetDefault) : base(0, 0) {
            UpdateSize(); if (SetDefault) DefaultBackend = this; cursor_visible = 1;
        }
    }
}
