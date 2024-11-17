using System;

namespace UFPTool
{
    [Flags]
    public enum FlashFlags : uint
    {
        Normal = 0U,
        SkipPlatformIDCheck = 1U,
        SkipSignatureCheck = 2U,
        SkipHash = 4U,
        VerifyWrite = 8U,
        SkipWrite = 16U,
        ForceSynchronousWrite = 32U,
        FlashToRAM = 64U
    }
}