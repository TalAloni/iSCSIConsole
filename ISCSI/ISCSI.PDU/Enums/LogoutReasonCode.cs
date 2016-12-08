
namespace ISCSI
{
    public enum LogoutReasonCode : byte
    {
        CloseTheSession = 0,
        CloseTheConnection = 1,
        RemoveTheConnectionForRecovery = 2,
    }
}
