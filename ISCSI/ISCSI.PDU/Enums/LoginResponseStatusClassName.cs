
namespace ISCSI
{
    public enum LoginResponseStatusName : ushort
    {
        Success = 0x00,
        TargetMovedTemporarily = 0x101,
        TargetMovedPermanently = 0x102,
        InitiatorError = 0x200,
        AuthenticationFailure = 0x201,
        AuthorizationFailure = 0x202,
        NotFound = 0x203,
        TargetRemoved = 0x204,
        UnsupportedVersion = 0x205,
        TooManyConnections = 0x206,
        MissingParameter = 0x207,
        CanNotIncludeInSession = 0x208,
        SessionTypeNotSupported = 0x209,
        SessionDoesNotExist = 0x20a,
        InvalidDuringLogon = 0x20b,
        TargetError = 0x300,
        ServiceUnavailable = 0x301,
        OutOfResources = 0x302,
    }
}
