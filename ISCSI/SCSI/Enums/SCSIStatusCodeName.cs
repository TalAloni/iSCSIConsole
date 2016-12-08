
namespace SCSI
{
    public enum SCSIStatusCodeName : byte
    {
        Good = 0x00,
        CheckCondition = 0x02,
        ConditionMet = 0x04,
        Busy = 0x08,
        // ReservationConflict = 0x18,
        // TaskSetFull = 0x28,
        // ACAActive = 0x30,
        TaskAborted = 0x40,
    }
}
