
namespace SCSI
{
    public enum SCSIOpCodeName : byte
    {
        TestUnitReady = 0x00,
        RequestSense = 0x03,
        Read6 = 0x08,
        Write6 = 0x0A,
        Inquiry = 0x12,
        ModeSelect6 = 0x15,
        Reserve6 = 0x16,
        Release6 = 0x17,
        ModeSense6 = 0x1A,
        ReadCapacity10 = 0x25,
        Read10 = 0x28,
        Write10 = 0x2A,
        Verify10 = 0x2F,
        WriteBuffer = 0x3B,
        ReadBuffer = 0x3C,
        SynchronizeCache10 = 0x35,
        WriteSame10 = 0x41,
        ModeSelect10 = 0x15,
        ModeSense10 = 0x5A,
        PersistentReserveIn = 0x5E,
        PersistentReserveOut = 0x5F,
        Read16 = 0x88,
        Write16 = 0x8A,
        Verify16 = 0x8F,
        WriteSame16 = 0x93,
        ServiceActionIn = 0x9E,
        ReportLUNs = 0xA0,
    }
}
