using System;
using System.IO;

namespace TUI {
    /// <summary>
    /// Class Rectangle represents simple geometric rectangle defined by position (x, y), width and height
    /// These could only be changed by a virtual Move(...) method, which could be overriden to process coordinate changes
    /// It also offers some "syntactic sugar" and handy utilities:
    /// - methods to Move without resizing and Resize without moving
    /// - properties to access rectangle as (X, Y, W, H), as (X1, Y1, X2, Y2) or as (Left, Top, Right, Bottom), whatever user prefers
    /// - methods to check if the rectangle is empty and to calculate intersection of two rectangles (returns null if intersection is empty)
    /// </summary>
    public class Rectangle {
        protected int x, y, w, h;  // Position, width and height

        public virtual void Move(int _x, int _y, int _w, int _h) { x = _x; y = _y; w = _w; h = _h; }
        public Rectangle(int _x, int _y, int _w, int _h) { Move(_x, _y, _w, _h); }
        public Rectangle() : this(0, 0, 0, 0) { }

        public void Move(int _x, int _y) { Move(_x, _y, w, h); }
        public void Resize(int _w, int _h) { Move(x, y, _w, _h); }

        public int X { get => x; set { Move(value, y, w, h); } }
        public int Y { get => y; set { Move(x, value, w, h); } }
        public int W { get => w; set { Move(x, y, value, h); } }
        public int H { get => h; set { Move(x, y, w, value); } }

        public int X1 { get => x; set { Move(value, y, w, h); } }
        public int Y1 { get => y; set { Move(x, value, w, h); } }
        public int X2 { get => x+w; set { Move(x, y, value-x, h); } }
        public int Y2 { get => y+h; set { Move(x, y, w, value-y); } }

        public int Left { get => x; set { Move(value, y, w, h); } }
        public int Top { get => y; set { Move(x, value, w, h); } }
        public int Right { get => x+w; set { Move(x, y, value-x, h); } }
        public int Bottom { get => y+h; set { Move(x, y, w, value-y); } }

        public bool IsEmpty() => ((w <= 0) || (h <= 0));

        public static Rectangle Intersect(Rectangle a, Rectangle b) {
            int x, y, w, h;
            x = Math.Max(a.x, b.x); w = Math.Min(a.x + a.w, b.x + b.w) - x;
            y = Math.Max(a.y, b.y); h = Math.Min(a.y + a.h, b.y + b.h) - y;
            return ((w > 0) && (h > 0)) ? new Rectangle(x, y, w, h) : null;
        }
    }

    /// <summary>
    /// Class Frame is the base class of the TUI visual hierarchy, but actually it is not part of the window/widget architecture
    /// It is just a rectangular area over specified Backend with Write methods,
    ///   which could be handy in applications that don't need whole of TUI, but just simple text interface
    /// Class also maintains current output position and current color
    /// </summary>
    public class Frame : Rectangle {
        protected Backend backend;

        public Frame(Backend _backend, int _x, int _y, int _w, int _h) : base(_x, _y, _w, _h)
            { backend = _backend; CurrX = CurrY = 0; DefaultColors = 0x0F; }
        public Frame(int _x, int _y, int _w, int _h) : this(Backend.DefaultBackend, _x, _y, _w, _h) { }
        public Frame(Backend _backend) : this(_backend, 0, 0, 0, 0) { }
        public Frame() : this(Backend.DefaultBackend, 0, 0, 0, 0) { }

        public int CurrX, CurrY,    // Current output position
                   DefaultColors;   // Currently used default colors

        public const int AlignNone = 0,
                         AlignRight = 1,
                         AlignLeft = 2,
                         AlignCenter = 3;

        private void WriteExt(char c, int width, int flags, int colors) {
            /*
            int i, l = 0;
            width = FixWidth(width, 1); if (width < 1) return;
            if (CurrX + width > )

                if ((CurrX < 0) || (width <= 0) || (CurrY < 0) && (CurrY >= h))
                    if (OutsideException) throw new InvalidOperationException("Output outside area"); else return;
            if (align != 0) { l = align*(width-1)/2; for (i = 0; i < l; i++) backend.OutChar(CurrX++, CurrY, ' ', colors); }
            */
            backend.OutChar(x + CurrX++, y + CurrY, c, colors);
            // if (align == 1) { l = width-1-l; for (i = 0; i < l; i++) backend.OutChar(CurrX++, CurrY, ' ', colors); }
        }

