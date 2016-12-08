
namespace ISCSI
{
    public enum RejectReason : byte
    {
        Reserved = 0x01,
        DataDigestError = 0x02,
        SnackReject = 0x03,
        ProtocolError = 0x04,
        CommandNotSupported = 0x05,
        ImmediateCommandReject = 0x06, // too many immediate commands
        TaskInProgress = 0x07,
        InvalidDataAck = 0x08,
        InvalidPDUField = 0x09,
        LongOperationReject = 0x0A, // Can't generate Target Transfer Tag - out of resources
        NegotiationReset = 0x0B,
        WaitingforLogout = 0x0C,
    }
}
