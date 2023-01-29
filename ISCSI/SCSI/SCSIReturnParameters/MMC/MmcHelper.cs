namespace SCSI
{
    public static class MmcHelper
    {
        public static uint Lba2Msf(uint lba)
        {
            uint m, s, f;

            lba += 150;
            m = (lba / 75) / 60;
            s = (lba / 75) % 60;
            f = lba % 75;

            return ((m << 16) | (s << 8) | f);
        }
    }
}