        public void WriteMany(char c, int num, int colors) {
            /*
            if ((CurrX < 0) || (width <= 0) || (CurrY < 0) && (CurrY >= h))
                if (OutsideException) throw new InvalidOperationException("Output outside area"); else return;
            if (align != 0) { l = align*(width-1)/2; for (i = 0; i < l; i++) backend.OutChar(CurrX++, CurrY, ' ', colors); }
            backend.OutChar(CurrX++, CurrY, c, colors);
            if (align == 1) { l = width-1-l; for (i = 0; i < l; i++) backend.OutChar(CurrX++, CurrY, ' ', colors); }
            */
        }



        public void WriteExt(char[] c, int index, int count, int width, int flags, int colors) { for (int i = 0; i < c.Length; i++) Write(c[i]); }
        public void WriteExt(string s, int width, int flags, int colors) { for (int i = 0; i < s.Length; i++) backend.OutChar(x + CurrX++, y + CurrY, s[i], colors); ; }
        /*!!!!!!*/
        public void WriteExt(long v, int width, int flags, int colors) { Write(v.ToString()); }
        /*!!!!!!*/
        public void WriteExt(double v, int width, int flags, int colors) { Write(v.ToString()); }
        public void WriteExt(object obj, int width, int flags, int colors) { Write(obj.ToString()); }



        // ---------- Variations of Write(...) functions
        public void Write(char c) { WriteExt(c, 0, 0, DefaultColors); }
        public void Write(char c, int colors) { WriteExt(c, 0, 0, colors); }
        public void Write(int x, int y, char c) { CurrX = x; CurrY = y; WriteExt(c, 0, 0, DefaultColors); }
        public void Write(int x, int y, char c, int colors) { CurrX = x; CurrY = y; WriteExt(c, 0, 0, colors); }
        public void Write(char[] c) { WriteExt(c, 0, c.Length, 0, 0, DefaultColors); }
        public void Write(char[] c, int colors) { WriteExt(c, 0, c.Length, 0, 0, colors); }
        public void Write(int x, int y, char[] c) { CurrX = x; CurrY = y; WriteExt(c, 0, c.Length, 0, 0, DefaultColors); }
        public void Write(int x, int y, char[] c, int colors) { CurrX = x; CurrY = y; WriteExt(c, 0, c.Length, 0, 0, colors); }
        public void Write(char[] c, int index, int count) { WriteExt(c, index, Math.Min(count, c.Length - index), 0, 0, DefaultColors); }
        public void Write(char[] c, int index, int count, int colors) { WriteExt(c, index, Math.Min(count, c.Length - index), 0, 0, colors); }
        public void Write(int x, int y, char[] c, int index, int count) { CurrX = x; CurrY = y; WriteExt(c, index, Math.Min(count, c.Length - index), 0, 0, DefaultColors); }
        public void Write(int x, int y, char[] c, int index, int count, int colors) { CurrX = x; CurrY = y; WriteExt(c, index, Math.Min(count, c.Length - index), 0, 0, colors); }
        public void Write(string s) { WriteExt(s, 0, 0, DefaultColors); }
        public void Write(string s, int colors) { WriteExt(s, 0, 0, colors); }
        public void Write(int x, int y, string s) { CurrX = x; CurrY = y; WriteExt(s, 0, 0, DefaultColors); }
        public void Write(int x, int y, string s, int colors) { CurrX = x; CurrY = y; WriteExt(s, 0, 0, colors); }
        public void Write(long v) { WriteExt(v, 0, 0, DefaultColors); }
        public void Write(long v, int colors) { WriteExt(v, 0, 0, colors); }
        public void Write(int x, int y, long v) { CurrX = x; CurrY = y; WriteExt(v, 0, 0, DefaultColors); }
        public void Write(int x, int y, long v, int colors) { CurrX = x; CurrY = y; WriteExt(v, 0, 0, colors); }
        public void Write(double v) { WriteExt(v, 0, 0, DefaultColors); }
        public void Write(double v, int colors) { WriteExt(v, 0, 0, colors); }
        public void Write(int x, int y, double v) { CurrX = x; CurrY = y; WriteExt(v, 0, 0, DefaultColors); }
        public void Write(int x, int y, double v, int colors) { CurrX = x; CurrY = y; WriteExt(v, 0, 0, colors); }
        public void Write(object obj) { WriteExt(obj, 0, 0, DefaultColors); }
        public void Write(object obj, int colors) { WriteExt(obj, 0, 0, colors); }
        public void Write(int x, int y, object obj) { CurrX = x; CurrY = y; WriteExt(obj, 0, 0, DefaultColors); }
        public void Write(int x, int y, object obj, int colors) { CurrX = x; CurrY = y; WriteExt(obj, 0, 0, colors); }

