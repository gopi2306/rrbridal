using System;
using System.Runtime.InteropServices;
using System.Text;

namespace RRBridal.StoreBilling.App.Services.BarcodePrinting;

/// <summary>Sends raw EPL/text to a Windows printer queue via Win32 spooler.</summary>
public static class RawPrinterHelper
{
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi)]
    private class DOCINFOA
    {
        [MarshalAs(UnmanagedType.LPStr)]
        public string pDocName = "";

        [MarshalAs(UnmanagedType.LPStr)]
        public string pOutputFile = "";

        [MarshalAs(UnmanagedType.LPStr)]
        public string pDataType = "RAW";
    }

    [DllImport("winspool.drv", EntryPoint = "OpenPrinterA", SetLastError = true, CharSet = CharSet.Ansi)]
    private static extern bool OpenPrinter(string szPrinter, out IntPtr hPrinter, IntPtr pd);

    [DllImport("winspool.drv", EntryPoint = "ClosePrinter", SetLastError = true)]
    private static extern bool ClosePrinter(IntPtr hPrinter);

    [DllImport("winspool.drv", EntryPoint = "StartDocPrinterA", SetLastError = true, CharSet = CharSet.Ansi)]
    private static extern bool StartDocPrinter(IntPtr hPrinter, int level, [In] DOCINFOA di);

    [DllImport("winspool.drv", EntryPoint = "EndDocPrinter", SetLastError = true)]
    private static extern bool EndDocPrinter(IntPtr hPrinter);

    [DllImport("winspool.drv", EntryPoint = "StartPagePrinter", SetLastError = true)]
    private static extern bool StartPagePrinter(IntPtr hPrinter);

    [DllImport("winspool.drv", EntryPoint = "EndPagePrinter", SetLastError = true)]
    private static extern bool EndPagePrinter(IntPtr hPrinter);

    [DllImport("winspool.drv", EntryPoint = "WritePrinter", SetLastError = true)]
    private static extern bool WritePrinter(IntPtr hPrinter, IntPtr pBytes, int dwCount, out int dwWritten);

    public static bool SendStringToPrinter(string printerName, string data, out string? error)
    {
        error = null;
        if (string.IsNullOrWhiteSpace(printerName))
        {
            error = "Printer name is required.";
            return false;
        }

        if (!OpenPrinter(printerName.Trim(), out var hPrinter, IntPtr.Zero))
        {
            error = $"Could not open printer \"{printerName}\" (Win32 {Marshal.GetLastWin32Error()}).";
            return false;
        }

        try
        {
            var di = new DOCINFOA { pDocName = "RR Bridal barcode", pDataType = "RAW" };
            if (!StartDocPrinter(hPrinter, 1, di))
            {
                error = $"StartDocPrinter failed (Win32 {Marshal.GetLastWin32Error()}).";
                return false;
            }

            try
            {
                if (!StartPagePrinter(hPrinter))
                {
                    error = $"StartPagePrinter failed (Win32 {Marshal.GetLastWin32Error()}).";
                    return false;
                }

                try
                {
                    // CRLF improves compatibility with TVS LP 46 NEO and other RAW label drivers
                    var normalized = data.Replace("\r\n", "\n", StringComparison.Ordinal)
                        .Replace("\n", "\r\n", StringComparison.Ordinal);
                    var bytes = Encoding.ASCII.GetBytes(normalized);
                    var unmanaged = Marshal.AllocCoTaskMem(bytes.Length);
                    try
                    {
                        Marshal.Copy(bytes, 0, unmanaged, bytes.Length);
                        if (!WritePrinter(hPrinter, unmanaged, bytes.Length, out var written) || written != bytes.Length)
                        {
                            error = $"WritePrinter failed (Win32 {Marshal.GetLastWin32Error()}).";
                            return false;
                        }
                    }
                    finally
                    {
                        Marshal.FreeCoTaskMem(unmanaged);
                    }
                }
                finally
                {
                    EndPagePrinter(hPrinter);
                }
            }
            finally
            {
                EndDocPrinter(hPrinter);
            }
        }
        finally
        {
            ClosePrinter(hPrinter);
        }

        return true;
    }
}
