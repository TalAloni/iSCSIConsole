
namespace ISCSI
{
    public enum ISCSIOpCodeName : byte
    {
        NOPOut = 0x00,
        SCSICommand = 0x01,
        //SCSITaskManagementFunctionRequest = 0x02,
        LoginRequest = 0x03,
        TextRequest = 0x04,
        SCSIDataOut = 0x05,
        LogoutRequest = 0x06,
        NOPIn = 0x20,
        SCSIResponse = 0x21,
        //SCSITaskManagementFunctionResponse = 0x22,
        LoginResponse = 0x23,
        TextResponse = 0x24,
        SCSIDataIn = 0x25,
        LogoutResponse = 0x26,
        ReadyToTransfer = 0x31,
        //AsynchronousMessage = 0x32,
        Reject = 0x3F,
    }
}