        public void WriteExt(char c, int width, int flags) { WriteExt(c, width, flags, DefaultColors); }
        public void WriteExt(int x, int y, char c, int width, int flags) { CurrX = x; CurrY = y; WriteExt(c, width, flags, DefaultColors); }
        public void WriteExt(int x, int y, char c, int width, int flags, int colors) { CurrX = x; CurrY = y; WriteExt(c, width, flags, colors); }
        public void WriteExt(char[] c, int width, int flags) { WriteExt(c, 0, c.Length, width, flags, DefaultColors); }
        public void WriteExt(char[] c, int width, int flags, int colors) { WriteExt(c, 0, c.Length, width, flags, colors); }
        public void WriteExt(int x, int y, char[] c, int width, int flags) { CurrX = x; CurrY = y; WriteExt(c, 0, c.Length, width, flags, DefaultColors); }
        public void WriteExt(int x, int y, char[] c, int width, int flags, int colors) { CurrX = x; CurrY = y; WriteExt(c, 0, c.Length, width, flags, colors); }
        public void WriteExt(char[] c, int index, int count, int width, int flags) { WriteExt(c, index, Math.Min(count, c.Length - index), width, flags, DefaultColors); }
        public void WriteExt(int x, int y, char[] c, int index, int count, int width, int flags) { CurrX = x; CurrY = y; WriteExt(c, index, Math.Min(count, c.Length - index), width, flags, DefaultColors); }
        public void WriteExt(int x, int y, char[] c, int index, int count, int width, int flags, int colors) { CurrX = x; CurrY = y; WriteExt(c, index, Math.Min(count, c.Length - index), width, flags, colors); }
        public void WriteExt(string s, int width, int flags) { WriteExt(s, width, flags, DefaultColors); }
        public void WriteExt(int x, int y, string s, int width, int flags) { CurrX = x; CurrY = y; WriteExt(s, width, flags, DefaultColors); }
        public void WriteExt(int x, int y, string s, int width, int flags, int colors) { CurrX = x; CurrY = y; WriteExt(s, width, flags, colors); }
        public void WriteExt(long v, int width, int flags) { WriteExt(v, width, flags, DefaultColors); }
        public void WriteExt(int x, int y, long v, int width, int flags) { CurrX = x; CurrY = y; WriteExt(v, width, flags, DefaultColors); }
        public void WriteExt(int x, int y, long v, int width, int flags, int colors) { CurrX = x; CurrY = y; WriteExt(v, width, flags, colors); }
        public void WriteExt(double v, int width, int flags) { WriteExt(v, width, flags, DefaultColors); }
        public void WriteExt(int x, int y, double v, int width, int flags) { CurrX = x; CurrY = y; WriteExt(v, width, flags, DefaultColors); }
        public void WriteExt(int x, int y, double v, int width, int flags, int colors) { CurrX = x; CurrY = y; WriteExt(v, width, flags, colors); }
        public void WriteExt(object obj, int width, int flags) { WriteExt(obj, width, flags, DefaultColors); }
        public void WriteExt(int x, int y, object obj, int width, int flags) { CurrX = x; CurrY = y; WriteExt(obj, width, flags, DefaultColors); }
        public void WriteExt(int x, int y, object obj, int width, int flags, int colors) { CurrX = x; CurrY = y; WriteExt(obj, width, flags, colors); }

        public void WriteMany(char c, int num) { WriteMany(c, num, DefaultColors); }
        public void WriteMany(int x, int y, char c, int num) { CurrX = x; CurrY = y; WriteMany(c, num, DefaultColors); }
        public void WriteMany(int x, int y, char c, int num, int colors) { CurrX = x; CurrY = y; WriteMany(c, num, colors); }
    }
}
