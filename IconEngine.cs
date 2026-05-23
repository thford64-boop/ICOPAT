using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Drawing;
using System.Collections.Generic;

namespace IconPatcher
{
    /// <summary>
    /// Patches icons into Windows PE files (EXE, SCR, DLL, etc.)
    /// using the Windows UpdateResource API — no external tools needed.
    /// </summary>
    public static class IconEngine
    {
        // ── Windows API imports ──────────────────────────────────────────────

        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern IntPtr BeginUpdateResource(string pFileName, bool bDeleteExistingResources);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool UpdateResource(
            IntPtr hUpdate, uint lpType, uint lpName,
            ushort wLanguage, byte[] lpData, uint cbData);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool EndUpdateResource(IntPtr hUpdate, bool fDiscard);

        // Resource type constants
        private const uint RT_ICON       = 3;
        private const uint RT_GROUP_ICON = 14;
        private const ushort LANG_NEUTRAL = 0;

        // ── ICO file structures ──────────────────────────────────────────────

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        private struct ICONDIRENTRY
        {
            public byte  Width;
            public byte  Height;
            public byte  ColorCount;
            public byte  Reserved;
            public ushort Planes;
            public ushort BitCount;
            public uint  BytesInRes;
            public uint  ImageOffset;
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        private struct GRPICONDIR
        {
            public ushort Reserved;
            public ushort Type;
            public ushort Count;
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        private struct GRPICONDIRENTRY
        {
            public byte  Width;
            public byte  Height;
            public byte  ColorCount;
            public byte  Reserved;
            public ushort Planes;
            public ushort BitCount;
            public uint  BytesInRes;
            public ushort Id;
        }

        // ── Public API ───────────────────────────────────────────────────────

        /// <summary>
        /// Patches the icon of a PE file.
        /// Converts PNG/JPG/BMP to ICO in memory if needed.
        /// Returns null on success, error message on failure.
        /// </summary>
        public static string PatchIcon(string exePath, string iconPath)
        {
            try
            {
                byte[] icoBytes = LoadAsIco(iconPath);
                if (icoBytes == null)
                    return "Failed to load or convert icon file.";

                return WriteIconToExe(exePath, icoBytes);
            }
            catch (Exception ex)
            {
                return ex.Message;
            }
        }

        // ── Icon loading / conversion ────────────────────────────────────────

        /// <summary>
        /// Loads a file as ICO bytes. If it's already an ICO, returns raw bytes.
        /// If it's a PNG/JPG/BMP, converts to a single-image ICO in memory.
        /// </summary>
        private static byte[] LoadAsIco(string path)
        {
            string ext = Path.GetExtension(path).ToLowerInvariant();

            if (ext == ".ico")
                return File.ReadAllBytes(path);

            // Convert raster image to ICO
            using (Image src = Image.FromFile(path))
            {
                // Use up to 256x256; resize if larger
                int size = Math.Min(Math.Max(src.Width, src.Height), 256);
                using (Bitmap bmp = new Bitmap(src, size, size))
                {
                    return BitmapToIco(bmp);
                }
            }
        }

        /// <summary>Encodes a Bitmap as a single-image ICO file in memory.</summary>
        private static byte[] BitmapToIco(Bitmap bmp)
        {
            // Save bitmap as PNG into a memory stream
            byte[] pngBytes;
            using (var ms = new MemoryStream())
            {
                bmp.Save(ms, System.Drawing.Imaging.ImageFormat.Png);
                pngBytes = ms.ToArray();
            }

            using (var ico = new MemoryStream())
            using (var w = new BinaryWriter(ico))
            {
                // ICONDIR header
                w.Write((ushort)0);          // reserved
                w.Write((ushort)1);          // type = icon
                w.Write((ushort)1);          // count = 1 image

                // ICONDIRENTRY
                int headerSize = 6 + 16;     // ICONDIR + 1 ICONDIRENTRY
                w.Write((byte)(bmp.Width > 255 ? 0 : bmp.Width));
                w.Write((byte)(bmp.Height > 255 ? 0 : bmp.Height));
                w.Write((byte)0);            // color count
                w.Write((byte)0);            // reserved
                w.Write((ushort)1);          // planes
                w.Write((ushort)32);         // bit count
                w.Write((uint)pngBytes.Length);
                w.Write((uint)headerSize);

                // PNG image data
                w.Write(pngBytes);
                return ico.ToArray();
            }
        }

        // ── PE resource writing ──────────────────────────────────────────────

        private static string WriteIconToExe(string exePath, byte[] icoBytes)
        {
            // Parse ICO
            using (var ms = new MemoryStream(icoBytes))
            using (var r = new BinaryReader(ms))
            {
                ushort reserved = r.ReadUInt16();
                ushort type     = r.ReadUInt16();
                ushort count    = r.ReadUInt16();

                if (type != 1)
                    return "Not a valid ICO file.";

                var entries = new List<(ICONDIRENTRY entry, byte[] data)>();
                var dirEntries = new ICONDIRENTRY[count];

                for (int i = 0; i < count; i++)
                {
                    dirEntries[i] = new ICONDIRENTRY
                    {
                        Width       = r.ReadByte(),
                        Height      = r.ReadByte(),
                        ColorCount  = r.ReadByte(),
                        Reserved    = r.ReadByte(),
                        Planes      = r.ReadUInt16(),
                        BitCount    = r.ReadUInt16(),
                        BytesInRes  = r.ReadUInt32(),
                        ImageOffset = r.ReadUInt32()
                    };
                }

                for (int i = 0; i < count; i++)
                {
                    ms.Seek(dirEntries[i].ImageOffset, SeekOrigin.Begin);
                    byte[] imgData = r.ReadBytes((int)dirEntries[i].BytesInRes);
                    entries.Add((dirEntries[i], imgData));
                }

                // Begin resource update
                IntPtr hUpdate = BeginUpdateResource(exePath, false);
                if (hUpdate == IntPtr.Zero)
                    return $"Cannot open {Path.GetFileName(exePath)} for editing. " +
                           $"Error: {Marshal.GetLastWin32Error()}. Is it running or read-only?";

                // Write each RT_ICON resource
                for (int i = 0; i < entries.Count; i++)
                {
                    byte[] imgData = entries[i].data;
                    bool ok = UpdateResource(hUpdate, RT_ICON, (uint)(i + 1),
                                             LANG_NEUTRAL, imgData, (uint)imgData.Length);
                    if (!ok)
                    {
                        EndUpdateResource(hUpdate, true); // discard
                        return $"Failed writing icon image {i + 1}. Error: {Marshal.GetLastWin32Error()}";
                    }
                }

                // Write RT_GROUP_ICON resource
                byte[] grp = BuildGroupIcon(entries, count);
                bool grpOk = UpdateResource(hUpdate, RT_GROUP_ICON, 1,
                                            LANG_NEUTRAL, grp, (uint)grp.Length);
                if (!grpOk)
                {
                    EndUpdateResource(hUpdate, true);
                    return $"Failed writing icon group. Error: {Marshal.GetLastWin32Error()}";
                }

                // Commit
                bool committed = EndUpdateResource(hUpdate, false);
                if (!committed)
                    return $"Failed to commit changes. Error: {Marshal.GetLastWin32Error()}";

                return null; // success
            }
        }

        /// <summary>Builds the RT_GROUP_ICON binary structure.</summary>
        private static byte[] BuildGroupIcon(List<(ICONDIRENTRY entry, byte[] data)> entries, int count)
        {
            using (var ms = new MemoryStream())
            using (var w = new BinaryWriter(ms))
            {
                // GRPICONDIR
                w.Write((ushort)0);     // reserved
                w.Write((ushort)1);     // type
                w.Write((ushort)count); // count

                // GRPICONDIRENTRY for each image
                for (int i = 0; i < count; i++)
                {
                    var e = entries[i].entry;
                    w.Write(e.Width);
                    w.Write(e.Height);
                    w.Write(e.ColorCount);
                    w.Write(e.Reserved);
                    w.Write(e.Planes);
                    w.Write(e.BitCount);
                    w.Write(e.BytesInRes);
                    w.Write((ushort)(i + 1)); // resource ID
                }

                return ms.ToArray();
            }
        }
    }
}
