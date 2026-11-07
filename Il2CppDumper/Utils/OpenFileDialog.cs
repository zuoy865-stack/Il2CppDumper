using System;
using System.Runtime.InteropServices;

namespace Il2CppDumper
{
    public class OpenFileDialog
    {
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct OpenFileName
        {
            public uint Size;
            public IntPtr Owner;
            public IntPtr Instance;
            public IntPtr Filter;
            public IntPtr CustomFilter;
            public uint MaxCustomFilter;
            public uint FilterIndex;
            public IntPtr File;
            public uint MaxFile;
            public IntPtr FileTitle;
            public uint MaxFileTitle;
            public string InitialDirectory;
            public string Title;
            public uint Flags;
            public ushort FileOffset;
            public ushort ExtensionOffset;
            public string DefaultExtension;
            public IntPtr CustomData;
            public IntPtr Hook;
            public IntPtr TemplateName;
            public IntPtr Reserved;
            public uint ReservedFlags;
            public uint ExtendedFlags;
        }

        private const uint NoChangeDirectory = 0x00000008;
        private const uint FileMustExist = 0x00001000;
        private const uint Explorer = 0x00080000;
        private const uint DontAddToRecent = 0x02000000;

        [DllImport("comdlg32.dll", EntryPoint = "GetOpenFileNameW", CharSet = CharSet.Unicode, ExactSpelling = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GetOpenFileName(ref OpenFileName options);

        public string Title { get; set; }
        public string Filter { get; set; }
        public string FileName { get; set; }

        public bool ShowDialog()
        {
            const int pathCapacity = 32768;
            var path = Marshal.AllocHGlobal(pathCapacity * sizeof(char));
            // The Win32 filter is a sequence of NUL-terminated strings, followed by an extra NUL.
            var filter = string.IsNullOrEmpty(Filter) ? IntPtr.Zero :
                Marshal.StringToHGlobalUni(Filter.Replace('|', '\0') + "\0");
            try
            {
                Marshal.WriteInt16(path, 0);
                var options = new OpenFileName
                {
                    Size = (uint)Marshal.SizeOf<OpenFileName>(),
                    Filter = filter,
                    FilterIndex = 1,
                    File = path,
                    MaxFile = pathCapacity,
                    Title = Title,
                    Flags = NoChangeDirectory | FileMustExist | Explorer | DontAddToRecent
                };
                if (!GetOpenFileName(ref options))
                {
                    return false;
                }

                FileName = Marshal.PtrToStringUni(path);
                return true;
            }
            finally
            {
                Marshal.FreeHGlobal(filter);
                Marshal.FreeHGlobal(path);
            }
        }
    }
}
