namespace UnifiedFlashingPlatform
{
    public partial class UnifiedFlashingPlatformTransport
    {
        //
        // Not valid commands
        //
        /* NOK    */
        private static string Signature => "NOK";
        /* NOKX   */
        private static string ExtendedMessageSignature => $"{Signature}X";
        /* NOKXC  */
        private static string CommonExtendedMessageSignature => $"{ExtendedMessageSignature}C";
        /* NOKXF  */
        private static string UFPExtendedMessageSignature => $"{ExtendedMessageSignature}F";

        //
        // Normal commands
        //
        /* NOKF   */
        private static string FlashSignature => $"{Signature}F";
        /* NOKI   */
        private static string HelloSignature => $"{Signature}I";
        /* NOKM   */
        private static string MassStorageSignature => $"{Signature}M";
        /* NOKN   */
        private static string TelemetryEndSignature => $"{Signature}N";
        /* NOKR   */
        private static string RebootSignature => $"{Signature}R";
        /* NOKS   */
        private static string TelemetryStartSignature => $"{Signature}S";
        /* NOKT   */
        private static string GetGPTSignature => $"{Signature}T";
        /* NOKV   */
        private static string InfoQuerySignature => $"{Signature}V";
        /* NOKZ   */
        private static string ShutdownSignature => $"{Signature}Z";

        //
        // Common extended commands
        //
        /* NOKXCB */
        private static string SwitchModeSignature => $"{CommonExtendedMessageSignature}B";
        /* NOKXCC */
        private static string ClearScreenSignature => $"{CommonExtendedMessageSignature}C";
        /* NOKXCD */
        private static string GetDirectoryEntriesSignature => $"{CommonExtendedMessageSignature}D";
        /* NOKXCE */
        private static string EchoSignature => $"{CommonExtendedMessageSignature}E";
        /* NOKXCF */
        private static string GetFileSignature => $"{CommonExtendedMessageSignature}F";
        /* NOKXCM */
        private static string DisplayCustomMessageSignature => $"{CommonExtendedMessageSignature}M";
        /* NOKXCP */
        private static string PutFileSignature => $"{CommonExtendedMessageSignature}P";
        /* NOKXCT */
        private static string BenchmarkTestsSignature => $"{CommonExtendedMessageSignature}T";

        //
        // UFP extended commands
        //
        /* NOKXFF */
        private static string AsyncFlashModeSignature => $"{UFPExtendedMessageSignature}F";
        /* NOKXFI */
        private static string UnlockSignature => $"{UFPExtendedMessageSignature}I";
        /* NOKXFO */
        private static string RelockSignature => $"{UFPExtendedMessageSignature}O";
        /* NOKXFR */
        private static string ReadParamSignature => $"{UFPExtendedMessageSignature}R";
        /* NOKXFS */
        private static string SecureFlashSignature => $"{UFPExtendedMessageSignature}S";
        /* NOKXFT */
        private static string TelemetryReadSignature => $"{UFPExtendedMessageSignature}T";
        /* NOKXFW */
        private static string WriteParamSignature => $"{UFPExtendedMessageSignature}W";
        /* NOKXFX */
        private static string GetLogsSignature => $"{UFPExtendedMessageSignature}X";
    }
}
