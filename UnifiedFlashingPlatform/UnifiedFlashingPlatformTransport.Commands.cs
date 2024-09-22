namespace UnifiedFlashingPlatform
{
    public partial class UnifiedFlashingPlatformTransport
    {
        //
        // Not valid commands
        //
        /* NOK    */
        private const string Signature = "NOK";
        /* NOKX   */
        private const string ExtendedMessageSignature = $"{Signature}X";
        /* NOKXC  */
        private const string CommonExtendedMessageSignature = $"{ExtendedMessageSignature}C";
        /* NOKXF  */
        private const string UFPExtendedMessageSignature = $"{ExtendedMessageSignature}F";

        //
        // Normal commands
        //
        /* NOKF   */
        private const string FlashSignature = $"{Signature}F";
        /* NOKI   */
        private const string HelloSignature = $"{Signature}I";
        /* NOKM   */
        private const string MassStorageSignature = $"{Signature}M";
        /* NOKN   */
        private const string TelemetryEndSignature = $"{Signature}N";
        /* NOKR   */
        private const string RebootSignature = $"{Signature}R";
        /* NOKS   */
        private const string TelemetryStartSignature = $"{Signature}S";
        /* NOKT   */
        private const string GetGPTSignature = $"{Signature}T";
        /* NOKV   */
        private const string InfoQuerySignature = $"{Signature}V";
        /* NOKZ   */
        private const string ShutdownSignature = $"{Signature}Z";

        //
        // Common extended commands
        //
        /* NOKXCB */
        private const string SwitchModeSignature = $"{CommonExtendedMessageSignature}B";
        /* NOKXCC */
        private const string ClearScreenSignature = $"{CommonExtendedMessageSignature}C";
        /* NOKXCD */
        private const string GetDirectoryEntriesSignature = $"{CommonExtendedMessageSignature}D";
        /* NOKXCE */
        private const string EchoSignature = $"{CommonExtendedMessageSignature}E";
        /* NOKXCF */
        private const string GetFileSignature = $"{CommonExtendedMessageSignature}F";
        /* NOKXCM */
        private const string DisplayCustomMessageSignature = $"{CommonExtendedMessageSignature}M";
        /* NOKXCP */
        private const string PutFileSignature = $"{CommonExtendedMessageSignature}P";
        /* NOKXCT */
        private const string BenchmarkTestsSignature = $"{CommonExtendedMessageSignature}T";

        //
        // UFP extended commands
        //
        /* NOKXFF */
        private const string AsyncFlashModeSignature = $"{UFPExtendedMessageSignature}F";
        /* NOKXFI */
        private const string UnlockSignature = $"{UFPExtendedMessageSignature}I";
        /* NOKXFO */
        private const string RelockSignature = $"{UFPExtendedMessageSignature}O";
        /* NOKXFR */
        private const string ReadParamSignature = $"{UFPExtendedMessageSignature}R";
        /* NOKXFS */
        private const string SecureFlashSignature = $"{UFPExtendedMessageSignature}S";
        /* NOKXFT */
        private const string TelemetryReadSignature = $"{UFPExtendedMessageSignature}T";
        /* NOKXFW */
        private const string WriteParamSignature = $"{UFPExtendedMessageSignature}W";
        /* NOKXFX */
        private const string GetLogsSignature = $"{UFPExtendedMessageSignature}X";
    }
}
