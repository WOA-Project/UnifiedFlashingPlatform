using System;
using System.Diagnostics;

namespace UnifiedFlashingPlatform
{
    public class ProgressUpdater
    {
        private readonly DateTime InitTime;
        private DateTime LastUpdateTime;
        private readonly ulong MaxValue;
        private readonly Action<int, TimeSpan?> ProgressUpdateCallback;
        public int ProgressPercentage;

        public ProgressUpdater(ulong MaxValue, Action<int, TimeSpan?> ProgressUpdateCallback)
        {
            InitTime = DateTime.Now;
            LastUpdateTime = DateTime.Now;
            this.MaxValue = MaxValue;
            this.ProgressUpdateCallback = ProgressUpdateCallback;
            SetProgress(0);
        }

        private ulong _Progress;
        public ulong Progress
        {
            get
            {
                return _Progress;
            }
        }

        public void SetProgress(ulong NewValue)
        {
            if (_Progress != NewValue)
            {
                int PreviousProgressPercentage = (int)((double)_Progress / MaxValue * 100);
                ProgressPercentage = (int)((double)NewValue / MaxValue * 100);

                _Progress = NewValue;

                if (((DateTime.Now - LastUpdateTime) > TimeSpan.FromSeconds(0.5)) || (ProgressPercentage == 100))
                {
#if DEBUG
                    Debug.WriteLine("Init time: " + InitTime.ToShortTimeString() + " / Now: " + DateTime.Now.ToString() + " / NewValue: " + NewValue.ToString() + " / MaxValue: " + MaxValue.ToString() + " ->> Percentage: " + ProgressPercentage.ToString() + " / Remaining: " + TimeSpan.FromTicks((long)((DateTime.Now - InitTime).Ticks / ((double)NewValue / MaxValue) * (1 - ((double)NewValue / MaxValue)))).ToString());
#endif

                    if (((DateTime.Now - InitTime) < TimeSpan.FromSeconds(30)) && (ProgressPercentage < 15))
                    {
                        ProgressUpdateCallback(ProgressPercentage, null);
                    }
                    else
                    {
                        ProgressUpdateCallback(ProgressPercentage, TimeSpan.FromTicks((long)((DateTime.Now - InitTime).Ticks / ((double)NewValue / MaxValue) * (1 - ((double)NewValue / MaxValue)))));
                    }

                    LastUpdateTime = DateTime.Now;
                }
            }
        }

        public void IncreaseProgress(ulong Progress)
        {
            SetProgress(_Progress + Progress);
        }
    }
}
